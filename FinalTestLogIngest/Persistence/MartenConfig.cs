using Marten;
using Marten.Schema;
using Marten.Services;
using FinalTestLogIngest.Options;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinalTestLogIngest.Persistence;

public static class MartenConfig
{
    public static void AddMartenStore(this IServiceCollection services, DatabaseOptions options)
    {
        services.AddMarten(martenOptions =>
        {
            // Set connection string
            martenOptions.Connection(options.ConnectionString);

            // Set schema name
            martenOptions.DatabaseSchemaName = options.Schema;

            // Configure auto-create behavior based on environment
            // Note: The AutoCreateSchemaObjects property will be configured when document models are added in Phase 3
            // For now, keeping default behavior

            // TODO: Add document mappings and indexes when models are created in Phase 3
            // This configuration will be extended to include:
            // - FinalTestLog document mapping
            // - Index on DeviceSerial
            // - Index on TimestampLocal
            
            // Configure for development - detailed logging
            if (options.AutoCreate == "All")
            {
                martenOptions.Logger(new ConsoleMartenLogger());
            }
        });
    }
}

/// <summary>
/// Simple console logger for Marten operations during development
/// </summary>
public class ConsoleMartenLogger : IMartenLogger
{
    public IMartenSessionLogger StartSession(IQuerySession session)
    {
        return new ConsoleMartenSessionLogger();
    }

    public void SchemaChange(string sql)
    {
        Console.WriteLine($"[Marten Schema] {sql}");
    }
}

/// <summary>
/// Session-level logger for Marten operations
/// </summary>
public class ConsoleMartenSessionLogger : IMartenSessionLogger
{
    public void LogSuccess(NpgsqlCommand command)
    {
        // Optionally log successful commands during development
    }

    public void LogSuccess(NpgsqlBatch batch)
    {
        // Optionally log successful batch commands during development
    }

    public void LogFailure(NpgsqlCommand command, Exception ex)
    {
        Console.WriteLine($"[Marten Error] Command failed: {command.CommandText}");
        Console.WriteLine($"[Marten Error] Exception: {ex.Message}");
    }

    public void LogFailure(NpgsqlBatch batch, Exception ex)
    {
        Console.WriteLine($"[Marten Error] Batch command failed");
        Console.WriteLine($"[Marten Error] Exception: {ex.Message}");
    }

    public void LogFailure(Exception ex, string message)
    {
        Console.WriteLine($"[Marten Error] {message}");
        Console.WriteLine($"[Marten Error] Exception: {ex.Message}");
    }

    public void RecordSavedChanges(IDocumentSession session, IChangeSet commit)
    {
        var inserts = commit.Inserted.Count();
        var updates = commit.Updated.Count();
        var deletes = commit.Deleted.Count();
        
        if (inserts > 0 || updates > 0 || deletes > 0)
        {
            Console.WriteLine($"[Marten Session] Saved changes - Inserts: {inserts}, Updates: {updates}, Deletes: {deletes}");
        }
    }

    public void OnBeforeExecute(NpgsqlCommand command)
    {
        // Optionally log commands before execution
    }

    public void OnBeforeExecute(NpgsqlBatch batch)
    {
        // Optionally log batch commands before execution
    }
}

