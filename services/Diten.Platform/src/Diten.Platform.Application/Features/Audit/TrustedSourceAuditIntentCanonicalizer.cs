using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Contracts.Audit;

namespace Diten.Platform.Application.Features.Audit;

public static class TrustedSourceAuditIntentCanonicalizer
{
    public static string ComputeFingerprint(TrustedSourceAuditIntentEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var stream = new MemoryStream();
        Write(stream, envelope.SourceService);
        Write(stream, envelope.ContractVersion);
        Write(stream, envelope.IntentId.ToString("N"));
        Write(stream, envelope.TenantId.ToString("N"));
        Write(stream, envelope.AggregateType);
        Write(stream, envelope.AggregateId.ToString("N"));
        Write(stream, envelope.PreVersion.ToString(CultureInfo.InvariantCulture));
        Write(stream, envelope.PostVersion.ToString(CultureInfo.InvariantCulture));
        Write(stream, envelope.Operation);
        Write(stream, envelope.ActorId);
        Write(stream, envelope.CorrelationId.ToString("N"));
        Write(stream, envelope.CausationId);
        Write(stream, envelope.CommandId);
        Write(stream, envelope.Sequence.ToString(CultureInfo.InvariantCulture));
        Write(stream, envelope.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        Write(stream, envelope.EvidenceHash);
        Write(stream, envelope.SnapshotReference);
        Write(stream, envelope.IdempotencyKey);

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static string BuildCentralIdempotencyKey(TrustedSourceAuditIntentEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return $"Diten.MDM:{envelope.TenantId:N}:{envelope.IntentId:N}:mod-0290.audit-intent.v1";
    }

    private static void Write(Stream stream, string? value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        if (value is null)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, -1);
            stream.Write(length);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
    }
}
