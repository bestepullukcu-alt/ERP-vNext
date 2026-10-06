using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using InvoiceStatusEnum = Diten.ProcurementService.Domain.Entities.InvoiceStatus;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.CommandHandlers;

/// <summary>
/// Soft delete tek Invoice. Yalnız Captured silinebilir; eşleşmiş/çözülmüş fatura silinemez (→ 409 INVALID_STATE) —
/// match outcome/approval kalıcıdır (append-only). Cross-tenant/LE → 404 NOT_FOUND. Hard delete YOK.
/// </summary>
public sealed class DeleteInvoiceHandler : IRequestHandler<DeleteInvoiceCommand, Response<bool>>
{
    private readonly IInvoiceMatchRepository _repository;

    public DeleteInvoiceHandler(IInvoiceMatchRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteInvoiceCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        if (entity.Status != InvoiceStatusEnum.Captured)
        {
            return Response<bool>.Fail("INVALID_STATE", 409);
        }

        var ok = await _repository.DeleteAsync(entity.Id, cancellationToken);
        if (!ok)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        return Response<bool>.Success(true);
    }
}
