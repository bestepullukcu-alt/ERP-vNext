using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Notifications.Services;

/// <summary>
/// MOD-0027-FU03 — reconciles the Notification Event Catalog from the in-process module manifests. Each producer
/// declares its events (+ pages + permissions) in ONE manifest, so an event's targetPageCode / requiredPermissionKey
/// is validated against that SAME manifest (single source of truth; no cross-repository / no Module Catalog write).
/// HARD fields are reconciled every sync; SOFT operator-owned fields are seeded once. Invalid events stay Draft with
/// a validation issue and never reach the active template-slot list. Partial-failure aware (per-event outcomes).
/// </summary>
public sealed class NotificationEventManifestSyncService : INotificationEventManifestSyncService
{
    private readonly IEnumerable<IModuleManifestProvider> _providers;
    private readonly INotificationEventDefinitionRepository _repository;

    public NotificationEventManifestSyncService(
        IEnumerable<IModuleManifestProvider> providers,
        INotificationEventDefinitionRepository repository)
    {
        _providers = providers;
        _repository = repository;
    }

    public async Task<NotificationEventSyncResultDto> SyncAsync(CancellationToken ct = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<NotificationEventSyncItemDto>();
        var providersScanned = 0;
        var declared = 0;
        var synced = 0;
        var updated = 0;
        var withIssues = 0;

        foreach (var provider in _providers)
        {
            providersScanned++;
            var manifest = provider.GetManifest();

            // GUARDRAIL: null NotificationEvents (old/absent) -> empty; never throws.
            var events = manifest.NotificationEvents ?? Array.Empty<ModuleManifestNotificationEvent>();
            if (events.Count == 0)
            {
                continue;
            }

            var pageCodes = BuildPageCodeSet(manifest);
            var permissionKeys = BuildPermissionKeySet(manifest);

            foreach (var me in events)
            {
                declared++;
                var issues = new List<string>();
                var eventCode = (me.EventCode ?? string.Empty).Trim().ToLowerInvariant();

                if (!NotificationParsing.IsValidTemplateKey(eventCode))
                {
                    issues.Add($"EventCode '{me.EventCode}' is invalid; expected canonical lowercase dotted '{{domain}}.{{aggregate}}.{{event}}'.");
                }

                if (!seen.Add(eventCode))
                {
                    issues.Add($"Duplicate EventCode '{eventCode}' declared more than once; only the first is kept.");
                }

                var ownerModuleId = (manifest.ModuleCode ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(ownerModuleId))
                {
                    issues.Add("Owner module code is missing on the manifest.");
                }

                if (!NotificationParsing.TryParseChannel(me.Channel, out var channel))
                {
                    issues.Add($"Unknown channel '{me.Channel}'.");
                }

                if (!NotificationParsing.IsValidTemplateKey(me.DefaultTemplateKey))
                {
                    issues.Add($"DefaultTemplateKey '{me.DefaultTemplateKey}' has an invalid format.");
                }

                if (!string.IsNullOrWhiteSpace(me.TargetPageCode) && !pageCodes.Contains(me.TargetPageCode!.Trim()))
                {
                    issues.Add($"TargetPageCode '{me.TargetPageCode}' is not declared as a page in module '{ownerModuleId}'.");
                }

                if (!string.IsNullOrWhiteSpace(me.RequiredPermissionKey) && !permissionKeys.Contains(me.RequiredPermissionKey!.Trim()))
                {
                    issues.Add($"RequiredPermissionKey '{me.RequiredPermissionKey}' is not declared by module '{ownerModuleId}'.");
                }

                var requiredVars = MapVariables(me.RequiredVariables, "required", issues);
                var optionalVars = MapVariables(me.OptionalVariables, "optional", issues);

                var usageType = Enum.TryParse<NotificationEventUsageType>(me.UsageType, ignoreCase: true, out var ut)
                    ? ut : NotificationEventUsageType.SystemEvent;
                var declaredStatus = Enum.TryParse<NotificationEventStatus>(me.Status, ignoreCase: true, out var ds)
                    ? ds : NotificationEventStatus.Draft;
                var severity = Enum.TryParse<NotificationEventSeverity>(me.SeverityDefault, ignoreCase: true, out var sv)
                    ? sv : NotificationEventSeverity.Info;
                var linkPolicy = Enum.TryParse<NotificationEventLinkPolicy>(me.LinkPolicy, ignoreCase: true, out var lp)
                    ? lp : NotificationEventLinkPolicy.None;
                Guid? routeDescriptorId = Guid.TryParse(me.TargetRouteDescriptorId, out var rd) ? rd : null;

                var isValid = issues.Count == 0;
                // Invalid events never go Active; they stay Draft with an issue.
                var effectiveStatus = !isValid
                    ? NotificationEventStatus.Draft
                    : declaredStatus;

                var existing = await _repository.GetByEventCodeAsync(eventCode, ct);
                string outcome;

                // MOD-0027-FU03A (Bridge) — clobber guard: manifest sync owns ONLY Manifest-sourced records. If an
                // EventCode already exists as a PlatformSeed/SystemSeed record, this is a cross-source collision;
                // skip it with a controlled issue rather than overwriting the seed-owned record.
                if (existing is not null && existing.SourceType != NotificationEventSourceType.Manifest)
                {
                    issues.Add($"Cross-source collision: EventCode '{eventCode}' is owned by a {existing.SourceType} seed record; manifest sync skips it (no clobber).");
                    withIssues++;
                    items.Add(new NotificationEventSyncItemDto(
                        eventCode,
                        ownerModuleId,
                        "skipped",
                        existing.Status.ToString(),
                        issues));
                    continue;
                }

                if (existing is null)
                {
                    var entity = new NotificationEventDefinition
                    {
                        EventCode = eventCode,
                        // Manifest-sourced (bridge): reconciled from ModuleManifestDocument.NotificationEvents.
                        SourceType = NotificationEventSourceType.Manifest,
                        OwnerDomain = (manifest.Domain ?? string.Empty).Trim(),
                        OwnerModuleId = ownerModuleId,
                        OwnerService = (manifest.Service ?? string.Empty).Trim(),
                        Channel = channel,
                        DefaultTemplateKey = NotificationParsing.NormalizeTemplateKey(me.DefaultTemplateKey),
                        RequiredVariables = requiredVars,
                        OptionalVariables = optionalVars,
                        TargetPageCode = string.IsNullOrWhiteSpace(me.TargetPageCode) ? null : me.TargetPageCode!.Trim(),
                        TargetRouteDescriptorId = routeDescriptorId,
                        RequiredPermissionKey = string.IsNullOrWhiteSpace(me.RequiredPermissionKey) ? null : me.RequiredPermissionKey!.Trim(),
                        // SOFT (seeded once from manifest declaration):
                        DisplayNameKey = me.DisplayNameKey,
                        FallbackDisplayName = string.IsNullOrWhiteSpace(me.FallbackDisplayName) ? eventCode : me.FallbackDisplayName!.Trim(),
                        Description = me.Description,
                        CanTenantOverride = me.CanTenantOverride,
                        UsageType = usageType,
                        IsSystemEvent = usageType == NotificationEventUsageType.SystemEvent,
                        DefaultSeverity = severity,
                        LinkPolicy = linkPolicy,
                        Status = effectiveStatus,
                        ManifestSource = ownerModuleId,
                        ManifestVersion = manifest.ModuleVersion,
                        LastSyncedAt = DateTimeOffset.UtcNow
                    };
                    await _repository.CreateAsync(entity, ct);
                    outcome = "synced";
                    synced++;
                }
                else
                {
                    // Reconcile HARD (manifest-owned); preserve SOFT operator-owned fields (seeded once).
                    existing.OwnerDomain = (manifest.Domain ?? string.Empty).Trim();
                    existing.OwnerModuleId = ownerModuleId;
                    existing.OwnerService = (manifest.Service ?? string.Empty).Trim();
                    existing.Channel = channel;
                    existing.DefaultTemplateKey = NotificationParsing.NormalizeTemplateKey(me.DefaultTemplateKey);
                    existing.RequiredVariables = requiredVars;
                    existing.OptionalVariables = optionalVars;
                    existing.TargetPageCode = string.IsNullOrWhiteSpace(me.TargetPageCode) ? null : me.TargetPageCode!.Trim();
                    existing.TargetRouteDescriptorId = routeDescriptorId;
                    existing.RequiredPermissionKey = string.IsNullOrWhiteSpace(me.RequiredPermissionKey) ? null : me.RequiredPermissionKey!.Trim();
                    existing.UsageType = usageType;
                    existing.IsSystemEvent = usageType == NotificationEventUsageType.SystemEvent;
                    existing.ManifestSource = ownerModuleId;
                    existing.ManifestVersion = manifest.ModuleVersion;
                    existing.LastSyncedAt = DateTimeOffset.UtcNow;
                    // Preserve operator terminal states; otherwise reflect validity. Invalid always forces Draft.
                    if (!isValid)
                    {
                        existing.Status = NotificationEventStatus.Draft;
                    }
                    else if (existing.Status is not NotificationEventStatus.Archived and not NotificationEventStatus.Deprecated)
                    {
                        existing.Status = effectiveStatus;
                    }
                    await _repository.UpdateAsync(existing, ct);
                    outcome = "updated";
                    updated++;
                }

                if (!isValid)
                {
                    withIssues++;
                    outcome = "issue";
                }

                items.Add(new NotificationEventSyncItemDto(
                    eventCode,
                    ownerModuleId,
                    outcome,
                    (isValid ? effectiveStatus : NotificationEventStatus.Draft).ToString(),
                    issues));
            }
        }

        return new NotificationEventSyncResultDto(providersScanned, declared, synced, updated, withIssues, items);
    }

    private static List<TemplateVariableDefinition> MapVariables(
        IReadOnlyList<ModuleManifestNotificationVariable>? source,
        string kind,
        List<string> issues)
    {
        var result = new List<TemplateVariableDefinition>();
        foreach (var v in source ?? Array.Empty<ModuleManifestNotificationVariable>())
        {
            var name = (v.Name ?? string.Empty).Trim();
            if (!NotificationParsing.IsValidVariableName(name))
            {
                issues.Add($"Invalid {kind} variable name '{v.Name}'.");
                continue;
            }
            if (!NotificationParsing.TryParseVariableType(v.Type, out var type))
            {
                issues.Add($"Unknown {kind} variable type '{v.Type}' for '{name}'.");
                continue;
            }
            result.Add(new TemplateVariableDefinition { Name = name, Type = type, IsRequired = v.IsRequired });
        }
        return result;
    }

    private static HashSet<string> BuildPageCodeSet(ModuleManifestDocument manifest)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in manifest.Pages ?? Array.Empty<ModuleManifestPage>())
        {
            if (!string.IsNullOrWhiteSpace(p.PageCode))
            {
                set.Add(p.PageCode.Trim());
            }
        }
        return set;
    }

    private static HashSet<string> BuildPermissionKeySet(ModuleManifestDocument manifest)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in manifest.Pages ?? Array.Empty<ModuleManifestPage>())
        {
            if (!string.IsNullOrWhiteSpace(p.RequiredPermission))
            {
                set.Add(p.RequiredPermission.Trim());
            }
            foreach (var a in p.Actions ?? Array.Empty<ModuleManifestAction>())
            {
                if (!string.IsNullOrWhiteSpace(a.PermissionKey))
                {
                    set.Add(a.PermissionKey.Trim());
                }
            }
        }
        return set;
    }
}
