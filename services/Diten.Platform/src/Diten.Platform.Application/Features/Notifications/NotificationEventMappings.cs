using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications;

// MOD-0027-FU03 — entity -> DTO projections for the Notification Event Catalog.
internal static class NotificationEventMappings
{
    public static NotificationEventDefinitionDto ToDto(this NotificationEventDefinition e) => new(
        e.Id,
        e.EventCode,
        e.DisplayNameKey,
        e.FallbackDisplayName,
        e.Description,
        e.OwnerDomain,
        e.OwnerModuleId,
        e.OwnerService,
        e.Channel.ToString(),
        e.DefaultTemplateKey,
        e.RequiredVariables.Select(ToVarDto).ToArray(),
        e.OptionalVariables.Select(ToVarDto).ToArray(),
        e.CanTenantOverride,
        e.UsageType.ToString(),
        e.IsSystemEvent,
        e.TargetPageCode,
        e.TargetRouteDescriptorId,
        e.RequiredPermissionKey,
        e.DefaultSeverity.ToString(),
        e.LinkPolicy.ToString(),
        e.Status.ToString(),
        e.ManifestSource,
        e.ManifestVersion,
        e.LastSyncedAt,
        e.CreatedAt,
        e.UpdatedAt,
        e.SourceType.ToString(),
        e.OwnerArea,
        e.TargetRoute);

    public static NotificationEventTemplateContractDto ToTemplateContractDto(this NotificationEventDefinition e) => new(
        e.EventCode,
        e.Channel.ToString(),
        e.DefaultTemplateKey,
        e.RequiredVariables.Select(ToVarDto).ToArray(),
        e.OptionalVariables.Select(ToVarDto).ToArray(),
        e.CanTenantOverride);

    public static NotificationEventTemplateSlotDto ToSlotDto(this NotificationEventDefinition e) => new(
        e.EventCode,
        e.FallbackDisplayName,
        e.DisplayNameKey,
        e.Channel.ToString(),
        e.DefaultTemplateKey,
        e.UsageType.ToString(),
        e.CanTenantOverride,
        e.RequiredVariables.Select(ToVarDto).ToArray());

    private static TemplateVariableDefinitionDto ToVarDto(TemplateVariableDefinition v) =>
        new(v.Name, v.Type.ToString(), v.IsRequired);
}
