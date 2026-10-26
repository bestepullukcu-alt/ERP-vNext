using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>A hook case to open: what it is about and who caused it (CT-F10).</summary>
public sealed record HookCase(
    ExpiryExceptionType Type,
    string DedupKey,
    Lot Lot,
    string? WarehouseId,
    decimal? Quantity,
    string? BaseUomId,
    FefoSourceMovement? SourceMovement,
    Dictionary<string, string> Facts,
    Guid? OriginUserId,
    string? Description);

/// <summary>
/// MOD-0176 F2 — the writes the hooks make INSIDE a foreign transaction (the ledger posting, the lot transaction): an exception case
/// opened by the system with its <c>Opened</c> event and its <c>expiry.exception.opened</c> audit row, the FEFO snapshot, the policy
/// read, and audit rows. Every write takes the caller's session, so it commits with the movement / lot or not at all. Scoped to the
/// caller's resolved Tenant + LegalEntity.
/// </summary>
public sealed class ExpiryHookWriter
{
    private readonly IMongoCollection<ExpiryPolicy> _policies;
    private readonly IMongoCollection<FefoExceptionCase> _cases;
    private readonly IMongoCollection<FefoExceptionEvent> _events;
    private readonly IMongoCollection<FefoIssueSnapshot> _snapshots;
    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly IMongoCollection<ExpiryAuditRecord> _audit;
    private readonly ITenantContext _tenant;
    private readonly ICorrelationContext _correlation;
    private readonly TimeProvider _clock;

    public ExpiryHookWriter(IMongoDatabase database, ITenantContext tenant, ICorrelationContext correlation, TimeProvider clock)
    {
        _policies = database.GetCollection<ExpiryPolicy>(ExpiryCollections.Policies);
        _cases = database.GetCollection<FefoExceptionCase>(ExpiryCollections.Exceptions);
        _events = database.GetCollection<FefoExceptionEvent>(ExpiryCollections.ExceptionEvents);
        _snapshots = database.GetCollection<FefoIssueSnapshot>(ExpiryCollections.FefoIssueSnapshots);
        _counters = database.GetCollection<BsonDocument>(ExpiryCollections.Counters);
        _audit = database.GetCollection<ExpiryAuditRecord>(InventoryCollections.Audit);
        _tenant = tenant;
        _correlation = correlation;
        _clock = clock;
    }

    public DateTimeOffset Now() => _clock.GetUtcNow();

    private FilterDefinition<ExpiryPolicy> PolicyScope()
        => Builders<ExpiryPolicy>.Filter.Eq(x => x.TenantId, _tenant.TenantId) & Builders<ExpiryPolicy>.Filter.Eq(x => x.LegalEntityId, _tenant.LegalEntityId)
           & Builders<ExpiryPolicy>.Filter.Eq(x => x.IsDeleted, false);

    /// <summary>The company's policy (defaults when none is stored); in <paramref name="session"/> when given.</summary>
    public async Task<ExpiryPolicy> PolicyAsync(IClientSessionHandle? session, CancellationToken ct)
        => (session is null
               ? await _policies.Find(PolicyScope()).FirstOrDefaultAsync(ct)
               : await _policies.Find(session, PolicyScope()).FirstOrDefaultAsync(ct))
           ?? ExpiryPolicyRules.Defaults();

    /// <summary>Opens a system case (origin System, no opener) in <paramref name="session"/>; returns its id.</summary>
    public async Task<Guid> OpenAsync(IClientSessionHandle session, HookCase hook, ExpiryPolicy policy, CancellationToken ct)
    {
        var now = Now();
        var c = new FefoExceptionCase
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            LegalEntityId = _tenant.LegalEntityId,
            CreatedAt = now,
            Number = await ExpiryNumbers.NextAsync(_counters, session, _tenant.TenantId, now.UtcDateTime.Year, ct),
            Type = hook.Type,
            Origin = ExpiryExceptionOrigin.System,
            Status = ExpiryExceptionStatus.Open,
            ItemId = hook.Lot.ItemId,
            SkuId = hook.Lot.SkuId,
            SkuLevel = hook.Lot.SkuLevel,
            LotId = hook.Lot.Id.ToString("D"),
            LotNumber = hook.Lot.LotNumber,
            ExpiryDate = hook.Lot.ExpiryDate,
            RetestDate = hook.Lot.RetestDate,
            WarehouseId = hook.WarehouseId,
            Quantity = hook.Quantity,
            BaseUomId = hook.BaseUomId,
            SourceMovement = hook.SourceMovement,
            Facts = hook.Facts,
            Description = hook.Description,
            OpenedAtUtc = now,
            OpenedBy = null,
            OriginUserId = hook.OriginUserId,
            DueAtUtc = ExpiryExceptionRules.DueAt(now, policy, hook.Type),
            DedupKey = hook.DedupKey
        };
        await _cases.InsertOneAsync(session, c, cancellationToken: ct);
        await _events.InsertOneAsync(session, new FefoExceptionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            LegalEntityId = _tenant.LegalEntityId,
            CreatedAt = now,
            CaseId = c.Id,
            Sequence = ExpirySupport.NextSequence(),
            Action = ExpiryExceptionEventAction.Opened,
            ActorUserId = null,
            AtUtc = now,
            Text = hook.Description,
            CorrelationId = _correlation.CorrelationId
        }, cancellationToken: ct);
        await AuditAsync(session, ExpiryContract.AuditExceptionOpened, ExpiryContract.SubjectException, c.Id, c.Number, hook.OriginUserId,
            new Dictionary<string, string?>
            {
                ["type"] = ExpiryWire.Code(c.Type),
                ["origin"] = c.Origin.ToString(),
                ["lotId"] = c.LotId,
                ["warehouseId"] = c.WarehouseId,
                ["sourceTransactionId"] = c.SourceMovement?.TransactionId,
                ["dueAtUtc"] = ExpirySupport.Wire(c.DueAtUtc)
            }, ct);
        return c.Id;
    }

    public Task SnapshotAsync(IClientSessionHandle session, FefoIssueSnapshot snapshot, CancellationToken ct)
    {
        snapshot.TenantId = _tenant.TenantId;
        snapshot.LegalEntityId = _tenant.LegalEntityId;
        return _snapshots.InsertOneAsync(session, snapshot, cancellationToken: ct);
    }

    /// <summary>An audit row (insert-only, <c>inventory_audit</c>); in <paramref name="session"/> when given, else on its own.</summary>
    public Task AuditAsync(
        IClientSessionHandle? session, string action, string subjectType, Guid subjectId, string? number, Guid? userId,
        Dictionary<string, string?> detail, CancellationToken ct)
    {
        var now = Now();
        var record = new ExpiryAuditRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            LegalEntityId = _tenant.LegalEntityId,
            CreatedAt = now,
            Action = action,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectNumber = number,
            UserId = userId,
            Detail = detail,
            CorrelationId = _correlation.CorrelationId,
            LeSource = _tenant.LegalEntitySource,
            OccurredAt = now
        };
        return session is null
            ? _audit.InsertOneAsync(record, cancellationToken: ct)
            : _audit.InsertOneAsync(session, record, cancellationToken: ct);
    }

    public static string Day(DateTimeOffset? value) => value?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
}
