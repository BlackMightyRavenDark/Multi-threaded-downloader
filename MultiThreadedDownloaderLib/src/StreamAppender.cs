using System.IO;
using System.Threading;

namespace MultiThreadedDownloaderLib
{
	public static class StreamAppender
	{
		public delegate void StreamAppendStartedDelegate(long sourcePosition, long sourceLength,
			long destinationPosition, long destinationLength);
		public delegate void StreamAppendProgressDelegate(long sourcePosition, long sourceLength,
			long destinationPosition, long destinationLength);
		public delegate void StreamAppendFinishedDelegate(long sourcePosition, long sourceLength,
			long destinationPosition, long destinationLength);

		public static bool Append(Stream inputStream, Stream outputStream,
			StreamAppendStartedDelegate streamAppendStarted,
			StreamAppendProgressDelegate streamAppendProgress,
			StreamAppendFinishedDelegate streamAppendFinished,
			CancellationToken cancellationToken,
			long updateIntervalMilliseconds, int bufferSize = 4096)
		{
			if (bufferSize <= 0 ||
				inputStream == null || outputStream == null ||
				inputStream.Position != 0L || outputStream.Position != outputStream.Length)
			{
				return false;
			}

			streamAppendStarted?.Invoke(inputStream.Position, inputStream.Length,
				outputStream.Position, outputStream.Length);

			long inputStreamLength = inputStream.Length;
			long outputStreamInitialLength = outputStream.Length;
			byte[] buffer = new byte[bufferSize];

			System.Diagnostics.Stopwatch stopwatch =
				streamAppendProgress != null && updateIntervalMilliseconds > 0L ?
				new System.Diagnostics.Stopwatch() : null;
			do
			{
				int bytesRead = inputStream.Read(buffer, 0, buffer.Length);
				if (bytesRead <= 0) { break; }
				outputStream.Write(buffer, 0, bytesRead);

				if (stopwatch != null && stopwatch.ElapsedMilliseconds >= updateIntervalMilliseconds)
				{
					streamAppendProgress.Invoke(
						inputStream.Position, inputStreamLength,
						outputStream.Position, outputStream.Length);
					stopwatch.Reset();
				}
			} while (!cancellationToken.IsCancellationRequested);
			streamAppendProgress?.Invoke(inputStream.Position, inputStreamLength,
				outputStream.Position, outputStream.Length);

			streamAppendFinished?.Invoke(inputStream.Position, inputStreamLength,
				outputStream.Position, outputStream.Length);

			return outputStream.Length == outputStreamInitialLength + inputStreamLength;
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
