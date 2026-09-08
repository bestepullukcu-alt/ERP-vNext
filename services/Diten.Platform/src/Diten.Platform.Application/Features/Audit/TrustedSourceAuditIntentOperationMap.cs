using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Audit;

public static class TrustedSourceAuditIntentOperationMap
{
    private static readonly IReadOnlyDictionary<(string AggregateType, string Operation), (string EntityType, AuditOperation Operation)> Mappings =
        new Dictionary<(string, string), (string, AuditOperation)>
        {
            [("CodeReservation", "CodeReserved")] = ("CodeReservation", AuditOperation.Create),
            [("CodeReservation", "CodeConsumed")] = ("CodeReservation", AuditOperation.Update),
            [("CodeReservation", "CodeBindingConfirmed")] = ("CodeReservation", AuditOperation.Update),
            [("CodeReservation", "CodeBurned")] = ("CodeReservation", AuditOperation.Deactivate),
            [("GlobalProduct", "GlobalProductDraftCreated")] = ("GlobalProduct", AuditOperation.Create),
            [("ProductDefinitionRevision", "ProductDefinitionRevisionDraftCreated")] = ("ProductDefinitionRevision", AuditOperation.Create),
            [("ProductDefinitionRevision", "ProductDefinitionRevisionIdentitySubmitted")] = ("ProductDefinitionRevision", AuditOperation.LifecycleTransition),
            [("ProductDefinitionRevision", "ProductDefinitionRevisionIdentityApproved")] = ("ProductDefinitionRevision", AuditOperation.LifecycleTransition),
            [("ProductDefinitionRevision", "ProductDefinitionRevisionIdentityRejected")] = ("ProductDefinitionRevision", AuditOperation.LifecycleTransition),
            [("ProductDefinitionRevision", "ProductDefinitionRevisionIdentityApprovalWithdrawn")] = ("ProductDefinitionRevision", AuditOperation.LifecycleTransition),
            [("ProductDefinitionRevision", "ProductDefinitionRevisionIdentityRetired")] = ("ProductDefinitionRevision", AuditOperation.Deactivate),
            [("Gsku", "GskuDraftCreated")] = ("Gsku", AuditOperation.Create),
            [("Gsku", "GskuDraftUpdated")] = ("Gsku", AuditOperation.Update),
            [("Gsku", "GskuIdentitySubmitted")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuIdentityApproved")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuIdentityRejected")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuIdentityApprovalWithdrawn")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuCorrectionRequested")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuCorrectionApplied")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuCorrectionRejected")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuCorrectionManualReconciliationRequired")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuRetirementRequested")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuRetirementRejected")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuRetirementManualReconciliationRequired")] = ("Gsku", AuditOperation.LifecycleTransition),
            [("Gsku", "GskuIdentityRetired")] = ("Gsku", AuditOperation.Deactivate),
            [("FinishedGood", "FinishedGoodDraftCreated")] = ("FinishedGood", AuditOperation.Create),
            [("Lsku", "LskuDraftCreated")] = ("Lsku", AuditOperation.Create),
            [("Lsku", "LskuIdentitySubmitted")] = ("Lsku", AuditOperation.LifecycleTransition),
            [("Lsku", "LskuIdentityApproved")] = ("Lsku", AuditOperation.LifecycleTransition),
            [("Lsku", "LskuIdentityRejected")] = ("Lsku", AuditOperation.LifecycleTransition),
            [("Lsku", "LskuIdentityRetired")] = ("Lsku", AuditOperation.Deactivate),
            [("Lsku", "LskuIdentityApprovalWithdrawn")] = ("Lsku", AuditOperation.LifecycleTransition),
            [("Lsku", "LskuRetirementRequested")] = ("Lsku", AuditOperation.LifecycleTransition),
            [("Lsku", "LskuRetirementRejected")] = ("Lsku", AuditOperation.LifecycleTransition),
            [("ProductLegalEntityScopePolicy", "ProductLegalEntityScopePolicyCreated")] = ("ProductLegalEntityScopePolicy", AuditOperation.Create),
            [("ProductLegalEntityScopePolicy", "ProductLegalEntityScopePolicyReplaced")] = ("ProductLegalEntityScopePolicy", AuditOperation.Update),
            [("ProductLegalEntityScopePolicy", "ProductLegalEntityScopePolicyEnded")] = ("ProductLegalEntityScopePolicy", AuditOperation.Deactivate),
            [("ProductLegalEntityScopeRolloutState", "ProductLegalEntityScopeEnforcementActivated")] = ("ProductLegalEntityScopeRolloutState", AuditOperation.Activate),
            [("ProductLegalEntityScopeRolloutState", "ProductLegalEntityScopeEnforcementSuspended")] = ("ProductLegalEntityScopeRolloutState", AuditOperation.Suspend)
        };

    public static bool TryMap(
        string aggregateType,
        string operation,
        out string entityType,
        out AuditOperation mappedOperation)
    {
        if (Mappings.TryGetValue((aggregateType, operation), out var mapping))
        {
            entityType = mapping.EntityType;
            mappedOperation = mapping.Operation;
            return true;
        }

        entityType = string.Empty;
        mappedOperation = AuditOperation.Unknown;
        return false;
    }
}
