using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using FinalTestLogIngest.Parsing.Models;

namespace FinalTestLogIngest.Persistence.Repositories;

/// <summary>
/// Result of a persistence operation with versioning and deduplication
/// </summary>
public enum PersistenceResult
{
    /// <summary>
    /// Document was inserted as a new record
    /// </summary>
    Inserted,

    /// <summary>
    /// Document was updated (new version of existing document with different content)
    /// </summary>
    Updated,

    /// <summary>
    /// Document was skipped because it's a duplicate (same Id and same ContentSha256)
    /// </summary>
    Duplicate
}

/// <summary>
/// Repository for FinalTestLog documents in Marten
/// </summary>
public class FinalTestLogRepository
{
    private readonly IDocumentStore _documentStore;

    public FinalTestLogRepository(IDocumentStore documentStore)
    {
        _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
    }

    /// <summary>
    /// Find a FinalTestLog document by its Id
    /// </summary>
    /// <param name="id">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The document if found, null otherwise</returns>
    public async Task<FinalTestLog?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id cannot be null or whitespace", nameof(id));
        }

        await using var session = _documentStore.LightweightSession();
        return await session.LoadAsync<FinalTestLog>(id, cancellationToken);
    }

    /// <summary>
    /// Check if a document with the specified Id exists
    /// </summary>
    /// <param name="id">Document identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the document exists, false otherwise</returns>
    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Id cannot be null or whitespace", nameof(id));
        }

        var document = await FindByIdAsync(id, cancellationToken);
        return document != null;
    }

    /// <summary>
    /// Insert a new FinalTestLog document
    /// </summary>
    /// <param name="log">The document to insert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task InsertAsync(FinalTestLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (string.IsNullOrWhiteSpace(log.Id))
        {
            throw new ArgumentException("Document Id cannot be null or whitespace", nameof(log));
        }

        await using var session = _documentStore.LightweightSession();
        session.Insert(log);
        await session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Update an existing FinalTestLog document
    /// </summary>
    /// <param name="log">The document to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task UpdateAsync(FinalTestLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (string.IsNullOrWhiteSpace(log.Id))
        {
            throw new ArgumentException("Document Id cannot be null or whitespace", nameof(log));
        }

        await using var session = _documentStore.LightweightSession();
        session.Update(log);
        await session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Insert or update a FinalTestLog document (upsert operation)
    /// </summary>
    /// <param name="log">The document to insert or update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task UpsertAsync(FinalTestLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (string.IsNullOrWhiteSpace(log.Id))
        {
            throw new ArgumentException("Document Id cannot be null or whitespace", nameof(log));
        }

        await using var session = _documentStore.LightweightSession();
        session.Store(log);
        await session.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Insert or update a FinalTestLog document with deduplication and versioning logic.
    /// - If the document doesn't exist: insert as new (Version = 1)
    /// - If the document exists with the same ContentSha256: skip as duplicate
    /// - If the document exists with different ContentSha256: create new version, preserve history
    /// </summary>
    /// <param name="log">The document to insert or update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the persistence operation (Inserted, Updated, or Duplicate)</returns>
    public async Task<PersistenceResult> UpsertWithVersioningAsync(FinalTestLog log, CancellationToken cancellationToken = default)
    {
        if (log == null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        if (string.IsNullOrWhiteSpace(log.Id))
        {
            throw new ArgumentException("Document Id cannot be null or whitespace", nameof(log));
        }

        if (string.IsNullOrWhiteSpace(log.ContentSha256))
        {
            throw new ArgumentException("ContentSha256 must be computed before persistence", nameof(log));
        }

        // Use a single session for atomicity
        await using var session = _documentStore.LightweightSession();

        // Check if document exists
        var existingDoc = await session.LoadAsync<FinalTestLog>(log.Id, cancellationToken);

        if (existingDoc == null)
        {
            // New document - insert with Version = 1
            log.Version = 1;
            log.ReplacedHistory = new List<ReplacedVersion>();
            session.Insert(log);
            await session.SaveChangesAsync(cancellationToken);
            return PersistenceResult.Inserted;
        }

        // Document exists - check if content is the same
        if (existingDoc.ContentSha256 == log.ContentSha256)
        {
            // Duplicate - same Id and same content hash
            return PersistenceResult.Duplicate;
        }

        // Document exists with different content - create new version
        // 1. Create a ReplacedVersion entry for the existing document
        var replacedVersion = new ReplacedVersion
        {
            Version = existingDoc.Version,
            ContentSha256 = existingDoc.ContentSha256,
            ReplacedAtUtc = DateTime.UtcNow
        };

        // 2. Preserve the existing ReplacedHistory and add the new entry
        log.ReplacedHistory = existingDoc.ReplacedHistory ?? new List<ReplacedVersion>();
        log.ReplacedHistory.Add(replacedVersion);

        // 3. Increment version
        log.Version = existingDoc.Version + 1;

        // 4. Update the document
        session.Update(log);
        await session.SaveChangesAsync(cancellationToken);
        
        return PersistenceResult.Updated;
    }
}

