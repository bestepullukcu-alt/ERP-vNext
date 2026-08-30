using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Audit;

public sealed class PpmAuditTrustedTransportMetadataProvider(
    IAuditIntentRepository auditIntents,
    IOptions<PpmAuditProducerOptions> options) : ITrustedTransportMetadataProvider
{
    public const string SignatureScheme = "ppm-event-hmac-sha256.v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly PpmAuditProducerOptions _options = options.Value;

    public async ValueTask<TrustedTransportMetadata> CreateAsync(
        EventMetadata metadata,
        ReadOnlyMemory<byte> canonicalPayloadUtf8,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled
            || metadata.EventId == Guid.Empty
            || !metadata.TenantId.HasValue
            || metadata.TenantId.Value == Guid.Empty
            || metadata.CorrelationId == Guid.Empty
            || !string.Equals(metadata.EventName, "ppm.audit-intent.submitted.v1", StringComparison.Ordinal)
            || metadata.EventVersion != 1
            || !string.Equals(metadata.Producer, "Diten.PpmService", StringComparison.Ordinal)
            || !PpmAuditProducerOptionsValidator.TryDecodeSecret(_options.SecretBase64, out var secret)
            || secret.Length < 32)
        {
            throw new EventValidationException("PPM audit signing context is invalid.");
        }

        var signingInput = BuildSigningInput(metadata, canonicalPayloadUtf8.Span);
        var signature = Convert.ToHexString(HMACSHA256.HashData(secret, signingInput)).ToLowerInvariant();
        var persisted = await auditIntents.EnsureDispatchMetadataAsync(
            metadata.EventId,
            new AuditIntentDispatchMetadata(SignatureScheme, _options.KeyId!, signature),
            DateTime.UtcNow,
            cancellationToken);

        if (!IsValidSigningMetadata(persisted))
        {
            throw new EventValidationException("Persisted PPM audit signing metadata is invalid.");
        }

        return new TrustedTransportMetadata(new Dictionary<string, string>
        {
            [TrustedTransportMetadata.SignatureSchemeHeader] = persisted.SignatureScheme,
            [TrustedTransportMetadata.KeyIdHeader] = persisted.KeyId,
            [TrustedTransportMetadata.SignatureHeader] = persisted.Signature
        });
    }

    internal static bool IsValidSigningMetadata(AuditIntentDispatchMetadata metadata) =>
        string.Equals(metadata.SignatureScheme, SignatureScheme, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(metadata.KeyId)
        && metadata.KeyId.Length <= 128
        && !metadata.KeyId.Any(character =>
            char.IsControl(character) || char.IsWhiteSpace(character))
        && metadata.Signature.Length == 64
        && !metadata.Signature.Any(character => character is not (>= '0' and <= '9')
            and not (>= 'a' and <= 'f'));

    public static byte[] BuildSigningInput(
        EventMetadata metadata,
        ReadOnlySpan<byte> canonicalPayloadUtf8)
    {
        var prefix = string.Join('\n',
            SignatureScheme,
            metadata.EventId.ToString("D"),
            metadata.EventName,
            metadata.EventVersion.ToString(CultureInfo.InvariantCulture),
            metadata.TenantId!.Value.ToString("D"),
            metadata.CorrelationId.ToString("D"),
            metadata.Producer,
            metadata.CausationId?.ToString("D") ?? "-",
            metadata.OccurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            canonicalPayloadUtf8.Length.ToString(CultureInfo.InvariantCulture)) + "\n";
        var prefixBytes = StrictUtf8.GetBytes(prefix);
        var result = new byte[prefixBytes.Length + canonicalPayloadUtf8.Length];
        prefixBytes.CopyTo(result, 0);
        canonicalPayloadUtf8.CopyTo(result.AsSpan(prefixBytes.Length));
        return result;
    }
}
