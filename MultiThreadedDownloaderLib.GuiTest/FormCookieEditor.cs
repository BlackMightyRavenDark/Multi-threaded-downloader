using System;
using System.Net;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace MultiThreadedDownloaderLib.GuiTest
{
	public partial class FormCookieEditor : Form
	{
		public CookieCollection Cookies;

		public FormCookieEditor()
		{
			InitializeComponent();
		}

		private void btnOk_Click(object sender, EventArgs e)
		{
			string jsonString = textBoxCookiesJson.Text;
			if (string.IsNullOrEmpty(jsonString) || string.IsNullOrWhiteSpace(jsonString))
			{
				Cookies = null;
				return;
			}

			try
			{
				Cookies = ParseCookies(jsonString);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Cookies error!\n{ex.Message}", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				Cookies = null;
			}
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
	}
}
