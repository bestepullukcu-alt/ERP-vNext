using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Services;

/// <summary>
/// MOD-0029-FU13 — retirement case orchestration (GMG-QMS-SOP-0001 §9.16: retirement requires justification, transition
/// assessment, communication and archival). Execution delegates the lifecycle change to the FU08 engine. The retired
/// document's UID/code are RETAINED and never reused (FU07 invariant); nothing is deleted.
/// </summary>
public sealed class DocumentRetirementService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentRetirementCaseRepository _cases;
    private readonly IDocumentSuspensionCaseRepository _suspensionCases;
    private readonly DocumentLifecycleService _lifecycle;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    private readonly IControlledCopyWithdrawalPort? _copyWithdrawal;

    public DocumentRetirementService(
        IDocumentMasterRegisterRepository register,
        IDocumentRetirementCaseRepository cases,
        IDocumentSuspensionCaseRepository suspensionCases,
        DocumentLifecycleService lifecycle,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        // MOD-0029-FU17 controlled-copy withdrawal port (optional). Null → no automatic withdrawal plan.
        IControlledCopyWithdrawalPort? copyWithdrawal = null)
    {
        _register = register;
        _cases = cases;
        _suspensionCases = suspensionCases;
        _lifecycle = lifecycle;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _copyWithdrawal = copyWithdrawal;
    }

    public async Task<Response<RetirementCaseModel>> RequestAsync(Guid registerEntryId, RequestRetirementInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (entry.LifecycleStatus is ControlledDocumentLifecycleStatus.Retired or ControlledDocumentLifecycleStatus.Superseded)
        {
            return Fail($"A {entry.LifecycleStatus} document cannot be retired again.", 409, SuspensionReasonCodes.NotEligible, correlationId);
        }

        // SOP §9.16: justification + transition assessment are mandatory at request time.
        if (string.IsNullOrWhiteSpace(input.RetirementReason)
            || string.IsNullOrWhiteSpace(input.JustificationReference)
            || string.IsNullOrWhiteSpace(input.TransitionAssessmentReference))
        {
            return Fail("A retirement reason, justification reference and transition assessment reference are required.", 400, SuspensionReasonCodes.EvidenceRequired, correlationId);
        }

        var history = await _cases.GetByRegisterEntryAsync(registerEntryId, ct);
        var retirementCase = new DocumentRetirementCase
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            CaseNumber = history.Count + 1,
            CaseStatus = RetirementCaseStatus.Requested,
            RetirementReason = input.RetirementReason.Trim(),
            JustificationReference = input.JustificationReference.Trim(),
            TransitionAssessmentReference = input.TransitionAssessmentReference.Trim(),
            ReplacementDocumentUid = TrimOrNull(input.ReplacementDocumentUid),
            ReplacementDocumentCode = TrimOrNull(input.ReplacementDocumentCode),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _cases.CreateAsync(retirementCase, ct);
        return Response<RetirementCaseModel>.Success(SuspensionWire.ToRetirement(retirementCase), 201, correlationId);
    }

    public async Task<Response<RetirementCaseModel>> ApproveAsync(Guid registerEntryId, Guid caseId, ApproveRetirementInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var role = SuspensionWire.ParseRole(input.ApprovedByRole);
        if (role is null || !SuspensionApprovers.IsPermitted(role.Value))
        {
            return Fail("Retirement must be approved by the GQD or an independent qualified QA delegate.", 409, SuspensionReasonCodes.ApproverRoleInvalid, correlationId);
        }

        var warnings = new List<string>();
        // Product decision: an unresolved suspension case does not BLOCK retirement (retiring may be the resolution),
        // but it is surfaced as a warning for the approver.
        var openSuspension = await _suspensionCases.GetOpenAsync(registerEntryId, ct);
        if (openSuspension is not null)
        {
            warnings.Add($"An unresolved suspension case (#{openSuspension.CaseNumber}, {openSuspension.CaseStatus}) exists for this document.");
        }

        var now = DateTimeOffset.UtcNow;
        c!.CaseStatus = RetirementCaseStatus.Approved;
        c.ApprovedBy = _currentUser.ActorName;
        c.ApprovedByRole = role.Value;
        c.ApprovedAt = now;
        c.UpdatedAt = now;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);
        return Response<RetirementCaseModel>.Success(SuspensionWire.ToRetirement(c, warnings), correlationId: correlationId);
    }

    public async Task<Response<RetirementCaseModel>> RejectAsync(Guid registerEntryId, Guid caseId, RejectRetirementInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400, SuspensionReasonCodes.ReasonRequired, correlationId);
        }

        c!.CaseStatus = RetirementCaseStatus.Rejected;
        c.RejectionReason = input.Reason.Trim();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);
        return Response<RetirementCaseModel>.Success(SuspensionWire.ToRetirement(c), correlationId: correlationId);
    }

    public async Task<Response<RetirementCaseModel>> ExecuteAsync(Guid registerEntryId, Guid caseId, ExecuteRetirementInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (c!.CaseStatus != RetirementCaseStatus.Approved)
        {
            return Fail("An approved retirement case is required before execution.", 409, SuspensionReasonCodes.CaseNotApproved, correlationId);
        }

        // SOP §9.16: communication + archival evidence are mandatory before execution.
        if (string.IsNullOrWhiteSpace(input.CommunicationEvidenceReference) || string.IsNullOrWhiteSpace(input.ArchivalEvidenceReference))
        {
            return Fail("Communication and archival evidence references are required to execute a retirement.", 400, SuspensionReasonCodes.EvidenceRequired, correlationId);
        }

        var transition = await _lifecycle.TransitionAsync(registerEntryId,
            new TransitionDocumentLifecycleInput(nameof(ControlledDocumentLifecycleStatus.Retired), c.RetirementReason,
                c.JustificationReference, "Retirement executed (MOD-0029-FU13).", null, null, null),
            correlationId, ct);
        if (!transition.IsSuccessful)
        {
            return Fail($"Lifecycle retirement failed: {string.Join("; ", transition.Errors)}", transition.StatusCode,
                SuspensionReasonCodes.LifecycleTransitionFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        c.CaseStatus = RetirementCaseStatus.Executed;
        c.CommunicationEvidenceReference = input.CommunicationEvidenceReference.Trim();
        c.ArchivalEvidenceReference = input.ArchivalEvidenceReference.Trim();
        c.ExecutedAt = now;
        c.ExecutedBy = _currentUser.ActorName;
        c.UpdatedAt = now;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);

        // MOD-0029-FU17 seam: raise a controlled-copy withdrawal plan for the now-retired document, when available.
        if (_copyWithdrawal is not null)
        {
            await _copyWithdrawal.OnDocumentWithdrawnAsync(entry!, ControlledDocumentLifecycleStatus.Retired, correlationId, ct);
        }

        return Response<RetirementCaseModel>.Success(SuspensionWire.ToRetirement(c), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<RetirementCaseModel>>> ListAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<RetirementCaseModel>>.Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _cases.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<RetirementCaseModel>>.Success(rows.Select(x => SuspensionWire.ToRetirement(x)).ToList(), correlationId: correlationId);
    }

    private async Task<(Response<RetirementCaseModel>? Fail, DocumentMasterRegisterEntry? Entry, DocumentRetirementCase? Case)> LoadAsync(
        Guid registerEntryId, Guid caseId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId), null, null);
        }

        var c = await _cases.GetByIdAsync(caseId, ct);
        if (c is null || c.RegisterEntryId != registerEntryId)
        {
            return (Fail("Retirement case not found.", 404, SuspensionReasonCodes.CaseNotFound, correlationId), null, null);
        }

        return (null, entry, c);
    }

    private static Response<RetirementCaseModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<RetirementCaseModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
