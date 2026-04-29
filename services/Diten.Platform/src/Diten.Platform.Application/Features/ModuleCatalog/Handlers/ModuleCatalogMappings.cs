using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers;

internal static class ModuleCatalogMappings
{
    public static DomainLandscapeDto Map(DomainLandscape entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.Description, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public static SuitePlatformDto Map(SuitePlatform entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.DomainLandscapeId, entity.Description, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public static CapabilityGroupDto Map(CapabilityGroup entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.DomainLandscapeId, entity.SuitePlatformId, entity.Description, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);

    public static ModuleDefinitionListItemDto MapListItem(ModuleDefinition entity, DomainLandscape domain, SuitePlatform suite, CapabilityGroup capability) =>
        new(
            entity.Id,
            entity.ModuleId,
            entity.ModuleName,
            entity.DomainLandscapeId,
            entity.SuitePlatformId,
            entity.CapabilityGroupId,
            domain.Name,
            suite.Name,
            capability.Name,
            entity.Status.ToString(),
            entity.IsPlatformCore,
            entity.IsTenantAssignable,
            entity.SupportModel,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static ModuleDefinitionDetailDto MapDetail(ModuleDefinition entity, DomainLandscape domain, SuitePlatform suite, CapabilityGroup capability) =>
        new(
            entity.Id,
            entity.ModuleId,
            entity.ModuleName,
            entity.DomainLandscapeId,
            domain.Name,
            entity.SuitePlatformId,
            suite.Name,
            entity.CapabilityGroupId,
            capability.Name,
            entity.DependencyGate,
            entity.DeliveryOutcome,
            entity.Placement,
            entity.SupportModel,
            entity.Status.ToString(),
            entity.IsPlatformCore,
            entity.IsTenantAssignable,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static ModulePageDefinitionDto Map(ModulePageDefinition entity) =>
        new(
            entity.Id,
            entity.ModuleId,
            entity.PageCode,
            entity.PageName,
            entity.Description,
            entity.RoutePath,
            entity.PageType.ToString(),
            entity.RequiredPermissionKey,
            entity.IsNavigationCandidate,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string NormalizeModuleId(string value) => value.Trim().ToUpperInvariant();

    public static ModuleLifecycleStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ModuleLifecycleStatus.Active;
        }

        return Enum.TryParse<ModuleLifecycleStatus>(value.Trim(), true, out var status)
            ? status
            : throw new InvalidOperationException($"Module lifecycle status '{value}' is invalid.");
    }

    public static string ResolveActor(ICurrentUserContext currentUser) =>
        currentUser.IsAuthenticated && currentUser.UserId != Guid.Empty
            ? currentUser.UserId.ToString()
            : "system";

    public static string BuildSuiteKey(Guid domainId, string code) => $"{domainId:N}:{code}";

    public static string BuildCapabilityKey(Guid suiteId, string code) => $"{suiteId:N}:{code}";

    public static string? ValidateImportRow(ModuleCatalogImportRowDto row)
    {
        if (string.IsNullOrWhiteSpace(row.ModuleId))
        {
            return "ModuleId is required.";
        }

        if (string.IsNullOrWhiteSpace(row.ModuleName))
        {
            return "ModuleName is required.";
        }

        if (string.IsNullOrWhiteSpace(row.DomainLandscape))
        {
            return "Domain / Landscape is required.";
        }

        if (string.IsNullOrWhiteSpace(row.SuitePlatform))
        {
            return "Suite / Platform is required.";
        }

        if (string.IsNullOrWhiteSpace(row.CapabilityGroup))
        {
            return "Capability Group is required.";
        }

        return null;
    }

    public static bool ApplyModuleUpdate(
        ModuleDefinition module,
        Guid domainId,
        Guid suiteId,
        Guid capabilityId,
        string? dependencyGate,
        string? deliveryOutcome,
        string? placement,
        string? supportModel,
        string moduleName,
        ModuleLifecycleStatus status,
        bool isPlatformCore,
        bool isTenantAssignable,
        string actor)
    {
        var changed = false;
        if (!string.Equals(module.ModuleName, moduleName, StringComparison.Ordinal))
        {
            module.ModuleName = moduleName;
            changed = true;
        }

        if (module.DomainLandscapeId != domainId)
        {
            module.DomainLandscapeId = domainId;
            changed = true;
        }

        if (module.SuitePlatformId != suiteId)
        {
            module.SuitePlatformId = suiteId;
            changed = true;
        }

        if (module.CapabilityGroupId != capabilityId)
        {
            module.CapabilityGroupId = capabilityId;
            changed = true;
        }

        changed |= SetNullable(() => module.DependencyGate, value => module.DependencyGate = value, dependencyGate);
        changed |= SetNullable(() => module.DeliveryOutcome, value => module.DeliveryOutcome = value, deliveryOutcome);
        changed |= SetNullable(() => module.Placement, value => module.Placement = value, placement);
        changed |= SetNullable(() => module.SupportModel, value => module.SupportModel = value, supportModel);

        if (module.Status != status)
        {
            module.Status = status;
            changed = true;
        }

        if (module.IsPlatformCore != isPlatformCore)
        {
            module.IsPlatformCore = isPlatformCore;
            changed = true;
        }

        if (module.IsTenantAssignable != isTenantAssignable)
        {
            module.IsTenantAssignable = isTenantAssignable;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        module.UpdatedAt = DateTimeOffset.UtcNow;
        module.UpdatedBy = actor;
        module.Version++;
        return true;
    }

    private static bool SetNullable(Func<string?> getVal, Action<string?> setVal, string? newVal)
    {
        if (getVal() != newVal)
        {
            setVal(newVal);
            return true;
        }
        return false;
    }

    public static string NormalizePageCode(string value) => value.Trim().ToUpperInvariant();

    public static string? NormalizeRoutePath(string? value)
    {
        var normalized = NormalizeNullable(value);
        if (normalized == null)
        {
            return null;
        }

        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }

    public static ModulePageType ParsePageType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ModulePageType.Other;
        }

        return Enum.TryParse<ModulePageType>(value.Trim(), true, out var pageType)
            ? pageType
            : throw new InvalidOperationException($"PageType '{value}' is invalid.");
    }
}
