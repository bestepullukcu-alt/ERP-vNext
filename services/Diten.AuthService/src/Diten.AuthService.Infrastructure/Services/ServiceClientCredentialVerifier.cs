using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class ServiceClientCredentialVerifier : IServiceClientCredentialVerifier
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly string DummyHash = Encode(
        "service-identity-dummy",
        Convert.FromHexString("00112233445566778899AABBCCDDEEFF"));

    public bool Verify(ServiceClientIdentity identity, string presentedSecret, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(presentedSecret)) return false;
        var activeMatch = Matches(identity.ActiveCredentialHash, presentedSecret);
        var previousMatch = Matches(identity.PreviousCredentialHash ?? DummyHash, presentedSecret);
        var previousAllowed = Parse(identity.PreviousCredentialHash) is not null
            && IsExactVersion(identity.PreviousCredentialVersion)
            && identity.PreviousValidUntilUtc is { } until
            && nowUtc < until;
        return !identity.IsRevoked & (activeMatch | (previousMatch & previousAllowed));
    }

    public bool IsStateCoherent(ServiceClientIdentity identity)
    {
        var activeCoherent = Parse(identity.ActiveCredentialHash) is not null
            && IsExactVersion(identity.ActiveCredentialVersion);
        var previousAbsent = identity.PreviousCredentialHash is null
            && identity.PreviousCredentialVersion is null
            && identity.PreviousValidUntilUtc is null;
        var previousCoherent = Parse(identity.PreviousCredentialHash) is not null
            && IsExactVersion(identity.PreviousCredentialVersion)
            && identity.PreviousValidUntilUtc is not null;
        return activeCoherent && (previousAbsent || previousCoherent);
    }

    public void PerformUnknownClientWork(string presentedSecret)
    {
        _ = Matches(DummyHash, presentedSecret);
        _ = Matches(DummyHash, presentedSecret);
    }

    public string Hash(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("Secret is required.", nameof(secret));
        return Encode(secret, RandomNumberGenerator.GetBytes(SaltSize));
    }

    private static bool Matches(string? encoded, string presentedSecret)
    {
        var parsed = Parse(encoded);
        var candidate = parsed ?? Parse(DummyHash)!;
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(presentedSecret), candidate.Salt, candidate.Iterations,
            HashAlgorithmName.SHA256, candidate.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(candidate.Hash, derived)
            && parsed is not null;
    }

    private static string Encode(string secret, byte[] salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret), salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static ParsedHash? Parse(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return null;
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations)) return null;
            var salt = Convert.FromBase64String(parts[2]);
            var hash = Convert.FromBase64String(parts[3]);
            if (iterations != Iterations || salt.Length != SaltSize || hash.Length != KeySize) return null;
            return new ParsedHash(salt, hash, iterations);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return null;
        }
    }

    private static bool IsExactVersion(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private sealed record ParsedHash(byte[] Salt, byte[] Hash, int Iterations);
}
