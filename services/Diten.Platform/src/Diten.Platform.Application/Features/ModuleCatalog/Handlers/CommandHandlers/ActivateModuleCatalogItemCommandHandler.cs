using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class ActivateModuleCatalogItemCommandHandler : IRequestHandler<ActivateModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly IModuleCatalogRepository _repository;

    public ActivateModuleCatalogItemCommandHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(ActivateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module catalog item not found.", 404);
        }

        if (item.Status is not (ModuleCatalogStatus.Draft or ModuleCatalogStatus.Inactive))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {item.Status} to Active.", 400);
        }

        item.Status = ModuleCatalogStatus.Active;
        await _repository.UpdateAsync(item, ct);
        return Response<NoContent>.Success(204);
    }
}
