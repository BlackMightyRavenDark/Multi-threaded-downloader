
namespace MultiThreadedDownloaderLib
{
	public class DownloadRange
	{
		public long StartPosition { get; }
		public long EndPosition { get; }
		public long ContentLength { get; internal set; }
		public bool IsValid => IsValidRange(StartPosition, EndPosition, ContentLength);
		public long Length => EndPosition - StartPosition + 1L;

		public DownloadRange(long startPosition, long endPosition, long contentLength = -1L)
		{
			StartPosition = startPosition;
			EndPosition = endPosition;
			ContentLength = contentLength;
		}

		public string GetFormattedString()
		{
			return ContentLength >= 0 && EndPosition < 0L ?
				$"{StartPosition}-{ContentLength}" : $"{StartPosition}-{EndPosition}";
		}

		public static bool IsValidRange(long startPosition, long endPosition, long contentLength = -1L)
		{
			if (contentLength > 0L)
			{
				return startPosition < contentLength && (endPosition < 0L || (endPosition >= startPosition && endPosition < contentLength));
			}
			else if (contentLength < 0L)
			{
				return startPosition >= 0L && (endPosition < 0L || startPosition < endPosition);
			}
			else
			{
				return false;
			}
		}
	}
}
