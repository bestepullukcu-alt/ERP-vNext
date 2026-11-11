using Diten.InventoryService.Domain.Expiry.Enums;

namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>What the MOD-0022 effective policy says about one signable action for this company (KF-E: by policy).</summary>
public sealed record ExpirySignatureRequirement(bool Required, string MeaningCode, string VisualMode);

/// <summary>One signature to check against the prepared decision (pack §8.6 — signer, action, meaning, subject, after preparation).</summary>
public sealed record ExpirySignatureVerification(
    string SignatureId,
    string ActionCode,
    string MeaningCode,
    Guid SignerUserId,
    Guid ExceptionId,
    ExpiryResolution Resolution,
    string Reason,
    DateTimeOffset PreparedAtUtc);

/// <summary>
/// The e-signature gate of an exception resolution (KF-E). F1 ships the port and its FAIL-CLOSED production default
/// (<see cref="FailClosedExpiryResolutionSignature"/>); F3 replaces it with the MOD-0022 implementation (effective policy +
/// <c>verifySignature</c>). 0176 never stores a signature — only the id the caller sends.
/// </summary>
public interface IExpiryResolutionSignature
{
    Task<ExpirySignatureRequirement> RequirementAsync(string actionCode, Guid exceptionId, CancellationToken cancellationToken = default);

    /// <summary>true only when MOD-0022 confirms the signature belongs to exactly this prepared decision.</summary>
    Task<bool> VerifyAsync(ExpirySignatureVerification verification, CancellationToken cancellationToken = default);
}

/// <summary>
/// WP-0176-F1 production default until F3 (CT §4 of the WP): KF-E's default policy requires a signature on all three
/// resolutions, and nothing can verify one yet. So EVERY resolution is refused — without a signature 400
/// <c>EXPIRY_SIGNATURE_REQUIRED</c>, with one 409 <c>EXPIRY_SIGNATURE_MISMATCH</c>. No exception is resolved unsigned or with an
/// unverified signature in production. The state-machine tests run with a test double.
/// </summary>
public sealed class FailClosedExpiryResolutionSignature : IExpiryResolutionSignature
{
    public Task<ExpirySignatureRequirement> RequirementAsync(string actionCode, Guid exceptionId, CancellationToken cancellationToken = default)
        => Task.FromResult(new ExpirySignatureRequirement(true, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode));

    public Task<bool> VerifyAsync(ExpirySignatureVerification verification, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
