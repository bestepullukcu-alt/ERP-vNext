using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// MOD-0155 FU02 test doubles — pure in-memory, no Mongo. The report store and the read-only PlanAtom seam are both
/// fakes, so the FU02 rules are exercised without a database and without touching FU01's aggregate.
/// </summary>
internal sealed class FakeVisitReportRepository : IVisitReportRepository
{
    public List<VisitReportEntity> Items { get; } = new();
    public int InsertCount { get; private set; }
    public int ReplaceCount { get; private set; }

    private IEnumerable<VisitReportEntity> Scope(Guid tenantId)
        => Items.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public Task<VisitReportEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Scope(tenantId).FirstOrDefault(x => x.Id == id));

    public Task<VisitReportEntity?> GetByPlannedVisitIdAsync(Guid tenantId, Guid plannedVisitId, CancellationToken ct)
        => Task.FromResult(Scope(tenantId)
            .Where(x => x.PlannedVisitId == plannedVisitId)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefault());

    public Task<IReadOnlyList<VisitReportEntity>> ListAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VisitReportEntity>>(
            Scope(tenantId).OrderByDescending(x => x.ExecutedAt).ToList());

    public Task<IReadOnlyList<VisitReportEntity>> ListByPlannedVisitIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> plannedVisitIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VisitReportEntity>>(
            Scope(tenantId).Where(x => plannedVisitIds.Contains(x.PlannedVisitId)).ToList());

    public Task InsertAsync(VisitReportEntity entity, CancellationToken ct)
    {
        InsertCount++;
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(VisitReportEntity entity, int expectedVersion, CancellationToken ct)
    {
        ReplaceCount++;
        var existing = Items.FirstOrDefault(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
        if (existing is null || existing.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        entity.Version = expectedVersion + 1;
        Items[Items.IndexOf(existing)] = entity;
        return Task.FromResult(true);
    }
}

/// <summary>Read-only PlanAtom seam. FU02 reads it to reject an orphan report and to default the reporting resource;
/// it NEVER writes it. The write counters exist only so a test can assert FU02 leaves FU01's aggregate untouched.</summary>
internal sealed class FakePlannedVisitReadRepository : IPlannedVisitRepository
{
    public List<PlanAtom> Items { get; } = new();
    public int InsertCount { get; private set; }
    public int ReplaceCount { get; private set; }

    private IEnumerable<PlanAtom> Scope(Guid tenantId)
        => Items.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public Task<PlanAtom?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Scope(tenantId).FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<PlanAtom>> ListAsync(Guid tenantId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlanAtom>>(Scope(tenantId).ToList());

    public Task<IReadOnlyList<PlanAtom>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlanAtom>>(
            Scope(tenantId).Where(x => x.VisitCode == visitCode).ToList());

    public Task<IReadOnlyList<PlanAtom>> ListByResourceAndDateAsync(
        Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlanAtom>>(
            Scope(tenantId).Where(x => x.Resource.ResourceId == resourceId && x.PlannedDate == plannedDate).ToList());

    public Task<IReadOnlyList<PlanAtom>> ListByTargetAndDateAsync(
        Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlanAtom>>(
            Scope(tenantId).Where(x => x.TargetId == targetId && x.PlannedDate == plannedDate).ToList());

    public Task InsertAsync(PlanAtom entity, CancellationToken ct)
    {
        InsertCount++;
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(PlanAtom entity, int expectedVersion, CancellationToken ct)
    {
        ReplaceCount++;
        return Task.FromResult(true);
    }
}
