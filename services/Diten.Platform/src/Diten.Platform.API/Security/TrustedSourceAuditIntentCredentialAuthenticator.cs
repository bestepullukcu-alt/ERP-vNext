using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

public sealed class TrustedSourceAuditIntentCredentialAuthenticator : ITrustedSourceAuditIntentCredentialAuthenticator
{
    public const string ConsumerService = "Diten.MDM";
    public const string Audience = "TRUSTED_AUDIT_SOURCE_INGEST";

    private readonly TrustedSourceAuditIntentCredentialOptions _options;
    private readonly TimeProvider _timeProvider;

    public TrustedSourceAuditIntentCredentialAuthenticator(
        IOptions<TrustedSourceAuditIntentCredentialOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult Authenticate(
        string? identifier,
        string? secret,
        string? audience)
    {
        var configured = _options.Mdm;
        if (configured.IsRevoked
            || string.IsNullOrWhiteSpace(identifier)
            || string.IsNullOrEmpty(secret)
            || !string.Equals(identifier, configured.Identifier, StringComparison.Ordinal))
        {
            return ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult.Unauthenticated;
        }

        if (!string.Equals(configured.ConsumerService, ConsumerService, StringComparison.Ordinal)
            || !string.Equals(configured.AllowedAudience, Audience, StringComparison.Ordinal)
            || !string.Equals(audience, Audience, StringComparison.Ordinal)
            || !TryParseExactTenantGrants(configured.AllowedTenantIds, out var grants))
        {
            return ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult.Forbidden;
        }

        var validSecret = FixedTimeEquals(secret, configured.ActiveSecret);
        if (!validSecret
            && !string.IsNullOrEmpty(configured.PreviousSecret)
            && configured.PreviousValidUntilUtc.HasValue
            && _timeProvider.GetUtcNow() < configured.PreviousValidUntilUtc.Value)
        {
            validSecret = FixedTimeEquals(secret, configured.PreviousSecret);
        }

        return validSecret
            ? new(true, false, grants)
            : ITrustedSourceAuditIntentCredentialAuthenticator.AuthenticationResult.Unauthenticated;
    }

    private static bool TryParseExactTenantGrants(IReadOnlyCollection<string>? configured, out IReadOnlySet<Guid> grants)
    {
        var parsed = new HashSet<Guid>();
        if (configured is null || configured.Count == 0)
        {
            grants = parsed;
            return false;
        }

        foreach (var value in configured)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Contains('*', StringComparison.Ordinal)
                || !Guid.TryParseExact(value, "D", out var tenantId)
                || tenantId == Guid.Empty
                || !parsed.Add(tenantId))
            {
                grants = new HashSet<Guid>();
                return false;
            }
        }

        grants = parsed;
        return true;
    }

    private static bool FixedTimeEquals(string provided, string? expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
