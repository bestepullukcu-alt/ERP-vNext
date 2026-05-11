using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeleteModulePageActionDescriptorCommandHandler
    : IRequestHandler<DeleteModulePageActionDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageActionDescriptorRepository _repository;

    public DeleteModulePageActionDescriptorCommandHandler(IModulePageActionDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModulePageActionDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page action descriptor not found.", 404);
        }

        await _repository.DeleteAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
