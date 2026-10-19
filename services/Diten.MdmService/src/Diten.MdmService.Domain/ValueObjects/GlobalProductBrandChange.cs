namespace Diten.MdmService.Domain.ValueObjects;

/// <summary>
/// MOD-0290-FU05 slice 2 (CT decision) — one brand change of an approved Global Product, written in the same conditional
/// write as its audit intent (<c>GlobalProductBrandChanged</c>). Ids only: the brand's code and name are never stored on
/// the product (Karar b).
///
/// <para>Why it is kept: the intent's evidence carries the brand before and after. "Before" is the brand at the version
/// the write was conditioned on — after the write the product no longer shows it, so an exact repeat of the command finds
/// it here and rebuilds the same intent (replay), instead of a conflict.</para>
/// </summary>
public sealed class GlobalProductBrandChange
{
    public string OperationKey { get; set; } = string.Empty;
    public int PreVersion { get; set; }
    public Guid? FromBrandId { get; set; }
    public Guid? ToBrandId { get; set; }
}
