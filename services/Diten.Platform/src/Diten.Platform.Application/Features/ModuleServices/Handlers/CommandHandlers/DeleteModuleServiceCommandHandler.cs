using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleServices.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleServices.Handlers.CommandHandlers;

public sealed class DeleteModuleServiceCommandHandler : IRequestHandler<DeleteModuleServiceCommand, Response<NoContent>>
{
    private readonly IModuleServiceRepository _repository;

    public DeleteModuleServiceCommandHandler(IModuleServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModuleServiceCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module service not found.", 404);
        }

        await _repository.DeleteAsync(item.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
