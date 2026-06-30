using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeleteModulePageDescriptorCommandHandler : IRequestHandler<DeleteModulePageDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageDescriptorRepository _repository;
    private readonly IModuleCatalogRepository _catalogRepository;

    public DeleteModulePageDescriptorCommandHandler(
        IModulePageDescriptorRepository repository,
        IModuleCatalogRepository catalogRepository)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModulePageDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page descriptor not found.", 404);
        }

        // MC-7 — code-owned module: pages are reconciled from the manifest, not deleted by hand.
        if (await SelfRegisteredModuleGuard.IsManagedByCodeAsync(_catalogRepository, descriptor.ModuleCode, ct))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        await _repository.DeleteAsync(descriptor.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
