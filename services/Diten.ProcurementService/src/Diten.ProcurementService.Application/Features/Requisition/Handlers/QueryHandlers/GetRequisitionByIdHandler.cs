using Diten.ProcurementService.Application.Features.Requisition.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Handlers.QueryHandlers;

public sealed class GetRequisitionByIdHandler
    : IRequestHandler<GetRequisitionByIdQuery, Response<RequisitionDto>>
{
    private readonly IRequisitionRepository _repository;

    public GetRequisitionByIdHandler(IRequisitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<RequisitionDto>> Handle(
        GetRequisitionByIdQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE erişim → repository null döner → 404 (NOT_FOUND; sızıntı yok).
        var entity = await _repository.GetByRequisitionIdAsync(request.RequisitionId, cancellationToken);
        if (entity is null)
        {
            return Response<RequisitionDto>.Fail("NOT_FOUND", 404);
        }

        return Response<RequisitionDto>.Success(RequisitionMapping.ToDto(entity));
    }
}
