using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace MultiThreadedDownloaderLib.RequestsTest
{
	public partial class FormCookieEditor : Form
	{
		public CookieCollection Cookies { get; private set; }

		public FormCookieEditor()
		{
			InitializeComponent();
		}

		private void btnLoadFromFile_Click(object sender, EventArgs e)
		{
			try
			{
				using (OpenFileDialog ofd = new OpenFileDialog())
				{
					ofd.Filter = "JSON-files|*.json";
					if (ofd.ShowDialog() == DialogResult.OK)
					{
						listViewCookies.Items.Clear();
						string content = File.ReadAllText(ofd.FileName);
						Cookies = ParseCookies(content);
					}
				}
			} catch (Exception ex)
			{
				MessageBox.Show($"Cookies error!\n{ex.Message}", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				Cookies = null;
			}

			DisplayCookies();
		}

		private void btnClear_Click(object sender, EventArgs e)
		{
			listViewCookies.Items.Clear();
			Cookies = null;
			toolStripStatusLabel1.Text = "No cookies loaded";
		}

		private static CookieCollection ParseCookies(string jsonString)
		{
			CookieCollection collection = new CookieCollection();
			JArray jsonArr = JArray.Parse(jsonString);
			foreach (JObject jCookie in jsonArr)
			{
				string domain = jCookie.Value<string>("domain");
				string path = jCookie.Value<string>("path");
				string name = jCookie.Value<string>("name");
				string value = jCookie.Value<string>("value");

				Cookie cookie = new Cookie(name, value, path, domain);
				collection.Add(cookie);
			}
			return collection;
		}

		private void DisplayCookies()
		{
			listViewCookies.Items.Clear();
			if (Cookies != null)
			{
				foreach (Cookie cookie in Cookies)
				{
					ListViewItem item = new ListViewItem(cookie.Domain);
					string[] subItems = new string[] { cookie.Path, cookie.Name, cookie.Value };
					item.SubItems.AddRange(subItems);
					listViewCookies.Items.Add(item);
				}
			}
			DisplayCookieCount();
		}

		private void DisplayCookieCount()
		{
			string s = Cookies == null || Cookies.Count == 0 ? "No cookies" :
				(Cookies.Count > 1 ? $"{Cookies.Count} cookies" : "1 cookie");
			toolStripStatusLabel1.Text = $"{s} loaded";
		}
	}
}
