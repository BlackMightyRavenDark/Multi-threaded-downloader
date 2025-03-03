using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace MultiThreadedDownloaderLib
{
	public sealed class WebContent : IDisposable
	{
		public Stream Data { get; private set; }
		public long Length { get; private set; }

		public delegate void ProgressDelegate(long byteCount);

		private string _contentEncodingHeaderValue;

		public WebContent(Stream dataStream, long length, string contentEncodingHeaderValue = null)
		{
			Data = dataStream;
			Length = length;
			_contentEncodingHeaderValue = contentEncodingHeaderValue;
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

		public int ContentToStream(Stream outputStream, int bufferSize,
			ProgressDelegate progress, CancellationToken cancellationToken)
		{
			if (Data == null)
			{
				return FileDownloader.DOWNLOAD_ERROR_NULL_CONTENT;
			}

			byte[] buffer = new byte[bufferSize];
			long bytesTransferred = 0L;
			Stream readingStream = GetReadingStream(out bool isComressed);

			do
			{
				int bytesRead = readingStream.Read(buffer, 0, bufferSize);
				if (bytesRead <= 0) { break; }
				outputStream?.Write(buffer, 0, bytesRead);
				bytesTransferred += bytesRead;

				progress?.Invoke(bytesTransferred);
			}
			while (!cancellationToken.IsCancellationRequested);

			if (isComressed) { readingStream.Close(); }

			if (cancellationToken.IsCancellationRequested)
			{
				return FileDownloader.DOWNLOAD_ERROR_CANCELED_BY_USER;
			}
			else if (!isComressed && Length >= 0L && bytesTransferred != Length)
			{
				return FileDownloader.DOWNLOAD_ERROR_INCOMPLETE_DATA_READ;
			}

			return 200;
		}

		public int ContentToString(out string resultString, Encoding encoding, int bufferSize,
			ProgressDelegate progress, CancellationToken cancellationToken)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				{
					int errorCode = ContentToStream(stream, bufferSize, progress, cancellationToken);
					resultString = errorCode == 200 || errorCode == 206 ?
						encoding.GetString(stream.ToArray()) : null;
					return errorCode;
				}
			}
			catch (Exception ex)
			{
#if DEBUG
				System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
				resultString = ex.Message;
				return ex.HResult;
			}
		}

		public int ContentToString(out string resultString, int bufferSize,
			ProgressDelegate progress, CancellationToken cancellationToken)
		{
			return ContentToString(out resultString, Encoding.UTF8, bufferSize,
				progress, cancellationToken);
		}

		public int ContentToString(out string resultString, Encoding encoding, int bufferSize = 4096)
		{
			return ContentToString(out resultString, encoding, bufferSize, null, default);
		}

		public int ContentToString(out string resultString, int bufferSize = 4096)
		{
			return ContentToString(out resultString, Encoding.UTF8, bufferSize);
		}

		public bool IsCompressedContent()
		{
			return !string.IsNullOrEmpty(_contentEncodingHeaderValue) &&
				(_contentEncodingHeaderValue.Contains("gzip") ||
				_contentEncodingHeaderValue.Contains("br") ||
				_contentEncodingHeaderValue.Contains("deflate"));
		}

		private Stream GetReadingStream(out bool isCompressedData)
		{
			isCompressedData = IsCompressedContent();
			return isCompressedData ? new GZipStream(Data, CompressionMode.Decompress, true) : Data;
		}
	}
}
