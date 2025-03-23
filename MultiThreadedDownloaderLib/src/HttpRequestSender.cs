using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using static MultiThreadedDownloaderLib.Utils;

namespace MultiThreadedDownloaderLib
{
	public static class HttpRequestSender
	{
		public static HttpRequestResult Send(string method, string url,
			Stream body, NameValueCollection headers, int timeout, bool sendExpect100ContinueHeader = false)
		{
			HttpWebResponse response = null;
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
				httpWebRequest.Method = method;
				httpWebRequest.ServicePoint.Expect100Continue = sendExpect100ContinueHeader;
				if (timeout >= 500)
				{
					httpWebRequest.Timeout = timeout;
				}

				if (headers != null && headers.Count > 0)
				{
					SetRequestHeaders(httpWebRequest, headers);
				}

				bool canSendBody = method == "POST" || method == "PUT";
				if (canSendBody)
				{
					if (body != null && body.Length > 0L)
					{
						long contentLength = body.Length - body.Position;
						httpWebRequest.ContentLength = contentLength > 0L ? contentLength : 0L;
						if (httpWebRequest.ContentLength > 0L)
						{
							using (Stream requestStream = httpWebRequest.GetRequestStream())
							{
								byte[] buffer = new byte[4096];
								while (true)
								{
									int bytesRead = body.Read(buffer, 0, buffer.Length);
									if (bytesRead <= 0) { break; }
									requestStream.Write(buffer, 0, bytesRead);
								}
							}
						}
					}
					else
					{
						httpWebRequest.ContentLength = 0L;
					}
				}

				response = (HttpWebResponse)httpWebRequest.GetResponse();
				int resultErrorCode = (int)response.StatusCode;
				WebContent webContent = null;
				if (resultErrorCode == 200 || resultErrorCode == 206)
				{
					string contentEncodingHeaderValue = response.GetContentEncodingHeaderValue();
					webContent = new WebContent(response.GetResponseStream(), response.ContentLength, contentEncodingHeaderValue);
				}

				return CreateRequestResult(resultErrorCode, response.StatusDescription, response, webContent);
			}
			catch (System.Exception ex)
			{
				response?.Close();
				if (ex is WebException && (ex as WebException).Status == WebExceptionStatus.ProtocolError &&
					((ex as WebException).Response is HttpWebResponse responseException))
				{
					int errorCode = (int)responseException.StatusCode;
					WebContent webContent = new WebContent(responseException.GetResponseStream(), responseException.ContentLength);
					return CreateRequestResult(errorCode, responseException.StatusDescription, responseException, webContent);
				}

				return CreateRequestResult(ex.HResult, ex.Message, null, null);
			}
		}

		public static HttpRequestResult Send(HttpRequestSenderParameters parameters)
		{
			return Send(parameters.Method, parameters.Url, parameters.Body, parameters.Headers,
				parameters.Timeout, parameters.SendExpect100ContinueHeader);
		}

		public static HttpRequestResult Send(string method, string url,
			Stream body, NameValueCollection headers, int timeout = 0)
		{
			HttpRequestSenderParameters parameters = new HttpRequestSenderParameters()
			{
				Method = method,
				Url = url,
				Body = body,
				Headers = headers,
				Timeout = timeout,
				SendExpect100ContinueHeader = false
			};
			return Send(parameters);
		}

		public static HttpRequestResult Send(string method, string url,
			byte[] body, NameValueCollection headers, int timeout = 0)
		{
			Stream bodyStream = body?.ToStream(true);
			HttpRequestResult result = Send(method, url, bodyStream, headers, timeout);
			bodyStream?.Dispose();
			return result;
		}

		public static HttpRequestResult Send(string method, string url,
			string body, Encoding bodyEncoding, NameValueCollection headers, int timeout = 0)
		{
			byte[] bodyBytes = !string.IsNullOrEmpty(body) && bodyEncoding != null ? bodyEncoding.GetBytes(body) : null;
			return Send(method, url, bodyBytes, headers, timeout);
		}

		public static HttpRequestResult Send(string method, string url,
			NameValueCollection headers, int timeout = 0)
		{
			return Send(method, url, (Stream)null, headers, timeout);
		}

		public static HttpRequestResult Send(string method, string url, int timeout = 0)
		{
			return Send(method, url, null, timeout);
		}

		public static HttpRequestResult Send(string url, int timeout = 0)
		{
			return Send("GET", url, timeout);
		}

		private static HttpRequestResult CreateRequestResult(int errorCode, string errorMessage,
			HttpWebResponse httpWebResponse, WebContent webContent)
		{
			HttpRequestResult result = new HttpRequestResult(errorCode, errorMessage, httpWebResponse, webContent);
			if (httpWebResponse != null)
			{
				Utils.CombineHeaders(httpWebResponse, result.Headers);
			}
			return result;
		}
	}
}
