using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0022 e-Signature Service (E2) — Mongo repositories.
//
// ⚠ The four insert-only stores below deliberately do NOT derive from TenantRepository<T>: that base class exposes a public
// soft DeleteAsync, which would make "insert-only" a convention again (FU23's measured gap, pack §4). They hold their
// collection privately and expose exactly the interface: InsertOneAsync and reads. The architecture test reflects over
// these classes as well as their interfaces.

internal static class ESignatureMongo
{
    public static bool IsDuplicateKey(MongoWriteException ex) => ex.WriteError?.Category == ServerErrorCategory.DuplicateKey;

    public static bool IsDuplicateKeyOn(MongoWriteException ex, string indexName) =>
        IsDuplicateKey(ex) && (ex.WriteError?.Message?.Contains(indexName, StringComparison.Ordinal) ?? false);

    public static IClientSessionHandle Handle(IPlatformTransactionSession session, IPlatformDbContext db) =>
        PlatformMongoTransactionSession.Require(session, db);

    /// <summary>Insert inside a transaction; a unique-index hit aborts the transaction, so it surfaces as a domain exception.</summary>
    public static async Task InsertOrThrowDuplicateAsync<T>(IMongoCollection<T> collection, IClientSessionHandle handle, T document, CancellationToken ct)
    {
        try
        {
            await collection.InsertOneAsync(handle, document, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            throw new ESignatureDuplicateException(ex);
        }
    }
}

public sealed class ESignatureRecordRepository : IESignatureRecordRepository
{
    private const string WorkflowTaskIndex = "ux_esign_records_tenant_workflow_task";

    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureRecord> _collection;

    public ESignatureRecordRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureRecord>(PlatformCollections.ESignatureRecords);
    }

    private static FilterDefinitionBuilder<ESignatureRecord> F => Builders<ESignatureRecord>.Filter;

    public async Task InsertAsync(IPlatformTransactionSession session, ESignatureRecord record, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(ESignatureMongo.Handle(session, _db), record, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ESignatureMongo.IsDuplicateKeyOn(ex, WorkflowTaskIndex))
        {
            throw new ESignatureTaskBindingException(ex);
        }
        catch (MongoWriteException ex) when (ESignatureMongo.IsDuplicateKey(ex))
        {
            throw new ESignatureDuplicateException(ex);
        }
    }

    public async Task<ESignatureRecord?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public async Task<ESignatureRecord?> GetByIdempotencyKeyAsync(Guid tenantId, string signerUserId, string idempotencyKey, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.SignerUserId, signerUserId) & F.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ESignatureRecord>> ListBySubjectAsync(Guid tenantId, string module, string subjectType, string subjectId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Module, module) & F.Eq(x => x.SubjectType, subjectType) & F.Eq(x => x.SubjectId, subjectId))
            .SortBy(x => x.SignedAtUtc).ThenBy(x => x.SignatureNumber)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, long>> GetSampleUsageAsync(Guid tenantId, IReadOnlyCollection<Guid> sampleIds, CancellationToken ct = default)
    {
        if (sampleIds.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var used = await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.In(x => x.AppearanceSampleId, sampleIds.Select(id => (Guid?)id)))
            .Project(x => x.AppearanceSampleId)
            .ToListAsync(ct);
        return used.Where(id => id is not null).GroupBy(id => id!.Value).ToDictionary(g => g.Key, g => (long)g.Count());
    }

    public async Task<(IReadOnlyList<ESignatureRecord> Items, long Total)> SearchAsync(
        Guid tenantId, ESignatureSearch search, int skip, int take, CancellationToken ct = default)
    {
        var conditions = new List<FilterDefinition<ESignatureRecord>> { F.Eq(x => x.TenantId, tenantId) };
        if (search.FromUtc is { } from) conditions.Add(F.Gte(x => x.SignedAtUtc, from));
        if (search.ToUtc is { } to) conditions.Add(F.Lte(x => x.SignedAtUtc, to));
        if (!string.IsNullOrWhiteSpace(search.Module)) conditions.Add(F.Eq(x => x.Module, search.Module));
        if (!string.IsNullOrWhiteSpace(search.SignerUserId)) conditions.Add(F.Eq(x => x.SignerUserId, search.SignerUserId));
        if (!string.IsNullOrWhiteSpace(search.MeaningCode)) conditions.Add(F.Eq(x => x.MeaningCode, search.MeaningCode));
        if (search.LegalEntityId is { } legalEntity) conditions.Add(F.Eq(x => x.LegalEntityId, legalEntity));

        // Visibility (pack §8 KS-2, fail closed): the caller's own signatures, plus records of the legal entities in the
        // caller's data scope. No scope ⇒ own signatures only.
        var visibility = F.Eq(x => x.SignerUserId, search.ViewerUserId);
        if (search.VisibleLegalEntityIds.Count > 0)
        {
            visibility |= F.In(x => x.LegalEntityId, search.VisibleLegalEntityIds.Select(id => (Guid?)id));
        }

        conditions.Add(visibility);
        var filter = F.And(conditions);
        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _collection.Find(filter)
            .SortByDescending(x => x.SignedAtUtc).ThenByDescending(x => x.SignatureNumber)
            .Skip(skip).Limit(take).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class ESignatureInvalidationRepository : IESignatureInvalidationRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureInvalidation> _collection;

    public ESignatureInvalidationRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureInvalidation>(PlatformCollections.ESignatureInvalidations);
    }

    private static FilterDefinitionBuilder<ESignatureInvalidation> F => Builders<ESignatureInvalidation>.Filter;

    public Task InsertAsync(IPlatformTransactionSession session, ESignatureInvalidation invalidation, CancellationToken ct = default) =>
        ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), invalidation, ct);

    public async Task<ESignatureInvalidation?> GetBySignatureAsync(Guid tenantId, Guid signatureId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.SignatureId, signatureId)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, ESignatureInvalidation>> GetBySignaturesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> signatureIds, CancellationToken ct = default)
    {
        if (signatureIds.Count == 0)
        {
            return new Dictionary<Guid, ESignatureInvalidation>();
        }

        var rows = await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.In(x => x.SignatureId, signatureIds)).ToListAsync(ct);
        return rows.ToDictionary(x => x.SignatureId);
    }
}

public sealed class ESignatureExportRepository : IESignatureExportRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureExport> _collection;

    public ESignatureExportRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureExport>(PlatformCollections.ESignatureExports);
    }

    public Task InsertAsync(IPlatformTransactionSession session, ESignatureExport export, CancellationToken ct = default) =>
        ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), export, ct);

    public async Task<ESignatureExport?> GetByIdempotencyKeyAsync(Guid tenantId, string byUserId, string idempotencyKey, CancellationToken ct = default)
    {
        var f = Builders<ESignatureExport>.Filter;
        return await _collection.Find(f.Eq(x => x.TenantId, tenantId) & f.Eq(x => x.ByUserId, byUserId) & f.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<ESignatureExport> Items, long Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct = default)
    {
        var filter = Builders<ESignatureExport>.Filter.Eq(x => x.TenantId, tenantId);
        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _collection.Find(filter).SortByDescending(x => x.AtUtc).Skip(skip).Limit(take).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class ESignaturePolicyChangeRepository : IESignaturePolicyChangeRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignaturePolicyChange> _collection;

    public ESignaturePolicyChangeRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignaturePolicyChange>(PlatformCollections.ESignaturePolicyChanges);
    }

    public Task InsertAsync(IPlatformTransactionSession session, ESignaturePolicyChange change, CancellationToken ct = default) =>
        _collection.InsertOneAsync(ESignatureMongo.Handle(session, _db), change, cancellationToken: ct);

    public async Task<IReadOnlyList<ESignaturePolicyChange>> ListAsync(Guid tenantId, Guid? legalEntityId, CancellationToken ct = default)
    {
        var f = Builders<ESignaturePolicyChange>.Filter;
        var filter = f.Eq(x => x.TenantId, tenantId);
        if (legalEntityId is { } id)
        {
            filter &= f.Eq(x => x.LegalEntityId, id);
        }

        return await _collection.Find(filter).SortByDescending(x => x.ChangedAtUtc).Limit(500).ToListAsync(ct);
    }
}

// ── mutable (guarded) ────────────────────────────────────────────────────────────────────────────────────────────

public sealed class ESignRequestRepository : IESignRequestRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureRequest> _collection;

    public ESignRequestRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureRequest>(PlatformCollections.ESignatureRequests);
    }

    private static FilterDefinitionBuilder<ESignatureRequest> F => Builders<ESignatureRequest>.Filter;

    public Task InsertAsync(IPlatformTransactionSession session, ESignatureRequest request, CancellationToken ct = default) =>
        ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), request, ct);

    public async Task<ESignatureRequest?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public async Task<ESignatureRequest?> GetByIdempotencyKeyAsync(Guid tenantId, string requestedBy, string idempotencyKey, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.RequestedBy, requestedBy) & F.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<ESignatureRequest> Items, long Total, long WaitingCount)> ListForSignerAsync(
        Guid tenantId, string signerUserId, ESignatureRequestQuery query, int skip, int take, CancellationToken ct = default)
    {
        var mine = F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.SignerUserId, signerUserId);
        var filter = mine;
        if (!string.IsNullOrWhiteSpace(query.Module)) filter &= F.Eq(x => x.Module, query.Module);
        if (query.Status is { } status) filter &= F.Eq(x => x.Status, status);
        if (query.FromUtc is { } from) filter &= F.Gte(x => x.RequestedAtUtc, from);
        if (query.ToUtc is { } to) filter &= F.Lte(x => x.RequestedAtUtc, to);

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var waiting = await _collection.CountDocumentsAsync(mine & F.Eq(x => x.Status, ESignatureRequestStatus.Waiting), cancellationToken: ct);
        var items = await _collection.Find(filter).SortBy(x => x.DueAtUtc).ThenByDescending(x => x.RequestedAtUtc)
            .Skip(skip).Limit(take).ToListAsync(ct);
        return (items, total, waiting);
    }

    public async Task<bool> SaveDecisionAsync(IPlatformTransactionSession session, ESignatureRequest request, int expectedVersion, CancellationToken ct = default)
    {
        var update = Builders<ESignatureRequest>.Update
            .Set(x => x.Status, request.Status)
            .Set(x => x.SignatureId, request.SignatureId)
            .Set(x => x.RejectReason, request.RejectReason)
            .Set(x => x.CancelReason, request.CancelReason)
            .Set(x => x.DecidedAtUtc, request.DecidedAtUtc)
            .Set(x => x.UpdatedAt, request.UpdatedAt)
            .Set(x => x.UpdatedBy, request.UpdatedBy)
            .Set(x => x.Version, expectedVersion + 1);
        var result = await _collection.UpdateOneAsync(ESignatureMongo.Handle(session, _db),
            F.Eq(x => x.TenantId, request.TenantId) & F.Eq(x => x.Id, request.Id)
            & F.Eq(x => x.Status, ESignatureRequestStatus.Waiting) & F.Eq(x => x.Version, expectedVersion),
            update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<ESignatureRequest>> ListOverdueAsync(Guid tenantId, DateTime nowUtc, int take, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Status, ESignatureRequestStatus.Waiting) & F.Lt(x => x.DueAtUtc, nowUtc))
            .SortBy(x => x.DueAtUtc).Limit(take).ToListAsync(ct);
}

public sealed class ESignPolicyRepository : IESignPolicyRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignaturePolicy> _collection;

    public ESignPolicyRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignaturePolicy>(PlatformCollections.ESignaturePolicies);
    }

    private static FilterDefinitionBuilder<ESignaturePolicy> F => Builders<ESignaturePolicy>.Filter;

    public async Task<ESignaturePolicy?> GetTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Scope, ESignaturePolicyScope.Tenant)).FirstOrDefaultAsync(ct);

    public async Task<ESignaturePolicy?> GetLegalEntityAsync(Guid tenantId, Guid legalEntityId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Scope, ESignaturePolicyScope.LegalEntity)
                               & F.Eq(x => x.LegalEntityId, legalEntityId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ESignaturePolicy>> ListOverridesAsync(Guid tenantId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Scope, ESignaturePolicyScope.LegalEntity)).ToListAsync(ct);

    public async Task<bool> InsertAsync(IPlatformTransactionSession? session, ESignaturePolicy policy, CancellationToken ct = default)
    {
        try
        {
            if (session is null)
            {
                await _collection.InsertOneAsync(policy, cancellationToken: ct);
            }
            else
            {
                await ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), policy, ct);
            }

            return true;
        }
        catch (MongoWriteException ex) when (ESignatureMongo.IsDuplicateKey(ex))
        {
            return false; // only reachable without a session (seed): the session path throws above
        }
    }

    public async Task<bool> ReplaceAsync(IPlatformTransactionSession session, ESignaturePolicy policy, int expectedVersion, CancellationToken ct = default)
    {
        var result = await _collection.ReplaceOneAsync(ESignatureMongo.Handle(session, _db),
            F.Eq(x => x.TenantId, policy.TenantId) & F.Eq(x => x.Id, policy.Id) & F.Eq(x => x.Version, expectedVersion),
            policy, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> DeleteOverrideAsync(IPlatformTransactionSession session, Guid tenantId, Guid legalEntityId, int expectedVersion, CancellationToken ct = default)
    {
        // The override is configuration, not evidence: "revert to tenant default" removes it (pack §8 policy). Its whole
        // content survives in the ESignaturePolicyChange row written in the same transaction (BeforeJson).
        var result = await _collection.DeleteOneAsync(ESignatureMongo.Handle(session, _db),
            F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Scope, ESignaturePolicyScope.LegalEntity)
            & F.Eq(x => x.LegalEntityId, legalEntityId) & F.Eq(x => x.Version, expectedVersion),
            cancellationToken: ct);
        return result.DeletedCount == 1;
    }

    public async Task<bool> AnyRowUsesMeaningAsync(Guid tenantId, string meaningCode, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId)
                               & F.ElemMatch(x => x.Rows, Builders<ESignaturePolicyRow>.Filter.Eq(r => r.MeaningCode, meaningCode)))
            .AnyAsync(ct);
}

public sealed class ESignMeaningRepository : IESignMeaningRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureMeaning> _collection;

    public ESignMeaningRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureMeaning>(PlatformCollections.ESignatureMeanings);
    }

    private static FilterDefinitionBuilder<ESignatureMeaning> F => Builders<ESignatureMeaning>.Filter;

    public async Task<IReadOnlyList<ESignatureMeaning>> ListAsync(Guid tenantId, bool activeOnly, CancellationToken ct = default)
    {
        var filter = F.Eq(x => x.TenantId, tenantId);
        if (activeOnly)
        {
            filter &= F.Eq(x => x.IsActive, true);
        }

        return await _collection.Find(filter).SortBy(x => x.Code).ToListAsync(ct);
    }

    public async Task<ESignatureMeaning?> GetAsync(Guid tenantId, string code, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Code, code)).FirstOrDefaultAsync(ct);

    public async Task<bool> InsertAsync(IPlatformTransactionSession? session, ESignatureMeaning meaning, CancellationToken ct = default)
    {
        try
        {
            if (session is null)
            {
                await _collection.InsertOneAsync(meaning, cancellationToken: ct);
            }
            else
            {
                await ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), meaning, ct);
            }

            return true;
        }
        catch (MongoWriteException ex) when (ESignatureMongo.IsDuplicateKey(ex))
        {
            return false; // only reachable without a session (seed): the session path throws above
        }
    }

    public async Task<bool> ReplaceAsync(IPlatformTransactionSession session, ESignatureMeaning meaning, int expectedVersion, CancellationToken ct = default)
    {
        var result = await _collection.ReplaceOneAsync(ESignatureMongo.Handle(session, _db),
            F.Eq(x => x.TenantId, meaning.TenantId) & F.Eq(x => x.Code, meaning.Code) & F.Eq(x => x.Version, expectedVersion),
            meaning, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task MarkUsedAsync(IPlatformTransactionSession session, Guid tenantId, string code, DateTime atUtc, CancellationToken ct = default)
    {
        var handle = ESignatureMongo.Handle(session, _db);
        var filter = F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Code, code);
        await _collection.UpdateOneAsync(handle, filter, Builders<ESignatureMeaning>.Update.Inc(x => x.UsageCount, 1), cancellationToken: ct);
        // First use locks the code. Not $min: BSON orders null BELOW every date, so $min would keep the null forever.
        await _collection.UpdateOneAsync(handle, filter & F.Eq(x => x.FirstUsedAtUtc, null),
            Builders<ESignatureMeaning>.Update.Set(x => x.FirstUsedAtUtc, atUtc), cancellationToken: ct);
    }
}

public sealed class ESignActionRepository : IESignActionRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignableAction> _collection;

    public ESignActionRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignableAction>(PlatformCollections.ESignatureSignableActions);
    }

    private static FilterDefinitionBuilder<ESignableAction> F => Builders<ESignableAction>.Filter;

    public async Task<IReadOnlyList<ESignableAction>> ListAsync(CancellationToken ct = default) =>
        await _collection.Find(F.Empty).SortBy(x => x.Module).ThenBy(x => x.ActionCode).ToListAsync(ct);

    public async Task<ESignableAction?> GetAsync(string actionCode, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.ActionCode, actionCode)).FirstOrDefaultAsync(ct);

    public async Task<bool> UpsertAsync(IPlatformTransactionSession session, ESignableAction action, CancellationToken ct = default)
    {
        var handle = ESignatureMongo.Handle(session, _db);
        var filter = F.Eq(x => x.ActionCode, action.ActionCode);
        var existing = await _collection.Find(handle, filter).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            await ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, handle, action, ct);
            return true;
        }

        if (existing.Module == action.Module
            && existing.DefaultRequired == action.DefaultRequired
            && existing.DefaultMeaningCode == action.DefaultMeaningCode
            && existing.DefaultVisualMode == action.DefaultVisualMode
            && existing.Names.Count == action.Names.Count
            && existing.Names.All(n => action.Names.TryGetValue(n.Key, out var v) && v == n.Value))
        {
            return false;
        }

        var update = Builders<ESignableAction>.Update
            .Set(x => x.Module, action.Module)
            .Set(x => x.Names, action.Names)
            .Set(x => x.DefaultRequired, action.DefaultRequired)
            .Set(x => x.DefaultMeaningCode, action.DefaultMeaningCode)
            .Set(x => x.DefaultVisualMode, action.DefaultVisualMode)
            .Set(x => x.RegisteredAtUtc, action.RegisteredAtUtc)
            .Inc(x => x.Version, 1);
        await _collection.UpdateOneAsync(handle, filter, update, cancellationToken: ct);
        return true;
    }
}

public sealed class ESignNumberGenerator : IESignNumberGenerator
{
    private readonly IMongoCollection<ESignatureCounter> _collection;

    public ESignNumberGenerator(IPlatformDbContext db) =>
        _collection = db.GetCollection<ESignatureCounter>(PlatformCollections.ESignatureCounters);

    public async Task<long> NextAsync(Guid tenantId, string series, int year, CancellationToken ct = default)
    {
        var key = ESignatureCounter.KeyFor(tenantId, series, year);
        var update = Builders<ESignatureCounter>.Update
            .SetOnInsert(x => x.TenantId, tenantId)
            .SetOnInsert(x => x.Series, series)
            .SetOnInsert(x => x.Year, year)
            .Inc(x => x.Current, 1L);
        var options = new FindOneAndUpdateOptions<ESignatureCounter> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var counter = await _collection.FindOneAndUpdateAsync(Builders<ESignatureCounter>.Filter.Eq(x => x.Id, key), update, options, ct);
                return counter.Current;
            }
            catch (MongoCommandException ex) when (ex.Code == 11000 && attempt < 2)
            {
                // Two first-ever upserts raced on the same _id; the loser retries and increments the winner's row.
            }
        }
    }
}

// ── E3 — signature samples. The sample and event stores are insert-only (no TenantRepository<T> base); the owner pointer
// is a compare-and-set document. ──────────────────────────────────────────────────────────────────────────────────────

public sealed class ESignatureSampleRepository : IESignatureSampleRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureSample> _collection;

    public ESignatureSampleRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureSample>(PlatformCollections.ESignatureSamples);
    }

    private static FilterDefinitionBuilder<ESignatureSample> F => Builders<ESignatureSample>.Filter;

    public Task InsertAsync(IPlatformTransactionSession session, ESignatureSample sample, CancellationToken ct = default) =>
        ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), sample, ct);

    public async Task<ESignatureSample?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Id, id)).FirstOrDefaultAsync(ct);

    public async Task<ESignatureSample?> GetByIdempotencyKeyAsync(Guid tenantId, string ownerUserId, string idempotencyKey, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.OwnerUserId, ownerUserId) & F.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ESignatureSample>> ListByOwnerAsync(Guid tenantId, string ownerUserId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.OwnerUserId, ownerUserId))
            .SortByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.SampleNumber).Limit(200).ToListAsync(ct);

    public async Task<IReadOnlyList<ESignatureSample>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        ids.Count == 0
            ? []
            : await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.In(x => x.Id, ids)).ToListAsync(ct);
}

public sealed class ESignatureSampleEventRepository : IESignatureSampleEventRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureSampleEvent> _collection;

    public ESignatureSampleEventRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureSampleEvent>(PlatformCollections.ESignatureSampleEvents);
    }

    private static FilterDefinitionBuilder<ESignatureSampleEvent> F => Builders<ESignatureSampleEvent>.Filter;

    public Task InsertAsync(IPlatformTransactionSession session, ESignatureSampleEvent sampleEvent, CancellationToken ct = default) =>
        ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, ESignatureMongo.Handle(session, _db), sampleEvent, ct);

    public async Task<IReadOnlyList<ESignatureSampleEvent>> ListBySampleAsync(Guid tenantId, Guid sampleId, IPlatformTransactionSession? session = null,
        CancellationToken ct = default)
    {
        var filter = F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.SampleId, sampleId);
        var find = session is null ? _collection.Find(filter) : _collection.Find(ESignatureMongo.Handle(session, _db), filter);
        return await find.SortBy(x => x.Sequence).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ESignatureSampleEvent>> ListBySamplesAsync(Guid tenantId, IReadOnlyCollection<Guid> sampleIds, CancellationToken ct = default) =>
        sampleIds.Count == 0
            ? []
            : await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.In(x => x.SampleId, sampleIds)).SortBy(x => x.Sequence).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListSubmittedSampleIdsAsync(Guid tenantId, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.Event, ESignatureSampleEventKind.Submitted))
            .SortByDescending(x => x.AtUtc).Limit(1000).Project(x => x.SampleId).ToListAsync(ct);

    public async Task<IReadOnlyList<ESignatureSampleEvent>> ListDecisionsAsync(Guid tenantId, int take, CancellationToken ct = default) =>
        await _collection.Find(F.Eq(x => x.TenantId, tenantId)
                               & F.In(x => x.Event, new[] { ESignatureSampleEventKind.Approved, ESignatureSampleEventKind.Rejected }))
            .SortByDescending(x => x.AtUtc).Limit(take).ToListAsync(ct);
}

public sealed class ESignSampleOwnerStateRepository : IESignSampleOwnerStateRepository
{
    private readonly IPlatformDbContext _db;
    private readonly IMongoCollection<ESignatureSampleOwnerState> _collection;

    public ESignSampleOwnerStateRepository(IPlatformDbContext db)
    {
        _db = db;
        _collection = db.GetCollection<ESignatureSampleOwnerState>(PlatformCollections.ESignatureSampleOwnerStates);
    }

    private static FilterDefinitionBuilder<ESignatureSampleOwnerState> F => Builders<ESignatureSampleOwnerState>.Filter;

    public async Task<ESignatureSampleOwnerState?> GetAsync(IPlatformTransactionSession? session, Guid tenantId, string ownerUserId, CancellationToken ct = default)
    {
        var filter = F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.OwnerUserId, ownerUserId);
        var find = session is null ? _collection.Find(filter) : _collection.Find(ESignatureMongo.Handle(session, _db), filter);
        return await find.FirstOrDefaultAsync(ct);
    }

    public async Task<bool> MoveActiveAsync(IPlatformTransactionSession session, Guid tenantId, string ownerUserId, Guid? expectedActiveSampleId,
        Guid? newActiveSampleId, CancellationToken ct = default)
    {
        var handle = ESignatureMongo.Handle(session, _db);
        var owner = F.Eq(x => x.TenantId, tenantId) & F.Eq(x => x.OwnerUserId, ownerUserId);
        var existing = await _collection.Find(handle, owner).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            if (expectedActiveSampleId is not null)
            {
                return false;
            }

            await ESignatureMongo.InsertOrThrowDuplicateAsync(_collection, handle, new ESignatureSampleOwnerState
            {
                TenantId = tenantId,
                OwnerUserId = ownerUserId,
                ActiveSampleId = newActiveSampleId,
                CreatedBy = ownerUserId
            }, ct);
            return true;
        }

        // The write itself is the guard: two transactions moving one owner's pointer conflict on this document, and the
        // filter on the expected value refuses a move that started from a stale pointer.
        var result = await _collection.UpdateOneAsync(handle, owner & F.Eq(x => x.ActiveSampleId, expectedActiveSampleId),
            Builders<ESignatureSampleOwnerState>.Update
                .Set(x => x.ActiveSampleId, newActiveSampleId)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                .Inc(x => x.Version, 1),
            cancellationToken: ct);
        return result.ModifiedCount == 1;
    }
}
