using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateModuleCatalogItemCommandHandler : IRequestHandler<UpdateModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly IModuleCatalogRepository _repository;
    private readonly Services.IModuleTaxonomyResolver _taxonomyResolver;

    public UpdateModuleCatalogItemCommandHandler(
        IModuleCatalogRepository repository,
        Services.IModuleTaxonomyResolver taxonomyResolver)
    {
        _repository = repository;
        _taxonomyResolver = taxonomyResolver;
    }

    public async Task<Response<NoContent>> Handle(UpdateModuleCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        if (item is null)
        {
            return Response<NoContent>.Fail("Module catalog item not found.", 404);
        }

        // FIX-DOMAIN-SERVICE-CANONICAL — resolve to canonical lookup Codes up front; used for both the
        // deprecated-metadata-only check and the persisted values so a DisplayName/enum-name is never stored.
        var domain = await _taxonomyResolver.ResolveDomainCodeAsync(request.Request.Domain, ct);
        var service = await _taxonomyResolver.ResolveServiceCodeAsync(request.Request.Service, ct);

        var nextStatus = Enum.Parse<ModuleCatalogStatus>(request.Request.Status, ignoreCase: false);
        if (item.Status == ModuleCatalogStatus.Deprecated && !IsDeprecatedMetadataOnlyUpdate(request, item, domain, service))
        {
            return Response<NoContent>.Fail("Deprecated module catalog items are read-only except DisplayName, Description and SortOrder.", 400);
        }

        if (item.Status != nextStatus && !IsAllowedTransition(item.Status, nextStatus))
        {
            return Response<NoContent>.Fail($"Invalid status transition from {item.Status} to {nextStatus}.", 400);
        }

        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        if (!string.Equals(canonicalCode, item.ModuleCode, StringComparison.Ordinal))
        {
            return Response<NoContent>.Fail("ModuleCode cannot be changed after creation.", 400);
        }

        if (await _repository.ExistsByCodeAsync(canonicalCode, item.Id, ct))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409);
        }

        // MC-4 — a self-registered (code-owned) module's HARD identity (ModuleName/ModuleVersion) is manifest-owned
        // and refreshed on re-push. Operators may still edit SOFT fields (Domain/Service/DisplayName/SortOrder/
        // IsTenantAssignable/Status/Description), but a manual HARD-field change is refused.
        if (item.Origin == ModuleCatalogOrigin.SelfRegistered
            && (request.Request.ModuleName.Trim() != item.ModuleName
                || request.Request.ModuleVersion.Trim() != item.ModuleVersion))
        {
            return Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409);
        }

        item.ModuleName = request.Request.ModuleName.Trim();
        item.DisplayName = request.Request.DisplayName.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        item.Domain = domain;
        item.Service = service;
        item.Status = nextStatus;
        item.ModuleVersion = request.Request.ModuleVersion.Trim();
        item.IsCoreModule = request.Request.IsCoreModule;
        item.IsTenantAssignable = request.Request.IsTenantAssignable;
        item.SortOrder = request.Request.SortOrder ?? 0;

        await _repository.UpdateAsync(item, ct);
        return Response<NoContent>.Success(204);
    }

    // MC-1b — approved promotion-only lifecycle: Draft→Preview→Beta→Active→Inactive⇄Active, Active→Deprecated,
    // plus forward-jumps (Draft→Beta/Active, Preview→Active). No demotion (e.g. Beta→Preview, Active→Draft).
    private static bool IsAllowedTransition(ModuleCatalogStatus current, ModuleCatalogStatus next) =>
        (current, next) is
        (ModuleCatalogStatus.Draft, ModuleCatalogStatus.Preview) or
        (ModuleCatalogStatus.Draft, ModuleCatalogStatus.Beta) or
        (ModuleCatalogStatus.Draft, ModuleCatalogStatus.Active) or
        (ModuleCatalogStatus.Preview, ModuleCatalogStatus.Beta) or
        (ModuleCatalogStatus.Preview, ModuleCatalogStatus.Active) or
        (ModuleCatalogStatus.Beta, ModuleCatalogStatus.Active) or
        (ModuleCatalogStatus.Active, ModuleCatalogStatus.Inactive) or
        (ModuleCatalogStatus.Inactive, ModuleCatalogStatus.Active) or
        (ModuleCatalogStatus.Active, ModuleCatalogStatus.Deprecated);

    private static bool IsDeprecatedMetadataOnlyUpdate(UpdateModuleCatalogItemCommand request, Diten.Platform.Domain.Entities.ModuleCatalogItem item, string resolvedDomain, string resolvedService)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        return canonicalCode == item.ModuleCode
            && request.Request.ModuleName.Trim() == item.ModuleName
            && resolvedDomain == item.Domain
            && resolvedService == item.Service
            && request.Request.Status == item.Status.ToString()
            && request.Request.ModuleVersion.Trim() == item.ModuleVersion
            && request.Request.IsCoreModule == item.IsCoreModule
            && request.Request.IsTenantAssignable == item.IsTenantAssignable;
    }
}
