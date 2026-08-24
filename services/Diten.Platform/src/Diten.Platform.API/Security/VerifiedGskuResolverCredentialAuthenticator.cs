using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

public interface IVerifiedGskuResolverCredentialAuthenticator
{
    VerifiedGskuResolverCredentialAuthenticationResult Authenticate(
        string? identifier,
        string? secret,
        string? audience);
}

public sealed record VerifiedGskuResolverCredentialAuthenticationResult(
    bool IsAuthenticated,
    bool IsForbidden,
    string? ConsumerService,
    string? AllowedAudience)
{
    public static VerifiedGskuResolverCredentialAuthenticationResult Unauthenticated { get; } =
        new(false, false, null, null);

    public static VerifiedGskuResolverCredentialAuthenticationResult Forbidden { get; } =
        new(false, true, null, null);
}

public sealed class VerifiedGskuResolverCredentialAuthenticator : IVerifiedGskuResolverCredentialAuthenticator
{
    public const string ConsumerService = "DITENMDMSERVICE";
    public const string Audience = "VERIFIED_GSKU_RESOLVE";

    private readonly VerifiedGskuResolverCredentialOptions _options;
    private readonly TimeProvider _timeProvider;

    public VerifiedGskuResolverCredentialAuthenticator(
        IOptions<VerifiedGskuResolverCredentialOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public VerifiedGskuResolverCredentialAuthenticationResult Authenticate(
        string? identifier,
        string? secret,
        string? audience)
    {
        var credential = _options.Mdm;
        if (credential.IsRevoked
            || string.IsNullOrWhiteSpace(identifier)
            || string.IsNullOrEmpty(secret)
            || !string.Equals(identifier, credential.Identifier, StringComparison.Ordinal))
        {
            return VerifiedGskuResolverCredentialAuthenticationResult.Unauthenticated;
        }

        if (!string.Equals(credential.ConsumerService, ConsumerService, StringComparison.Ordinal)
            || !string.Equals(credential.AllowedAudience, Audience, StringComparison.Ordinal)
            || !string.Equals(audience, Audience, StringComparison.Ordinal))
        {
            return VerifiedGskuResolverCredentialAuthenticationResult.Forbidden;
        }

        var validSecret = FixedTimeEquals(secret, credential.ActiveSecret);
        if (!validSecret
            && !string.IsNullOrEmpty(credential.PreviousSecret)
            && credential.PreviousValidUntilUtc.HasValue
            && _timeProvider.GetUtcNow() < credential.PreviousValidUntilUtc.Value)
        {
            validSecret = FixedTimeEquals(secret, credential.PreviousSecret);
        }

        return validSecret
            ? new(true, false, ConsumerService, Audience)
            : VerifiedGskuResolverCredentialAuthenticationResult.Unauthenticated;
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
