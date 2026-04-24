using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Handlers;

public sealed class DeleteGoldenReferenceItemHandler : IRequestHandler<DeleteGoldenReferenceItemCommand, Response<bool>>
{
    private readonly IGoldenReferenceItemRepository _repository;

    public DeleteGoldenReferenceItemHandler(IGoldenReferenceItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteGoldenReferenceItemCommand request, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(request.Id, cancellationToken);
        if (!result)
        {
            return Response<bool>.Fail("RecordNotFound");
        }

        return Response<bool>.Success(true);
    }
}
