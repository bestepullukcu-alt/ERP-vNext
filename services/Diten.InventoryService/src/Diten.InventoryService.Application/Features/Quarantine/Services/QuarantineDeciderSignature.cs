using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Domain.Quarantine.Entities;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>The outcome of the decider-signature check: a refusal, or what the Submitted event records.</summary>
public sealed record DeciderSignatureCheck(Response<DecisionDto>? Refusal, bool Required, string? SignatureId);

/// <summary>
/// MOD-0175 Q3 — the decider's signature on submit (pack §8 "Decider" steps 3–5, V15, KQ-B), read with the CALLER's token:
/// <list type="number">
/// <item>The decision's signable action (strictest line: Release &gt; Reject &gt; Block) → MOD-0022 effective policy for this
/// decision's company (the subject resolves through 0175's own provider).</item>
/// <item>Policy not required and no <c>signatureId</c> → accepted (KQ-B "politika kapalı · bu adım atlanır").
/// Required and none → <b>400 QUARANTINE_SIGNATURE_REQUIRED</b>.</item>
/// <item>A <c>signatureId</c> is read (<c>GET /api/v1/esign/signatures/{id}</c>) and must bind: signer = the caller; subject =
/// <c>quarantine / quarantine.decision / this decision</c>; meaning = the policy's meaning; not invalidated; the signed subject
/// hash = this decision's current <c>qd-v1</c> hash (ASSUMPTION-0175-Q3-01: "verification valid" for the subject; signers
/// hold no <c>esign.signatures.verify</c> by default). Any miss → <b>409 QUARANTINE_SIGNATURE_MISMATCH</b>.</item>
/// </list>
/// MOD-0022 refuses the caller → 403; MOD-0022 cannot answer → 503 (nothing locked, nothing started). No bypass exists.
/// </summary>
public sealed class QuarantineDeciderSignature
{
    private readonly IQuarantineESignClient _esign;
    private readonly QuarantineSignableSubjectBuilder _subjects;
    private readonly QuarantineSupport _support;

    public QuarantineDeciderSignature(IQuarantineESignClient esign, QuarantineSignableSubjectBuilder subjects, QuarantineSupport support)
    {
        _esign = esign;
        _subjects = subjects;
        _support = support;
    }

    public async Task<DeciderSignatureCheck> CheckAsync(QuarantineDecision decision, string? signatureId, CancellationToken ct)
    {
        signatureId = string.IsNullOrWhiteSpace(signatureId) ? null : signatureId.Trim();
        var policy = await _esign.EffectivePolicyAsync(QuarantineMapping.EsignActionCode(decision), decision.Id, ct);
        if (!policy.IsOk)
        {
            return Refused(Unanswered<DecisionDto>(policy.Outcome, "the e-signature policy"));
        }

        if (signatureId is null)
        {
            return policy.Value!.Required
                ? Refused(QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.SignatureRequired,
                    "This decision needs the decider's e-signature (MOD-0022 policy).", 400))
                : new DeciderSignatureCheck(null, false, null);
        }

        var read = await _esign.SignatureAsync(signatureId, ct);
        if (read.Outcome == WorkflowCallOutcome.NotFound)
        {
            return Refused(Mismatch("The signature does not exist or is not visible to you."));
        }

        if (!read.IsOk)
        {
            return Refused(Unanswered<DecisionDto>(read.Outcome, "the signature"));
        }

        var signature = read.Value!;
        if (!Guid.TryParse(signature.SignerUserId, out var signer) || signer != _support.UserId)
        {
            return Refused(Mismatch("The signature was not made by you."));
        }

        if (!string.Equals(signature.Module, QuarantineSignableActions.Module, StringComparison.Ordinal)
            || !string.Equals(signature.SubjectType, QuarantineSignableActions.SubjectType, StringComparison.Ordinal)
            || !Guid.TryParse(signature.SubjectId, out var subjectId) || subjectId != decision.Id)
        {
            return Refused(Mismatch("The signature is not on this decision."));
        }

        if (policy.Value!.MeaningCode is { } meaning && !string.Equals(signature.MeaningCode, meaning, StringComparison.Ordinal))
        {
            return Refused(Mismatch($"The signature means {signature.MeaningCode}; this decision needs {meaning}."));
        }

        if (signature.Invalidated)
        {
            return Refused(Mismatch("The signature has been invalidated."));
        }

        var current = await _subjects.BuildAsync(decision, ct);
        if (!string.Equals(signature.SubjectHash, QuarantineSignableSubjectBuilder.Hash(decision.Id.ToString("D"), current.Fields), StringComparison.Ordinal))
        {
            return Refused(Mismatch("The decision is not the one that was signed (subject hash)."));
        }

        return new DeciderSignatureCheck(null, policy.Value.Required, signature.Id);
    }

    private static DeciderSignatureCheck Refused(Response<DecisionDto> refusal) => new(refusal, false, null);

    private static Response<DecisionDto> Mismatch(string message) => QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.SignatureMismatch, message, 409);

    private static Response<T> Unanswered<T>(WorkflowCallOutcome outcome, string what) => outcome switch
    {
        WorkflowCallOutcome.Forbidden => QuarantineSupport.Fail<T>(null!,
            $"MOD-0022 refused to show you {what} (esign.signatures.sign / esign.signatures.read).", 403),
        _ => QuarantineSupport.Fail<T>(QuarantineErrorCodes.WorkflowUnavailable,
            $"The e-signature service could not answer for {what}; nothing was submitted.", 503)
    };
}
