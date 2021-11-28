﻿using System;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace Multi_threaded_downloader
{
    public partial class Form1 : Form
    {
        private bool isDownloading = false;
        private bool needCancel = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ServicePointManager.DefaultConnectionLimit = 100;
            lblDownloadingProgress.Text = null;
            lblMergingProgress.Text = null;
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Выберите файл, куда будем качать";
            sfd.Filter = "Все файлы|*.*";
            sfd.InitialDirectory = Application.StartupPath;
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                editFileName.Text = sfd.FileName;
            }
            sfd.Dispose();
        }

        private void btnSelectTempDir_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Выберите папку для временных файлов";
            folderBrowserDialog.SelectedPath = Application.StartupPath;
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                editTempPath.Text =
                    folderBrowserDialog.SelectedPath.EndsWith("\\")
                    ? folderBrowserDialog.SelectedPath : folderBrowserDialog.SelectedPath + "\\";
            }

        }

        private void btnSelectMergingDir_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Выберите папку для объединения чанков";
            folderBrowserDialog.SelectedPath = Application.StartupPath;
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                editMergingPath.Text =
                    folderBrowserDialog.SelectedPath.EndsWith("\\")
                    ? folderBrowserDialog.SelectedPath : folderBrowserDialog.SelectedPath + "\\";
            }
        }

        private void btnDownloadSingleThreaded_Click(object sender, EventArgs e)
        {
            if (isDownloading)
            {
                needCancel = true;
                return;
            }

            if (string.IsNullOrEmpty(editFileName.Text) || string.IsNullOrWhiteSpace(editFileName.Text))
            {
                MessageBox.Show("Не указано имя файла!", "Ошибка!",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            isDownloading = true;

            btnDownloadSingleThreaded.Text = "Stop";
            btnDownloadMultiThreaded.Enabled = false;
            lblMergingProgress.Text = null;

            FileDownloader downloader = new FileDownloader();
            downloader.WorkStarted += (s, max) =>
            {
                progressBar1.Value = 0;
                progressBar1.Maximum = 100;
                lblDownloadingProgress.Text = $"Скачано: 0 из {max}";
            };
            downloader.WorkProgress += (s, bytes, max) =>
            {
                if (max > 0L)
                {
                    double percent = 100.0 / max * bytes;
                    progressBar1.Value = (int)percent;
                    lblDownloadingProgress.Text = $"Скачано {bytes} из {max} ({string.Format("{0:F3}", percent)}%)";
                }
                Application.DoEvents();
            };
            downloader.WorkFinished += (s, bytes, max, errCode) =>
            {
                if (max > 0L)
                {
                    double percent = 100.0 / max * bytes;
                    progressBar1.Value = (int)percent;
                    lblDownloadingProgress.Text = $"Скачано {bytes} из {max} ({string.Format("{0:F3}", percent)}%)";
                }
                if (errCode == 200)
                {
                    MessageBox.Show($"Скачано {bytes} байт", "Скачано!",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Код ошибки: {errCode}", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            downloader.CancelTest += (object s, ref bool stop) =>
            {
                stop = needCancel;
            };

            downloader.Url = editUrl.Text;
            string fn = editFileName.Text;
            if (File.Exists(fn))
            {
                File.Delete(fn);
            }
            Stream stream = File.OpenWrite(fn);
            int errorCode = downloader.Download(stream);
            stream.Dispose();
            System.Diagnostics.Debug.WriteLine($"Error code = {errorCode}");
            if (errorCode != 200)
            {
                ShowErrorMessage(errorCode);
            }

            isDownloading = false;
            needCancel = false;

            btnDownloadSingleThreaded.Text = "Download single threaded";
            btnDownloadSingleThreaded.Enabled = true;
            btnDownloadMultiThreaded.Enabled = true;
        }

        private async void btnDownloadMultiThreaded_Click(object sender, EventArgs e)
        {
            if (isDownloading)
            {
                needCancel = true;
                btnDownloadMultiThreaded.Text = "Stopping...";
                btnDownloadMultiThreaded.Enabled = false;
                return;
            }

            isDownloading = true;

            btnDownloadMultiThreaded.Text = "Stop";
            btnDownloadSingleThreaded.Enabled = false;
            numericUpDownThreadCount.Enabled = false;
            lblMergingProgress.Text = null;

            MultiThreadedDownloader multiThreadedDownloader = new MultiThreadedDownloader();
            multiThreadedDownloader.DownloadStarted += (s, max) =>
            {
                progressBar1.Value = 0;
                progressBar1.Maximum = 100;
                lblDownloadingProgress.Text = $"Скачано: 0 из {max}";
            };
            multiThreadedDownloader.DownloadProgress += (s, bytes) =>
            {
                long max = (s as MultiThreadedDownloader).ContentLength;
                if (max > 0L)
                {
                    double percent = 100.0 / max * bytes;
                    progressBar1.Value = (int)percent;
                    lblDownloadingProgress.Text = $"Скачано {bytes} из {max} ({string.Format("{0:F3}", percent)}%)";
                }
            };
            multiThreadedDownloader.DownloadFinished += (s, bytes, errCode, fileName) =>
            {
                if (errCode == 200)
                {
                    string t = $"Имя файла: {fileName}\nСкачано: {bytes} байт";
                    MessageBox.Show(t, "Скачано!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Код ошибки: {errCode}", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            multiThreadedDownloader.MergingStarted += (s, chunkCount) =>
            {
                progressBar1.Value = 0;
                progressBar1.Maximum = chunkCount;
                lblMergingProgress.Left = lblDownloadingProgress.Left + lblDownloadingProgress.Width;
                lblMergingProgress.Text = $"Объединение чанков: 0 / {chunkCount}";
            };
            multiThreadedDownloader.MergingProgress += (s, chunkId) =>
            {
                progressBar1.Value = chunkId + 1;
                lblMergingProgress.Text = $"Объединение чанков: {chunkId + 1} / {progressBar1.Maximum}";
            };
            multiThreadedDownloader.MergingFinished += (s, errCode) =>
            {
                lblMergingProgress.Text = errCode == 200 ? null : $"Ошибка объединения чанков! Код: {errCode}";
            };
            multiThreadedDownloader.CancelTest += (object s, ref bool stop) =>
            {
                stop = needCancel;
            };

            multiThreadedDownloader.ThreadCount = (int)numericUpDownThreadCount.Value;
            multiThreadedDownloader.Url = editUrl.Text;
            multiThreadedDownloader.OutputFileName = editFileName.Text;
            multiThreadedDownloader.TempDirectory = editTempPath.Text;
            multiThreadedDownloader.MergingDirectory = editMergingPath.Text;
            int errorCode = await multiThreadedDownloader.Download();
            System.Diagnostics.Debug.WriteLine($"Error code = {errorCode}");
            if (errorCode != 200)
            {
                ShowErrorMessage(errorCode);
            }

            isDownloading = false;
            needCancel = false;

            btnDownloadMultiThreaded.Text = "Download multi threaded";
            btnDownloadMultiThreaded.Enabled = true;
            btnDownloadSingleThreaded.Enabled = true;
            numericUpDownThreadCount.Enabled = true;
        }

        private void ShowErrorMessage(int errorCode)
        {
            switch (errorCode)
            {
                case MultiThreadedDownloader.DOWNLOAD_ERROR_CANCELED:
                    MessageBox.Show("Скачивание успешно отменено!", "Отменятор отменения отмены",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case MultiThreadedDownloader.DOWNLOAD_ERROR_NO_URL_SPECIFIED:
                    MessageBox.Show("Не указана ссылка!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case MultiThreadedDownloader.DOWNLOAD_ERROR_NO_FILE_NAME_SPECIFIED:
                    MessageBox.Show("Не указано имя файла!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case MultiThreadedDownloader.DOWNLOAD_ERROR_MERGING_CHUNKS:
                    MessageBox.Show("Ошибка объединения чанков!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case MultiThreadedDownloader.DOWNLOAD_ERROR_CREATE_FILE:
                    MessageBox.Show("Ошибка создания файла!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case MultiThreadedDownloader.DOWNLOAD_ERROR_TEMPORARY_DIR_NOT_EXISTS:
                    MessageBox.Show("Не найдена папка для временных файлов!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case MultiThreadedDownloader.DOWNLOAD_ERROR_MERGING_DIR_NOT_EXISTS:
                    MessageBox.Show("Не найдена папка для объединения чанков!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case FileDownloader.DOWNLOAD_ERROR_INCOMPLETE_DATA_READ:
                    MessageBox.Show("Ошибка чтения данных!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case FileDownloader.DOWNLOAD_ERROR_RANGE:
                    MessageBox.Show("Указан неверный диапазон!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case FileDownloader.DOWNLOAD_ERROR_ZERO_LENGTH_CONTENT:
                    MessageBox.Show("Файл на сервере пуст!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case FileDownloader.DOWNLOAD_ERROR_UNKNOWN:
                    MessageBox.Show("Неизвестная ошибка!", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                default:
                    MessageBox.Show($"Код ошибки: {errorCode}", "Ошибка!",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }
        }
    }
}