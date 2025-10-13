using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using FinalTestLogIngest.Options;

namespace FinalTestLogIngest.Ingestion;

/// <summary>
/// Background service that monitors a directory for log files and processes them.
/// Uses FileSystemWatcher for real-time monitoring and processes files through
/// a debounce/queue mechanism to ensure files are fully written before processing.
/// </summary>
public class FileWatcherService : BackgroundService
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FileQueue _fileQueue;
    private readonly DebounceTracker _debounceTracker;
    private readonly WatcherOptions _watcherOptions;
    private readonly ProcessingOptions _processingOptions;
    private FileSystemWatcher? _fileSystemWatcher;

    public FileWatcherService(
        ILogger<FileWatcherService> logger,
        IServiceProvider serviceProvider,
        FileQueue fileQueue,
        DebounceTracker debounceTracker,
        IOptions<WatcherOptions> watcherOptions,
        IOptions<ProcessingOptions> processingOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _fileQueue = fileQueue ?? throw new ArgumentNullException(nameof(fileQueue));
        _debounceTracker = debounceTracker ?? throw new ArgumentNullException(nameof(debounceTracker));
        _watcherOptions = watcherOptions?.Value ?? throw new ArgumentNullException(nameof(watcherOptions));
        _processingOptions = processingOptions?.Value ?? throw new ArgumentNullException(nameof(processingOptions));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileWatcherService starting...");

        try
        {
            // Ensure watch directory exists
            if (!Directory.Exists(_watcherOptions.Path))
            {
                _logger.LogError("Watch directory does not exist: {Path}", _watcherOptions.Path);
                return;
            }

            // Perform initial scan if enabled
            if (_watcherOptions.InitialScan)
            {
                PerformInitialScan();
            }

            // Set up FileSystemWatcher
            SetupFileSystemWatcher();

            // Start processing loop
            await ProcessQueueAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FileWatcherService stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in FileWatcherService");
            throw;
        }
        finally
        {
            _fileSystemWatcher?.Dispose();
            _logger.LogInformation("FileWatcherService stopped");
        }
    }

    /// <summary>
    /// Performs an initial non-recursive scan of the watch directory for existing log files.
    /// </summary>
    private void PerformInitialScan()
    {
        _logger.LogInformation("Performing initial scan of directory: {Path}", _watcherOptions.Path);

        try
        {
            var searchOption = _watcherOptions.IncludeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(_watcherOptions.Path, _watcherOptions.Filter, searchOption);

            foreach (var file in files)
            {
                _fileQueue.Enqueue(file);
                _logger.LogDebug("Queued existing file: {FilePath}", file);
            }

            _logger.LogInformation("Initial scan complete. Found {Count} file(s)", files.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial scan of directory: {Path}", _watcherOptions.Path);
        }
    }

    /// <summary>
    /// Sets up the FileSystemWatcher to monitor for file changes.
    /// </summary>
    private void SetupFileSystemWatcher()
    {
        _fileSystemWatcher = new FileSystemWatcher(_watcherOptions.Path)
        {
            Filter = _watcherOptions.Filter,
            IncludeSubdirectories = _watcherOptions.IncludeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        // Subscribe to events
        _fileSystemWatcher.Created += OnFileEvent;
        _fileSystemWatcher.Changed += OnFileEvent;
        _fileSystemWatcher.Renamed += OnFileRenamed;

        // Start monitoring
        _fileSystemWatcher.EnableRaisingEvents = true;

        _logger.LogInformation(
            "FileSystemWatcher started for path: {Path}, filter: {Filter}, includeSubdirectories: {IncludeSubdirectories}",
            _watcherOptions.Path,
            _watcherOptions.Filter,
            _watcherOptions.IncludeSubdirectories);
    }

    /// <summary>
    /// Event handler for Created and Changed events.
    /// </summary>
    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
        {
            _logger.LogDebug("File event detected: {ChangeType} - {FilePath}", e.ChangeType, e.FullPath);
            _fileQueue.Enqueue(e.FullPath);
        }
    }

    /// <summary>
    /// Event handler for Renamed events.
    /// </summary>
    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        _fileQueue.Enqueue(e.FullPath);
    }

    /// <summary>
    /// Main processing loop that dequeues files, checks stability, and processes them.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting queue processing loop");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check if there are files in the queue
                if (_fileQueue.Count == 0)
                {
                    // No files to process, wait briefly
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                // Try to dequeue a file
                if (!_fileQueue.TryDequeue(out var filePath) || string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                // Check if file still exists
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("File no longer exists, skipping: {FilePath}", filePath);
                    _debounceTracker.RemoveFile(filePath);
                    continue;
                }

                // Track the file and check if it's stable
                _debounceTracker.TrackFile(filePath);

                if (!_debounceTracker.IsFileStable(filePath, _processingOptions.StableWaitMs))
                {
                    // File not stable yet, re-queue and wait
                    _fileQueue.Enqueue(filePath);
                    await Task.Delay(200, stoppingToken);
                    continue;
                }

                // File is stable, process it
                _logger.LogInformation("File is stable, processing: {FilePath}", filePath);
                await ProcessStableFileAsync(filePath, stoppingToken);

                // Remove from tracking after processing
                _debounceTracker.RemoveFile(filePath);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processing loop");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Processes a stable file using a scoped FileIngestor service.
    /// </summary>
    private async Task ProcessStableFileAsync(string filePath, CancellationToken stoppingToken)
    {
        try
        {
            // Create a scope to get scoped services (IDocumentSession, etc.)
            using var scope = _serviceProvider.CreateScope();
            var fileIngestor = scope.ServiceProvider.GetRequiredService<FileIngestor>();

            var success = await fileIngestor.ProcessFileAsync(filePath, stoppingToken);

            if (success)
            {
                _logger.LogInformation("Successfully processed: {FilePath}", filePath);
            }
            else
            {
                _logger.LogWarning("Failed to process: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stable file: {FilePath}", filePath);
        }
    }

    public override void Dispose()
    {
        _fileSystemWatcher?.Dispose();
        base.Dispose();
    }
}

