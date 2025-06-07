using System.IO;
using System.Threading;

namespace MultiThreadedDownloaderLib
{
	public static class StreamAppender
	{
		public delegate void StreamAppendStartedDelegate(long sourcePosition, long sourceLength,
			long destinationPosition, long destinationLength);
		public delegate void StreamAppendProgressDelegate(long sourcePosition, long sourceLength,
			long destinationPosition, long destinationLength, long bytesTransferred);
		public delegate void StreamAppendFinishedDelegate(long sourcePosition, long sourceLength,
			long destinationPosition, long destinationLength, long bytesTransferred);

		public static bool Append(Stream inputStream, Stream outputStream,
			StreamAppendStartedDelegate streamAppendStarted,
			StreamAppendProgressDelegate streamAppendProgress,
			StreamAppendFinishedDelegate streamAppendFinished,
			CancellationToken cancellationToken,
			long updateIntervalMilliseconds, int bufferSize = 4096)
		{
			if (bufferSize <= 0 ||
				inputStream == null || inputStream.Length <= 0L || outputStream == null ||
				inputStream.Position != 0L || outputStream.Position != outputStream.Length)
			{
				return false;
			}

			streamAppendStarted?.Invoke(inputStream.Position, inputStream.Length,
				outputStream.Position, outputStream.Length);

			long bytesTransferred = 0L;
			long inputStreamLength = inputStream.Length;
			long outputStreamInitialLength = outputStream.Length;
			byte[] buffer = new byte[bufferSize];

			System.Diagnostics.Stopwatch stopwatch =
				streamAppendProgress != null && updateIntervalMilliseconds > 0L ?
				new System.Diagnostics.Stopwatch() : null;
			stopwatch?.Start();
			do
			{
				int bytesRead = inputStream.Read(buffer, 0, bufferSize);
				if (bytesRead <= 0) { break; }
				outputStream.Write(buffer, 0, bytesRead);
				bytesTransferred += bytesRead;

				if (stopwatch != null && stopwatch.ElapsedMilliseconds >= updateIntervalMilliseconds)
				{
					streamAppendProgress.Invoke(
						inputStream.Position, inputStreamLength,
						outputStream.Position, outputStream.Length,
						bytesTransferred);
					stopwatch.Restart();
				}
			} while (!cancellationToken.IsCancellationRequested);
			stopwatch?.Stop();

			if (!cancellationToken.IsCancellationRequested)
			{
				streamAppendProgress?.Invoke(inputStream.Position, inputStreamLength,
					outputStream.Position, outputStream.Length, bytesTransferred);
				if (streamAppendFinished != null && (streamAppendProgress == null ||
					(streamAppendFinished.Method != streamAppendProgress.Method)))
				{
					streamAppendFinished.Invoke(inputStream.Position, inputStreamLength,
						outputStream.Position, outputStream.Length, bytesTransferred);
				}
			}

			return outputStream.Length == outputStreamInitialLength + inputStreamLength && inputStreamLength == bytesTransferred;
		}

		public static bool Append(Stream inputStream, Stream outputStream,
			StreamAppendStartedDelegate streamAppendStarted,
			StreamAppendProgressDelegate streamAppendProgress,
			StreamAppendFinishedDelegate streamAppendFinished,
			CancellationToken cancellationToken,
			long updateIntervalMilliseconds = 100L)
		{
			return Append(inputStream, outputStream,
				streamAppendStarted, streamAppendProgress, streamAppendFinished,
				cancellationToken, updateIntervalMilliseconds, 4096);
		}

		public static bool Append(Stream inputStream, Stream outputStream,
			StreamAppendStartedDelegate streamAppendStarted,
			StreamAppendProgressDelegate streamAppendProgress,
			StreamAppendFinishedDelegate streamAppendFinished,
			long updateIntervalMilliseconds = 100L)
		{
			return Append(inputStream, outputStream,
				streamAppendStarted, streamAppendProgress, streamAppendFinished,
				default, updateIntervalMilliseconds);
		}

		public static bool Append(Stream inputStream, Stream outputStream)
		{
			return Append(inputStream, outputStream, null, null, null);
		}
	}
}
