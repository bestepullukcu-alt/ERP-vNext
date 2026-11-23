namespace Diten.MdmService.Domain.Enums;

public enum AuditAggregateType
{
    CodeReservation = 1,
    GlobalProduct = 2,
    ProductDefinitionRevision = 3,
    Gsku = 4,
    FinishedGood = 5,
    Lsku = 6,
    ProductLegalEntityScopePolicy = 7,
    ProductLegalEntityScopeRolloutState = 8,
    // MOD-0289 DL-1: added before the last member so the file only gains lines; the numbers are explicit.
    Bank = 10,
    BankBranch = 11,
    ProductAbbreviation = 9
}
