using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinalTestLogIngest.Options;
using FinalTestLogIngest.Persistence;
using FinalTestLogIngest.Persistence.Repositories;
using FinalTestLogIngest.Ingestion;
using FinalTestLogIngest.Logging;

namespace FinalTestLogIngest;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Bind and validate configuration options
                services.AddOptions<WatcherOptions>()
                    .Bind(context.Configuration.GetSection("Watcher"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddOptions<ProcessingOptions>()
                    .Bind(context.Configuration.GetSection("Processing"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddOptions<ArchiveOptions>()
                    .Bind(context.Configuration.GetSection("Archive"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddOptions<DatabaseOptions>()
                    .Bind(context.Configuration.GetSection("Database"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddOptions<ParsingOptions>()
                    .Bind(context.Configuration.GetSection("Parsing"))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                // Configure Marten document store
                var databaseOptions = context.Configuration.GetSection("Database").Get<DatabaseOptions>()
                    ?? throw new InvalidOperationException("Database configuration is required");
                services.AddMartenStore(databaseOptions);

                // Register repositories
                services.AddScoped<FinalTestLogRepository>();

                // Register ingestion pipeline components
                services.AddSingleton<FileQueue>();
                services.AddSingleton<DebounceTracker>();
                services.AddSingleton<IngestionMetrics>();
                services.AddScoped<FileIngestor>();

                // Register background services
                services.AddHostedService<FileWatcherService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var config = host.Services.GetRequiredService<IConfiguration>();
        var metrics = host.Services.GetRequiredService<IngestionMetrics>();
        var environment = host.Services.GetRequiredService<IHostEnvironment>();

        // Log application startup with configuration summary
        logger.LogInformation(LogEvents.ApplicationStarting, 
            "FinalTest Log Ingest service starting...");
        
        logger.LogInformation("Environment: {EnvironmentName}", environment.EnvironmentName);

        var watcherSection = config.GetSection("Watcher");
        var databaseSection = config.GetSection("Database");

        // Extract database name from connection string
        var connectionString = databaseSection["ConnectionString"] ?? "";
        var databaseName = ExtractDatabaseName(connectionString);

        logger.LogInformation(LogEvents.StartupSummary,
            "Startup Configuration - Watch Path: {WatchPath}, Filter: {Filter}, " +
            "Database: {DatabaseName}, Schema: {SchemaName}, Auto Create: {AutoCreate}",
            watcherSection["Path"],
            watcherSection["Filter"],
            databaseName,
            databaseSection["Schema"],
            databaseSection["AutoCreate"]);

        logger.LogInformation(LogEvents.ApplicationStarted, 
            "FinalTest Log Ingest service started successfully");

        // Run the application
        await host.RunAsync();

        // Log application shutdown with metrics summary
        logger.LogInformation(LogEvents.ApplicationStopping, 
            "FinalTest Log Ingest service stopping...");

        logger.LogInformation(LogEvents.ShutdownSummary,
            "Shutdown Statistics - {Metrics}",
            metrics.GetSummary());

        logger.LogInformation(LogEvents.ApplicationStopped, 
            "FinalTest Log Ingest service stopped");
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        // Simple parser to extract Database value from connection string
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2 && 
                keyValue[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                return keyValue[1].Trim();
            }
        }
        return "unknown";
    }
}
