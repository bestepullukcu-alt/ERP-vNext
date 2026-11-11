using Diten.InventoryService.Domain.Expiry.Entities;

namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// What MOD-0022's effective policy says about <c>expiry.lot-date.override</c> for one override. <see cref="PolicyRead"/> false = MOD-0022
/// could not answer: the gate then fails closed. CT-F6: under <c>ReasonAndSignature</c> the signature is required REGARDLESS of
/// <see cref="Required"/> — the row only supplies the meaning (and the visual mode for the dialog).
/// </summary>
public sealed record LotDateOverrideSignatureRequirement(bool Required, string? MeaningCode, bool PolicyRead);

/// <summary>
/// KF-D signature of a lot-date override (MOD-0022 action <c>expiry.lot-date.override</c>, subject <c>expiry.date-override/{overrideId}</c>).
/// F3: <c>ESignLotDateOverrideSignature</c> (getSignature + the local <c>do-v1</c> hash, CT decision F3-A). 0176 stores only the
/// signature id (KF-E).
/// </summary>
public interface ILotDateOverrideSignature
{
    Task<LotDateOverrideSignatureRequirement> RequirementAsync(LotDateOverride dateOverride, CancellationToken cancellationToken = default);

    /// <summary>true only when MOD-0022's record binds <paramref name="signatureId"/> to <paramref name="signer"/> and exactly this override.</summary>
    Task<bool> VerifyAsync(string signatureId, LotDateOverride dateOverride, Guid signer, LotDateOverrideSignatureRequirement requirement,
        CancellationToken cancellationToken = default);
}

/// <summary>The F2 production default, kept as the fallback of a host without MOD-0022: nothing verifies, a signed override is refused.</summary>
public sealed class FailClosedLotDateOverrideSignature : ILotDateOverrideSignature
{
    public Task<LotDateOverrideSignatureRequirement> RequirementAsync(LotDateOverride dateOverride, CancellationToken cancellationToken = default)
        => Task.FromResult(new LotDateOverrideSignatureRequirement(true, null, false));

    public Task<bool> VerifyAsync(string signatureId, LotDateOverride dateOverride, Guid signer, LotDateOverrideSignatureRequirement requirement,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
