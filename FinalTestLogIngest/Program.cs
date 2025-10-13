using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinalTestLogIngest.Options;
using FinalTestLogIngest.Persistence;
using FinalTestLogIngest.Persistence.Repositories;
using FinalTestLogIngest.Ingestion;

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
        logger.LogInformation("FinalTest Log Ingest service starting...");

        await host.RunAsync();

        logger.LogInformation("FinalTest Log Ingest service stopped.");
    }
}
