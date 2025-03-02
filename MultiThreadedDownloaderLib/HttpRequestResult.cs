using System;
using System.Net;

namespace MultiThreadedDownloaderLib
{
	public class HttpRequestResult : IDisposable
	{
		public int ErrorCode { get; }
		public string ErrorMessage { get; }
		public bool HasErrorMessage => HasErrorMessageText();
		public HttpWebResponse HttpWebResponse { get; private set; }
		public WebContent WebContent { get; private set; }

		public HttpRequestResult(int errorCode, string errorMessage,
			HttpWebResponse httpWebResponse, WebContent webContent)
		{
			ErrorCode = errorCode;
			ErrorMessage = errorMessage;
			HttpWebResponse = httpWebResponse;
			WebContent = webContent;
		}

		public void Dispose()
		{
			if (WebContent != null)
			{
				WebContent.Dispose();
				WebContent = null;
			}

			if (HttpWebResponse != null)
			{
				HttpWebResponse.Close();
				HttpWebResponse = null;
			}
		}

		public bool IsZippedContent()
		{
			if (HttpWebResponse != null)
			{
				string value = HttpWebResponse.Headers?.Get("Content-Encoding");
				if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
				{
					value = HttpWebResponse.ContentEncoding;
				}
				return !string.IsNullOrEmpty(value) && value.Contains("gzip");
			}
			return false;
		}

		private bool HasErrorMessageText()
		{
			return !string.IsNullOrEmpty(ErrorMessage) && !string.IsNullOrWhiteSpace(ErrorMessage) &&
				string.Compare(ErrorMessage, "ok", true) != 0;
		}
	}
}
