using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using FinalTestLogIngest.Parsing.Models;

namespace FinalTestLogIngest.Persistence.Repositories;

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
}

