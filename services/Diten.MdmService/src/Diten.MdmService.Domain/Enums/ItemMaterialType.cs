namespace Diten.MdmService.Domain.Enums;

// MOD-0290-FU04 §1: the six material types, one for one with the contract's MaterialType and the MOD-0048
// `material-type` list. A new value needs a new contract version.
public enum ItemMaterialType
{
    ActiveIngredient = 1,
    Excipient = 2,
    RawMaterial = 3,
    PackagingPrimary = 4,
    PackagingSecondary = 5,
    Intermediate = 6
}
