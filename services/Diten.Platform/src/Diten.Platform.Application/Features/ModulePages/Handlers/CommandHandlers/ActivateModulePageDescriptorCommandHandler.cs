using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class ActivateModulePageDescriptorCommandHandler : IRequestHandler<ActivateModulePageDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public ActivateModulePageDescriptorCommandHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(ActivateModulePageDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page descriptor not found.", 404);
        }

        if (descriptor.Status == ModulePageStatus.Deprecated)
        {
            return Response<NoContent>.Fail("Deprecated page descriptors cannot be activated.", 400);
        }

        descriptor.Status = ModulePageStatus.Active;
        await _repository.UpdateAsync(descriptor, ct);
        return Response<NoContent>.Success(204);
    }
}
