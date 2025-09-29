using System;
using System.IO;

namespace MultiThreadedDownloaderLib
{
	public sealed class ContentChunkStream : IDisposable
	{
		public Stream Stream { get; private set; }
		public string FilePath { get; }

		public ContentChunkStream(Stream stream, string filePath = null)
		{
			Stream = stream;
			FilePath = filePath;
		}

		public void Dispose()
		{
			if (Stream != null)
			{
				Stream.Dispose();
				Stream = null;
			}
		}

		public bool IsFileStream()
		{
			return !string.IsNullOrEmpty(FilePath) && !string.IsNullOrWhiteSpace(FilePath);
		}

		public bool DeleteFile(out string errorMessage)
		{
			try
			{
				if (!IsFileStream())
				{
					errorMessage = "Not a file stream";
					return false;
				}

				if (File.Exists(FilePath))
				{
					File.Delete(FilePath);
					errorMessage = null;
					return true;
				}

				errorMessage = "File not found";
			} catch (Exception ex)
			{
				errorMessage = ex.Message;
			}

			return false;
		}
	}
}
