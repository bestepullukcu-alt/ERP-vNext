using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Expiry.Services;

/// <summary>
/// CT decision F3-A — the binding rules of a MOD-0022 signature to one 0176 subject, read with <c>getSignature</c>
/// (<c>esign.openapi.yaml:654-684</c>, the caller's token) and compared with the CURRENT subject hash computed by
/// <see cref="ExpirySignableSubjects"/> (the provider's own code). <c>verifySignature</c> is never called. Every rule is required:
/// signer = the acting user; action code; meaning = the effective policy's; subject = {expiry, type, id}; not invalidated; signed at or
/// after the preparation; signed hash = current hash; same company. MOD-0022 unreadable → refused (fail-closed).
/// </summary>
public static class ExpirySignatureBinding
{
    public static string? Refusal(
        ExpiryESignSignature s, Guid signer, string actionCode, string? meaningCode, string subjectType, Guid subjectId, Guid? legalEntityId,
        DateTimeOffset preparedAtUtc, string currentHash)
    {
        if (!Guid.TryParse(s.SignerUserId, out var signedBy) || signedBy != signer) return "signer";
        if (!string.Equals(s.ActionCode, actionCode, StringComparison.Ordinal)) return "action";
        if (meaningCode is null || !string.Equals(s.MeaningCode, meaningCode, StringComparison.Ordinal)) return "meaning";
        if (!string.Equals(s.Module, ExpirySignableActions.Module, StringComparison.Ordinal)
            || !string.Equals(s.SubjectType, subjectType, StringComparison.Ordinal)
            || !Guid.TryParse(s.SubjectId, out var signedSubject) || signedSubject != subjectId)
        {
            return "subject";
        }

        if (s.Invalidated) return "invalidated";
        if (s.SignedAtUtc is not { } signedAt || signedAt < preparedAtUtc) return "signedBeforePreparation";
        if (!string.Equals(s.SubjectHash, currentHash, StringComparison.Ordinal)) return "subjectHash";
        return legalEntityId is null ? "company" : null;
    }
}

/// <summary>
/// F3 — <see cref="IExpiryResolutionSignature"/> over MOD-0022 (replaces F1's fail-closed default in production). The requirement is the
/// effective policy of the resolution's action for this case; unreadable → required with the default meaning (the dialog still opens,
/// <see cref="VerifyAsync"/> then refuses). Verification re-reads the policy and the case (never trusts the caller's view).
/// </summary>
public sealed class ESignExpiryResolutionSignature : IExpiryResolutionSignature
{
    private readonly IExpiryESignClient _esign;
    private readonly IExpiryRepository _repository;
    private readonly ILogger<ESignExpiryResolutionSignature> _logger;

    public ESignExpiryResolutionSignature(IExpiryESignClient esign, IExpiryRepository repository, ILogger<ESignExpiryResolutionSignature> logger)
    {
        _esign = esign;
        _repository = repository;
        _logger = logger;
    }

    public async Task<ExpirySignatureRequirement> RequirementAsync(string actionCode, Guid exceptionId, CancellationToken cancellationToken = default)
    {
        var policy = await _esign.EffectivePolicyAsync(actionCode, ExpirySignableActions.ExceptionSubjectType, exceptionId.ToString("D"), cancellationToken);
        if (!policy.IsOk)
        {
            _logger.LogWarning("expiry.esign.policy_unavailable Action={Action} Outcome={Outcome} — fail-closed: required", actionCode, policy.Outcome);
            return new ExpirySignatureRequirement(true, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode);
        }

        var p = policy.Value!;
        return new ExpirySignatureRequirement(p.Required, p.MeaningCode ?? ExpiryContract.DefaultMeaningCode, p.VisualMode ?? ExpiryContract.DefaultVisualMode);
    }

    public async Task<bool> VerifyAsync(ExpirySignatureVerification verification, CancellationToken cancellationToken = default)
    {
        var policy = await _esign.EffectivePolicyAsync(verification.ActionCode, ExpirySignableActions.ExceptionSubjectType,
            verification.ExceptionId.ToString("D"), cancellationToken);
        var signature = await _esign.SignatureAsync(verification.SignatureId, cancellationToken);
        var current = await _repository.FindCaseAsync(verification.ExceptionId, cancellationToken);
        if (!policy.IsOk || !signature.IsOk || current is null)
        {
            _logger.LogWarning("expiry.esign.verify_refused Reason=unavailable Policy={Policy} Signature={Signature}", policy.Outcome, signature.Outcome);
            return false;
        }

        var refusal = ExpirySignatureBinding.Refusal(signature.Value!, verification.SignerUserId, verification.ActionCode,
            policy.Value!.MeaningCode ?? ExpiryContract.DefaultMeaningCode, ExpirySignableActions.ExceptionSubjectType, verification.ExceptionId,
            current.LegalEntityId, verification.PreparedAtUtc, ExpirySignableSubjects.ExceptionHash(current));
        if (refusal is not null)
        {
            _logger.LogWarning("expiry.esign.verify_refused Reason={Reason} Exception={Exception}", refusal, verification.ExceptionId);
        }

        return refusal is null;
    }
}

/// <summary>
/// F3 — <see cref="ILotDateOverrideSignature"/> over MOD-0022 (KF-D, CT-F6). The policy row gives the meaning; the gate requires the
/// signature under <c>ReasonAndSignature</c> whatever the row's <c>required</c> says. "Prepared" = the override's creation.
/// </summary>
public sealed class ESignLotDateOverrideSignature : ILotDateOverrideSignature
{
    private readonly IExpiryESignClient _esign;
    private readonly ILogger<ESignLotDateOverrideSignature> _logger;

    public ESignLotDateOverrideSignature(IExpiryESignClient esign, ILogger<ESignLotDateOverrideSignature> logger)
    {
        _esign = esign;
        _logger = logger;
    }

    public async Task<LotDateOverrideSignatureRequirement> RequirementAsync(LotDateOverride dateOverride, CancellationToken cancellationToken = default)
    {
        var policy = await _esign.EffectivePolicyAsync(ExpirySignableActions.LotDateOverride, ExpirySignableActions.DateOverrideSubjectType,
            dateOverride.Id.ToString("D"), cancellationToken);
        if (!policy.IsOk)
        {
            _logger.LogWarning("expiry.esign.policy_unavailable Action={Action} Outcome={Outcome} — fail-closed", ExpirySignableActions.LotDateOverride, policy.Outcome);
            return new LotDateOverrideSignatureRequirement(true, null, false);
        }

        return new LotDateOverrideSignatureRequirement(policy.Value!.Required, policy.Value.MeaningCode ?? ExpiryContract.DefaultMeaningCode, true);
    }

    public async Task<bool> VerifyAsync(string signatureId, LotDateOverride dateOverride, Guid signer, LotDateOverrideSignatureRequirement requirement,
        CancellationToken cancellationToken = default)
    {
        if (!requirement.PolicyRead) return false;
        var signature = await _esign.SignatureAsync(signatureId, cancellationToken);
        if (!signature.IsOk)
        {
            _logger.LogWarning("expiry.esign.verify_refused Reason=unavailable Signature={Signature}", signature.Outcome);
            return false;
        }

        var refusal = ExpirySignatureBinding.Refusal(signature.Value!, signer, ExpirySignableActions.LotDateOverride, requirement.MeaningCode,
            ExpirySignableActions.DateOverrideSubjectType, dateOverride.Id, dateOverride.LegalEntityId, dateOverride.CreatedAt,
            ExpirySignableSubjects.DateOverrideHash(dateOverride));
        if (refusal is not null)
        {
            _logger.LogWarning("expiry.esign.verify_refused Reason={Reason} Override={Override}", refusal, dateOverride.Id);
        }

        return refusal is null;
    }
}
