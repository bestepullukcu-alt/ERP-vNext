using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Services;

/// <summary>
/// MOD-0029-FU13 — urgent/temporary instruction 30-day validity control (GMG-QMS-SOP-0001 §6.1 class 7). A temporary
/// instruction is valid for a MAXIMUM of 30 calendar days and at expiry shall transition to EXACTLY ONE of: incorporated
/// / formally withdrawn / replaced under a NEW identifier / suspended because no valid replacement exists. An expired
/// temporary instruction SHALL NEVER remain operational by default — expiry without an action raises a suspension case.
/// Never hard-deleted.
/// </summary>
public sealed class TemporaryInstructionService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly ITemporaryInstructionControlRepository _controls;
    private readonly DocumentSuspensionService _suspension;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentWithdrawalOptions _options;

    public TemporaryInstructionService(
        IDocumentMasterRegisterRepository register,
        ITemporaryInstructionControlRepository controls,
        DocumentSuspensionService suspension,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<DocumentWithdrawalOptions> options)
    {
        _register = register;
        _controls = controls;
        _suspension = suspension;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _options = options.Value;
    }

    public async Task<Response<TemporaryInstructionModel>> StartAsync(Guid registerEntryId, StartTemporaryInstructionInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (!IsTemporary(entry))
        {
            return Fail("This document is not an urgent/temporary instruction (SOP §6.1 class 7).", 409, SuspensionReasonCodes.NotTemporaryInstruction, correlationId);
        }

        var validFrom = input.ValidFrom ?? DateTimeOffset.UtcNow;
        var ceiling = validFrom.AddDays(_options.TemporaryInstructionMaxValidityDays);
        if (input.ValidUntil <= validFrom || input.ValidUntil > ceiling)
        {
            return Fail($"A temporary instruction may be valid for at most {_options.TemporaryInstructionMaxValidityDays} calendar days.", 409, SuspensionReasonCodes.TemporaryValidityExceeded, correlationId);
        }

        var existing = await _controls.GetByRegisterEntryAsync(registerEntryId, ct);
        if (existing is not null)
        {
            return Response<TemporaryInstructionModel>.Success(SuspensionWire.ToTemporary(existing, DateTimeOffset.UtcNow), correlationId: correlationId);
        }

        var control = new TemporaryInstructionControl
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            TemporaryInstructionStatus = TemporaryInstructionStatus.Active,
            ValidFrom = validFrom,
            ValidUntil = input.ValidUntil,
            MaxValidityDays = _options.TemporaryInstructionMaxValidityDays,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _controls.CreateAsync(control, ct);
        return Response<TemporaryInstructionModel>.Success(SuspensionWire.ToTemporary(control, DateTimeOffset.UtcNow), 201, correlationId);
    }

    public async Task<Response<TemporaryInstructionModel>> EvaluateExpiryAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        var (fail, control) = await LoadAsync(registerEntryId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        control!.CheckedAt = now;

        if (control.TemporaryInstructionStatus is TemporaryInstructionStatus.Active or TemporaryInstructionStatus.DueToExpire)
        {
            if (now > control.ValidUntil)
            {
                // Expired with no expiry action → it must not remain operational: raise a suspension case (SOP §6.1).
                control.TemporaryInstructionStatus = TemporaryInstructionStatus.Expired;
                if (control.ExpiryAction is null)
                {
                    var raised = await _suspension.OpenInternalAsync(registerEntryId, SuspensionTriggerType.Other,
                        "Temporary instruction expired without an expiry action — it shall not remain operational by default (SOP §6.1 class 7).",
                        correlationId, ct);
                    control.SuspensionCaseId = raised.Id;
                    warnings.Add("The temporary instruction expired with no expiry action; a suspension case was raised.");
                }
            }
            else if (now >= control.ValidUntil.AddDays(-_options.DueToExpireWarningDays))
            {
                control.TemporaryInstructionStatus = TemporaryInstructionStatus.DueToExpire;
                warnings.Add($"The temporary instruction expires within {_options.DueToExpireWarningDays} days; an expiry action must be decided.");
            }
        }

        control.UpdatedAt = now;
        control.UpdatedBy = _currentUser.ActorName;
        await _controls.UpdateAsync(control, ct);
        return Response<TemporaryInstructionModel>.Success(SuspensionWire.ToTemporary(control, now, warnings), correlationId: correlationId);
    }

    public async Task<Response<TemporaryInstructionModel>> CloseAsync(Guid registerEntryId, CloseTemporaryInstructionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, control) = await LoadAsync(registerEntryId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        // Exactly ONE expiry action is required (SOP §6.1 class 7).
        var action = SuspensionWire.ParseExpiryAction(input.ExpiryAction);
        if (action is null)
        {
            return Fail("Exactly one expiry action is required: IncorporateIntoPermanent, FormallyWithdraw, ReplaceWithNewTemporary or SuspendNoReplacement.", 400, SuspensionReasonCodes.ExpiryActionRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();

        switch (action.Value)
        {
            case TemporaryInstructionExpiryAction.IncorporateIntoPermanent:
            case TemporaryInstructionExpiryAction.FormallyWithdraw:
                if (string.IsNullOrWhiteSpace(input.ExpiryActionEvidenceReference))
                {
                    return Fail("An expiry action evidence reference is required.", 400, SuspensionReasonCodes.EvidenceRequired, correlationId);
                }

                control!.TemporaryInstructionStatus = action.Value == TemporaryInstructionExpiryAction.IncorporateIntoPermanent
                    ? TemporaryInstructionStatus.Incorporated
                    : TemporaryInstructionStatus.Withdrawn;
                break;

            case TemporaryInstructionExpiryAction.ReplaceWithNewTemporary:
                // A replacement is a NEW temporary instruction under a NEW identifier (its own register entry / FU07 UID).
                if (input.ReplacementRegisterEntryId is not { } replacementId || replacementId == Guid.Empty)
                {
                    return Fail("A replacement register entry (new identifier) is required for ReplaceWithNewTemporary.", 400, SuspensionReasonCodes.ReplacementRequired, correlationId);
                }

                var replacement = await _register.GetByIdAsync(replacementId, ct);
                if (replacement is null)
                {
                    return Fail("The replacement register entry was not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
                }

                if (replacement.Id == registerEntryId)
                {
                    return Fail("The replacement must be a NEW register entry under a new identifier.", 409, SuspensionReasonCodes.ReplacementRequired, correlationId);
                }

                control!.ReplacementRegisterEntryId = replacementId;
                control.TemporaryInstructionStatus = TemporaryInstructionStatus.ReplacedByNewTemporary;
                break;

            case TemporaryInstructionExpiryAction.SuspendNoReplacement:
                var raised = await _suspension.OpenInternalAsync(registerEntryId, SuspensionTriggerType.Other,
                    "Temporary instruction expired with no valid replacement — suspension required (SOP §6.1 class 7).", correlationId, ct);
                control!.SuspensionCaseId = raised.Id;
                control.TemporaryInstructionStatus = TemporaryInstructionStatus.Suspended;
                warnings.Add("A suspension case was raised; execute it to move the document to Suspended.");
                break;
        }

        control!.ExpiryAction = action.Value;
        control.ExpiryActionEvidenceReference = TrimOrNull(input.ExpiryActionEvidenceReference);
        control.ClosedAt = now;
        control.ClosedBy = _currentUser.ActorName;
        control.UpdatedAt = now;
        control.UpdatedBy = _currentUser.ActorName;
        await _controls.UpdateAsync(control, ct);
        return Response<TemporaryInstructionModel>.Success(SuspensionWire.ToTemporary(control, now, warnings), correlationId: correlationId);
    }

    public async Task<Response<TemporaryInstructionModel>> GetAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        var (fail, control) = await LoadAsync(registerEntryId, correlationId, ct);
        return fail ?? Response<TemporaryInstructionModel>.Success(SuspensionWire.ToTemporary(control!, DateTimeOffset.UtcNow), correlationId: correlationId);
    }

    private static bool IsTemporary(DocumentMasterRegisterEntry entry) =>
        entry.DocumentClass == ControlledDocumentClass.UrgentTemporaryInstruction || entry.Criticality == DocumentCriticality.UrgentTemporary;

    private async Task<(Response<TemporaryInstructionModel>? Fail, TemporaryInstructionControl? Control)> LoadAsync(
        Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId), null);
        }

        var control = await _controls.GetByRegisterEntryAsync(registerEntryId, ct);
        if (control is null)
        {
            return (Fail("No temporary instruction control exists for this document.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId), null);
        }

        return (null, control);
    }

    private static Response<TemporaryInstructionModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<TemporaryInstructionModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
