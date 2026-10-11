using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;

/// <summary>Lists reports for the tenant, applying the supported filters in memory (the repository never sorts the
/// DateTimeOffset fields at the server — parallel-arrays).</summary>
public sealed class ListVisitReportsHandler : IRequestHandler<ListVisitReportsQuery, Response<VisitReportListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitReportRepository _repository;

    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly ICallerScope _caller;

    public ListVisitReportsHandler(
        ITenantContext tenant, IVisitReportRepository repository, IPlannedVisitRepository plannedVisits, ICallerScope caller)
    {
        _plannedVisits = plannedVisits;
        _caller = caller;
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<VisitReportListDto>> Handle(
        ListVisitReportsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitReportListDto>.Fail("Tenant context is required.", 400);
        }

        var rows = await _repository.ListAsync(tenantId, cancellationToken);
        IEnumerable<Domain.Entities.VisitReport> query = rows;

        // WP-VP-2 (B-1) — without read-all only the reports on the caller's own planned visits are listed.
        if (!_caller.HasPermission(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll))
        {
            var own = (await _plannedVisits.ListAsync(tenantId, cancellationToken))
                .Where(v => _caller.MayAccess(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll, v.Resource.ResourceId))
                .Select(v => v.Id)
                .ToHashSet();
            query = query.Where(r => own.Contains(r.PlannedVisitId));
        }

        if (request.PlannedVisitId is { } plannedVisitId && plannedVisitId != Guid.Empty)
        {
            query = query.Where(r => r.PlannedVisitId == plannedVisitId);
        }

        if (VisitReportValidation.Trim(request.ReportStatus) is { } status)
        {
            var s = VisitReportStatus.Normalize(status);
            query = query.Where(r => string.Equals(r.ReportStatus, s, StringComparison.Ordinal));
        }

        if (VisitReportValidation.Trim(request.ExecutionOutcome) is { } outcome)
        {
            var o = VisitExecutionOutcome.Normalize(outcome);
            query = query.Where(r => string.Equals(r.ExecutionOutcome, o, StringComparison.Ordinal));
        }

        if (VisitReportValidation.Trim(request.ResourceId) is { } resourceId)
        {
            query = query.Where(r => string.Equals(r.ReportedByResourceId, resourceId, StringComparison.Ordinal));
        }

        var items = query.Select(VisitReportMapper.ToListItem).ToList();
        return Response<VisitReportListDto>.Success(new VisitReportListDto(items, items.Count));
    }
}
