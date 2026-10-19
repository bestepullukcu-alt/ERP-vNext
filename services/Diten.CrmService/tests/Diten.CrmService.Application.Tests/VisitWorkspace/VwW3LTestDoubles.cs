using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>WP-VW-W3L — the layers in memory, with the production repository's contract: one ACTIVE layer per
/// (tenant, type, ref — the ref case-insensitively), a versioned replace that refuses a stale version.</summary>
internal sealed class FakeLayerRepository : IVisitEvidencePolicyLayerRepository
{
    public List<VisitEvidencePolicyLayer> Items { get; } = new();
    public int Reads { get; private set; }

    public Task<IReadOnlyList<VisitEvidencePolicyLayer>> ListActiveAsync(Guid tenantId, CancellationToken ct)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<VisitEvidencePolicyLayer>>(Items.Where(l => l.TenantId == tenantId && l.IsActive()).ToList());
    }

    public Task<VisitEvidencePolicyLayer?> GetActiveAsync(Guid tenantId, string scopeType, string scopeRef, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(l => l.TenantId == tenantId && l.IsActive() && l.ScopeType == scopeType
                                                    && string.Equals(l.ScopeRef, scopeRef, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> InsertAsync(VisitEvidencePolicyLayer entity, CancellationToken ct)
    {
        if (Items.Any(l => l.TenantId == entity.TenantId && l.IsActive() && l.ScopeType == entity.ScopeType
                           && string.Equals(l.ScopeRef, entity.ScopeRef, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(false);
        }

        Items.Add(entity);
        return Task.FromResult(true);
    }

    public Task<bool> ReplaceAsync(VisitEvidencePolicyLayer entity, int expectedVersion, CancellationToken ct)
    {
        var index = Items.FindIndex(l => l.Id == entity.Id && l.TenantId == entity.TenantId);
        if (index < 0 || Items[index].Version != expectedVersion || StaleVersion == expectedVersion)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        Items[index] = entity;
        return Task.FromResult(true);
    }

    /// <summary>A replace at this version loses (a concurrent writer got there first).</summary>
    public int? StaleVersion { get; set; }

    public VisitEvidencePolicyLayer Add(Guid tenant, string type, string scopeRef, Action<VisitEvidencePolicyLayer> set, int version = 1)
    {
        var layer = new VisitEvidencePolicyLayer { TenantId = tenant, ScopeType = type, ScopeRef = scopeRef, Version = version };
        set(layer);
        Items.Add(layer);
        return layer;
    }
}

/// <summary>The Platform seat in memory: counts the calls, fails on demand.</summary>
internal sealed class FakeSeatReader : IOrganizationSeatReader
{
    public Dictionary<string, OrganizationSeat?> Seats { get; } = new(StringComparer.Ordinal);
    public bool Fail { get; set; }
    public int SingleCalls { get; private set; }
    public int BulkCalls { get; private set; }
    public List<int> BulkSizes { get; } = new();

    public Task<OrganizationSeatResult> GetAsync(string userId, CancellationToken ct)
    {
        SingleCalls++;
        return Task.FromResult(Fail ? new OrganizationSeatResult(false, null) : new OrganizationSeatResult(true, Seats.GetValueOrDefault(userId)));
    }

    public Task<OrganizationSeatBulkResult> LookupAsync(IReadOnlyCollection<string> userIds, CancellationToken ct)
    {
        BulkCalls++;
        BulkSizes.Add(userIds.Count);
        return Task.FromResult(Fail
            ? new OrganizationSeatBulkResult(new Dictionary<string, OrganizationSeat?>(), userIds.ToHashSet(StringComparer.Ordinal))
            : new OrganizationSeatBulkResult(userIds.ToDictionary(u => u, u => Seats.GetValueOrDefault(u), StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)));
    }

    public void Seat(string userId, Guid legalEntity)
        => Seats[userId] = new OrganizationSeat(Guid.NewGuid(), "uz-medical-representative", Guid.NewGuid(), "UZRO-FF", legalEntity, DateTimeOffset.UnixEpoch);
}

internal sealed class RecordingChanges : IVisitEvidencePolicyChangeRepository
{
    public List<VisitEvidencePolicyChange> Rows { get; } = new();

    public Task InsertManyAsync(IReadOnlyCollection<VisitEvidencePolicyChange> rows, CancellationToken ct)
    {
        Rows.AddRange(rows);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<VisitEvidencePolicyChange> Items, long Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct)
    {
        var rows = Rows.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.At).ToList();
        return Task.FromResult<(IReadOnlyList<VisitEvidencePolicyChange>, long)>((rows.Skip(skip).Take(take).ToList(), rows.Count));
    }
}

internal sealed class RecordingAudit : IVisitEvidenceAuditPublisher
{
    public List<(string Event, Guid Tenant, Guid Entity, int Version, string? Detail)> Events { get; } = new();

    public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version, string? detail, CancellationToken ct)
    {
        Events.Add((eventName, tenantId, entityId, version, detail));
        return Task.CompletedTask;
    }
}

internal sealed class FixedRoster(IReadOnlyList<EvidenceRepDto> reps) : IVisitEvidenceRepRoster
{
    public Task<IReadOnlyList<EvidenceRepDto>> ListAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(reps);
}

internal sealed class W3LClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
