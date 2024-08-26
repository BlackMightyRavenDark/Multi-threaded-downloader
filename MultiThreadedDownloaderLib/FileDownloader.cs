﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace MultiThreadedDownloaderLib
{
	public sealed class FileDownloader : IDisposable
	{
		public string Url { get; set; }

		/// <summary>
		/// Set it to zero or less for infinite retries.
		/// </summary>
		public int TryCount { get; set; } = 1;

		public NameValueCollection Headers { get { return _headers; } set { SetHeaders(value); } }
		public double UpdateIntervalMilliseconds { get; set; } = 100.0;
		public long DownloadedInLastSession { get; private set; } = 0L;
		public long OutputStreamSize => DownloadingTask?.OutputStream?.Stream != null ?
			DownloadingTask.OutputStream.Stream.Length : 0L;
		public DownloadingTask DownloadingTask { get; private set; }
		private NameValueCollection _headers = new NameValueCollection();
		private CancellationTokenSource _cancellationTokenSource;
		public bool IsActive { get; private set; } = false;
		public int LastErrorCode { get; private set; } = 200;
		public string LastErrorMessage { get; private set; }
		public bool HasErrors => LastErrorCode != 200 && LastErrorCode != 206;
		public bool HasErrorMessage => !string.IsNullOrEmpty(LastErrorMessage) &&
			!string.IsNullOrWhiteSpace(LastErrorMessage) &&
			!string.Equals(LastErrorMessage, "OK", StringComparison.OrdinalIgnoreCase);
		private bool _isAborted = false;
		private long _rangeFrom = 0L;
		private long _rangeTo = -1L;

		public const int DOWNLOAD_ERROR_URL_NOT_DEFINED = -1;
		public const int DOWNLOAD_ERROR_INVALID_URL = -2;
		public const int DOWNLOAD_ERROR_CANCELED_BY_USER = -3;
		public const int DOWNLOAD_ERROR_INCOMPLETE_DATA_READ = -4;
		public const int DOWNLOAD_ERROR_RANGE = -5;
		public const int DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT = -6;
		public const int DOWNLOAD_ERROR_INSUFFICIENT_DISK_SPACE = -7;
		public const int DOWNLOAD_ERROR_DRIVE_NOT_READY = -8;
		public const int DOWNLOAD_ERROR_NULL_CONTENT = -9;
		public const int DOWNLOAD_ERROR_ABORTED = -10;
		public const int DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT = -11;

		public delegate void ConnectingDelegate(object sender, string url, int tryNumber, int maxTryCount);
		public delegate int ConnectedDelegate(object sender, string url, long contentLength, int errorCode);
		public delegate void WorkStartedDelegate(object sender, long contentLength, int tryNumber, int maxTryCount);
		public delegate void WorkProgressDelegate(object sender, long bytesTransferred, long contentLength,
			int tryNumber, int maxTryCount);
		public delegate void WorkFinishedDelegate(object sender, long bytesTransferred, long contentLength,
			int tryNumber, int maxTryCount, int errorCode);
		public ConnectingDelegate Connecting;
		public ConnectedDelegate Connected;
		public WorkStartedDelegate WorkStarted;
		public WorkProgressDelegate WorkProgress;
		public WorkFinishedDelegate WorkFinished;

		public void Dispose()
		{
			if (_cancellationTokenSource != null)
			{
				Stop();
				_cancellationTokenSource.Dispose();
				_cancellationTokenSource = null;
			}
		}

		public void DisposeOutputStream()
		{
			if (DownloadingTask != null)
			{
				DownloadingTask.OutputStream?.Dispose();
				DownloadingTask = null;
			}
		}

		public int Download(DownloadingTask downloadingTask, int bufferSize,
			CancellationTokenSource cancellationTokenSource)
		{
			IsActive = true;
			_isAborted = false;
			LastErrorMessage = null;
			DownloadingTask = downloadingTask;
			DownloadedInLastSession = 0L;

			if (string.IsNullOrEmpty(Url) || string.IsNullOrWhiteSpace(Url))
			{
				IsActive = false;
				return DOWNLOAD_ERROR_URL_NOT_DEFINED;
			}

			if (!IsRangeValid(downloadingTask.ByteFrom, downloadingTask.ByteTo))
			{
				IsActive = false;
				return DOWNLOAD_ERROR_RANGE;
			}

			long outputStreamInitialPosition = downloadingTask.OutputStream.Stream.Position;
			int tryNumber = 0;
			int maximumTryCount = TryCount;
			bool isInfiniteRetries = maximumTryCount <= 0;

			Connecting?.Invoke(this, Url, tryNumber, maximumTryCount);

			LastErrorCode = GetUrlResponseHeaders(Url, Headers, out NameValueCollection responseHeaders, out string headersErrorText);
			if (LastErrorCode != 200 && LastErrorCode != 206)
			{
				LastErrorMessage = headersErrorText;
				WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, tryNumber, maximumTryCount, LastErrorCode);
				return LastErrorCode;
			}

			_cancellationTokenSource = cancellationTokenSource != null ?
				cancellationTokenSource : new CancellationTokenSource();

			Dictionary<int, long> chunkProcessingDict = new Dictionary<int, long>();

			bool isRangeSupported = IsRangeSupported(responseHeaders);
			long contentLength;
			if (isRangeSupported)
			{
				ExtractContentLengthFromHeaders(responseHeaders, out contentLength);
			}
			else
			{
				contentLength = -1L;
				ResetRange();
			}

			do
			{
				tryNumber++;

				long byteTo = downloadingTask.ByteTo == -1L ? contentLength - 1L : downloadingTask.ByteTo;
				if (isRangeSupported && !SetRange(DownloadedInLastSession + downloadingTask.ByteFrom, byteTo))
				{
					LastErrorCode = DOWNLOAD_ERROR_RANGE;
					LastErrorMessage = "Ошибка диапазона! Скачивание прервано!";
					return LastErrorCode;
				}
				HttpRequestResult requestResult = HttpRequestSender.Send("GET", Url, Headers);
				LastErrorCode = requestResult.ErrorCode;
				LastErrorMessage = HasErrors ? requestResult.ErrorMessage : null;
				if (HasErrors)
				{
					requestResult.Dispose();
					IsActive = false;
					return LastErrorCode;
				}
				else if (requestResult.WebContent == null)
				{
					requestResult.Dispose();
					LastErrorCode = DOWNLOAD_ERROR_NULL_CONTENT;
					IsActive = false;
					return LastErrorCode;
				}

				if (contentLength == -1L)
				{
					contentLength = requestResult.WebContent.Length;
				}

				if (Connected != null)
				{
					LastErrorCode = Connected.Invoke(this, Url, contentLength, LastErrorCode);
				}

				if (HasErrors)
				{
					requestResult.Dispose();
					IsActive = false;
					return LastErrorCode;
				}

				if (contentLength == 0L)
				{
					requestResult.Dispose();
					IsActive = false;
					LastErrorCode = DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
					return DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
				}

				WorkStarted?.Invoke(this, contentLength, tryNumber, maximumTryCount);

				int lastTime = Environment.TickCount;
				bool completed = false;
				try
				{
					bool gZipped = requestResult.IsZippedContent();
					LastErrorCode = requestResult.WebContent.ContentToStream(
						downloadingTask.OutputStream.Stream, bufferSize, gZipped, (long bytes) =>
						{
							chunkProcessingDict[tryNumber] = bytes;
							DownloadedInLastSession = chunkProcessingDict.Sum(item => item.Value);
							int currentTime = Environment.TickCount;
							if (currentTime - lastTime >= UpdateIntervalMilliseconds)
							{
								WorkProgress?.Invoke(this, DownloadedInLastSession, contentLength,
									tryNumber, maximumTryCount);
								lastTime = currentTime;
							}
						}, _cancellationTokenSource.Token);
					completed = true;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.Message);
					if (isInfiniteRetries)
					{
						System.Diagnostics.Debug.WriteLine($"Restarting... Try №{++tryNumber}");
					}
					else if (tryNumber < maximumTryCount)
					{
						System.Diagnostics.Debug.WriteLine($"Restarting... Try №{tryNumber + 1} / {maximumTryCount}");
					}
				}

				requestResult.Dispose();

				if (completed) { break; }
				else if (!isInfiniteRetries && tryNumber >= maximumTryCount)
				{
					System.Diagnostics.Debug.WriteLine("Out of tries");
					LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
					break;
				}
				else if (!isRangeSupported)
				{
					System.Diagnostics.Debug.WriteLine("Resuming downloads is unavailable for this URL! Restarting from the beginning...");
					chunkProcessingDict.Clear();
					DownloadedInLastSession = 0L;
					downloadingTask.OutputStream.Stream.Position = outputStreamInitialPosition;
				}
			} while (!_cancellationTokenSource.IsCancellationRequested);
			if (_cancellationTokenSource.IsCancellationRequested)
			{
				LastErrorCode = _isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED_BY_USER;
			}
			WorkFinished?.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, maximumTryCount, LastErrorCode);

			IsActive = false;
			return LastErrorCode;
		}

		public int Download(DownloadingTask downloadingTask,
			CancellationTokenSource cancellationTokenSource)
		{
			return Download(downloadingTask, 4096, cancellationTokenSource);
		}

		public int Download(DownloadingTask downloadingTask, int bufferSize = 4096)
		{
			return Download(downloadingTask, bufferSize, null);
		}

		public int Download(ContentChunkStream contentChunkStream,
			long rangeFrom, long rangeTo, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			DownloadingTask downloadingTask = new DownloadingTask(contentChunkStream, rangeFrom, rangeTo);
			return Download(downloadingTask, bufferSize, cancellationTokenSource);
		}

		public int Download(ContentChunkStream contentChunkStream,
			long rangeFrom, long rangeTo,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(contentChunkStream, rangeFrom, rangeTo, 4096, cancellationTokenSource);
		}

		public int Download(ContentChunkStream contentChunkStream,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(contentChunkStream, _rangeFrom, _rangeTo, cancellationTokenSource);
		}

		public int Download(Stream outputStream, string outputFilePath,
			long rangeFrom, long rangeTo, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			ContentChunkStream contentChunkStream = new ContentChunkStream(outputStream, outputFilePath);
			return Download(contentChunkStream, rangeFrom, rangeTo,
				bufferSize, cancellationTokenSource);
		}

		public int Download(Stream outputStream,
			long rangeFrom, long rangeTo, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(outputStream, null, rangeFrom, rangeTo,
				bufferSize, cancellationTokenSource);
		}

		public int Download(Stream outputStream, string outputFilePath, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(outputStream, outputFilePath, _rangeFrom, _rangeTo,
				bufferSize, cancellationTokenSource);
		}

		public int Download(Stream outputStream, string outputFilePath,
			long rangeFrom, long rangeTo, CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(outputStream, outputFilePath, rangeFrom, rangeTo, 4096, cancellationTokenSource);
		}

		public int Download(Stream outputStream, long rangeFrom, long rangeTo,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(outputStream, null, rangeFrom, rangeTo, cancellationTokenSource);
		}

		public int Download(Stream outputStream, string outputFilePath, int bufferSize = 4096)
		{
			return Download(outputStream, outputFilePath, bufferSize, null);
		}

		public int Download(Stream outputStream, int bufferSize = 4096)
		{
			return Download(outputStream, bufferSize, null);
		}

		public int Download(Stream outputStream, int bufferSize,
			CancellationTokenSource cancellationTokenSource)
		{
			return Download(outputStream, null, bufferSize, cancellationTokenSource);
		}

		public int Download(Stream outputStream,
			CancellationTokenSource cancellationTokenSource)
		{
			return Download(outputStream, 4096, cancellationTokenSource);
		}

		public int DownloadString(out string responseString, Encoding encoding, int bufferSize = 4096)
		{
			try
			{
				using (MemoryStream mem = new MemoryStream())
				{
					int errorCode = Download(mem, bufferSize);
					responseString = errorCode == 200 || errorCode == 206 ?
						encoding.GetString(mem.ToArray()) : null;
					return errorCode;
				}
			} catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
				responseString = ex.Message;
				return ex.HResult;
			}
		}

		public int DownloadString(out string responseString, int bufferSize = 4096)
		{
			return DownloadString(out responseString, Encoding.UTF8, bufferSize);
		}

		public void Stop()
		{
			if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
			}
		}

		public void Abort()
		{
			_isAborted = true;
			Stop();
		}

		public static int GetUrlContentLength(string url, NameValueCollection inHeaders,
			out long contentLength, out string errorText)
		{
			int errorCode = GetUrlResponseHeaders(url, inHeaders,
				out NameValueCollection responseHeaders, out errorText);
			if (errorCode == 200)
			{
				return ExtractContentLengthFromHeaders(responseHeaders, out contentLength);
			}

			contentLength = -1L;
			return errorCode;
		}

		public static int GetUrlContentLength(string url, out long contentLength, out string errorText)
		{
			return GetUrlContentLength(url, null, out contentLength, out errorText);
		}

		public static int GetUrlContentLength(string url, out long contentLength)
		{
			return GetUrlContentLength(url, out contentLength, out _);
		}

		public static int IsRangeSupported(string url, NameValueCollection inHeaders,
			out bool result, out string errorText)
		{
			int errorCode = GetUrlResponseHeaders(url, inHeaders,
				out NameValueCollection responseHeaders, out errorText);
			if (errorCode == 200)
			{
				return IsAcceptRangeBytes(responseHeaders, out result);
			}

			result = false;
			return 404;
		}

		public static int IsRangeSupported(string url, NameValueCollection inHeaders,
			out bool result)
		{
			return IsRangeSupported(url, inHeaders, out result, out _);
		}

		public static int IsRangeSupported(string url, out bool result)
		{
			return IsRangeSupported(url, null, out result);
		}

		public static bool IsRangeSupported(NameValueCollection responseHeaders)
		{
			IsAcceptRangeBytes(responseHeaders, out bool result);
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
			out NameValueCollection outHeaders, out string errorText)
		{
			HttpRequestResult requestResult = HttpRequestSender.Send("HEAD", url, inHeaders);
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

		public void GetRange(out long rangeFrom, out long rangeTo)
		{
			if (DownloadingTask != null)
			{
				rangeFrom = DownloadingTask.ByteFrom;
				rangeTo = DownloadingTask.ByteTo;
			}
			else
			{
				rangeFrom = _rangeFrom;
				rangeTo = _rangeTo;
			}
		}

		public bool SetRange(long rangeFrom, long rangeTo)
		{
			if (!IsRangeValid(rangeFrom, rangeTo))
			{
				return false;
			}

			ResetRange();
			_rangeFrom = rangeFrom;
			_rangeTo = rangeTo;

			string rangeValue = rangeTo >= 0L ? $"{rangeFrom}-{rangeTo}" : $"{rangeFrom}-";
			Headers.Add("Range", rangeValue);

			return true;
		}

		public void ResetRange()
		{
			_rangeFrom = 0L;
			_rangeTo = -1L;

			for (int i = 0; i < Headers.Count; ++i)
			{
				string headerName = Headers.GetKey(i);

				if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrWhiteSpace(headerName) &&
					headerName.ToLower().Equals("range"))
				{
					Headers.Remove(headerName);
					break;
				}
			}
		}

		public static bool IsRangeValid(long rangeFrom, long rangeTo)
		{
			return rangeFrom >= 0L && (rangeTo < 0L || rangeTo >= rangeFrom);
		}

		private void SetHeaders(NameValueCollection headers)
		{
			_rangeFrom = 0L;
			_rangeTo = -1L;
			Headers.Clear();
			if (headers != null)
			{
				for (int i = 0; i < headers.Count; ++i)
				{
					string headerName = headers.GetKey(i);

					if (!string.IsNullOrEmpty(headerName) && !string.IsNullOrWhiteSpace(headerName))
					{
						string headerValue = headers.Get(i);

						if (!string.IsNullOrEmpty(headerValue) && headerName.ToLower().Equals("range"))
						{
							if (HttpRequestSender.ParseRangeHeaderValue(headerValue, out long rangeFrom, out long rangeTo))
							{
								SetRange(rangeFrom, rangeTo);
							}
							else
							{
								System.Diagnostics.Debug.WriteLine("Failed to parse the \"Range\" header!");
							}
							continue;
						}

						Headers.Add(headerName, headerValue);
					}
				}
			}
		}

		public static string ErrorCodeToString(int errorCode)
		{
			switch (errorCode)
			{
				case 400:
					return "Ошибка клиента!";

				case 403:
					return "Файл по ссылке недоступен!";

				case 404:
					return "Файл по ссылке не найден!";

				case DOWNLOAD_ERROR_INVALID_URL:
					return "Указана неправильная ссылка!";

				case DOWNLOAD_ERROR_URL_NOT_DEFINED:
					return "Не указана ссылка!";

				case DOWNLOAD_ERROR_CANCELED_BY_USER:
					return "Скачивание успешно отменено!";

				case DOWNLOAD_ERROR_ABORTED:
					return "Скачивание прервано!";

				case DOWNLOAD_ERROR_INCOMPLETE_DATA_READ:
					return "Ошибка чтения данных!";

				case DOWNLOAD_ERROR_RANGE:
					return "Указан неверный диапазон!";

				case DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT:
					return "Файл на сервере пуст!";

				case DOWNLOAD_ERROR_DRIVE_NOT_READY:
					return "Диск не готов!";

				case DOWNLOAD_ERROR_INSUFFICIENT_DISK_SPACE:
					return "Недостаточно места на диске!";

				case DOWNLOAD_ERROR_NULL_CONTENT:
					return "Ошибка получения контента!";

				case DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT:
					return "Закончились попытки!";

				default:
					return $"Код ошибки: {errorCode}";
			}
		}
	}
}
