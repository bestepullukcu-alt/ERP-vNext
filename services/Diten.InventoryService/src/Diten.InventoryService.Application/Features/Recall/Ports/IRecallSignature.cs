namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>What the MOD-0022 effective policy says about one signable action for this company.</summary>
public sealed record RecallSignatureRequirement(bool Required, string MeaningCode, string VisualMode);

/// <summary>
/// One signature to check against the prepared decision (pack §8.8 — signer, action, meaning, subject, after preparation). WP-0177-R4 (CT decision
/// F3-A): the subject HASH is not passed in — the verifier recomputes it from the stored record with the provider's own function.
/// </summary>
public sealed record RecallSignatureVerification(
    string SignatureId,
    string ActionCode,
    string MeaningCode,
    Guid SignerUserId,
    string SubjectType,
    Guid SubjectId,
    DateTimeOffset PreparedAtUtc);

/// <summary>
/// The e-signature gate of the signable recall decisions (pack §8.8: scope approval, effectiveness completion, closure; settings by policy).
/// R1 shipped the port and its FAIL-CLOSED default (<see cref="FailClosedRecallSignature"/>); R4 replaces it in production with the MOD-0022
/// implementation <c>ESignRecallSignature</c>. 0177 never stores a signature — only the id the caller sends.
/// </summary>
public interface IRecallSignature
{
    Task<RecallSignatureRequirement> RequirementAsync(string actionCode, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>true only when MOD-0022 confirms the signature belongs to exactly this prepared decision.</summary>
    Task<bool> VerifyAsync(RecallSignatureVerification verification, CancellationToken cancellationToken = default);
}

/// <summary>
/// WP-0177-R1 production default until R4 (0176 F1 pattern): the three case decisions require a signature and nothing can verify one yet,
/// so EVERY approval / completion / closure is refused — without a signature 400 <c>RECALL_SIGNATURE_REQUIRED</c>, with one 409
/// <c>RECALL_SIGNATURE_MISMATCH</c>. CT-R16: <c>recall.settings.update</c> is not required by default, so a settings save without a
/// signature passes; a sent signature cannot be verified and is refused (409). The state-machine tests run with a test double.
/// </summary>
public sealed class FailClosedRecallSignature : IRecallSignature
{
    public Task<RecallSignatureRequirement> RequirementAsync(string actionCode, Guid subjectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new RecallSignatureRequirement(
            !string.Equals(actionCode, RecallContract.SettingsUpdateAction, StringComparison.Ordinal),
            RecallContract.DefaultMeaningCode,
            RecallContract.DefaultVisualMode));

    public Task<bool> VerifyAsync(RecallSignatureVerification verification, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
