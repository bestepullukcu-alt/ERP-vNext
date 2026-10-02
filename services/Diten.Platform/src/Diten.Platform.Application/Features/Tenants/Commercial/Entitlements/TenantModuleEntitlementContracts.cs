using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;

public sealed record TenantModuleEntitlementRowDto(
    Guid TenantId,
    string ModuleCode,
    string ModuleName,
    string DisplaySource,
    Guid? PhysicalEntitlementId,
    bool IsEnabled,
    DateTimeOffset? ExpiryDateUtc,
    string EffectiveAccess,
    string? Reason,
    bool IsProjectionRow,
    bool HasManualOverride,
    DateTimeOffset? LastUpdatedAtUtc,
    byte[]? RowVersion,
    /// <summary>
    /// BL-500 — what the Modules tab may offer on this row, in order (the first is the primary action). Decided by
    /// <see cref="TenantModuleEntitlementRowActions"/>; the screen draws these and nothing else. Trailing and
    /// optional only so the callers that build a row for other purposes keep compiling: the list query always sets
    /// it, and a row without it offers nothing.
    /// </summary>
    IReadOnlyList<string>? AllowedActions = null);

// FIX-3 — S2S projection for AuthService's catalog-key-driven entitlement → permission sync. For each
// effectively-Active entitled module, carries the permission keys the module DECLARES in the page/action
// descriptor catalog (page RequiredPermission ∪ action PermissionKey). PermissionKeys may be empty when a
// module ships no descriptors yet — the AuthService caller then falls back to its convention resolver.
public sealed record TenantEntitledModulePermissionsDto(
    string ModuleCode,
    IReadOnlyList<string> PermissionKeys);

public sealed record TenantModuleEffectiveAccessDto(
    Guid TenantId,
    string ModuleCode,
    string ModuleName,
    string Source,
    TenantModuleEffectiveAccess EffectiveAccess,
    bool HasAccess,
    string? Reason,
    DateTimeOffset? ExpiryDateUtc);

public sealed record TenantVisibleModuleDto(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description);

public sealed record TenantAvailableModuleDto(
    string ModuleCode,
    string ModuleName,
    string DisplayName);

public sealed record TenantModuleEntitlementRequest(
    string ModuleCode,
    EntitlementSource Source,
    bool IsEnabled,
    DateTimeOffset? ExpiryDateUtc,
    string? Reason,
    byte[]? RowVersion);

public sealed record DisableTenantModuleEntitlementRequest(
    string ModuleCode,
    Guid? PhysicalEntitlementId,
    string Reason,
    byte[]? RowVersion);

public sealed record UpdateTenantModuleEntitlementExpiryRequest(
    DateTimeOffset? ExpiryDateUtc,
    string? Reason,
    byte[]? RowVersion);

public sealed record RemoveTenantManualModuleOverrideRequest(byte[]? RowVersion);
