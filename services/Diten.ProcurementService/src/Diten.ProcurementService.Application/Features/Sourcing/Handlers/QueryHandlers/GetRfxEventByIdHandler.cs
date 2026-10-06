using Diten.ProcurementService.Application.Features.Sourcing.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.QueryHandlers;

public sealed class GetRfxEventByIdHandler
    : IRequestHandler<GetRfxEventByIdQuery, Response<RfxEventDto>>
{
    private readonly IRfxRepository _repository;

    public GetRfxEventByIdHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<RfxEventDto>> Handle(
        GetRfxEventByIdQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE erişim → repository null döner → 404 (NOT_FOUND; sızıntı yok).
        var entity = await _repository.GetByRfxIdAsync(request.RfxId, cancellationToken);
        if (entity is null)
        {
            return Response<RfxEventDto>.Fail("NOT_FOUND", 404);
        }

        return Response<RfxEventDto>.Success(SourcingMapping.ToRfxDto(entity));
    }
}
