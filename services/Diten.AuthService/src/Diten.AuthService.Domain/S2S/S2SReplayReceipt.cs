using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.S2S;

public sealed class S2SReplayReceipt : GlobalEntityBase
{
    public static readonly TimeSpan MinimumRetentionSkew = TimeSpan.FromSeconds(30);

    private S2SReplayReceipt()
    {
    }

    public S2SReplayReceipt(string issuer, string jti, string nonce, string requestHash, DateTimeOffset expiresAtUtc, DateTimeOffset acceptedAtUtc)
    {
        Issuer = S2SExactValue.RequiredLowercase(issuer, nameof(issuer));
        Jti = S2SExactValue.Required(jti, nameof(jti));
        Nonce = S2SExactValue.Required(nonce, nameof(nonce));
        RequestHash = S2SExactValue.Required(requestHash, nameof(requestHash));
        if (expiresAtUtc <= acceptedAtUtc) throw new S2SContractException("Replay receipt expiry must be in the future.", nameof(expiresAtUtc));
        ExpiresAtUtc = expiresAtUtc;
        RetainUntilUtc = expiresAtUtc.Add(MinimumRetentionSkew);
        AcceptedAtUtc = acceptedAtUtc;
        CreatedAt = acceptedAtUtc;
        CreatedBy = "s2s-replay-authority";
    }

    public string Issuer { get; private init; } = string.Empty;
    public string Jti { get; private init; } = string.Empty;
    public string Nonce { get; private init; } = string.Empty;
    public string RequestHash { get; private init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; private init; }
    public DateTimeOffset RetainUntilUtc { get; private init; }
    public DateTimeOffset AcceptedAtUtc { get; private init; }
}
