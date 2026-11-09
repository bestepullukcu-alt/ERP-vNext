using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static Diten.Platform.Application.Tests.GlobalApplicability.GlobalApplicabilityTestDependencies;

namespace Diten.Platform.Application.Tests.Crm;

// WP-0149-AR-DL1-FU01 — MOD-0149 §30, CRM-ACCOUNT-LOOKUP 1.0.0. CrmService enforces crm.account.lookup-validation on the
// Treasury AR endpoints U-1 / U-3; only a key the module manifest declares reaches the Auth permission catalog. The key
// has no UI, so the CRM manifest declares it as an API-only permission (FU-RSV-01) with Tenant scope — on no page and
// no action. Every assertion runs against the PRODUCTION CrmManifestProvider (and, for the sync path, the PRODUCTION
// RegisterModuleManifestCommandHandler).
public sealed class CrmManifestAccountLookupPermissionTests
{
    private const string Key = "crm.account.lookup-validation";

    private static readonly ModuleManifestDocument Manifest = new CrmManifestProvider().GetManifest();

    // The 25 CRM pages on origin/main ed722a700 before this change, in manifest order. FU01 adds no page.
    private static readonly string[] PageCodesBefore =
    [
        "ACCOUNTS", "CONTACTS", "TERRITORY_MANAGEMENT", "SEGMENTS", "STRATEGY_TEMPLATES", "CAMPAIGNS", "VISIT_WORKSPACE",
        "VISIT_EVIDENCE_SETTINGS", "CRM_DOCTOR_INSIGHT_THRESHOLDS", "VISIT_PLANNING", "VISIT_EXECUTION", "PLANNED_VISITS",
        "CYCLE_PERIODS", "CYCLE_CAPACITIES", "CONSENT_PREFERENCES", "KNOWLEDGE", "KNOWLEDGE_CONCEPTS", "KNOWLEDGE_PATHS",
        "CONTENT_ENGAGEMENT_JOURNEYS", "CLAIMS", "OBJECTION_RESPONSES", "SAFETY_TEXTS", "LEGAL_PROFILES",
        "ELIGIBILITY_POLICIES", "VISIT_FREQUENCY_POLICIES"
    ];

    // (a)
    [Fact]
    public void The_lookup_key_is_declared_exactly_once_as_a_Tenant_api_permission()
    {
        var declared = Assert.Single(Manifest.ApiPermissions ?? [], p => p.PermissionKey == Key);
        Assert.Equal("Tenant", declared.Scope);
        Assert.False(string.IsNullOrWhiteSpace(declared.DisplayName));
    }

    // (b)
    [Fact]
    public void The_lookup_key_is_on_no_page_and_no_page_action()
    {
        var onPages = Manifest.Pages
            .Where(p => string.Equals(p.RequiredPermission, Key, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.PageCode);
        var onActions = Manifest.Pages
            .SelectMany(p => p.Actions.Select(a => (Page: p.PageCode, Action: a)))
            .Where(x => string.Equals(x.Action.PermissionKey, Key, StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Page}:{x.Action.ActionCode}");

        Assert.Empty(onPages);
        Assert.Empty(onActions);
    }

    // (c)
    [Fact]
    public void Every_api_permission_is_crm_prefixed_and_listed_once()
    {
        var keys = (Manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToList();

        Assert.NotEmpty(keys);
        Assert.All(keys, k => Assert.StartsWith("crm.", k, StringComparison.Ordinal));
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // (d) CrmManifestRegulatoryTextsTests / CrmManifestVisitPlanningTests pin individual pages and code uniqueness; this
    // pins the whole page list, so an API-only key can never be "declared" by adding a page.
    [Fact]
    public void The_module_identity_and_the_page_list_are_unchanged()
    {
        Assert.Equal("CRM", Manifest.ModuleCode);
        Assert.Equal("1.0.0", Manifest.ModuleVersion);
        Assert.Equal(60, Manifest.SortOrder);
        Assert.Equal(PageCodesBefore, Manifest.Pages.Select(p => p.PageCode).ToArray());
    }

    // (e) The registration path: pushing the real CRM manifest through the real reconcile sends the key to the Auth
    // catalog sync exactly once, owned by CRM, with Tenant scope — and creates no catalog page or action for it.
    [Fact]
    public async Task Registering_the_CRM_manifest_syncs_the_lookup_key_to_the_Auth_catalog_with_Tenant_scope()
    {
        var synced = new List<(string? Key, string? ModuleCode, string? Scope)>();
        var sync = new Mock<ICatalogPermissionSyncService>();
        sync.Setup(s => s.SyncPermissionAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string?, string?, string?, string?, CancellationToken>((key, _, module, scope, _) => synced.Add((key, module, scope)))
            .ReturnsAsync(CatalogPermissionSyncStatus.Synced);

        var catalog = new Mock<IModuleCatalogRepository>();
        catalog.Setup(c => c.CreateAsync(It.IsAny<ModuleCatalogItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModuleCatalogItem item, CancellationToken _) => item);

        var createdPages = new List<ModulePageDescriptor>();
        var pages = new Mock<IModulePageDescriptorRepository>();
        pages.Setup(p => p.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ModulePageDescriptor>());
        pages.Setup(p => p.CreateAsync(It.IsAny<ModulePageDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<ModulePageDescriptor, CancellationToken>((d, _) => createdPages.Add(d))
            .ReturnsAsync((ModulePageDescriptor d, CancellationToken _) => d);

        var createdActions = new List<ModulePageActionDescriptor>();
        var actions = new Mock<IModulePageActionDescriptorRepository>();
        actions.Setup(a => a.GetByPageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ModulePageActionDescriptor>());
        actions.Setup(a => a.CreateAsync(It.IsAny<ModulePageActionDescriptor>(), It.IsAny<CancellationToken>()))
            .Callback<ModulePageActionDescriptor, CancellationToken>((d, _) => createdActions.Add(d))
            .ReturnsAsync((ModulePageActionDescriptor d, CancellationToken _) => d);

        var taxonomy = new Mock<Diten.Platform.Application.Features.ModuleCatalog.Services.IModuleTaxonomyResolver>();
        taxonomy.Setup(t => t.ResolveServiceCodeAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? raw, CancellationToken _) => raw?.Trim() ?? string.Empty);

        var domains = new Mock<IModuleDomainRepository>();
        domains.Setup(d => d.QueryAsync(It.IsAny<ModuleDomainQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ModuleDomain>)[new ModuleDomain { Code = "SALES", DisplayName = "Sales", IsActive = true }], 1L));

        var handler = new RegisterModuleManifestCommandHandler(
            Module(catalog.Object), pages.Object, actions.Object, sync.Object, taxonomy.Object, domains.Object,
            NullLogger<RegisterModuleManifestCommandHandler>.Instance, Coordinator, State);

        var result = await handler.Handle(new RegisterModuleManifestCommand(Manifest), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        var call = Assert.Single(synced, s => s.Key == Key);
        Assert.Equal("CRM", call.ModuleCode);
        Assert.Equal("Tenant", call.Scope);
        Assert.Equal(PageCodesBefore.Length, createdPages.Count);
        Assert.DoesNotContain(createdPages, p => string.Equals(p.RequiredPermission, Key, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(createdActions, a => string.Equals(a.PermissionKey, Key, StringComparison.OrdinalIgnoreCase));
    }
}
