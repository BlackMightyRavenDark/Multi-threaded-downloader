using System;
using System.Collections.Specialized;
using System.Net;

namespace MultiThreadedDownloaderLib
{
	public class HttpRequestResult : IDisposable
	{
		public int ErrorCode { get; }
		public string ErrorMessage { get; }
		public NameValueCollection Headers { get; }
		public DateTime LastModifiedDate => HttpWebResponse != null ? HttpWebResponse.LastModified : DateTime.MinValue;
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
			Headers = new NameValueCollection();
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

		private bool HasErrorMessageText()
		{
			return !string.IsNullOrEmpty(ErrorMessage) && !string.IsNullOrWhiteSpace(ErrorMessage) &&
				string.Compare(ErrorMessage, "ok", true) != 0;
		}
	}
}
