namespace Diten.MdmService.Domain.Enums;

public enum CodeBearingEntityType
{
    GlobalProduct = 1,
    Gsku = 2,
    Lsku = 3,
    FinishedGood = 4,
    // MOD-0290-FU04 (Item / Material Master): prefix IT, the product identity family's counter.
    Item = 5
}
