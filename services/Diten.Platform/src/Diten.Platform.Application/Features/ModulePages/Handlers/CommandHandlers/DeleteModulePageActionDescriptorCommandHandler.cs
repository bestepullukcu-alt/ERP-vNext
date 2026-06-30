using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;

public sealed class DeleteModulePageActionDescriptorCommandHandler
    : IRequestHandler<DeleteModulePageActionDescriptorCommand, Response<NoContent>>
{
    private readonly IModulePageActionDescriptorRepository _repository;
    private readonly IModuleCatalogRepository _catalogRepository;

    public DeleteModulePageActionDescriptorCommandHandler(
        IModulePageActionDescriptorRepository repository,
        IModuleCatalogRepository catalogRepository)
    {
        _repository = repository;
        _catalogRepository = catalogRepository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModulePageActionDescriptorCommand request, CancellationToken ct)
    {
        var descriptor = await _repository.GetByIdAsync(request.Id, ct);
        if (descriptor is null)
        {
            return Response<NoContent>.Fail("Module page action descriptor not found.", 404);
        }

        // MC-7 — code-owned module: actions are reconciled from the manifest, not deleted by hand.
        if (await SelfRegisteredModuleGuard.IsManagedByCodeAsync(_catalogRepository, descriptor.ModuleCode, ct))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        await _repository.DeleteAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
