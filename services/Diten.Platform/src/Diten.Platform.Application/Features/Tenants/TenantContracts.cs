namespace Diten.Platform.Application.Features.Tenants;

public sealed record TenantListItemDto(
    Guid Id,
    string Code,
    string Name,
    string DisplayName,
    string Domain,
    string Region,
    string Environment,
    string Status,
    string ProvisioningStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string CreatedBy);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record TenantRegistryStatsDto(
    long Total,
    long Active,
    long Provisioning,
    long Suspended,
    long Deactivated);

public sealed record TenantProvisioningStepDto(
    string Key,
    string Label,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Detail);

public sealed record TenantActivityEventDto(
    string EventType,
    string Message,
    DateTimeOffset At,
    string? Actor);

public sealed record TenantOverviewMetricsDto(
    int ProvisioningStepCount,
    int CompletedProvisioningStepCount,
    int RecentActivityCount,
    string LifecycleStatus,
    bool IsOpenAppAvailable);

public sealed record TenantDetailDto(
    Guid Id,
    string Code,
    string Name,
    string DisplayName,
    string Domain,
    string Region,
    string Environment,
    string Status,
    string ProvisioningStatus,
    string Tier,
    string? AppUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string CreatedBy,
    TenantOverviewMetricsDto Overview,
    IReadOnlyList<TenantProvisioningStepDto> ProvisioningSteps,
    IReadOnlyList<TenantActivityEventDto> RecentActivity);

public sealed record TenantModulesSummaryDto(
    Guid TenantId,
    string Plan,
    IReadOnlyList<TenantModuleEntitlementDto> Entitlements);

public sealed record TenantModuleEntitlementDto(
    string ModuleKey,
    string ModuleName,
    bool Enabled,
    string Source);

public sealed record TenantUsersSummaryDto(
    Guid TenantId,
    int TotalUsers,
    int ActiveUsers,
    int PendingInvitations,
    string InvitationPolicy);

public sealed record TenantSettingsDto(
    Guid TenantId,
    string Region,
    string Language,
    string Timezone,
    string Currency,
    string Environment);

public sealed record TenantSettingsUpdateRequest(
    string Language,
    string Timezone,
    string Currency,
    string Environment);

public sealed record TenantLifecycleResultDto(
    Guid TenantId,
    string Status,
    DateTimeOffset UpdatedAt,
    string Message);
