using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Handlers.CommandHandlers;

public sealed class BulkDeleteGoldenReferenceSlimHandler : IRequestHandler<BulkDeleteGoldenReferenceSlimCommand, Response<int>>
{
    private readonly IGoldenReferenceSlimRepository _repository;

    public BulkDeleteGoldenReferenceSlimHandler(IGoldenReferenceSlimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteGoldenReferenceSlimCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids == null || request.Ids.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        var deletedCount = await _repository.BulkDeleteAsync(request.Ids, cancellationToken);
        return Response<int>.Success(deletedCount);
    }
}
