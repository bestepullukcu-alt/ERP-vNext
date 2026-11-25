using Diten.BuildingBlocks.CentralAudit;
using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Suppliers;

/// <summary>
/// MOD-0140 DL-3 — <c>Suppliers:Model = TenantShared</c> (K-3, §8): one <c>supplier_masters</c> record per supplier in the
/// tenant (common fields, K-1) and one <c>supplier_company_relationships</c> record per company (status, onboarding, approval,
/// payment terms, purchasing, external mapping, legacy codes). Every read goes THROUGH the caller company's non-deleted
/// relationship (tenant + company server-resolved, §8): a supplier the company is not related to is not found, and another
/// company's relationship data never leaves this class. The only tenant-wide reads are the tax-number duplicate check (K-2 /
/// K-6, returns a code) and the "link to this company" lookup (returns id + code).
///
/// <para>Every write runs in ONE Mongo transaction (<see cref="InTransactionAsync{T}"/>) that enlists the running marked
/// command's central-audit event (BL-501, <c>RequireEnlistment</c>, §7, §21) — the record commits with the data or not at all.
/// Each record is written under its own version (optimistic concurrency); a unique-index refusal becomes a typed
/// exception (409, never 500, §12).</para>
/// </summary>
public sealed class TenantSharedSupplierStore : ISupplierStore
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<SupplierMaster> _masters;
    private readonly IMongoCollection<SupplierCompanyRelationship> _relationships;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public TenantSharedSupplierStore(IMongoDatabase database, ITenantContext tenant)
    {
        _client = database.Client;
        _masters = database.GetCollection<SupplierMaster>(SupplierTenantModelIndexConfiguration.MasterCollectionName);
        _relationships = database.GetCollection<SupplierCompanyRelationship>(SupplierTenantModelIndexConfiguration.RelationshipCollectionName);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    public bool IsTenantShared => true;

    // ── reads (through the caller company's relationship) ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SupplierSnapshot>> ListAsync(SupplierStatus? status, CancellationToken ct)
    {
        var filter = RelationshipFilter();
        if (status.HasValue)
        {
            filter &= Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.Status, status.Value);
        }

        return await JoinAsync(await _relationships.Find(filter).ToListAsync(ct), ct);
    }

    public async Task<SupplierSnapshot?> FindAsync(string code, CancellationToken ct)
    {
        // §4 code resolution: the canonical code first, then the caller company's legacy code (K-5).
        var master = await _masters.Find(MasterFilter() & Builders<SupplierMaster>.Filter.Eq(x => x.SupplierId, code)).FirstOrDefaultAsync(ct);
        if (master is not null)
        {
            var relationship = await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.SupplierRef, master.Id))
                .FirstOrDefaultAsync(ct);
            if (relationship is not null)
            {
                return Snapshot(master, relationship);
            }
        }

        var aliased = await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.AnyEq(x => x.LegacySupplierCodes, code))
            .FirstOrDefaultAsync(ct);
        return aliased is null ? null : (await JoinAsync([aliased], ct)).FirstOrDefault();
    }

    public async Task<SupplierSnapshot?> FindByMasterIdAsync(Guid masterId, CancellationToken ct)
    {
        var relationship = await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.SupplierRef, masterId))
            .FirstOrDefaultAsync(ct);
        return relationship is null ? null : (await JoinAsync([relationship], ct)).FirstOrDefault();
    }

    public async Task<IReadOnlyDictionary<string, SupplierSnapshot>> FindManyAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        var result = new Dictionary<string, SupplierSnapshot>(StringComparer.Ordinal);
        if (codes.Count == 0)
        {
            return result;
        }

        var masters = await _masters.Find(MasterFilter() & Builders<SupplierMaster>.Filter.In(x => x.SupplierId, codes)).ToListAsync(ct);
        var byMaster = masters.ToDictionary(m => m.Id);
        var relationships = masters.Count == 0
            ? []
            : await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.In(x => x.SupplierRef, byMaster.Keys)).ToListAsync(ct);
        foreach (var r in relationships)
        {
            var m = byMaster[r.SupplierRef];
            result[m.SupplierId] = Snapshot(m, r);
        }

        var unresolved = codes.Where(c => !result.ContainsKey(c)).ToList();
        if (unresolved.Count > 0)
        {
            var aliased = await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.AnyIn(x => x.LegacySupplierCodes, unresolved))
                .ToListAsync(ct);
            foreach (var snapshot in await JoinAsync(aliased, ct))
            {
                foreach (var legacy in snapshot.LegacySupplierCodes.Where(unresolved.Contains))
                {
                    result[legacy] = snapshot;
                }
            }
        }

        return result;
    }

    public async Task<SupplierSnapshot?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        var relationship = await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(ct);
        return relationship is null ? null : (await JoinAsync([relationship], ct)).FirstOrDefault();
    }

    /// <summary>K-2 / K-6: tenant-wide, (country, normalized tax number), in ANY company. Returns only the code.</summary>
    public async Task<string?> FindDuplicateTaxIdAsync(string? country, string taxId, Guid? excludeMasterId, CancellationToken ct)
    {
        var normalized = SupplierTaxId.Normalize(taxId);
        if (normalized is null)
        {
            return null;
        }

        var filter = MasterFilter()
                     & Builders<SupplierMaster>.Filter.Eq(x => x.Country, country)
                     & Builders<SupplierMaster>.Filter.Eq(x => x.TaxIdNormalized, normalized);
        if (excludeMasterId.HasValue)
        {
            filter &= Builders<SupplierMaster>.Filter.Ne(x => x.Id, excludeMasterId.Value);
        }

        return (await _masters.Find(filter).FirstOrDefaultAsync(ct))?.SupplierId;
    }

    /// <summary>§3.1 / F-2: exactly the eligible relationships (non-deleted, Active — S-1), ordered by canonical code only (ordinal).</summary>
    public async Task<IReadOnlyList<SupplierSnapshot>> ListEligibleAsync(string? afterCode, int take, CancellationToken ct)
    {
        var active = await _relationships.Find(RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.Status, SupplierStatus.Active))
            .ToListAsync(ct);
        return (await JoinAsync(active, ct))
            .Where(SupplierEligibilityRule.IsEligible)
            .Where(s => afterCode is null || string.CompareOrdinal(s.SupplierId, afterCode) > 0)
            .OrderBy(s => s.SupplierId, StringComparer.Ordinal)
            .Take(take)
            .ToList();
    }

    public async Task<(Guid MasterId, string SupplierId)?> FindTenantSupplierAsync(string code, CancellationToken ct)
    {
        var master = await _masters.Find(MasterFilter() & Builders<SupplierMaster>.Filter.Eq(x => x.SupplierId, code)).FirstOrDefaultAsync(ct);
        return master is null ? null : (master.Id, master.SupplierId);
    }

    // ── writes (one transaction each, the central-audit event enlisted in it) ───────────────────────────────────────────

    public async Task<SupplierSnapshot> CreateAsync(SupplierSnapshot draft, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var master = new SupplierMaster
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            LegalEntityId = null,
            SupplierId = draft.SupplierId,
            Name = draft.Name,
            Country = draft.Country,
            TaxId = draft.TaxId,
            TaxIdNormalized = SupplierTaxId.Normalize(draft.TaxId),
            Contacts = draft.Contacts,
            Documents = draft.Documents,
            Screening = new SupplierScreening { KycOutcome = draft.KycOutcome, SanctionsOutcome = draft.SanctionsOutcome },
            CreatedAt = now,
            UpdatedAt = now
        };
        var relationship = NewRelationship(master.Id, draft.IdempotencyKey, now);
        relationship.Status = draft.Status;
        relationship.OnboardingStatus = draft.OnboardingStatus;
        relationship.PurchasingEnabled = draft.PurchasingEnabled;
        relationship.PurchasingSince = draft.PurchasingSince ?? now;
        relationship.SourceSystem = draft.SourceSystem;
        relationship.ExternalRef = draft.ExternalRef;

        try
        {
            await InTransactionAsync(master.Id, async (s, c) =>
            {
                await _masters.InsertOneAsync(s, master, cancellationToken: c);
                await _relationships.InsertOneAsync(s, relationship, cancellationToken: c);
                return true;
            }, ct);
        }
        catch (Exception ex) when (SupplierWriteErrors.IsDuplicateKey(ex, out var message))
        {
            throw await DuplicateAsync(message, draft.Country, draft.TaxId, null, ex, ct);
        }

        return Snapshot(master, relationship);
    }

    public async Task<SupplierSnapshot?> SaveRelationshipAsync(SupplierSnapshot snapshot, CancellationToken ct)
    {
        var saved = await InTransactionAsync(snapshot.RelationshipId, async (s, c) =>
            await UpdateRelationshipAsync(s, snapshot, c) is { } r ? r : null, ct);
        return saved is null ? null : await FindByMasterIdAsync(snapshot.MasterId, ct);
    }

    public async Task<SupplierSnapshot?> SaveMasterAsync(SupplierSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var saved = await InTransactionAsync(snapshot.MasterId, async (s, c) =>
            {
                var master = Builders<SupplierMaster>.Update
                    .Set(x => x.Name, snapshot.Name)
                    .Set(x => x.Country, snapshot.Country)
                    .Set(x => x.TaxId, snapshot.TaxId)
                    .Set(x => x.TaxIdNormalized, SupplierTaxId.Normalize(snapshot.TaxId))
                    .Set(x => x.Contacts, snapshot.Contacts)
                    .Set(x => x.Documents, snapshot.Documents)
                    .Set(x => x.Screening.KycOutcome, snapshot.KycOutcome)
                    .Set(x => x.Screening.SanctionsOutcome, snapshot.SanctionsOutcome)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Inc(x => x.Version, 1);
                var masterFilter = MasterFilter()
                                   & Builders<SupplierMaster>.Filter.Eq(x => x.Id, snapshot.MasterId)
                                   & Builders<SupplierMaster>.Filter.Eq(x => x.Version, snapshot.MasterVersion);
                if ((await _masters.UpdateOneAsync(s, masterFilter, master, cancellationToken: c)).ModifiedCount == 0)
                {
                    return null;
                }

                // Both records or neither: a stale relationship aborts the master change with it.
                return await UpdateRelationshipAsync(s, snapshot, c) ?? throw new StaleRelationshipException();
            }, ct);
            return saved is null ? null : await FindByMasterIdAsync(snapshot.MasterId, ct);
        }
        catch (StaleRelationshipException)
        {
            return null;
        }
        catch (Exception ex) when (SupplierWriteErrors.IsDuplicateKey(ex, out var message))
        {
            throw await DuplicateAsync(message, snapshot.Country, snapshot.TaxId, snapshot.MasterId, ex, ct);
        }
    }

    public async Task<bool> DeleteAsync(SupplierSnapshot snapshot, CancellationToken ct)
        => await DeleteManyAsync([snapshot], snapshot.RelationshipId, ct) > 0;

    public Task<int> DeleteManyAsync(IReadOnlyList<SupplierSnapshot> snapshots, CancellationToken ct)
        => DeleteManyAsync(snapshots, null, ct);

    public async Task<SupplierSnapshot> LinkAsync(Guid masterId, string? idempotencyKey, CancellationToken ct)
    {
        var relationship = NewRelationship(masterId, idempotencyKey, DateTimeOffset.UtcNow);
        try
        {
            await InTransactionAsync(relationship.Id, async (s, c) =>
            {
                await _relationships.InsertOneAsync(s, relationship, cancellationToken: c);
                return true;
            }, ct);
        }
        catch (Exception ex) when (SupplierWriteErrors.IsDuplicateKey(ex, out var message))
        {
            if (message.Contains("ux_tenant_le_idempotencykey", StringComparison.Ordinal) && idempotencyKey is not null
                && await FindByIdempotencyKeyAsync(idempotencyKey, ct) is { } winner)
            {
                return winner;
            }

            throw new SupplierRelationshipExistsException();
        }

        return await FindByMasterIdAsync(masterId, ct) ?? throw new InvalidOperationException("The new relationship was not found.");
    }

    // ── internals ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The one transaction of this store: the write, then — only when it wrote — the running marked command's central-audit
    /// event in the SAME transaction (BL-501; no marked command → no-op, e.g. the unmarked onboarding submit until DL-4).
    /// </summary>
    private async Task<T?> InTransactionAsync<T>(Guid? entityId, Func<IClientSessionHandle, CancellationToken, Task<T?>> write, CancellationToken ct)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(async (s, c) =>
        {
            var result = await write(s, c);
            if (result is not null && result is not (false or 0))
            {
                await CentralAuditTransaction.EnlistAsync(s, entityId, c);
            }

            return result;
        }, cancellationToken: ct);
    }

    private async Task<SupplierCompanyRelationship?> UpdateRelationshipAsync(IClientSessionHandle session, SupplierSnapshot snapshot, CancellationToken ct)
    {
        var update = Builders<SupplierCompanyRelationship>.Update
            .Set(x => x.Status, snapshot.Status)
            .Set(x => x.OnboardingStatus, snapshot.OnboardingStatus)
            .Set(x => x.OnboardingCase, snapshot.OnboardingCase)
            .Set(x => x.Approval, snapshot.Approval)
            .Set(x => x.PaymentTermsCode, snapshot.PaymentTermsCode)
            .Set(x => x.PurchasingEnabled, snapshot.PurchasingEnabled)
            .Set(x => x.PurchasingSince, snapshot.PurchasingSince)
            .Set(x => x.SourceSystem, snapshot.SourceSystem)
            .Set(x => x.ExternalRef, snapshot.ExternalRef)
            .Set(x => x.OnboardingIdempotencyKey, snapshot.OnboardingIdempotencyKey)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1);
        var filter = RelationshipFilter()
                     & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.Id, snapshot.RelationshipId)
                     & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.Version, snapshot.RelationshipVersion);
        return await _relationships.FindOneAndUpdateAsync(session, filter, update,
            new FindOneAndUpdateOptions<SupplierCompanyRelationship> { ReturnDocument = ReturnDocument.After }, ct);
    }

    private async Task<int> DeleteManyAsync(IReadOnlyList<SupplierSnapshot> snapshots, Guid? entityId, CancellationToken ct)
    {
        if (snapshots.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        var filter = RelationshipFilter() & Builders<SupplierCompanyRelationship>.Filter.In(x => x.Id, snapshots.Select(s => s.RelationshipId));
        var update = Builders<SupplierCompanyRelationship>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now)
            .Inc(x => x.Version, 1);
        return await InTransactionAsync(entityId, async (s, c) =>
            (int)(await _relationships.UpdateManyAsync(s, filter, update, cancellationToken: c)).ModifiedCount, ct);
    }

    private SupplierCompanyRelationship NewRelationship(Guid masterId, string? idempotencyKey, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        LegalEntityId = _legalEntityId,
        SupplierRef = masterId,
        Status = SupplierStatus.Active,
        OnboardingStatus = OnboardingStatus.Draft,
        PurchasingEnabled = true,           // K-4 (Revizyon 9): default true, since = the link / create moment
        PurchasingSince = now,
        IdempotencyKey = idempotencyKey,
        CreatedAt = now,
        UpdatedAt = now
    };

    private async Task<Exception> DuplicateAsync(string message, string? country, string? taxId, Guid? excludeMasterId, Exception inner, CancellationToken ct)
    {
        if (message.Contains("ux_tenant_le_idempotencykey", StringComparison.Ordinal))
        {
            return new SupplierDuplicateException(null, idempotencyRace: true, inner);
        }

        var existing = taxId is null ? null : await FindDuplicateTaxIdAsync(country, taxId, excludeMasterId, ct);
        return new SupplierDuplicateException(existing, idempotencyRace: false, inner);
    }

    private async Task<IReadOnlyList<SupplierSnapshot>> JoinAsync(IReadOnlyList<SupplierCompanyRelationship> relationships, CancellationToken ct)
    {
        if (relationships.Count == 0)
        {
            return [];
        }

        var masters = (await _masters.Find(MasterFilter() & Builders<SupplierMaster>.Filter.In(x => x.Id, relationships.Select(r => r.SupplierRef).Distinct()))
                .ToListAsync(ct))
            .ToDictionary(m => m.Id);
        return relationships.Where(r => masters.ContainsKey(r.SupplierRef)).Select(r => Snapshot(masters[r.SupplierRef], r)).ToList();
    }

    private static SupplierSnapshot Snapshot(SupplierMaster m, SupplierCompanyRelationship r) => new()
    {
        MasterId = m.Id,
        RelationshipId = r.Id,
        SupplierId = m.SupplierId,
        Name = m.Name,
        Country = m.Country,
        TaxId = m.TaxId,
        Contacts = m.Contacts,
        Documents = m.Documents,
        KycOutcome = m.Screening.KycOutcome,
        SanctionsOutcome = m.Screening.SanctionsOutcome,
        Status = r.Status,
        OnboardingStatus = r.OnboardingStatus,
        OnboardingCase = r.OnboardingCase,
        Approval = r.Approval,
        PaymentTermsCode = r.PaymentTermsCode,
        PurchasingEnabled = r.PurchasingEnabled,
        PurchasingSince = r.PurchasingSince,
        SourceSystem = r.SourceSystem,
        ExternalRef = r.ExternalRef,
        LegacySupplierCodes = r.LegacySupplierCodes,
        IdempotencyKey = r.IdempotencyKey,
        OnboardingIdempotencyKey = r.OnboardingIdempotencyKey,
        MasterVersion = m.Version,
        RelationshipVersion = r.Version
    };

    /// <summary>The supplier itself: tenant + not deleted (no company — the record belongs to the tenant).</summary>
    private FilterDefinition<SupplierMaster> MasterFilter() =>
        Builders<SupplierMaster>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<SupplierMaster>.Filter.Eq(x => x.IsDeleted, false);

    /// <summary>The caller company's relationships: tenant + company + not deleted — every read goes through this.</summary>
    private FilterDefinition<SupplierCompanyRelationship> RelationshipFilter() =>
        Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
        & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
        & Builders<SupplierCompanyRelationship>.Filter.Eq(x => x.IsDeleted, false);

    private sealed class StaleRelationshipException() : Exception("The relationship changed since it was read.");
}
