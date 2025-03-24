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
			btnProxy.Enabled = false;

			string requestUrl = textBoxRequestUrl.Text;
			if (string.IsNullOrEmpty(requestUrl) || string.IsNullOrWhiteSpace(requestUrl))
			{
				MessageBox.Show("Введите ссылку!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				btnSetRequestBody.Enabled = true;
				btnProxy.Enabled = true;
				btnSend.Enabled = true;
				return;
			}

			string requestType = textBoxRequestType.Text;
			if (string.IsNullOrEmpty(requestType) || string.IsNullOrWhiteSpace(requestType))
			{
				MessageBox.Show("Введите тип запроса!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				btnSetRequestBody.Enabled = true;
				btnProxy.Enabled = true;
				btnSend.Enabled = true;
				return;
			}

			lblStatusCode.Text = null;
			textBoxServerAnswer.Text = null;

			NameValueCollection headers = Utils.ParseHeaderList(textBoxRequestHeaders.Text);
			HttpRequestResult requestResult = await Task.Run(() =>
			{
				HttpRequestSenderParameters requestParameters = new HttpRequestSenderParameters()
				{
					Method = requestType,
					Url = requestUrl,
					Body = string.IsNullOrEmpty(_requestBody) ? null : Encoding.UTF8.GetBytes(_requestBody).ToStream(true),
					Headers = headers,
					Proxy = _proxy
				};
				return HttpRequestSender.Send(requestParameters);
			});
			lblStatusCode.Text = $"Код возврата: {requestResult.ErrorCode}";
			textBoxServerAnswer.Text = Utils.HeadersToString(requestResult.Headers);
			requestResult.Dispose();

			btnSetRequestBody.Enabled = true;
			btnProxy.Enabled = true;
			btnSend.Enabled = true;
		}

		private void btnSetRequestBody_Click(object sender, EventArgs e)
		{
			RequestBodyEditor editor = new RequestBodyEditor(_requestBody);
			if (editor.ShowDialog() == DialogResult.OK)
			{
				_requestBody = editor.BodyContent;
			}
		}

		private void btnProxy_Click(object sender, EventArgs e)
		{
			string address = _proxy != null ? _proxy.Address.Host : null;
			ushort port = (ushort)(_proxy != null ? _proxy.Address.Port : 1);
			FormProxyEditor proxyEditor = new FormProxyEditor(address, port);
			if (proxyEditor.ShowDialog() == DialogResult.OK)
			{
				_proxy = proxyEditor.Proxy;
			}
		}
	}
}
