using System.Text.RegularExpressions;
using Diten.Platform.Application.Contracts.Audit;

namespace Diten.Platform.Application.Features.Audit;

public sealed partial class TrustedSourceAuditIntentAcceptanceService : ITrustedSourceAuditIntentAcceptanceService
{
    private const string SourceService = "Diten.MDM";
    private const string ContractVersion = "mod-0290.audit-intent.v1";
    private readonly ITrustedSourceAuditIntentOutbox _outbox;
    private readonly TimeProvider _timeProvider;

    public TrustedSourceAuditIntentAcceptanceService(
        ITrustedSourceAuditIntentOutbox outbox,
        TimeProvider timeProvider)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TrustedSourceAuditIntentAcceptanceResult> AcceptAsync(
        TrustedSourceAuditIntentEnvelope envelope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!string.Equals(envelope.SourceService, SourceService, StringComparison.Ordinal)
            || !string.Equals(envelope.ContractVersion, ContractVersion, StringComparison.Ordinal))
        {
            return TrustedSourceAuditIntentAcceptanceResult.ContractUnsupported();
        }

        if (!IsValid(envelope, _timeProvider.GetUtcNow()))
        {
            return TrustedSourceAuditIntentAcceptanceResult.Invalid();
        }

        if (!TrustedSourceAuditIntentOperationMap.TryMap(
                envelope.AggregateType,
                envelope.Operation,
                out var entityType,
                out var operation))
        {
            return TrustedSourceAuditIntentAcceptanceResult.MappingUnsupported();
        }

        var centralKey = TrustedSourceAuditIntentCanonicalizer.BuildCentralIdempotencyKey(envelope);
        var fingerprint = TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(envelope);

        return await _outbox.AcceptAsync(
            envelope,
            centralKey,
            fingerprint,
            entityType,
            operation,
            ct);
    }

    private static bool IsValid(TrustedSourceAuditIntentEnvelope envelope, DateTimeOffset now)
    {
        return envelope.IntentId != Guid.Empty
               && envelope.TenantId != Guid.Empty
               && envelope.AggregateId != Guid.Empty
               && envelope.CorrelationId != Guid.Empty
               && envelope.PreVersion >= -1
               && envelope.PostVersion >= 0
               && envelope.PostVersion == envelope.PreVersion + 1
               && envelope.Sequence >= 0
               && envelope.TimestampUtc.Offset == TimeSpan.Zero
               && envelope.TimestampUtc <= now.AddMinutes(5)
               && IsExactBounded(envelope.AggregateType, 160)
               && IsExactBounded(envelope.Operation, 160)
               && IsExactBounded(envelope.ActorId, 160)
               && IsExactBounded(envelope.CausationId, 160)
               && IsExactBounded(envelope.CommandId, 160)
               && IsExactBounded(envelope.IdempotencyKey, 240)
               && envelope.EvidenceHash is not null
               && UpperSha256Regex().IsMatch(envelope.EvidenceHash)
               && (envelope.SnapshotReference is null || SafeSnapshotReferenceRegex().IsMatch(envelope.SnapshotReference));
    }

    private static bool IsExactBounded(string value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maxLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    [GeneratedRegex("^[0-9A-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex UpperSha256Regex();

    [GeneratedRegex("^[A-Za-z0-9._:/-]{1,256}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSnapshotReferenceRegex();
}
