using System;
using System.IO;
using System.Net;
using System.Text;
using static MultiThreadedDownloaderLib.Utils;

namespace MultiThreadedDownloaderLib
{
	public static class HttpRequestSender
	{
		public static HttpRequestResult Send(string method, string url, Stream body,
			WebHeaderCollection headers, CookieContainer cookies, IWebProxy proxy,
			int timeout, bool sendExpect100ContinueHeader = false)
		{
			HttpWebResponse response = null;
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
				httpWebRequest.Method = method;
				httpWebRequest.CookieContainer = cookies;
				httpWebRequest.Proxy = proxy;
				httpWebRequest.ServicePoint.Expect100Continue = sendExpect100ContinueHeader;
				if (timeout >= 500)
				{
					httpWebRequest.Timeout = timeout;
				}

				if (headers != null && headers.Count > 0)
				{
					SetHttpRequestHeaders(httpWebRequest, headers);
				}

				bool canSendBody = method == "POST" || method == "PUT" || method == "PATCH";
				if (canSendBody)
				{
					if (body != null && body.Length > 0L)
					{
						long difference = body.Length - body.Position;
						long contentLength = difference > 0L ? difference : 0L;
						if (contentLength > 0L)
						{
							httpWebRequest.ContentLength = contentLength;

							using (Stream requestStream = httpWebRequest.GetRequestStream())
							{
								if (requestStream.CanWrite)
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
					}
					else
					{
						httpWebRequest.ContentLength = 0L;
					}
				}

				response = (HttpWebResponse)httpWebRequest.GetResponse();
				int resultErrorCode = (int)response.StatusCode;
				return CreateHttpRequestResult(resultErrorCode, response.StatusDescription, response);
			}
			catch (Exception ex)
			{
				response?.Close();
				if (ex is WebException && (ex as WebException).Status == WebExceptionStatus.ProtocolError &&
					((ex as WebException).Response is HttpWebResponse responseException))
				{
					int errorCode = (int)responseException.StatusCode;
					return CreateHttpRequestResult(errorCode, responseException.StatusDescription, responseException, true);
				}
				
				return CreateHttpRequestResult(ex.HResult, ex.Message, null, true);
			}
		}

		public static HttpRequestResult Send(HttpRequestSenderParameters parameters)
		{
			return Send(parameters.Method, parameters.Url, parameters.Body,
				parameters.Headers, parameters.Cookies, parameters.Proxy,
				parameters.Timeout, parameters.SendExpect100ContinueHeader);
		}

		public static HttpRequestResult Send(string method, string url, Stream body,
			CookieContainer cookies, WebHeaderCollection headers, IWebProxy proxy, int timeout = 0)
		{
			HttpRequestSenderParameters parameters = new HttpRequestSenderParameters()
			{
				Method = method,
				Url = url,
				Body = body,
				Headers = headers,
				Cookies = cookies,
				Proxy = proxy,
				Timeout = timeout,
				SendExpect100ContinueHeader = false
			};
			return Send(parameters);
		}

		public static HttpRequestResult Send(string method, string url,
			Stream body, WebHeaderCollection headers, int timeout = 0)
		{
			return Send(method, url, body, headers, null, null, timeout);
		}

		public static HttpRequestResult Send(string method, string url, byte[] body,
			WebHeaderCollection headers, CookieContainer cookies, IWebProxy proxy, int timeout = 0)
		{
			Stream bodyStream = body != null && body.Length > 0 ? new MemoryStream(body) : null;
			HttpRequestResult result = Send(method, url, bodyStream, cookies, headers, proxy, timeout);
			bodyStream?.Dispose();
			return result;
		}

		public static HttpRequestResult Send(string method, string url, byte[] body,
			WebHeaderCollection headers, IWebProxy proxy, int timeout = 0)
		{
			return Send(method, url, body, headers, null, proxy, timeout);
		}

		public static HttpRequestResult Send(string method, string url, byte[] body,
			WebHeaderCollection headers, CookieContainer cookies, int timeout = 0)
		{
			return Send(method, url, body, headers, cookies, null, timeout);
		}

		public static HttpRequestResult Send(string method, string url,
			string body, Encoding bodyEncoding, WebHeaderCollection headers, int timeout = 0)
		{
			byte[] bodyBytes = !string.IsNullOrEmpty(body) && bodyEncoding != null ? bodyEncoding.GetBytes(body) : null;
			return Send(method, url, bodyBytes, headers, null, null, timeout);
		}

		public static HttpRequestResult Send(string method, string url,
			WebHeaderCollection headers, int timeout = 0)
		{
			return Send(method, url, null, headers, timeout);
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
