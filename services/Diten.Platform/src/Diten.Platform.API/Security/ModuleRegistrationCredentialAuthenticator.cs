using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Security;

public interface IModuleRegistrationCredentialAuthenticator
{
    ModuleRegistrationAuthenticationResult Authenticate(string? identifier, string? secret);
}

public sealed record ModuleRegistrationAuthenticationResult(bool IsAuthenticated, string? ProducerOwnerCode)
{
    public static ModuleRegistrationAuthenticationResult Rejected { get; } = new(false, null);
}

public sealed class ModuleRegistrationCredentialAuthenticator : IModuleRegistrationCredentialAuthenticator
{
    public const string MdmProducerOwnerCode = "DITENMDMSERVICE";

    private readonly ModuleRegistrationCredentialOptions _options;
    private readonly TimeProvider _timeProvider;

    public ModuleRegistrationCredentialAuthenticator(
        IOptions<ModuleRegistrationCredentialOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public ModuleRegistrationAuthenticationResult Authenticate(string? identifier, string? secret)
    {
        var credential = _options.Mdm;
        if (credential.IsRevoked
            || string.IsNullOrWhiteSpace(identifier)
            || string.IsNullOrEmpty(secret)
            || !string.Equals(identifier, credential.Identifier, StringComparison.Ordinal))
        {
            return ModuleRegistrationAuthenticationResult.Rejected;
        }

        if (FixedTimeEquals(secret, credential.ActiveSecret))
        {
            return new(true, MdmProducerOwnerCode);
        }

        var previousIsInOverlap = !string.IsNullOrEmpty(credential.PreviousSecret)
            && credential.PreviousValidUntilUtc.HasValue
            && _timeProvider.GetUtcNow() < credential.PreviousValidUntilUtc.Value;
        return previousIsInOverlap && FixedTimeEquals(secret, credential.PreviousSecret!)
            ? new(true, MdmProducerOwnerCode)
            : ModuleRegistrationAuthenticationResult.Rejected;
    }

    private static bool FixedTimeEquals(string provided, string expected)
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
