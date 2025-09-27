
namespace MultiThreadedDownloaderLib
{
	public sealed class DownloadableTask
	{
		public string Url { get; }
		public DownloadableChunk DownloadableChunk { get; }
		public int TaskId { get; }
		public long FullContentLength { get; }
		public long ChunkFileSize { get; }
		public long ProcessedBytes { get; }
		public bool IsCompleted => State == DownloadableTaskState.Finished &&
			ChunkFileSize > 0L && ProcessedBytes == ChunkFileSize;
		public int TryNumber { get; }
		public int TryCountLimit { get; }
		public DownloadableTaskState State { get; }

		public DownloadableTask(string url, DownloadableChunk downloadableChunk, int taskId,
			long fullContentLength, long processedBytes, int tryNumber, int tryCountLimit,
			DownloadableTaskState state)
		{
			Url = url;
			DownloadableChunk = downloadableChunk;
			TaskId = taskId;
			FullContentLength = fullContentLength;
			ChunkFileSize = DownloadableChunk?.Range != null && DownloadableChunk.Range.EndPosition >= 0L ?
				DownloadableChunk.Range.Length : (fullContentLength > 0L ? fullContentLength - 1L : -1L);
			ProcessedBytes = processedBytes;
			TryNumber = tryNumber;
			TryCountLimit = tryCountLimit;
			State = state;
		}
	}

	public enum DownloadableTaskState
	{
		Preparing, Connecting, Connected, Downloading, Finished, Errored
	}
}
