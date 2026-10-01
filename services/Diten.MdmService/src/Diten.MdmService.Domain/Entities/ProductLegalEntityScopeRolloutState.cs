using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductLegalEntityScopeRolloutState : EntityBase, IAuditIntentAggregate
{
    public ProductLegalEntityScopeRolloutMode Mode { get; set; } = ProductLegalEntityScopeRolloutMode.Preparation;
    public Guid CreationCommandId { get; set; }
    public Guid CreatedByActorId { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
    public ProductLegalEntityScopeWriterLease? ActiveWriterLease { get; set; }
    public long WriterLeaseGeneration { get; set; }
    public ProductLegalEntityScopeActivationFence? ActiveFence { get; set; }
    public ProductLegalEntityScopeInventorySnapshot? LastInventorySnapshot { get; set; }
    public Guid? LastTransitionCommandId { get; set; }
    public Guid? LastTransitionActorId { get; set; }
    public string? LastTransitionAction { get; set; }
    public string? LastTransitionReasonCode { get; set; }
    public string? LastTransitionEvidenceHash { get; set; }
    public Guid? LastTransitionIntentId { get; set; }

    public static ProductLegalEntityScopeRolloutState CreatePreparation(
        Guid tenantId,
        Guid creationCommandId,
        Guid actorId,
        DateTimeOffset serverNowUtc)
    {
        if (tenantId == Guid.Empty || creationCommandId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Rollout identities must be non-empty.");
        }
        if (serverNowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", nameof(serverNowUtc));
        }

        return new ProductLegalEntityScopeRolloutState
        {
            TenantId = tenantId,
            CreationCommandId = creationCommandId,
            CreatedByActorId = actorId,
            Mode = ProductLegalEntityScopeRolloutMode.Preparation,
            CreatedAt = serverNowUtc,
            Version = 0
        };
    }

    public void EnsureValid()
    {
        if (TenantId == Guid.Empty
            || CreationCommandId == Guid.Empty
            || CreatedByActorId == Guid.Empty
            || Version < 0
            || WriterLeaseGeneration < 0
            || !Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_STATE_INVALID");
        }
        ActiveWriterLease?.EnsureValid();
        ActiveFence?.EnsureValid();
        LastInventorySnapshot?.EnsureValid();
        var hasTransition = LastTransitionCommandId.HasValue || LastTransitionActorId.HasValue
            || LastTransitionIntentId.HasValue || LastTransitionAction is not null
            || LastTransitionReasonCode is not null || LastTransitionEvidenceHash is not null;
        if (hasTransition && (!LastTransitionCommandId.HasValue || LastTransitionCommandId.Value == Guid.Empty
            || !LastTransitionActorId.HasValue || LastTransitionActorId.Value == Guid.Empty
            || !LastTransitionIntentId.HasValue || LastTransitionIntentId.Value == Guid.Empty
            || LastTransitionAction is not ("ActivateEnforced" or "SuspendFailClosed")
            || LastTransitionReasonCode is null || !ProductLegalEntityScopeActivationFence.Reason(LastTransitionReasonCode)
            || LastTransitionEvidenceHash is null || LastTransitionEvidenceHash.Length != 64
            || !LastTransitionEvidenceHash.All(Uri.IsHexDigit)
            || LastInventorySnapshot is null
            || !string.Equals(LastTransitionEvidenceHash, LastInventorySnapshot.SnapshotHash, StringComparison.Ordinal)))
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_TRANSITION_INVALID");
    }

    public void EnsureExpectedVersion(int expectedVersion)
    {
        if (expectedVersion < 0 || Version != expectedVersion)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_VERSION_CONFLICT");
        }
    }
}
