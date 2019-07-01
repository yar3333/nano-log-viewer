using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        readonly Dictionary<string, int> columnIndexes = new Dictionary<string, int>();

        bool noRemeberColumns = false;

        public MainForm()
		{
			synchronizationContext = SynchronizationContext.Current;

			InitializeComponent();

            wbDetails.Navigate("about:blank");

            if (File.Exists(sourcesIniFileName))
            {
                foreach (var source in File.ReadAllLines(sourcesIniFileName))
                {
                    if (!string.IsNullOrWhiteSpace(source)) cbSource.Items.Add(source);
                }
            }

            if (File.Exists(columnsIniFileName))
            {
                var i = 0;
                foreach (var column in File.ReadAllLines(columnsIniFileName))
                {
                    if (!string.IsNullOrWhiteSpace(column))
                    {
                        columnWidths[column.Split(':')[0]] = int.Parse(column.Split(':')[1]);
                        columnIndexes[column.Split(':')[0]] = i; i++;
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
            var urlHistory = new List<string>();
            for (var i = 0; i < cbSource.Items.Count; i++)
            {
                urlHistory.Add(cbSource.GetItemText(cbSource.Items[i]));
            }

            updateInner(cbSource.Text.Trim(), urlHistory);
        }

        private void updateInner(string uri, List<string> urlHistory)
		{
            btUpdate.Enabled = false;
            var isLoadTail = cbTailMB.Checked;

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
                            var size = isLoadTail ? HttpTools.getSize(uri) : null;
                            var text = HttpTools.download(uri, size != null ? (int?)Math.Max(0, size.Value - 1024*1024) : null);
                            runInFormThread(() =>
                            {
                                parse(text);
                                btUpdate.Enabled = true;
                                cbSource.Text = uri;
                            });
                        }
                        catch (Exception ee)
                        {
                            if (ee is WebException)
                            {
                                var response = ((WebException)ee).Response as HttpWebResponse;
                                if (response?.StatusCode == HttpStatusCode.Unauthorized)
                                {
                                    var parsedUri = new Uri(uri);
                                    if (parsedUri.UserInfo == "")
                                    {
                                        foreach (var h in urlHistory)
                                        {
                                            if (h.StartsWith("http://") || h.StartsWith("https://"))
                                            {
                                                var pu = new Uri(h);
                                                if (pu.Host == parsedUri.Host && pu.UserInfo != "")
                                                {
                                                    runInFormThread(() => updateInner(parsedUri.Scheme + "://" + pu.UserInfo + "@" + parsedUri.Host + parsedUri.PathAndQuery, new List<string>()));
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            runInFormThread(() =>
                            {
                                MessageBox.Show(this, ee.Message, "Downloading error", MessageBoxButtons.OK);
                                btUpdate.Enabled = true;
                            });
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
            noRemeberColumns = true;

            wbDetails.DocumentText = "";

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            lvLogLines.Clear();

            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var json = line.Trim();
                if (!json.StartsWith("{")) continue;

                var obj = JObject.Parse(json);

                foreach (var prop in obj)
                {
                    if (!lvLogLines.Columns.ContainsKey(prop.Key))
                    {
                        lvLogLines.Columns.Add(prop.Key, prop.Key);
                    }
                }
            }

            foreach (var line in lines)
            {
                var json = line.Trim();
                if (!json.StartsWith("{")) continue;

                var obj = JObject.Parse(json);

                var subItems = new List<string>();
                foreach (ColumnHeader col in lvLogLines.Columns)
                {
                    subItems.Add(jtokenToString(obj[col.Name]));
                }

                var item = new ListViewItem(subItems.ToArray());
                item.Tag = obj;
                lvLogLines.Items.Add(item);
            }

            var columns = new List<ColumnHeader>();
            foreach (ColumnHeader col in lvLogLines.Columns) columns.Add(col);

            var sortedColumns = columns.Select(col => Tuple.Create(col, columnWidths.ContainsKey(col.Name) ? columnIndexes[col.Name] : 1000))
                                       .OrderBy(x => x.Item2).ThenBy(x => x.Item1.Name)
                                       .ToArray();

            for (var i = 0; i < sortedColumns.Length; i++)
            {
                var col = sortedColumns[i].Item1;
                if (columnWidths.ContainsKey(col.Name))
                {
                    col.Width = columnWidths[col.Name];
                }
                else
                {
                    columnWidths[col.Name] = col.Width;
                    columnIndexes[col.Name] = sortedColumns[i].Item2;
                }
                col.DisplayIndex = i;
            }

            if (lvLogLines.Items.Count > 0)
            {
                lvLogLines.EnsureVisible(lvLogLines.Items.Count - 1);
                lvLogLines.Items[lvLogLines.Items.Count - 1].Selected = true;
            }

            noRemeberColumns = false;

            lvLogLines.Refresh();
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
                    var value = jtokenToString(prop.Value).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                    s += prop.Key + ": " + (value.Contains("\n") ? "\n\t" + value.Replace("\n", "\n\t") : value) + "\n";
                }
            }
			return s.Trim();
		}

        private string jtokenToString(JToken value)
        {
            if (value == null) return "";

            if (value.Type == JTokenType.Date)
            {
                var dt = ((DateTime)value).ToLocalTime();
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            }

            return value.ToString() ?? "";
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            var sources = new List<string>();
            for (var i = Math.Max(0, cbSource.Items.Count - 10); i < cbSource.Items.Count; i++)
            {
                sources.Add(cbSource.GetItemText(cbSource.Items[i]));
            }
            File.WriteAllLines(sourcesIniFileName, sources);

            rememberListState();

            var columns = new List<string>();
            foreach (var key in columnIndexes.OrderBy(x => x.Value).Select(x => x.Key))
            {
                columns.Add(key + ":" + columnWidths[key]);
            }
            File.WriteAllLines(columnsIniFileName, columns);
        }

        private void LvLogLines_ColumnReordered(object sender, ColumnReorderedEventArgs e)
        {
            rememberListState();
        }

        private void LvLogLines_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            rememberListState();
        }

        private void rememberListState()
        {
            if (noRemeberColumns) return;

            foreach (ColumnHeader col in lvLogLines.Columns)
            {
                columnWidths[col.Name] = col.Width;
                columnIndexes[col.Name] = col.DisplayIndex;
            }
        }

        private void CbSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            btUpdate_Click(null, null);
        }
    }
}
