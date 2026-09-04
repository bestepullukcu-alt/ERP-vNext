using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;

/// <summary>
/// Publishes what this FU is and, just as importantly, what it is NOT. The limitations are the contract a consumer can
/// rely on: they say out loud that FU02 records actuals but advances no stage, that it never mutates the plan atom or the
/// plan's lifecycle, that a submitted report is immutable, and that the executed-marker reflection onto the plan is a
/// documented no-op (F-EXECUTED-MARKER) because FU01 exposes no "executed" transition.
/// </summary>
public sealed class GetVisitReportContractHandler
    : IRequestHandler<GetVisitReportContractQuery, Response<VisitReportContractDto>>
{
    public const string ModuleId = "MOD-0155-FU02";
    public const string ModuleName = "Visit Report";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU02-visit-report (the EXECUTION counterpart of FU05's setup: the rep's Day/Week execution calendar of the FU01 "
        + "PlannedVisit atoms FU05 generated, mark done/missed/rescheduled inline, and record the immutable Visit Report - "
        + "the ACTUAL content presented + outcome + doctor feedback + samples/materials + follow-up flag, linked to the "
        + "plan by PlannedVisitId). record-outcome / submit-report / amend (append-only) / calendar read / report "
        + "read-list / contract, an in-domain fail-closed vocabulary, reference-data-driven outcome + sample types. FU02 "
        + "is NOT an engine (D8): it generates no plan, packs no slot, computes no route/capacity, and advances no content "
        + "stage - it RECORDS the actual StageIndex (the loop-closing value) and writes NO advanced cursor onto the plan "
        + "atom (D-STAGE-ADVANCE = B). FU01/FU03/FU04/FU05 and every master are read-only.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a VisitReport is a NEW immutable aggregate linked to the FU01 plan atom by PlannedVisitId (D-REPORT-PERSISTENCE = A); execution data is NEVER written as fields onto the PlannedVisit atom (FU01 §2.3 bans PlannedVisit.ActualStartTime) - plan and execution are not one document",
        "the executed-marker reflection onto the plan atom is a DOCUMENTED NO-OP (F-EXECUTED-MARKER): FU01's PlanStatus machine (draft/planned/confirmed/cancelled/archived) exposes no clean 'executed' transition and its aggregate is protected, so rather than forcing a semantically-wrong FU01 transition, the report-side VisitExecutionOutcome (completed/missed/rescheduled) is the SOLE source of truth and the plan atom is left byte-for-byte unchanged (D-EXECUTION-STATUS = A, honoured with the gap flagged)",
        "cancelled is NOT an FU02 outcome: it stays FU01's existing cancel command, so FU02 never touches the plan's PlanStatus machine or adds a terminal state to it",
        "FU02 records the ACTUAL presented StageIndex on the report and writes NO advanced cursor onto the plan atom (D-STAGE-ADVANCE = B). The next-stage arithmetic (nextIndex = prior + 1) stays FU04; FU05's switch to read the last COMPLETED VisitReport's StageIndex as PriorStageIndex is a FU05-side follow-up (F-STAGE-READ), out of FU02's write scope",
        "a submitted report is immutable in place after a short correction window (D-EDIT-WINDOW); after the window a correction is an append-only amendment (who/when/why + changed fields), never a silent in-place edit - the pharma audit posture",
        "there is exactly one report per plan atom (1:1 by PlannedVisitId); a second report for the same visit is refused, and a report that links to no existing plan atom is refused (no orphan reports)",
        "outcome codes and sample/material types are REFERENCE-DATA-driven (MOD-0048, F-RD): they are bounded-string validated but NOT enum-checked against a hardcoded fallback list. ExecutionOutcome and ReportStatus ARE in-domain fail-closed vocabularies (out-of-set → 400)",
        "the execution calendar is a bespoke tenant-shell Day/Week surface (D-CALENDAR-UI = A), NOT a Golden DataTable CRUD page - verify_datatable_page is N/A",
        "FU02 computes no schedule, route, capacity or next stage (D8); it holds no /generate, /optimize, /pack, /advance and no write path into another module's aggregate",
        "RBAC keys crm.visit-report.{read,record,amend} are DEFINED but NOT seeded; record ALSO requires FU01 crm.planned-visit.manage; the endpoints run on the documented DEV-ONLY territory fallback (F-RBAC), under which the read/record/amend split cannot be enforced in dev",
        "the FU02 supply of a real LastVisitDate/DueStatus to the MOD-0151 readiness projection (FU01 §8.5) is a downstream read (F-READINESS) - FU02 records the executed-visit fact; it does not write the projection",
        "GPS/geo check-in, e-signature, expense/time entry are out of scope (deferred / MOD-0280 SoR); TenantId is server-resolved and never accepted from a payload; there is no DELETE and no bulk-delete anywhere"
    };

    private readonly ITenantContext _tenant;

    public GetVisitReportContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<VisitReportContractDto>> Handle(
        GetVisitReportContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<VisitReportContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new VisitReportContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            VisitReportFeatureFlags.Current,
            VisitReportVocabularyDto.Current,
            VisitReportSupportedFilters.Current,
            VisitReportContractLimits.Current,
            VisitReportErrorCodes.All,
            VisitReportPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<VisitReportContractDto>.Success(dto));
    }
}
