using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

public sealed class DeleteSupplierHandler : IRequestHandler<DeleteSupplierCommand, Response<bool>>
{
    private readonly ISupplierRepository _repository;

    public DeleteSupplierHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE → null → 404 (cross-LE delete engellenir).
        var entity = await _repository.GetBySupplierIdAsync(request.SupplierId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        // Soft delete zorunlu (hard delete YOK).
        var ok = await _repository.DeleteAsync(entity.Id, cancellationToken);
        if (!ok)
        {
            return Response<bool>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        return Response<bool>.Success(true);
    }
}
