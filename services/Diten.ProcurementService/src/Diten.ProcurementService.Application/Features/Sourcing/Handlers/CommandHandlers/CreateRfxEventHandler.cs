using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Application.Features.Sourcing.Validators;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;

/// <summary>
/// createRfxEvent (Draft). Idempotent (Idempotency-Key). invitedSupplierIds MOD-0140'ta (fail-closed), line.itemId
/// MOD-0290'da (progressive seam, fail-closed) doğrulanır → bilinmeyen referans 404 UNKNOWN_REFERENCE. İş kuralı
/// (≥1 satır, Decimal/float) 422 VALIDATION_FAILED (contract Unprocessable).
/// </summary>
public sealed class CreateRfxEventHandler : IRequestHandler<CreateRfxEventCommand, Response<RfxEventDto>>
{
    private readonly IRfxRepository _repository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductReferenceValidator _productValidator;

    public CreateRfxEventHandler(
        IRfxRepository repository,
        ISupplierRepository supplierRepository,
        IProductReferenceValidator productValidator)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _productValidator = productValidator;
    }

    public async Task<Response<RfxEventDto>> Handle(CreateRfxEventCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotent create (MOD-0145 §8): aynı Idempotency-Key ile replay → mevcut kaydı döner, yeni yaratmaz ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var replay = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return Response<RfxEventDto>.Success(SourcingMapping.ToRfxDto(replay), 201);
            }
        }

        var lines = request.Lines ?? new List<RfxLineInput>();

        // ── İş kuralı (contract Unprocessable 422): ≥1 satır + Decimal string (float YASAK) ──
        if (lines.Count == 0)
        {
            return Response<RfxEventDto>.Fail("VALIDATION_FAILED", 422);
        }
        if (lines.Any(l => !SourcingValidationRules.IsPositiveDecimal(l.Quantity)
                           || string.IsNullOrWhiteSpace(l.ItemId)
                           || string.IsNullOrWhiteSpace(l.UomId)))
        {
            return Response<RfxEventDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── PRODUCT-MASTER (0290) consume — fail-closed: bilinmeyen itemId → 404 UNKNOWN_REFERENCE ──
        var itemIds = lines.Select(l => l.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var unknownItems = await _productValidator.GetUnknownItemIdsAsync(itemIds, cancellationToken);
        if (unknownItems.Count > 0)
        {
            return Response<RfxEventDto>.Fail("UNKNOWN_REFERENCE", 404);
        }

        // ── SUPPLIER (0140) consume — fail-closed: bilinmeyen invited supplier → 404 UNKNOWN_REFERENCE ──
        var invited = (request.InvitedSupplierIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (invited.Count > 0)
        {
            var knownSuppliers = await _supplierRepository.GetBySupplierIdsAsync(invited, cancellationToken);
            var knownSet = knownSuppliers.Select(s => s.SupplierId).ToHashSet(StringComparer.Ordinal);
            if (invited.Any(id => !knownSet.Contains(id)))
            {
                return Response<RfxEventDto>.Fail("UNKNOWN_REFERENCE", 404);
            }
        }

        var entity = new RfxEvent
        {
            RfxId = GenerateRfxId(),
            Type = request.Type,
            Title = request.Title.Trim(),
            Status = RfxStatus.Draft,
            ClosesAt = request.ClosesAt,
            InvitedSupplierIds = invited,
            Lines = lines.Select(l => new RfxLine { ItemId = l.ItemId, Quantity = l.Quantity.Trim(), UomId = l.UomId }).ToList(),
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<RfxEventDto>.Success(SourcingMapping.ToRfxDto(created), 201);
    }

    private static string GenerateRfxId()
        => "RFX-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
