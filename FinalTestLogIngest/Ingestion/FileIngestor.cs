using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FinalTestLogIngest.Options;
using FinalTestLogIngest.Parsing;
using FinalTestLogIngest.Persistence.Repositories;

namespace FinalTestLogIngest.Ingestion;

/// <summary>
/// Orchestrates the complete file ingestion pipeline:
/// read → parse → compute hash → persist → archive
/// </summary>
public class FileIngestor
{
    private readonly ILogger<FileIngestor> _logger;
    private readonly FinalTestLogRepository _repository;
    private readonly ArchiveOptions _archiveOptions;

    public FileIngestor(
        ILogger<FileIngestor> logger,
        FinalTestLogRepository repository,
        IOptions<ArchiveOptions> archiveOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _archiveOptions = archiveOptions?.Value ?? throw new ArgumentNullException(nameof(archiveOptions));
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
            _logger.LogWarning("ProcessFileAsync called with null or empty file path");
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File does not exist: {FilePath}", filePath);
            return false;
        }

        try
        {
            // Step 1: Read file content with shared read access
            string rawText;
            try
            {
                rawText = await ReadFileWithRetryAsync(filePath, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to read file after retries: {FilePath}", filePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning("File is empty: {FilePath}", filePath);
                return false;
            }

            // Step 2: Parse the log file
            var fileName = Path.GetFileName(filePath);
            var parsedLog = FinalTestLogParser.Parse(rawText, fileName);

            // Step 3: Compute ContentSha256 hash
            parsedLog.ContentSha256 = ComputeSha256Hash(rawText);

            // Step 4: Persist to database with deduplication and versioning
            var persistenceResult = await _repository.UpsertWithVersioningAsync(parsedLog, cancellationToken);

            // Log appropriate message based on persistence result
            switch (persistenceResult)
            {
                case Persistence.Repositories.PersistenceResult.Inserted:
                    _logger.LogInformation(
                        "Inserted new document: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}, Version: {Version}",
                        filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial, parsedLog.Version);
                    break;

                case Persistence.Repositories.PersistenceResult.Updated:
                    _logger.LogInformation(
                        "Updated existing document: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}, Version: {Version} (replaced version {PreviousVersion})",
                        filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial, parsedLog.Version, parsedLog.Version - 1);
                    break;

                case Persistence.Repositories.PersistenceResult.Duplicate:
                    _logger.LogInformation(
                        "Skipped duplicate document: {FilePath}, Id: {Id}, DeviceSerial: {DeviceSerial}, ContentSha256: {ContentSha256}",
                        filePath, parsedLog.Id, parsedLog.Identity.DeviceSerial, parsedLog.ContentSha256);
                    break;
            }

            // Step 5: Archive will be implemented in Phase 7
            // For now, just log success
            return true;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Parse error for file: {FilePath} - {Message}", filePath, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing file: {FilePath}", filePath);
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
    /// Computes SHA256 hash of the input text.
    /// </summary>
    private static string ComputeSha256Hash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

