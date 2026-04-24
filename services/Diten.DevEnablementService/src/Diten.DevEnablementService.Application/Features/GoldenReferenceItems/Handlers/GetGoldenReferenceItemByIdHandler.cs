using Diten.Shared.Core;
using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Queries;
using Diten.DevEnablementService.Domain.Repositories;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Handlers;

public sealed class GetGoldenReferenceItemByIdHandler : IRequestHandler<GetGoldenReferenceItemByIdQuery, Response<GoldenReferenceItemDetailDto>>
{
    private readonly IGoldenReferenceItemRepository _repository;

    public GetGoldenReferenceItemByIdHandler(IGoldenReferenceItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<GoldenReferenceItemDetailDto>> Handle(GetGoldenReferenceItemByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            return Response<GoldenReferenceItemDetailDto>.Fail("Record not found.", 404);
        }

        var dto = new GoldenReferenceItemDetailDto(entity.Id, entity.Code, entity.Name, entity.Description, entity.ReferenceType, entity.Priority, entity.IsActive);
        return Response<GoldenReferenceItemDetailDto>.Success(dto);
    }
}
