using System;
using System.Net;
using System.Windows.Forms;

namespace MultiThreadedDownloaderLib.RequestsTest
{
	public partial class FormProxyEditor : Form
	{
		public WebProxy Proxy { get; private set; }

		public FormProxyEditor(WebProxy proxy)
		{
			InitializeComponent();
			Proxy = proxy;

			numericUpDownPort.Maximum = ushort.MaxValue;
			if (proxy != null)
			{
				textBoxAddress.Text = proxy.Address.Host;
				int min = (int)numericUpDownPort.Minimum;
				int max = (int)numericUpDownPort.Maximum;
				if (proxy.Address.Port >= min && proxy.Address.Port <= max)
				{
					numericUpDownPort.Value = proxy.Address.Port;
				}
			}
		}

		public FormProxyEditor(string address, ushort port) : this(new WebProxy(address, port)) { }

		private void btnOk_Click(object sender, EventArgs e)
		{
			string address = textBoxAddress.Text;
			try
			{
				Proxy = string.IsNullOrEmpty(address) || string.IsNullOrWhiteSpace(address) ? null :
					new WebProxy(textBoxAddress.Text, (int)numericUpDownPort.Value);
			}
			catch (Exception ex)
			{
				string msg = "Адрес прокси-сервера указан неправильно!";
				MessageBox.Show($"{msg}\n{ex.Message}", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				textBoxAddress.Focus();
				return;
			}

			DialogResult = DialogResult.OK;
			Close();
		}
	}
}
