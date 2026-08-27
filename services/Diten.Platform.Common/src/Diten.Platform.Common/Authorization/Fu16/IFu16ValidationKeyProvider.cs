using Microsoft.IdentityModel.Tokens;

namespace Diten.Platform.Common.Authorization.Fu16;

public enum Fu16ValidationKeyDisposition { Found, Unknown, Revoked, Expired, Unavailable }

public sealed record Fu16ValidationKeyResult(
    Fu16ValidationKeyDisposition Disposition,
    SecurityKey? Key,
    DateTimeOffset? NotBeforeUtc = null,
    DateTimeOffset? ExpiresAtUtc = null);

public interface IFu16ValidationKeyProvider
{
    Task<Fu16ValidationKeyResult> ResolveAsync(string issuer, string kid, CancellationToken cancellationToken);
}
