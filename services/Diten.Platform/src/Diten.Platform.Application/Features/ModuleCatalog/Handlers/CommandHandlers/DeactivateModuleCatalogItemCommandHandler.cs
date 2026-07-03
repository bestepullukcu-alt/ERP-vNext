using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class DeactivateModuleCatalogItemCommandHandler : IRequestHandler<DeactivateModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly IModuleCatalogRepository _repository;

    public DeactivateModuleCatalogItemCommandHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeactivateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module catalog item not found.", 404);
        }

        // FIX-BASELINE-NO-DEACTIVATE — a baseline module is entitlement-free (reaches every tenant). Deactivating it
        // would break RBAC/settings for ALL tenants and a re-push won't restore SOFT Status. Authoritative refusal.
        if (item.IsBaseline)
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.BaselineCannotBeDeactivated, 409);
        }

        if (item.Status != ModuleCatalogStatus.Active)
        {
            return Response<NoContent>.Fail($"Invalid status transition from {item.Status} to Inactive.", 400);
        }

        item.Status = ModuleCatalogStatus.Inactive;
        await _repository.UpdateAsync(item, ct);
        return Response<NoContent>.Success(204);
    }
}
