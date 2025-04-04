
namespace MultiThreadedDownloaderLib
{
	public sealed class DownloadableChunk
	{
		public ContentChunkStream OutputStream { get; }
		public DownloadRange Range { get; }

		public DownloadableChunk(ContentChunkStream outputStream, DownloadRange downloadRange)
		{
			OutputStream = outputStream;
			Range = downloadRange;
		}
	}
}
