namespace Diten.MdmService.Domain.Enums;

public enum ProductAuditOperation
{
    CodeReserved = 1,
    CodeConsumed = 2,
    CodeBindingConfirmed = 3,
    CodeBurned = 4,
    GlobalProductDraftCreated = 5,
    ProductDefinitionRevisionDraftCreated = 6,
    GskuDraftCreated = 7,
    GskuDraftUpdated = 8,
    FinishedGoodDraftCreated = 9,
    LskuDraftCreated = 10,
    ProductLegalEntityScopePolicyCreated = 11,
    ProductLegalEntityScopePolicyReplaced = 12,
    ProductLegalEntityScopePolicyEnded = 13,
    ProductLegalEntityScopeEnforcementActivated = 14,
    ProductLegalEntityScopeEnforcementSuspended = 15,
    GlobalProductIdentitySubmitted = 16,
    GlobalProductIdentityApproved = 17,
    GlobalProductIdentityRejected = 18,
    GlobalProductIdentityRetired = 19,
    ProductDefinitionRevisionIdentitySubmitted = 20,
    GskuIdentitySubmitted = 21,
    ProductDefinitionRevisionIdentityApproved = 22,
    GskuIdentityApproved = 23,
    ProductDefinitionRevisionIdentityRejected = 24,
    GskuIdentityRejected = 25,
    GskuIdentityRetired = 26,
    ProductDefinitionRevisionIdentityRetired = 27,
    LskuIdentitySubmitted = 28,
    LskuIdentityApproved = 29,
    LskuIdentityRejected = 30,
    LskuIdentityRetired = 31,
    FinishedGoodIdentitySubmitted = 32,
    FinishedGoodIdentityApproved = 33,
    FinishedGoodIdentityRejected = 34,
    FinishedGoodIdentityRetired = 35,
    GlobalProductDraftUpdated = 38,
    GlobalProductIdentityApprovalWithdrawn = 39,
    GlobalProductCorrectionRequested = 40,
    GlobalProductCorrectionApplied = 41,
    GlobalProductCorrectionRejected = 42,
    GlobalProductCorrectionCancelled = 43,
    GlobalProductCorrectionManualReconciliationRequired = 44,
    GlobalProductRetirementRequested = 45,
    GlobalProductRetirementRejected = 46,
    GlobalProductRetirementCancelled = 47,
    GlobalProductRetirementManualReconciliationRequired = 48,
    ProductDefinitionRevisionIdentityApprovalWithdrawn = 49,
    GskuIdentityApprovalWithdrawn = 50,
    ProductAbbreviationAllocationRequested = 51,
    ProductAbbreviationAllocationApproved = 52,
    ProductAbbreviationAllocationRejected = 53,
    ProductAbbreviationAllocationCancelled = 54,
    ProductAbbreviationCorrectionRequested = 55,
    ProductAbbreviationCorrectionApproved = 56,
    ProductAbbreviationCorrectionRejected = 57,
    ProductAbbreviationCorrectionCancelled = 58,
    ProductAbbreviationRetirementRequested = 59,
    ProductAbbreviationRetirementApproved = 60,
    ProductAbbreviationRetirementRejected = 61,
    GskuCorrectionRequested = 62,
    GskuCorrectionApplied = 63,
    GskuCorrectionRejected = 64,
    GskuCorrectionManualReconciliationRequired = 65,
    LskuIdentityApprovalWithdrawn = 66,
    LskuRetirementRequested = 67,
    LskuRetirementRejected = 68,
    GskuRetirementRequested = 69,
    GskuRetirementRejected = 70,
    GskuRetirementManualReconciliationRequired = 71,
    FinishedGoodDraftCancelled = 72,
    FinishedGoodIdentityApprovalWithdrawn = 73,
    FinishedGoodRetirementRequested = 74,
    FinishedGoodRetirementRejected = 75,
    FinishedGoodRetirementCancelled = 76,
    // The engine approved the pair's identity and MDM could not apply it: both are drafts again.
    ProductDefinitionRevisionIdentityDecisionNotApplicable = 77,
    GskuIdentityDecisionNotApplicable = 78,
    // BL-552: the engine closed the Global Product's identity approval without a decision (cancelled in the Task Center,
    // timed out) or with its maker's own decision: no decision can be applied, the product is a draft again.
    GlobalProductIdentityDecisionNotApplicable = 79,
    // BL-552 flow 2 FIX1 item 12: a Global Product correction / retirement request ENDED without its decision applied —
    // its lock given back, nothing held (the reason is in the intent's facts). Kept apart from
    // …ManualReconciliationRequired so the central audit can tell "ended" from "stuck".
    GlobalProductCorrectionEnded = 80,
    GlobalProductRetirementEnded = 81,
    // LSKU FIX3 item 18: 79–81 belong to GP flow 3 (GlobalProductIdentityDecisionNotApplicable, GlobalProductCorrectionEnded,
    // GlobalProductRetirementEnded, takeover 23fb2d85f); the five LSKU values moved above them, order kept. Never
    // persisted anywhere (this branch was never merged): no migration. FG's own new values start at 87.
    // LSKU FIX1 B1 (c): the operator repair gave back a creation admission whose child row never came.
    GskuChildCreationAdmissionReleased = 82,
    // LSKU FIX1 B3: a draft LSKU cancelled so it no longer holds its parent GSKU.
    LskuDraftCancelled = 83,
    // LSKU FIX1 B5: the engine approved the LSKU's identity and MDM could not apply it: the LSKU is a draft again.
    LskuIdentityDecisionNotApplicable = 84,
    // LSKU FIX1 B2: a retirement request ended without its decision applied; the lock went back in the same write.
    LskuRetirementManualReconciliationRequired = 85,
    // LSKU FIX1 B3: the engine refused the submit before starting anything; the draft's version moves so a resubmit
    // is a new operation.
    LskuIdentitySubmitAbandoned = 86,

    // MOD-0290-FU05 slice 2 (CT decision T-2, 2026-10-15): an approved Global Product's brand changed by its own audited
    // command (no approval flow — owner Karar a). 120: after the Finished Good (87–99) and Item (100–119) ranges.
    GlobalProductBrandChanged = 120

    // CT decision 3 (2026-10-07, MOD-0290-FU04 S1): 87–99 stay reserved for the Finished Good lifecycle (unused);
    // 100–119 are the Item's (MOD-0290-FU04). S1 needs none — its commands audit through path b (AuditOperation); the
    // range is for the item's own audit intents if a later slice adds them. Guarded by
    // ItemMasterUnitTests.Reserved_ranges_hold_no_value_outside_their_owner.
}
