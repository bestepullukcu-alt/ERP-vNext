using System.Text.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// MOD-0027-FU03 — Notification Event Catalog manifest sync + template-slot + backward-compat guardrail tests.
/// </summary>
public sealed class NotificationEventCatalogTests
{
    // --- BuildingBlocks guardrail: backward-compat deserialization ---

    [Fact]
    public void Old_manifest_json_without_notification_events_deserializes_without_exception()
    {
        const string oldJson = """
        {
          "moduleCode":"workflow","moduleName":"Workflow","displayName":"Workflow","domain":"Workflow",
          "service":"DitenPlatform","moduleVersion":"1.0.0","isTenantAssignable":true,"sortOrder":10,
          "pages":[],"icon":"bx-git-merge","isBaseline":false
        }
        """;

        var doc = JsonSerializer.Deserialize<ModuleManifestDocument>(oldJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(doc);
        Assert.Null(doc!.NotificationEvents); // missing field -> null (no exception, no UnmappedMemberHandling.Disallow)
        Assert.Equal("workflow", doc.ModuleCode);
    }

    [Fact]
    public async Task Sync_with_null_notification_events_coalesces_to_empty()
    {
        var repo = new InMemoryEventRepo();
        var provider = new FakeManifestProvider(BuildManifest(notificationEvents: null));
        var service = new NotificationEventManifestSyncService(new IModuleManifestProvider[] { provider }, repo);

        var result = await service.SyncAsync();

        Assert.Equal(1, result.ProvidersScanned);
        Assert.Equal(0, result.EventsDeclared);
        Assert.Empty(repo.Items);
    }

    // --- Sync valid / invalid ---

    [Fact]
    public async Task Valid_event_syncs_as_active_with_no_issues()
    {
        var repo = new InMemoryEventRepo();
        var ev = Event("workflow.task.assigned", pageCode: "WF_TASKS", permission: "workflow.tasks.read", status: "Active");
        var service = Service(repo, BuildManifest(new[] { ev }, pageCode: "WF_TASKS", permission: "workflow.tasks.read"));

        var result = await service.SyncAsync();

        Assert.Equal(1, result.Synced);
        Assert.Equal(0, result.WithIssues);
        var stored = Assert.Single(repo.Items);
        Assert.Equal(NotificationEventStatus.Active, stored.Status);
        Assert.Equal("workflow.task.assigned", stored.EventCode);
    }

    [Fact]
    public async Task Invalid_owner_page_and_permission_produce_issue_and_stay_draft()
    {
        var repo = new InMemoryEventRepo();
        var ev = Event("workflow.task.assigned", pageCode: "NOT_A_PAGE", permission: "not.a.permission", status: "Active");
        var service = Service(repo, BuildManifest(new[] { ev }, pageCode: "WF_TASKS", permission: "workflow.tasks.read"));

        var result = await service.SyncAsync();

        Assert.Equal(1, result.WithIssues);
        var stored = Assert.Single(repo.Items);
        Assert.Equal(NotificationEventStatus.Draft, stored.Status); // invalid never goes Active
        Assert.Contains(result.Items[0].Issues, i => i.Contains("TargetPageCode"));
        Assert.Contains(result.Items[0].Issues, i => i.Contains("RequiredPermissionKey"));
    }

    [Fact]
    public async Task Invalid_eventcode_format_produces_issue()
    {
        var repo = new InMemoryEventRepo();
        var ev = Event("Workflow Task Assigned!", status: "Active"); // spaces/caps/bang -> invalid
        var service = Service(repo, BuildManifest(new[] { ev }));

        var result = await service.SyncAsync();

        Assert.Equal(1, result.WithIssues);
        Assert.Contains(result.Items[0].Issues, i => i.Contains("EventCode"));
    }

    [Fact]
    public async Task Duplicate_eventcode_produces_issue()
    {
        var repo = new InMemoryEventRepo();
        var a = Event("workflow.task.assigned", pageCode: "WF_TASKS", permission: "workflow.tasks.read", status: "Active");
        var b = Event("workflow.task.assigned", pageCode: "WF_TASKS", permission: "workflow.tasks.read", status: "Active");
        var service = Service(repo, BuildManifest(new[] { a, b }, pageCode: "WF_TASKS", permission: "workflow.tasks.read"));

        var result = await service.SyncAsync();

        Assert.Equal(2, result.EventsDeclared);
        Assert.Contains(result.Items, i => i.Issues.Any(x => x.Contains("Duplicate")));
    }

    [Fact]
    public async Task Second_sync_updates_existing_event_not_duplicates_it()
    {
        var repo = new InMemoryEventRepo();
        var ev = Event("workflow.task.assigned", pageCode: "WF_TASKS", permission: "workflow.tasks.read", status: "Active");
        var service = Service(repo, BuildManifest(new[] { ev }, pageCode: "WF_TASKS", permission: "workflow.tasks.read"));

        await service.SyncAsync();
        var second = await service.SyncAsync();

        Assert.Single(repo.Items);              // uniqueness: upsert by EventCode, no duplicate
        Assert.Equal(1, second.Updated);
    }

    [Fact]
    public async Task Active_template_slots_exclude_deprecated_and_archived()
    {
        var repo = new InMemoryEventRepo();
        repo.Items.Add(Def("a.b.active", NotificationEventStatus.Active));
        repo.Items.Add(Def("a.b.deprecated", NotificationEventStatus.Deprecated));
        repo.Items.Add(Def("a.b.archived", NotificationEventStatus.Archived));
        repo.Items.Add(Def("a.b.draft", NotificationEventStatus.Draft));

        var handler = new GetActiveTemplateSlotsHandler(repo);
        var response = await handler.Handle(new GetActiveTemplateSlotsQuery(), default);

        Assert.True(response.IsSuccessful);
        var slot = Assert.Single(response.Data!);
        Assert.Equal("a.b.active", slot.EventCode);
    }

    // --- MOD-0027-FU03A (Bridge) — SourceType / PlatformSeed foundation ---

    [Fact]
    public void SourceType_enum_manifest_is_zero_and_entity_defaults_to_manifest()
    {
        Assert.Equal(0, (int)NotificationEventSourceType.Manifest);           // backward-compat: missing field => 0
        Assert.Equal(NotificationEventSourceType.Manifest, new NotificationEventDefinition().SourceType);
    }

    [Fact]
    public async Task Manifest_sync_create_writes_sourcetype_manifest()
    {
        var repo = new InMemoryEventRepo();
        var ev = Event("workflow.task.assigned", pageCode: "WF_TASKS", permission: "workflow.tasks.read", status: "Active");
        var service = Service(repo, BuildManifest(new[] { ev }, pageCode: "WF_TASKS", permission: "workflow.tasks.read"));

        await service.SyncAsync();

        var stored = Assert.Single(repo.Items);
        Assert.Equal(NotificationEventSourceType.Manifest, stored.SourceType);
    }

    [Fact]
    public async Task Manifest_sync_does_not_clobber_platformseed_record()
    {
        var repo = new InMemoryEventRepo();
        repo.Items.Add(new NotificationEventDefinition
        {
            EventCode = "workflow.task.assigned",
            SourceType = NotificationEventSourceType.PlatformSeed,
            OwnerModuleId = "MOD-0009",
            Channel = NotificationChannelCode.Email,
            DefaultTemplateKey = "seed.template",
            FallbackDisplayName = "seed",
            RequiredPolicy = "PlatformActor",
            Status = NotificationEventStatus.Active
        });
        var ev = Event("workflow.task.assigned", pageCode: "WF_TASKS", permission: "workflow.tasks.read", status: "Active");
        var service = Service(repo, BuildManifest(new[] { ev }, pageCode: "WF_TASKS", permission: "workflow.tasks.read"));

        var result = await service.SyncAsync();

        var after = Assert.Single(repo.Items);
        Assert.Equal(NotificationEventSourceType.PlatformSeed, after.SourceType);  // untouched
        Assert.Equal("seed.template", after.DefaultTemplateKey);                   // manifest did NOT reconcile it
        Assert.Contains(result.Items, i => i.Outcome == "skipped" && i.Issues.Any(x => x.Contains("Cross-source collision")));
    }

    [Fact]
    public void PlatformSeed_validation_accepts_policy_gated_null_permission_and_skips_catalog()
    {
        // No page code, no manifest permission — a seed only needs a policy (PlatformActor) when permission is null.
        var def = SeedDef("platform.tenant.invited", requiredPolicy: "PlatformActor");
        var v = NotificationEventSeedPlanner.Validate(def, templateExists: true);

        Assert.True(v.IsValid);
        Assert.Empty(v.Issues);
        Assert.Equal(NotificationEventStatus.Active, v.EffectiveStatus);
    }

    [Fact]
    public void PlatformSeed_validation_permission_gated_is_draft_pending_api_pass()
    {
        var def = SeedDef("platform.tenant.invited", requiredPermissionKey: "platform.tenants.quotas.read");
        var v = NotificationEventSeedPlanner.Validate(def, templateExists: true);

        Assert.True(v.IsValid);
        Assert.Equal(NotificationEventStatus.Draft, v.EffectiveStatus);  // deferred to API-side activation pass (§5.1)
    }

    [Fact]
    public void PlatformSeed_validation_missing_template_produces_issue()
    {
        var def = SeedDef("platform.tenant.invited", requiredPolicy: "PlatformActor");
        var v = NotificationEventSeedPlanner.Validate(def, templateExists: false);

        Assert.False(v.IsValid);
        Assert.Contains(v.Issues, i => i.Contains("no seeded template"));
    }

    [Fact]
    public async Task Generic_seed_does_not_clobber_manifest_record()
    {
        var repo = new InMemoryEventRepo();
        repo.Items.Add(Def("platform.tenant.invited", NotificationEventStatus.Active)); // SourceType defaults Manifest
        var seeder = new NotificationEventSeeder(repo, (_, _) => Task.FromResult(true));

        var result = await seeder.SeedAsync(new[] { SeedDef("platform.tenant.invited", requiredPolicy: "PlatformActor") });

        Assert.Equal(1, result.Skipped);
        var after = Assert.Single(repo.Items);
        Assert.Equal(NotificationEventSourceType.Manifest, after.SourceType);  // untouched
    }

    [Fact]
    public async Task Generic_seed_is_idempotent_reconciles_hard_preserves_soft_no_duplicate()
    {
        var repo = new InMemoryEventRepo();
        var seeder = new NotificationEventSeeder(repo, (_, _) => Task.FromResult(true));

        // 1st run creates.
        var r1 = await seeder.SeedAsync(new[]
        {
            SeedDef("platform.tenant.suspended", targetRoute: "/Platform/Tenants", requiredPolicy: "PlatformActor",
                vars: new[] { Var("TenantDisplayName") })
        });
        Assert.Equal(1, r1.Created);
        var stored = Assert.Single(repo.Items);
        Assert.Equal(NotificationEventSourceType.PlatformSeed, stored.SourceType);
        Assert.Equal(NotificationEventStatus.Active, stored.Status); // policy-gated -> Active

        // Operator changes SOFT fields.
        stored.Status = NotificationEventStatus.Archived;
        stored.FallbackDisplayName = "OPERATOR EDIT";

        // 2nd run: changed HARD fields (route + variables).
        var r2 = await seeder.SeedAsync(new[]
        {
            SeedDef("platform.tenant.suspended", targetRoute: "/Platform/Tenants/v2", requiredPolicy: "PlatformActor",
                vars: new[] { Var("TenantDisplayName"), Var("Reason") })
        });

        Assert.Equal(1, r2.Updated);
        var after = Assert.Single(repo.Items);              // no duplicate
        Assert.Equal("/Platform/Tenants/v2", after.TargetRoute);   // HARD reconciled
        Assert.Equal(2, after.RequiredVariables.Count);            // HARD reconciled
        Assert.Equal(NotificationEventStatus.Archived, after.Status);       // SOFT preserved
        Assert.Equal("OPERATOR EDIT", after.FallbackDisplayName);           // SOFT preserved
    }

    [Fact]
    public async Task Active_template_slots_are_source_agnostic()
    {
        var repo = new InMemoryEventRepo();
        repo.Items.Add(Def("workflow.task.assigned", NotificationEventStatus.Active)); // Manifest, Active
        repo.Items.Add(new NotificationEventDefinition
        {
            EventCode = "platform.tenant.invited",
            SourceType = NotificationEventSourceType.PlatformSeed,
            OwnerModuleId = "MOD-0009",
            Channel = NotificationChannelCode.Email,
            DefaultTemplateKey = "tenant.invite.email",
            FallbackDisplayName = "invite",
            RequiredPolicy = "PlatformActor",
            Status = NotificationEventStatus.Active
        });

        var handler = new GetActiveTemplateSlotsHandler(repo);
        var response = await handler.Handle(new GetActiveTemplateSlotsQuery(), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data!.Count);
        Assert.Contains(response.Data!, s => s.EventCode == "platform.tenant.invited"); // PlatformSeed appears in slots
    }

    // --- MOD-0027-FU04A — Tenant Management Notification Event Opt-in (PlatformSeed catalog content) ---

    private static readonly HashSet<string> TenantTemplateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenant.invite.email", "tenant.suspended.email", "tenant.reactivated.email"
    };

    [Fact]
    public void PlatformSeed_catalog_contains_exactly_the_three_tenant_events()
    {
        var defs = NotificationEventSeedCatalog.PlatformSeedDefinitions;

        Assert.Equal(3, defs.Count);
        Assert.Equal(
            new[] { "tenant.lifecycle.reactivated", "tenant.lifecycle.suspended", "tenant.user.invited" },
            defs.Select(d => d.EventCode).OrderBy(x => x).ToArray());

        foreach (var d in defs)
        {
            Assert.Equal(NotificationEventSourceType.PlatformSeed, d.SourceType);
            Assert.Equal("MOD-0009", d.OwnerModuleId);
            Assert.Equal("PlatformAdmin", d.OwnerArea);
            Assert.Equal("Tenant / Environment Management", d.OwnerDisplayName);
            Assert.Equal("/Platform/Tenants", d.TargetRoute);
            Assert.Equal("PlatformActor", d.RequiredPolicy);
            Assert.Null(d.RequiredPermissionKey);                 // policy-gated; platform.tenants.read NOT invented
            Assert.Equal(NotificationChannelCode.Email, d.Channel);
            Assert.True(d.CanTenantOverride);
            Assert.Equal(NotificationEventUsageType.SystemEvent, d.UsageType);
            Assert.Contains(d.DefaultTemplateKey, TenantTemplateKeys); // binds to existing FU02 templates
        }
    }

    [Fact]
    public async Task Tenant_events_seed_as_active_policy_gated()
    {
        var repo = new InMemoryEventRepo();
        var seeder = new NotificationEventSeeder(repo, (key, _) => Task.FromResult(TenantTemplateKeys.Contains(key)));

        var result = await seeder.SeedAsync(NotificationEventSeedCatalog.PlatformSeedDefinitions);

        Assert.Equal(3, result.Created);
        Assert.Equal(0, result.WithIssues);                       // policy-gated + templates resolve => valid
        Assert.Equal(3, repo.Items.Count);
        Assert.All(repo.Items, e => Assert.Equal(NotificationEventStatus.Active, e.Status));
        Assert.All(repo.Items, e => Assert.Equal(NotificationEventSourceType.PlatformSeed, e.SourceType));
    }

    [Fact]
    public async Task Tenant_events_seed_is_idempotent_no_duplicate_on_second_run()
    {
        var repo = new InMemoryEventRepo();
        var seeder = new NotificationEventSeeder(repo, (key, _) => Task.FromResult(TenantTemplateKeys.Contains(key)));

        await seeder.SeedAsync(NotificationEventSeedCatalog.PlatformSeedDefinitions);
        var second = await seeder.SeedAsync(NotificationEventSeedCatalog.PlatformSeedDefinitions);

        Assert.Equal(0, second.Created);
        Assert.Equal(3, second.Updated);
        Assert.Equal(3, repo.Items.Count);                        // no duplicate
    }

    [Fact]
    public async Task Active_template_slots_include_the_three_tenant_events_after_seed()
    {
        var repo = new InMemoryEventRepo();
        var seeder = new NotificationEventSeeder(repo, (key, _) => Task.FromResult(TenantTemplateKeys.Contains(key)));
        await seeder.SeedAsync(NotificationEventSeedCatalog.PlatformSeedDefinitions);

        var handler = new GetActiveTemplateSlotsHandler(repo);
        var response = await handler.Handle(new GetActiveTemplateSlotsQuery(), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(3, response.Data!.Count);
        Assert.Contains(response.Data!, s => s.EventCode == "tenant.user.invited");
        Assert.Contains(response.Data!, s => s.EventCode == "tenant.lifecycle.suspended");
        Assert.Contains(response.Data!, s => s.EventCode == "tenant.lifecycle.reactivated");
    }

    [Fact]
    public async Task Tenant_seed_does_not_clobber_a_manifest_record_with_same_code()
    {
        var repo = new InMemoryEventRepo();
        // A manifest-owned record collides on one of the tenant codes.
        repo.Items.Add(Def("tenant.user.invited", NotificationEventStatus.Active)); // SourceType defaults Manifest
        var seeder = new NotificationEventSeeder(repo, (key, _) => Task.FromResult(TenantTemplateKeys.Contains(key)));

        var result = await seeder.SeedAsync(NotificationEventSeedCatalog.PlatformSeedDefinitions);

        Assert.Equal(1, result.Skipped);                          // the colliding manifest record is skipped
        Assert.Equal(2, result.Created);                          // the other two tenant events created
        var kept = repo.Items.Single(e => e.EventCode == "tenant.user.invited");
        Assert.Equal(NotificationEventSourceType.Manifest, kept.SourceType); // untouched
    }

    // --- helpers ---

    private static NotificationEventManifestSyncService Service(InMemoryEventRepo repo, ModuleManifestDocument manifest) =>
        new(new IModuleManifestProvider[] { new FakeManifestProvider(manifest) }, repo);

    // MOD-0027-FU03A (Bridge) — PlatformSeed definition + variable helpers (test-only fixture; NO tenant event content).
    private static NotificationEventSeedDefinition SeedDef(
        string eventCode,
        string? targetRoute = null,
        string? requiredPolicy = null,
        string? requiredPermissionKey = null,
        TemplateVariableDefinition[]? vars = null) =>
        new(
            EventCode: eventCode,
            SourceType: NotificationEventSourceType.PlatformSeed,
            Channel: NotificationChannelCode.Email,
            DefaultTemplateKey: "tenant.invite.email",
            RequiredVariables: vars ?? new[] { Var("TenantDisplayName") },
            OptionalVariables: Array.Empty<TemplateVariableDefinition>(),
            OwnerModuleId: "MOD-0009",
            OwnerArea: "PlatformAdmin",
            OwnerDisplayName: "Tenant / Environment Management",
            TargetRoute: targetRoute,
            RequiredPolicy: requiredPolicy,
            RequiredPermissionKey: requiredPermissionKey,
            CanTenantOverride: true,
            UsageType: NotificationEventUsageType.SystemEvent,
            DefaultSeverity: NotificationEventSeverity.Info,
            LinkPolicy: NotificationEventLinkPolicy.None,
            DisplayNameKey: null,
            FallbackDisplayName: eventCode,
            Description: null);

    private static TemplateVariableDefinition Var(string name) =>
        new() { Name = name, Type = TemplateVariableType.String, IsRequired = true };

    private static ModuleManifestNotificationEvent Event(
        string eventCode, string? pageCode = null, string? permission = null, string status = "Active") =>
        new(
            EventCode: eventCode,
            Channel: "Email",
            DefaultTemplateKey: "workflow.task.assigned",
            RequiredVariables: new[] { new ModuleManifestNotificationVariable("TaskId") },
            TargetPageCode: pageCode,
            RequiredPermissionKey: permission,
            Status: status);

    private static ModuleManifestDocument BuildManifest(
        IReadOnlyList<ModuleManifestNotificationEvent>? notificationEvents,
        string? pageCode = null, string? permission = null)
    {
        var pages = pageCode is null
            ? Array.Empty<ModuleManifestPage>()
            : new[]
            {
                new ModuleManifestPage(pageCode, pageCode, "/workflow/tasks", permission ?? "workflow.tasks.read",
                    null, true, "List", 10, Array.Empty<ModuleManifestAction>())
            };
        return new ModuleManifestDocument(
            "workflow", "Workflow", "Workflow", "Workflow", "DitenPlatform", "1.0.0", true, 10,
            pages, "bx-git-merge", false, notificationEvents);
    }

    private static NotificationEventDefinition Def(string code, NotificationEventStatus status) => new()
    {
        EventCode = code,
        OwnerModuleId = "workflow",
        Channel = NotificationChannelCode.Email,
        DefaultTemplateKey = code,
        FallbackDisplayName = code,
        Status = status
    };

    private sealed class FakeManifestProvider : IModuleManifestProvider
    {
        private readonly ModuleManifestDocument _doc;
        public FakeManifestProvider(ModuleManifestDocument doc) => _doc = doc;
        public ModuleManifestDocument GetManifest() => _doc;
    }

    private sealed class InMemoryEventRepo : INotificationEventDefinitionRepository
    {
        public List<NotificationEventDefinition> Items { get; } = new();

        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition d, CancellationToken ct = default)
        { Items.Add(d); return Task.FromResult(d); }

        public Task UpdateAsync(NotificationEventDefinition d, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == d.Id);
            if (i >= 0) Items[i] = d;
            return Task.CompletedTask;
        }

        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                string.Equals(x.EventCode, (eventCode ?? "").Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) && !x.IsDeleted));

        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
            string? ownerModuleId = null, NotificationChannelCode? channel = null, NotificationEventStatus? status = null,
            bool? canTenantOverride = null, NotificationEventUsageType? usageType = null,
            int skip = 0, int take = 100, CancellationToken ct = default)
        {
            IEnumerable<NotificationEventDefinition> q = Items.Where(x => !x.IsDeleted);
            if (ownerModuleId is not null) q = q.Where(x => x.OwnerModuleId == ownerModuleId);
            if (status is not null) q = q.Where(x => x.Status == status);
            return Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(q.ToArray());
        }

        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(
                Items.Where(x => !x.IsDeleted && x.Status == NotificationEventStatus.Active).ToArray());
    }
}
