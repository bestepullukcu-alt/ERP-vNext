using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// ASSUMPTION-0173-05 (the frozen request has no "reversed transaction" field): a REVERSAL is posted with
/// <c>sourceModule = MOD-0173</c>, <c>sourceType = REVERSAL</c>, <c>sourceDocumentId = &lt;original transactionId&gt;</c>;
/// item / sku / skuLevel / quantity / uom must equal the original; the server derives the inverse legs.
/// A REVERSAL cannot itself be reversed.
/// </summary>
public static class ReversalRules
{
    public const string SourceModule = "MOD-0173";

    public const string SourceType = "REVERSAL";

    /// <summary>Checks the reversal envelope before the original is looked up.</summary>
    public static (Guid OriginalId, string? Error) ReadOriginalReference(
        string sourceModule, string sourceType, string? sourceDocumentId)
    {
        if (!string.Equals(sourceModule, SourceModule, StringComparison.Ordinal)
            || !string.Equals(sourceType, SourceType, StringComparison.Ordinal))
        {
            return (Guid.Empty, $"REVERSAL requires sourceModule '{SourceModule}' and sourceType '{SourceType}'.");
        }

        if (!Guid.TryParse(sourceDocumentId, out var originalId))
        {
            return (Guid.Empty, "REVERSAL requires sourceDocumentId to be the original transactionId (UUID).");
        }

        return (originalId, null);
    }

    /// <summary>Checks the request against the original it claims to reverse.</summary>
    public static string? MatchOriginal(
        InventoryTransaction original, Guid itemId, Guid skuId, SkuLevel skuLevel, decimal quantity, string uomId)
    {
        if (original.MovementType == MovementType.Reversal)
        {
            return "A REVERSAL cannot itself be reversed.";
        }

        if (original.ItemId != itemId || original.SkuId != skuId || original.SkuLevel != skuLevel)
        {
            return "REVERSAL itemId/skuId/skuLevel must equal the original transaction.";
        }

        if (original.Quantity != quantity || !string.Equals(original.UomId, uomId, StringComparison.Ordinal))
        {
            return "REVERSAL quantity and uomId must equal the original transaction.";
        }

        return null;
    }

    public static MovementDirection DirectionOf(InventoryTransaction transaction) => new(
        transaction.FromWarehouseId,
        transaction.FromLocationId,
        transaction.FromStockStatus,
        transaction.ToWarehouseId,
        transaction.ToLocationId,
        transaction.ToStockStatus);
}
