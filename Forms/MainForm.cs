using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NanoLogViewer.Models;
using NanoMigratorLibrary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NanoLogViewer.Forms
{
	public partial class MainForm : Form
	{
		readonly SynchronizationContext synchronizationContext;
		readonly BackgroundThread backgroundThread = new BackgroundThread();

		public MainForm()
		{
			synchronizationContext = SynchronizationContext.Current;

			InitializeComponent();

			var args = Environment.GetCommandLineArgs();
			if (args.Length == 2)
			{
				tbSource.Text = args[1];
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
				tbSource.Text = openLogFileDialog.FileName;
				btUpdate_Click(null, null);
			}
		}

		private void btUpdate_Click(object sender, EventArgs e)
		{
			btUpdate.Enabled = false;
			Task.Run(() =>
			{
				var text = "";

				var uri = tbSource.Text.Trim();
				if (uri != "")
				{
					if (uri.StartsWith("http://") || uri.StartsWith("https://"))
					{
						using (var client = new WebClient())
						{
							text = client.DownloadString(uri);
						}
					}
					else
					{
						text = File.ReadAllText(uri);
					}
				}

				runInFormThread(() =>
				{
					parse(text);
					btUpdate.Enabled = true;
				});
			});
		}

		void parse(string text)
		{
			text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

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

				var item = new ListViewItem();
				item.Tag = obj;
				foreach (ColumnHeader col in lvLogLines.Columns)
				{
					item.SubItems.Add(obj[col.Name]?.ToString() ?? "");
				}
				lvLogLines.Items.Add(item);
			}
		}

		private void lvLogLines_SelectedIndexChanged(object sender, EventArgs e)
		{
			var item = lvLogLines.SelectedItems.Count == 1 ? lvLogLines.SelectedItems[0] : null;

			wbDetails.DocumentText = item != null ? itemToString(item) : "";
		}

		string itemToString(ListViewItem item)
		{
			JObject obj = (JObject)item.Tag;

			var s = "";
			foreach (var prop in obj)
			{
				var value = prop.Value.ToString().Replace("\r\n", "\n").Replace("\r", "\n").Trim();
				s += prop.Key + ": " + (value.Contains("\n") ? "\n\t" + value.Replace("\n", "\n\t") : value) + "\n";
			}
			return "<pre>" + s.Trim() + "</pre>";
		}
	}
}
