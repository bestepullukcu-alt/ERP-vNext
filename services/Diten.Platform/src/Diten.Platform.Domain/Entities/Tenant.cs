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

    /*
     * WP-TASK-CALENDAR-ENGINE-01 (BL-451 decision note) — the tenant's DEFAULT working window, the last ring of the
     * working-hours chain (person → assignment → unit → legal entity → tenant). No lunch break is subtracted.
     *
     * ⚠ READ ONLY THROUGH IWorkingHoursProvider. Nothing else — no task rule, no calendar feed — may read these
     * two fields; TaskCalendarGuardTests fails the build on a second reader. When person/shift schedules
     * arrive (MOD-0280) they fill an earlier ring of the same seam, and every consumer keeps working unchanged.
     *
     * A record written before these fields existed has neither key; the driver keeps the initializer values, so
     * old tenants read 09:00–18:00 without a migration.
     */
    public TimeOnly DefaultWorkdayStart { get; set; } = new(9, 0);
    public TimeOnly DefaultWorkdayEnd { get; set; } = new(18, 0);

    /*
     * MOD-0280-FU01 D13 — the day's TARGET, distinct from the window above: 09:00–18:00 includes lunch, so a target
     * derived from the window would make everyone look an hour short. Read only through IWorkingHoursProvider, like
     * the two fields above. A record written before this field existed keeps the initializer value (480) without a
     * migration, for the same reason.
     */
    public int DefaultDailyTargetMinutes { get; set; } = 480;

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
    /// <summary>
    /// BL-454 stage D FIX2 (2) — the initial administrator's invitation. Its state is written ONLY by what knows it (the
    /// tenant-created consumer, the operator's "Invite", the e-mail ledger); a subscription activation's "everything still
    /// pending is now done" never touches it — the invitation is not done because the tenant was activated.
    /// </summary>
    public const string AdminInvitationKey = "admin-invitation";

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
    // FIX-TENANT-ADMIN-INVITE-ACTIVATION — stamped when the invited admin completes its forced first-login change.
    public DateTimeOffset? ActivatedAt { get; set; }
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
