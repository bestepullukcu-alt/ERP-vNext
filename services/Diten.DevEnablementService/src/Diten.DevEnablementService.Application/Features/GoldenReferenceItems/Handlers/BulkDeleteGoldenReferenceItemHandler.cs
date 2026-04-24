using Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Handlers;

public sealed class BulkDeleteGoldenReferenceItemHandler : IRequestHandler<BulkDeleteGoldenReferenceItemCommand, Response<int>>
{
    private readonly IGoldenReferenceItemRepository _repository;

    public BulkDeleteGoldenReferenceItemHandler(IGoldenReferenceItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteGoldenReferenceItemCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids == null || request.Ids.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        var deletedCount = await _repository.BulkDeleteAsync(request.Ids, cancellationToken);
        return Response<int>.Success(deletedCount);
    }
}
