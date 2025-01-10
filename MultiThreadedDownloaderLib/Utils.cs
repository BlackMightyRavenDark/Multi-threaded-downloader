using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;

namespace MultiThreadedDownloaderLib
{
	public class Utils
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
			if (contentLength <= 0L || rangeTo < rangeFrom)
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
			HttpRequestResult requestResult = HttpRequestSender.Send("HEAD", url, timeout, inHeaders);
			if (requestResult.ErrorCode == 200 || requestResult.ErrorCode == 206)
			{
				outHeaders = new NameValueCollection();
				for (int i = 0; i < requestResult.HttpWebResponse.Headers.Count; ++i)
				{
					string name = requestResult.HttpWebResponse.Headers.GetKey(i);
					string value = requestResult.HttpWebResponse.Headers.Get(i);
					outHeaders.Add(name, value);
				}

				requestResult.Dispose();
				errorText = null;
				return 200;
			}

			outHeaders = null;
			errorText = requestResult.ErrorMessage;
			int errorCode = requestResult.ErrorCode;
			requestResult.Dispose();
			return errorCode;
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
