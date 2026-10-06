using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;

/// <summary>
/// publishRfxEvent: Draft→Published. Draft dışı → 409 INVALID_STATE. Idempotent: aynı Idempotency-Key ile replay →
/// yeniden yayınlamaz, mevcut durumu döner (yan-etkisiz). Cross-tenant/LE → 404 NOT_FOUND.
/// </summary>
public sealed class PublishRfxEventHandler : IRequestHandler<PublishRfxEventCommand, Response<RfxEventDto>>
{
    private readonly IRfxRepository _repository;

    public PublishRfxEventHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<RfxEventDto>> Handle(PublishRfxEventCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByRfxIdAsync(request.RfxId, cancellationToken);
        if (entity is null)
        {
            return Response<RfxEventDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent publish (MOD-0145 §8): aynı key ile replay → mevcut durumu döner, yeniden işlemez ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(entity.PublishIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<RfxEventDto>.Success(SourcingMapping.ToRfxDto(entity));
        }

        // ── State gate: yalnız Draft → Published ──
        if (entity.Status != RfxStatus.Draft)
        {
            return Response<RfxEventDto>.Fail("INVALID_STATE", 409);
        }

        var expectedVersion = entity.Version;
        entity.Status = RfxStatus.Published;
        entity.PublishIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var ok = await _repository.UpdateRfxAsync(entity, expectedVersion, cancellationToken);
        if (!ok)
        {
            // Eşzamanlı değişim (stale) → geçiş uygulanamadı.
            return Response<RfxEventDto>.Fail("INVALID_STATE", 409);
        }

        return Response<RfxEventDto>.Success(SourcingMapping.ToRfxDto(entity));
    }
}
