
namespace MultiThreadedDownloaderLib
{
	public sealed class DownloadableChunk
	{
		public ContentChunkStream OutputStream { get; }
		public long ByteFrom { get; }
		public long ByteTo { get; }

		public DownloadableChunk(ContentChunkStream outputStream, long byteFrom, long byteTo)
		{
			OutputStream = outputStream;
			ByteFrom = byteFrom;
			ByteTo = byteTo;
		}
	}
}
