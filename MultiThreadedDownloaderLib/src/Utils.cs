using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace MultiThreadedDownloaderLib
{
	public static class Utils
	{
		public const int ONE_MEGABYTE = 1048576; //1024 * 1024;
		public static int ConnectionLimit
		{
			get => ServicePointManager.DefaultConnectionLimit;
			set => ServicePointManager.DefaultConnectionLimit = value;
		}

		public static string GetNumberedFileName(string filePath)
		{
			try
			{
				if (string.IsNullOrEmpty(filePath) || string.IsNullOrWhiteSpace(filePath)) { return null; }
				if (File.Exists(filePath))
				{
					string path = Path.GetDirectoryName(filePath);
					string name = Path.GetFileNameWithoutExtension(filePath);
					string extension = Path.GetExtension(filePath);
					string part1 = Path.Combine(path, name);
					bool isExtensionPresent = !string.IsNullOrEmpty(extension) && !string.IsNullOrWhiteSpace(extension);

					int i = 1;
					while (true)
					{
						string newFilePath = isExtensionPresent ? $"{part1}_{++i}{extension}" : $"{part1}_{++i}";
						if (!File.Exists(newFilePath)) { return newFilePath; }
					}
				}

				return filePath;
			}
#if DEBUG
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
			}
#else
			catch { }
#endif
			return null;
		}

		internal static IEnumerable<DownloadRange> SplitContentToChunks(
			long contentLength, long rangeFrom, long rangeTo, int chunkCount)
		{
			if (rangeTo < 0L) { rangeTo = contentLength; }
			if (contentLength <= 0L || rangeTo < rangeFrom || chunkCount <= 1)
			{
				yield return new DownloadRange(0L, contentLength - 1L, contentLength);
				yield break;
			}

			long contentLengthRanged = rangeTo >= 0L ? rangeTo - rangeFrom : contentLength - rangeFrom;
			if (chunkCount <= 1 || contentLengthRanged <= ONE_MEGABYTE)
			{
				long byteTo = rangeTo >= 0L ? rangeTo : contentLengthRanged + rangeFrom - 1L;
				yield return new DownloadRange(rangeFrom, byteTo, contentLength);
				yield break;
			}

			long chunkSize = contentLengthRanged / chunkCount;
			long startPos = rangeFrom;
			for (int i = 0; i < chunkCount; ++i)
			{
				bool lastChunk = i == chunkCount - 1;
				long endPos = lastChunk ? (rangeTo >= 0L ? rangeTo : contentLength - 1L) : (startPos + chunkSize);

				yield return new DownloadRange(startPos, endPos, contentLength);

				if (!lastChunk) { startPos += chunkSize + 1L; }
			}
		}

		public static int GetUrlContentLength(string url, WebHeaderCollection inHeaders,
			CookieContainer cookies, IWebProxy proxy,
			out long contentLength, out string errorText, int timeout = 0)
		{
			int errorCode = GetUrlResponseHeaders(url, inHeaders, cookies, proxy, timeout,
				out WebHeaderCollection responseHeaders, out errorText);
			if (errorCode == 200)
			{
				return ExtractContentLengthFromHeaders(responseHeaders, out contentLength);
			}

			contentLength = -1L;
			return errorCode;
		}

		public static int GetUrlContentLength(string url, out long contentLength, out string errorText, int timeout = 0)
		{
			return GetUrlContentLength(url, null, null, null, out contentLength, out errorText, timeout);
		}

		public static int GetUrlContentLength(string url, out long contentLength, int timeout = 0)
		{
			return GetUrlContentLength(url, out contentLength, out _, timeout);
		}

		public static int IsRangeSupported(string url, WebHeaderCollection inHeaders,
			CookieContainer cookies, IWebProxy proxy,
			out bool result, out string errorText, int timeout = 0)
		{
			int errorCode = GetUrlResponseHeaders(url, inHeaders, cookies, proxy, timeout,
				out WebHeaderCollection responseHeaders, out errorText);
			if (errorCode == 200)
			{
				errorCode = IsAcceptRangeBytes(responseHeaders, out result);
				return result ? errorCode : IsContentRangeBytes(responseHeaders, out result);
			}

			result = false;
			return 404;
		}

		public static int IsRangeSupported(string url, WebHeaderCollection inHeaders,
			out bool result, int timeout = 0)
		{
			return IsRangeSupported(url, inHeaders, null, null, out result, out _, timeout);
		}

		public static int IsRangeSupported(string url, out bool result, int timeout = 0)
		{
			return IsRangeSupported(url, null, out result, timeout);
		}

		public static bool IsRangeSupported(WebHeaderCollection responseHeaders)
		{
			IsAcceptRangeBytes(responseHeaders, out bool result);
			if (!result) { IsContentRangeBytes(responseHeaders, out result); }
			return result;
		}

		private static int IsAcceptRangeBytes(WebHeaderCollection responseHeaders, out bool result)
		{
			for (int i = 0; i < responseHeaders.Count; ++i)
			{
				string headerName = responseHeaders.GetKey(i);
				if (string.Compare(headerName, "accept-ranges", StringComparison.OrdinalIgnoreCase) == 0)
				{
					string headerValue = responseHeaders.Get(i);
					if (headerValue.ToLower().Contains("bytes"))
					{
						result = true;
						return 200;
					}

					result = false;
					return 204;
				}
			}

			result = false;
			return 404;
		}

		private static int IsContentRangeBytes(WebHeaderCollection responseHeaders, out bool result)
		{
			for (int i = 0; i < responseHeaders.Count; ++i)
			{
				string headerName = responseHeaders.GetKey(i);
				if (string.Compare(headerName, "content-range", StringComparison.OrdinalIgnoreCase) == 0)
				{
					string headerValue = responseHeaders.Get(i);
					if (headerValue.ToLower().Contains("bytes"))
					{
						result = true;
						return 200;
					}

					result = false;
					return 204;
				}
			}

			result = false;
			return 404;
		}

		public static int ExtractContentLengthFromHeaders(WebHeaderCollection responseHeaders, out long contentLength)
		{
			for (int i = 0; i < responseHeaders.Count; ++i)
			{
				string headerName = responseHeaders.GetKey(i);
				if (headerName.Equals("Content-Length"))
				{
					string headerValue = responseHeaders.Get(i);
					if (!long.TryParse(headerValue, out contentLength))
					{
						contentLength = -1L;
						return 204;
					}
					return 200;
				}
			}

			contentLength = -1L;
			return 404;
		}

		public static bool IsCompressedContent(string contentEncodingHeaderValue, out string algorithmId)
		{
			if (!string.IsNullOrEmpty(contentEncodingHeaderValue) && !string.IsNullOrWhiteSpace(contentEncodingHeaderValue))
			{
				if (contentEncodingHeaderValue.Contains("gzip"))
				{
					algorithmId = "gzip";
					return true;
				}
				else if (contentEncodingHeaderValue.Contains("deflate"))
				{
					algorithmId = "deflate";
					return true;
				}
				else if (contentEncodingHeaderValue.Contains("br"))
				{
					algorithmId = "br";
					return true;
				}
				else if (contentEncodingHeaderValue.Contains("zstd"))
				{
					algorithmId = "zstd";
					return true;
				}
			}

			algorithmId = null;
			return false;
		}

		public static bool IsCompressedContent(WebHeaderCollection headers, out string algorithmId)
		{
			string contentEncodingHeaderValue = headers?.Get("Content-Encoding");
			return IsCompressedContent(contentEncodingHeaderValue, out algorithmId);
		}

		public static int GetUrlResponseHeaders(string url, WebHeaderCollection inHeaders,
			CookieContainer cookies, IWebProxy proxy,
			int timeout, out WebHeaderCollection outHeaders, out string errorText)
		{
			try
			{
				HttpRequestSenderParameters requestParameters = new HttpRequestSenderParameters()
				{
					Method = "HEAD",
					Url = url,
					Headers = inHeaders,
					Cookies = cookies,
					Proxy = proxy,
					Timeout = timeout,
				};
				using (HttpRequestResult requestResult = HttpRequestSender.Send(requestParameters))
				{
					if (requestResult.ErrorCode == 200 || requestResult.ErrorCode == 206)
					{
						outHeaders = new WebHeaderCollection();
						for (int i = 0; i < requestResult.HttpWebResponse.Headers.Count; ++i)
						{
							string name = requestResult.HttpWebResponse.Headers.GetKey(i);
							string value = requestResult.HttpWebResponse.Headers.Get(i);
							outHeaders.Add(name, value);
						}

						errorText = null;
						return 200;
					}

					outHeaders = null;
					errorText = requestResult.HasErrorMessage ? requestResult.ErrorMessage : null;
					return requestResult.ErrorCode;
				}
			} catch (Exception ex)
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
				outHeaders = null;
				errorText = ex.Message;
				return ex.HResult;
			}
		}

		public static int GetUrlResponseHeaders(string url, WebHeaderCollection inHeaders,
			out WebHeaderCollection outHeaders, out string errorText)
		{
			return GetUrlResponseHeaders(url, inHeaders, null, null, 0, out outHeaders, out errorText);
		}

		internal static HttpRequestResult CreateHttpRequestResult(int errorCode, string errorMessage,
			HttpWebResponse httpWebResponse, bool exceptionWasRaised = false)
		{
			HttpRequestResult result = new HttpRequestResult(errorCode, errorMessage, httpWebResponse, exceptionWasRaised);
			if (httpWebResponse != null)
			{
				CombineHeaders(httpWebResponse, result.Headers);
			}
			return result;
		}

		internal static List<DownloadableChunk> BuildChunkSequence(
			ConcurrentDictionary<int, DownloadableTask> downloadableTasks,
			int threadCount, out bool isValidSequence)
		{
			int elementCount = downloadableTasks.Count;
			if (elementCount > 0 && elementCount == threadCount)
			{
				isValidSequence = true;
				for (int i = 0; i < threadCount; ++i)
				{
					isValidSequence &= downloadableTasks.ContainsKey(i) &&
						downloadableTasks[i]?.DownloadableChunk?.OutputStream != null;
					if (!isValidSequence) { return null; }
				}

				List<DownloadableChunk> taskList = downloadableTasks.Select(item => item.Value.DownloadableChunk).ToList();
				taskList.Sort((x, y) => x.Range.StartPosition < y.Range.StartPosition ? -1 : 1);

				return taskList;
			}

			isValidSequence = false;
			return null;
		}

		internal static bool IsEnoughDiskSpace(char driveLetter, long bytesNeeded, out string errorMessage)
		{
			try
			{
				DriveInfo di = new DriveInfo(driveLetter.ToString());
				if (!di.IsReady)
				{
					errorMessage = "Диск не готов";
					return false;
				}
				bool ok = di.AvailableFreeSpace > bytesNeeded;
				errorMessage = ok ? null : "Недостаточно места на диске";
				return ok;
			}
			catch (Exception ex)
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
				errorMessage = ex.Message;
				return false;
			}
		}

		public static bool IsValidUrl(string url, out string errorMessage)
		{
			try
			{
				Uri uri = new Uri(url);
				errorMessage = null;
				return true;
			}
			catch (Exception ex)
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
				errorMessage = ex.Message;
			}

			return false;
		}

		public static void SetRequestHeaders(HttpWebRequest request, WebHeaderCollection headers)
		{
			request.Headers.Clear();
			for (int i = 0; i < headers.Count; ++i)
			{
				string headerName = headers.GetKey(i).Trim();
				if (string.IsNullOrEmpty(headerName) || string.IsNullOrWhiteSpace(headerName))
				{
					continue;
				}
				string headerValue = headers.Get(i).Trim();
				string headerNameLowercased = headerName.ToLower();

				//TODO: Complete headers support.
				if (headerNameLowercased.Equals("accept"))
				{
					request.Accept = headerValue;
					continue;
				}
				else if (headerNameLowercased.Equals("user-agent"))
				{
					request.UserAgent = headerValue;
					continue;
				}
				else if (headerNameLowercased.Equals("referer"))
				{
					request.Referer = headerValue;
					continue;
				}
				else if (headerNameLowercased.Equals("host"))
				{
					request.Host = headerValue;
					continue;
				}
				else if (headerNameLowercased.Equals("content-type"))
				{
					request.ContentType = headerValue;
					continue;
				}
				else if (headerNameLowercased.Equals("content-length"))
				{
					if (long.TryParse(headerValue, out long length))
					{
						request.ContentLength = length;
					}
#if DEBUG
					else
					{
						System.Diagnostics.Debug.WriteLine("Can't parse value of \"Content-Length\" header!");
					}
#endif
					continue;
				}
				else if (headerNameLowercased.Equals("connection"))
				{
#if DEBUG
					System.Diagnostics.Debug.WriteLine("The \"Connection\" header is not supported yet.");
#endif
					continue;
				}
				else if (headerNameLowercased.Equals("range"))
				{
					if (ParseRangeHeaderValue(headerValue, out long byteFrom, out long byteTo))
					{
						if (byteFrom >= 0L && byteTo >= 0L && byteTo >= byteFrom)
						{
							request.AddRange(byteFrom, byteTo);
						}
					}
#if DEBUG
					else
					{
						System.Diagnostics.Debug.WriteLine("Invalid \"Range\" header value! The header will not bind!");
					}
#endif
					continue;
				}
				else if (headerNameLowercased.Equals("if-modified-since"))
				{
#if DEBUG
					System.Diagnostics.Debug.WriteLine("The \"If-Modified-Since\" header is not supported yet.");
#endif
					continue;
				}
				else if (headerNameLowercased.Equals("transfer-encoding"))
				{
#if DEBUG
					System.Diagnostics.Debug.WriteLine("The \"Transfer-Encoding\" header is not supported yet.");
#endif
					continue;
				}

				request.Headers.Add(headerName, headerValue);
			}
		}

		public static bool ParseRangeHeaderValue(string headerValue, out long byteFrom, out long byteTo)
		{
			string[] splitted = headerValue.Split('-');
			if (splitted.Length == 2)
			{
				bool isStr0Empty = string.IsNullOrEmpty(splitted[0]) || string.IsNullOrWhiteSpace(splitted[0]);
				bool isStr1Empty = string.IsNullOrEmpty(splitted[1]) || string.IsNullOrWhiteSpace(splitted[1]);
				if (isStr0Empty && isStr1Empty)
				{
					byteFrom = 0L;
					byteTo = -1L;
					return false;
				}

				if (!isStr0Empty)
				{
					if (!long.TryParse(splitted[0], out byteFrom))
					{
						byteFrom = 0L;
						byteTo = -1L;
						return false;
					}
				}
				else
				{
					byteFrom = 0L;
				}

				if (!isStr1Empty)
				{
					if (!long.TryParse(splitted[1], out byteTo))
					{
						byteFrom = 0L;
						byteTo = -1L;
						return false;
					}
				}
				else
				{
					byteTo = -1L;
				}

				return true;
			}

			byteFrom = 0L;
			byteTo = -1L;
			return false;
		}

		public static WebHeaderCollection ParseHeaderList(string headersText)
		{
			WebHeaderCollection headers = new WebHeaderCollection();

			string[] strings = headersText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
			foreach (string str in strings)
			{
				if (!string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str))
				{
					string[] splitted = str.Split(new char[] { ':' }, 2);
					if (splitted.Length == 2)
					{
						string headerName = splitted[0].Trim();
						if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrWhiteSpace(headerName))
						{
							string headerValue = splitted[1].Trim();
							headers.Add(headerName, headerValue);
						}
					}
				}
			}

			return headers;
		}

		internal static WebHeaderCollection GetUnrangedHeaders(WebHeaderCollection headers)
		{
			WebHeaderCollection result = new WebHeaderCollection();
			if (headers != null && headers.Count > 0)
			{
				for (int i = 0; i < headers.Count; ++i)
				{
					string headerName = headers.GetKey(i);
					if (string.Compare(headerName, "Range", true) != 0)
					{
						result.Add(headerName, headers.Get(i));
					}
				}
			}
			return result;
		}

		public static string HeadersToString(WebHeaderCollection headers)
		{
			string t = string.Empty;

			for (int i = 0; i < headers.Count; ++i)
			{
				string headerName = headers.GetKey(i);
				string headerValue = headers.Get(i);
				t += $"{headerName}: {headerValue}{Environment.NewLine}";
			}

			return t;
		}

		public static string HeadersToCode(WebHeaderCollection headers)
		{
			string code = $"WebHeaderCollection headers = new WebHeaderCollection(){Environment.NewLine}{{{Environment.NewLine}";
			for (int i = 0; i < headers.Count; ++i)
			{
				string keyName = headers.GetKey(i);
				string keyValue = headers.Get(i).Replace("\\", "\\\\").Replace("\"", "\\\"");
				code += $"\t{{ \"{keyName}\", \"{keyValue}\" }},{Environment.NewLine}";
			}

			int n = Environment.NewLine.Length;
			code = $"{code.Substring(0, code.Length - n - 1)}{Environment.NewLine}}};{Environment.NewLine}";
			return code;
		}

		internal static bool CombineHeaders(this HttpWebResponse response, WebHeaderCollection resultHeaders)
		{
			if (response.Headers != null && resultHeaders != null)
			{
				int keyCount = response.Headers.Count;
				for (int i = 0; i < keyCount; ++i)
				{
					string keyName = response.Headers.GetKey(i);
					string keyValue = response.Headers.Get(i);
					resultHeaders.Add(keyName, keyValue);
				}

				if (string.IsNullOrEmpty(resultHeaders["Content-Encoding"]) && !string.IsNullOrEmpty(response.ContentEncoding))
				{
					resultHeaders["Content-Encoding"] = response.ContentEncoding;
				}

				if (string.IsNullOrEmpty(resultHeaders["Content-Length"]) && response.ContentLength >= 0L)
				{
					resultHeaders["Content-Length"] = response.ContentLength.ToString();
				}

				if (string.IsNullOrEmpty(resultHeaders["Content-Type"]) && !string.IsNullOrEmpty(response.ContentType))
				{
					resultHeaders["Content-Type"] = response.ContentType;
				}

				if (string.IsNullOrEmpty(resultHeaders["Last-Modified"]))
				{
					resultHeaders["Last-Modified"] = response.LastModified.ToString("R");
				}

				if (string.IsNullOrEmpty(resultHeaders["Server"]) && !string.IsNullOrEmpty(response.Server))
				{
					resultHeaders["Server"] = response.Server;
				}

				return true;
			}

			return false;
		}

		public static string GetContentEncodingHeaderValue(this HttpWebResponse httpWebResponse)
		{
			string value = httpWebResponse.Headers?.Get("Content-Encoding");
			if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
			{
				value = httpWebResponse.ContentEncoding;
			}
			return value;
		}

		public static bool IsSameLogicalDrive(char driveLetter1, char driveLetter2)
		{
			return char.ToUpper(driveLetter1) == char.ToUpper(driveLetter2);
		}

		public static bool IsSameLogicalDrive(string path1, string path2)
		{
			return !string.IsNullOrEmpty(path1) && !string.IsNullOrEmpty(path2) && IsSameLogicalDrive(path1[0], path2[0]);
		}
	}
}
