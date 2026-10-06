using Diten.ProcurementService.Application.Features.Grn.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Handlers.QueryHandlers;

/// <summary>getGrn (contract GET /api/grn/{grnId}). Cross-tenant/LE → repository null → 404 UNKNOWN_GRN (sızıntı yok).</summary>
public sealed class GetGrnByIdHandler : IRequestHandler<GetGrnByIdQuery, Response<GrnResponseDto>>
{
    private readonly IGrnRepository _repository;

    public GetGrnByIdHandler(IGrnRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<GrnResponseDto>> Handle(GetGrnByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByGrnIdAsync(request.GrnId, cancellationToken);
        if (entity is null)
        {
            return Response<GrnResponseDto>.Fail("UNKNOWN_GRN", 404);
        }

        return Response<GrnResponseDto>.Success(GrnMapping.ToDto(entity));
    }
}
