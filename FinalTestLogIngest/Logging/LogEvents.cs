using Microsoft.Extensions.Logging;

namespace FinalTestLogIngest.Logging;

/// <summary>
/// Centralized log event ID definitions for structured logging throughout the application.
/// Event IDs are grouped by functional area for easier categorization and filtering.
/// </summary>
public static class LogEvents
{
    // ========================================================================================
    // File Watching and Queue Operations (1000-1099)
    // ========================================================================================

    /// <summary>FileWatcherService has started.</summary>
    public static readonly EventId ServiceStarted = new(1000, nameof(ServiceStarted));

    /// <summary>FileWatcherService is stopping due to cancellation.</summary>
    public static readonly EventId ServiceStopping = new(1001, nameof(ServiceStopping));

    /// <summary>FileWatcherService has stopped.</summary>
    public static readonly EventId ServiceStopped = new(1002, nameof(ServiceStopped));

    /// <summary>A file has been added to the processing queue.</summary>
    public static readonly EventId FileQueued = new(1010, nameof(FileQueued));

    /// <summary>A file has stabilized and is ready for processing.</summary>
    public static readonly EventId FileStable = new(1011, nameof(FileStable));

    /// <summary>A queued file no longer exists and will be skipped.</summary>
    public static readonly EventId FileNoLongerExists = new(1012, nameof(FileNoLongerExists));

    /// <summary>Initial directory scan has started.</summary>
    public static readonly EventId InitialScanStarted = new(1020, nameof(InitialScanStarted));

    /// <summary>Initial directory scan has completed.</summary>
    public static readonly EventId InitialScanCompleted = new(1021, nameof(InitialScanCompleted));

    /// <summary>FileSystemWatcher has been configured and started.</summary>
    public static readonly EventId FileSystemWatcherStarted = new(1030, nameof(FileSystemWatcherStarted));

    /// <summary>Error occurred during initial directory scan.</summary>
    public static readonly EventId InitialScanError = new(1040, nameof(InitialScanError));

    /// <summary>Fatal error in FileWatcherService.</summary>
    public static readonly EventId ServiceFatalError = new(1041, nameof(ServiceFatalError));

    // ========================================================================================
    // File Ingestion Operations (1100-1199)
    // ========================================================================================

    /// <summary>File ingestion processing has started for a file.</summary>
    public static readonly EventId FileProcessingStarted = new(1100, nameof(FileProcessingStarted));

    /// <summary>File content has been successfully read.</summary>
    public static readonly EventId FileReadCompleted = new(1101, nameof(FileReadCompleted));

    /// <summary>File has been successfully parsed into a FinalTestLog document.</summary>
    public static readonly EventId FileParsed = new(1110, nameof(FileParsed));

    /// <summary>A new document has been inserted into the database.</summary>
    public static readonly EventId FileInserted = new(1120, nameof(FileInserted));

    /// <summary>An existing document has been updated with a new version.</summary>
    public static readonly EventId FileUpdated = new(1121, nameof(FileUpdated));

    /// <summary>A duplicate document was detected and skipped.</summary>
    public static readonly EventId FileDuplicate = new(1122, nameof(FileDuplicate));

    /// <summary>File has been successfully archived after processing.</summary>
    public static readonly EventId FileArchived = new(1130, nameof(FileArchived));

    /// <summary>Error occurred while reading a file.</summary>
    public static readonly EventId FileReadError = new(1140, nameof(FileReadError));

    /// <summary>Error occurred while parsing a file.</summary>
    public static readonly EventId ParseError = new(1141, nameof(ParseError));

    /// <summary>Error occurred during database persistence.</summary>
    public static readonly EventId PersistError = new(1142, nameof(PersistError));

    /// <summary>Error occurred while archiving a file.</summary>
    public static readonly EventId ArchiveError = new(1143, nameof(ArchiveError));

    /// <summary>File does not exist when attempting to process.</summary>
    public static readonly EventId FileNotFound = new(1144, nameof(FileNotFound));

    /// <summary>File is empty and cannot be processed.</summary>
    public static readonly EventId FileEmpty = new(1145, nameof(FileEmpty));

    // ========================================================================================
    // Application Lifecycle (1200-1299)
    // ========================================================================================

    /// <summary>Application is starting up.</summary>
    public static readonly EventId ApplicationStarting = new(1200, nameof(ApplicationStarting));

    /// <summary>Application has started successfully.</summary>
    public static readonly EventId ApplicationStarted = new(1201, nameof(ApplicationStarted));

    /// <summary>Application is shutting down.</summary>
    public static readonly EventId ApplicationStopping = new(1202, nameof(ApplicationStopping));

    /// <summary>Application has stopped.</summary>
    public static readonly EventId ApplicationStopped = new(1203, nameof(ApplicationStopped));

    /// <summary>Application startup summary with configuration details.</summary>
    public static readonly EventId StartupSummary = new(1204, nameof(StartupSummary));

    /// <summary>Application shutdown summary with processing statistics.</summary>
    public static readonly EventId ShutdownSummary = new(1205, nameof(ShutdownSummary));
}

