namespace Diten.MdmService.Domain.ValueObjects;

/// <summary>
/// MOD-0290-FU04 S2 FIX1 (minor) — one code an item held when it was submitted for approval, as the list answered it
/// then: the code, its ConditionKind (storage conditions; filled from the list for an S1 row that never stored it) and
/// the catalog version that answered. Written once on the operation; the approval compares the list with THIS, never
/// with the live item ("S2 kanıt karşılaştırma kuralı": the code still in its list, the same kind — the version is kept,
/// not compared).
/// </summary>
public sealed class ItemReferenceEvidenceEntry
{
    public string SetCode { get; set; } = string.Empty;
    public string ValueCode { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public int CatalogVersionNumber { get; set; }
}
