using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

/// <summary>
/// MOD-0027-FU03 — platform-owned system contract mapping a canonical event to its default notification template slot.
/// Global/platform record (no TenantId): tenants cannot create events. HARD fields (EventCode, OwnerModuleId, Channel,
/// RequiredVariables) are reconciled from the module manifest at sync time; SOFT fields (display overrides,
/// CanTenantOverride, Status, severity, link policy) are operator-editable. EventCode is unique + immutable
/// (rename = deprecate + new code).
/// </summary>
public sealed class NotificationEventDefinition : BaseEntity
{
    // HARD identity (manifest-reconciled) — immutable EventCode.
    public string EventCode { get; set; } = string.Empty;

    // Producer ownership (validated against Module Catalog / Domain Management at sync).
    public string OwnerDomain { get; set; } = string.Empty;
    public string OwnerModuleId { get; set; } = string.Empty;
    public string OwnerService { get; set; } = string.Empty;

    public NotificationChannelCode Channel { get; set; } = NotificationChannelCode.Email;

    // Event -> template binding.
    public string DefaultTemplateKey { get; set; } = string.Empty;

    // Same shape as MOD-0027-FU02 template variable contract (preview/save validation alignment).
    public List<TemplateVariableDefinition> RequiredVariables { get; set; } = [];
    public List<TemplateVariableDefinition> OptionalVariables { get; set; } = [];

    // Cross-module references (validated at sync; unresolved -> Draft + validation issue).
    public string? TargetPageCode { get; set; }
    public Guid? TargetRouteDescriptorId { get; set; }
    public string? RequiredPermissionKey { get; set; }

    // MOD-0027-FU03A (Bridge) — source model + platform-seed metadata (additive, nullable/default; no migration).
    // SourceType default Manifest (=0) keeps every existing FU03 record on the manifest branch. PlatformSeed/SystemSeed
    // records skip Module Catalog / ModulePages validation; when RequiredPermissionKey is null they are policy-gated via
    // RequiredPolicy (canonical field name; there is no AccessPolicy). TargetRoute is a free fixed-admin route string and
    // is intentionally distinct from the Guid TargetRouteDescriptorId above.
    public NotificationEventSourceType SourceType { get; set; } = NotificationEventSourceType.Manifest;
    public string? OwnerArea { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string? TargetRoute { get; set; }
    public string? ModuleCatalogRef { get; set; }
    public string? RequiredPolicy { get; set; }

    // SOFT / operator-owned metadata.
    public string? DisplayNameKey { get; set; }
    public string FallbackDisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool CanTenantOverride { get; set; }
    public NotificationEventUsageType UsageType { get; set; } = NotificationEventUsageType.SystemEvent;
    public bool IsSystemEvent { get; set; } = true;
    public NotificationEventSeverity DefaultSeverity { get; set; } = NotificationEventSeverity.Info;
    public NotificationEventLinkPolicy LinkPolicy { get; set; } = NotificationEventLinkPolicy.None;
    public NotificationEventStatus Status { get; set; } = NotificationEventStatus.Draft;

    // Manifest provenance.
    public string? ManifestSource { get; set; }
    public string? ManifestVersion { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}
