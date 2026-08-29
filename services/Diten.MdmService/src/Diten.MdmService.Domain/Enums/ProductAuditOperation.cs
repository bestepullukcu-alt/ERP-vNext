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
    GskuIdentityRejected = 25
}
