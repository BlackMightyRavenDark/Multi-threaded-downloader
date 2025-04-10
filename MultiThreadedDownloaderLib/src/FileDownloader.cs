using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using static MultiThreadedDownloaderLib.Utils;

namespace MultiThreadedDownloaderLib
{
	public sealed class FileDownloader : IDisposable
	{
		public int Id { get; }
		public string Url { get; set; }
		public int ConnectionTimeout { get; set; }

		/// <summary>
		/// Set it to zero or less for infinite retries.
		/// </summary>
		public int TryCountLimit { get; set; } = 1;

		public NameValueCollection Headers { get => _headers; set { SetHeaders(value); } }
		public CookieContainer Cookies { get; set; }
		public WebProxy Proxy { get; set; }
		public int UpdateIntervalMilliseconds { get; set; } = 100;
		public int RetryIntervalMilliseconds { get; set; } = 1000;
		public bool IgnoreStreamSizeExceededError { get; set; } = false;
		public bool IgnoreHeaderRequestErrors { get; set; } = true;
		public bool SkipHeaderRequest { get; set; } = false;
		public long DownloadedInLastSession { get; private set; } = 0L;
		public long OutputStreamSize => DownloadableChunk?.OutputStream?.Stream != null ?
			DownloadableChunk.OutputStream.Stream.Length : 0L;
		public DownloadableChunk DownloadableChunk { get; private set; }

		/// <summary>
		/// Don't save downloaded data to anywhere.
		/// </summary>
		public bool FakeDownloading { get; set; } = false;

		public bool IsActive { get; private set; } = false;
		public MultiThreadedDownloader Owner { get; }
		public int LastErrorCode { get; private set; } = 200;
		public string LastErrorMessage { get; private set; }
		public bool HasErrors => LastErrorCode != 200 && LastErrorCode != 206;
		public bool HasErrorMessage => HasErrorMessageText();

		private NameValueCollection _headers = new NameValueCollection();
		private CancellationTokenSource _cancellationTokenSource;
		private bool _isAborted = false;
		private long _rangeFrom = 0L;
		private long _rangeTo = -1L;

		public const int DOWNLOAD_ERROR_URL_NOT_DEFINED = -1;
		public const int DOWNLOAD_ERROR_INVALID_URL = -2;
		public const int DOWNLOAD_ERROR_CANCELED_BY_USER = -3;
		public const int DOWNLOAD_ERROR_DATA_SIZE_MISMATCH = -4;
		public const int DOWNLOAD_ERROR_RANGE = -5;
		public const int DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT = -6;
		public const int DOWNLOAD_ERROR_INSUFFICIENT_DISK_SPACE = -7;
		public const int DOWNLOAD_ERROR_DRIVE_NOT_READY = -8;
		public const int DOWNLOAD_ERROR_NULL_CONTENT = -9;
		public const int DOWNLOAD_ERROR_ABORTED = -10;
		public const int DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT = -11;
		public const int DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED = -12;
		public const int DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED = -13;
		public const int DOWNLOAD_ERROR_UNSUPPORTED_COMPRESSION_ALGORITHM = -14;
		public const int DOWNLOAD_ERROR_OUTPUT_STREAM_NOT_ASSIGNED = -15;

		public delegate void PreparingDelegate(object sender, string url, DownloadableChunk downloadableChunk);
		public delegate void HeadersReceivingDelegate(object sender, string url, DownloadableChunk downloadableChunk,
			int tryNumber, int tryCountLimit);
		public delegate void HeadersReceivedDelegate(object sender, string url,
			DownloadableChunk downloadableChunk, NameValueCollection headers,
			int tryNumber, int tryCountLimit, int errorCode);
		public delegate void ConnectingDelegate(object sender, string url, int tryNumber, int tryCountLimit);
		public delegate int ConnectedDelegate(object sender, string url, long contentLength,
			NameValueCollection headers, int tryNumber, int tryCountLimit, int errorCode);
		public delegate void WorkStartedDelegate(object sender, long contentLength, int tryNumber, int tryCountLimit);
		public delegate void WorkProgressDelegate(object sender, long bytesTransferred, long contentLength,
			int tryNumber, int tryCountLimit);
		public delegate void WorkFinishedDelegate(object sender, long bytesTransferred, long contentLength,
			int tryNumber, int tryCountLimit, int errorCode);
		public PreparingDelegate Preparing;
		public HeadersReceivingDelegate HeadersReceiving;
		public HeadersReceivedDelegate HeadersReceived;
		public ConnectingDelegate Connecting;
		public ConnectedDelegate Connected;
		public WorkStartedDelegate WorkStarted;
		public WorkProgressDelegate WorkProgress;
		public WorkFinishedDelegate WorkFinished;

		public FileDownloader(int id) { Id = id; }
		public FileDownloader() : this(0) { }
		internal FileDownloader(MultiThreadedDownloader owner, int id)
			: this(id) { Owner = owner; }

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
			if (DownloadableChunk != null)
			{
				DownloadableChunk.OutputStream?.Dispose();
				DownloadableChunk = null;
			}
		}

		public int Download(DownloadableChunk downloadableChunk, int bufferSize,
			CancellationTokenSource cancellationTokenSource)
		{
			Preparing?.Invoke(this, Url, downloadableChunk);

			IsActive = true;
			_isAborted = false;
			LastErrorMessage = null;
			DownloadableChunk = downloadableChunk;
			DownloadedInLastSession = 0L;

			if (string.IsNullOrEmpty(Url) || string.IsNullOrWhiteSpace(Url))
			{
				LastErrorCode = DOWNLOAD_ERROR_URL_NOT_DEFINED;
				WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, 0, TryCountLimit, LastErrorCode);
				IsActive = false;
				return LastErrorCode;
			}

			if (!IsValidUrl(Url, out string urlErrorMessage))
			{
				LastErrorCode = DOWNLOAD_ERROR_INVALID_URL;
				LastErrorMessage = urlErrorMessage;
				WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, 0, TryCountLimit, LastErrorCode);
				IsActive = false;
				return LastErrorCode;
			}

			if (!FakeDownloading && downloadableChunk?.OutputStream?.Stream == null)
			{
				LastErrorCode = DOWNLOAD_ERROR_OUTPUT_STREAM_NOT_ASSIGNED;
				WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, 0, TryCountLimit, LastErrorCode);
				IsActive = false;
				return LastErrorCode;
			}

			bool isIndependent = Owner == null;
			bool isRangeAssigned = downloadableChunk.Range != null;
			if (isIndependent)
			{
				if (!isRangeAssigned)
				{
					ResetRange();
				}
				else if (!downloadableChunk.Range.IsValid)
				{
					LastErrorCode = DOWNLOAD_ERROR_RANGE;
					WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, 0, TryCountLimit, LastErrorCode);
					IsActive = false;
					return LastErrorCode;
				}
			}

			_cancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();

			int tryNumber = 0;
			int tryCountLimit = TryCountLimit;
			bool isInfiniteRetries = tryCountLimit <= 0;

			Stopwatch stopwatch = new Stopwatch();
			NameValueCollection responseHeaders = null;
			if (!SkipHeaderRequest && isIndependent)
			{
				while (true)
				{
					tryNumber++;
					HeadersReceiving?.Invoke(this, Url, downloadableChunk, tryNumber, tryCountLimit);
					stopwatch.Restart();
					LastErrorCode = GetUrlResponseHeaders(Url, Headers, Cookies, Proxy, ConnectionTimeout,
						out responseHeaders, out string headersErrorText);

					if (_cancellationTokenSource.IsCancellationRequested)
					{
						stopwatch.Stop();
						LastErrorCode = _isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED_BY_USER;
						LastErrorMessage = null;
						IsActive = false;
						return LastErrorCode;
					}
					else if (IgnoreHeaderRequestErrors || LastErrorCode == 200 || LastErrorCode == 206)
					{
						HeadersReceived?.Invoke(this, Url, downloadableChunk, responseHeaders, tryNumber, tryCountLimit, LastErrorCode);
						tryNumber = 0;
						break;
					}

					if (!isInfiniteRetries && tryNumber + 1 > tryCountLimit)
					{
						stopwatch.Stop();
						LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
						LastErrorMessage = "Не удалось получить HTTP-заголовки!";
						HeadersReceived?.Invoke(this, Url, downloadableChunk, responseHeaders, tryNumber, tryCountLimit, LastErrorCode);
						WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, tryNumber, tryCountLimit, LastErrorCode);
						IsActive = false;
						return LastErrorCode;
					}

					WaitInterval(stopwatch, tryNumber, tryCountLimit);
				}
			}

			bool isRangeSupported = responseHeaders != null && IsRangeSupported(responseHeaders);
			long contentLength;
			if (isIndependent)
			{
				if (isRangeSupported && responseHeaders != null)
				{
					ExtractContentLengthFromHeaders(responseHeaders, out contentLength);
					if (isRangeAssigned) { downloadableChunk.Range.ContentLength = contentLength; }
				}
				else
				{
					if (isRangeAssigned) { downloadableChunk.Range.ContentLength = -1L; }
					contentLength = -1L;
					ResetRange();
				}

				if (isRangeAssigned && !downloadableChunk.Range.IsValid)
				{
					LastErrorCode = DOWNLOAD_ERROR_RANGE;
					WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, 0, TryCountLimit, LastErrorCode);
					IsActive = false;
					return LastErrorCode;
				}
			}
			else
			{
				contentLength = Owner.ContentLength;
			}

			bool isFakeDownloading = FakeDownloading;
			long outputStreamInitialPosition = isFakeDownloading ? 0L : downloadableChunk.OutputStream.Stream.Position;

			if (isIndependent && !isFakeDownloading && !IgnoreStreamSizeExceededError && contentLength > 0L &&
				outputStreamInitialPosition + contentLength < downloadableChunk.OutputStream.Stream.Length)
			{
				LastErrorCode = DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED;
				WorkFinished?.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, tryCountLimit, LastErrorCode);
				IsActive = false;
				return LastErrorCode;
			}

			Dictionary<int, long> chunkProcessingDict = new Dictionary<int, long>();

			do
			{
				tryNumber++;
				bool isTryLimitReached = !isInfiniteRetries && tryNumber > tryCountLimit;
				if (isTryLimitReached)
				{
#if DEBUG
					Debug.WriteLine($"Downloader №{Id}: Out of tries");
#endif
					stopwatch.Stop();
					LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
					WorkFinished?.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, tryCountLimit, LastErrorCode);
					IsActive = false;
					return LastErrorCode;
				}
#if DEBUG
				Debug.WriteLine(isInfiniteRetries ?
					$"Downloader №{Id}: Try №{tryNumber}" :
					$"Downloader №{Id}: Try №{tryNumber} / {tryCountLimit}");
				if (Cookies != null && Cookies.Count > 0)
				{
					Debug.WriteLine($"Downloader №{Id}: Using {Cookies.Count} {(Cookies.Count > 1 ? "cookies" : "cookie")}");
				}

				if (Proxy != null)
				{
					Debug.WriteLine($"Downloader №{Id}: Using a proxy server {Proxy.Address}");
				}
#endif
				if (isRangeSupported && isRangeAssigned)
				{
					long byteTo = downloadableChunk.Range.EndPosition >= 0L ? downloadableChunk.Range.EndPosition :
						(contentLength >= 0L ? contentLength - 1L : -1L);
					if (!SetRange(DownloadedInLastSession + downloadableChunk.Range.StartPosition, byteTo))
					{
						stopwatch.Stop();
						LastErrorCode = DOWNLOAD_ERROR_RANGE;
						LastErrorMessage = "Ошибка диапазона! Скачивание прервано!";
						WorkFinished?.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, tryCountLimit, LastErrorCode);
						IsActive = false;
						return LastErrorCode;
					}
#if DEBUG
					long resumingPosition = outputStreamInitialPosition + DownloadedInLastSession;
#endif
					if (!isFakeDownloading)
					{
						downloadableChunk.OutputStream.Stream.Position =
#if DEBUG
							resumingPosition;
#else
							outputStreamInitialPosition + DownloadedInLastSession;
#endif
					}
#if DEBUG
					if (tryNumber > 1)
					{
						Debug.WriteLine($"Downloader №{Id}: Resuming from position {resumingPosition}...");
					}
#endif
				}
				else //range is not supported.
				{
#if DEBUG
					if (tryNumber > 1)
					{
						Debug.WriteLine($"Downloader №{Id}: Resuming downloads is unavailable for this URL! Restarting from the beginning...");
					}
#endif
					chunkProcessingDict.Clear();
					DownloadedInLastSession = 0L;
					if (!isFakeDownloading)
					{
#if DEBUG
						Debug.WriteLine($"Downloader №{Id}: Output stream position is {outputStreamInitialPosition}");
#endif
						downloadableChunk.OutputStream.Stream.Position = outputStreamInitialPosition;
					}
				}

				Connecting?.Invoke(this, Url, tryNumber, tryCountLimit);

				stopwatch.Restart();
				HttpRequestSenderParameters requestParameters = new HttpRequestSenderParameters()
				{
					Method = "GET",
					Url = Url,
					Headers = Headers,
					Cookies = Cookies,
					Proxy = Proxy,
					Timeout = ConnectionTimeout
				};
				HttpRequestResult requestResult = HttpRequestSender.Send(requestParameters);
				requestResult.GetContent(out string webContentErrorMessage);
				if (requestResult.WebContent == null)
				{
					requestResult.Dispose();
					stopwatch.Stop();
					LastErrorCode = DOWNLOAD_ERROR_NULL_CONTENT;
					LastErrorMessage = webContentErrorMessage;
					IsActive = false;
					return LastErrorCode;
				}

				if (requestResult.WebContent.Length == 0L)
				{
					requestResult.Dispose();
					stopwatch.Stop();
					LastErrorCode = DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
					WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, tryNumber, tryCountLimit, LastErrorCode);
					IsActive = false;
					return LastErrorCode;
				}

				if (requestResult.IsExceptionRaised)
				{
#if DEBUG
					Debug.WriteLine($"Downloader №{Id}: The 'GET' request is failed with an exception! " +
						$"Error code: {requestResult.ErrorCode}. Restarting...");
#endif
					if (!isTryLimitReached)
					{
						LastErrorCode = requestResult.ErrorCode;
						if (tryNumber == tryCountLimit)
						{
							LastErrorMessage = requestResult.WebContent.ContentToString(
								out webContentErrorMessage) == 200 ? webContentErrorMessage :
								(requestResult.HasErrorMessage ? requestResult.ErrorMessage : null);
						}
					}
					requestResult.Dispose();

					WaitInterval(stopwatch, tryNumber, tryCountLimit);
					continue;
				}

				LastErrorCode = requestResult.ErrorCode;
				LastErrorMessage = HasErrors && requestResult.HasErrorMessage ? requestResult.ErrorMessage : null;
				if (HasErrors)
				{
					requestResult.Dispose();
#if DEBUG
					Debug.WriteLine($"Downloader №{Id}: The 'GET' request is failed with error code {LastErrorCode}! Restarting...");
#endif
					WaitInterval(stopwatch, tryNumber, tryCountLimit);
					continue;
				}

				if (contentLength == -1L && isIndependent)
				{
					contentLength = requestResult.WebContent.Length;
				}

				if (Connected != null)
				{
					LastErrorCode = Connected.Invoke(this, Url, contentLength,
						requestResult.HttpWebResponse.Headers,
						tryNumber, tryCountLimit, LastErrorCode);
				}

				if (HasErrors)
				{
					requestResult.Dispose();
					stopwatch.Stop();
					WorkFinished?.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, tryCountLimit, LastErrorCode);
					IsActive = false;
					return LastErrorCode;
				}

				if (contentLength == 0L)
				{
					requestResult.Dispose();
					stopwatch.Stop();
					LastErrorCode = DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
					WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, tryNumber, tryCountLimit, LastErrorCode);
					IsActive = false;
					return DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
				}

				WorkStarted?.Invoke(this, contentLength, tryNumber, tryCountLimit);

				int lastTime = Environment.TickCount;
				bool completed = false;
				try
				{
					CancellationToken token = _cancellationTokenSource.Token;
					Stream actualOutputStream = isFakeDownloading ? null : downloadableChunk.OutputStream.Stream;
					LastErrorCode = requestResult.WebContent.ContentToStream(
						actualOutputStream, bufferSize, (long bytes) =>
						{
							chunkProcessingDict[tryNumber] = bytes;
							DownloadedInLastSession = chunkProcessingDict.Sum(item => item.Value);
							if (!_isAborted && WorkProgress != null)
							{
								int currentTime = Environment.TickCount;
								if (currentTime - lastTime >= UpdateIntervalMilliseconds)
								{
									WorkProgress.Invoke(this, DownloadedInLastSession, contentLength,
										tryNumber, tryCountLimit);
									lastTime = currentTime;
								}
							}
						}, token);
					completed = true;
				}
				catch (Exception ex)
				{
#if DEBUG
					Debug.WriteLine($"Downloader №{Id} catches exception!\n{ex.Message}");
#endif
					LastErrorCode = ex.HResult;
					LastErrorMessage = ex.Message;
				}

				requestResult.Dispose();

				if (completed) { break; }
			} while (!_cancellationTokenSource.IsCancellationRequested);
			stopwatch.Stop();

			if (_cancellationTokenSource.IsCancellationRequested)
			{
				LastErrorCode = _isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED_BY_USER;
			}
			else if (!IgnoreStreamSizeExceededError && !isFakeDownloading &&
				contentLength > 0L && downloadableChunk.OutputStream.Stream.Length > contentLength)
			{
				LastErrorCode = DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED;
			}

			if (!_isAborted && WorkFinished != null)
			{
				WorkFinished.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, tryCountLimit, LastErrorCode);
			}

			IsActive = false;
			return LastErrorCode;
		}

		public int Download(DownloadableChunk downloadableChunk,
			CancellationTokenSource cancellationTokenSource)
		{
			return Download(downloadableChunk, 4096, cancellationTokenSource);
		}

		public int Download(DownloadableChunk downloadableChunk, int bufferSize = 4096)
		{
			return Download(downloadableChunk, bufferSize, null);
		}

		public int Download(ContentChunkStream contentChunkStream,
			DownloadRange downloadRange, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			DownloadableChunk downloadableChunk = new DownloadableChunk(contentChunkStream, downloadRange);
			return Download(downloadableChunk, bufferSize, cancellationTokenSource);
		}

		public int Download(ContentChunkStream contentChunkStream,
			long rangeFrom, long rangeTo, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			DownloadRange range = new DownloadRange(rangeFrom, rangeTo);
			return Download(contentChunkStream, range, bufferSize, cancellationTokenSource);
		}

		public int Download(ContentChunkStream contentChunkStream, int bufferSize,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(contentChunkStream, _rangeFrom, _rangeTo, bufferSize, cancellationTokenSource);
		}

		public int Download(ContentChunkStream contentChunkStream,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(contentChunkStream, 4096, cancellationTokenSource);
		}

		public int Download(ContentChunkStream contentChunkStream,
			long rangeFrom, long rangeTo,
			CancellationTokenSource cancellationTokenSource = null)
		{
			return Download(contentChunkStream, rangeFrom, rangeTo, 4096, cancellationTokenSource);
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
#if DEBUG
				Debug.WriteLine(ex.Message);
#endif
				responseString = ex.Message;
				return ex.HResult;
			}
		}

		public int DownloadString(out string responseString, int bufferSize = 4096)
		{
			return DownloadString(out responseString, Encoding.UTF8, bufferSize);
		}

		public bool Stop()
		{
			if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
				return true;
			}

			return false;
		}

		public bool Abort()
		{
			_isAborted = Stop();
			return _isAborted;
		}

		public void GetRange(out DownloadRange downloadRange)
		{
			downloadRange = DownloadableChunk?.Range != null ?
				DownloadableChunk.Range :
				new DownloadRange(_rangeFrom, _rangeTo,
				DownloadableChunk?.Range != null ? DownloadableChunk.Range.ContentLength : -1L);
		}

		public void GetRange(out long startPosition, out long endPosition)
		{
			if (DownloadableChunk?.Range != null)
			{
				startPosition = DownloadableChunk.Range.StartPosition;
				endPosition = DownloadableChunk.Range.EndPosition;
			}
			else
			{
				startPosition = _rangeFrom;
				endPosition = _rangeTo;
			}
		}

		public bool SetRange(DownloadRange downloadRange)
		{
			return SetRange(downloadRange.StartPosition, downloadRange.EndPosition);
		}

		public bool SetRange(long startPosition, long endPosition)
		{
			if (!DownloadRange.IsValidRange(startPosition, endPosition))
			{
				return false;
			}

			ResetRange();
			_rangeFrom = startPosition;
			_rangeTo = endPosition;

			string rangeValue = endPosition >= 0L ? $"{startPosition}-{endPosition}" : $"{startPosition}-";
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
							if (ParseRangeHeaderValue(headerValue, out long rangeFrom, out long rangeTo))
							{
								SetRange(rangeFrom, rangeTo);
							}
#if DEBUG
							else
							{
								Debug.WriteLine("Failed to parse the \"Range\" header!");
							}
#endif
							continue;
						}

						Headers.Add(headerName, headerValue);
					}
				}
			}
		}

		private void WaitInterval(Stopwatch stopwatch, int tryNumber, int tryCountLimit)
		{
			if (stopwatch != null && RetryIntervalMilliseconds > 0 &&
				(tryCountLimit <= 0 || tryCountLimit > 0 && tryNumber < tryCountLimit))
			{
				TimeSpan interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
				TimeSpan elapsed = stopwatch.Elapsed;
				if (elapsed < interval)
				{
					TimeSpan difference = interval - elapsed;
#if DEBUG
					Debug.WriteLine($"Downloader №{Id}: Waiting {(int)difference.TotalMilliseconds} milliseconds, then restarting...");
#endif
					Thread.Sleep(difference);
				}
			}
		}

		private bool HasErrorMessageText()
		{
			return !string.IsNullOrEmpty(LastErrorMessage) &&
				!string.IsNullOrWhiteSpace(LastErrorMessage) &&
				string.Compare(LastErrorMessage, "ok", true) != 0;
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

				case DOWNLOAD_ERROR_DATA_SIZE_MISMATCH:
					return "Размер скачанного не совпадает с заявленным!";

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

				case DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED:
					return "Скачано успешно, но размер файла больше размера скачанного! Файл содержит лишние данные!";

				case DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED:
					return "Размер файла может получиться больше размера скачанного! Данные могут быть повреждены!";

				case DOWNLOAD_ERROR_UNSUPPORTED_COMPRESSION_ALGORITHM:
					return "Алгоритм сжатия данных не поддерживается!";

				case DOWNLOAD_ERROR_OUTPUT_STREAM_NOT_ASSIGNED:
					return "Не указан поток для сохранения данных!";

				default:
					return $"Код ошибки: {errorCode}";
			}
		}
	}
}
