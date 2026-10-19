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

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 B4 — the product writers in flight, one lease per command, keyed by what each touches.
    /// <see cref="ActiveWriterLease"/> stays the scope policy's own (foreground replace) lease.
    /// </summary>
    public List<ProductLegalEntityScopeWriterLease> ProductWriterLeases { get; set; } = [];

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX2 (5) — the most product writers one tenant holds at once. A new writer beyond it is
    /// refused as busy; the list (rewritten by every acquisition) never grows without bound. A stored list above it is
    /// still readable.
    /// </summary>
    public const int MaximumProductWriterLeases = 256;

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX3 (3) — the ceiling of the OTHER bucket: leases that name no object (creates). They never
    /// count against <see cref="MaximumProductWriterLeases"/>, so failed creates cannot refuse the writers of products.
    /// </summary>
    public const int MaximumObjectlessWriterLeases = 512;

    /// <summary>Optimistic revision of <see cref="ProductWriterLeases"/>: every change of the list moves it by one.</summary>
    public long ProductWriterLeaseRevision { get; set; }

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 B3 — the last fence that was abandoned (readiness or inventory refused the transition,
    /// or an operator aborted it with the fence's own command): who, which command, why, when. Kept as evidence.
    /// </summary>
    public ProductLegalEntityScopeFenceAbort? LastFenceAbort { get; set; }
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
        LastFenceAbort?.EnsureValid();
        if (ProductWriterLeases is null || ProductWriterLeaseRevision < 0
            || ProductWriterLeases.Select(lease => lease.Token).Distinct().Count() != ProductWriterLeases.Count)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_STATE_INVALID");
        }
        foreach (var lease in ProductWriterLeases) lease.EnsureValid();
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
