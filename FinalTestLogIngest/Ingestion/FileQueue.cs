using System.Collections.Concurrent;

namespace FinalTestLogIngest.Ingestion;

/// <summary>
/// Thread-safe queue for managing files awaiting processing.
/// </summary>
public class FileQueue
{
    private readonly ConcurrentQueue<string> _queue = new();

    /// <summary>
    /// Gets the current number of files in the queue.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Adds a file path to the processing queue.
    /// </summary>
    /// <param name="filePath">The full path to the file to be processed.</param>
    public void Enqueue(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        _queue.Enqueue(filePath);
    }

    /// <summary>
    /// Attempts to remove and return a file path from the queue.
    /// </summary>
    /// <param name="filePath">The file path if successful; otherwise, null.</param>
    /// <returns>True if a file was successfully dequeued; otherwise, false.</returns>
    public bool TryDequeue(out string? filePath)
    {
        return _queue.TryDequeue(out filePath);
    }
}

