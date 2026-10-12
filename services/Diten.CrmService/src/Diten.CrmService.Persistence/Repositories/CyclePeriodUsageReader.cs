using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-CYC-UI-1 — the Mongo side of <see cref="ICyclePeriodUsageReader"/>: four READ-ONLY queries per call, whatever
/// the number of periods (the list endpoint passes a whole page at once).
/// <para>Every query is tenant-scoped and excludes soft-deleted rows. Filters go through the typed builders so each
/// Guid member uses the serializer its class map declares (string vs binary) — a raw BSON filter here is how a by-id
/// query silently returns nothing. Nothing is sorted server-side (no DateTimeOffset sort key, the parallel-arrays trap).</para>
/// <para>Planned visits are fetched ONLY by the ids the sessions committed: a visit has no period field, and inferring
/// one from its date would attribute another period's visit to this one.</para>
/// </summary>
public sealed class CyclePeriodUsageReader : ICyclePeriodUsageReader
{
    private readonly IMongoCollection<Domain.Entities.CycleCapacity> _capacities;
    private readonly IMongoCollection<Campaign> _campaigns;
    private readonly IMongoCollection<PlanningSession> _sessions;
    private readonly IMongoCollection<PlannedVisit> _visits;

    public CyclePeriodUsageReader(IMongoDatabase database)
    {
        _capacities = database.GetCollection<Domain.Entities.CycleCapacity>(CycleCapacityRepository.CollectionName);
        _campaigns = database.GetCollection<Campaign>(CampaignRepository.CollectionName);
        _sessions = database.GetCollection<PlanningSession>(PlanningSessionRepository.CollectionName);
        _visits = database.GetCollection<PlannedVisit>(PlannedVisitRepository.CollectionName);
    }

    public async Task<CyclePeriodUsageSnapshot> ReadAsync(
        Guid tenantId, IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken cancellationToken)
    {
        if (cyclePeriodIds.Count == 0)
        {
            return CyclePeriodUsageSnapshot.Empty;
        }

        var periodIds = cyclePeriodIds.Distinct().ToList();
        var nullablePeriodIds = periodIds.Select(id => (Guid?)id).ToList();

        var capacities = await _capacities
            .Find(Builders<Domain.Entities.CycleCapacity>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted)
                  & Builders<Domain.Entities.CycleCapacity>.Filter.In(x => x.CyclePeriodId, periodIds))
            .ToListAsync(cancellationToken);

        var campaigns = await _campaigns
            .Find(Builders<Campaign>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted)
                  & Builders<Campaign>.Filter.In(x => x.CyclePeriodId, nullablePeriodIds))
            .ToListAsync(cancellationToken);

        var sessions = await _sessions
            .Find(Builders<PlanningSession>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted)
                  & Builders<PlanningSession>.Filter.In(x => x.CyclePeriodId, periodIds))
            .ToListAsync(cancellationToken);

        var visitIds = sessions.SelectMany(s => s.CommittedPlannedVisitIds).Distinct().ToList();
        var visits = visitIds.Count == 0
            ? new List<PlannedVisit>()
            : await _visits
                .Find(Builders<PlannedVisit>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted)
                      & Builders<PlannedVisit>.Filter.In(x => x.Id, visitIds))
                .ToListAsync(cancellationToken);

        return new CyclePeriodUsageSnapshot(
            capacities.Select(c => new CyclePeriodUsageCapacityRow(c.Id, c.CyclePeriodId, c.IsArchived)).ToList(),
            campaigns
                .Where(c => c.CyclePeriodId is not null)
                .Select(c => new CyclePeriodUsageCampaignRow(
                    c.Id, c.CyclePeriodId!.Value, c.CampaignCode, c.CampaignName, c.CampaignStatus))
                .ToList(),
            sessions.Select(s => new CyclePeriodUsageSessionRow(
                    s.Id, s.CyclePeriodId, s.ResourceId, s.Status, s.TargetWeekStart, s.CreatedAt,
                    s.CommittedPlannedVisitIds.ToList()))
                .ToList(),
            visits.Select(v => new CyclePeriodUsageVisitRow(v.Id, v.PlanStatus, v.PlannedDate)).ToList());
    }
}
