using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Diten.Platform.Application.Features.GlobalApplicability;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateModuleCatalogItemCommandHandler : IRequestHandler<UpdateModuleCatalogItemCommand, Response<NoContent>>
{
    private readonly ITransactionalModuleCatalogRepository _repository;
    private readonly Services.IModuleTaxonomyResolver _taxonomyResolver;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public UpdateModuleCatalogItemCommandHandler(
        ITransactionalModuleCatalogRepository repository,
        Services.IModuleTaxonomyResolver taxonomyResolver, IGlobalApplicabilityTransactionCoordinator transaction,
        IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _taxonomyResolver = taxonomyResolver;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(UpdateModuleCatalogItemCommand request, CancellationToken ct)
    {
        // FIX-DOMAIN-SERVICE-CANONICAL — resolve to canonical lookup Codes up front; used for both the
        // deprecated-metadata-only check and the persisted values so a DisplayName/enum-name is never stored.
        var domain = await _taxonomyResolver.ResolveDomainCodeAsync(request.Request.Domain, ct);
        var service = await _taxonomyResolver.ResolveServiceCodeAsync(request.Request.Service, ct);

        var nextStatus = Enum.Parse<ModuleCatalogStatus>(request.Request.Status, ignoreCase: false);
        return await _transaction.ExecuteAsync<Response<NoContent>>(
            new(nameof(UpdateModuleCatalogItemCommand), AuditOperation.Update, "ModuleCatalogItem", request.Id),
            async (session, transactionCt) =>
            {
        var item = await _repository.GetByIdAsync(session, request.Id, transactionCt);
        if (item is null) return new(Response<NoContent>.Fail("Module catalog item not found.", 404), false);

        // FIX-BASELINE-NO-DEACTIVATE — a baseline module must stay Active (it reaches every tenant automatically);
        // moving it off Active via edit would break RBAC/settings for ALL tenants. Refused (authoritative).
        if (item.IsBaseline && nextStatus != ModuleCatalogStatus.Active)
        {
            return new(Response<NoContent>.Fail(ModuleCatalogErrorCodes.BaselineCannotBeDeactivated, 409), false);
        }

        if (item.Status == ModuleCatalogStatus.Deprecated && !IsDeprecatedMetadataOnlyUpdate(request, item, domain, service))
        {
            return new(Response<NoContent>.Fail("Deprecated module catalog items are read-only except DisplayName, Description and SortOrder.", 400), false);
        }

        if (item.Status != nextStatus && !IsAllowedTransition(item.Status, nextStatus))
        {
            return new(Response<NoContent>.Fail($"Invalid status transition from {item.Status} to {nextStatus}.", 400), false);
        }

        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.Request.ModuleCode);
        if (!string.Equals(canonicalCode, item.ModuleCode, StringComparison.Ordinal))
        {
            return new(Response<NoContent>.Fail("ModuleCode cannot be changed after creation.", 400), false);
        }

        if (await _repository.ExistsByCodeAsync(session, canonicalCode, item.Id, transactionCt))
        {
            return new(Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleCodeInUse, 409), false);
        }

        // MC-4 — a self-registered (code-owned) module's HARD identity (ModuleName/ModuleVersion) is manifest-owned
        // and refreshed on re-push. Operators may still edit SOFT fields (Domain/Service/DisplayName/SortOrder/
        // IsTenantAssignable/Status/Description), but a manual HARD-field change is refused.
        if (item.Origin == ModuleCatalogOrigin.SelfRegistered
            && (request.Request.ModuleName.Trim() != item.ModuleName
                || request.Request.ModuleVersion.Trim() != item.ModuleVersion))
        {
            return new(Response<NoContent>.Fail(ModuleCatalogErrorCodes.ModuleManagedByCode, 409), false);
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
        // FIX-MODULE-ICON — SOFT + operator-owned: the admin form is the ONLY place the icon changes (self-registration
        // seeds it once, never re-writes). Allowed for self-registered modules too (icon is not a HARD field).
        item.Icon = string.IsNullOrWhiteSpace(request.Request.Icon) ? null : request.Request.Icon.Trim();

        await _repository.UpdateAsync(session, item, transactionCt);
        return new(Response<NoContent>.Success(204), true,
            (s, version, token) => _state.UpsertModuleCatalogAsync(s, item, version, token));
            }, ct);
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
