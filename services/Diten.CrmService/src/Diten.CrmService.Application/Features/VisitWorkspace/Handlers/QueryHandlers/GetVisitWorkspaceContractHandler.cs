using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;

/// <summary>WP-VW-W2 — the workspace contract: the calendar statuses (W1 codes + <c>draft</c>), the reason set and its
/// actions, the limits, the week states, the visit sources and the refusal codes the dialogs map to text.</summary>
public sealed class GetVisitWorkspaceContractHandler
    : IRequestHandler<GetVisitWorkspaceContractQuery, Response<VisitWorkspaceContractDto>>
{
    public const string ModuleId = "MOD-0155-VW";

    public static readonly IReadOnlyList<string> Permissions = new[]
    {
        VisitReport.VisitReportPermissions.Read, VisitPlanning.VisitPlanningPermissions.Read
    };

    private readonly ITenantContext _tenant;

    public GetVisitWorkspaceContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<VisitWorkspaceContractDto>> Handle(
        GetVisitWorkspaceContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<VisitWorkspaceContractDto>.Fail("Tenant context is required.", 400));
        }

        var codes = VisitWorkspaceErrorCodes.All
            .Concat(new[]
            {
                VisitReportErrorCodes.CalendarRangeInvalid, VisitReportErrorCodes.DeadlinePassed,
                VisitReportErrorCodes.PlanCancelled, VisitOwnership.ResourceNotCaller
            })
            .ToList();

        return Task.FromResult(Response<VisitWorkspaceContractDto>.Success(new VisitWorkspaceContractDto(
            ModuleId,
            tenantId,
            VisitWorkspaceLimits.WorkStatuses,
            VisitOutcomeReasons.ReasonSet,
            VisitOutcomeReasons.AppliesToAll,
            VisitWorkspaceLimits.RescheduleOptionDays,
            VisitWorkspaceLimits.MaxWindowDays,
            VisitReportLimits.ReportDeadlineHours,
            VisitReportLimits.MaxReasonLength,
            new[] { WorkspaceWeekStates.Draft, WorkspaceWeekStates.Approved, WorkspaceWeekStates.Past, WorkspaceWeekStates.None },
            PlannedVisitSource.All,
            codes,
            Permissions)));
    }
}
