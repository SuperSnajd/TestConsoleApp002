using System.Collections.Concurrent;

namespace FinalTestLogIngest.Ingestion;

/// <summary>
/// Tracks file stability by monitoring size changes over time.
/// A file is considered stable when its size hasn't changed for a specified duration.
/// </summary>
public class DebounceTracker
{
    private readonly ConcurrentDictionary<string, FileDebounceInfo> _trackedFiles = new();

    /// <summary>
    /// Starts or updates tracking for a file.
    /// </summary>
    /// <param name="filePath">The full path to the file to track.</param>
    public void TrackFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            // File doesn't exist, remove from tracking if present
            _trackedFiles.TryRemove(filePath, out _);
            return;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var currentSize = fileInfo.Length;
            var now = DateTime.UtcNow;

            _trackedFiles.AddOrUpdate(
                filePath,
                _ => new FileDebounceInfo
                {
                    LastSeenSize = currentSize,
                    LastCheckTime = now,
                    IsStable = false
                },
                (_, existing) =>
                {
                    if (existing.LastSeenSize == currentSize)
                    {
                        // Size hasn't changed, keep the original check time
                        return existing;
                    }
                    else
                    {
                        // Size changed, reset tracking
                        return new FileDebounceInfo
                        {
                            LastSeenSize = currentSize,
                            LastCheckTime = now,
                            IsStable = false
                        };
                    }
                });
        }
        catch (IOException)
        {
            // File is locked or inaccessible, will retry later
        }
        catch (UnauthorizedAccessException)
        {
            // No access to file, remove from tracking
            _trackedFiles.TryRemove(filePath, out _);
        }
    }

    /// <summary>
    /// Checks if a file is stable (size hasn't changed for the specified duration).
    /// </summary>
    /// <param name="filePath">The full path to the file to check.</param>
    /// <param name="stableWaitMs">The minimum duration in milliseconds the file must remain unchanged.</param>
    /// <returns>True if the file is stable; otherwise, false.</returns>
    public bool IsFileStable(string filePath, int stableWaitMs)
    {
        if (!_trackedFiles.TryGetValue(filePath, out var info))
        {
            // File not tracked yet, track it now
            TrackFile(filePath);
            return false;
        }

        if (!File.Exists(filePath))
        {
            // File deleted, remove from tracking
            _trackedFiles.TryRemove(filePath, out _);
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var currentSize = fileInfo.Length;

            if (currentSize != info.LastSeenSize)
            {
                // Size changed since last check, reset tracking
                TrackFile(filePath);
                return false;
            }

            // Check if enough time has passed
            var elapsed = DateTime.UtcNow - info.LastCheckTime;
            return elapsed.TotalMilliseconds >= stableWaitMs;
        }
        catch (IOException)
        {
            // File is locked or inaccessible
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // No access to file, remove from tracking
            _trackedFiles.TryRemove(filePath, out _);
            return false;
        }
    }

    /// <summary>
    /// Removes a file from tracking (typically after successful processing).
    /// </summary>
    /// <param name="filePath">The full path to the file to stop tracking.</param>
    public void RemoveFile(string filePath)
    {
        _trackedFiles.TryRemove(filePath, out _);
    }

    /// <summary>
    /// Information about a file being tracked for stability.
    /// </summary>
    private class FileDebounceInfo
    {
        public long LastSeenSize { get; init; }
        public DateTime LastCheckTime { get; init; }
        public bool IsStable { get; init; }
    }
}

