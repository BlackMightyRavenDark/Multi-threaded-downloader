using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
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
		/// Warning! The file name will be automatically changed after downloading if a file with that name already exists!
		/// Therefore, you need to double-check this value after the download is complete.
		/// </summary>
		public string OutputFileName { get; set; } = null;

		public string TempDirectory { get; set; } = null;
		public int UpdateIntervalMilliseconds { get; set; } = 100;
		public int RetryIntervalMilliseconds { get; set; } = 1000;
		public int ChunksMergingUpdateIntervalMilliseconds { get; set; } = 100;
		public long DownloadedBytes { get; private set; } = 0L;
		public long ContentLength { get; private set; } = -1L;
		public long RangeFrom { get; private set; } = 0L;
		public long RangeTo { get; private set; } = -1L;
		internal bool IsRangeSupported { get; private set; }

		/// <summary>
		/// Don't save downloaded data to anywhere.
		/// </summary>
		public bool FakeDownloading { get; set; } = false;

		/// <summary>
		/// WARNING!!! Experimental feature!
		/// Must be used very softly and carefully!
		/// </summary>
		public bool UseRamForTempFiles { get; set; } = false;

		public int ThreadCount { get; set; } = 2;

		/// <summary>
		/// Set it to zero or less for infinite retries.
		/// </summary>
		public int TryCountLimitPerThread { get; set; } = 1;

		/// <summary>
		/// Try count inside each download thread.
		/// The thread will be restarted when out of tries.
		/// Set it to zero or less for infinite retries.
		/// </summary>
		public int TryCountLimitInsideThread { get; set; } = 1;

		public bool IsActive { get; private set; }
		public NameValueCollection Headers { get => _headers; set { SetHeaders(value); } }
		public CookieContainer Cookies { get; set; }
		public WebProxy Proxy { get; set; }
		public bool MergeChunksAutomatically { get; set; } = true;
		public int LastErrorCode { get; private set; }
		public string LastErrorMessage { get; private set; }
		public bool IsTempDirectoryAvailable => !string.IsNullOrEmpty(TempDirectory) &&
			!string.IsNullOrWhiteSpace(TempDirectory) && Directory.Exists(TempDirectory);
		public bool HasErrorMessage => HasErrorMessageText();

		private NameValueCollection _headers = new NameValueCollection();
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

		public delegate void PreparingDelegate(object sender);
		public delegate void ConnectingDelegate(object sender, string url, int tryNumber, int tryCountLimit);
		public delegate void ConnectedDelegate(object sender, string url, long contentLength,
			NameValueCollection headers, int tryNumber, int tryCountLimit, CustomError customError);
		public delegate void DownloadStartedDelegate(object sender, long contentLength);
		public delegate void DownloadProgressDelegate(object sender, ConcurrentDictionary<int, DownloadableTask> tasks);
		public delegate CustomError ChunksDownloadedDelegate(object sender, List<DownloadableChunk> downloadableChunks, long contentLength);
		public delegate void DownloadFinishedDelegate(object sender, long bytesTransferred, int errorCode, string fileName);
		public delegate void ChunkMergingStartedDelegate(object sender, int chunkCount);
		public delegate void ChunkMergingProgressDelegate(object sender, int chunkId,
			int chunkCount, long chunkPosition, long chunkSize);
		public delegate void ChunkMergingFinishedDelegate(object sender, int errorCode);
		public delegate void MovingFileToDestinationDelegate(object sender, long bytesTransferred, long fileSize, string destinationFilePath,
			char sourceDriveLetter, char destnationDriveLetter);

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

		public void Dispose()
		{
			if (!_isDisposed)
			{
				_isDisposed = true;
				Abort();
			}
		}

		/// <summary>
		/// Execute the downloading task.
		/// </summary>
		/// <param name="outputStream">
		/// The stream to download to.</param>
		/// <param name="accurateMode">
		/// If 'true' - locks the thread list object before accessing it.
		/// It's prevents losing the downloaded file parts sometimes.
		/// But it's may be some slower.
		/// This is a quick test bugfix. It's must be fixed another way.</param>
		/// <param name="bufferSize">
		/// Buffer size per thread.
		/// Warning! Do not use numbers smaller than 8192!
		/// Leave zero for auto select.</param>
		public int Download(Stream outputStream, bool accurateMode, int bufferSize = 0)
		{
			IsActive = true;
			Preparing?.Invoke(this);

			_isAborted = _isCanceled = false;
			LastErrorMessage = null;
			DownloadedBytes = 0L;

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
			bool isInfiniteRetries = TryCountLimitPerThread <= 0;
			System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

			NameValueCollection responseHeaders = null;
			while (true)
			{
				stopwatch.Restart();
				headersReceivingTryNumber++;
				Connecting?.Invoke(this, Url, headersReceivingTryNumber, TryCountLimitPerThread);
				LastErrorCode = GetUrlResponseHeaders(Url, Headers, Cookies, Proxy, ConnectionTimeout,
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
				else if (!isInfiniteRetries && headersReceivingTryNumber + 1 > TryCountLimitPerThread)
				{
					stopwatch.Stop();
					LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
					LastErrorMessage = "Не удалось получить HTTP-заголовки!";
					DownloadFinished?.Invoke(this, DownloadedBytes, LastErrorCode, OutputFileName);
					IsActive = false;
					return LastErrorCode;
				}

				if (isInfiniteRetries || !isInfiniteRetries && headersReceivingTryNumber < TryCountLimitPerThread)
				{
					TimeSpan interval = TimeSpan.FromMilliseconds(RetryIntervalMilliseconds);
					TimeSpan elapsed = stopwatch.Elapsed;
					if (elapsed < interval)
					{
						TimeSpan difference = interval - elapsed;
#if DEBUG
						System.Diagnostics.Debug.WriteLine($"Receiving headers: {(int)difference.TotalMilliseconds} milliseconds until next try...");
#endif
						Thread.Sleep(difference);
					}
				}
			}
			stopwatch.Stop();

			ExtractContentLengthFromHeaders(responseHeaders, out long fullContentLength);
			ContentLength = fullContentLength == -1L ? -1L :
				(RangeTo >= 0L ? RangeTo - RangeFrom + 1 : fullContentLength - RangeFrom);
			if (ContentLength < -1L) { ContentLength = -1L; }

			CustomError customError = new CustomError(LastErrorCode, null);
			Connected?.Invoke(this, Url, ContentLength, responseHeaders,
				headersReceivingTryNumber, TryCountLimitPerThread, customError);
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

			ConcurrentDictionary<int, DownloadableTask> downloadableTasks = new ConcurrentDictionary<int, DownloadableTask>();

			void OnProgressUpdatedFunc(DownloadableTask downloadableTask)
			{
				if (accurateMode && ThreadCount > 1)
				{
					lock (downloadableTasks)
					{
						downloadableTasks[downloadableTask.TaskId] = downloadableTask;
						DownloadedBytes = downloadableTasks.Sum(item => item.Value.ProcessedBytes);
					}
				}
				else
				{
					downloadableTasks[downloadableTask.TaskId] = downloadableTask;
					DownloadedBytes = downloadableTasks.Sum(item => item.Value.ProcessedBytes);
				}

				DownloadProgress?.Invoke(this, downloadableTasks);
			}

			void CallProgressUpdaterFunc(FileDownloader fd, long processedBytes,
				int tryNumber, DownloadableTaskState state)
			{
				DownloadableChunk downloadableChunk = null;
				if (state != DownloadableTaskState.Preparing)
				{
					fd.GetRange(out DownloadRange range);
					downloadableChunk = new DownloadableChunk(fd.DownloadableChunk.OutputStream, range);
				}
				DownloadableTask downloadableTask = new DownloadableTask(
					downloadableChunk, fd.Id, fullContentLength, processedBytes, tryNumber, TryCountLimitPerThread, state);
				OnProgressUpdatedFunc(downloadableTask);
			}

			bool isRangeSupported = IsRangeSupported(responseHeaders);
#if DEBUG
			if (!isRangeSupported && ThreadCount != 1)
			{
				System.Diagnostics.Debug.WriteLine("The \"Range\" header is not found! " +
					"Can't use multiple threads! Switching to single-threaded mode!");
			}
#endif
			IsRangeSupported = isRangeSupported;

			if (bufferSize == 0)
			{
				bufferSize = isRangeSupported ? 8192 : 4096;
			}

			bool isOutOfTries = false;
			bool isExceptionRaised = false;

			List<FileDownloader> downloaders = new List<FileDownloader>();
			int predictedChunkCount = isRangeSupported && ContentLength > ONE_MEGABYTE ? ThreadCount : 1;
			var chunkRanges = SplitContentToChunks(fullContentLength, RangeFrom, RangeTo, predictedChunkCount);
			int chunkCount = chunkRanges.Count();
			ThreadCount = chunkCount;
			for (int i = 0; i < chunkCount; ++i)
			{
				downloadableTasks[i] = new DownloadableTask(
					null, i, fullContentLength, 0L, -1, TryCountLimitPerThread, DownloadableTaskState.Preparing);
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

				FileDownloader downloader = new FileDownloader(this, taskId)
				{
					Url = Url,
					ConnectionTimeout = ConnectionTimeout,
					Headers = Headers,
					Cookies = Cookies,
					Proxy = Proxy,
					TryCountLimit = TryCountLimitInsideThread,
					RetryIntervalMilliseconds = RetryIntervalMilliseconds,
					FakeDownloading = FakeDownloading
				};
				lock (downloaders) { downloaders.Add(downloader); }

				#region Downloader event handlers
#if DEBUG
				downloader.Preparing += (object sender, string url, DownloadableChunk downloadableChunk) =>
				{
					int id = (sender as FileDownloader).Id;
					System.Diagnostics.Debug.WriteLine($"Task №{id}: Preparing...");
				};
				downloader.HeadersReceiving += (object sender, string url, DownloadableChunk downloadableChunk,
					int tryNumber, int tryCountLimit) =>
				{
					bool infiniteThreadRetries = tryCountLimit <= 0;
					int id = (sender as FileDownloader).Id;
					string msg = $"Task №{id}: Receiving headers... Try №{tryNumber}";
					if (!infiniteThreadRetries) { msg += $" / {tryCountLimit}"; }
					System.Diagnostics.Debug.WriteLine(msg);
				};
				downloader.HeadersReceived += (object sender, string url,
					DownloadableChunk downloadableChunk, NameValueCollection headers,
					int tryNumber, int tryCountLimit, int errCode) =>
				{
					bool infiniteThreadRetries = tryCountLimit <= 0;
					int id = (sender as FileDownloader).Id;
					string msg = errCode == 200 || errCode == 206 ?
						$"Task №{id}: Headers are received OK with try №{tryNumber}" :
						$"Task №{id}: Headers not received! Try №{tryNumber}";
					if (!infiniteThreadRetries) { msg += $" / {tryCountLimit}"; }
					System.Diagnostics.Debug.WriteLine(msg);
				};
#endif
				downloader.Connecting += (object sender, string url, int tryNumber, int tryCountLimit) =>
				{
					FileDownloader d = sender as FileDownloader;
					CallProgressUpdaterFunc(d, -1L, taskTryNumber, DownloadableTaskState.Connecting);
				};
				downloader.Connected += (object sender, string url, long contentLength,
					NameValueCollection headers, int tryNumber, int tryCountLimit, int errCode) =>
				{
					FileDownloader d = sender as FileDownloader;
					DownloadableTaskState state = errCode == 200 || errCode == 206 ?
						DownloadableTaskState.Connected : DownloadableTaskState.Errored;
					CallProgressUpdaterFunc(d, 0L, taskTryNumber, state);

					return errCode;
				};

				int lastTime = Environment.TickCount;
				downloader.WorkProgress += (object sender, long transferred, long contentLen, int tryNumber, int tryCountLimit) =>
				{
					int currentTime = Environment.TickCount;
					if (currentTime - lastTime >= UpdateIntervalMilliseconds)
					{
						FileDownloader d = sender as FileDownloader;
						CallProgressUpdaterFunc(d, transferred, taskTryNumber, DownloadableTaskState.Downloading);

						lastTime = currentTime;
					}
				};
				downloader.WorkFinished += (object sender, long transferred, long contentLen, int tryNumber, int tryCountLimit, int errCode) =>
				{
					DownloadableTaskState taskState;
					FileDownloader d = sender as FileDownloader;
					if (errCode != 200 && errCode != 206 && !isExceptionRaised && !_isCanceled)
					{
						lock (downloaders)
						{
							if (!isOutOfTries)
							{
								isOutOfTries = !isInfiniteRetries && taskTryNumber + 1 > TryCountLimitPerThread;
								if (isOutOfTries)
								{
#if DEBUG
									System.Diagnostics.Debug.WriteLine($"Task №{d.Id}: Out of tries! Aborting all tasks...");
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
					DownloadableTask downloadableTask = new DownloadableTask(
						downloadableChunk, d.Id, fullContentLength, transferred, taskTryNumber, TryCountLimitPerThread, taskState);
					OnProgressUpdatedFunc(downloadableTask);
				};
				downloader.WorkError += (object sender, int errCode, string errorMessage,
					long transferred, long contentLen, int tryNumber, int tryCountLimit) =>
				{
					if (errCode != 200 && errCode != 206)
					{
						FileDownloader d = sender as FileDownloader;
						d.GetRange(out DownloadRange range);
						DownloadableChunk downloadableChunk = new DownloadableChunk(d.DownloadableChunk.OutputStream, range);
						DownloadableTask downloadableTask = new DownloadableTask(
							downloadableChunk, d.Id, fullContentLength, transferred,
							taskTryNumber, TryCountLimitPerThread, DownloadableTaskState.Errored);
						OnProgressUpdatedFunc(downloadableTask);
					}
				};
				#endregion

				while (true)
				{
					try
					{
						taskTryNumber++;
						if (!isInfiniteRetries && taskTryNumber > TryCountLimitPerThread)
						{
							lock (downloaders)
							{
								if (!isOutOfTries)
								{
									isOutOfTries = true;
#if DEBUG

									System.Diagnostics.Debug.WriteLine($"Task №{taskId}: Out of tries! Aborting all tasks...");
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
							tryMessage += $" / {TryCountLimitPerThread}";
						}
						System.Diagnostics.Debug.WriteLine(tryMessage);
#endif
						if (!GetChunkStream(downloader, taskDownloadRange, chunkFileName,
							UseRamForTempFiles, isFakeDownloading, out Stream streamChunk))
						{
							Abort();
							return;
						}

						if (isRangeSupported)
						{
							downloader.SetRange(taskDownloadRange);
						}

						ContentChunkStream chunkStream = new ContentChunkStream(
							streamChunk, UseRamForTempFiles || isFakeDownloading ? null : chunkFileName);
						LastErrorCode = downloader.Download(chunkStream, bufferSize, _cancellationTokenSource);

						if (LastErrorCode == 200 || LastErrorCode == 206)
						{
							if (!UseRamForTempFiles && !isFakeDownloading) { downloader.DisposeOutputStream(); }
							break;
						}
						downloader.DisposeOutputStream();
						if (UseRamForTempFiles) { GC.Collect(); }

						if (_isCanceled || _isAborted || isOutOfTries) { break; }
#if DEBUG
						else
						{
							System.Diagnostics.Debug.WriteLine($"Task №{downloader.Id}: Restarting...");
						}
#endif
					}
					catch (Exception ex)
					{
#if DEBUG
						System.Diagnostics.Debug.WriteLine($"Task №{downloader.Id} catches exception while try №{taskTryNumber}!\n{ex.Message}");
#endif
						LastErrorCode = DOWNLOAD_ERROR_ABORTED;
						LastErrorMessage = ex.Message;
						isExceptionRaised = true;
#if DEBUG
						System.Diagnostics.Debug.WriteLine($"Task №{downloader.Id}: Aborting all tasks...");
#endif
						Abort();
						break;
					}
				}

				if (LastErrorCode != 200 && LastErrorCode != 206)
				{
					if (_isCanceled)
					{
						LastErrorCode = DOWNLOAD_ERROR_CANCELED_BY_USER;
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
				System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
				LastErrorMessage = ex.Message;
				Abort();
				ClearGarbage(downloadableTasks);
				_cancellationTokenSource.Dispose();
				_cancellationTokenSource = null;
				LastErrorCode = (ex is OperationCanceledException) ? DOWNLOAD_ERROR_CANCELED_BY_USER : ex.HResult;
				DownloadFinished?.Invoke(this, DownloadedBytes, LastErrorCode, OutputFileName);
				IsActive = false;
				return LastErrorCode;
			}

			downloaders = null;
			if (!_isCanceled && (LastErrorCode == 200 || LastErrorCode == 206))
			{
				List<DownloadableChunk> downloadableChunks = BuildChunkSequence(downloadableTasks, chunkCount, out bool isValidChunkSequence);
				if (downloadableChunks != null && ChunksDownloaded != null && isValidChunkSequence)
				{
					customError = ChunksDownloaded.Invoke(this, downloadableChunks, ContentLength);
					if (customError != null && customError.ErrorCode != 200)
					{
						ClearGarbage(downloadableTasks);
						LastErrorCode = customError.ErrorCode;
						LastErrorMessage = customError.ErrorMessage;
						_cancellationTokenSource.Dispose();
						_cancellationTokenSource = null;
						DownloadFinished?.Invoke(this, DownloadedBytes, LastErrorCode, OutputFileName);
						IsActive = false;
						return LastErrorCode;
					}
				}

				if (!isFakeDownloading)
				{
					if (!isValidChunkSequence || downloadableChunks == null || downloadableChunks.Count <= 0)
					{
						downloadableTasks = null;
						if (UseRamForTempFiles && downloadableChunks != null) { ClearGarbage(downloadableChunks); }
						LastErrorCode = DOWNLOAD_ERROR_CHUNK_SEQUENCE;
						LastErrorMessage = null;
						_cancellationTokenSource.Dispose();
						_cancellationTokenSource = null;
						DownloadFinished?.Invoke(this, DownloadedBytes, LastErrorCode, OutputFileName);
						IsActive = false;
						return LastErrorCode;
					}

					downloadableTasks = null;

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
											LastErrorCode = _isAborted ? DOWNLOAD_ERROR_ABORTED : DOWNLOAD_ERROR_CANCELED_BY_USER;
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
				LastErrorCode = DOWNLOAD_ERROR_CANCELED_BY_USER;
				LastErrorMessage = null;
			} else if (isOutOfTries)
			{
				LastErrorCode = DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT;
				LastErrorMessage = null;
			}

			if (downloadableTasks != null)
			{
				ClearGarbage(downloadableTasks);
				downloadableTasks = null;
			}

			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;

			DownloadFinished?.Invoke(this, DownloadedBytes, LastErrorCode, OutputFileName);

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
				System.Diagnostics.Debug.WriteLine("All tasks is aborted!");
			}
#endif
			_isCanceled = false;
			_isAborted = true;
			return b;
		}

		private int GetCancellationErrorCode()
		{
			if (_isAborted) { return DOWNLOAD_ERROR_ABORTED; }
			else if (_isCanceled) { return DOWNLOAD_ERROR_CANCELED_BY_USER; }
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

				outputStream = File.OpenWrite(chunkFileName);
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
					outputStream?.Close();
					return LastErrorCode;
				}

				try
				{
					outputStream = File.OpenWrite(tmpFileName);
				}
#if DEBUG
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.Message);
#else
				catch
				{
#endif
					if (!isSharedStream && outputStream != null) { outputStream.Close(); }
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
					string chunkFilePath = downloadableChunk.OutputStream.FilePath;
					bool fileExists;
					Stream tmpStream = downloadableChunk.OutputStream.Stream;
					bool isMemoryStream = tmpStream != null && tmpStream is MemoryStream;
					if (!isMemoryStream)
					{
						fileExists = !string.IsNullOrEmpty(chunkFilePath) && !string.IsNullOrWhiteSpace(chunkFilePath) &&
							File.Exists(chunkFilePath);
						if (!fileExists)
						{
							return DOWNLOAD_ERROR_MERGING_CHUNKS;
						}
						tmpStream = File.OpenRead(chunkFilePath);
					}
					else
					{
						tmpStream.Position = 0L;
						fileExists = false;
					}

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

					bool appended = Append(tmpStream, outputStream,
						(sourcePosition, sourceLength, destinationPosition, destinationLength) =>
							updateProgressFunc(0L, sourceLength)
						, func, func,
						_cancellationTokenSource.Token, ChunksMergingUpdateIntervalMilliseconds);

					downloadableChunk.OutputStream.Dispose();
					if (isMemoryStream)
					{
						//TODO: Fix possible memory leaking
						GC.Collect();
					}
					else
					{
						tmpStream.Close();
					}

					if (!appended)
					{
						if (!isSharedStream && outputStream != null) { outputStream.Close(); }
						ClearGarbage(downloadableChunks);
						return _cancellationTokenSource.IsCancellationRequested ?
							DOWNLOAD_ERROR_CANCELED_BY_USER : DOWNLOAD_ERROR_MERGING_CHUNKS;
					}

					if (!isMemoryStream && fileExists)
					{
						File.Delete(chunkFilePath);
					}

					if (_isCanceled) { break; }

					++i;
				}
			}
#if DEBUG
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
#else
			catch
			{
#endif
				if (!isSharedStream && outputStream != null) { outputStream.Close(); }
				ClearGarbage(downloadableChunks);
				return DOWNLOAD_ERROR_MERGING_CHUNKS;
			}

			if (!isSharedStream && outputStream != null) { outputStream.Close(); }

			if (_isCanceled)
			{
				ClearGarbage(downloadableChunks);
				return DOWNLOAD_ERROR_CANCELED_BY_USER;
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
						System.Diagnostics.Debug.WriteLine(ex.Message);
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
			if (UseRamForTempFiles)
			{
				var tasks = dictionary.Values.Where(item => item.DownloadableChunk != null).Select(item => item.DownloadableChunk);
				ClearGarbage(tasks);
			}
		}

		private void ClearGarbage(IEnumerable<DownloadableChunk> downloadableChunks)
		{
			if (UseRamForTempFiles)
			{
				var chunks = downloadableChunks.Where(item => item.OutputStream != null).Select(item => item.OutputStream);
				ClearGarbage(chunks);
			}
		}

		private void ClearGarbage(IEnumerable<ContentChunkStream> contentChunkStreams)
		{
			if (UseRamForTempFiles)
			{
				foreach (ContentChunkStream chunk in contentChunkStreams)
				{
					chunk.Dispose();
				}

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

		private void SetHeaders(NameValueCollection headers)
		{
			RangeFrom = 0L;
			RangeTo = -1L;
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
							ParseRangeHeaderValue(headerValue, out long rangeFrom, out long rangeTo);
							SetRange(rangeFrom, rangeTo);
							continue;
						}

						Headers.Add(headerName, headerValue);
					}
				}
			}
		}

		public bool SetRange(DownloadRange downloadRange)
		{
			return SetRange(downloadRange.StartPosition, downloadRange.EndPosition);
		}

		public bool SetRange(long rangeFrom, long rangeTo)
		{
			if (DownloadRange.IsValidRange(rangeFrom, rangeTo))
			{
				RangeFrom = rangeFrom;
				RangeTo = rangeTo;
				return true;
			}
			return false;
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

				default:
					return FileDownloader.ErrorCodeToString(errorCode);
			}
		}
	}
}
