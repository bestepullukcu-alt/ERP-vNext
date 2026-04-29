using Diten.Platform.Common.Persistence;

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
    public TenantType TenantType { get; set; } = TenantType.Trial;

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
    public TenantSettings Settings { get; set; } = new();
    public string? AppUrl { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? ProvisionedAt { get; set; }
    public DateTimeOffset? SuspendedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
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
