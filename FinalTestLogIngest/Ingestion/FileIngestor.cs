using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FinalTestLogIngest.Options;
using FinalTestLogIngest.Parsing;
using FinalTestLogIngest.Persistence.Repositories;
using FinalTestLogIngest.Logging;

namespace FinalTestLogIngest.Ingestion;

/// <summary>
/// Orchestrates the complete file ingestion pipeline:
/// read → parse → compute hash → persist → archive
/// </summary>
public class FileIngestor
{
    private readonly ILogger<FileIngestor> _logger;
    private readonly FinalTestLogRepository _repository;
    private readonly IngestionMetrics _metrics;
    private readonly ArchiveOptions _archiveOptions;
    private readonly WatcherOptions _watcherOptions;

    public FileIngestor(
        ILogger<FileIngestor> logger,
        FinalTestLogRepository repository,
        IngestionMetrics metrics,
        IOptions<ArchiveOptions> archiveOptions,
        IOptions<WatcherOptions> watcherOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _archiveOptions = archiveOptions?.Value ?? throw new ArgumentNullException(nameof(archiveOptions));
        _watcherOptions = watcherOptions?.Value ?? throw new ArgumentNullException(nameof(watcherOptions));
    }

    /// <summary>
    /// Processes a single file through the complete ingestion pipeline.
    /// </summary>
    /// <param name="filePath">The full path to the file to process.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>True if processing succeeded; false otherwise.</returns>
    public async Task<bool> ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning(LogEvents.FileNotFound, 
                "ProcessFileAsync called with null or empty file path");
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning(LogEvents.FileNotFound, 
                "File does not exist: {FilePath}", filePath);
            return false;
        }

        _logger.LogDebug(LogEvents.FileProcessingStarted, 
            "Starting ingestion pipeline for file: {FilePath}", filePath);

        try
        {
            // Step 1: Read file content with shared read access
            string rawText;
            try
            {
                rawText = await ReadFileWithRetryAsync(filePath, cancellationToken);
                _logger.LogDebug(LogEvents.FileReadCompleted, 
                    "File read completed: {FilePath}, Size: {Size} bytes", 
                    filePath, rawText.Length);
            }
            catch (IOException ex)
            {
                _logger.LogError(LogEvents.FileReadError, ex, 
                    "Failed to read file after retries: {FilePath}", filePath);
                _metrics.IncrementErrored();
                return false;
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning(LogEvents.FileEmpty, 
                    "File is empty: {FilePath}", filePath);
                _metrics.IncrementErrored();
                return false;
            }

            // Step 2: Parse the log file
            var fileName = Path.GetFileName(filePath);
            var parsedLog = FinalTestLogParser.Parse(rawText, fileName);
            _logger.LogDebug(LogEvents.FileParsed, 
                "File parsed successfully: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}", 
                filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial);

            // Step 3: Compute ContentSha256 hash
            parsedLog.ContentSha256 = ComputeSha256Hash(rawText);

            // Step 4: Persist to database with deduplication and versioning
            var persistenceResult = await _repository.UpsertWithVersioningAsync(parsedLog, cancellationToken);

            // Log appropriate message based on persistence result and track metrics
            switch (persistenceResult)
            {
                case Persistence.Repositories.PersistenceResult.Inserted:
                    _logger.LogInformation(LogEvents.FileInserted,
                        "Inserted new document: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}, Version: {Version}",
                        filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial, parsedLog.Version);
                    _metrics.IncrementInserted();
                    break;

                case Persistence.Repositories.PersistenceResult.Updated:
                    _logger.LogInformation(LogEvents.FileUpdated,
                        "Updated existing document: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}, Version: {Version} (replaced version {PreviousVersion})",
                        filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial, parsedLog.Version, parsedLog.Version - 1);
                    _metrics.IncrementUpdated();
                    break;

                case Persistence.Repositories.PersistenceResult.Duplicate:
                    _logger.LogInformation(LogEvents.FileDuplicate,
                        "Skipped duplicate document: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}, ContentSha256: {ContentSha256}",
                        filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial, parsedLog.ContentSha256);
                    _metrics.IncrementDuplicate();
                    break;
            }

            // Step 5: Archive file on success
            ArchiveFile(filePath, isSuccess: true);
            _metrics.IncrementProcessed();
            return true;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(LogEvents.ParseError, ex, 
                "Parse error for file: {FilePath} - {Message}", filePath, ex.Message);
            _metrics.IncrementErrored();
            ArchiveFile(filePath, isSuccess: false);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.PersistError, ex, 
                "Unexpected error processing file: {FilePath}", filePath);
            _metrics.IncrementErrored();
            ArchiveFile(filePath, isSuccess: false);
            return false;
        }
    }

    /// <summary>
    /// Reads a file with shared read access and retries on transient failures.
    /// </summary>
    private async Task<string> ReadFileWithRetryAsync(string filePath, CancellationToken cancellationToken, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Use FileShare.ReadWrite to allow other processes to access the file
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                return await reader.ReadToEndAsync(cancellationToken);
            }
            catch (IOException) when (attempt < maxRetries - 1)
            {
                // File is locked, wait and retry
                await Task.Delay(500, cancellationToken);
            }
        }

        throw new IOException($"Failed to read file after {maxRetries} attempts: {filePath}");
    }

    /// <summary>
    /// Archives a file to the appropriate folder based on processing result.
    /// </summary>
    /// <param name="sourcePath">The full path to the file to archive.</param>
    /// <param name="isSuccess">True if processing succeeded, false for errors.</param>
    private void ArchiveFile(string sourcePath, bool isSuccess)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                _logger.LogWarning("Cannot archive file - file does not exist: {FilePath}", sourcePath);
                return;
            }

            // Determine target base folder
            var targetBaseFolder = isSuccess ? _archiveOptions.SuccessPath : _archiveOptions.ErrorPath;
            var operation = isSuccess ? _archiveOptions.OnSuccess : _archiveOptions.OnError;

            if (string.IsNullOrWhiteSpace(targetBaseFolder))
            {
                _logger.LogWarning("Archive path not configured for {Result}, skipping archive: {FilePath}",
                    isSuccess ? "success" : "error", sourcePath);
                return;
            }

            // Calculate target path
            string targetPath;
            if (_archiveOptions.PreserveSubfolders)
            {
                // Preserve subfolder structure relative to watch folder
                var relativePath = Path.GetRelativePath(_watcherOptions.Path, sourcePath);
                targetPath = Path.Combine(targetBaseFolder, relativePath);
            }
            else
            {
                // Just use filename
                var fileName = Path.GetFileName(sourcePath);
                targetPath = Path.Combine(targetBaseFolder, fileName);
            }

            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Handle filename conflicts with timestamped naming
            if (File.Exists(targetPath))
            {
                targetPath = GenerateTimestampedPath(targetPath);
            }

            // Perform the archive operation
            if (string.Equals(operation, "Move", StringComparison.OrdinalIgnoreCase))
            {
                File.Move(sourcePath, targetPath);
                _logger.LogInformation(LogEvents.FileArchived, 
                    "Archived (moved) {Result} file: {SourcePath} → {TargetPath}",
                    isSuccess ? "success" : "error", sourcePath, targetPath);
                _metrics.IncrementArchived();
            }
            else if (string.Equals(operation, "Copy", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, targetPath, overwrite: false);
                _logger.LogInformation(LogEvents.FileArchived, 
                    "Archived (copied) {Result} file: {SourcePath} → {TargetPath}",
                    isSuccess ? "success" : "error", sourcePath, targetPath);
                _metrics.IncrementArchived();
            }
            else
            {
                _logger.LogWarning(LogEvents.ArchiveError, 
                    "Unknown archive operation '{Operation}', skipping archive: {FilePath}",
                    operation, sourcePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(LogEvents.ArchiveError, ex, 
                "Failed to archive file: {FilePath}", sourcePath);
            // Don't throw - archive failure should not prevent ingestion success
        }
    }

    /// <summary>
    /// Generates a timestamped filename to avoid conflicts.
    /// Pattern: {name}-{yyyyMMdd_HHmmss}{ext}
    /// </summary>
    /// <param name="originalPath">The original target path with conflict.</param>
    /// <returns>A new path with timestamp inserted before the extension.</returns>
    private string GenerateTimestampedPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var newFileName = $"{fileNameWithoutExt}-{timestamp}{extension}";
        return Path.Combine(directory, newFileName);
    }

    /// <summary>
    /// Computes SHA256 hash of the input text.
    /// </summary>
    private static string ComputeSha256Hash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

