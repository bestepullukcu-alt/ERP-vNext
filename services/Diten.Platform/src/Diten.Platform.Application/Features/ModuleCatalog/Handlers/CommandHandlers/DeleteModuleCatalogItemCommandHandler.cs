using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class DeleteModuleCatalogItemCommandHandler : IRequestHandler<DeleteModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly IModuleCatalogRepository _repository;

    public DeleteModuleCatalogItemCommandHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteModuleCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module catalog item not found.", 404);
        }

        if (item.IsCoreModule)
        {
            return Response<NoContent>.Fail("Core modules cannot be deleted.", 400);
        }

        // MC-4 — a self-registered (code-owned) module must be removed from its manifest, not deleted manually.
        if (item.Origin == ModuleCatalogOrigin.SelfRegistered)
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        await _repository.DeleteAsync(item.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
