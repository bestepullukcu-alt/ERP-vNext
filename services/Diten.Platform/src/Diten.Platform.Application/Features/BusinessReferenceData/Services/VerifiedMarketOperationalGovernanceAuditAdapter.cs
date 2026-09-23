using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class VerifiedMarketOperationalGovernanceAuditAdapter : IBusinessReferenceDataGovernanceAuditAdapter
{
    private static readonly Guid LockedReferenceTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    internal const string RequestType = "BusinessReferenceData.publish";
    internal const string EntityType = "BusinessReferenceDataVersion";
    internal const string GovernanceEvent = "publish";
    private const string SourceModule = "MOD-0048-FU01";
    private const string LockedCatalogVersion = "UNSD-M49-2026-08-08";
    private const string LockedCatalogFingerprint = "b94c45280195b0cb5faa155656c4690938790144d148fba279d2232204360039";
    private const string LockedOverrideReason = "BusinessReferenceData catalog load override";

    private readonly IAuditService _auditService;
    private readonly IVerifiedMarketOperationalPreflight _preflight;
    private VerifiedMarketOperationalFacts? _facts;
    private PendingOverride? _pendingOverride;

    public VerifiedMarketOperationalGovernanceAuditAdapter(
        IAuditService auditService,
        IVerifiedMarketOperationalPreflight preflight)
    {
        _auditService = auditService;
        _preflight = preflight;
    }

    public void Bind(VerifiedMarketOperationalFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (_facts is not null
            || facts.ReferenceTenantId != LockedReferenceTenantId
            || !string.Equals(facts.CatalogVersion, LockedCatalogVersion, StringComparison.Ordinal)
            || !string.Equals(facts.CatalogFingerprint, LockedCatalogFingerprint, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(facts.ActorId)
            || string.IsNullOrWhiteSpace(facts.IdempotencyNamespace))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION");
        }

        _facts = facts;
    }

    public Task EmitPublishAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        bool success,
        string? reasonCode,
        string publishMode,
        CancellationToken ct = default) =>
        EmitVerifiedPublishAsync(version, actorId, correlationId, success, reasonCode, publishMode, ct);

    private async Task EmitVerifiedPublishAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string publicationCorrelationId,
        bool success,
        string? reasonCode,
        string publishMode,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);

        var facts = _facts;
        var pending = _pendingOverride;
        if (facts is null
            || pending is null
            || !success
            || !string.Equals(publishMode, "Immediate", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(version.LastPublishIdempotencyKey)
            || !string.Equals(version.LastPublishIdempotencyKey, BuildOperationKey(facts), StringComparison.Ordinal)
            || version.TenantId != LockedReferenceTenantId
            || version.BusinessReferenceDataVersionId == Guid.Empty
            || string.IsNullOrWhiteSpace(actorId)
            || string.IsNullOrWhiteSpace(publicationCorrelationId)
            || !string.Equals(actorId.Trim(), facts.ActorId, StringComparison.Ordinal)
            || pending.VersionId != version.BusinessReferenceDataVersionId
            || !string.Equals(pending.ActorId, actorId.Trim(), StringComparison.Ordinal)
            || !string.Equals(pending.PublicationCorrelationId, publicationCorrelationId, StringComparison.Ordinal)
            || !string.Equals(version.LastCorrelationId, publicationCorrelationId, StringComparison.Ordinal)
            || !string.Equals(version.OverrideReason, LockedOverrideReason, StringComparison.Ordinal)
            || version.Values.Count != 249)
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION");
        }

        var operationKey = version.LastPublishIdempotencyKey;
        var auditCorrelationId = BuildDeterministicCorrelationId(operationKey);
        var normalizedActor = actorId.Trim();
        var parsedActor = Guid.TryParse(normalizedActor, out var actorIdValue);
        var request = new AuditAppendRequest
        {
            CorrelationId = auditCorrelationId,
            RequestType = RequestType,
            ActorType = parsedActor ? AuditActorType.TenantUser : AuditActorType.System,
            ActorId = parsedActor ? actorIdValue : null,
            ActorDisplayName = normalizedActor,
            TargetTenantId = version.TenantId,
            Category = AuditCategory.ReferenceData,
            EntityType = EntityType,
            EntityId = version.BusinessReferenceDataVersionId,
            Operation = AuditOperation.Activate,
            Outcome = AuditOutcome.Succeeded,
            Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["governanceEvent"] = GovernanceEvent,
                ["publishMode"] = publishMode,
                ["reasonCode"] = reasonCode,
                ["publicationCorrelationId"] = publicationCorrelationId,
                ["publicationOperationKey"] = operationKey,
                ["catalogVersion"] = "UNSD-M49-2026-08-08",
                ["setCode"] = "market",
                ["actor"] = normalizedActor,
                ["actorType"] = parsedActor ? AuditActorType.TenantUser.ToString() : AuditActorType.System.ToString()
            },
            SourceModule = SourceModule
        };

        var result = await _auditService.AppendAsync(request, ct);
        if (result.Status is not AuditAppendStatus.Queued and not AuditAppendStatus.Duplicate
            || string.IsNullOrWhiteSpace(result.IdempotencyKey))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_AUDIT_APPEND_FAILED");
        }

        var proof = await _preflight.VerifyAuditOutboxAsync(
            new VerifiedMarketOperationalAuditProofRequest(
                version.TenantId,
                auditCorrelationId,
                publicationCorrelationId,
                result.IdempotencyKey,
                RequestType,
                EntityType,
                version.BusinessReferenceDataVersionId,
                AuditOperation.Activate,
                GovernanceEvent,
                publishMode,
                normalizedActor,
                operationKey,
                "UNSD-M49-2026-08-08",
                "market"),
            ct);
        if (!proof.IsExact || proof.MatchCount != 1)
        {
            throw new InvalidOperationException(proof.ReasonCode);
        }

        _pendingOverride = null;
    }

    internal static Guid BuildDeterministicCorrelationId(string operationKey)
    {
        var material = Encoding.UTF8.GetBytes($"verified-market-audit|{operationKey}");
        Span<byte> guidBytes = stackalloc byte[16];
        SHA256.HashData(material).AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    private static Task Reject() =>
        Task.FromException(new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION"));

    private static string BuildOperationKey(VerifiedMarketOperationalFacts facts) =>
        $"{facts.IdempotencyNamespace}:businessreferencedata-catalog-v{facts.CatalogVersion}:market".ToLowerInvariant();

    public Task EmitSubmitAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, bool success, string? reasonCode, CancellationToken ct = default) => Reject();
    public Task EmitWorkflowStartAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, Guid? workflowInstanceId, string? workflowState, bool success, string? reasonCode, CancellationToken ct = default) => Reject();
    public Task EmitWorkflowTransitionRequestedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid workflowInstanceId, string idempotencyKey, CancellationToken ct = default) => Reject();
    public Task EmitWorkflowTransitionSucceededAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default) => Reject();
    public Task EmitWorkflowTransitionFailedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid? workflowInstanceId, string? idempotencyKey, string reasonCode, CancellationToken ct = default) => Reject();
    public Task EmitWorkflowSyncAppliedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default) => Reject();
    public Task EmitEvidenceCheckAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default) => Reject();
    public Task EmitEvidenceRequirementEvaluatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default) => Reject();
    public Task EmitEvidenceArtifactValidatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default) => Reject();
    public Task EmitEvidenceUnsatisfiedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, string actionCode, CancellationToken ct = default) => Reject();
    public Task EmitApprovalDecisionAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool approved, string? rejectionReason, bool success, string? reasonCode, CancellationToken ct = default) => Reject();
    public Task EmitOverrideAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        string overrideReason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        var facts = _facts;
        if (facts is null
            || _pendingOverride is not null
            || version.TenantId != LockedReferenceTenantId
            || version.BusinessReferenceDataVersionId == Guid.Empty
            || string.IsNullOrWhiteSpace(actorId)
            || string.IsNullOrWhiteSpace(correlationId)
            || !string.Equals(actorId.Trim(), facts.ActorId, StringComparison.Ordinal)
            || !string.Equals(overrideReason, LockedOverrideReason, StringComparison.Ordinal)
            || !string.Equals(version.OverrideReason, LockedOverrideReason, StringComparison.Ordinal)
            || version.Values.Count != 249)
        {
            return Reject();
        }

        _pendingOverride = new PendingOverride(version.BusinessReferenceDataVersionId, actorId.Trim(), correlationId);
        return Task.CompletedTask;
    }

    private sealed record PendingOverride(Guid VersionId, string ActorId, string PublicationCorrelationId);
}
