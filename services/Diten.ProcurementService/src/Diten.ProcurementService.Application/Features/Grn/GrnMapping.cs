using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Grn;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.
/// Not: DTO'ya line.quantity mal-kabul DOKÜMANI miktarıdır; stok balance DEĞİL (envanter gerçeği MOD-0173'te).</summary>
internal static class GrnMapping
{
    public static GrnResponseDto ToDto(GoodsReceipt e) => new(
        e.Id,
        e.GrnId,
        e.PoId,
        e.Status,
        e.Lines.Select(l => new GrnLineResultDto(l.PoLineId, l.InventoryTransactionId, l.LotId, l.Quantity)).ToList(),
        e.ReceivedAt,
        e.Version,
        GrnContract.Version);
}
