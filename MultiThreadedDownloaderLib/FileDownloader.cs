﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

			DownloadedInLastSession = 0L;
			int tryNumber = 1;
			int maximumTryCount = TryCount;
			bool isInfiniteRetries = maximumTryCount <= 0;

			Connecting?.Invoke(this, Url, tryNumber, maximumTryCount);

			_cancellationTokenSource = cancellationTokenSource != null ?
				cancellationTokenSource : new CancellationTokenSource();

			Dictionary<int, long> chunkProcessingDict = new Dictionary<int, long>();
			long contentLength = downloadingTask.ByteTo == -1L ? -1L : downloadingTask.ByteTo - downloadingTask.ByteFrom + 1L;
			do
			{
				SetRange(downloadingTask.ByteFrom + DownloadedInLastSession, downloadingTask.ByteTo);
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

				if (contentLength == -1L) { contentLength = requestResult.WebContent.Length; }

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
			} while (!_cancellationTokenSource.IsCancellationRequested &&
				((!isInfiniteRetries && tryNumber++ < maximumTryCount) || isInfiniteRetries));
			if (_cancellationTokenSource.IsCancellationRequested)
			{
				LastErrorCode = _isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED_BY_USER;
			}
			else if (!isInfiniteRetries && tryNumber > maximumTryCount)
			{
				System.Diagnostics.Debug.WriteLine("Out of tries");
				LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
				tryNumber--;
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
			return Download(contentChunkStream, 0L, -1L, 4096, cancellationTokenSource);
		}

		public int Download(Stream outputStream, string outputFilePath,
			long rangeFrom, long rangeTo, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			ContentChunkStream contentChunkStream = new ContentChunkStream(outputFilePath, outputStream);
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

		public static int GetUrlContentLength(string url, NameValueCollection headers,
			out long contentLength, out string errorText)
		{
			int errorCode = GetUrlResponseHeaders(url, headers,
				out WebHeaderCollection responseHeaders, out errorText);
			if (errorCode == 200)
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
			}

			contentLength = -1L;
			return errorCode;
		}

		public static int GetUrlResponseHeaders(string url, NameValueCollection inHeaders,
			out WebHeaderCollection outHeaders, out string errorText)
		{
			HttpRequestResult requestResult = HttpRequestSender.Send("HEAD", url, inHeaders);
			if (requestResult.ErrorCode == 200)
			{
				outHeaders = new WebHeaderCollection();
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
			_rangeFrom = rangeFrom;
			_rangeTo = rangeTo;

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

			string rangeValue = rangeTo >= 0L ? $"{rangeFrom}-{rangeTo}" : $"{rangeFrom}-";
			Headers.Add("Range", rangeValue);

			return true;
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
