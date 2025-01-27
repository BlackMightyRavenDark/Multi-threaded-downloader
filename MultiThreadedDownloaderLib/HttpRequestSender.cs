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

				HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
				int resultErrorCode = (int)response.StatusCode;
				WebContent webContent = resultErrorCode == 200 || resultErrorCode == 206 ?
					new WebContent(response.GetResponseStream(), response.ContentLength) : null;
				return new HttpRequestResult(resultErrorCode, response.StatusDescription, response, webContent);
			}
			catch (System.Exception ex)
			{
				int errorCode;
				if (ex is WebException && (ex as WebException).Status == WebExceptionStatus.ProtocolError)
				{
					HttpWebResponse response = (ex as WebException).Response as HttpWebResponse;
					errorCode = (int)response.StatusCode;
					WebContent webContent = new WebContent(response.GetResponseStream(), response.ContentLength);
					return new HttpRequestResult(errorCode, response.StatusDescription, response, webContent);
				}

				errorCode = ex.HResult;
				string errorMessage = ex.Message;
				return new HttpRequestResult(errorCode, errorMessage, null, null);
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
	}
}
