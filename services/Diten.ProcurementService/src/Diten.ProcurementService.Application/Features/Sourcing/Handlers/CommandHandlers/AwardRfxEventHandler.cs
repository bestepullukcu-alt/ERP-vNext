using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;

/// <summary>
/// awardRfxEvent: kazanan bid seçilir. awardedBidId RFx'e ait mevcut bir bid olmalı (yabancı/olmayan → 404 NOT_FOUND,
/// sessiz overwrite YOK). RFx status ∈ {Published, Evaluating} (aksi 409 INVALID_STATE). awardedSupplierId sunucu
/// tarafından bid'den çözülür (payload'dan alınmaz). Idempotent (Idempotency-Key). Cross-tenant/LE → 404 NOT_FOUND.
/// </summary>
public sealed class AwardRfxEventHandler : IRequestHandler<AwardRfxEventCommand, Response<AwardDecisionDto>>
{
    private readonly IRfxRepository _repository;

    public AwardRfxEventHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<AwardDecisionDto>> Handle(AwardRfxEventCommand request, CancellationToken cancellationToken)
    {
        var rfx = await _repository.GetByRfxIdAsync(request.RfxId, cancellationToken);
        if (rfx is null)
        {
            return Response<AwardDecisionDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent award (MOD-0145 §8): aynı key ile replay → mevcut award'ı döner, yeniden işlemez ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(rfx.AwardIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal)
            && rfx.Award is not null)
        {
            return Response<AwardDecisionDto>.Success(SourcingMapping.ToAwardDto(rfx.RfxId, rfx.Award));
        }

        // ── State gate: award yalnız Published/Evaluating'den (aksi 409) ──
        if (rfx.Status is not (RfxStatus.Published or RfxStatus.Evaluating))
        {
            return Response<AwardDecisionDto>.Fail("INVALID_STATE", 409);
        }

        // ── ≥1 bid zorunlu: hiç teklif yoksa award edilemez → 409 INVALID_STATE ──
        var bids = await _repository.GetBidsByRfxIdAsync(rfx.RfxId, cancellationToken);
        if (bids.Count == 0)
        {
            return Response<AwardDecisionDto>.Fail("INVALID_STATE", 409);
        }

        // ── Bid ownership gate: awardedBidId bu RFx'in mevcut bid'i olmalı (yabancı/olmayan → 404 NOT_FOUND) ──
        var bid = bids.FirstOrDefault(b => string.Equals(b.BidId, request.AwardedBidId, StringComparison.Ordinal));
        if (bid is null)
        {
            return Response<AwardDecisionDto>.Fail("NOT_FOUND", 404);
        }

        var expectedVersion = rfx.Version;
        var award = new AwardDecision
        {
            AwardedBidId = bid.BidId,
            AwardedSupplierId = bid.SupplierId, // server-resolved from bid
            Rationale = string.IsNullOrWhiteSpace(request.Rationale) ? null : request.Rationale!.Trim(),
            DecidedAt = DateTimeOffset.UtcNow
        };
        rfx.Award = award;
        rfx.Status = RfxStatus.Awarded;
        rfx.AwardIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var ok = await _repository.UpdateRfxAsync(rfx, expectedVersion, cancellationToken);
        if (!ok)
        {
            // Eşzamanlı değişim (stale) → award uygulanamadı.
            return Response<AwardDecisionDto>.Fail("INVALID_STATE", 409);
        }

        return Response<AwardDecisionDto>.Success(SourcingMapping.ToAwardDto(rfx.RfxId, award));
    }
}
