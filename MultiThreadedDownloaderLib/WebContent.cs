﻿using System;
using System.IO;
using System.Text;
using System.Threading;

namespace MultiThreadedDownloaderLib
{
    public sealed class WebContent : IDisposable
    {
        public Stream Data { get; private set; }
        public long Length { get; private set; }

        public delegate void ProgressDelegate(long byteCount);

        public WebContent(Stream dataStream, long length)
        {
            Data = dataStream;
            Length = length;
        }

        public void Dispose()
        {
            if (Data != null)
            {
                Data.Close();
                Data = null;
            }

            Length = -1L;
        }

        public int ContentToStream(Stream stream, int bufferSize,
            ProgressDelegate progress, CancellationToken cancellationToken)
        {
            if (Data == null)
            {
                return FileDownloader.DOWNLOAD_ERROR_NULL_CONTENT;
            }

            byte[] buf = new byte[bufferSize];
            long bytesTransfered = 0L;
            do
            {
                int bytesRead = Data.Read(buf, 0, buf.Length);
                if (bytesRead <= 0)
                {
                    break;
                }
                stream.Write(buf, 0, bytesRead);
                bytesTransfered += bytesRead;

                progress?.Invoke(bytesTransfered);
            }
            while (!cancellationToken.IsCancellationRequested);

            if (cancellationToken.IsCancellationRequested)
            {
                return FileDownloader.DOWNLOAD_ERROR_CANCELED_BY_USER;
            }
            else if (Length >= 0L && bytesTransfered != Length)
            {
                return FileDownloader.DOWNLOAD_ERROR_INCOMPLETE_DATA_READ;
            }

            return 200;
        }

        public int ContentToString(out string resultString, int bufferSize,
            ProgressDelegate progress, CancellationToken cancellationToken)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    int errorCode = ContentToStream(stream, bufferSize, progress, cancellationToken);
                    resultString = errorCode == 200 || errorCode == 206 ?
                        Encoding.UTF8.GetString(stream.ToArray()) : null;
                    return errorCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                resultString = ex.Message;
                return ex.HResult;
            }
        }

        public int ContentToString(out string resultString, int bufferSize = 4096)
        {
            return ContentToString(out resultString, bufferSize, null, default);
        }
    }
}
