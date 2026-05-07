using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeleteModulePageDescriptorCommandHandler : IRequestHandler<DeleteModulePageDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public DeleteModulePageDescriptorCommandHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModulePageDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page descriptor not found.", 404);
        }

        await _repository.DeleteAsync(descriptor.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
