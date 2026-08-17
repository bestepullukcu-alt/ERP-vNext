using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class AuditEventRepository : IAuditEventRepository
{
    private readonly IMongoCollection<AuditEvent> _collection;
    private readonly ITenantContext _tenantContext;

    public AuditEventRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents);
        _tenantContext = tenantContext;
    }

    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        EnsureTenantScope(auditEvent);
        auditEvent.ValidateAppend();

        await _collection.InsertOneAsync(auditEvent, cancellationToken: ct);
    }

    public async Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit event id is required.", nameof(id));
        }

        var tenantId = GetCurrentReadTenantId();
        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<AuditEvent>.Filter.Eq(x => x.Id, id),
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        EnsurePlatformCrossTenantReadAllowed(tenantId);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit event id is required.", nameof(id));
        }

        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<AuditEvent>.Filter.Eq(x => x.Id, id),
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid id, CancellationToken ct = default)
    {
        EnsurePlatformCrossTenantReadAllowed();

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Audit event id is required.", nameof(id));
        }

        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.Id, id),
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<AuditEventSearchResult> SearchForPlatformCrossTenantAsync(
        AuditEventSearchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePlatformCrossTenantReadAllowed();

        var filter = BuildSearchFilter(request);
        var skip = Math.Max(request.Skip, 0);
        var take = Math.Clamp(request.Take, 1, 50_000);

        var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await _collection.Find(filter)
            .Sort(Builders<AuditEvent>.Sort.Descending(x => x.OccurredAtUtc))
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);

        return new AuditEventSearchResult(items, totalCount);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Audit event correlation id is required.", nameof(correlationId));
        }

        var tenantId = GetCurrentReadTenantId();
        var filters = new List<FilterDefinition<AuditEvent>>
        {
            Builders<AuditEvent>.Filter.Eq(x => x.CorrelationId, correlationId),
            Builders<AuditEvent>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false)
        };

        return await _collection.Find(Builders<AuditEvent>.Filter.And(filters))
            .Sort(Builders<AuditEvent>.Sort.Descending(x => x.OccurredAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdForPlatformCrossTenantAsync(Guid correlationId, CancellationToken ct = default)
    {
        EnsurePlatformCrossTenantReadAllowed();

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Audit event correlation id is required.", nameof(correlationId));
        }

        // Phase 2 IAuditService must wrap this raw cross-tenant query and meta-audit the access.
        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.CorrelationId, correlationId),
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter)
            .Sort(Builders<AuditEvent>.Sort.Descending(x => x.OccurredAtUtc))
            .ToListAsync(ct);
    }

    public async Task<int> RedactActorPiiForPlatformCrossTenantAsync(
        AuditActorPiiRedactionRequest request,
        CancellationToken ct = default)
    {
        EnsurePlatformCrossTenantReadAllowed();

        if (request.ActorId == Guid.Empty)
        {
            throw new ArgumentException("Audit actor id is required.", nameof(request));
        }

        if (request.RedactedByActorId == Guid.Empty)
        {
            throw new ArgumentException("Audit redaction actor id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Audit redaction reason is required.", nameof(request));
        }

        var reason = request.Reason.Trim();
        if (reason.Length > 500)
        {
            reason = reason[..500];
        }

        var filter = Builders<AuditEvent>.Filter.And(
            Builders<AuditEvent>.Filter.Eq(x => x.ActorId, request.ActorId),
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false));

        var update = Builders<AuditEvent>.Update
            .Set(nameof(AuditEvent.ActorEmailMasked), "[REDACTED]")
            .Set(nameof(AuditEvent.ActorDisplayNameMasked), "[REDACTED]")
            .Set(nameof(AuditEvent.IpAddressMasked), "[REDACTED]")
            .Set(nameof(AuditEvent.RedactionStatus), AuditRedactionStatus.ActorPiiRedacted)
            .Set(nameof(AuditEvent.RedactedAtUtc), request.RedactedAtUtc)
            .Set(nameof(AuditEvent.RedactedByActorId), request.RedactedByActorId)
            .Set(nameof(AuditEvent.RedactionReason), reason);

        var result = await _collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > int.MaxValue ? int.MaxValue : (int)result.ModifiedCount;
    }

    private void EnsureTenantScope(AuditEvent auditEvent)
    {
        if (auditEvent.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit event tenant id is required. Use PlatformSystemTenantId for platform-global audit events.");
        }

        if (_tenantContext.IsPlatformContext)
        {
            var targetTenantId = _tenantContext.TargetTenantId;
            if (AuditTenantIds.IsPlatformSystemTenantId(auditEvent.TenantId) || auditEvent.TenantId == targetTenantId)
            {
                return;
            }

            throw new InvalidOperationException("Platform audit events must use PlatformSystemTenantId or the explicit target tenant id.");
        }

        if (!_tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Tenant context must be resolved before appending tenant audit events.");
        }

        if (auditEvent.TenantId != _tenantContext.TenantId)
        {
            throw new InvalidOperationException("Audit event tenant id must match the resolved tenant context.");
        }
    }

    private static FilterDefinition<AuditEvent> BuildSearchFilter(AuditEventSearchRequest request)
    {
        var filters = new List<FilterDefinition<AuditEvent>>
        {
            Builders<AuditEvent>.Filter.Eq(x => x.IsDeleted, false)
        };

        if (request.TenantId is { } tenantId)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.TenantId, tenantId));
        }

        if (request.TargetTenantId is { } targetTenantId)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.TargetTenantId, targetTenantId));
        }

        if (request.CorrelationId is { } correlationId)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.CorrelationId, correlationId));
        }

        if (request.ActorId is { } actorId)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.ActorId, actorId));
        }

        if (request.Category is { } category)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.Category, category));
        }

        if (request.Operation is { } operation)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.Operation, operation));
        }

        if (request.Outcome is { } outcome)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.Outcome, outcome));
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.EntityType, request.EntityType.Trim()));
        }

        if (request.EntityId is { } entityId)
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.EntityId, entityId));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceModule))
        {
            filters.Add(Builders<AuditEvent>.Filter.Eq(x => x.SourceModule, request.SourceModule.Trim()));
        }

        if (request.FromUtc.HasValue)
        {
            filters.Add(Builders<AuditEvent>.Filter.Gte(x => x.OccurredAtUtc, request.FromUtc.Value));
        }

        if (request.ToUtc.HasValue)
        {
            filters.Add(Builders<AuditEvent>.Filter.Lte(x => x.OccurredAtUtc, request.ToUtc.Value));
        }

        return Builders<AuditEvent>.Filter.And(filters);
    }

    private Guid GetCurrentReadTenantId()
    {
        if (!_tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Tenant context must be resolved before reading audit events.");
        }

        if (_tenantContext.IsPlatformContext && _tenantContext.TargetTenantId == Guid.Empty)
        {
            return AuditTenantIds.PlatformSystemTenantId;
        }

        return _tenantContext.TenantId;
    }

    private void EnsurePlatformCrossTenantReadAllowed(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Audit event tenant id is required. Use PlatformSystemTenantId for platform-global audit events.", nameof(tenantId));
        }

        EnsurePlatformCrossTenantReadAllowed();
    }

    private void EnsurePlatformCrossTenantReadAllowed()
    {
        if (!_tenantContext.IsPlatformContext)
        {
            throw new InvalidOperationException("Platform context is required for cross-tenant audit reads.");
        }

        if (!_tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Tenant context must be resolved before reading audit events.");
        }
    }
}
