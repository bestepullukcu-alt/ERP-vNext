using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateModuleCatalogItemCommandHandler : IRequestHandler<UpdateModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly IModuleCatalogRepository _repository;

    public UpdateModuleCatalogItemCommandHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(UpdateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module catalog item not found.", 404);
        }

        var nextStatus = Enum.Parse<ModuleCatalogStatus>(request.Request.Status, ignoreCase: false);
        if (item.Status == ModuleCatalogStatus.Deprecated && !IsDeprecatedMetadataOnlyUpdate(request, item))
        {
            return Response<NoContent>.Fail("Deprecated module catalog items are read-only except DisplayName, Description and SortOrder.", 400);
        }

        if (item.Status != nextStatus && !IsAllowedTransition(item.Status, nextStatus))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {item.Status} to {nextStatus}.", 400);
        }

        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        if (await _repository.ExistsByCodeAsync(canonicalCode, item.Id, ct))
        {
            return Response<NoContent>.Fail("ModuleCode already exists.", 409);
        }

        item.ModuleCode = canonicalCode;
        item.ModuleName = request.Request.ModuleName.Trim();
        item.DisplayName = request.Request.DisplayName.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        item.Domain = request.Request.Domain.Trim();
        item.Service = request.Request.Service.Trim();
        item.Category = string.IsNullOrWhiteSpace(request.Request.Category) ? null : request.Request.Category.Trim();
        item.Status = nextStatus;
        item.ModuleVersion = request.Request.ModuleVersion.Trim();
        item.IsCoreModule = request.Request.IsCoreModule;
        item.IsTenantAssignable = request.Request.IsTenantAssignable;
        item.SortOrder = request.Request.SortOrder ?? 0;

        await _repository.UpdateAsync(item, ct);
        return Response<NoContent>.Success(204);
    }

    private static bool IsAllowedTransition(ModuleCatalogStatus current, ModuleCatalogStatus next) =>
        (current, next) is
        (ModuleCatalogStatus.Draft, ModuleCatalogStatus.Active) or
        (ModuleCatalogStatus.Active, ModuleCatalogStatus.Inactive) or
        (ModuleCatalogStatus.Inactive, ModuleCatalogStatus.Active) or
        (ModuleCatalogStatus.Active, ModuleCatalogStatus.Deprecated);

    private static bool IsDeprecatedMetadataOnlyUpdate(UpdateModuleCatalogItemCommand request, Diten.Platform.Domain.Entities.ModuleCatalogItem item)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        return canonicalCode == item.ModuleCode
            && request.Request.ModuleName.Trim() == item.ModuleName
            && request.Request.Domain.Trim() == item.Domain
            && request.Request.Service.Trim() == item.Service
            && NormalizeNullable(request.Request.Category) == NormalizeNullable(item.Category)
            && request.Request.Status == item.Status.ToString()
            && request.Request.ModuleVersion.Trim() == item.ModuleVersion
            && request.Request.IsCoreModule == item.IsCoreModule
            && request.Request.IsTenantAssignable == item.IsTenantAssignable;
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
