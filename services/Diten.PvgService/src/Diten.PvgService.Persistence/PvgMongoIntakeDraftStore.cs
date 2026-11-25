using System.Globalization;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PvgService.Persistence;

// MOD-0230 DL-10: the build/test Mongo implementation of IPvgIntakeDraftStore (pack "Build/Test Persistence (Mongo
// Store)"). Same versioning contract as the in-memory store. Every query, count and write carries the server-resolved
// TenantId and IsDeleted = false; a draft of another tenant is never read, counted or replaced. AddAsync and
// ReplaceAsync each run in one Mongo transaction on a client session.
public sealed class PvgMongoIntakeDraftStore : IPvgIntakeDraftStore
{
    private const int WriteConflictCode = 112;

    private readonly PvgMongoContext _context;
    private readonly TimeProvider _timeProvider;

    internal PvgMongoIntakeDraftStore(PvgMongoContext context, TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async ValueTask<string> AddAsync(
        PvgPersistenceTenantScope tenantScope,
        string actorId,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope, intake);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var draftGuid = Guid.NewGuid();
        var intakeDraftId = $"pvg-draft-{draftGuid:N}";
        var document = PvgIntakeDraftDocumentMapper.ToNewDocument(
            draftGuid,
            intakeDraftId,
            1,
            actorId,
            _timeProvider.GetUtcNow().UtcDateTime,
            intake);

        await RunInTransactionAsync(
            async session =>
            {
                await _context.Drafts.InsertOneAsync(session, document, cancellationToken: cancellationToken);
                return (true, true);
            },
            cancellationToken);

        return intakeDraftId;
    }

    public async ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        long? expectedVersion,
        string actorId,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope, intake);

        if (string.IsNullOrWhiteSpace(intakeDraftId))
        {
            return PvgIntakeDraftReplaceResult.NotFound();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var set = PvgIntakeDraftDocumentMapper.ToDomainFieldSet(intake);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return await RunInTransactionAsync(
            async session =>
            {
                if (expectedVersion is null)
                {
                    // No version to match: nothing is written, the draft is only classified.
                    return (await ClassifyAsync(session, tenantScope.TenantId, intakeDraftId, cancellationToken), false);
                }

                set["Version"] = expectedVersion.Value + 1;
                set["UpdatedAt"] = new BsonDateTime(now);
                set["UpdatedBy"] = actorId;

                var filter = Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.TenantId, tenantScope.TenantId)
                             & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IntakeDraftId, intakeDraftId)
                             & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IsDeleted, false)
                             & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.Version, expectedVersion.Value);
                var update = new BsonDocumentUpdateDefinition<PvgIntakeDraftDocument>(new BsonDocument("$set", set));

                UpdateResult result;
                try
                {
                    result = await _context.Drafts.UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
                }
                catch (Exception exception) when (IsWriteConflict(exception))
                {
                    // A concurrent replace of the same draft won; this one lost. The aborted transaction is not committed.
                    return (PvgIntakeDraftReplaceResult.VersionConflict(), false);
                }

                if (result.MatchedCount == 1)
                {
                    return (PvgIntakeDraftReplaceResult.Replaced(expectedVersion.Value + 1), true);
                }

                // Nothing matched: separate not found from a version conflict with a read, never a second write.
                return (await ClassifyAsync(session, tenantScope.TenantId, intakeDraftId, cancellationToken), false);
            },
            cancellationToken);
    }

    public async ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope);

        if (string.IsNullOrWhiteSpace(intakeDraftId))
        {
            return null;
        }

        var filter = Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.TenantId, tenantScope.TenantId)
                     & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IntakeDraftId, intakeDraftId)
                     & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IsDeleted, false);
        var document = await _context.Drafts.Find(filter).FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ToPersisted(document);
    }

    public async ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
        PvgPersistenceListScope listScope,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(listScope.TenantScope);

        var pageNumber = Math.Max(1, listScope.PageNumber);
        var pageSize = Math.Max(1, listScope.PageSize);

        // A page beyond any representable offset is an empty page, not an overflowed (negative) offset.
        var skip = (long)(pageNumber - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            return [];
        }

        var filter = Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.TenantId, listScope.TenantScope.TenantId)
                     & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IsDeleted, false);
        if (listScope.Status is { } status)
        {
            filter &= Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.Status, status.ToString());
        }

        var documents = await _context.Drafts
            .Find(filter)
            .Sort(Builders<PvgIntakeDraftDocument>.Sort
                .Descending(draft => draft.ReceivedAtUtc)
                .Ascending(draft => draft.IntakeDraftId))
            .Skip((int)skip)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return documents.Select(ToPersisted).ToArray();
    }

    public async ValueTask<int> CountAsync(
        PvgPersistenceTenantScope tenantScope,
        PvgIntakeStatus? status,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(tenantScope);

        var filter = Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.TenantId, tenantScope.TenantId)
                     & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IsDeleted, false);
        if (status is { } value)
        {
            filter &= Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.Status, value.ToString());
        }

        return (int)await _context.Drafts.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    private async Task<PvgIntakeDraftReplaceResult> ClassifyAsync(
        IClientSessionHandle session,
        string tenantId,
        string intakeDraftId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.TenantId, tenantId)
                     & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IntakeDraftId, intakeDraftId)
                     & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IsDeleted, false);
        var exists = await _context.Drafts.Find(session, filter).Limit(1).AnyAsync(cancellationToken);

        return exists ? PvgIntakeDraftReplaceResult.VersionConflict() : PvgIntakeDraftReplaceResult.NotFound();
    }

    // One Mongo transaction (read concern snapshot, write concern majority). The work returns its result and whether to
    // commit; a transaction that wrote nothing, or that already lost a write conflict, is aborted instead.
    private async Task<T> RunInTransactionAsync<T>(
        Func<IClientSessionHandle, Task<(T Result, bool Commit)>> work,
        CancellationToken cancellationToken)
    {
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(PvgMongoContext.WriteTransactionOptions);

        try
        {
            var (result, commit) = await work(session);
            if (commit)
            {
                await session.CommitTransactionAsync(cancellationToken);
            }
            else
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            return result;
        }
        catch
        {
            await TryAbortAsync(session);
            throw;
        }
    }

    private static async Task TryAbortAsync(IClientSessionHandle session)
    {
        try
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // The original failure is the one that matters; the session is disposed right after.
        }
    }

    private static bool IsWriteConflict(Exception exception) =>
        exception switch
        {
            MongoCommandException command => command.Code == WriteConflictCode,
            MongoWriteException write => write.WriteError?.Code == WriteConflictCode ||
                                         write.InnerException is MongoCommandException { Code: WriteConflictCode },
            MongoBulkWriteException bulk => bulk.WriteErrors.Any(error => error.Code == WriteConflictCode),
            _ => false
        };

    private static PvgPersistedIntakeDraft ToPersisted(PvgIntakeDraftDocument document) =>
        new(document.IntakeDraftId, PvgIntakeDraftDocumentMapper.ToIntake(document), document.Version);

    private static void EnsureTenantScope(PvgPersistenceTenantScope tenantScope, SafetyCaseIntake? intake = null)
    {
        if (string.IsNullOrWhiteSpace(tenantScope.TenantId))
        {
            throw new InvalidOperationException("PVG_PERSISTENCE_TENANT_SCOPE_REQUIRED");
        }

        if (intake is not null && !string.Equals(intake.TenantId, tenantScope.TenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PVG_PERSISTENCE_TENANT_SCOPE_MISMATCH");
        }
    }
}
