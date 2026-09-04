using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Queries;

/// <summary>Loads one report's detail. A cross-tenant id resolves to nothing and returns 404 (no existence leak).</summary>
public sealed record GetVisitReportByIdQuery(Guid VisitReportId) : IRequest<Response<VisitReportDetailDto>>;
