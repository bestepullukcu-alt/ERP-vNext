using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities;

public sealed class Tenant : GlobalEntity
{
    public required string Code { get; init; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Domain { get; set; }
    public string? Region { get; set; }
    public string? Environment { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;
    public string? Tier { get; set; } = "Standard";
    public TenantType TenantType { get; set; } = TenantType.Customer;
    public Guid? PlanId { get; set; }
    public string? PlanCode { get; set; }
    public string? PlanName { get; set; }
    public TenantSubscriptionStatus SubscriptionStatus { get; set; } = TenantSubscriptionStatus.Active;
    public DateTimeOffset? TrialStartDateUtc { get; set; }
    public DateTimeOffset? TrialEndDateUtc { get; set; }
    public int ActiveUserCount { get; set; }
    public int UserLimit { get; set; } = 50;
    public decimal StorageUsedGb { get; set; }
    public decimal StorageQuotaGb { get; set; } = 500;

    // Legal & Company Info
    public string? LegalName { get; set; }
    public string? TaxNumber { get; set; }
    public string? Country { get; set; }
    public string? Industry { get; set; }

    // Contact Info
    public string? ContactPerson { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // Default Locale (tenant profile defaults — TenantSettings holds runtime overrides)
    public string DefaultTimezone { get; set; } = "UTC";
    public string DefaultLanguage { get; set; } = "en";
    public string DefaultCurrency { get; set; } = "USD";

    // Provisioning & Lifecycle
    public string ProvisioningStatus { get; set; } = "Queued";
    public List<TenantProvisioningStep> ProvisioningSteps { get; set; } = [];
    public List<TenantActivityEvent> ActivityTimeline { get; set; } = [];
    public List<TenantAdminUser> AdminUsers { get; set; } = [];
    public TenantSettings Settings { get; set; } = new();
    public string? AppUrl { get; set; }
    public string? LogoDataUrl { get; set; }
    public string? FaviconDataUrl { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? ProvisionedAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }

    [BsonIgnore]
    public bool IsOverQuota => StorageQuotaGb > 0 && StorageUsedGb > StorageQuotaGb;
}

public sealed class TenantProvisioningStep
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Detail { get; set; }
}

public sealed class TenantActivityEvent
{
    public required string EventType { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public string? Actor { get; init; }
}

public sealed class TenantAdminUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Email { get; set; }
    public TenantAdminUserStatus Status { get; set; } = TenantAdminUserStatus.PendingInvitation;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? InvitedAt { get; set; }
}

public sealed class TenantSettings
{
    public string Language { get; set; } = "en";
    public string Timezone { get; set; } = "UTC";
    public string Currency { get; set; } = "USD";
    public string Environment { get; set; } = "Production";
}

public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
    Deactivated
}

public enum TenantAdminUserStatus
{
    PendingInvitation,
    Invited,
    Active,
    Disabled
}
