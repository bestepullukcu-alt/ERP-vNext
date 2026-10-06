using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using PoStatusEnum = Diten.ProcurementService.Domain.Entities.PoStatus;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.CommandHandlers;

/// <summary>
/// approvePurchaseOrder: Draft→Approved. Draft dışı → 409 INVALID_STATE. Approve'da MOD-0023 workflowInstanceId
/// atanır (server-side seam). Idempotent: aynı Idempotency-Key ile replay → yeniden onaylamaz, mevcut durumu döner
/// (yan-etkisiz). Cross-tenant/LE → 404 NOT_FOUND. Approved PO = G2A golden flow upstream belgesi (GRN 0142 okur).
/// </summary>
public sealed class ApprovePurchaseOrderHandler : IRequestHandler<ApprovePurchaseOrderCommand, Response<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _repository;

    public ApprovePurchaseOrderHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PurchaseOrderDto>> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByPoIdAsync(request.PoId, cancellationToken);
        if (entity is null)
        {
            return Response<PurchaseOrderDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent approve (MOD-0141 §8): aynı key ile replay → mevcut durumu döner, yeniden işlemez ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(entity.ApproveIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<PurchaseOrderDto>.Success(PurchaseOrderMapping.ToDto(entity));
        }

        // ── State gate: yalnız Draft → Approved ──
        if (entity.Status != PoStatusEnum.Draft)
        {
            return Response<PurchaseOrderDto>.Fail("INVALID_STATE", 409);
        }

        var expectedVersion = entity.Version;
        entity.Status = PoStatusEnum.Approved;
        // MOD-0023 workflow seam: bu dilimde iç workflow-instance kimliği server-side üretilir (dış kimlik uydurulmaz).
        entity.WorkflowInstanceId ??= GenerateWorkflowInstanceId();
        entity.ApproveIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var ok = await _repository.UpdateAsync(entity, expectedVersion, cancellationToken);
        if (!ok)
        {
            // Eşzamanlı değişim (stale) → geçiş uygulanamadı.
            return Response<PurchaseOrderDto>.Fail("INVALID_STATE", 409);
        }

        return Response<PurchaseOrderDto>.Success(PurchaseOrderMapping.ToDto(entity));
    }

    private static string GenerateWorkflowInstanceId()
        => "WF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
