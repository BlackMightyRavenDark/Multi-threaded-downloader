
namespace MultiThreadedDownloaderLib
{
	public class DependentTaskInfo
	{
		public MultiThreadedDownloader Owner { get; }
		public long ContentLength { get; }
		public bool IsRangeSupported { get; }
		public string ContentCompressionAlgorithm { get; }
		public bool IsCompressedContent { get; }

		public DependentTaskInfo(MultiThreadedDownloader owner, long contentLength,
			bool isRangeSupported, string contentCompressionAlgorithm)
		{
			Owner = owner;
			ContentLength = contentLength;
			IsRangeSupported = isRangeSupported;
			ContentCompressionAlgorithm = contentCompressionAlgorithm;
			IsCompressedContent = !string.IsNullOrEmpty(contentCompressionAlgorithm) && !string.IsNullOrWhiteSpace(contentCompressionAlgorithm);
		}
	}
}
