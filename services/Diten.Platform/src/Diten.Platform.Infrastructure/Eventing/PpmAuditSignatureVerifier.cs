using System.Security.Cryptography;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Eventing;

internal sealed class PpmAuditSignatureVerifier
{
    internal const string SchemeHeader = "X-Diten-Event-Signature-Scheme";
    internal const string KeyIdHeader = "X-Diten-Event-Key-Id";
    internal const string SignatureHeader = "X-Diten-Event-Signature";

    private readonly PpmAuditConsumerOptions _options;

    public PpmAuditSignatureVerifier(IOptions<PpmAuditConsumerOptions> options)
    {
        _options = options.Value;
    }

    public void Verify(
        EventTransportMessage message,
        PpmAuditIntent intent,
        string? scheme,
        string? keyId,
        string? signature)
    {
        if (!_options.Enabled)
        {
            throw new PpmAuditSecurityException("PPM audit consumer is disabled.");
        }

        if (!string.Equals(scheme, PpmAuditIntentParser.SignatureScheme, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(keyId)
            || string.IsNullOrWhiteSpace(signature)
            || signature.Length != 64
            || signature.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new PpmAuditSecurityException("PPM audit publisher signature headers are invalid.");
        }

        var encodedSecret = ResolveSecret(keyId);
        if (encodedSecret is null
            || !PpmAuditConsumerOptionsValidator.TryDecodeSecret(encodedSecret, out var secret))
        {
            throw new PpmAuditSecurityException("PPM audit publisher key is not trusted.");
        }

        byte[] suppliedSignature;
        try
        {
            suppliedSignature = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            throw new PpmAuditSecurityException("PPM audit publisher signature format is invalid.");
        }

        var signingInput = PpmAuditIntentParser.BuildSigningInput(message, intent.CanonicalPayload);
        var expectedSignature = HMACSHA256.HashData(secret, signingInput);
        if (suppliedSignature.Length != 32
            || !CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature))
        {
            throw new PpmAuditSecurityException("PPM audit publisher signature is invalid.");
        }
    }

    private string? ResolveSecret(string keyId)
    {
        if (string.Equals(keyId, _options.ActiveKeyId, StringComparison.Ordinal))
        {
            return _options.ActiveSecret;
        }

        return string.Equals(keyId, _options.PreviousKeyId, StringComparison.Ordinal)
            ? _options.PreviousSecret
            : null;
    }
}
