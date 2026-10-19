using Diten.Platform.Common.Persistence;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// BL-533 — the ONE whole-document write the meeting and timesheet repositories share. A replace sends the WHOLE
/// in-memory document, so the version in the filter must be the version that document was READ at — not just the
/// version the client sent. Otherwise a handler that read at N while its client sent N+1 overwrites the N+1 written
/// meanwhile with its stale N copy (a published minutes row put back to Draft, say), and the filter happily matches.
/// <para>
/// Two conditions, both required: the document in hand is the one expected (<see cref="IsTheDocumentRead{T}"/>),
/// and the stored document is still at that version — inside the repository's own scope (tenant, not deleted). A
/// refusal leaves the entity exactly as it was handed in, so a caller that goes on using it never carries a version
/// nobody stored.
/// </para>
/// <para>
/// The session overloads write inside the caller's Platform transaction. A write that succeeds there moves the entity
/// to the next version before anything is committed; when the transaction is aborted and run again, the caller first
/// puts back the versions it read (<c>ReadVersionSnapshot</c>).
/// </para>
/// </summary>
internal static class ReadVersionWrites
{
    /// <summary>True only when the document in hand was read at <paramref name="expectedVersion"/>.</summary>
    public static bool IsTheDocumentRead<T>(T entity, int expectedVersion) where T : BaseEntity
        => entity.Version == expectedVersion;

    public static Task<bool> ReplaceAsync<T>(
        IMongoCollection<T> collection,
        FilterDefinition<T> scope,
        T entity,
        int expectedVersion,
        CancellationToken ct) where T : BaseEntity
        => ReplaceCoreAsync(collection, null, scope, entity, expectedVersion, ct);

    public static Task<bool> ReplaceAsync<T>(
        IMongoCollection<T> collection,
        IClientSessionHandle session,
        FilterDefinition<T> scope,
        T entity,
        int expectedVersion,
        CancellationToken ct) where T : BaseEntity
        => ReplaceCoreAsync(collection, session, scope, entity, expectedVersion, ct);

    /// <summary>An insert inside the caller's transaction, under the base repository's own rule for every insert: the
    /// tenant comes from the server context, never from the entity handed in.</summary>
    public static async Task<T> InsertAsync<T>(
        IMongoCollection<T> collection,
        IClientSessionHandle session,
        T entity,
        Guid tenantId,
        CancellationToken ct) where T : TenantScopedEntity
    {
        typeof(T).GetProperty(nameof(TenantScopedEntity.TenantId))!.SetValue(entity, tenantId);
        await collection.InsertOneAsync(session, entity, cancellationToken: ct);
        return entity;
    }

    private static async Task<bool> ReplaceCoreAsync<T>(
        IMongoCollection<T> collection,
        IClientSessionHandle? session,
        FilterDefinition<T> scope,
        T entity,
        int expectedVersion,
        CancellationToken ct) where T : BaseEntity
    {
        if (!IsTheDocumentRead(entity, expectedVersion))
        {
            return false;
        }

        var readUpdatedAt = entity.UpdatedAt;
        entity.Version = expectedVersion + 1;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<T>.Filter.And(
            scope,
            Builders<T>.Filter.Eq(x => x.Id, entity.Id),
            Builders<T>.Filter.Eq(x => x.Version, expectedVersion));
        var options = new FindOneAndReplaceOptions<T> { ReturnDocument = ReturnDocument.Before };

        T? previous;
        try
        {
            previous = session is null
                ? await collection.FindOneAndReplaceAsync(filter, entity, options, ct)
                : await collection.FindOneAndReplaceAsync(session, filter, entity, options, ct);
        }
        catch
        {
            Restore(entity, expectedVersion, readUpdatedAt);
            throw;
        }

        if (previous is null)
        {
            Restore(entity, expectedVersion, readUpdatedAt);
            return false;
        }

        return true;
    }

    private static void Restore<T>(T entity, int readVersion, DateTimeOffset? readUpdatedAt) where T : BaseEntity
    {
        entity.Version = readVersion;
        entity.UpdatedAt = readUpdatedAt;
    }
}
