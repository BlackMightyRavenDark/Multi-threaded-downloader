
namespace MultiThreadedDownloaderLib
{
	public class DownloadRange
	{
		public long StartPosition { get; }
		public long EndPosition { get; }
		public bool IsValid => IsValidRange(StartPosition, EndPosition);
		public long Length => EndPosition - StartPosition + 1L;

		public DownloadRange(long startPosition, long endPosition)
		{
			StartPosition = startPosition;
			EndPosition = endPosition;
		}

		public static bool IsValidRange(long startPosition, long endPosition)
		{
			return startPosition >= 0L && (endPosition < 0L || endPosition >= startPosition);
		}
	}
}
