using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Diten.BuildingBlocks.Eventing;

public sealed class EventSigningInputBuilder : IEventSigningInputBuilder
{
    private const string MissingValue = "-";

    public byte[] Build(EventTransportMessage message, string scheme)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrEmpty(scheme) || !string.Equals(scheme, scheme.Trim(), StringComparison.Ordinal))
        {
            throw new EventContractException("Signature scheme is invalid.", "event.scheme.invalid");
        }

        var payload = message.CanonicalPayloadUtf8.Span;
        var prefix = string.Join('\n',
            scheme,
            message.EventId.ToString("D"),
            message.EventName,
            message.EventVersion.ToString(CultureInfo.InvariantCulture),
            message.TenantId?.ToString("D") ?? MissingValue,
            message.CorrelationId.ToString("D"),
            message.Producer,
            message.CausationId?.ToString("D") ?? MissingValue,
            message.OccurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            payload.Length.ToString(CultureInfo.InvariantCulture)) + "\n";
        var prefixBytes = Encoding.UTF8.GetBytes(prefix);
        var result = new byte[prefixBytes.Length + payload.Length];
        prefixBytes.CopyTo(result, 0);
        payload.CopyTo(result.AsSpan(prefixBytes.Length));
        return result;
    }
}

public sealed class HmacSha256EventSignatureVerifier : IEventSignatureVerifier
{
    public static readonly TimeSpan MaximumEventAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan AllowedFutureSkew = TimeSpan.FromMinutes(2);

    private readonly IEventVerificationKeyProvider _keyProvider;
    private readonly IEventSigningInputBuilder _inputBuilder;
    private readonly TimeProvider _timeProvider;

    public HmacSha256EventSignatureVerifier(
        IEventVerificationKeyProvider keyProvider,
        IEventSigningInputBuilder inputBuilder,
        TimeProvider timeProvider)
    {
        _keyProvider = keyProvider;
        _inputBuilder = inputBuilder;
        _timeProvider = timeProvider;
    }

    public async ValueTask VerifyAsync(
        EventTransportMessage message,
        SignedEventAuthorizationTuple authorization,
        string signingIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(authorization);
        var actual = new SignedEventAuthorizationTuple(
            message.Producer,
            message.EventName,
            message.EventVersion,
            Header(message, TrustedTransportMetadata.SignatureSchemeHeader),
            signingIdentity);
        if (!authorization.Matches(actual))
        {
            throw new EventSecurityException("The signed event tuple is not authorized.", "event.tuple.unauthorized");
        }

        var nowUtc = _timeProvider.GetUtcNow();
        if (message.OccurredAtUtc < nowUtc - MaximumEventAge
            || message.OccurredAtUtc > nowUtc + AllowedFutureSkew)
        {
            throw new EventSecurityException("The signed event is outside the freshness window.", "event.freshness.rejected");
        }

        var keyId = Header(message, TrustedTransportMetadata.KeyIdHeader);
        EventVerificationKey key;
        try
        {
            key = await _keyProvider.GetVerificationKeyAsync(authorization, keyId, cancellationToken);
        }
        catch (EventTransportTerminalExceptionBase)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EventDependencyException("Verification key dependency failed.", exception);
        }

        if (!string.Equals(key.KeyId, keyId, StringComparison.Ordinal) || key.Secret.IsEmpty)
        {
            throw new EventSecurityException("The signing key is not trusted.", "event.key.untrusted");
        }

        key.EnsureUsable(nowUtc);
        var signatureText = Header(message, TrustedTransportMetadata.SignatureHeader);
        if (signatureText.Length != 64
            || signatureText.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new EventSecurityException("The event signature format is invalid.", "event.signature.format");
        }

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signatureText);
        }
        catch (FormatException)
        {
            throw new EventSecurityException("The event signature format is invalid.", "event.signature.format");
        }

        var expected = HMACSHA256.HashData(key.Secret.Span, _inputBuilder.Build(message, authorization.Scheme));
        if (supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            throw new EventSecurityException("The event signature is invalid.", "event.signature.invalid");
        }
    }

    private static string Header(EventTransportMessage message, string name)
    {
        if (!message.TransportMetadata.Headers.TryGetValue(name, out var value))
        {
            throw new EventContractException("The signature header set is incomplete.", "event.header.incomplete");
        }

        return value;
    }
}
