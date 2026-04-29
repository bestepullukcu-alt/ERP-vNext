using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Handlers.CommandHandlers;

public sealed class DeleteGoldenReferenceCompactHandler : IRequestHandler<DeleteGoldenReferenceCompactCommand, Response<bool>>
{
    private readonly IGoldenReferenceCompactRepository _repository;

    public DeleteGoldenReferenceCompactHandler(IGoldenReferenceCompactRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteGoldenReferenceCompactCommand request, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(request.Id, cancellationToken);
        if (!result)
        {
            return Response<bool>.Fail("RecordNotFound");
        }

        return Response<bool>.Success(true);
    }
}
