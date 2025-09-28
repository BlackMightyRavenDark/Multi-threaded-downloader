using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static MultiThreadedDownloaderLib.FileDownloader;
using static MultiThreadedDownloaderLib.StreamAppender;
using static MultiThreadedDownloaderLib.Utils;

namespace MultiThreadedDownloaderLib
{
	public sealed class MultiThreadedDownloader : IDisposable
	{
		public string Url { get; set; } = null;
		public int ConnectionTimeout { get; set; }

		/// <summary>
		/// Путь к файлу, куда будут сохранены скачанные данные.
		/// Если файл не существует, он автоматически будет создан.
		/// Иначе, будет создан файл с пронумерованным именем и значение этого свойства изменится.
		/// </summary>
		public string OutputFileName { get; set; } = null;

		public string TempDirectory { get; set; } = null;
		public int UpdateIntervalMilliseconds { get; set; } = 100;
		public int RetryIntervalMilliseconds { get; set; } = 1000;
		public int ChunksMergingUpdateIntervalMilliseconds { get; set; } = 100;
		public long DownloadedBytes { get; private set; } = 0L;
		public long ContentLength { get; private set; } = -1L;

		/// <summary>
		/// Если 'true', скачанные данные не будут никуда сохранены.
		/// </summary>
		public bool FakeDownloading { get; set; } = false;

		/// <summary>
		/// Использовать оперативную память (RAM) для хранения временных файлов.
		/// Позволяет существенно сократить количество обращений к накопителю,
		/// увеличив таким образом скорость скачивания.
		/// Внимание! Экспериментальная функция!
		/// Должно использоваться аккуратно и очень очень нежно!
		/// </summary>
		public bool UseRamForTempFiles { get; set; } = false;

		/// <summary>
		/// Количество одновременных потоков скачивания.
		/// Каждый поток будет скачивать свой сегмент (чанк) файла.
		/// Внимание! Для файлов, размером в один мегабайт и меньше, принудительно будет установлен 1 поток!
		/// </summary>
		public int ThreadCount { get; set; } = 2;

		/// <summary>
		/// Ограничение на число попыток скачивания для каждого потока.
		/// Если хоть один поток превысит это значение, скачивание будет прервано.
		/// Значение '0' или меньше - для бесконечного числа попыток.
		/// </summary>
		public int TryCountLimitPerThread { get; set; } = 1;

		/// <summary>
		/// Ограничение на число попыток внутри каждого потока.
		/// Позволяет не перекачивать весь чанк заново при возникновении ошибок в потоках.
		/// При достижении этого значения, скачивание чанка будет перезапущено и счётчик обнулится.
		/// Значение '0' или меньше - для бесконечного числа попыток.
		/// </summary>
		public int TryCountLimitInsideThread { get; set; } = 1;

		/// <summary>
		/// Для получения HTTP-заголовков существует метод "HEAD".
		/// Но иногда серверы отказываются отвечать на запрос "HEAD" (или возвращают неверные данные).
		/// Тогда для получения заголовков можно использовать другой метод. Например, "GET".
		/// </summary>
		public string HeaderRequestMethod { get; set; } = "HEAD";

		public bool IsActive { get; private set; }
		public WebHeaderCollection Headers { get; set; }
		public CookieContainer Cookies { get; set; }
		public WebProxy Proxy { get; set; }
		public bool MergeChunksAutomatically { get; set; } = true;
		public bool IsCompressedContent { get; private set; }
		public string ContentCompressionAlgorithm { get; private set; }
		public int LastErrorCode { get; private set; }
		public string LastErrorMessage { get; private set; }
		public bool IsTempDirectoryAvailable => !string.IsNullOrEmpty(TempDirectory) &&
			!string.IsNullOrWhiteSpace(TempDirectory) && Directory.Exists(TempDirectory);
		public bool HasErrorMessage => HasErrorMessageText();

		private bool _isCanceled = false;
		private bool _isAborted = false;
		private bool _isDisposed = false;

		private CancellationTokenSource _cancellationTokenSource;

		public const int DOWNLOAD_ERROR_MERGING_CHUNKS = -200;
		public const int DOWNLOAD_ERROR_CREATE_FILE = -201;
		public const int DOWNLOAD_ERROR_NO_URL_SPECIFIED = -202;
		public const int DOWNLOAD_ERROR_NO_FILE_NAME_SPECIFIED = -203;
		public const int DOWNLOAD_ERROR_TEMPORARY_DIR_NOT_EXISTS = -204;
		public const int DOWNLOAD_ERROR_FINAL_FILE_MOVE = -205;
		public const int DOWNLOAD_ERROR_CUSTOM = -206;
		public const int DOWNLOAD_ERROR_CHUNK_SEQUENCE = -207;
		public const int DOWNLOAD_ERROR_UNDEFINED = -208;
		public const int DOWNLOAD_ERROR_FILE_NUMBERING = -209;
		public const int DOWNLOAD_ERROR_ABORTED = -210;

		public delegate void PreparingDelegate(object sender);
		public delegate void ConnectingDelegate(object sender, string url, int tryNumber, int tryCountLimit);
		public delegate void ConnectedDelegate(object sender, string url, long contentLength,
			WebHeaderCollection headers, int tryNumber, int tryCountLimit, CustomError customError);
		public delegate void DownloadStartedDelegate(object sender, long contentLength);
		public delegate void DownloadProgressDelegate(object sender, ConcurrentDictionary<int, DownloadableTask> tasks);
		public delegate CustomError ChunksDownloadedDelegate(object sender, List<DownloadableChunk> downloadableChunks, long contentLength);
		public delegate void DownloadFinishedDelegate(object sender, long bytesTransferred, long contentLength, int errorCode, string fileName,
			IEnumerable<DownloadableTask> downloadableTasks);
		public delegate void ChunkMergingStartedDelegate(object sender, int chunkCount);
		public delegate void ChunkMergingProgressDelegate(object sender, int chunkId,
			int chunkCount, long chunkPosition, long chunkSize);
		public delegate void ChunkMergingFinishedDelegate(object sender, int errorCode);
		public delegate void MovingFileToDestinationDelegate(object sender, long bytesTransferred, long fileSize, string destinationFilePath,
			char sourceDriveLetter, char destinationDriveLetter);

		public delegate void TaskPreparingDelegate(object sender, DownloadableTask task);
		public delegate void TaskHeadersReceivingDelegate(object sender, DownloadableTask task,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit);
		public delegate void TaskHeadersReceivedDelegate(object sender, DownloadableTask task, WebHeaderCollection headers,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit, int errorCode);
		public delegate void TaskConnectingDelegate(object sender, DownloadableTask task,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit);
		public delegate int TaskConnectedDelegate(object sender, DownloadableTask task,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit, long innerContentLength, int errorCode);
		public delegate void TaskStartedDelegate(object sender, DownloadableTask task, long innerContentLength,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit);
		public delegate void TaskProgressDelegate(object sender, DownloadableTask task,
			long bytesTransferred, long innerContentLength,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit);
		public delegate void TaskErrorDelegate(object sender, DownloadableTask task, int errorCode, string errorMessage,
			long bytesTransferred, long innerContentLength,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit);
		public delegate void TaskFinishedDelegate(object sender, DownloadableTask task,
			long bytesTransferred, long innerContentLength,
			int innerTryNumber, int innerTryCountLimit, int taskTryNumber, int taskTryCountLimit, int errorCode);

		public PreparingDelegate Preparing;
		public ConnectingDelegate Connecting;
		public ConnectedDelegate Connected;
		public DownloadStartedDelegate DownloadStarted;
		public DownloadProgressDelegate DownloadProgress;
		public ChunksDownloadedDelegate ChunksDownloaded;
		public DownloadFinishedDelegate DownloadFinished;
		public ChunkMergingStartedDelegate ChunkMergingStarted;
		public ChunkMergingProgressDelegate ChunkMergingProgress;
		public ChunkMergingFinishedDelegate ChunkMergingFinished;
		public MovingFileToDestinationDelegate MovingFileToDestination;

		public TaskPreparingDelegate TaskPreparing;
		public TaskHeadersReceivingDelegate TaskHeadersReceiving;
		public TaskHeadersReceivedDelegate TaskHeadersReceived;
		public TaskConnectingDelegate TaskConnecting;
		public TaskConnectedDelegate TaskConnected;
		public TaskStartedDelegate TaskStarted;
		public TaskProgressDelegate TaskProgress;
		public TaskErrorDelegate TaskError;
		public TaskFinishedDelegate TaskFinished;

		public void Dispose()
		{
			if (!_isDisposed)
			{
				_isDisposed = true;
				Abort();
			}
		}

		/// <summary>
		/// Запустить процесс скачивания.
		/// </summary>
		/// <param name="outputStream">
		/// Поток для сохранения данных. Должен быть доступен для записи!
		/// Если передать 'null', будет автоматически создан файл по пути, указанному в свойстве 'OutputFileName'.
		/// Если файл с таким именем уже существует, будет создан файл с пронумерованным именем.
		/// </param>
		/// <param name="accurateMode">
		/// Если 'true' - блокирует внутренний список потоков перед доступом к нему.
		/// Это позволяет предотвратить потерю скачанных сегментов файла,
		/// которая иногда случается (по неизвестной причине). Однако, включение
		/// данного режима может немного увеличить нагрузку системы и/или занизить скорость скачивания (но это не точно!).
		/// Нужно найти другое решение данной проблемы.
		/// </param>
		/// <param name="bufferSize">
		/// Размер буфера при скачивании.
		/// Внимание! Если используется больше одного потока, то значения меньше 8192 не рекомендуются!
		/// Установите значение '0' для автоматического выбора.
		/// </param>>
		public int Download(Stream outputStream, bool accurateMode, int bufferSize = 0)
		{
			IsActive = true;
			Preparing?.Invoke(this);

			_isAborted = _isCanceled = false;
			LastErrorMessage = null;
			DownloadedBytes = 0L;
			IsCompressedContent = false;
			ContentCompressionAlgorithm = string.Empty;

			if (string.IsNullOrEmpty(Url) || string.IsNullOrWhiteSpace(Url))
			{
				LastErrorCode = DOWNLOAD_ERROR_NO_URL_SPECIFIED;
				IsActive = false;
				return DOWNLOAD_ERROR_NO_URL_SPECIFIED;
			}

			bool isMemoryStream = outputStream != null && outputStream is MemoryStream;
			bool isTempDirectoryProvided = !string.IsNullOrEmpty(TempDirectory) && !string.IsNullOrWhiteSpace(TempDirectory);
			if (!UseRamForTempFiles && !FakeDownloading && !isMemoryStream &&
				(!isTempDirectoryProvided || !Directory.Exists(TempDirectory)))
			{
				LastErrorCode = DOWNLOAD_ERROR_CUSTOM;
				LastErrorMessage = "Не указана или недоступна папка для временных файлов!";
				IsActive = false;
				return LastErrorCode;
			}

			bool isFakeDownloading = FakeDownloading;
			bool isSharedStream = outputStream != null;
			if (!isSharedStream && !isFakeDownloading)
			{
				if (string.IsNullOrEmpty(OutputFileName) || string.IsNullOrWhiteSpace(OutputFileName))
				{
					LastErrorCode = DOWNLOAD_ERROR_NO_FILE_NAME_SPECIFIED;
					IsActive = false;
					return DOWNLOAD_ERROR_NO_FILE_NAME_SPECIFIED;
				}
				if (!UseRamForTempFiles && IsTempDirectoryAvailable && !Directory.Exists(TempDirectory))
				{
					LastErrorCode = DOWNLOAD_ERROR_TEMPORARY_DIR_NOT_EXISTS;
					IsActive = false;
					return DOWNLOAD_ERROR_TEMPORARY_DIR_NOT_EXISTS;
				}

				string dirName = Path.GetDirectoryName(OutputFileName);
				if (string.IsNullOrEmpty(dirName) || string.IsNullOrWhiteSpace(dirName))
				{
					string selfDirPath = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
					OutputFileName = Path.Combine(selfDirPath, OutputFileName);
				}
				if (!IsTempDirectoryAvailable)
				{
					TempDirectory = Path.GetDirectoryName(OutputFileName);
				}

				List<char> driveLetters = GetUsedDriveLetters();
				if (driveLetters.Count > 0 && !driveLetters.Contains('\\') && !IsDrivesReady(driveLetters))
				{
					IsActive = false;
					return DOWNLOAD_ERROR_DRIVE_NOT_READY;
				}
			}

			_cancellationTokenSource = new CancellationTokenSource();

			int headersReceivingTryNumber = 0;
			int taskTryCountLimit = TryCountLimitPerThread;
			bool isInfiniteRetries = taskTryCountLimit <= 0;
			Stopwatch stopwatch = new Stopwatch();

			WebHeaderCollection unrangedHeaders = GetUnrangedHttpHeaders(Headers);
			WebHeaderCollection responseHeaders = null;
			while (true)
			{
				stopwatch.Restart();
				headersReceivingTryNumber++;
				Connecting?.Invoke(this, Url, headersReceivingTryNumber, taskTryCountLimit);
				LastErrorCode = GetUrlResponseHttpHeaders(HeaderRequestMethod, Url, unrangedHeaders, Cookies, Proxy, ConnectionTimeout,
					out responseHeaders, out string headersErrorMessage);
				
				if (_cancellationTokenSource.IsCancellationRequested)
				{
					stopwatch.Stop();
					LastErrorCode = GetCancellationErrorCode();
					LastErrorMessage = LastErrorCode != DOWNLOAD_ERROR_UNDEFINED ? null : "Aborted by unknown reason";
					IsActive = false;
					return LastErrorCode;
				}
				else if (LastErrorCode == 200 || LastErrorCode == 206) { break; }
				else if (!isInfiniteRetries && headersReceivingTryNumber + 1 > taskTryCountLimit)
				{
					stopwatch.Stop();
					LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
					LastErrorMessage = "Не удалось получить HTTP-заголовки!";
					DownloadFinished?.Invoke(this, DownloadedBytes, -1L, LastErrorCode, OutputFileName, null);
					IsActive = false;
					return LastErrorCode;
				}

				if (isInfiniteRetries || !isInfiniteRetries && headersReceivingTryNumber < taskTryCountLimit)
				{
					TimeSpan interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
					TimeSpan elapsed = stopwatch.Elapsed;
					if (elapsed < interval)
					{
						TimeSpan difference = interval - elapsed;
#if DEBUG
						Debug.WriteLine($"Receiving headers: {(int)difference.TotalMilliseconds} milliseconds until next try...");
#endif
						Thread.Sleep(difference);
					}
				}
			}
			stopwatch.Stop();

			IsCompressedContent = Utils.IsCompressedContent(responseHeaders, out string algorithmId);
			ContentCompressionAlgorithm = algorithmId;

			if (IsCompressedContent || !IsRangeSupported(responseHeaders))
			{
				LastErrorMessage = "Невозможно начать скачивание, так как HTTP-заголовок \"Range\" " +
					"не поддерживается сервером и/или контент является сжатым! Используйте класс \"FileDownloader\".";
				LastErrorCode = DOWNLOAD_ERROR_ABORTED;
				DownloadFinished?.Invoke(this, DownloadedBytes, -1L, LastErrorCode, null, null);
				IsActive = false;
				return LastErrorCode;
			}

			ExtractRangeFromHttpHeaders(Headers, out long rangeFrom, out long rangeTo, out _);
			ExtractContentLengthFromHttpHeaders(responseHeaders, out long fullContentLength);
			ContentLength = fullContentLength == -1L ? -1L :
				(rangeTo >= 0L ? rangeTo - rangeFrom + 1 : fullContentLength - rangeFrom);
			if (fullContentLength < 0L || ContentLength < 0L) { ContentLength = -1L; }

			if (fullContentLength <= 0L)
			{
				LastErrorMessage = "Невозможно начать скачивание, так как размер скачиваемых данных не определён!";
				LastErrorCode = DOWNLOAD_ERROR_ABORTED;
				DownloadFinished?.Invoke(this, DownloadedBytes, fullContentLength, LastErrorCode, null, null);
				IsActive = false;
				return LastErrorCode;
			}

			if (!DownloadRange.IsValidRange(rangeFrom, rangeTo, fullContentLength))
			{
				LastErrorCode = DOWNLOAD_ERROR_RANGE;
				DownloadFinished?.Invoke(this, DownloadedBytes, fullContentLength, LastErrorCode, null, null);
				IsActive = false;
				return LastErrorCode;
			}

			CustomError customError = new CustomError(LastErrorCode, null);
			Connected?.Invoke(this, Url, ContentLength, responseHeaders,
				headersReceivingTryNumber, taskTryCountLimit, customError);
			if (LastErrorCode != customError.ErrorCode)
			{
				LastErrorCode = customError.ErrorCode;
			}
			if (LastErrorCode != 200 && LastErrorCode != 206)
			{
				LastErrorMessage = customError.ErrorMessage;
				IsActive = false;
				return LastErrorCode;
			}
			if (ContentLength == 0L)
			{
				LastErrorCode = DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
				IsActive = false;
				return DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT;
			}

			DownloadStarted?.Invoke(this, ContentLength);

			ConcurrentDictionary<int, DownloadableTask> downloadableTaskDictionary = new ConcurrentDictionary<int, DownloadableTask>();

			void OnProgressUpdatedFunc(DownloadableTask downloadableTask)
			{
				if (accurateMode && ThreadCount > 1)
				{
					lock (downloadableTaskDictionary)
					{
						downloadableTaskDictionary[downloadableTask.TaskId] = downloadableTask;
						DownloadedBytes = downloadableTaskDictionary.Sum(item => item.Value.ProcessedBytes);
					}
				}
				else
				{
					downloadableTaskDictionary[downloadableTask.TaskId] = downloadableTask;
					DownloadedBytes = downloadableTaskDictionary.Sum(item => item.Value.ProcessedBytes);
				}

				DownloadProgress?.Invoke(this, downloadableTaskDictionary);
			}

			DownloadableTask MakeDownloadableTaskFunc(FileDownloader fd, long processedBytes,
				int tryNumber, DownloadableTaskState state, bool callProgressUpdaterFunction)
			{
				DownloadableChunk downloadableChunk = null;
				if (state != DownloadableTaskState.Preparing)
				{
					fd.GetRange(out DownloadRange range);
					downloadableChunk = new DownloadableChunk(fd.DownloadableChunk.OutputStream, range);
				}
				DownloadableTask downloadableTask = new DownloadableTask(fd.Url, downloadableChunk, fd.Id,
					fullContentLength, processedBytes, tryNumber, taskTryCountLimit, state);
				if (callProgressUpdaterFunction) { OnProgressUpdatedFunc(downloadableTask); }
				return downloadableTask;
			}

			bool isOutOfTries = false;
			bool isExceptionRaised = false;
			bool isHeadersReceived = false;

			List<FileDownloader> downloaders = new List<FileDownloader>();
			int predictedChunkCount = ContentLength > ONE_MEGABYTE ? ThreadCount : 1;
			var chunkRanges = SplitContentToChunks(fullContentLength, rangeFrom, rangeTo, predictedChunkCount);
			int chunkCount = chunkRanges.Count();
			if (bufferSize == 0) { bufferSize = chunkCount > 1 ? 8192 : 4096; }
			ThreadCount = chunkCount;
			for (int i = 0; i < chunkCount; ++i)
			{
				downloadableTaskDictionary[i] = new DownloadableTask(Url, null, i, fullContentLength,
					0L, -1, taskTryCountLimit, DownloadableTaskState.Preparing);
			}

			var tasks = chunkRanges.Select((taskDownloadRange, taskId) => Task.Run(() =>
			{
				string chunkFileName = null;
				if (!UseRamForTempFiles && !isFakeDownloading)
				{
					chunkFileName = GetNumberedFileName(FormatChunkTempFilePath(chunkCount,
						taskDownloadRange.StartPosition, taskDownloadRange.EndPosition, fullContentLength));
					if (string.IsNullOrEmpty(chunkFileName) || string.IsNullOrWhiteSpace(chunkFileName))
					{
						LastErrorCode = DOWNLOAD_ERROR_FILE_NUMBERING;
						return;
					}
				}

				int taskTryNumber = 0;

				DependentTaskInfo dti = new DependentTaskInfo(this,
					taskDownloadRange.Length, true, ContentCompressionAlgorithm);
				FileDownloader downloader = new FileDownloader(dti, taskId)
				{
					Url = Url,
					ConnectionTimeout = ConnectionTimeout,
					Headers = CopyHttpHeaders(unrangedHeaders),
					Cookies = Cookies,
					Proxy = Proxy,
					SkipHeaderRequest = true,
					TryCountLimit = TryCountLimitInsideThread,
					RetryIntervalMilliseconds = RetryIntervalMilliseconds,
					FakeDownloading = FakeDownloading
				};
				lock (downloaders) { downloaders.Add(downloader); }

				#region Downloader event handlers
				downloader.Preparing += (sender, url, downloadableChunk) =>
				{
					FileDownloader fd = sender as FileDownloader;
#if DEBUG
					Debug.WriteLine($"Task №{fd.Id}: Preparing...");
#endif
					DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
						fd, -1L, taskTryNumber, DownloadableTaskState.Preparing, false);
					TaskPreparing?.Invoke(this, downloadableTask);
				};
				downloader.HeadersReceiving += (sender, url, downloadableChunk, tryNumber, tryCountLimit) =>
				{
					FileDownloader fd = sender as FileDownloader;
#if DEBUG
					bool infiniteThreadRetries = tryCountLimit <= 0;
					string msg = $"Task №{fd.Id}: Receiving headers... Try №{tryNumber}";
					if (!infiniteThreadRetries) { msg += $" / {tryCountLimit}"; }
					Debug.WriteLine(msg);
#endif
					DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
						fd, -1L, taskTryNumber, DownloadableTaskState.Preparing, false);
					TaskHeadersReceiving?.Invoke(this, downloadableTask,
						tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit);
				};
				downloader.HeadersReceived += (sender, url, downloadableChunk, headers,
					tryNumber, tryCountLimit, errCode) =>
				{
					FileDownloader fd = sender as FileDownloader;
#if DEBUG
					bool infiniteThreadRetries = tryCountLimit <= 0;
					int id = (sender as FileDownloader).Id;
					string msg = errCode == 200 || errCode == 206 ?
						$"Task №{id}: Headers are received OK with try №{tryNumber}" :
						$"Task №{id}: Headers not received! Try №{tryNumber}";
					if (!infiniteThreadRetries) { msg += $" / {tryCountLimit}"; }
					Debug.WriteLine(msg);
#endif
					DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
						fd, -1L, taskTryNumber, DownloadableTaskState.Preparing, false);
					TaskHeadersReceived?.Invoke(this, downloadableTask, headers,
						tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit, errCode);
				};
				downloader.Connecting += (sender, url, tryNumber, tryCountLimit) =>
				{
					FileDownloader d = sender as FileDownloader;
					DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
						d, -1L, taskTryNumber, DownloadableTaskState.Connecting, true);
					TaskConnecting?.Invoke(this, downloadableTask,
						tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit);
				};
				downloader.Connected += (sender, url, contentLength,
					headers, tryNumber, tryCountLimit, errCode) =>
				{
					FileDownloader d = sender as FileDownloader;
					lock (ContentCompressionAlgorithm)
					{
						if (!isHeadersReceived)
						{
							isHeadersReceived = true;
							IsCompressedContent = d.IsCompressedContent;
							ContentCompressionAlgorithm = d.ContentCompressionAlgorithm;
						}
					}

					DownloadableTaskState state = errCode == 200 || errCode == 206 ?
						DownloadableTaskState.Connected : DownloadableTaskState.Errored;
					DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
						d, 0L, taskTryNumber, state, true);
					if (TaskConnected != null)
					{
						errCode = TaskConnected.Invoke(this, downloadableTask,
							tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit, contentLength, errCode);
					}

					return errCode;
				};
				downloader.WorkStarted += (sender, contentLength, tryNumber, tryCountLimit) =>
				{
					FileDownloader fd = sender as FileDownloader;
					DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
						fd, 0L, taskTryNumber, DownloadableTaskState.Downloading, true);
					TaskStarted?.Invoke(this, downloadableTask, contentLength,
						tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit);
				};

				int lastTime = Environment.TickCount;
				downloader.WorkProgress += (sender, transferred, contentLength, tryNumber, tryCountLimit) =>
				{
					int currentTime = Environment.TickCount;
					if (currentTime - lastTime >= UpdateIntervalMilliseconds)
					{
						FileDownloader d = sender as FileDownloader;
						DownloadableTask downloadableTask = MakeDownloadableTaskFunc(
							d, transferred, taskTryNumber, DownloadableTaskState.Downloading, true);

						OnProgressUpdatedFunc(downloadableTask);
						TaskProgress?.Invoke(this, downloadableTask, transferred, contentLength,
							tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit);

						lastTime = currentTime;
					}
				};
				downloader.WorkFinished += (sender, transferred, contentLength, tryNumber, tryCountLimit, errCode) =>
				{
					DownloadableTaskState taskState;
					FileDownloader d = sender as FileDownloader;
					if (errCode != 200 && errCode != 206 && !isExceptionRaised && !_isCanceled)
					{
						lock (downloaders)
						{
							if (!isOutOfTries)
							{
								isOutOfTries = !isInfiniteRetries && taskTryNumber + 1 > taskTryCountLimit;
								if (isOutOfTries)
								{
#if DEBUG
									Debug.WriteLine($"Task №{d.Id}: Out of tries! Aborting all tasks...");
#endif
									Abort();
								}
							}
						}

						taskState = DownloadableTaskState.Errored;
					}
					else
					{
						taskState = DownloadableTaskState.Finished;
					}

					d.GetRange(out DownloadRange range);
					DownloadableChunk downloadableChunk = new DownloadableChunk(d.DownloadableChunk.OutputStream, range);
					DownloadableTask downloadableTask = new DownloadableTask(d.Url,
						downloadableChunk, d.Id, fullContentLength, transferred, taskTryNumber, tryCountLimit, taskState);
					OnProgressUpdatedFunc(downloadableTask);

					TaskFinished?.Invoke(this, downloadableTask, transferred, contentLength,
						tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit, errCode);
				};
				downloader.WorkError += (sender, errCode, errorMessage,
					transferred, contentLength, tryNumber, tryCountLimit) =>
				{
					if (errCode != 200 && errCode != 206)
					{
						FileDownloader d = sender as FileDownloader;
						d.GetRange(out DownloadRange range);
						DownloadableChunk downloadableChunk = new DownloadableChunk(d.DownloadableChunk.OutputStream, range);
						DownloadableTask downloadableTask = new DownloadableTask(d.Url,
							downloadableChunk, d.Id, fullContentLength, transferred,
							taskTryNumber, taskTryCountLimit, DownloadableTaskState.Errored);
						OnProgressUpdatedFunc(downloadableTask);

						TaskError?.Invoke(this, downloadableTask, errCode, errorMessage, transferred, contentLength,
							tryNumber, tryCountLimit, taskTryNumber, taskTryCountLimit);
					}
				};
#endregion

				while (true)
				{
					try
					{
						taskTryNumber++;
						if (!isInfiniteRetries && taskTryNumber > taskTryCountLimit)
						{
							lock (downloaders)
							{
								if (!isOutOfTries)
								{
									isOutOfTries = true;
#if DEBUG
									Debug.WriteLine($"Task №{taskId}: Out of tries! Aborting all tasks...");
#endif
									Abort();
								}
							}

							return;
						}
#if DEBUG
						string tryMessage = $"Task №{taskId}: Try №{taskTryNumber}";
						if (!isInfiniteRetries)
						{
							tryMessage += $" / {taskTryCountLimit}";
						}
						Debug.WriteLine(tryMessage);
#endif
						if (!GetChunkStream(downloader, taskDownloadRange, chunkFileName,
							UseRamForTempFiles, isFakeDownloading, out Stream streamChunk))
						{
							Abort();
							return;
						}

						downloader.SetRange(taskDownloadRange);

						ContentChunkStream chunkStream = new ContentChunkStream(
							streamChunk, UseRamForTempFiles || isFakeDownloading ? null : chunkFileName);
						LastErrorCode = downloader.Download(chunkStream, bufferSize, _cancellationTokenSource);

						if (LastErrorCode == 200 || LastErrorCode == 206)
						{
							break;
						}
						downloader.DisposeOutputStream();
						if (UseRamForTempFiles) { GC.Collect(); }

						if (_isCanceled || _isAborted || isOutOfTries) { break; }
#if DEBUG
						else
						{
							Debug.WriteLine($"Task №{downloader.Id}: Restarting...");
						}
#endif
					}
					catch (Exception ex)
					{
#if DEBUG
						Debug.WriteLine($"Task №{downloader.Id} catches exception while try №{taskTryNumber}!\n{ex.Message}");
#endif
						LastErrorCode = DOWNLOAD_ERROR_ABORTED;
						LastErrorMessage = ex.Message;
						isExceptionRaised = true;
#if DEBUG
						Debug.WriteLine($"Task №{downloader.Id}: Aborting all tasks...");
#endif
						Abort();
						break;
					}
				}

				if (LastErrorCode != 200 && LastErrorCode != 206)
				{
					if (_isCanceled)
					{
						LastErrorCode = DOWNLOAD_ERROR_CANCELED;
						LastErrorMessage = null;
					}
					else if (isOutOfTries)
					{
						LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
						LastErrorMessage = null;
					}
					else if (!isExceptionRaised)
					{
						LastErrorMessage = downloader.LastErrorMessage;
					}
				}
			}
			));

			try
			{
				Task.WhenAll(tasks).Wait();
			}
			catch (Exception ex)
			{
#if DEBUG
				Debug.WriteLine(ex.Message);
#endif
				LastErrorMessage = ex.Message;
				Abort();
				ClearGarbage(downloadableTaskDictionary);
				_cancellationTokenSource.Dispose();
				_cancellationTokenSource = null;
				LastErrorCode = (ex is OperationCanceledException) ? DOWNLOAD_ERROR_CANCELED : DOWNLOAD_ERROR_ABORTED;
				var downloadableTasks = downloadableTaskDictionary?.Select(item => item.Value);
				DownloadFinished?.Invoke(this, DownloadedBytes, ContentLength, LastErrorCode, OutputFileName, downloadableTasks);
				downloadableTasks = null;
				IsActive = false;
				return LastErrorCode;
			}

			downloaders = null;
			if (!_isCanceled && (LastErrorCode == 200 || LastErrorCode == 206))
			{
				List<DownloadableChunk> downloadableChunks = BuildChunkSequence(downloadableTaskDictionary, chunkCount, out bool isValidChunkSequence);
				if (downloadableChunks != null && ChunksDownloaded != null && isValidChunkSequence)
				{
					customError = ChunksDownloaded.Invoke(this, downloadableChunks, ContentLength);
					if (customError != null && customError.ErrorCode != 200)
					{
						ClearGarbage(downloadableTaskDictionary);
						LastErrorCode = customError.ErrorCode;
						LastErrorMessage = customError.ErrorMessage;
						_cancellationTokenSource.Dispose();
						_cancellationTokenSource = null;
						var downloadableTasks = downloadableTaskDictionary?.Select(item => item.Value);
						DownloadFinished?.Invoke(this, DownloadedBytes, ContentLength, LastErrorCode, OutputFileName, downloadableTasks);
						downloadableTasks = null;
						IsActive = false;
						return LastErrorCode;
					}
				}

				if (!isFakeDownloading)
				{
					if (!isValidChunkSequence || downloadableChunks == null || downloadableChunks.Count <= 0)
					{
						downloadableTaskDictionary = null;
						if (UseRamForTempFiles && downloadableChunks != null) { ClearGarbage(downloadableChunks); }
						LastErrorCode = DOWNLOAD_ERROR_CHUNK_SEQUENCE;
						LastErrorMessage = null;
						_cancellationTokenSource.Dispose();
						_cancellationTokenSource = null;
						var downloadableTasks = downloadableTaskDictionary?.Select(item => item.Value);
						DownloadFinished?.Invoke(this, DownloadedBytes, ContentLength, LastErrorCode, OutputFileName, downloadableTasks);
						downloadableTasks = null;
						IsActive = false;
						return LastErrorCode;
					}

					downloadableTaskDictionary = null;

					if (MergeChunksAutomatically)
					{
						if (UseRamForTempFiles || downloadableChunks.Count > 1)
						{
							bool canMerge = true;
							if (!isMemoryStream && !IsTempDirectoryAvailable)
							{
								LastErrorCode = DOWNLOAD_ERROR_TEMPORARY_DIR_NOT_EXISTS;
								LastErrorMessage = null;
								canMerge = false;
							}

							if (canMerge)
							{
								ChunkMergingStarted?.Invoke(this, downloadableChunks.Count);
								LastErrorCode = MergeChunks(downloadableChunks, outputStream);
								ChunkMergingFinished?.Invoke(this, LastErrorCode);
							}
						}
						else if (!UseRamForTempFiles && downloadableChunks.Count == 1)
						{
							try
							{
								string chunkFilePath = downloadableChunks[0].OutputStream.FilePath;
								if (!string.IsNullOrEmpty(chunkFilePath) && !string.IsNullOrWhiteSpace(chunkFilePath) &&
									File.Exists(chunkFilePath))
								{
									OutputFileName = GetNumberedFileName(OutputFileName);
									if (IsSameLogicalDrive(OutputFileName, chunkFilePath))
									{
										File.Move(chunkFilePath, OutputFileName);
										LastErrorCode = 200;
										LastErrorMessage = null;
									}
									else
									{
										void func(long sourcePosition, long sourceLength, long destinationPosition, long destinationLength, long bytesTransferred)
										{
											DownloadedBytes = bytesTransferred;
											MovingFileToDestination?.Invoke(this, sourcePosition, sourceLength, chunkFilePath,
												char.ToUpper(chunkFilePath[0]), char.ToUpper(OutputFileName[0]));
										};

										using (Stream destinationStream = File.OpenWrite(OutputFileName))
										{
											using (Stream inputStream = downloadableChunks[0].OutputStream.Stream ??
												File.OpenRead(chunkFilePath))
											{
												inputStream.Position = DownloadedBytes = 0L;
												Append(inputStream, destinationStream,
													(sourcePosition, sourceLength, destinationPosition, destinationLength) =>
													{
														DownloadedBytes = 0L;
														MovingFileToDestination?.Invoke(this, sourcePosition, sourceLength, chunkFilePath,
															char.ToUpper(chunkFilePath[0]), char.ToUpper(OutputFileName[0]));
													}, func, func,
													_cancellationTokenSource.Token, UpdateIntervalMilliseconds);
											}
										}

										if (!_cancellationTokenSource.IsCancellationRequested)
										{
											File.Delete(chunkFilePath);
											LastErrorCode = 200;
											LastErrorMessage = null;
										}
										else
										{
											LastErrorCode = _isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED;
											LastErrorMessage = "Финальное перемещение файла было прервано";
										}
									}
								}
								else
								{
									LastErrorCode = 400;
								}
							} catch (Exception ex)
							{
								LastErrorCode = DOWNLOAD_ERROR_FINAL_FILE_MOVE;
								LastErrorMessage = ex.Message;
							}
						}
						else
						{
							LastErrorCode = 400;
						}
					}
				}

				if (downloadableChunks != null)
				{
					ClearGarbage(downloadableChunks);
					downloadableChunks = null;
				}
			} else if (_isCanceled)
			{
				LastErrorCode = DOWNLOAD_ERROR_CANCELED;
				LastErrorMessage = null;
			} else if (isOutOfTries)
			{
				LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
				LastErrorMessage = null;
			}

			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;

			var downloadableTasksFinal = downloadableTaskDictionary?.Select(item => item.Value);
			DownloadFinished?.Invoke(this, DownloadedBytes, ContentLength, LastErrorCode, OutputFileName, downloadableTasksFinal);
			downloadableTasksFinal = null;

			if (downloadableTaskDictionary != null)
			{
				ClearGarbage(downloadableTaskDictionary);
				downloadableTaskDictionary = null;
			}

			IsActive = false;
			return LastErrorCode;
		}

		public int Download(bool accurateMode, int bufferSize = 0)
		{
			return Download(null, accurateMode, bufferSize);
		}

		public int Download(int bufferSize = 0)
		{
			return Download(null, false, bufferSize);
		}

		public bool Stop()
		{
			if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
				_isAborted = false;
				_isCanceled = true;
				return true;
			}

			return false;
		}

		public bool Abort()
		{
			bool b = Stop();
#if DEBUG
			if (b)
			{
				Debug.WriteLine("All tasks is aborted!");
			}
#endif
			_isCanceled = false;
			_isAborted = true;
			return b;
		}

		private int GetCancellationErrorCode()
		{
			if (_isAborted) { return DOWNLOAD_ERROR_ABORTED; }
			else if (_isCanceled) { return DOWNLOAD_ERROR_CANCELED; }
			return DOWNLOAD_ERROR_UNDEFINED;
		}

		private bool GetChunkStream(FileDownloader downloader, DownloadRange range,
			string chunkFileName,
			bool useRamForTempFiles, bool isFakeDownloading, out Stream outputStream)
		{
			if (useRamForTempFiles || isFakeDownloading)
			{
				downloader.DisposeOutputStream();
				GC.Collect();
				outputStream = isFakeDownloading ? null : new MemoryStream();
			}
			else
			{
				long bytesNeeded = range.Length + ONE_MEGABYTE;
				if (!IsEnoughDiskSpace(chunkFileName[0], bytesNeeded, out string errorMsg))
				{
					LastErrorCode = DOWNLOAD_ERROR_ABORTED;
					LastErrorMessage = errorMsg;
					outputStream = null;
					return false;
				}

				outputStream = File.Open(chunkFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
				outputStream.Position = 0L;
			}

			return true;
		}

		private int MergeChunks(IEnumerable<DownloadableChunk> downloadableChunks, Stream outputStream)
		{
			bool isSharedStream = outputStream != null;
			string tmpFileName = !isSharedStream ? GetNumberedFileName($"{OutputFileName}.tmp") : null;
			if (!isSharedStream)
			{
				if (string.IsNullOrEmpty(tmpFileName) || string.IsNullOrWhiteSpace(tmpFileName))
				{
					LastErrorCode = DOWNLOAD_ERROR_FILE_NUMBERING;
					LastErrorMessage = null;
					outputStream?.Dispose();
					return LastErrorCode;
				}

				try
				{
					outputStream = File.OpenWrite(tmpFileName);
				}
#if DEBUG
				catch (Exception ex)
				{
					Debug.WriteLine(ex.Message);
#else
				catch
				{
#endif
					if (!isSharedStream && outputStream != null) { outputStream.Dispose(); }
					ClearGarbage(downloadableChunks);
					return DOWNLOAD_ERROR_CREATE_FILE;
				}
			}

			try
			{
				DownloadedBytes = 0L;
				int chunkCount = downloadableChunks.Count();
				long[] chunkMergeProgresses = new long[chunkCount];

				int i = 0;
				foreach (DownloadableChunk downloadableChunk in downloadableChunks)
				{
					void updateProgressFunc(long chunkPosition, long chunkSize)
					{
						chunkMergeProgresses[i] = chunkPosition;
						DownloadedBytes = chunkMergeProgresses.Sum();
						ChunkMergingProgress?.Invoke(this, i, chunkCount, chunkPosition, chunkSize);
					};

					void func(long sourcePosition, long sourceLength, long destinationPosition, long destinationLength, long bytesTransferred)
					{
						updateProgressFunc(bytesTransferred, sourceLength);
					};

					downloadableChunk.OutputStream.Stream.Position = 0L;
					bool appended = Append(downloadableChunk.OutputStream.Stream, outputStream,
						(sourcePosition, sourceLength, destinationPosition, destinationLength) =>
							updateProgressFunc(0L, sourceLength)
						, func, func,
						_cancellationTokenSource.Token, ChunksMergingUpdateIntervalMilliseconds);

					bool isMemoryStream = downloadableChunk.OutputStream.Stream != null && downloadableChunk.OutputStream.Stream is MemoryStream;
					downloadableChunk.OutputStream.Dispose();
					if (isMemoryStream)
					{
						//TODO: Fix possible memory leaking
						GC.Collect();
					}

					if (!appended)
					{
						if (!isSharedStream && outputStream != null) { outputStream.Dispose(); }
						ClearGarbage(downloadableChunks);
						return !_cancellationTokenSource.IsCancellationRequested ? DOWNLOAD_ERROR_MERGING_CHUNKS :
							(_isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED);
					}

					if (!isMemoryStream &&
						!string.IsNullOrEmpty(downloadableChunk.OutputStream.FilePath) &&
						!string.IsNullOrWhiteSpace(downloadableChunk.OutputStream.FilePath) &&
						File.Exists(downloadableChunk.OutputStream.FilePath))
					{
						File.Delete(downloadableChunk.OutputStream.FilePath);
					}

					if (_isCanceled) { break; }

					++i;
				}
			}
#if DEBUG
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
#else
			catch
			{
#endif
				if (!isSharedStream && outputStream != null) { outputStream.Dispose(); }
				ClearGarbage(downloadableChunks);
				return DOWNLOAD_ERROR_MERGING_CHUNKS;
			}

			if (!isSharedStream && outputStream != null) { outputStream.Dispose(); }

			if (_isCanceled)
			{
				ClearGarbage(downloadableChunks);
				return DOWNLOAD_ERROR_CANCELED;
			}

			if (!isSharedStream)
			{
				string outputFn = GetNumberedFileName(OutputFileName);
				if (string.IsNullOrEmpty(outputFn) || string.IsNullOrWhiteSpace(outputFn))
				{
					ClearGarbage(downloadableChunks);
					LastErrorCode = DOWNLOAD_ERROR_FILE_NUMBERING;
					LastErrorMessage = null;
					return LastErrorCode;
				}
				OutputFileName = outputFn;

				if (!string.IsNullOrEmpty(tmpFileName) &&
					!string.IsNullOrWhiteSpace(tmpFileName))
				{
					try
					{
						if (File.Exists(tmpFileName))
						{
							File.Move(tmpFileName, OutputFileName);
						}
					}
#if DEBUG
					catch (Exception ex)
					{
						Debug.WriteLine(ex.Message);
#else
					catch
					{
#endif
						return DOWNLOAD_ERROR_MERGING_CHUNKS;
					}
				}
			}

			return 200;
		}

		private void ClearGarbage(ConcurrentDictionary<int, DownloadableTask> dictionary)
		{
			var tasks = dictionary.Values.Where(item => item.DownloadableChunk != null).Select(item => item.DownloadableChunk);
			ClearGarbage(tasks);
		}

		private void ClearGarbage(IEnumerable<DownloadableChunk> downloadableChunks)
		{
			var chunks = downloadableChunks.Where(item => item.OutputStream != null).Select(item => item.OutputStream);
			ClearGarbage(chunks);
		}

		private void ClearGarbage(IEnumerable<ContentChunkStream> contentChunkStreams)
		{
			foreach (ContentChunkStream chunk in contentChunkStreams)
			{
				chunk.Dispose();
			}

			if (UseRamForTempFiles)
			{
				//TODO: Fix possible memory leaking
				GC.Collect();
			}
		}

		private string FormatChunkTempFilePath(int chunkCount, long byteStart, long byteEnd, long fileSize = -1L)
		{
			string fn = Path.GetFileName(OutputFileName);
			string fnRange = FormatChunkTempFileNameRange(byteStart, byteEnd, fileSize);
			string suffix = $".chunk{fnRange}.tmp";

			string chunkFileName;
			if (chunkCount > 1)
			{
				chunkFileName = IsTempDirectoryAvailable ?
					Path.Combine(TempDirectory, fn + suffix) : fn + suffix;
			}
			else if (IsTempDirectoryAvailable)
			{
				chunkFileName = Path.Combine(TempDirectory, fn + suffix);
			}
			else
			{
				chunkFileName = $"{OutputFileName}{fnRange}.tmp";
			}

			return chunkFileName;
		}

		private static string FormatChunkTempFileNameRange(long byteStart, long byteEnd, long fileSize = -1L)
		{
			if (byteStart >= 0L && byteEnd >= 0L)
			{
				return fileSize >= 0L ? $"_{byteStart}-{byteEnd}={fileSize}=" : $"_{byteStart}-{byteEnd}";
			}
			else if (byteStart < 0L && byteEnd >= 0L)
			{
				return fileSize >= 0L ? $"_0-{byteEnd}={fileSize}=" : $"_0-{byteEnd}";
			}
			else if (byteStart >= 0L && byteEnd < 0L)
			{
				return fileSize >= 0 ? $"_{byteStart}-{fileSize}={fileSize}=" : $"_{byteStart}-";
			}
			else if (byteStart < 0L && byteEnd < 0L)
			{
				return fileSize >= 0L ? $"_0-{fileSize}={fileSize}=" : "_0-";
			}

			return string.Empty;
		}

		public bool SetRange(DownloadRange downloadRange)
		{
			return SetRange(downloadRange.StartPosition, downloadRange.EndPosition);
		}

		public bool SetRange(long rangeFrom, long rangeTo)
		{
			if (DownloadRange.IsValidRange(rangeFrom, rangeTo))
			{
				if (Headers == null) { Headers = new WebHeaderCollection(); }
				string formattedRange = FormatHttpHeadersRangeValue(rangeFrom, rangeTo);
				Headers["Range"] = formattedRange;
				return true;
			}
			return false;
		}

		public void ResetRange()
		{
			Headers?.Remove(HttpRequestHeader.Range);
		}

		public List<char> GetUsedDriveLetters()
		{
			List<char> driveLetters = new List<char>();
			if (!string.IsNullOrEmpty(OutputFileName) && !string.IsNullOrWhiteSpace(OutputFileName))
			{
				char c = OutputFileName.Length > 2 && OutputFileName[1] == ':' && OutputFileName[2] == '\\' ?
					OutputFileName[0] : Environment.GetCommandLineArgs()[0][0];
				driveLetters.Add(char.ToUpper(c));
			}
			if (IsTempDirectoryAvailable && !driveLetters.Contains(char.ToUpper(TempDirectory[0])))
			{
				driveLetters.Add(char.ToUpper(TempDirectory[0]));
			}
			return driveLetters;
		}

		public bool IsDrivesReady(IEnumerable<char> driveLetters)
		{
			foreach (char driveLetter in driveLetters)
			{
				if (driveLetter == '\\')
				{
					return false;
				}
				DriveInfo driveInfo = new DriveInfo(driveLetter.ToString());
				if (!driveInfo.IsReady)
				{
					return false;
				}
			}
			return true;
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
				case DOWNLOAD_ERROR_NO_URL_SPECIFIED:
					return "Не указана ссылка!";

				case DOWNLOAD_ERROR_NO_FILE_NAME_SPECIFIED:
					return "Не указано имя файла!";

				case DOWNLOAD_ERROR_MERGING_CHUNKS:
					return "Ошибка объединения чанков!";

				case DOWNLOAD_ERROR_CREATE_FILE:
					return "Ошибка создания файла!";

				case DOWNLOAD_ERROR_TEMPORARY_DIR_NOT_EXISTS:
					return "Не найдена папка для временных файлов!";

				case DOWNLOAD_ERROR_FINAL_FILE_MOVE:
					return "Ошибка при финальном перемещении файла!";

				case DOWNLOAD_ERROR_CUSTOM:
					return null;

				case DOWNLOAD_ERROR_CHUNK_SEQUENCE:
					return "Неправильная последовательность чанков!";

				case DOWNLOAD_ERROR_UNDEFINED:
					return "Неопределённая ошибка!";

				case DOWNLOAD_ERROR_FILE_NUMBERING:
					return "Ошибка при нумерации файла!";

				case DOWNLOAD_ERROR_ABORTED:
					return "Скачивание прервано!";

				default:
					return FileDownloader.ErrorCodeToString(errorCode);
			}
		}
	}
}
