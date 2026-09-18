using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Application.Features.Sourcing.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;

/// <summary>
/// submitBid: teklif yalnız Published + closesAt öncesi RFx'e verilebilir (aksi 409 INVALID_STATE, kayıt YOK).
/// supplierId MOD-0140'ta doğrulanır + RFx invited listesinde olmalı; line.itemId MOD-0290'da (seam). Bilinmeyen
/// referans → 404 UNKNOWN_REFERENCE (fail-closed). Idempotent (Idempotency-Key). Cross-tenant/LE → 404 NOT_FOUND.
/// </summary>
public sealed class SubmitBidHandler : IRequestHandler<SubmitBidCommand, Response<BidDto>>
{
    private readonly IRfxRepository _repository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductReferenceValidator _productValidator;

    public SubmitBidHandler(
        IRfxRepository repository,
        ISupplierRepository supplierRepository,
        IProductReferenceValidator productValidator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _productValidator = productValidator;
    }

    public async Task<Response<BidDto>> Handle(SubmitBidCommand request, CancellationToken cancellationToken)
    {
        var rfx = await _repository.GetByRfxIdAsync(request.RfxId, cancellationToken);
        if (rfx is null)
        {
            return Response<BidDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent bid (MOD-0145 §8): aynı key ile replay → mevcut bid'i döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetBidByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null && string.Equals(replay.RfxId, rfx.RfxId, StringComparison.Ordinal))
            {
                return Response<BidDto>.Success(SourcingMapping.ToBidDto(replay), 201);
            }
        }

        // ── State gate: yalnız Published + closesAt geçmemiş (aksi 409, teklif kaydı YOK) ──
        var closed = rfx.ClosesAt.HasValue && DateTimeOffset.UtcNow > rfx.ClosesAt.Value;
        if (rfx.Status != RfxStatus.Published || closed)
        {
            return Response<BidDto>.Fail("INVALID_STATE", 409);
        }

        var lines = request.Lines ?? new List<BidLineInput>();

        // ── İş kuralı (contract Unprocessable 422): ≥1 satır + Decimal (float YASAK) ──
        if (lines.Count == 0)
        {
            return Response<BidDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Any(l => !SourcingValidationRules.IsNonNegativeDecimal(l.UnitPrice)
                           || string.IsNullOrWhiteSpace(l.ItemId)
                           || (l.LeadTimeDays is not null && l.LeadTimeDays < 0)))
        {
            return Response<BidDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── PRODUCT-MASTER (0290) consume — fail-closed: bilinmeyen itemId → 404 UNKNOWN_REFERENCE ──
        var itemIds = lines.Select(l => l.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var unknownItems = await _productValidator.GetUnknownItemIdsAsync(itemIds, cancellationToken);
        if (unknownItems.Count > 0)
        {
            return Response<BidDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── SUPPLIER (0140) consume — fail-closed: bilinmeyen supplier → 404 UNKNOWN_REFERENCE ──
        var supplierId = (request.SupplierId ?? string.Empty).Trim();
        var known = await _supplierRepository.GetBySupplierIdsAsync(new List<string> { supplierId }, cancellationToken);
        if (known.All(s => !string.Equals(s.SupplierId, supplierId, StringComparison.Ordinal)))
        {
            return Response<BidDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── Invited-list zorunluluğu (MOD-0145 §12): invited listesi doluysa bidder o listede olmalı ──
        if (rfx.InvitedSupplierIds.Count > 0
            && !rfx.InvitedSupplierIds.Contains(supplierId, StringComparer.Ordinal))
        {
            return Response<BidDto>.Fail("VALIDATION_FAILED", 422);
        }

        var bid = new Bid
        {
            BidId = GenerateBidId(),
            RfxId = rfx.RfxId,
            SupplierId = supplierId,
            Lines = lines.Select(l => new BidLine { ItemId = l.ItemId, UnitPrice = l.UnitPrice.Trim(), LeadTimeDays = l.LeadTimeDays }).ToList(),
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateBidAsync(bid, cancellationToken);
        return Response<BidDto>.Success(SourcingMapping.ToBidDto(created), 201);
    }

    private static string GenerateBidId()
        => "BID-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
