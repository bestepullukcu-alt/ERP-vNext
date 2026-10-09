using System.Security.Cryptography;
using Diten.AuthService.Infrastructure.Services;

namespace Diten.AuthService.ServiceClientCredentialGenerator;

/// <summary>
/// MOD-0033-FU02 §F.2 — makes a service-client credential pair. The secret is generated HERE (32 random bytes); no
/// overload takes a secret from the caller. The hash comes from the production hasher.
/// </summary>
public static class ServiceClientCredentialFactory
{
    public const int SecretBytes = 32;

    /// <summary>
    /// Generates a secret, hands it to <paramref name="deliverRawSecret"/> exactly once, and returns the hash.
    /// The raw secret exists only inside that callback; the buffer is cleared before this method returns.
    /// </summary>
    public static string Create(RawSecretSink deliverRawSecret)
    {
        ArgumentNullException.ThrowIfNull(deliverRawSecret);
        var random = RandomNumberGenerator.GetBytes(SecretBytes);
        var secret = new char[Base64UrlLength(SecretBytes)];
        try
        {
            EncodeBase64Url(random, secret);
            var hash = new ServiceClientCredentialVerifier().Hash(new string(secret));
            deliverRawSecret(secret);
            return hash;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
            Array.Clear(secret);
        }
    }

    public delegate void RawSecretSink(ReadOnlySpan<char> rawSecret);

    private static int Base64UrlLength(int byteCount) => (byteCount * 4 + 2) / 3;

    private static void EncodeBase64Url(ReadOnlySpan<byte> source, Span<char> destination)
    {
        Span<char> padded = stackalloc char[((source.Length + 2) / 3) * 4];
        if (!Convert.TryToBase64Chars(source, padded, out var written))
            throw new InvalidOperationException("Secret encoding failed.");
        var length = 0;
        for (var index = 0; index < written; index++)
        {
            var character = padded[index];
            if (character == '=') break;
            destination[length++] = character switch { '+' => '-', '/' => '_', _ => character };
        }
        padded.Clear();
        if (length != destination.Length) throw new InvalidOperationException("Secret encoding failed.");
    }
}
