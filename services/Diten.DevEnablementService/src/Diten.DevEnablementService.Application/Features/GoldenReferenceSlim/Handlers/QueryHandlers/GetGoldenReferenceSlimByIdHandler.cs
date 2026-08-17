using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Handlers.QueryHandlers;

public sealed class GetGoldenReferenceSlimByIdHandler : IRequestHandler<GetGoldenReferenceSlimByIdQuery, Response<GoldenReferenceSlimDetailDto>>
{
    private readonly IGoldenReferenceSlimRepository _repository;

    public GetGoldenReferenceSlimByIdHandler(IGoldenReferenceSlimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<GoldenReferenceSlimDetailDto>> Handle(GetGoldenReferenceSlimByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            return Response<GoldenReferenceSlimDetailDto>.Fail("Record not found.", 404);
        }

        var dto = new GoldenReferenceSlimDetailDto(entity.Id, entity.Code, entity.Name, entity.Description, entity.ReferenceType, entity.Priority, entity.IsActive);
        return Response<GoldenReferenceSlimDetailDto>.Success(dto);
    }
}
