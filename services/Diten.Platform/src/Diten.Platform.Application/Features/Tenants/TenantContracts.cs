namespace Diten.Platform.Application.Features.Tenants;

public sealed record TenantListItemDto(
    Guid Id,
    string Code,
    string Slug,
    string Name,
    string DisplayName,
    string Domain,
    string Region,
    string Environment,
    string Status,
    string TenantType,
    string Plan,
    Guid? PlanId,
    string? PlanCode,
    string? PlanName,
    string SubscriptionStatus,
    DateTimeOffset? TrialStartDateUtc,
    DateTimeOffset? TrialEndDateUtc,
    string? Country,
    string ProvisioningStatus,
    int ActiveUserCount,
    int UserLimit,
    decimal StorageUsedGb,
    decimal StorageQuotaGb,
    decimal StorageUsagePercent,
    bool IsOverQuota,
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
    long Deactivated,
    long Trial,
    long OverQuota);

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
    string Slug,
    string Name,
    string DisplayName,
    string Domain,
    string Region,
    string Environment,
    string Status,
    string TenantType,
    string ProvisioningStatus,
    string Tier,
    Guid? PlanId,
    string? PlanCode,
    string? PlanName,
    string SubscriptionStatus,
    DateTimeOffset? TrialStartDateUtc,
    DateTimeOffset? TrialEndDateUtc,
    string? AppUrl,
    string? LogoDataUrl,
    string? FaviconDataUrl,

    // Legal & Company
    string? LegalName,
    string? TaxNumber,
    string? Country,
    string? Industry,

    // Contact
    string? ContactPerson,
    string? ContactEmail,
    string? ContactPhone,

    // Locale defaults
    string DefaultTimezone,
    string DefaultLanguage,
    string DefaultCurrency,

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

public sealed record TenantAdminUserDto(
    Guid Id,
    string Name,
    string Email,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? InvitedAt,
    // Dev-only invitation fallback (Development + SMTP not configured). These carry the login URL and
    // temporary password so an operator without SMTP can complete setup manually. Always null on
    // production, on the SMTP-on email path, and on every non-invite path. Mirrors UserDto.SetupUrl.
    string? LoginUrl = null,
    string? TemporaryPassword = null,
    bool? EmailSent = null);

public sealed record TenantAdminUserUpsertRequest(
    string Name,
    string Email);

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

public sealed record TenantUpdateRequest(
    string Name,
    string DisplayName,
    string Domain,
    string? Subdomain,
    string? Slug,
    string? Country,
    string? DefaultTimezone,
    string? DefaultLanguage,
    string? DefaultCurrency,
    Diten.Platform.Domain.Entities.TenantType? TenantType);

public sealed record TenantBrandingUpdateRequest(
    string? LogoDataUrl,
    string? FaviconDataUrl);

public sealed record TenantLoginSettingsDto(
    Guid TenantId,
    bool TwoFactorEnabled,
    bool MfaRequired,
    bool EmailLoginEnabled,
    bool PhoneLoginEnabled,
    int PasswordMinLength,
    bool PasswordRequireUppercase,
    bool PasswordRequireLowercase,
    bool PasswordRequireDigit,
    bool PasswordRequireSpecialChar,
    int? PasswordExpirationDays,
    int SessionTimeoutMinutes,
    int RefreshTokenLifetimeDays,
    int MaxFailedLoginAttempts,
    int LockoutDurationMinutes,
    bool IpWhitelistEnabled,
    IReadOnlyList<string> AllowedIps,
    IReadOnlyList<string> AllowedCountries,
    int LoginAuditRetentionDays,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TenantLoginSettingsUpdateRequest(
    bool TwoFactorEnabled,
    bool MfaRequired,
    bool EmailLoginEnabled,
    bool PhoneLoginEnabled,
    int PasswordMinLength,
    bool PasswordRequireUppercase,
    bool PasswordRequireLowercase,
    bool PasswordRequireDigit,
    bool PasswordRequireSpecialChar,
    int? PasswordExpirationDays,
    int SessionTimeoutMinutes,
    int RefreshTokenLifetimeDays,
    int MaxFailedLoginAttempts,
    int LockoutDurationMinutes,
    bool IpWhitelistEnabled,
    IReadOnlyList<string>? AllowedIps,
    IReadOnlyList<string>? AllowedCountries,
    int LoginAuditRetentionDays);

public sealed record TenantLifecycleResultDto(
    Guid TenantId,
    string Status,
    DateTimeOffset UpdatedAt,
    string Message);
