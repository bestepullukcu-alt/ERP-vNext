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

    public GetVisitReportByIdHandler(ITenantContext tenant, IVisitReportRepository repository)
    {
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
        return report is null
            ? Response<VisitReportDetailDto>.Fail("Visit report not found.", 404)
            : Response<VisitReportDetailDto>.Success(VisitReportMapper.ToDetail(report));
    }
}
