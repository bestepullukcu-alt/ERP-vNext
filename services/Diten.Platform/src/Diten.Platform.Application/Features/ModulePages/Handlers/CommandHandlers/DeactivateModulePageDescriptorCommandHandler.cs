using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeactivateModulePageDescriptorCommandHandler : IRequestHandler<DeactivateModulePageDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageDescriptorRepository _repository;

    public DeactivateModulePageDescriptorCommandHandler(IModulePageDescriptorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeactivateModulePageDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page descriptor not found.", 404);
        }

        if (descriptor.Status == ModulePageStatus.Deprecated)
        {
            return Response<NoContent>.Fail("Deprecated page descriptors cannot be deactivated.", 400);
        }

        descriptor.Status = ModulePageStatus.Inactive;
        await _repository.UpdateAsync(descriptor, ct);
        return Response<NoContent>.Success(204);
    }
}
