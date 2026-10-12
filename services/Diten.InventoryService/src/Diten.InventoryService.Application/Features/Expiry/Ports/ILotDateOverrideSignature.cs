namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// KF-D signature of a lot-date override (MOD-0022 action <c>expiry.lot-date.override</c>, subject <c>expiry.date-override/{overrideId}</c>).
/// F2 ships the FAIL-CLOSED production default (<see cref="FailClosedLotDateOverrideSignature"/>); F3 brings the MOD-0022 verification. 0176
/// stores only the signature id (KF-E).
/// </summary>
public interface ILotDateOverrideSignature
{
    /// <summary>true only when MOD-0022 confirms <paramref name="signatureId"/> is <paramref name="signer"/>'s signature of this override.</summary>
    Task<bool> VerifyAsync(string signatureId, Guid overrideId, Guid signer, CancellationToken cancellationToken = default);
}

/// <summary>
/// Until F3 nothing verifies a signature, so a signed override is refused (409 <c>LOT_DATE_OVERRIDE_SIGNATURE_MISMATCH</c>; unsigned in mode
/// ReasonAndSignature: 400). CT-F6 (WP-0176-F2): between F2 and F3 a lot with dates apart from the suggestion is created only in mode Reason.
/// </summary>
public sealed class FailClosedLotDateOverrideSignature : ILotDateOverrideSignature
{
    public Task<bool> VerifyAsync(string signatureId, Guid overrideId, Guid signer, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
