using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiThreadedDownloaderLib.RequestsTest
{
	public partial class Form1 : Form
	{
		private string _requestBody = null;
		private CookieContainer _cookies;
		private WebProxy _proxy = null;

		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			lblStatusCode.Text = null;
		}

		private async void btnSend_Click(object sender, EventArgs e)
		{
			btnSend.Enabled = false;
			btnSetRequestBody.Enabled = false;
			btnSetRequestCookies.Enabled = false;
			btnSetRequestProxy.Enabled = false;

			string requestUrl = textBoxRequestUrl.Text;
			if (string.IsNullOrEmpty(requestUrl) || string.IsNullOrWhiteSpace(requestUrl))
			{
				MessageBox.Show("Введите ссылку!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				btnSetRequestBody.Enabled = true;
				btnSetRequestCookies.Enabled = true;
				btnSetRequestProxy.Enabled = true;
				btnSend.Enabled = true;
				return;
			}

			string requestMethod = textBoxRequestMethod.Text;
			if (string.IsNullOrEmpty(requestMethod) || string.IsNullOrWhiteSpace(requestMethod))
			{
				MessageBox.Show("Введите тип запроса!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				btnSetRequestBody.Enabled = true;
				btnSetRequestCookies.Enabled = true;
				btnSetRequestProxy.Enabled = true;
				btnSend.Enabled = true;
				return;
			}

			lblStatusCode.Text = null;
			textBoxServerAnswer.Text = null;

			NameValueCollection headers = Utils.ParseHeaderList(textBoxRequestHeaders.Text);
			HttpRequestResult requestResult = await Task.Run(() =>
			{
				byte[] body = !string.IsNullOrEmpty(_requestBody) ? Encoding.UTF8.GetBytes(_requestBody) : null;
				return HttpRequestSender.Send(requestMethod, requestUrl, body, headers, _cookies, _proxy);
			});
			lblStatusCode.Text = $"Код возврата: {requestResult.ErrorCode}";
			textBoxServerAnswer.Text = Utils.HeadersToString(requestResult.Headers);
			requestResult.Dispose();

			btnSetRequestBody.Enabled = true;
			btnSetRequestCookies.Enabled = true;
			btnSetRequestProxy.Enabled = true;
			btnSend.Enabled = true;
		}

		private void btnSetRequestBody_Click(object sender, EventArgs e)
		{
			FormRequestBodyEditor editor = new FormRequestBodyEditor(_requestBody);
			if (editor.ShowDialog() == DialogResult.OK)
			{
				_requestBody = editor.BodyContent;
			}
		}

		private void btnSetRequestProxy_Click(object sender, EventArgs e)
		{
			string address = _proxy != null ? _proxy.Address.Host : null;
			ushort port = (ushort)(_proxy != null ? _proxy.Address.Port : 1);
			FormProxyEditor proxyEditor = new FormProxyEditor(address, port);
			if (proxyEditor.ShowDialog() == DialogResult.OK)
			{
				_proxy = proxyEditor.Proxy;
			}
		}

		private void btnSetRequestCookies_Click(object sender, EventArgs e)
		{
			FormCookieEditor cookieEditor = new FormCookieEditor();
			if (cookieEditor.ShowDialog() == DialogResult.OK)
			{
				_cookies = new CookieContainer();
				foreach (Cookie cookie in cookieEditor.Cookies)
				{
					_cookies.Add(cookie);
				}
			}
		}
	}
}
