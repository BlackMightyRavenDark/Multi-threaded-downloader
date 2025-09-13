using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiThreadedDownloaderLib.GuiTest
{
	public partial class Form1 : Form
	{
		private bool isDownloading = false;
		private bool isClosing = false;
		private WebHeaderCollection headers;
		private CookieContainer cookies;
		private FileDownloader singleThreadedDownloader;
		private MultiThreadedDownloader multiThreadedDownloader;

		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			Utils.ConnectionLimit = 100;
			lblDownloadProgress.Text = null;
			lblMergeProgress.Text = null;
			numericUpDownProxyPort.Maximum = ushort.MaxValue;

			headers = new WebHeaderCollection()
			{
				{ "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0" },
				{ "Accept", "*/*" },
				{ "Accept-Language", "en-US" },
				{ "Accept-Encoding", "gzip, deflate, br, zstd" }
			};
		}

		private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (isClosing) { e.Cancel = true; return; }
			else if (IsUnfinishedTaskPresent())
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine("Canceling tasks...");
#endif
				isClosing = true;
				e.Cancel = true;
				StopAll();
				bool unfinished = true;
				await Task.Run(() =>
				{
					while (unfinished)
					{
						Thread.Sleep(200);
						Invoke(new MethodInvoker(() => unfinished = IsUnfinishedTaskPresent()));
					}
				});

				isClosing = false;
				Close();
			}
		}

		private void Form1_Paint(object sender, PaintEventArgs e)
		{
			progressBarDownload.Invalidate();
		}

		private void btnSelectFile_Click(object sender, EventArgs e)
		{
			try
			{
				using (SaveFileDialog sfd = new SaveFileDialog())
				{
					sfd.Title = "Выберите файл, куда будем качать";
					sfd.Filter = "Все файлы|*.*";
					sfd.InitialDirectory = Application.StartupPath;
					if (sfd.ShowDialog() == DialogResult.OK)
					{
						textBoxOutputFileName.Text = sfd.FileName;
					}
				}
			} catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void btnSelectTempDir_Click(object sender, EventArgs e)
		{
			try
			{
				using (FolderBrowserDialog fbd = new FolderBrowserDialog())
				{
					fbd.Description = "Выберите папку для временных файлов";
					fbd.SelectedPath = Application.StartupPath;
					if (fbd.ShowDialog() == DialogResult.OK)
					{
						txtBoxTempDir.Text = fbd.SelectedPath;
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void btnHeaders_Click(object sender, EventArgs e)
		{
			btnHeaders.Enabled = false;
			btnDownloadMultiThreaded.Enabled = false;
			btnDownloadSingleThreaded.Enabled = false;

			try
			{
				FormHeadersEditor editor = new FormHeadersEditor(headers);
				if (editor.ShowDialog() == DialogResult.OK)
				{
					headers = editor.Headers;
				}
			} catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			btnDownloadMultiThreaded.Enabled = true;
			btnDownloadSingleThreaded.Enabled = true;
			btnHeaders.Enabled = true;
		}

		private void btnCookies_Click(object sender, EventArgs e)
		{
			FormCookieEditor editor = new FormCookieEditor();
			if (editor.ShowDialog() == DialogResult.OK)
			{
				if (editor.Cookies != null && editor.Cookies.Count > 0)
				{
					cookies = new CookieContainer();
					foreach (Cookie cookie in editor.Cookies)
					{
						cookies.Add(cookie);
					}

					string s = editor.Cookies.Count > 1 ? $"{editor.Cookies.Count} cookies" : "1 cookie";
					lblCookieCount.Text = $"{s} loaded";
				}
				else
				{
					cookies = null;
					lblCookieCount.Text = "No cookies loaded";
				}
			}
		}

		private async void btnDownloadSingleThreaded_Click(object sender, EventArgs e)
		{
			if (isDownloading)
			{
				singleThreadedDownloader?.Stop();
				return;
			}

			btnDownloadMultiThreaded.Enabled = false;
			EnableControls(false);

			string downloadUrl = textBoxUrl.Text;
			if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrWhiteSpace(downloadUrl))
			{
				MessageBox.Show("Не указана ссылка!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				EnableControls(true);
				btnDownloadMultiThreaded.Enabled = true;
				return;
			}

			string outputFilePath = textBoxOutputFileName.Text;
			if (!checkBoxDownloadToRAM.Checked && !checkBoxFakeDownloading.Checked &&
				(string.IsNullOrEmpty(outputFilePath) || string.IsNullOrWhiteSpace(outputFilePath)))
			{
				MessageBox.Show("Не указано имя файла!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				EnableControls(true);
				btnDownloadMultiThreaded.Enabled = true;
				return;
			}

			isDownloading = true;

			btnDownloadSingleThreaded.Text = "Stop";
			lblMergeProgress.Text = null;

			string actualOutputFilePath = null;
			try
			{
				if (!checkBoxDownloadToRAM.Checked && !checkBoxFakeDownloading.Checked)
				{
					actualOutputFilePath = outputFilePath;
				}

				if (!string.IsNullOrEmpty(actualOutputFilePath) &&
					!string.IsNullOrWhiteSpace(actualOutputFilePath) &&
					File.Exists(actualOutputFilePath))
				{
					File.Delete(actualOutputFilePath);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				EnableControls(true);
				btnDownloadMultiThreaded.Enabled = true;
				btnDownloadSingleThreaded.Text = "Download single threaded";
				isDownloading = false;
				return;
			}

			if (!CreateProxy(out WebProxy proxy, out string proxyError))
			{
				string msg = $"Неверно указан прокси-сервер!\n{proxyError}\nПродолжить скачивание без использования прокси-сервера?";
				if (MessageBox.Show(msg, "Ошибка!",
					MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.No)
				{
					btnDownloadSingleThreaded.Text = "Download single threaded";
					btnDownloadMultiThreaded.Enabled = true;
					EnableControls(true);
					isDownloading = false;
					return;
				}
			}

			singleThreadedDownloader = new FileDownloader();
			singleThreadedDownloader.Preparing += (s, url, downloadableChunk) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					const string t = "Подготовка";
					lblDownloadProgress.Text = $"{t} к скачиванию...";
					lblMergeProgress.Text = null;
					progressBarDownload.SetItem($"{t}...");
					AddToLog($"{t}... {url}");
				}));
			};
			singleThreadedDownloader.HeadersReceiving += (s, url,
				downloadableChunk, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Получение заголовков... Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					lblDownloadProgress.Text = t;
					progressBarDownload.ClearItems();
#if DEBUG
					System.Diagnostics.Debug.WriteLine($"{t} {url}");
#endif
					AddToLog(t);
				}));
			};
#if DEBUG
			singleThreadedDownloader.HeadersReceived += (s, url, downloadableChunk, headers,
				tryNumber, tryCountLimit, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					if (headers != null)
					{
						{
							string t = tryCountLimit > 0 ?
								$"Заголовки получены (попытка №{tryNumber} / {tryCountLimit}):" :
								$"Заголовки получены (попытка №{tryNumber}):";
							System.Diagnostics.Debug.WriteLine(t);
						}
						{
							string t = Utils.HttpHeadersToString(headers);
							System.Diagnostics.Debug.WriteLine(t);
						}
					}
					else
					{
						string t = $"Ошибка при получении заголовков! Код: {errCode}";
						System.Diagnostics.Debug.WriteLine(t);
						AddToLog(t);
						if (!(s as FileDownloader).IgnoreHeaderRequestErrors)
						{
							System.Diagnostics.Debug.WriteLine("Скачивание прервано!");
							AddToLog("Скачивание прервано!");
						}
					}
				}));
			};
#endif
			singleThreadedDownloader.Connecting += (s, url, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Подключение... Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }

					lblDownloadProgress.Text = t;
					progressBarDownload.SetItem(t);
					AddToLog(t);
				}));
			};
			singleThreadedDownloader.Connected += (s, url, contentLength, headers,
				tryNumber, tryCountLimit, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					if (errCode == 200 || errCode == 206)
					{
						string t = tryCountLimit > 0 ?
							$"Подключено! (попытка №{tryNumber} / {tryCountLimit})" :
							$"Подключено! (попытка №{tryNumber})";
						lblDownloadProgress.Text = t;
						AddToLog(t);
						if ((s as FileDownloader).IsCompressedContent)
						{
							AddToLog($"Алгоритм сжатия данных: {(s as FileDownloader).ContentCompressionAlgorithm}");
						}

						if (!checkBoxDownloadToRAM.Checked && !checkBoxFakeDownloading.Checked && contentLength > 0L)
						{
							string fn = textBoxOutputFileName.Text;
							char driveLetter = fn.Length > 2 && fn[1] == ':' && fn[2] == '\\' ? fn[0] : Application.ExecutablePath[0];
							if (driveLetter != '\\')
							{
								DriveInfo driveInfo = new DriveInfo(driveLetter.ToString());
								if (!driveInfo.IsReady)
								{
									errCode = FileDownloader.DOWNLOAD_ERROR_DRIVE_NOT_READY;
									return;
								}

								long minimumFreeSpaceRequired = (long)(contentLength * 1.1);
								if (driveInfo.AvailableFreeSpace <= minimumFreeSpaceRequired)
								{
									errCode = FileDownloader.DOWNLOAD_ERROR_INSUFFICIENT_DISK_SPACE;
									return;
								}
							}
						}

						progressBarDownload.SetItem("Подключено!");
					}
					else
					{
						string t = $"Ошибка {errCode}";
						lblDownloadProgress.Text = t;
						AddToLog(t);
						progressBarDownload.SetItems(null);
					}
				}));
				return errCode;
			};

			singleThreadedDownloader.WorkStarted += (s, contentLength, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string contentLengthString = contentLength > 0L ? contentLength.ToString() : "<Неизвестно>";
					string t = $"Скачано: 0 из {contentLengthString}, Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }

					lblDownloadProgress.Text = t;
					progressBarDownload.SetItem("0,000%");

					t = $"Скачивание начато. Размер файла: {contentLengthString}";
					if (contentLength > 0L) { t += " байт"; }
					AddToLog(t);
				}));
			};

			singleThreadedDownloader.WorkProgress += (s, bytesTransferred, contentLength, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					if (contentLength > 0L)
					{
						double percent = 100.0 / contentLength * bytesTransferred;
						string percentFormatted = string.Format("{0:F3}", percent);
						string t = $"Скачано {bytesTransferred} из {contentLength} ({percentFormatted}%), Попытка №{tryNumber}";
						if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
						lblDownloadProgress.Text = t;
						progressBarDownload.SetItem(0, 100, (int)percent, $"{percentFormatted}%");
					}
					else
					{
						lblDownloadProgress.Text = $"Скачано {bytesTransferred} из <Неизвестно>";
						progressBarDownload.SetItem($"Скачано {bytesTransferred} байт");
					}
				}));
			};

			singleThreadedDownloader.WorkError += (s, errCode, errorMessage,
				bytesTransferred, contentLength, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Ошибка {errCode}! Скачано {bytesTransferred}";
					t += contentLength > 0L ? $" из {contentLength}" : ".";
					AddToLog(t);
				}));
			};

			singleThreadedDownloader.WorkFinished += (s, bytesTransferred, contentLength, tryNumber, tryCountLimit, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					if (contentLength > 0L)
					{
						double percent = 100.0 / contentLength * bytesTransferred;
						string percentFormatted = string.Format("{0:F3}", percent);
						string t = $"Скачано {bytesTransferred} из {contentLength} ({percentFormatted}%), Попытка №{tryNumber}";
						if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
						lblDownloadProgress.Text = t;
						progressBarDownload.SetItem(0, 100, (int)percent, $"{percentFormatted}%");
						AddToLog($"Скачивание завершено с кодом {errCode} ({FileDownloader.ErrorCodeToString(errCode)}). Скачано {bytesTransferred} из {contentLength}.");
					}
					else
					{
						lblDownloadProgress.Text = $"Скачано {bytesTransferred} из <Неизвестно>";
						progressBarDownload.SetItem($"Скачано {bytesTransferred} байт");
						AddToLog($"Скачивание завершено. Скачано {bytesTransferred} из <Неизвестно>.");
					}
				}));
			};

			singleThreadedDownloader.Url = downloadUrl;
			singleThreadedDownloader.Headers = headers;
			singleThreadedDownloader.Cookies = cookies;
			singleThreadedDownloader.Proxy = proxy;
			singleThreadedDownloader.HeaderRequestMethod = textBoxHeaderRequestMethod.Text;
			singleThreadedDownloader.IgnoreHeaderRequestErrors = checkBoxIgnoreHeaderRequestErrors.Checked;
			singleThreadedDownloader.SkipHeaderRequest = checkBoxSkipHeadRequest.Checked;
			singleThreadedDownloader.UpdateIntervalMilliseconds = (int)numericUpDownUpdateInterval.Value;
			singleThreadedDownloader.TryCountLimit = (int)numericUpDownTryCountInsideEachThread.Value;
			singleThreadedDownloader.RetryIntervalMilliseconds = (int)numericUpDownRetryInterval.Value;
			singleThreadedDownloader.ConnectionTimeout = (int)numericUpDownConnectionTimeout.Value;
			singleThreadedDownloader.FakeDownloading = checkBoxFakeDownloading.Checked;

			Stream outputStream = checkBoxFakeDownloading.Checked ? null :
				(checkBoxDownloadToRAM.Checked ? new MemoryStream() : (Stream)File.OpenWrite(actualOutputFilePath));
			int errorCode = await Task.Run(() => singleThreadedDownloader.Download(outputStream, actualOutputFilePath));
			outputStream?.Close();
			if (checkBoxDownloadToRAM.Checked && !checkBoxFakeDownloading.Checked) { GC.Collect(); }
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"Error code = {errorCode}");
#endif
			if (!isClosing)
			{
				AddToLog("Поток завершён.");
				if (errorCode == 200 || errorCode == 206)
				{
					string messageText = $"Скачано {singleThreadedDownloader.DownloadedInLastSession} байт";
					MessageBox.Show(messageText, "Скачано!", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					progressBarDownload.ClearItems();
					string errorMessage = singleThreadedDownloader.HasErrorMessage ?
						singleThreadedDownloader.LastErrorMessage : FileDownloader.ErrorCodeToString(errorCode);
					lblDownloadProgress.Text = $"Ошибка {errorCode}: {errorMessage}";

					if (errorCode == FileDownloader.DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED)
					{
						errorMessage = $"Попытка скачивания в уже существующий не пустой файл!\n{errorMessage}";
					}
					else if (errorCode != FileDownloader.DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED)
					{
						if (singleThreadedDownloader.HasErrorMessage)
						{
							errorMessage += $"{Environment.NewLine}Текст ошибки: {singleThreadedDownloader.LastErrorMessage}";
						}
						else if (errorCode == FileDownloader.DOWNLOAD_ERROR_OUT_OF_TRIES_LEFT)
						{
							errorMessage = $"Скачивание прервано!{Environment.NewLine}{errorMessage}";
						}
					}
					else if (singleThreadedDownloader.HasErrorMessage)
					{
						errorMessage = $"{FileDownloader.ErrorCodeToString(errorCode)}{Environment.NewLine}{singleThreadedDownloader.LastErrorMessage}";
					}

					ShowErrorMessage(errorCode, errorMessage);
				}

				isDownloading = false;
				singleThreadedDownloader = null;

				btnDownloadSingleThreaded.Text = "Download single threaded";
				btnDownloadMultiThreaded.Enabled = true;
				EnableControls(true);
			}
		}

		private async void btnDownloadMultiThreaded_Click(object sender, EventArgs e)
		{
			if (isDownloading)
			{
				if (multiThreadedDownloader != null)
				{
					if (multiThreadedDownloader.Stop())
					{
						btnDownloadMultiThreaded.Text = "Stopping...";
						btnDownloadMultiThreaded.Enabled = false;
					}
				}
				return;
			}

			isDownloading = true;

			btnDownloadMultiThreaded.Text = "Stop";
			btnDownloadSingleThreaded.Enabled = false;
			EnableControls(false);
			lblMergeProgress.Text = null;

			string tempDirectory = txtBoxTempDir.Text;
			if (!checkBoxDownloadToRAM.Checked && !checkBoxFakeDownloading.Checked &&
				!string.IsNullOrEmpty(tempDirectory) && !string.IsNullOrWhiteSpace(tempDirectory) &&
				!Directory.Exists(tempDirectory))
			{
				MessageBox.Show("Не указана папка для временных файлов!", "Ошибка!",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				btnDownloadMultiThreaded.Text = "Download multi threaded";
				btnDownloadMultiThreaded.Enabled = true;
				EnableControls(true);
				isDownloading = false;
				return;
			}

			if (!CreateProxy(out WebProxy proxy, out string proxyError))
			{
				string msg = $"Неверно указан прокси-сервер!\n{proxyError}\nПродолжить скачивание без использования прокси-сервера?";
				if (MessageBox.Show(msg, "Ошибка!",
					MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.No)
				{
					btnDownloadMultiThreaded.Text = "Download multi threaded";
					btnDownloadSingleThreaded.Enabled = true;
					EnableControls(true);
					isDownloading = false;
					return;
				}
			}

			bool isPreparing = true;

			multiThreadedDownloader = new MultiThreadedDownloader();
			multiThreadedDownloader.Preparing += (s) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					const string t = "Подготовка";
					lblDownloadProgress.Text = $"{t} к скачиванию...";
					progressBarDownload.SetItem($"{t}...");

					string url = (s as MultiThreadedDownloader).Url;
					AddToLog($"{t}... {url}");
				}));
			};
			multiThreadedDownloader.Connecting += (s, url, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Подключение... Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					lblDownloadProgress.Text = t;
				}));
			};
			multiThreadedDownloader.Connected += (object s, string url, long contentLength,
				WebHeaderCollection headers, int tryNumber, int tryCountLimit, CustomError customError) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					if (customError.ErrorCode == 200 || customError.ErrorCode == 206)
					{
#if DEBUG
						string t = Utils.HttpHeadersToString(headers);
						System.Diagnostics.Debug.WriteLine($"Заголовки получены:\n{t}");
#endif
						string connectedString = tryCountLimit > 0 ?
							$"Подключено! (попытка №{tryNumber} / {tryCountLimit}" :
							$"Подключено! (попытка №{tryNumber}";
						AddToLog(connectedString);
						lblDownloadProgress.Text = connectedString;
						MultiThreadedDownloader mtd = s as MultiThreadedDownloader;
						if (mtd.IsCompressedContent)
						{
							AddToLog($"Алгоритм сжатия данных: {mtd.ContentCompressionAlgorithm}");
						}
						if (!checkBoxDownloadToRAM.Checked && !checkBoxFakeDownloading.Checked && contentLength > 0L)
						{
							long minimumFreeSpaceRequired = (long)(contentLength * 1.1);

							List<char> driveLetters = mtd.GetUsedDriveLetters();
							if (driveLetters.Count > 0 && !IsEnoughDiskSpace(driveLetters, minimumFreeSpaceRequired))
							{
								customError.ErrorCode = FileDownloader.DOWNLOAD_ERROR_INSUFFICIENT_DISK_SPACE;
								customError.ErrorMessage = "Недостаточно места на диске!";
								AddToLog(customError.ErrorMessage);
								return;
							}

							if (mtd.UseRamForTempFiles && MemoryWatcher.Update() &&
								MemoryWatcher.RamFree < (ulong)minimumFreeSpaceRequired)
							{
								customError.ErrorCode = MultiThreadedDownloader.DOWNLOAD_ERROR_CUSTOM;
								customError.ErrorMessage = "Недостаточно памяти!";
								AddToLog(customError.ErrorMessage);
								return;
							}
						}
					}
					else
					{
						string t = multiThreadedDownloader.HasErrorMessage ?
							$"Ошибка: {customError.ErrorMessage} (Код: {customError.ErrorCode})" :
							$"Код ошибки: {customError.ErrorCode}";
						lblDownloadProgress.Text = t;
						AddToLog(t);
					}
				}));
			};
			multiThreadedDownloader.DownloadStarted += (s, contentLength) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					isPreparing = false;
					progressBarDownload.SetItem(0, 100, 0);
					string contentLengthString = contentLength > 0L ? contentLength.ToString() : "<Неизвестно>";
					lblDownloadProgress.Text += $"Скачано 0 из {contentLengthString}";
					string t = $"Скачивание начато. Размер файла: {contentLengthString}";
					if (contentLength > 0L) { t += " байт"; }
					AddToLog(t);
				}));
			};
			multiThreadedDownloader.DownloadProgress += (s, taskDictionary) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					var tasks = taskDictionary.Values;
					int taskCount = tasks.Count;
					LinkedList<MultipleProgressBarItem> progressBarItems = new LinkedList<MultipleProgressBarItem>();
					foreach (DownloadableTask task in tasks)
					{
						string itemText;
						double percentItem = 0.0;
						switch (task.State)
						{
							case DownloadableTaskState.Preparing:
								itemText = $"{task.TaskId}: Preparing...";
								break;

							case DownloadableTaskState.Connecting:
								itemText = $"{task.TaskId}: Connecting...";
								break;

							case DownloadableTaskState.Connected:
								itemText = $"{task.TaskId}: Connected!";
								break;

							default:
								if (task.ChunkFileSize > 0L && task.ProcessedBytes >= 0L)
								{
									percentItem = 100.0 / task.ChunkFileSize * task.ProcessedBytes;
									string percentItemFormatted = string.Format("{0:F3}", percentItem);
									itemText = taskCount > 1 ? $"{task.TaskId}: {percentItemFormatted}%" : $"{percentItemFormatted}%";
								}
								else
								{
									string processedBytesString = task.ProcessedBytes < 0L ? "0" : task.ProcessedBytes.ToString();
									itemText = taskCount > 1 ? $"{task.TaskId}: {processedBytesString} / <Неизвестно>" :
										$"{processedBytesString} / <Неизвестно>";
								}

								if (task.State == DownloadableTaskState.Errored)
								{
									itemText += ", Error!";
								}

								break;
						}
						Color itemBackgroundColor = task.State == DownloadableTaskState.Errored ? Color.Orange : Color.Lime;
						MultipleProgressBarItem mpi = new MultipleProgressBarItem(
							0, 100, (int)percentItem, itemText, itemBackgroundColor);
						progressBarItems.AddLast(mpi);
					}

					progressBarDownload.SetItems(progressBarItems);

					long totalBytesTransferred = tasks.Where(item => item.ProcessedBytes >= 0L).Sum(item => item.ProcessedBytes);
					long contentLength = (s as MultiThreadedDownloader).ContentLength;
					if (contentLength > 0L)
					{
						double percent = 100.0 / contentLength * totalBytesTransferred;
						string percentFormatted = string.Format("{0:F3}", percent);
						lblDownloadProgress.Text = $"Скачано {totalBytesTransferred} из {contentLength} ({percentFormatted}%)";
					}
					else
					{
						lblDownloadProgress.Text = $"Скачано {totalBytesTransferred} из <Неизвестно>";
					}
				}));
			};
			multiThreadedDownloader.ChunksDownloaded += (s, chunks, contentLength) =>
			{
				Invoke(new MethodInvoker(() => AddToLog("Все чанки скачаны.")));
				MultiThreadedDownloader mtd = s as MultiThreadedDownloader;
#if DEBUG
				string t = "Chunks downloaded:\n";
				foreach (DownloadableChunk chunk in chunks)
				{
					string rangeString = chunk.Range != null ? chunk.Range.GetFormattedString() : $"0-{contentLength - 1L}";
					t += contentLength >= 0 ? $"{rangeString}/{contentLength}" : rangeString;
					string filePath = chunk.OutputStream?.FilePath;
					t += !string.IsNullOrEmpty(filePath) && !string.IsNullOrWhiteSpace(filePath) ? $" | {filePath}\n" :
						(mtd.FakeDownloading ? " | <fake stream>\n" : " | <memory stream>\n");
				}
				System.Diagnostics.Debug.WriteLine(t);
#endif
				if (mtd.FakeDownloading)
				{
					Invoke(new MethodInvoker(() => progressBarDownload.ClearItems()));
					const string msg = "No need to merge chunks while using fake downloading!";
					return new CustomError(msg);
				}
				else if (!mtd.MergeChunksAutomatically)
				{
					const string msg = "Manual chunk merging is not implemented";
					Invoke(new MethodInvoker(() => progressBarDownload.SetItem($"{msg}!")));
					return new CustomError(msg);
				}

				return null;
			};
			multiThreadedDownloader.DownloadFinished += (s, bytesTransferred, contentLength, errCode, fileName) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string contentLengthString = contentLength > 0L ? contentLength.ToString() : "<Неизвестно>";
					MultiThreadedDownloader mtd = s as MultiThreadedDownloader;
					string errorMessage = errCode == MultiThreadedDownloader.DOWNLOAD_ERROR_CUSTOM && mtd.HasErrorMessage ?
						mtd.LastErrorMessage : MultiThreadedDownloader.ErrorCodeToString(errCode);
					AddToLog($"Скачивание завершено с кодом {errCode} ({errorMessage}). " +
						$"Скачано {bytesTransferred} из {contentLengthString}.");
					if (errCode == 200 || errCode == 206)
					{
						string t = $"Скачано: {bytesTransferred} байт";
						if (!mtd.FakeDownloading &&
							!checkBoxDownloadToRAM.Checked &&
							!string.IsNullOrEmpty(fileName))
						{
							t = $"Имя файла: {fileName}\n{t}";
						}
						MessageBox.Show(t, "Скачано!", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}));
			};
			multiThreadedDownloader.ChunkMergingStarted += (s, chunkCount) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					AddToLog("Объединение чанков начато.");
					progressBarDownload.SetItem(0, chunkCount, 0);
					lblMergeProgress.Left = lblDownloadProgress.Left + lblDownloadProgress.Width;
					lblMergeProgress.Text = $"Объединение чанков: 0 / {chunkCount}";
				}));
			};
			multiThreadedDownloader.ChunkMergingProgress += (s, chunkId, chunkCount, chunkPosition, chunkSize) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					double percent = 100.0 / chunkSize * chunkPosition;
					string percentFormatted = string.Format("{0:F3}", percent);
					lblMergeProgress.Text = $"Объединение чанков: {chunkId + 1} / {chunkCount}, " +
						$"{chunkPosition} / {chunkSize} ({percentFormatted}%)";

					MultipleProgressBarItem[] progressBarItems = GenerateChunkMergingProgressVisualizationItems(chunkCount, chunkId, percent);
					progressBarDownload.SetItems(progressBarItems);
				}));
			};
			multiThreadedDownloader.ChunkMergingFinished += (s, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					AddToLog($"Объединение чанков завершено с кодом {errCode}.");
					lblMergeProgress.Text = errCode == 200 || errCode == 206 || errCode == FileDownloader.DOWNLOAD_ERROR_CANCELED ? null : $"Ошибка объединения чанков! Код: {errCode}";
				}));
			};
			multiThreadedDownloader.MovingFileToDestination += (s, bytesTransferred, fileSize, filePath, sourceDriveLetter, destnationDriveLetter) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					if (bytesTransferred == 0L)
					{
						lblMergeProgress.Text = null;
						AddToLog($"Финальное перемещение файла начато. {sourceDriveLetter}: -> {destnationDriveLetter}:");
					}

					double percent = 100.0 / fileSize * bytesTransferred;
					string percentFormatted = string.Format("{0:F2}", percent);
					lblDownloadProgress.Text = $"Финальное перемещение файла ({sourceDriveLetter}: -> {destnationDriveLetter}:): " +
						$"{bytesTransferred} / {fileSize} ({percentFormatted}%)";
					progressBarDownload.SetItem(0, 100, (int)percent);

					if (bytesTransferred == fileSize)
					{
						lblDownloadProgress.Text = "Скачано!";
						AddToLog("Финальное перемещение файла завершено.");
					}
				}));
			};
			multiThreadedDownloader.TaskPreparing += (s, task) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					AddToLog($"Task №{task.TaskId}: Подготовка...");
				}));
			};
			multiThreadedDownloader.TaskHeadersReceiving += (s, task, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Task №{task.TaskId}: Получение HTTP-заголовков... Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					AddToLog(t);
				}));
			};
			multiThreadedDownloader.TaskHeadersReceived += (s, task, headers, tryNumber, tryCountLimit, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Task №{task.TaskId}: Получено {headers.Count} HTTP-заголовков с кодом ошибки {errCode}. Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					AddToLog(t);
				}));
			};
			multiThreadedDownloader.TaskConnecting += (s, task, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Task №{task.TaskId}: Подключение... Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					AddToLog(t);
				}));
			};
			multiThreadedDownloader.TaskConnected += (s, task, tryNumber, tryCountLimit, taskContentLength, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Task №{task.TaskId}: Подключено! Код: {errCode}. " +
						$"Task range: {task.DownloadableChunk.Range.GetFormattedString()}/{task.FullContentLength}";
					AddToLog(t);
				}));
				return errCode;
			};
			multiThreadedDownloader.TaskStarted += (s, task, taskContentLength, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string t = $"Task №{task.TaskId}: Скачивание начато. " +
						$"Range: {task.DownloadableChunk.Range.GetFormattedString()}/{task.FullContentLength}";
					AddToLog(t);
				}));
			};
			multiThreadedDownloader.TaskFinished += (s, task,
				bytesTransferred, taskContentLength, tryNumber, tryCountLimit, errCode) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string contentLengthString = taskContentLength > 0L ? taskContentLength.ToString() : "<Неизвестно>";
					string t = $"Task №{task.TaskId}: Скачивание завершено с кодом {errCode} ({MultiThreadedDownloader.ErrorCodeToString(errCode)})! " +
						$"Скачано {bytesTransferred} из {contentLengthString} / {task.FullContentLength}. Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					AddToLog(t);
				}));
			};
			multiThreadedDownloader.TaskError += (s, task, errCode, errorMessage,
				bytesTransferred, taskContentLength, tryNumber, tryCountLimit) =>
			{
				Invoke(new MethodInvoker(() =>
				{
					string contentLengthString = taskContentLength > 0L ? taskContentLength.ToString() : "<Неизвестно>";
					string t = $"Task №{task.TaskId}: Ошибка {errCode} ({errorMessage})! " +
						$"Скачано {bytesTransferred} из {contentLengthString} / {task.FullContentLength}. Попытка №{tryNumber}";
					if (tryCountLimit > 0) { t += $" / {tryCountLimit}"; }
					AddToLog(t);
				}));
			};

			multiThreadedDownloader.Url = textBoxUrl.Text;
			multiThreadedDownloader.OutputFileName = textBoxOutputFileName.Text;
			multiThreadedDownloader.TempDirectory = tempDirectory;
			multiThreadedDownloader.ThreadCount = (int)numericUpDownThreadCount.Value;
			multiThreadedDownloader.TryCountLimitPerThread = (int)numericUpDownTryCountPerThread.Value;
			multiThreadedDownloader.TryCountLimitInsideThread = (int)numericUpDownTryCountInsideEachThread.Value;
			multiThreadedDownloader.RetryIntervalMilliseconds = (int)numericUpDownRetryInterval.Value;
			multiThreadedDownloader.Headers = headers;
			multiThreadedDownloader.Cookies = cookies;
			multiThreadedDownloader.Proxy = proxy;
			multiThreadedDownloader.HeaderRequestMethod = textBoxHeaderRequestMethod.Text;
			multiThreadedDownloader.UseRamForTempFiles = checkBoxUseRamForTempFiles.Checked;
			multiThreadedDownloader.FakeDownloading = checkBoxFakeDownloading.Checked;
			multiThreadedDownloader.UpdateIntervalMilliseconds = (int)numericUpDownUpdateInterval.Value;
			multiThreadedDownloader.ChunksMergingUpdateIntervalMilliseconds = (int)numericUpDownChunksMergingUpdateInterval.Value;
			multiThreadedDownloader.MergeChunksAutomatically = checkBoxMergeChunksAutomatically.Checked;
			multiThreadedDownloader.ConnectionTimeout = (int)numericUpDownConnectionTimeout.Value;

			Stream outputStream = checkBoxFakeDownloading.Checked ? null :
				(checkBoxDownloadToRAM.Checked ? new MemoryStream() : null);
			bool useAccurateMode = checkBoxUseAccurateMode.Checked;
			int errorCode = await Task.Run(() => multiThreadedDownloader.Download(outputStream, useAccurateMode));
#if DEBUG
			System.Diagnostics.Debug.WriteLine($"Error code = {errorCode}");
#endif
			if (!checkBoxFakeDownloading.Checked &&
				(multiThreadedDownloader.UseRamForTempFiles || checkBoxDownloadToRAM.Checked))
			{
				GC.Collect();
			}

			if (!isClosing)
			{
				AddToLog("Поток завершён.");
				if (errorCode != 200 && errorCode != 206)
				{
					if (isPreparing) { progressBarDownload.ClearItems(); }
					string errorMessage = multiThreadedDownloader.HasErrorMessage ? multiThreadedDownloader.LastErrorMessage :
						MultiThreadedDownloader.ErrorCodeToString(errorCode);
					lblDownloadProgress.Text = $"Ошибка {errorCode}: {errorMessage}";
					if (multiThreadedDownloader.HasErrorMessage)
					{
						errorMessage = $"{MultiThreadedDownloader.ErrorCodeToString(errorCode)}{Environment.NewLine}{errorMessage}";
					}
					lblMergeProgress.Left = lblDownloadProgress.Left + lblDownloadProgress.Width + 4;

					ShowErrorMessage(errorCode, errorMessage);
				}

				isDownloading = false;
				multiThreadedDownloader = null;

				btnDownloadMultiThreaded.Text = "Download multi threaded";
				btnDownloadMultiThreaded.Enabled = true;
				btnDownloadSingleThreaded.Enabled = true;
				EnableControls(true);
			}
		}

		private void EnableControls(bool enable)
		{
			textBoxUrl.Enabled = enable;
			textBoxOutputFileName.Enabled = enable;
			txtBoxTempDir.Enabled = enable;
			btnSelectFile.Enabled = enable;
			btnSelectTempDir.Enabled = enable;
			btnHeaders.Enabled = enable;
			checkBoxMergeChunksAutomatically.Enabled = enable;
			checkBoxDownloadToRAM.Enabled = enable;
			checkBoxUseRamForTempFiles.Enabled = enable;
			checkBoxFakeDownloading.Enabled = enable;
			checkBoxUseAccurateMode.Enabled = enable;
			checkBoxIgnoreHeaderRequestErrors.Enabled = enable;
			checkBoxSkipHeadRequest.Enabled = enable;
			textBoxHeaderRequestMethod.Enabled = enable;
			numericUpDownThreadCount.Enabled = enable;
			numericUpDownTryCountPerThread.Enabled = enable;
			numericUpDownTryCountInsideEachThread.Enabled = enable;
			numericUpDownUpdateInterval.Enabled = enable;
			numericUpDownChunksMergingUpdateInterval.Enabled = enable;
			numericUpDownConnectionTimeout.Enabled = enable;
			numericUpDownRetryInterval.Enabled = enable;
			textBoxProxyAddress.Enabled = enable;
			numericUpDownProxyPort.Enabled = enable;
			btnCookies.Enabled = enable;
		}

		private bool CreateProxy(out WebProxy proxy, out string errorMessage)
		{
			try
			{
				string proxyAddress = textBoxProxyAddress.Text;
				if (proxyAddress.Contains(" "))
				{
					errorMessage = "Адрес прокси-сервера не должен содержать пробелов!";
					proxy = null;
					return false;
				}
				int proxyPort = (int)numericUpDownProxyPort.Value;
				proxy = !string.IsNullOrWhiteSpace(proxyAddress) && proxyPort > 0 ?
					new WebProxy(proxyAddress, proxyPort) : null;
				errorMessage = null;
				return true;
			}
			catch (Exception ex)
			{
				errorMessage = ex.Message;
				proxy = null;
				return false;
			}
		}

		private static MultipleProgressBarItem[] GenerateChunkMergingProgressVisualizationItems(
			int chunkCount, int currentChunkId, double currentChunkProgressPercent)
		{
			MultipleProgressBarItem[] items = new MultipleProgressBarItem[chunkCount];
			for (int i = 0; i < chunkCount; ++i)
			{
				if (i < currentChunkId) { items[i] = new MultipleProgressBarItem(100, "100,00%"); }
				else if (i > currentChunkId) { items[i] = new MultipleProgressBarItem(0, "0,00%"); }
				else
				{
					string percentFormatted = string.Format("{0:F2}", currentChunkProgressPercent);
					items[i] = new MultipleProgressBarItem((int)currentChunkProgressPercent, $"{percentFormatted}%");
				}
			}
			return items;
		}

		private static bool IsEnoughDiskSpace(IEnumerable<char> driveLetters, long contentLength)
		{
			return driveLetters.All(driveLetter =>
			{
				DriveInfo driveInfo = new DriveInfo(driveLetter.ToString());
				return driveInfo.AvailableFreeSpace > contentLength;
			});
		}

		private static void ShowErrorMessage(int errorCode, string errorMessage)
		{
			MessageBoxIcon icon = errorCode == FileDownloader.DOWNLOAD_ERROR_STREAM_SIZE_EXCEEDED_PREDICTED ?
				MessageBoxIcon.Exclamation : MessageBoxIcon.Error;
			string messageCaption = errorCode == FileDownloader.DOWNLOAD_ERROR_CANCELED ?
				"Отменятор отменения отмены" : "Ошибка!";
			MessageBox.Show(errorMessage, messageCaption, MessageBoxButtons.OK, icon);
		}

		private void AddToLog(string eventText)
		{
			DateTime now = DateTime.UtcNow;
			ListViewItem item = new ListViewItem(now.ToString("yyyy-MM-dd HH:mm:ss \"GMT\""));
			item.SubItems.Add(eventText);
			listViewLog.Items.Add(item);

			if (checkBoxAutoscrollLog.Checked)
			{
				listViewLog.EnsureVisible(listViewLog.Items.Count - 1);
			}
		}

		private void StopAll()
		{
			singleThreadedDownloader?.Stop();
			multiThreadedDownloader?.Stop();
		}

		private bool IsUnfinishedTaskPresent()
		{
			if (singleThreadedDownloader != null && singleThreadedDownloader.IsActive) { return true; }
			if (multiThreadedDownloader != null && multiThreadedDownloader.IsActive) { return true; }
			return false;
		}
	}
}
