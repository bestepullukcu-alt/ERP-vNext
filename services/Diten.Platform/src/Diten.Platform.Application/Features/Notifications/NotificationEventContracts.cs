namespace Diten.Platform.Application.Features.Notifications;

// MOD-0027-FU03 — Notification Event Catalog DTOs (read-only catalog + template slot contract for the template UI).

public sealed record NotificationEventDefinitionDto(
    Guid Id,
    string EventCode,
    string? DisplayNameKey,
    string FallbackDisplayName,
    string? Description,
    string OwnerDomain,
    string OwnerModuleId,
    string OwnerService,
    string Channel,
    string DefaultTemplateKey,
    IReadOnlyList<TemplateVariableDefinitionDto> RequiredVariables,
    IReadOnlyList<TemplateVariableDefinitionDto> OptionalVariables,
    bool CanTenantOverride,
    string UsageType,
    bool IsSystemEvent,
    string? TargetPageCode,
    Guid? TargetRouteDescriptorId,
    string? RequiredPermissionKey,
    string DefaultSeverity,
    string LinkPolicy,
    string Status,
    string? ManifestSource,
    string? ManifestVersion,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // MOD-0027-FU03A (Bridge) — additive read-only source model fields.
    string SourceType,
    string? OwnerArea,
    string? TargetRoute);

// Contract consumed by the (future) template create/edit UI to prefill an event's required/optional variables.
public sealed record NotificationEventTemplateContractDto(
    string EventCode,
    string Channel,
    string DefaultTemplateKey,
    IReadOnlyList<TemplateVariableDefinitionDto> RequiredVariables,
    IReadOnlyList<TemplateVariableDefinitionDto> OptionalVariables,
    bool CanTenantOverride);

// Active-only slot list; Deprecated/Archived events are excluded by the query.
public sealed record NotificationEventTemplateSlotDto(
    string EventCode,
    string FallbackDisplayName,
    string? DisplayNameKey,
    string Channel,
    string DefaultTemplateKey,
    string UsageType,
    bool CanTenantOverride,
    IReadOnlyList<TemplateVariableDefinitionDto> RequiredVariables);

// SOFT-field update only (HARD fields are manifest-reconciled; EventCode/OwnerModuleId/Channel/RequiredVariables immutable here).
public sealed record UpdateNotificationEventRequest(
    string? DisplayNameKey,
    string? FallbackDisplayName,
    string? Description,
    bool CanTenantOverride,
    string? DefaultSeverity,
    string? LinkPolicy,
    string? Status);

public sealed record NotificationEventSyncItemDto(
    string EventCode,
    string OwnerModuleId,
    string Outcome,               // synced | updated | issue
    string ResultStatus,          // Draft | Active | Deprecated | Archived
    IReadOnlyList<string> Issues);

public sealed record NotificationEventSyncResultDto(
    int ProvidersScanned,
    int EventsDeclared,
    int Synced,
    int Updated,
    int WithIssues,
    IReadOnlyList<NotificationEventSyncItemDto> Items);
