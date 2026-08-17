using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.CommandHandlers;

public sealed class BulkDeleteGoldenReferenceCompactHandler : IRequestHandler<BulkDeleteGoldenReferenceCompactCommand, Response<int>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public BulkDeleteGoldenReferenceCompactHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<int>> Handle(BulkDeleteGoldenReferenceCompactCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids == null || request.Ids.Count == 0)
        {
            return Response<int>.Fail("No identifiers provided for bulk deletion.");
        }

        var deletedCount = await _repository.BulkDeleteAsync(request.Ids, cancellationToken);
        return Response<int>.Success(deletedCount);
    }
}
