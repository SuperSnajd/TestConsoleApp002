using System.Threading;

namespace FinalTestLogIngest.Logging;

/// <summary>
/// Thread-safe metrics tracker for file ingestion operations.
/// Tracks counts of various processing outcomes for observability and reporting.
/// </summary>
public class IngestionMetrics
{
    private long _filesQueued;
    private long _filesProcessed;
    private long _filesInserted;
    private long _filesUpdated;
    private long _filesDuplicate;
    private long _filesArchived;
    private long _filesErrored;

    /// <summary>Total number of files added to the processing queue.</summary>
    public long FilesQueued => Interlocked.Read(ref _filesQueued);

    /// <summary>Total number of files that completed processing (success or error).</summary>
    public long FilesProcessed => Interlocked.Read(ref _filesProcessed);

    /// <summary>Number of new documents inserted into the database.</summary>
    public long FilesInserted => Interlocked.Read(ref _filesInserted);

    /// <summary>Number of documents updated with new versions.</summary>
    public long FilesUpdated => Interlocked.Read(ref _filesUpdated);

    /// <summary>Number of duplicate files skipped (same content hash).</summary>
    public long FilesDuplicate => Interlocked.Read(ref _filesDuplicate);

    /// <summary>Number of files successfully archived.</summary>
    public long FilesArchived => Interlocked.Read(ref _filesArchived);

    /// <summary>Number of files that failed processing (parse or persistence errors).</summary>
    public long FilesErrored => Interlocked.Read(ref _filesErrored);

    /// <summary>Increments the queued files counter.</summary>
    public void IncrementQueued() => Interlocked.Increment(ref _filesQueued);

    /// <summary>Increments the processed files counter.</summary>
    public void IncrementProcessed() => Interlocked.Increment(ref _filesProcessed);

    /// <summary>Increments the inserted files counter.</summary>
    public void IncrementInserted() => Interlocked.Increment(ref _filesInserted);

    /// <summary>Increments the updated files counter.</summary>
    public void IncrementUpdated() => Interlocked.Increment(ref _filesUpdated);

    /// <summary>Increments the duplicate files counter.</summary>
    public void IncrementDuplicate() => Interlocked.Increment(ref _filesDuplicate);

    /// <summary>Increments the archived files counter.</summary>
    public void IncrementArchived() => Interlocked.Increment(ref _filesArchived);

    /// <summary>Increments the errored files counter.</summary>
    public void IncrementErrored() => Interlocked.Increment(ref _filesErrored);

    /// <summary>
    /// Returns a formatted summary of all metrics.
    /// </summary>
    public string GetSummary()
    {
        return $"Queued: {FilesQueued}, Processed: {FilesProcessed}, " +
               $"Inserted: {FilesInserted}, Updated: {FilesUpdated}, " +
               $"Duplicate: {FilesDuplicate}, Archived: {FilesArchived}, " +
               $"Errors: {FilesErrored}";
    }
}

