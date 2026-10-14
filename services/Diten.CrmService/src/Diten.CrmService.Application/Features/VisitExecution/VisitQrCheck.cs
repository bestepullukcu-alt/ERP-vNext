using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>
/// WP-VW-W3E (W3E-BE-c, §2.10) — the start's plan rules, in the start's order, shared by the start and the QR pre-check
/// so both answer a plan alike: (1) <see cref="PlanAsync"/> — the plan exists, it is the caller's own (404 / 403
/// <c>resource_not_caller</c>), a known kind; (2) <see cref="StateAsync"/> — an active plan, its day is today, not
/// started, not reported, the 48 h lock. The start checks the coordinates between the two (as before).
/// </summary>
internal static class VisitStartGate
{
    public static async Task<(PlanAtom? Plan, VisitExecutionRules.Failure? Failure)> PlanAsync(
        Guid tenantId, Guid plannedVisitId, string? kind, ICallerScope caller, IPlannedVisitRepository plans, CancellationToken cancellationToken)
    {
        var plan = plannedVisitId == Guid.Empty ? null : await plans.GetByIdAsync(tenantId, plannedVisitId, cancellationToken);
        if (plan is null)
        {
            return (null, VisitExecutionAccess.PlanNotFound());
        }

        if (VisitExecutionAccess.WriteRefusal(caller, plan) is { } ownership)
        {
            return (null, ownership);
        }

        if (kind is not null && !VisitExecutionKinds.IsKnown(kind))
        {
            return (null, VisitExecutionRules.Capture($"kind must be one of: {string.Join(", ", VisitExecutionKinds.All)}."));
        }

        return (plan, null);
    }

    public static async Task<VisitExecutionRules.Failure?> StateAsync(
        Guid tenantId, PlanAtom plan, IVisitExecutionRepository executions, IVisitReportRepository reports, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // An approved week writes its visits as `planned` (the engine, unplanned and reschedule do too); `confirmed` is
        // the optional manual confirm. A draft or cancelled plan is not startable.
        if (!plan.IsActivePlan())
        {
            return new VisitExecutionRules.Failure("Only an approved visit can be started.", VisitExecutionErrorCodes.NotConfirmed, 409);
        }

        var now = clock.GetUtcNow();
        if (plan.PlannedDate != VisitReportValidation.Today(clock))
        {
            return new VisitExecutionRules.Failure("A visit is started on its own day only.", VisitExecutionErrorCodes.NotToday, 409);
        }

        if (await executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken) is not null)
        {
            return StartVisitExecutionHandler.AlreadyStarted();
        }

        var report = await reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken);
        if (VisitExecutionAccess.IsReported(report))
        {
            return new VisitExecutionRules.Failure("This visit already has its outcome.", VisitExecutionErrorCodes.AlreadyReported, 409);
        }

        return VisitReportDeadline.IsPassed(plan.PlannedDate, now) ? StartVisitExecutionHandler.DeadlinePassed() : null;
    }

    /// <summary>CT-1 (fail-closed): no QR verifier in this host. <c>off</c> needs none; any other effective mode is a 503 —
    /// a QR rule is never skipped silently because a registration went missing.</summary>
    public static VisitExecutionRules.Failure? QrVerifierMissing(string? qrMode)
        => (VisitExecutionRules.Normalize(qrMode) ?? VisitQrModes.Off) == VisitQrModes.Off
            ? null
            : new VisitExecutionRules.Failure(
                "The QR code cannot be verified right now. Please try again later.", VisitExecutionErrorCodes.QrSigningUnavailable, 503);
}

/// <summary>
/// WP-VW-W3E (W3E-BE-c, §2.10) — <c>POST api/crm/visit-execution/qr-check</c>: what the start WOULD answer for this QR
/// before the rep presses Start. <paramref name="Kind"/> defaults to <c>in_person</c> (a remote visit is <c>not_applicable</c>,
/// as at the start). The payload is never logged: <see cref="ToString"/> hides it.
/// </summary>
public sealed record CheckVisitQrQuery(Guid PlannedVisitId, string? QrPayload, string? Kind = null) : IRequest<Response<VisitQrCheckDto>>
{
    public override string ToString()
        => $"{nameof(CheckVisitQrQuery)} {{ PlannedVisitId = {PlannedVisitId}, QrPayload = {(QrPayload is null ? "null" : "***")}, Kind = {Kind} }}";
}

/// <summary>The body of <c>qr-check</c>.</summary>
public sealed record VisitQrCheckInput(Guid PlannedVisitId, string? QrPayload, string? Kind = null)
{
    public override string ToString()
        => $"{nameof(VisitQrCheckInput)} {{ PlannedVisitId = {PlannedVisitId}, QrPayload = {(QrPayload is null ? "null" : "***")}, Kind = {Kind} }}";
}

/// <summary>
/// The pre-check answer: the effective <c>qrMode</c>, the verdict (<see cref="VisitQrScanStatuses"/>), whether the start
/// would refuse it (+ its error code), the flag it would record, the SCANNED code's institution (only a valid signature of
/// this tenant: wrong_account / revoked / ok) and how it matched, and whether the visit's institution chain has an active
/// code (no payload: <c>not_issued</c> ↔ <c>missing</c>, screen S6).
/// </summary>
public sealed record VisitQrCheckDto(
    string QrMode,
    string Status,
    bool WouldRefuse,
    string? ErrorCode,
    string? Flag,
    Guid? AccountId,
    string? AccountName,
    string? MatchedVia,
    bool ExpectedHasActiveCode);

/// <summary>
/// The pre-check runs the start's plan rules (<see cref="VisitStartGate"/>), the same effective setting and the same
/// <see cref="IVisitQrVerifier.EvaluateAsync"/> — and WRITES NOTHING: no execution, no scan counter
/// (<see cref="IVisitQrVerifier.RecordScanAsync"/> is never called), no event. The final word stays the start's.
/// </summary>
public sealed class CheckVisitQrHandler : IRequestHandler<CheckVisitQrQuery, Response<VisitQrCheckDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IVisitExecutionRepository _executions;
    private readonly IAccountRepository _accounts;
    private readonly TimeProvider _clock;
    private readonly IVisitEvidencePolicyResolver _resolver;
    private readonly IVisitQrVerifier? _qr;

    public CheckVisitQrHandler(
        ITenantContext tenant, ICallerScope caller, IPlannedVisitRepository plans, IVisitReportRepository reports,
        IVisitExecutionRepository executions, IVisitEvidencePolicyRepository policies, IAccountRepository accounts,
        TimeProvider? clock = null, IVisitEvidencePolicyResolver? resolver = null, IVisitQrVerifier? qrVerifier = null)
    {
        _tenant = tenant;
        _caller = caller;
        _plans = plans;
        _reports = reports;
        _executions = executions;
        _accounts = accounts;
        _clock = clock ?? TimeProvider.System;
        _resolver = resolver ?? new VisitEvidencePolicyResolver(policies);
        _qr = qrVerifier;
    }

    public async Task<Response<VisitQrCheckDto>> Handle(CheckVisitQrQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitQrCheckDto>.Fail("Tenant context is required.", 400);
        }

        var (found, planFailure) = await VisitStartGate.PlanAsync(tenantId, request.PlannedVisitId, request.Kind, _caller, _plans, cancellationToken);
        if (planFailure is not null || found is not { } plan)
        {
            return VisitExecutionAccess.Fail<VisitQrCheckDto>(planFailure ?? VisitExecutionAccess.PlanNotFound());
        }

        if (await VisitStartGate.StateAsync(tenantId, plan, _executions, _reports, _clock, cancellationToken) is { } stateFailure)
        {
            return VisitExecutionAccess.Fail<VisitQrCheckDto>(stateFailure);
        }

        var effective = await _resolver.ResolveAsync(tenantId, plan.Resource.ResourceId, _clock.GetUtcNow(), cancellationToken);
        var mode = VisitExecutionRules.Normalize(effective.Policy.QrMode) ?? VisitQrModes.Off;
        if (_qr is null)
        {
            return VisitStartGate.QrVerifierMissing(mode) is { } noVerifier
                ? VisitExecutionAccess.Fail<VisitQrCheckDto>(noVerifier)
                : Response<VisitQrCheckDto>.Success(new VisitQrCheckDto(mode, VisitQrScanStatuses.NotApplicable, false, null, null, null, null, null, false));
        }

        var evaluation = await _qr.EvaluateAsync(tenantId, plan, VisitExecutionRules.Normalize(request.Kind) ?? VisitExecutionKinds.InPerson,
            mode, request.QrPayload, scannedAt: null, cancellationToken);

        // the scanned code's institution: set by the verifier only for a valid signature of THIS tenant; read in this
        // tenant only (another tenant's institution is never named).
        string? accountName = null;
        if (evaluation.Qr.AccountId is { } scanned
            && await _accounts.GetByIdAsync(tenantId, scanned, cancellationToken) is { } account
            && account.TenantId == tenantId && !account.IsDeleted)
        {
            accountName = account.AccountName;
        }

        return Response<VisitQrCheckDto>.Success(new VisitQrCheckDto(
            mode, evaluation.Qr.Status, evaluation.Refusal is not null, evaluation.Refusal?.Code, evaluation.Flag,
            evaluation.Qr.AccountId, accountName, evaluation.Qr.MatchedVia, evaluation.ExpectedHasActiveCode));
    }
}
