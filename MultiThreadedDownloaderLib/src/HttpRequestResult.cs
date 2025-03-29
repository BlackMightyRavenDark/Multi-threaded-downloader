using System;
using System.Collections.Specialized;
using System.IO;
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
		public bool IsExceptionRaised { get; }
		public WebContent WebContent { get; private set; }

		public HttpRequestResult(int errorCode, string errorMessage,
			HttpWebResponse httpWebResponse, bool exceptionWasRaised)
		{
			ErrorCode = errorCode;
			ErrorMessage = errorMessage;
			HttpWebResponse = httpWebResponse;
			IsExceptionRaised = exceptionWasRaised;
			Headers = new NameValueCollection();
		}

		public void Dispose()
		{
			DisposeContent();
			if (HttpWebResponse != null)
			{
				HttpWebResponse.Close();
				HttpWebResponse = null;
			}
		}

		public void DisposeContent()
		{
			if (WebContent != null)
			{
				WebContent.Dispose();
				WebContent = null;
			}
		}

		public int GetContent(out string errorMessage)
		{
			if (WebContent != null) { errorMessage = null; return 200; }

			if (HttpWebResponse != null)
			{
				try
				{
					Stream stream = HttpWebResponse.GetResponseStream();
					string encoding = Headers.Get("Content-Encoding");
					Utils.ExtractContentLengthFromHeaders(Headers, out long contentLength);
					WebContent = new WebContent(stream, contentLength, encoding);
					errorMessage = null;
					return 200;
				}
				catch (Exception ex)
				{
#if DEBUG
					System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
					WebContent = null;
					errorMessage = ex.Message;
					return ex.HResult;
				}
			}

			WebContent = null;
			errorMessage = null;
			return 404;
		}

		private bool HasErrorMessageText()
		{
			return !string.IsNullOrEmpty(ErrorMessage) && !string.IsNullOrWhiteSpace(ErrorMessage) &&
				string.Compare(ErrorMessage, "ok", true) != 0;
		}
	}
}
