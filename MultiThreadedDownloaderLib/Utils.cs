using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;

namespace MultiThreadedDownloaderLib
{
	public static class Utils
	{
		public const int ONE_MEGABYTE = 1048576; //1024 * 1024;

		public static string GetNumberedFileName(string filePath)
		{
			if (File.Exists(filePath))
			{
				string dirPath = Path.GetDirectoryName(filePath);
				string fileName = Path.GetFileNameWithoutExtension(filePath);
				string ext = Path.GetExtension(filePath);
				string part1 = !string.IsNullOrEmpty(dirPath) ? Path.Combine(dirPath, fileName) : fileName;
				bool isExtensionPresent = !string.IsNullOrEmpty(ext) && !string.IsNullOrWhiteSpace(ext);

				int i = 1;
				string newFilePath;
				do
				{
					newFilePath = isExtensionPresent ? $"{part1}_{++i}{ext}" : $"{part1}_{++i}";
				} while (File.Exists(newFilePath));
				return newFilePath;
			}
			return filePath;
		}

		internal static IEnumerable<Tuple<long, long>> SplitContentToChunks(
			long contentLength, long rangeFrom, long rangeTo, int chunkCount)
		{
			if (rangeTo < 0L) { rangeTo = contentLength; }
			if (contentLength <= 0L || rangeTo < rangeFrom || chunkCount <= 1)
			{
				yield return new Tuple<long, long>(0L, -1L);
				yield break;
			}

			long contentLengthRanged = rangeTo >= 0L ? rangeTo - rangeFrom : contentLength - rangeFrom;
			if (chunkCount <= 1 || contentLengthRanged <= ONE_MEGABYTE)
			{
				long byteTo = rangeTo >= 0L ? rangeTo : contentLengthRanged + rangeFrom - 1;
				yield return new Tuple<long, long>(rangeFrom, byteTo);
				yield break;
			}

			long chunkSize = contentLengthRanged / chunkCount;
			long startPos = rangeFrom;
			for (int i = 0; i < chunkCount; ++i)
			{
				bool lastChunk = i == chunkCount - 1;
				long endPos = lastChunk ? (rangeTo >= 0 ? rangeTo : contentLength - 1) : (startPos + chunkSize);

				yield return new Tuple<long, long>(startPos, endPos);

				if (!lastChunk) { startPos += chunkSize + 1; }
			}
		}

		public static int GetUrlContentLength(string url, NameValueCollection inHeaders,
			out long contentLength, out string errorText, int timeout = 0)
		{
			int errorCode = GetUrlResponseHeaders(url, inHeaders, timeout,
				out NameValueCollection responseHeaders, out errorText);
			if (errorCode == 200)
			{
				return ExtractContentLengthFromHeaders(responseHeaders, out contentLength);
			}

			contentLength = -1L;
			return errorCode;
		}

		public static int GetUrlContentLength(string url, out long contentLength, out string errorText, int timeout = 0)
		{
			return GetUrlContentLength(url, null, out contentLength, out errorText, timeout);
		}

		public static int GetUrlContentLength(string url, out long contentLength, int timeout = 0)
		{
			return GetUrlContentLength(url, out contentLength, out _, timeout);
		}

		public static int IsRangeSupported(string url, NameValueCollection inHeaders,
			out bool result, out string errorText, int timeout = 0)
		{
			int errorCode = GetUrlResponseHeaders(url, inHeaders, timeout,
				out NameValueCollection responseHeaders, out errorText);
			if (errorCode == 200)
			{
				errorCode = IsAcceptRangeBytes(responseHeaders, out result);
				return result ? errorCode : IsContentRangeBytes(responseHeaders, out result);
			}

			result = false;
			return 404;
		}

		public static int IsRangeSupported(string url, NameValueCollection inHeaders,
			out bool result, int timeout = 0)
		{
			return IsRangeSupported(url, inHeaders, out result, out _, timeout);
		}

		public static int IsRangeSupported(string url, out bool result, int timeout = 0)
		{
			return IsRangeSupported(url, null, out result, timeout);
		}

		public static bool IsRangeSupported(NameValueCollection responseHeaders)
		{
			IsAcceptRangeBytes(responseHeaders, out bool result);
			if (!result) { IsContentRangeBytes(responseHeaders, out result); }
			return result;
		}

		private static int IsAcceptRangeBytes(NameValueCollection responseHeaders, out bool result)
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

		private static int IsContentRangeBytes(NameValueCollection responseHeaders, out bool result)
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

		public static int ExtractContentLengthFromHeaders(NameValueCollection responseHeaders, out long contentLength)
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

		public static int GetUrlResponseHeaders(string url, NameValueCollection inHeaders,
			int timeout, out NameValueCollection outHeaders, out string errorText)
		{
			try
			{
				using (HttpRequestResult requestResult = HttpRequestSender.Send("HEAD", url, inHeaders, timeout))
				{
					if (requestResult.ErrorCode == 200 || requestResult.ErrorCode == 206)
					{
						outHeaders = new NameValueCollection();
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

		public static int GetUrlResponseHeaders(string url, NameValueCollection inHeaders,
			out NameValueCollection outHeaders, out string errorText)
		{
			return GetUrlResponseHeaders(url, inHeaders, 0, out outHeaders, out errorText);
		}

		public static bool IsRangeValid(long rangeFrom, long rangeTo)
		{
			return rangeFrom >= 0L && (rangeTo < 0L || rangeTo >= rangeFrom);
		}

		internal static List<DownloadingTask> BuildChunkSequence(
			ConcurrentDictionary<int, DownloadableContentChunk> contentChunks,
			int threadCount, out bool isValidSequence)
		{
			int elementCount = contentChunks.Count;
			if (elementCount > 0 && elementCount == threadCount)
			{
				isValidSequence = true;
				for (int i = 0; i < threadCount; ++i)
				{
					isValidSequence &= contentChunks.ContainsKey(i) &&
						contentChunks[i]?.DownloadingTask?.OutputStream != null;
					if (!isValidSequence) { return null; }
				}

				List<DownloadingTask> taskList = contentChunks.Select(item => item.Value.DownloadingTask).ToList();
				taskList.Sort((x, y) => x.ByteFrom < y.ByteFrom ? -1 : 1);

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
				System.Diagnostics.Debug.WriteLine(ex.Message);
				errorMessage = ex.Message;
				return false;
			}
		}

		public static void SetRequestHeaders(HttpWebRequest request, NameValueCollection headers)
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

		public static NameValueCollection ParseHeaderList(string headersText)
		{
			NameValueCollection headers = new NameValueCollection();

			string[] strings = headersText.Split(new string[] { "\r\n" }, System.StringSplitOptions.None);
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

		public static string HeadersToString(NameValueCollection headers)
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

		public static string GetContentEncodingHeaderValue(this HttpWebResponse httpWebResponse)
		{
			string value = httpWebResponse.Headers?.Get("Content-Encoding");
			if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
			{
				value = httpWebResponse.ContentEncoding;
			}
			return value;
		}

		public static Stream ToStream(this byte[] bytes, bool seekToBeginning = false)
		{
			if (bytes.Length > 0)
			{
				MemoryStream stream = new MemoryStream();
				stream.Write(bytes, 0, bytes.Length);
				if (seekToBeginning) { stream.Position = 0L; }
				return stream;
			}
			return null;
		}

		public static int GetDefaultMaximumConnectionLimit()
		{
			return ServicePointManager.DefaultConnectionLimit;
		}

		public static void SetDefaultMaximumConnectionLimit(int limit)
		{
			ServicePointManager.DefaultConnectionLimit = limit;
		}
	}
}
