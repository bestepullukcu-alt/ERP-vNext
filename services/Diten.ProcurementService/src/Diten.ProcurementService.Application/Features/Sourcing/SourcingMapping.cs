using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Sourcing;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class SourcingMapping
{
    public static RfxEventDto ToRfxDto(RfxEvent e) => new(
        e.Id,
        e.RfxId,
        e.Type,
        e.Title,
        e.Status,
        e.ClosesAt,
        e.InvitedSupplierIds.ToList(),
        e.Lines.Select(l => new RfxLineDto(l.ItemId, l.Quantity, l.UomId)).ToList(),
        e.Version,
        SourcingContract.Version);

    public static BidDto ToBidDto(Bid b) => new(
        b.BidId,
        b.RfxId,
        b.SupplierId,
        b.Lines.Select(l => new BidLineDto(l.ItemId, l.UnitPrice, l.LeadTimeDays)).ToList(),
        b.EvaluationScore,
        SourcingContract.Version);

    public static AwardDecisionDto ToAwardDto(string rfxId, AwardDecision a) => new(
        rfxId,
        a.AwardedBidId,
        a.AwardedSupplierId,
        a.Rationale,
        a.DecidedAt,
        SourcingContract.Version);
}
