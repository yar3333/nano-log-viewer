using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NanoLogViewer.Models;
using Newtonsoft.Json.Linq;

namespace NanoLogViewer.Forms
{
	public partial class MainForm : Form
	{
		readonly SynchronizationContext synchronizationContext;
		readonly BackgroundThread backgroundThread = new BackgroundThread();

        string sourcesIniFileName => Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "\\sources.ini";
        string columnsIniFileName => Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "\\columns.ini";

        readonly Dictionary<string, int> columnWidths = new Dictionary<string, int>();

		public MainForm()
		{
			synchronizationContext = SynchronizationContext.Current;

			InitializeComponent();

            if (File.Exists(sourcesIniFileName))
            {
                foreach (var source in File.ReadAllLines(sourcesIniFileName))
                {
                    if (!string.IsNullOrWhiteSpace(source)) cbSource.Items.Add(source);
                }
            }

            if (File.Exists(columnsIniFileName))
            {
                foreach (var column in File.ReadAllLines(columnsIniFileName))
                {
                    if (!string.IsNullOrWhiteSpace(column))
                    {
                        columnWidths[column.Split(':')[0]] = int.Parse(column.Split(':')[1]);
                    }
                }
            }

            var args = Environment.GetCommandLineArgs();
			if (args.Length == 2)
			{
				cbSource.Text = args[1];
				btUpdate_Click(null, null);
			}
        }

        private void runInFormThread(Action work)
		{
			synchronizationContext.Post(_ => work(), null);
		}

		private void btSelectSourceFile_Click(object sender, EventArgs e)
		{
			if (openLogFileDialog.ShowDialog(this) == DialogResult.OK)
			{
				cbSource.Text = openLogFileDialog.FileName;
				btUpdate_Click(null, null);
			}
		}

		private void btUpdate_Click(object sender, EventArgs e)
		{
			btUpdate.Enabled = false;

			var uri = cbSource.Text.Trim();
			if (uri != "")
			{
                var has = false;
                for (var i = 0; i < cbSource.Items.Count; i++)
                {
                    if (cbSource.GetItemText(cbSource.Items[i]) == uri) { has = true; break; }
                }
                if (!has) cbSource.Items.Add(uri);

                if (uri.StartsWith("http://") || uri.StartsWith("https://"))
				{
                    Task.Run(() =>
                    {
                        try
                        {
                            //ServicePointManager.Expect100Continue = true;
                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(delegate { return true; });
                            using (var client = new WebClient())
                            {
                                var parsedUri = new Uri(uri);
                                if (!string.IsNullOrEmpty(parsedUri.UserInfo))
                                {
                                    string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(parsedUri.UserInfo));
                                    client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;

                                }

                                var text = client.DownloadString(uri);
                                runInFormThread(() =>
                                {
                                    parse(text);
                                    btUpdate.Enabled = true;
                                });
                            }
                        }
                        catch (Exception ee)
                        {
                            runInFormThread(() => MessageBox.Show(this, ee.Message, "Downloading error", MessageBoxButtons.OK));
                        }
                    });
                }
                else
				{
                    parse(File.ReadAllText(uri));
                    btUpdate.Enabled = true;
                }
            }
		}

		void parse(string text)
		{
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

            for (var i = 0; i < lvLogLines.Columns.Count; i++)
            {
                columnWidths[lvLogLines.Columns[i].Name] = lvLogLines.Columns[i].Width;
            }

            lvLogLines.Clear();

			var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var json in lines)
			{
				var obj = JObject.Parse(json);

				foreach (var prop in obj)
				{
					if (!lvLogLines.Columns.ContainsKey(prop.Key))
					{
						lvLogLines.Columns.Add(prop.Key, prop.Key);
					}
				}

			}

			foreach (var json in lines)
			{
				var obj = JObject.Parse(json);

                var subItems = new List<string>();
                foreach (ColumnHeader col in lvLogLines.Columns)
				{
                    subItems.Add(obj[col.Name]?.ToString() ?? "");
				}

                var item = new ListViewItem(subItems.ToArray());
                item.Tag = obj;
                lvLogLines.Items.Add(item);
			}

            for (var i = 0; i < lvLogLines.Columns.Count; i++)
            {
                if (columnWidths.ContainsKey(lvLogLines.Columns[i].Name)) lvLogLines.Columns[i].Width = columnWidths[lvLogLines.Columns[i].Name];
            }
        }

        private void lvLogLines_SelectedIndexChanged(object sender, EventArgs e)
		{
			var item = lvLogLines.SelectedItems.Count == 1 ? lvLogLines.SelectedItems[0] : null;

			wbDetails.DocumentText = item != null ? "<pre>" + jobjectToString((JObject)item.Tag) + "</pre>" : "";
		}

		string jobjectToString(JObject obj)
		{
			var s = "";
			foreach (var prop in obj)
			{
                if (prop.Value is JObject)
                {
                    s += prop.Key + ":\n\t" + jobjectToString((JObject)prop.Value).Replace("\n", "\n\t") + "\n";
                }
                else
                {
                    var value = prop.Value.ToString().Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                    s += prop.Key + ": " + (value.Contains("\n") ? "\n\t" + value.Replace("\n", "\n\t") : value) + "\n";
                }
            }
			return s.Trim();
		}

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            var sources = new List<string>();
            for (var i = 0; i < cbSource.Items.Count; i++)
            {
                sources.Add(cbSource.GetItemText(cbSource.Items[i]));
            }
            File.WriteAllLines(sourcesIniFileName, sources);

            var columns = new List<string>();
            foreach (var kv in columnWidths)
            {
                columns.Add(kv.Key + ":" + kv.Value);
            }
            File.WriteAllLines(columnsIniFileName, columns);
        }
    }
}
