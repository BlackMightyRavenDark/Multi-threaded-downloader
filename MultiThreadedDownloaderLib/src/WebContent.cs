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
		public bool IsCompressed { get; }
		public string CompressionAlgorithm { get; }


		public delegate void ProgressDelegate(long byteCount);

		public WebContent(Stream dataStream, long dataStreamLength, string contentEncodingHeaderValue = null)
		{
			Data = dataStream;
			Length = dataStreamLength;
			IsCompressed = Utils.IsCompressedContent(contentEncodingHeaderValue, out string id);
			CompressionAlgorithm = id;
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

			Stream readingStream = GetReadingStream();
			if (IsCompressed && readingStream == null)
			{
				return FileDownloader.DOWNLOAD_ERROR_UNSUPPORTED_COMPRESSION_ALGORITHM;
			}

			byte[] buffer = new byte[bufferSize];
			long bytesTransferred = 0L;
			do
			{
				int bytesRead = readingStream.Read(buffer, 0, bufferSize);
				if (bytesRead <= 0) { break; }
				outputStream?.Write(buffer, 0, bytesRead);
				bytesTransferred += bytesRead;

				progress?.Invoke(bytesTransferred);
			}
			while (!cancellationToken.IsCancellationRequested);

			if (IsCompressed) { readingStream.Close(); }

			if (cancellationToken.IsCancellationRequested)
			{
				return FileDownloader.DOWNLOAD_ERROR_CANCELED;
			}
			else if (!IsCompressed && Length >= 0L && bytesTransferred != Length)
			{
				return FileDownloader.DOWNLOAD_ERROR_DATA_SIZE_MISMATCH;
			}

			return 200;
		}

		public int ContentToStream(Stream outputStream, int bufferSize = 4096)
		{
			return ContentToStream(outputStream, bufferSize, null, default);
		}

		public int ContentToString(out string resultString, Encoding encoding, int bufferSize,
			ProgressDelegate progress, CancellationToken cancellationToken)
		{
			if (Data == null)
			{
				resultString = null;
				return FileDownloader.DOWNLOAD_ERROR_NULL_CONTENT;
			}

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

		private Stream GetReadingStream()
		{
			if (IsCompressed)
			{
				switch (CompressionAlgorithm)
				{
					case "gzip": return new GZipStream(Data, CompressionMode.Decompress, true);
					case "deflate": return new DeflateStream(Data, CompressionMode.Decompress, true);
					case "br": return new BrotliSharpLib.BrotliStream(Data, CompressionMode.Decompress, true);
					case "zstd": return new Zstandard.Net.ZstandardStream(Data, CompressionMode.Decompress, true);
					default: return null;
				}
			}

			return Data;
		}
	}
}
