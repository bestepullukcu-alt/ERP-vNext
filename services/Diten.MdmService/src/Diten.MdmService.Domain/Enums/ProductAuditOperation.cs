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
    LskuDraftCreated = 10
}
