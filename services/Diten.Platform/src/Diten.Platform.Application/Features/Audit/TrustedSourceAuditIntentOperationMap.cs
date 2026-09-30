using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Audit;

public static class TrustedSourceAuditIntentOperationMap
{
    private static readonly IReadOnlyDictionary<(string AggregateType, string Operation), (string EntityType, AuditOperation Operation)> Mappings =
        new Dictionary<(string, string), (string, AuditOperation)>
        {
            [("ProductAbbreviation", "ProductAbbreviationAllocationRequested")] = ("ProductAbbreviation", AuditOperation.Create),
            [("ProductAbbreviation", "ProductAbbreviationAllocationApproved")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationAllocationRejected")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationAllocationCancelled")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationCorrectionRequested")] = ("ProductAbbreviation", AuditOperation.Create),
            [("ProductAbbreviation", "ProductAbbreviationCorrectionApproved")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationCorrectionRejected")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationCorrectionCancelled")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationRetirementRequested")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("ProductAbbreviation", "ProductAbbreviationRetirementApproved")] = ("ProductAbbreviation", AuditOperation.Deactivate),
            [("ProductAbbreviation", "ProductAbbreviationRetirementRejected")] = ("ProductAbbreviation", AuditOperation.LifecycleTransition),
            [("CodeReservation", "CodeReserved")] = ("CodeReservation", AuditOperation.Create),
            [("CodeReservation", "CodeConsumed")] = ("CodeReservation", AuditOperation.Update),
            [("CodeReservation", "CodeBindingConfirmed")] = ("CodeReservation", AuditOperation.Update),
            [("CodeReservation", "CodeBurned")] = ("CodeReservation", AuditOperation.Deactivate),
            [("GlobalProduct", "GlobalProductDraftCreated")] = ("GlobalProduct", AuditOperation.Create),
            [("GlobalProduct", "GlobalProductDraftUpdated")] = ("GlobalProduct", AuditOperation.Update),
            [("GlobalProduct", "GlobalProductIdentitySubmitted")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductIdentityApproved")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductIdentityRejected")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductIdentityApprovalWithdrawn")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductIdentityRetired")] = ("GlobalProduct", AuditOperation.Deactivate),
            [("GlobalProduct", "GlobalProductCorrectionRequested")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductCorrectionApplied")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductCorrectionRejected")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductCorrectionManualReconciliationRequired")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductRetirementRequested")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductRetirementRejected")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
            [("GlobalProduct", "GlobalProductRetirementManualReconciliationRequired")] = ("GlobalProduct", AuditOperation.LifecycleTransition),
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
            [("FinishedGood", "FinishedGoodIdentitySubmitted")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodIdentityApproved")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodIdentityRejected")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodIdentityRetired")] = ("FinishedGood", AuditOperation.Deactivate),
            [("FinishedGood", "FinishedGoodDraftCancelled")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodIdentityApprovalWithdrawn")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodRetirementRequested")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodRetirementRejected")] = ("FinishedGood", AuditOperation.LifecycleTransition),
            [("FinishedGood", "FinishedGoodRetirementCancelled")] = ("FinishedGood", AuditOperation.LifecycleTransition),
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
