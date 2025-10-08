using System;
using System.Collections.Generic;
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
		/// Ограничение на число попыток скачивания. Если установлено значение '0' или меньше,
		/// будет произведено бесконечное количество попыток.
		/// </summary>
		public int TryCountLimit { get; set; } = 1;

		public WebHeaderCollection Headers { get; set; } = new WebHeaderCollection();
		public CookieContainer Cookies { get; set; }
		public WebProxy Proxy { get; set; }
		public int UpdateIntervalMilliseconds { get; set; } = 100;
		public int RetryIntervalMilliseconds { get; set; } = 1000;
		public bool IgnoreStreamSizeExceededError { get; set; } = false;

		/// <summary>
		/// Для получения HTTP-заголовков существует метод "HEAD".
		/// Но иногда серверы отказываются отвечать на запрос "HEAD" (или возвращают неверные данные).
		/// Тогда для получения заголовков можно использовать другой метод. Например, "GET".
		/// </summary>
		public string HeaderRequestMethod { get; set; } = "HEAD";

		public bool IgnoreHeaderRequestErrors { get; set; } = true;
		public bool SkipHeaderRequest { get; set; } = false;
		public long DownloadedInLastSession { get; private set; } = 0L;
		public long OutputStreamSize => DownloadableChunk?.OutputStream?.Stream != null ?
			DownloadableChunk.OutputStream.Stream.Length : 0L;
		public DownloadableChunk DownloadableChunk { get; private set; }
		public bool IsCompressedContent { get; private set; }
		public string ContentCompressionAlgorithm { get; private set; }

		/// <summary>
		/// Если 'true', скачанные данные не будут никуда сохранены.
		/// </summary>
		public bool FakeDownloading { get; set; } = false;

		public bool IsActive { get; private set; } = false;
		public DependentTaskInfo DependentTaskInfo { get; } = null;
		public int LastErrorCode { get; private set; } = 200;
		public string LastErrorMessage { get; private set; }
		public bool HasErrors => LastErrorCode != 200 && LastErrorCode != 206;
		public bool HasErrorMessage => HasErrorMessageText();

		private CancellationTokenSource _cancellationTokenSource;

		public const int DOWNLOAD_ERROR_URL_NOT_DEFINED = -1;
		public const int DOWNLOAD_ERROR_INVALID_URL = -2;
		public const int DOWNLOAD_ERROR_CANCELED = -3;
		public const int DOWNLOAD_ERROR_DATA_SIZE_MISMATCH = -4;
		public const int DOWNLOAD_ERROR_RANGE = -5;
		public const int DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT = -6;
		public const int DOWNLOAD_ERROR_INSUFFICIENT_DISK_SPACE = -7;
		public const int DOWNLOAD_ERROR_DRIVE_NOT_READY = -8;
		public const int DOWNLOAD_ERROR_NULL_CONTENT = -9;
		public const int DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT = -11;
		public const int DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED = -12;
		public const int DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED = -13;
		public const int DOWNLOAD_ERROR_UNSUPPORTED_COMPRESSION_ALGORITHM = -14;
		public const int DOWNLOAD_ERROR_OUTPUT_STREAM_NOT_ASSIGNED = -15;

		public delegate void PreparingDelegate(object sender, string url, DownloadableChunk downloadableChunk);
		public delegate void HeadersReceivingDelegate(object sender, string url, DownloadableChunk downloadableChunk,
			int tryNumber, int tryCountLimit);
		public delegate void HeadersReceivedDelegate(object sender, string url,
			DownloadableChunk downloadableChunk, WebHeaderCollection headers,
			int tryNumber, int tryCountLimit, int errorCode);
		public delegate void ConnectingDelegate(object sender, string url, int tryNumber, int tryCountLimit);
		public delegate int ConnectedDelegate(object sender, string url, long contentLength,
			WebHeaderCollection headers, int tryNumber, int tryCountLimit, int errorCode);
		public delegate void WorkStartedDelegate(object sender, long contentLength, int tryNumber, int tryCountLimit);
		public delegate void WorkProgressDelegate(object sender, long bytesTransferred, long contentLength,
			int tryNumber, int tryCountLimit);
		public delegate void WorkErrorDelegate(object sender, int errorCode, string errorMessage,
			long bytesTransferred, long contentLength, int tryNumber, int tryCountLimit);
		public delegate void WorkFinishedDelegate(object sender, long bytesTransferred, long contentLength,
			int tryNumber, int tryCountLimit, int errorCode);
		public PreparingDelegate Preparing;
		public HeadersReceivingDelegate HeadersReceiving;
		public HeadersReceivedDelegate HeadersReceived;
		public ConnectingDelegate Connecting;
		public ConnectedDelegate Connected;
		public WorkStartedDelegate WorkStarted;
		public WorkProgressDelegate WorkProgress;
		public WorkErrorDelegate WorkError;
		public WorkFinishedDelegate WorkFinished;

		public FileDownloader(int id) { Id = id; }
		public FileDownloader() : this(0) { }
		internal FileDownloader(DependentTaskInfo dependentTaskInfo, int id)
			: this(id) { DependentTaskInfo = dependentTaskInfo; }

		public void Dispose()
		{
			if (IsActive) { Stop(); }
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
			LastErrorMessage = null;
			DownloadableChunk = downloadableChunk;
			DownloadedInLastSession = 0L;
			IsCompressedContent = false;
			ContentCompressionAlgorithm = null;

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

			bool isIndependent = DependentTaskInfo == null;
			bool isRangeAssigned = downloadableChunk.Range != null && downloadableChunk.Range.IsAssigned;
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
				else
				{
					SetRange(downloadableChunk.Range);
				}
			}

			if ((!isIndependent || !isRangeAssigned) && Headers != null && Headers.Count > 0)
			{
				Headers.Remove(HttpRequestHeader.Range);
			}

			bool isSharedCancellationToken = cancellationTokenSource != null;
			_cancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();

			int tryNumber = 0;
			int tryCountLimit = TryCountLimit;
			bool isInfiniteRetries = tryCountLimit <= 0;

			Stopwatch stopwatch = new Stopwatch();
			WebHeaderCollection responseHeaders = null;
			if (!SkipHeaderRequest && isIndependent)
			{
				while (true)
				{
					tryNumber++;
					HeadersReceiving?.Invoke(this, Url, downloadableChunk, tryNumber, tryCountLimit);
					stopwatch.Restart();
					LastErrorCode = GetUrlResponseHttpHeaders(HeaderRequestMethod, Url, Headers, Cookies, Proxy, ConnectionTimeout,
						out responseHeaders, out string headersErrorText);

					if (_cancellationTokenSource.IsCancellationRequested)
					{
						stopwatch.Stop();
						LastErrorCode = DOWNLOAD_ERROR_CANCELED;
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

			bool isRangeSupported;
			long contentLength;
			if (isIndependent)
			{
				IsCompressedContent = Utils.IsCompressedContent(responseHeaders, out string algorithmId);
				ContentCompressionAlgorithm = algorithmId;
				if (IsCompressedContent)
				{
					isRangeSupported = false;
					contentLength = -1L;
					ResetRange();
				}
				else
				{
					isRangeSupported = responseHeaders != null && IsRangeSupported(responseHeaders);
					if (isRangeSupported)
					{
						ExtractContentLengthFromHttpHeaders(responseHeaders, out contentLength);
						if (isRangeAssigned)
						{
							downloadableChunk.Range.ContentLength = contentLength;
							if (!downloadableChunk.Range.IsValid)
							{
								LastErrorCode = DOWNLOAD_ERROR_RANGE;
								WorkFinished?.Invoke(this, DownloadedInLastSession, -1L, 0, TryCountLimit, LastErrorCode);
								IsActive = false;
								return LastErrorCode;
							}
						}
					}
					else
					{
						if (isRangeAssigned) { downloadableChunk.Range.ContentLength = -1L; }
						contentLength = -1L;
						ResetRange();
					}
				}
			}
			else
			{
				isRangeSupported = DependentTaskInfo.IsRangeSupported;
				contentLength = DependentTaskInfo.ContentLength;
				IsCompressedContent = DependentTaskInfo.IsCompressedContent;
				ContentCompressionAlgorithm = DependentTaskInfo.ContentCompressionAlgorithm;
				if (isRangeSupported && isRangeAssigned)
				{
					downloadableChunk.Range.ContentLength = contentLength;
				}
			}

			bool isFakeDownloading = FakeDownloading;
			long outputStreamInitialPosition = isFakeDownloading ? 0L : downloadableChunk.OutputStream.Stream.Position;

			if (isIndependent && !IsCompressedContent && !isFakeDownloading && !IgnoreStreamSizeExceededError && contentLength > 0L &&
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
				bool isTryLimitReached = !isInfiniteRetries && tryNumber + 1 > tryCountLimit;
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

				tryNumber++;
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
				if (!IsCompressedContent && isRangeSupported && isRangeAssigned)
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
				else // Возможность докачки недоступна.
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

					ResetRange();
					isRangeAssigned = false;
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
				if (requestResult.IsExceptionRaised)
				{
#if DEBUG
					Debug.WriteLine($"Downloader №{Id}: The 'GET' request was failed with an exception: {requestResult.ErrorMessage}!. " +
						$"Error code: {requestResult.ErrorCode}. Restarting...");
#endif
					if (!isTryLimitReached)
					{
						LastErrorCode = requestResult.ErrorCode;
						LastErrorMessage = requestResult.ErrorMessage;
					}
					requestResult.Dispose();

					WaitInterval(stopwatch, tryNumber, tryCountLimit);
					continue;
				}

				requestResult.GetContent(out string webContentErrorMessage);
				if (requestResult.WebContent == null)
				{
#if DEBUG
					Debug.WriteLine($"Downloader №{Id}: Can't get content! The 'WebContent' property is null! {webContentErrorMessage}. " +
						$"Error code: {requestResult.ErrorCode}. Restarting...");
#endif
					if (!isTryLimitReached)
					{
						LastErrorCode = requestResult.ErrorCode;
						LastErrorMessage = webContentErrorMessage;
					}
					requestResult.Dispose();

					WaitInterval(stopwatch, tryNumber, tryCountLimit);
					continue;
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

				if (isIndependent)
				{
					/*
					 * Разные методы запроса могут выдавать разные HTTP-заголовки для одной и той же ссылки.
					 * Например, в ответе на 'HEAD'-запрос на google.com отсутствует заголовок 'Content-Encoding'
					 * и некоторые другие, которые есть в ответе на 'GET'-запрос.
					 * По-этому, необходимо перепроверять заголовки после каждого запроса.
					 */
					if (contentLength == -1L)
					{
						contentLength = requestResult.WebContent.Length;
						if (isRangeAssigned) { downloadableChunk.Range.ContentLength = contentLength; }
					}

					IsCompressedContent = requestResult.WebContent.IsCompressed;
					ContentCompressionAlgorithm = requestResult.WebContent.CompressionAlgorithm;
				}

				if (Connected != null)
				{
					LastErrorCode = Connected.Invoke(this, Url, contentLength, requestResult.Headers,
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

				if (isIndependent && IsCompressedContent)
				{
					Debug.WriteLine($"Downloader №{Id}: Content compression algorithm: {ContentCompressionAlgorithm}");
				}

				WorkStarted?.Invoke(this, contentLength, tryNumber, tryCountLimit);

				int lastTime = Environment.TickCount;
				bool completed = false;
				bool isExceptionRaised = false;
				try
				{
					CancellationToken token = _cancellationTokenSource.Token;
					Stream actualOutputStream = isFakeDownloading ? null : downloadableChunk.OutputStream.Stream;
					LastErrorCode = requestResult.WebContent.ContentToStream(
						actualOutputStream, bufferSize, (long bytes) =>
						{
							chunkProcessingDict[tryNumber] = bytes;
							DownloadedInLastSession = chunkProcessingDict.Sum(item => item.Value);
							if (WorkProgress != null)
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
					isExceptionRaised = true;
					LastErrorCode = ex.HResult;
					LastErrorMessage = ex.Message;
				}

				requestResult.Dispose();

				if (completed) { break; }
				else if (isExceptionRaised)
				{
					WorkError?.Invoke(this, LastErrorCode, LastErrorMessage,
						DownloadedInLastSession, contentLength, tryNumber, tryCountLimit);
					if (RetryIntervalMilliseconds > 0 &&
						!isInfiniteRetries && tryNumber < tryCountLimit)
					{
#if DEBUG
						Debug.WriteLine($"Downloader №{Id}: Waiting {RetryIntervalMilliseconds} milliseconds, then restarting...");
#endif
						Thread.Sleep(RetryIntervalMilliseconds);
					}
				}
			} while (!_cancellationTokenSource.IsCancellationRequested);
			stopwatch.Stop();

			if (_cancellationTokenSource.IsCancellationRequested)
			{
				LastErrorCode = DOWNLOAD_ERROR_CANCELED;
			}
			else if (!IgnoreStreamSizeExceededError && !isFakeDownloading &&
				contentLength > 0L && downloadableChunk.OutputStream.Stream.Length > contentLength)
			{
				LastErrorCode = DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED;
			}
			else if (LastErrorCode == 200 && (!isIndependent || isRangeAssigned))
			{
				LastErrorCode = 206;
				LastErrorMessage = "Partial Content";
			}

			if (!isSharedCancellationToken && _cancellationTokenSource != null)
			{
				_cancellationTokenSource.Dispose();
			}
			_cancellationTokenSource = null;

			WorkFinished?.Invoke(this, DownloadedInLastSession, contentLength, tryNumber, tryCountLimit, LastErrorCode);

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
			ExtractRangeFromHttpHeaders(Headers, out long rangeFrom, out long rangeTo, out _);
			return Download(contentChunkStream, rangeFrom, rangeTo, bufferSize, cancellationTokenSource);
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
			ExtractRangeFromHttpHeaders(Headers, out long rangeFrom, out long rangeTo, out _);
			return Download(outputStream, outputFilePath, rangeFrom, rangeTo,
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

		public void GetRange(out DownloadRange downloadRange)
		{
			downloadRange = DownloadableChunk?.Range != null ?
				DownloadableChunk.Range : new DownloadRange(0L, -1L);
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
				startPosition = 0L;
				endPosition = -1L;
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

			string rangeValue = FormatHttpHeadersRangeValue(startPosition, endPosition);
			if (Headers == null) { Headers = new WebHeaderCollection(); }
			Headers["Range"] = rangeValue;

			return true;
		}

		public void ResetRange()
		{
			Headers?.Remove("Range");
		}

		private void WaitInterval(Stopwatch stopwatch, int tryNumber, int tryCountLimit)
		{
			if (stopwatch != null && RetryIntervalMilliseconds > 0 &&
				(tryCountLimit <= 0 || (tryCountLimit > 0 && tryNumber < tryCountLimit)))
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
				string.Compare(LastErrorMessage, "ok", true) != 0 &&
				string.Compare(LastErrorMessage, "partial content", true) != 0;
		}

		public static string ErrorCodeToString(int errorCode)
		{
			switch (errorCode)
			{
				case 200:
					return "OK";

				case 206:
					return "Partial Content";

				case 204:
					return "Сервер не выдал полезных данных!";

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

				case DOWNLOAD_ERROR_CANCELED:
					return "Скачивание успешно отменено!";

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
					return "Неведомая ранее ошибка!";
			}
		}
	}
}
