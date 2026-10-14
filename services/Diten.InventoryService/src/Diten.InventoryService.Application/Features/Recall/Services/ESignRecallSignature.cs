using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// CT decision F3-A (the MOD-0176 F3 <c>ExpirySignatureBinding</c>, copied) — the binding rules of a MOD-0022 signature to one 0177 subject, read
/// with <c>getSignature</c> (<c>esign.openapi.yaml:654-684</c>, the caller's token: another tenant's signature is 404 there) and compared with
/// the CURRENT subject hash computed by <see cref="RecallSignableSubjects"/> (the provider's own code). <c>verifySignature</c> is never called.
/// Every rule is required: signer = the acting user; action code; meaning = the effective policy's; subject = {recall, type, id}; not
/// invalidated; signed at or after the preparation; signed hash = current hash; the record has a company. The reuse rule ("never used before")
/// is <see cref="ESignRecallSignature"/>'s (it needs the records).
/// </summary>
public static class RecallSignatureBinding
{
    public static string? Refusal(
        RecallESignSignature s, Guid signer, string actionCode, string? meaningCode, string subjectType, Guid subjectId, Guid? legalEntityId,
        DateTimeOffset preparedAtUtc, string currentHash)
    {
        if (!Guid.TryParse(s.SignerUserId, out var signedBy) || signedBy != signer) return "signer";
        if (!string.Equals(s.ActionCode, actionCode, StringComparison.Ordinal)) return "action";
        if (meaningCode is null || !string.Equals(s.MeaningCode, meaningCode, StringComparison.Ordinal)) return "meaning";
        if (!string.Equals(s.Module, RecallSignableActions.Module, StringComparison.Ordinal)
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
/// WP-0177-R4 — <see cref="IRecallSignature"/> over MOD-0022 (replaces R1's <see cref="FailClosedRecallSignature"/> in production). The
/// requirement is the effective policy of the action for its subject; unreadable → required with the default meaning (fail-closed: the
/// dialog still opens, <see cref="VerifyAsync"/> then refuses). Verification re-reads the policy, the signature and the subject record (never
/// trusts the caller's view), recomputes the subject hash with the provider's function and refuses a signature already used by a case
/// decision or a settings save of this company (unique per case + action, and per settings change).
/// </summary>
public sealed class ESignRecallSignature : IRecallSignature
{
    private readonly IRecallESignClient _esign;
    private readonly IRecallRepository _repository;
    private readonly IRecallDrillRepository _drills;
    private readonly RecallSubjectFacts _facts;
    private readonly RecallSupport _support;
    private readonly ILogger<ESignRecallSignature> _logger;

    public ESignRecallSignature(IRecallESignClient esign, IRecallRepository repository, IRecallDrillRepository drills, RecallSubjectFacts facts, RecallSupport support,
        ILogger<ESignRecallSignature> logger)
    {
        _esign = esign;
        _repository = repository;
        _drills = drills;
        _facts = facts;
        _support = support;
        _logger = logger;
    }

    public async Task<RecallSignatureRequirement> RequirementAsync(string actionCode, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var subjectType = RecallSignableActions.SubjectTypeOf(actionCode);
        var policy = subjectType is null ? null : await _esign.EffectivePolicyAsync(actionCode, subjectType, subjectId.ToString("D"), cancellationToken);
        if (policy is not { IsOk: true })
        {
            _logger.LogWarning("recall.esign.policy_unavailable Action={Action} Outcome={Outcome} — fail-closed: required", actionCode, policy?.Outcome);
            return new RecallSignatureRequirement(true, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode);
        }

        var p = policy.Value!;
        return new RecallSignatureRequirement(p.Required, p.MeaningCode ?? RecallContract.DefaultMeaningCode, p.VisualMode ?? RecallContract.DefaultVisualMode);
    }

    public async Task<bool> VerifyAsync(RecallSignatureVerification verification, CancellationToken cancellationToken = default)
    {
        var policy = await _esign.EffectivePolicyAsync(verification.ActionCode, verification.SubjectType, verification.SubjectId.ToString("D"), cancellationToken);
        var signature = await _esign.SignatureAsync(verification.SignatureId, cancellationToken);
        if (!policy.IsOk || !signature.IsOk)
        {
            _logger.LogWarning("recall.esign.verify_refused Reason=unavailable Policy={Policy} Signature={Signature}", policy.Outcome, signature.Outcome);
            return false;
        }

        var current = await CurrentAsync(verification.SubjectType, verification.SubjectId, cancellationToken);
        if (current is not { } subject)
        {
            _logger.LogWarning("recall.esign.verify_refused Reason=subjectMissing Subject={Subject}", verification.SubjectId);
            return false;
        }

        var refusal = RecallSignatureBinding.Refusal(signature.Value!, verification.SignerUserId, verification.ActionCode,
            policy.Value!.MeaningCode ?? RecallContract.DefaultMeaningCode, verification.SubjectType, verification.SubjectId, subject.LegalEntityId,
            verification.PreparedAtUtc, subject.Hash);
        if (refusal is null && await _repository.SignatureUsedAsync(verification.SignatureId, cancellationToken))
        {
            refusal = "used";
        }

        if (refusal is not null)
        {
            _logger.LogWarning("recall.esign.verify_refused Reason={Reason} Subject={Subject}", refusal, verification.SubjectId);
        }

        return refusal is null;
    }

    /// <summary>The subject as it is NOW (company + hash), from the provider's own functions; null = no such subject of this company.</summary>
    private async Task<(Guid? LegalEntityId, string Hash)?> CurrentAsync(string subjectType, Guid subjectId, CancellationToken ct)
    {
        if (subjectType == RecallSignableActions.CaseSubjectType)
        {
            var c = await _repository.FindCaseAsync(subjectId, ct);
            return c is null ? null : (c.LegalEntityId, RecallSignableSubjects.CaseHash(c, await _facts.FactsAsync(c, _support.Now(), ct)));
        }

        if (subjectType == RecallSignableActions.SettingsChangeSubjectType)
        {
            var change = await _repository.FindSettingsChangeAsync(subjectId, ct);
            return change is null ? null : (change.LegalEntityId, RecallSignableSubjects.SettingsChangeHash(change));
        }

        if (subjectType == RecallSignableActions.DrillRunSubjectType)
        {
            var run = await _drills.FindRunAsync(subjectId, ct);
            return run is null ? null : (run.LegalEntityId, RecallSignableSubjects.DrillRunHash(run)); // R5: dr-v1 of the stored run
        }

        return null;
    }
}
