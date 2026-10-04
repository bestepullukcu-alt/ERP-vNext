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
/// and the stored document is still at that version (the filter). A refusal leaves the entity exactly as it was
/// handed in, so a caller that goes on using it never carries a version nobody stored.
/// </para>
/// </summary>
internal static class ReadVersionWrites
{
    /// <summary>True only when the document in hand was read at <paramref name="expectedVersion"/>.</summary>
    public static bool IsTheDocumentRead<T>(T entity, int expectedVersion) where T : BaseEntity
        => entity.Version == expectedVersion;

    public static async Task<bool> ReplaceAsync<T>(
        IMongoCollection<T> collection,
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

        T? previous;
        try
        {
            previous = await collection.FindOneAndReplaceAsync(
                filter,
                entity,
                new FindOneAndReplaceOptions<T> { ReturnDocument = ReturnDocument.Before },
                ct);
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
