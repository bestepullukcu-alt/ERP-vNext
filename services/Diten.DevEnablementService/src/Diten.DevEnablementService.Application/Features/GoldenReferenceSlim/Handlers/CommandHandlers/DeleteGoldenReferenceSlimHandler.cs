using Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Handlers.CommandHandlers;

public sealed class DeleteGoldenReferenceSlimHandler : IRequestHandler<DeleteGoldenReferenceSlimCommand, Response<bool>>
{
    private readonly IGoldenReferenceSlimRepository _repository;

    public DeleteGoldenReferenceSlimHandler(IGoldenReferenceSlimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteGoldenReferenceSlimCommand request, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(request.Id, cancellationToken);
        if (!result)
        {
            return Response<bool>.Fail("RecordNotFound");
        }

        return Response<bool>.Success(true);
    }
}
