using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;

/// <summary>Loads one report's detail. A cross-tenant id resolves to nothing and returns 404 (no existence leak).</summary>
public sealed class GetVisitReportByIdHandler
    : IRequestHandler<GetVisitReportByIdQuery, Response<VisitReportDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitReportRepository _repository;

    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly ICallerScope _caller;

    public GetVisitReportByIdHandler(
        ITenantContext tenant, IVisitReportRepository repository, IPlannedVisitRepository plannedVisits, ICallerScope caller)
    {
        _plannedVisits = plannedVisits;
        _caller = caller;
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<VisitReportDetailDto>> Handle(
        GetVisitReportByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitReportDetailDto>.Fail("Tenant context is required.", 400);
        }

        var report = await _repository.GetByIdAsync(tenantId, request.VisitReportId, cancellationToken);
        // WP-VP-2 (B-1) — a report is readable only with its planned visit (own, or read-all); otherwise 404.
        var plan = report is null ? null : await _plannedVisits.GetByIdAsync(tenantId, report.PlannedVisitId, cancellationToken);
        return report is null || !_caller.MayAccess(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll, plan?.Resource.ResourceId)
            ? Response<VisitReportDetailDto>.Fail("Visit report not found.", 404)
            : Response<VisitReportDetailDto>.Success(VisitReportMapper.ToDetail(report));
    }
}
