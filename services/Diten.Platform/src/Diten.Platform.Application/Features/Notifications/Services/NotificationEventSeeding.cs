using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Notifications.Services;

// MOD-0027-FU03A (Bridge) — generic PlatformSeed/SystemSeed foundation for the Notification Event Catalog.
// This file provides the SOURCE-AGNOSTIC seed machinery ONLY: the decision planner, an idempotent seeder and an
// (empty) seed catalog. It contains NO tenant event content — the 3 tenant events (tenant.user.invited /
// tenant.lifecycle.suspended / tenant.lifecycle.reactivated) are FU04A scope and are added to
// NotificationEventSeedCatalog.PlatformSeedDefinitions there. Kept OUTSIDE INotificationEventManifestSyncService and
// uses NO IModuleManifestProvider.

/// <summary>Desired PlatformSeed/SystemSeed event to reconcile into the catalog (source item, not the persisted entity).</summary>
public sealed record NotificationEventSeedDefinition(
    string EventCode,
    NotificationEventSourceType SourceType,           // PlatformSeed or SystemSeed (never Manifest)
    NotificationChannelCode Channel,
    string DefaultTemplateKey,
    IReadOnlyList<TemplateVariableDefinition> RequiredVariables,
    IReadOnlyList<TemplateVariableDefinition> OptionalVariables,
    string OwnerModuleId,
    string? OwnerArea,
    string? OwnerDisplayName,
    string? TargetRoute,
    string? RequiredPolicy,                            // canonical field name (there is no AccessPolicy)
    string? RequiredPermissionKey,                     // null => policy-gated via RequiredPolicy
    bool CanTenantOverride,
    NotificationEventUsageType UsageType,
    NotificationEventSeverity DefaultSeverity,
    NotificationEventLinkPolicy LinkPolicy,
    string? DisplayNameKey,
    string FallbackDisplayName,
    string? Description);

public sealed record NotificationEventSeedValidation(
    bool IsValid,
    IReadOnlyList<string> Issues,
    NotificationEventStatus EffectiveStatus);

public enum NotificationEventSeedAction { Create, Update, Skip }

public sealed record NotificationEventSeedPlan(
    NotificationEventSeedAction Action,
    NotificationEventDefinition? Entity,               // for Create/Update
    string? Issue);                                    // for Skip (clobber guard)

/// <summary>
/// Pure decision logic (no persistence, no reflection). Unit-testable with plain objects.
/// </summary>
public static class NotificationEventSeedPlanner
{
    // Sentinel provenance for seed-sourced events (NOT a manifest ModuleCode).
    public const string SeedManifestSource = "notification-event-seed";

    /// <summary>
    /// Validate a seed definition. LAYER RULE (FU03A §5.1): the seed layer NEVER performs RBAC reflection.
    /// - Module Catalog / ModulePages are NOT validated for seed sources (bypass).
    /// - RequiredPermissionKey present (permission-gated) => seed as Draft; an API-side pass validates the literal and
    ///   promotes to Active (Infrastructure must not reach Platform.API to reflect it here).
    /// - RequiredPermissionKey null + RequiredPolicy present (policy-gated, e.g. PlatformActor) => needs no reflection,
    ///   eligible for Active.
    /// </summary>
    public static NotificationEventSeedValidation Validate(NotificationEventSeedDefinition def, bool templateExists)
    {
        var issues = new List<string>();
        var eventCode = (def.EventCode ?? string.Empty).Trim().ToLowerInvariant();

        if (!NotificationParsing.IsValidTemplateKey(eventCode))
        {
            issues.Add($"EventCode '{def.EventCode}' is invalid; expected canonical lowercase dotted '{{domain}}.{{aggregate}}.{{event}}'.");
        }

        if (def.SourceType == NotificationEventSourceType.Manifest)
        {
            issues.Add("Seed definition SourceType must be PlatformSeed or SystemSeed, not Manifest.");
        }

        if (!NotificationParsing.IsValidTemplateKey(def.DefaultTemplateKey))
        {
            issues.Add($"DefaultTemplateKey '{def.DefaultTemplateKey}' has an invalid format.");
        }
        else if (!templateExists)
        {
            issues.Add($"DefaultTemplateKey '{def.DefaultTemplateKey}' has no seeded template.");
        }

        var permissionGated = !string.IsNullOrWhiteSpace(def.RequiredPermissionKey);
        var policyGated = !string.IsNullOrWhiteSpace(def.RequiredPolicy);
        if (!permissionGated && !policyGated)
        {
            issues.Add("Seed event must carry a RequiredPermissionKey or a RequiredPolicy (fixed-page policy gate).");
        }

        // Invalid -> Draft. Permission-gated -> Draft (deferred to API-side activation pass). Policy-gated -> Active.
        NotificationEventStatus status;
        if (issues.Count > 0)
        {
            status = NotificationEventStatus.Draft;
        }
        else if (permissionGated)
        {
            status = NotificationEventStatus.Draft;
        }
        else
        {
            status = NotificationEventStatus.Active;
        }

        return new NotificationEventSeedValidation(issues.Count == 0, issues, status);
    }

    /// <summary>
    /// Decide create/update/skip against an existing record (or null). CLOBBER GUARD: never touch a Manifest-owned
    /// record (cross-source collision -> Skip + issue). Update reconciles HARD fields and preserves SOFT operator-owned
    /// fields (Status, display overrides, CanTenantOverride, severity, link policy).
    /// </summary>
    public static NotificationEventSeedPlan Plan(
        NotificationEventDefinition? existing,
        NotificationEventSeedDefinition def,
        NotificationEventStatus effectiveStatus)
    {
        var eventCode = (def.EventCode ?? string.Empty).Trim().ToLowerInvariant();

        if (existing is null)
        {
            return new NotificationEventSeedPlan(NotificationEventSeedAction.Create, ToNewEntity(def, eventCode, effectiveStatus), null);
        }

        if (existing.SourceType == NotificationEventSourceType.Manifest)
        {
            return new NotificationEventSeedPlan(
                NotificationEventSeedAction.Skip,
                null,
                $"Cross-source collision: EventCode '{eventCode}' is owned by a Manifest record; seed skips it (no clobber).");
        }

        ApplyHard(existing, def, eventCode);
        return new NotificationEventSeedPlan(NotificationEventSeedAction.Update, existing, null);
    }

    private static NotificationEventDefinition ToNewEntity(
        NotificationEventSeedDefinition def, string eventCode, NotificationEventStatus effectiveStatus) => new()
    {
        EventCode = eventCode,
        SourceType = def.SourceType,
        OwnerDomain = string.Empty,
        OwnerModuleId = (def.OwnerModuleId ?? string.Empty).Trim(),
        OwnerService = string.Empty,
        OwnerArea = def.OwnerArea,
        OwnerDisplayName = def.OwnerDisplayName,
        Channel = def.Channel,
        DefaultTemplateKey = NotificationParsing.NormalizeTemplateKey(def.DefaultTemplateKey),
        RequiredVariables = def.RequiredVariables.ToList(),
        OptionalVariables = def.OptionalVariables.ToList(),
        // Seed source: Module Catalog page binding is bypassed; a free fixed-admin route is carried instead.
        TargetPageCode = null,
        TargetRouteDescriptorId = null,
        TargetRoute = string.IsNullOrWhiteSpace(def.TargetRoute) ? null : def.TargetRoute!.Trim(),
        ModuleCatalogRef = null,
        RequiredPermissionKey = string.IsNullOrWhiteSpace(def.RequiredPermissionKey) ? null : def.RequiredPermissionKey!.Trim(),
        RequiredPolicy = string.IsNullOrWhiteSpace(def.RequiredPolicy) ? null : def.RequiredPolicy!.Trim(),
        DisplayNameKey = def.DisplayNameKey,
        FallbackDisplayName = string.IsNullOrWhiteSpace(def.FallbackDisplayName) ? eventCode : def.FallbackDisplayName.Trim(),
        Description = def.Description,
        CanTenantOverride = def.CanTenantOverride,
        UsageType = def.UsageType,
        IsSystemEvent = def.UsageType == NotificationEventUsageType.SystemEvent,
        DefaultSeverity = def.DefaultSeverity,
        LinkPolicy = def.LinkPolicy,
        Status = effectiveStatus,
        ManifestSource = SeedManifestSource,
        ManifestVersion = null,
        LastSyncedAt = DateTimeOffset.UtcNow
    };

    private static void ApplyHard(NotificationEventDefinition existing, NotificationEventSeedDefinition def, string eventCode)
    {
        // HARD (seed-owned) — reconciled every seed run:
        existing.SourceType = def.SourceType;
        existing.OwnerModuleId = (def.OwnerModuleId ?? string.Empty).Trim();
        existing.OwnerArea = def.OwnerArea;
        existing.OwnerDisplayName = def.OwnerDisplayName;
        existing.Channel = def.Channel;
        existing.DefaultTemplateKey = NotificationParsing.NormalizeTemplateKey(def.DefaultTemplateKey);
        existing.RequiredVariables = def.RequiredVariables.ToList();
        existing.OptionalVariables = def.OptionalVariables.ToList();
        existing.TargetPageCode = null;
        existing.TargetRoute = string.IsNullOrWhiteSpace(def.TargetRoute) ? null : def.TargetRoute!.Trim();
        existing.ModuleCatalogRef = null;
        existing.RequiredPermissionKey = string.IsNullOrWhiteSpace(def.RequiredPermissionKey) ? null : def.RequiredPermissionKey!.Trim();
        existing.RequiredPolicy = string.IsNullOrWhiteSpace(def.RequiredPolicy) ? null : def.RequiredPolicy!.Trim();
        existing.UsageType = def.UsageType;
        existing.IsSystemEvent = def.UsageType == NotificationEventUsageType.SystemEvent;
        existing.ManifestSource = SeedManifestSource;
        existing.LastSyncedAt = DateTimeOffset.UtcNow;
        // SOFT (operator-owned) intentionally NOT touched: Status, DisplayNameKey, FallbackDisplayName, Description,
        // CanTenantOverride, DefaultSeverity, LinkPolicy.
    }
}

public sealed record NotificationEventSeedResult(int Created, int Updated, int Skipped, int WithIssues);

/// <summary>
/// Idempotent seeder over the event repository. Reusable by any caller that has an
/// <see cref="INotificationEventDefinitionRepository"/> and a template-existence probe. Unit-testable with an in-memory
/// repository. Bridge ships it with an EMPTY catalog (see <see cref="NotificationEventSeedCatalog"/>).
/// </summary>
public sealed class NotificationEventSeeder
{
    private readonly INotificationEventDefinitionRepository _repository;
    private readonly Func<string, CancellationToken, Task<bool>> _templateExists;

    public NotificationEventSeeder(
        INotificationEventDefinitionRepository repository,
        Func<string, CancellationToken, Task<bool>> templateExists)
    {
        _repository = repository;
        _templateExists = templateExists;
    }

    public async Task<NotificationEventSeedResult> SeedAsync(
        IReadOnlyList<NotificationEventSeedDefinition> definitions,
        CancellationToken ct = default)
    {
        int created = 0, updated = 0, skipped = 0, withIssues = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in definitions)
        {
            var eventCode = (def.EventCode ?? string.Empty).Trim().ToLowerInvariant();
            if (!seen.Add(eventCode))
            {
                withIssues++;
                continue; // duplicate declaration within the seed set; only the first is kept.
            }

            var templateExists = await _templateExists(def.DefaultTemplateKey, ct);
            var validation = NotificationEventSeedPlanner.Validate(def, templateExists);
            if (!validation.IsValid)
            {
                withIssues++;
            }

            var existing = await _repository.GetByEventCodeAsync(eventCode, ct);
            var plan = NotificationEventSeedPlanner.Plan(existing, def, validation.EffectiveStatus);

            switch (plan.Action)
            {
                case NotificationEventSeedAction.Create:
                    await _repository.CreateAsync(plan.Entity!, ct);
                    created++;
                    break;
                case NotificationEventSeedAction.Update:
                    await _repository.UpdateAsync(plan.Entity!, ct);
                    updated++;
                    break;
                case NotificationEventSeedAction.Skip:
                    skipped++;
                    withIssues++;
                    break;
            }
        }

        return new NotificationEventSeedResult(created, updated, skipped, withIssues);
    }
}

/// <summary>
/// Catalog of PlatformSeed/SystemSeed event definitions to seed at startup. Kept as a single source so the startup
/// seed loop and tests reference the same list.
///
/// MOD-0027-FU04A — Tenant Management Notification Event Opt-in: the 3 Platform Admin fixed-page tenant events are
/// declared here as PlatformSeed (Module Catalog / IModuleManifestProvider NOT used). They are policy-gated
/// (RequiredPolicy=PlatformActor, RequiredPermissionKey=null — /Platform/Tenants is Authorize(Policy="PlatformActor"))
/// and bind to the existing FU02-seeded templates (tenant.invite/suspended/reactivated.email). No runtime eventCode
/// dispatch (that is FU04B); no tenant producer/template change.
/// </summary>
public static class NotificationEventSeedCatalog
{
    public static IReadOnlyList<NotificationEventSeedDefinition> PlatformSeedDefinitions { get; } = new[]
    {
        TenantEvent(
            eventCode: "tenant.user.invited",
            templateKey: "tenant.invite.email",
            severity: NotificationEventSeverity.Info,
            fallbackDisplayName: "Tenant user invited",
            requiredVariables: new[] { Var("TenantDisplayName") }),
        TenantEvent(
            eventCode: "tenant.lifecycle.suspended",
            templateKey: "tenant.suspended.email",
            severity: NotificationEventSeverity.Warning,
            fallbackDisplayName: "Tenant suspended",
            requiredVariables: new[] { Var("TenantDisplayName"), Var("Reason"), Var("SuspendedAtUtc") }),
        TenantEvent(
            eventCode: "tenant.lifecycle.reactivated",
            templateKey: "tenant.reactivated.email",
            severity: NotificationEventSeverity.Success,
            fallbackDisplayName: "Tenant reactivated",
            requiredVariables: new[] { Var("TenantDisplayName"), Var("ReactivatedAtUtc") }),
    };

    // Shared shape for the 3 MOD-0009 Tenant/Environment Management fixed-page events (PlatformSeed, policy-gated).
    private static NotificationEventSeedDefinition TenantEvent(
        string eventCode,
        string templateKey,
        NotificationEventSeverity severity,
        string fallbackDisplayName,
        IReadOnlyList<TemplateVariableDefinition> requiredVariables) =>
        new(
            EventCode: eventCode,
            SourceType: NotificationEventSourceType.PlatformSeed,
            Channel: NotificationChannelCode.Email,
            DefaultTemplateKey: templateKey,
            RequiredVariables: requiredVariables,
            OptionalVariables: Array.Empty<TemplateVariableDefinition>(),
            OwnerModuleId: "MOD-0009",
            OwnerArea: "PlatformAdmin",
            OwnerDisplayName: "Tenant / Environment Management",
            TargetRoute: "/Platform/Tenants",
            RequiredPolicy: "PlatformActor",
            RequiredPermissionKey: null,
            CanTenantOverride: true,
            UsageType: NotificationEventUsageType.SystemEvent,
            DefaultSeverity: severity,
            LinkPolicy: NotificationEventLinkPolicy.None,
            DisplayNameKey: null,
            FallbackDisplayName: fallbackDisplayName,
            Description: null);

    private static TemplateVariableDefinition Var(string name) =>
        new() { Name = name, Type = TemplateVariableType.String, IsRequired = true };
}
