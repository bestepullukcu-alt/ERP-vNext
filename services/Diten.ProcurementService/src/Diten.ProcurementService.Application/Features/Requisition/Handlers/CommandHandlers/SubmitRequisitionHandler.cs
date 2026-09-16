using Diten.ProcurementService.Application.Features.Requisition.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using RequisitionStatusEnum = Diten.ProcurementService.Domain.Entities.RequisitionStatus;

namespace Diten.ProcurementService.Application.Features.Requisition.Handlers.CommandHandlers;

/// <summary>
/// submitRequisition: Draft→Submitted. Draft dışı → 409 INVALID_STATE. Submit'te MOD-0023 workflowInstanceId atanır
/// (server-side seam; bu dilimde iç kimlik üretilir). Idempotent: aynı Idempotency-Key ile replay → yeniden
/// göndermez, mevcut durumu döner (yan-etkisiz). Cross-tenant/LE → 404 NOT_FOUND.
/// </summary>
public sealed class SubmitRequisitionHandler : IRequestHandler<SubmitRequisitionCommand, Response<RequisitionDto>>
{
    private readonly IRequisitionRepository _repository;

    public SubmitRequisitionHandler(IRequisitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<RequisitionDto>> Handle(SubmitRequisitionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByRequisitionIdAsync(request.RequisitionId, cancellationToken);
        if (entity is null)
        {
            return Response<RequisitionDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent submit (MOD-0141 §8): aynı key ile replay → mevcut durumu döner, yeniden işlemez ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(entity.SubmitIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<RequisitionDto>.Success(RequisitionMapping.ToDto(entity));
        }

        // ── State gate: yalnız Draft → Submitted ──
        if (entity.Status != RequisitionStatusEnum.Draft)
        {
            return Response<RequisitionDto>.Fail("INVALID_STATE", 409);
        }

        var expectedVersion = entity.Version;
        entity.Status = RequisitionStatusEnum.Submitted;
        // MOD-0023 workflow seam: bu dilimde iç workflow-instance kimliği server-side üretilir (dış kimlik uydurulmaz).
        entity.WorkflowInstanceId ??= GenerateWorkflowInstanceId();
        entity.SubmitIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var ok = await _repository.UpdateAsync(entity, expectedVersion, cancellationToken);
        if (!ok)
        {
            // Eşzamanlı değişim (stale) → geçiş uygulanamadı.
            return Response<RequisitionDto>.Fail("INVALID_STATE", 409);
        }

        return Response<RequisitionDto>.Success(RequisitionMapping.ToDto(entity));
    }

    private static string GenerateWorkflowInstanceId()
        => "WF-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
