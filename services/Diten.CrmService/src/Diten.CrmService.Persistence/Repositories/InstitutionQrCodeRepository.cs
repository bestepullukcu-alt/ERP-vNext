using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.2) — <c>crm_institution_qr_codes</c>. ONE active row per institution is the database's rule:
/// the partial unique index <c>(TenantId, AccountId)</c> where <c>Status = "active"</c> (an equality filter — a
/// <c>$ne</c> partial filter crash-loops the startup), so a concurrent second insert answers false. DateTimeOffset members
/// (IssuedAt, LastScannedAt …) are BSON arrays: never sort / range keys on the server — ordering is in memory (an
/// institution has a handful of rows).
/// </summary>
public sealed class InstitutionQrCodeRepository : IInstitutionQrCodeRepository
{
    public const string CollectionName = "crm_institution_qr_codes";

    private readonly IMongoCollection<InstitutionQrCode> _collection;
    private readonly IMongoCollection<VisitExecution> _executions;

    public InstitutionQrCodeRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<InstitutionQrCode>(CollectionName);
        _executions = database.GetCollection<VisitExecution>(VisitExecutionRepository.CollectionName);
    }

    private static FilterDefinition<InstitutionQrCode> Tenant(Guid tenantId)
        => Builders<InstitutionQrCode>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    private static FilterDefinition<InstitutionQrCode> Active
        => Builders<InstitutionQrCode>.Filter.Eq(x => x.Status, InstitutionQrCodeStatuses.Active);

    public async Task<InstitutionQrCode?> GetActiveAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Active & Builders<InstitutionQrCode>.Filter.Eq(x => x.AccountId, accountId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<InstitutionQrCode>> ListActiveByAccountsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
        => accountIds.Count == 0
            ? Array.Empty<InstitutionQrCode>()
            : await _collection.Find(Tenant(tenantId) & Active & Builders<InstitutionQrCode>.Filter.In(x => x.AccountId, accountIds))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InstitutionQrCode>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => (await _collection.Find(Tenant(tenantId) & Builders<InstitutionQrCode>.Filter.Eq(x => x.AccountId, accountId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.CodeVersion).ToList();

    public async Task<IReadOnlyList<Guid>> ListAccountIdsByStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken)
        => (await _collection.Find(Tenant(tenantId) & Builders<InstitutionQrCode>.Filter.Eq(x => x.Status, status))
                .Project(x => x.AccountId).ToListAsync(cancellationToken))
            .Distinct().ToList();

    public async Task<IReadOnlyList<InstitutionQrCode>> ListLatestByAccountsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return Array.Empty<InstitutionQrCode>();
        }

        var rows = await _collection.Find(Tenant(tenantId) & Builders<InstitutionQrCode>.Filter.In(x => x.AccountId, accountIds))
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.AccountId)
            .Select(g => g.FirstOrDefault(x => x.IsActive()) ?? g.OrderByDescending(x => x.CodeVersion).First())
            .ToList();
    }

    public async Task<bool> InsertAsync(InstitutionQrCode code, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(code, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false; // the institution already has an active code (the partial unique index won the race)
        }
    }

    public async Task<bool> RevokeAsync(
        Guid tenantId, Guid id, int expectedCodeVersion, string? by, DateTimeOffset at, string reason, CancellationToken cancellationToken)
    {
        var result = await _collection.UpdateOneAsync(
            Tenant(tenantId) & Active
            & Builders<InstitutionQrCode>.Filter.Eq(x => x.Id, id)
            & Builders<InstitutionQrCode>.Filter.Eq(x => x.CodeVersion, expectedCodeVersion),
            Builders<InstitutionQrCode>.Update
                .Set(x => x.Status, InstitutionQrCodeStatuses.Revoked)
                .Set(x => x.RevokedAt, at)
                .Set(x => x.RevokedBy, by)
                .Set(x => x.RevokeReason, reason)
                .Set(x => x.UpdatedAt, at)
                .Inc(x => x.Version, 1),
            cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task RecordScanAsync(Guid tenantId, Guid id, DateTimeOffset at, CancellationToken cancellationToken)
        => _collection.UpdateOneAsync(
            Tenant(tenantId) & Builders<InstitutionQrCode>.Filter.Eq(x => x.Id, id),
            Builders<InstitutionQrCode>.Update.Inc(x => x.ScanCount, 1).Set(x => x.LastScannedAt, at),
            cancellationToken: cancellationToken);

    /// <summary>The visit starts whose QR matched this institution (Qr.AccountId + Qr.Status "ok", scalar keys), counted
    /// in memory by the SERVER StartedAt (a DateTimeOffset array — never a server-side range key).</summary>
    public async Task<int> CountScansSinceAsync(Guid tenantId, Guid accountId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var filter = Builders<VisitExecution>.Filter.Where(e => e.TenantId == tenantId && !e.IsDeleted)
                     & Builders<VisitExecution>.Filter.Eq("Qr.AccountId", accountId.ToString("D"))
                     & Builders<VisitExecution>.Filter.Eq("Qr.Status", VisitQrScanStatuses.Ok);
        var started = await _executions.Find(filter).Project(e => e.StartedAt).ToListAsync(cancellationToken);
        return started.Count(s => s >= since);
    }
}

/// <summary>WP-VW-W3E (§2.2) — <c>crm_institution_qr_events</c>: append-only; ordered newest first in memory (At is a
/// DateTimeOffset BSON array).</summary>
public sealed class InstitutionQrEventRepository : IInstitutionQrEventRepository
{
    public const string CollectionName = "crm_institution_qr_events";

    private readonly IMongoCollection<InstitutionQrEvent> _collection;

    public InstitutionQrEventRepository(IMongoDatabase database)
        => _collection = database.GetCollection<InstitutionQrEvent>(CollectionName);

    public Task InsertManyAsync(IReadOnlyCollection<InstitutionQrEvent> events, CancellationToken cancellationToken)
        => events.Count == 0 ? Task.CompletedTask : _collection.InsertManyAsync(events, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<InstitutionQrEvent>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => (await _collection.Find(Builders<InstitutionQrEvent>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.AccountId == accountId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.At).ThenByDescending(x => x.CreatedAt).ToList();
}
