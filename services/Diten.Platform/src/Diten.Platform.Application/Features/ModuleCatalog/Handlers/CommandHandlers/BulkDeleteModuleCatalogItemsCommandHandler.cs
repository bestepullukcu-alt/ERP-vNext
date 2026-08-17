using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class BulkDeleteModuleCatalogItemsCommandHandler : IRequestHandler<BulkDeleteModuleCatalogItemsCommand, Response<NoContent>>
{
    private readonly IModuleCatalogRepository _repository;

    public BulkDeleteModuleCatalogItemsCommandHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(BulkDeleteModuleCatalogItemsCommand request, CancellationToken ct)
    {
        if (request.Ids.Count == 0)
        {
            return Response<NoContent>.Fail("At least one module catalog item id is required.", 400);
        }

        foreach (var id in request.Ids.Distinct())
        {
            var item = await _repository.GetByIdAsync(id, ct);
            if (item is null)
            {
                return Response<NoContent>.Fail("Module catalog item not found.", 404);
            }

            if (item.IsCoreModule)
            {
                return Response<NoContent>.Fail("Core modules cannot be deleted.", 400);
            }
        }

        foreach (var id in request.Ids.Distinct())
        {
            await _repository.DeleteAsync(id, ct);
        }

        return Response<NoContent>.Success(204);
    }
}
