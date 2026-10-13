using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// Parent lot → child lot through a transformation (collection <c>trace_genealogy_links</c>, pack §4). Written through
/// <c>linkGenealogy</c> (MOD-0194 later); insert-only, never deleted. Unique per Tenant + parent + child + reference.
/// </summary>
public sealed class LotGenealogyLink : EntityBase
{
    public Guid ParentLotId { get; set; }

    public Guid ChildLotId { get; set; }

    public string TransformationReference { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;

    /// <summary>Decimal string as received (never a float).</summary>
    public string? QuantityConsumed { get; set; }

    public string? UomId { get; set; }

    public Guid CreatedBy { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }
}
