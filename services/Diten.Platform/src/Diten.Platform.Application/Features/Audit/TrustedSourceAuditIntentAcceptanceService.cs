using System.Text.RegularExpressions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Audit;

public sealed partial class TrustedSourceAuditIntentAcceptanceService : ITrustedSourceAuditIntentAcceptanceService
{
    private const string SourceService = "Diten.MDM";
    private const string ContractVersion = "mod-0290.audit-intent.v1";
    private readonly ITrustedSourceAuditIntentOutbox _outbox;
    private readonly TimeProvider _timeProvider;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TrustedSourceAuditIntentAcceptanceService> _logger;

    public TrustedSourceAuditIntentAcceptanceService(
        ITrustedSourceAuditIntentOutbox outbox,
        TimeProvider timeProvider,
        // WP-GP-AUDIT-TRAIL-01 D1 — required, not defaulted: a missing registration must fail at startup, not quietly
        // write every master-data row with nobody named as the person who acted.
        IUserDisplayNameResolver displayNames,
        ITenantContext tenantContext,
        ILogger<TrustedSourceAuditIntentAcceptanceService> logger)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _displayNames = displayNames ?? throw new ArgumentNullException(nameof(displayNames));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var actor = await ResolveActorAsync(envelope, ct);

        return await _outbox.AcceptAsync(
            envelope,
            centralKey,
            fingerprint,
            entityType,
            operation,
            actor,
            ct);
    }

    /// <summary>
    /// The person behind the source's actor id, resolved HERE by Platform — the source sends an id and nothing else.
    ///
    /// <para>The name is looked up in the INTENT's tenant (the user belongs to it), masked by Platform's one audit rule,
    /// and never fails the acceptance: a name that cannot be resolved leaves the row nameless, with the person's id
    /// still on it, and says so in the log. An actor id that is not a user id (a job, a service) is not a person and the
    /// row stays a service row.</para>
    /// </summary>
    private async Task<TrustedSourceAuditActor> ResolveActorAsync(
        TrustedSourceAuditIntentEnvelope envelope, CancellationToken ct)
    {
        if (!Guid.TryParse(envelope.ActorId, out var userId) || userId == Guid.Empty)
        {
            return TrustedSourceAuditActor.None;
        }

        string? name = null;
        try
        {
            using (TenantScope.Begin(_tenantContext, envelope.TenantId))
            {
                var names = await _displayNames.ResolveAsync([userId], ct);
                name = names.TryGetValue(userId, out var resolved) ? resolved : null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInformation(
                "Trusted source audit intent {IntentId}: the actor's name could not be resolved ({ErrorType}); "
                + "the row is written with the actor id only.", envelope.IntentId, ex.GetType().Name);
            return new TrustedSourceAuditActor(userId, null);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogInformation(
                "Trusted source audit intent {IntentId}: the actor's name could not be resolved; "
                + "the row is written with the actor id only.", envelope.IntentId);
        }

        return new TrustedSourceAuditActor(userId, AuditService.MaskDisplayName(name));
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
