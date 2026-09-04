using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Queries;

/// <summary>Lists reports for the tenant, narrowed by the supported filters. The <c>resourceId</c> filter is an EXPLICIT
/// narrowing, not an ambient "only my reports" scope — that ABAC rule cannot be faked before MOD-0018-FU15 (F-ABAC).</summary>
public sealed record ListVisitReportsQuery(
    Guid? PlannedVisitId = null,
    string? ReportStatus = null,
    string? ExecutionOutcome = null,
    string? ResourceId = null) : IRequest<Response<VisitReportListDto>>;
