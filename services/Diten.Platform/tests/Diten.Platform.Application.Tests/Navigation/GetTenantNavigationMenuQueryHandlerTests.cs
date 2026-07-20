using Diten.Platform.Application.Features.Navigation.Handlers;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

public sealed class GetTenantNavigationMenuQueryHandlerTests
{
    private static AssignableModuleInfo Module(string code, string display, int sort) =>
        Module(code, display, sort, "D");

    private static AssignableModuleInfo Module(string code, string display, int sort, string domain) =>
        new(Guid.NewGuid(), code, $"{code} Module", display, "Desc", domain, "S", "Active", "1.0", false, true, sort, DateTimeOffset.UtcNow, null);

    private static IModuleDomainRepository DomainRepo(params (string Code, string Display)[] domains)
    {
        var mock = new Mock<IModuleDomainRepository>();
        mock.Setup(x => x.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(domains
                .Select(d => new ModuleDomain { Code = d.Code, DisplayName = d.Display, IsActive = true })
                .ToList());
        return mock.Object;
    }

    // Like DomainRepo but carries an explicit platform ModuleDomain.SortOrder per domain (Domain Management).
    private static IModuleDomainRepository DomainRepoSorted(params (string Code, string Display, int Sort)[] domains)
    {
        var mock = new Mock<IModuleDomainRepository>();
        mock.Setup(x => x.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(domains
                .Select(d => new ModuleDomain { Code = d.Code, DisplayName = d.Display, SortOrder = d.Sort, IsActive = true })
                .ToList());
        return mock.Object;
    }

    // Default: tenant has no nav preferences → menu is unchanged from catalog behavior.
    private static ITenantNavPreferenceRepository NoPrefs()
    {
        var mock = new Mock<ITenantNavPreferenceRepository>();
        mock.Setup(x => x.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantNavPreference>());
        return mock.Object;
    }

    private static ITenantNavPreferenceRepository PrefsRepo(params TenantNavPreference[] prefs)
    {
        var mock = new Mock<ITenantNavPreferenceRepository>();
        mock.Setup(x => x.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs.ToList());
        return mock.Object;
    }

    private static ITenantNavDomainPreferenceRepository NoDomainPrefs() => DomainPrefsRepo();

    private static ITenantNavDomainPreferenceRepository DomainPrefsRepo(params TenantNavDomainPreference[] prefs)
    {
        var mock = new Mock<ITenantNavDomainPreferenceRepository>();
        mock.Setup(x => x.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs.ToList());
        return mock.Object;
    }

    private static ModulePageDescriptor Page(
        string moduleCode,
        string pageCode,
        ModulePageStatus status = ModulePageStatus.Active,
        bool navVisible = true,
        string? requiredPermission = null,
        string? parentPageCode = null,
        int sortOrder = 0) =>
        new()
        {
            TenantId = Guid.Empty, // platform-scope template, like self-registration writes
            ModuleCode = moduleCode,
            PageCode = pageCode,
            DisplayName = $"{pageCode} Page",
            RoutePath = $"/{moduleCode}/{pageCode}",
            RequiredPermission = requiredPermission,
            ParentPageCode = parentPageCode,
            IsNavigationVisible = navVisible,
            Status = status,
            SortOrder = sortOrder
        };

    [Fact]
    public async Task EntitledModule_WithActiveVisibleDescriptor_ReturnsItem_AndFiltersDraftAndHidden()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("GOLDENSLIM", "Golden Slim", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor>
            {
                Page("GOLDENSLIM", "RECORDS", requiredPermission: "goldenslim.records.read", sortOrder: 10),
                Page("GOLDENSLIM", "DRAFTPAGE", status: ModulePageStatus.Draft, sortOrder: 20),
                Page("GOLDENSLIM", "HIDDENPAGE", navVisible: false, sortOrder: 30)
            });

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D Display")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        var group = Assert.Single(response.Data!);
        Assert.Equal("GOLDENSLIM", group.ModuleCode);
        Assert.Equal("Golden Slim", group.ModuleDisplayName);
        Assert.Equal("D", group.Domain);                          // catalog domain code carried through
        Assert.Equal("Domain D Display", group.DomainDisplayName); // resolved from platform_module_domains
        var item = Assert.Single(group.Items); // Draft + hidden filtered out
        Assert.Equal("RECORDS", item.PageCode);
        Assert.Equal("/GOLDENSLIM/RECORDS", item.RoutePath);
        Assert.Equal("goldenslim.records.read", item.RequiredPermission);
        // FIX-CTRLK-GROUPING+CREATE — a slim/offcanvas module (no "/Create" page) exposes no navigable create route.
        Assert.Null(group.CreateRoute);
        Assert.Null(group.CreatePermission);
    }

    [Fact]
    public async Task Module_WithNavigableCreatePage_ExposesCreateRouteAndPermission_ButKeepsItCreateOutOfItems()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("GOLDENCOMPACT", "Golden Compact", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "GOLDENCOMPACT", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("GOLDENCOMPACT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor>
            {
                Page("GOLDENCOMPACT", "RECORDS", requiredPermission: "goldencompact.records.read", sortOrder: 10),
                // Compact "Add New": non-nav-visible, route ends "/Create", no {id}, carries the create permission.
                Page("GOLDENCOMPACT", "CREATE", navVisible: false, requiredPermission: "goldencompact.records.create", sortOrder: 20),
                // Edit page carries an {id} placeholder → must NOT be picked as a navigable create route.
                new ModulePageDescriptor
                {
                    TenantId = Guid.Empty, ModuleCode = "GOLDENCOMPACT", PageCode = "EDIT", DisplayName = "Edit",
                    RoutePath = "/GOLDENCOMPACT/Edit/{id}", IsNavigationVisible = false, Status = ModulePageStatus.Active, SortOrder = 30
                }
            });

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D Display")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var group = Assert.Single(response.Data!);
        Assert.Equal("/GOLDENCOMPACT/CREATE", group.CreateRoute);            // navigable create route exposed
        Assert.Equal("goldencompact.records.create", group.CreatePermission); // its per-user permission gate
        var item = Assert.Single(group.Items);                               // only the nav-visible RECORDS is a menu item
        Assert.Equal("RECORDS", item.PageCode);
    }

    [Fact]
    public async Task DomainDisplayName_ResolvesAcrossSeparatorAndCaseDifferences()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            // Manifest domain is PascalCase, with no separators.
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("LEGALENTITY", "Legal Entity", 1, "MasterDataManagement") });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "LEGALENTITY", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("LEGALENTITY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor> { Page("LEGALENTITY", "INDEX", sortOrder: 10) });

        // Domain ROW code uses dashes + different case — must still resolve to the display name.
        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepo(("master-data-management", "Master Data Management")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var group = Assert.Single(response.Data!);
        Assert.Equal("MasterDataManagement", group.Domain);            // raw catalog code carried through
        Assert.Equal("Master Data Management", group.DomainDisplayName); // resolved despite separator/case mismatch
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateDomainRows_ResolveDeterministically_RegardlessOfRepoOrder(bool reversed)
    {
        // FIX-DOMAIN-DEDUP (defense) — two rows share a normalized key ("MASTERDATAMANAGEMENT"). The pick must be
        // DETERMINISTIC (active, then lowest SortOrder) whichever order the repo returns them in, so a stray
        // duplicate can never randomize the domain's display name / order. (Moot once the unique index lands.)
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("LEGALENTITY", "Legal Entity", 1, "MasterDataManagement") });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "LEGALENTITY", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var pageRepo = new Mock<IModulePageDescriptorRepository>();
        pageRepo.Setup(x => x.GetByModuleAsync("LEGALENTITY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor> { Page("LEGALENTITY", "INDEX", sortOrder: 10) });

        var rows = new List<ModuleDomain>
        {
            new() { Code = "MASTER-DATA-MANAGEMENT", DisplayName = "Should Not Win", SortOrder = 30, IsActive = true },
            new() { Code = "MASTERDATAMANAGEMENT", DisplayName = "Lowest Sort Wins", SortOrder = 10, IsActive = true }
        };
        if (reversed) rows.Reverse();

        var domainRepo = new Mock<IModuleDomainRepository>();
        domainRepo.Setup(x => x.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, pageRepo.Object,
            domainRepo.Object, NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var group = Assert.Single(response.Data!);
        Assert.Equal("Lowest Sort Wins", group.DomainDisplayName); // deterministic pick regardless of input order
    }

    [Fact]
    public async Task UnresolvedDomainCode_FallsBackToTheRawCodeAsDisplayName()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("GOLDENSLIM", "Golden Slim", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor> { Page("GOLDENSLIM", "RECORDS", sortOrder: 10) });

        // No domain rows registered → display name must fall back to the raw catalog code ("D").
        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object, DomainRepo(), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var group = Assert.Single(response.Data!);
        Assert.Equal("D", group.Domain);
        Assert.Equal("D", group.DomainDisplayName);
    }

    [Fact]
    public async Task NotEntitledModule_ReturnsEmpty_AndNeverReadsDescriptors()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("GOLDENSLIM", "Golden Slim", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "GOLDENSLIM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var repo = new Mock<IModulePageDescriptorRepository>();

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D Display")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data!);
        repo.Verify(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EntitledModule_WithNoVisibleDescriptors_OmitsGroup()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo> { Module("HR", "Human Resources", 1) });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor>()); // self-registered but no nav descriptors yet

        var handler = new GetTenantNavigationMenuQueryHandler(catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D Display")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!);
    }

    // ── FEAT-TENANT-NAV-PREFS ──

    private static (Mock<IPlatformCatalogContract> Catalog, Mock<ITenantModuleAccessService> Access, Mock<IModulePageDescriptorRepository> Repo) ThreeModuleScenario(Guid tenantId)
    {
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1),
                Module("BETA", "Beta", 2),
                Module("GAMMA", "Gamma", 3)
            });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        foreach (var code in new[] { "ALPHA", "BETA", "GAMMA" })
        {
            repo.Setup(x => x.GetByModuleAsync(code, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ModulePageDescriptor> { Page(code, "INDEX", sortOrder: 10) });
        }

        return (catalog, access, repo);
    }

    [Fact]
    public async Task Preferences_Hide_Rename_And_Reorder_AreApplied()
    {
        var tenantId = Guid.NewGuid();
        var (catalog, access, repo) = ThreeModuleScenario(tenantId);

        // Hide BETA; rename ALPHA; reorder GAMMA before ALPHA (UI sends explicit order for the visible modules).
        var prefs = PrefsRepo(
            new TenantNavPreference { TenantId = tenantId, ModuleCode = "BETA", IsHidden = true },
            new TenantNavPreference { TenantId = tenantId, ModuleCode = "ALPHA", DisplayNameOverride = "Custom Alpha", SortOrder = 2 },
            new TenantNavPreference { TenantId = tenantId, ModuleCode = "GAMMA", SortOrder = 1 });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D")), prefs, NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var codes = response.Data!.Select(g => g.ModuleCode).ToList();
        Assert.Equal(new[] { "GAMMA", "ALPHA" }, codes);                  // BETA hidden, GAMMA reordered first
        Assert.Equal("Custom Alpha", response.Data!.Single(g => g.ModuleCode == "ALPHA").ModuleDisplayName);
    }

    [Fact]
    public async Task NoPreferences_LeavesMenuUnchanged()
    {
        var tenantId = Guid.NewGuid();
        var (catalog, access, repo) = ThreeModuleScenario(tenantId);

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        // Catalog order preserved, nothing hidden/renamed.
        Assert.Equal(new[] { "ALPHA", "BETA", "GAMMA" }, response.Data!.Select(g => g.ModuleCode).ToList());
        Assert.Equal("Alpha", response.Data!.Single(g => g.ModuleCode == "ALPHA").ModuleDisplayName);
    }

    [Fact]
    public async Task Preference_ForNonEntitledModule_HasNoEffect()
    {
        var tenantId = Guid.NewGuid();
        var (catalog, access, repo) = ThreeModuleScenario(tenantId);

        // A stale preference for a module the tenant is NOT entitled to (not in the menu) must not break ordering.
        var prefs = PrefsRepo(
            new TenantNavPreference { TenantId = tenantId, ModuleCode = "DELTA", IsHidden = true, DisplayNameOverride = "X" });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D")), prefs, NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(new[] { "ALPHA", "BETA", "GAMMA" }, response.Data!.Select(g => g.ModuleCode).ToList());
    }

    // ── FEAT-NAVPREFS-DOMAINS ──

    [Fact]
    public async Task DomainPreference_OverridesDomainSortOrder_AndRenamesDomain()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1, "SALES"),
                Module("BETA", "Beta", 2, "FINANCE")
            });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync("ALPHA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor> { Page("ALPHA", "INDEX", sortOrder: 10) });
        repo.Setup(x => x.GetByModuleAsync("BETA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModulePageDescriptor> { Page("BETA", "INDEX", sortOrder: 10) });

        // FINANCE moved first (0) + renamed; SALES second (1).
        var domainPrefs = DomainPrefsRepo(
            new TenantNavDomainPreference { TenantId = tenantId, DomainCode = "FINANCE", SortOrder = 0, DisplayNameOverride = "Money" },
            new TenantNavDomainPreference { TenantId = tenantId, DomainCode = "SALES", SortOrder = 1 });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepo(("SALES", "Sales"), ("FINANCE", "Finance")),
            NoPrefs(), domainPrefs, new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var sales = response.Data!.Single(g => g.ModuleCode == "ALPHA");
        var finance = response.Data!.Single(g => g.ModuleCode == "BETA");
        Assert.Equal(1, sales.DomainSortOrder);
        Assert.Equal(0, finance.DomainSortOrder);
        Assert.Equal("Money", finance.DomainDisplayName);   // domain renamed
        Assert.Equal("Sales", sales.DomainDisplayName);      // untouched domain unchanged
    }

    // ── FIX-DOMAIN-SORT — Domain Management's ModuleDomain.SortOrder is the authoritative base domain order ──

    // Two domains whose ModuleDomain.SortOrder order is the REVERSE of the module first-appearance order.
    // ALPHA/SALES appears first in the catalog, BETA/FINANCE second — but FINANCE has the lower platform
    // SortOrder, so the domain groups must come out FINANCE-before-SALES, driven by SortOrder, not module order.
    [Fact]
    public async Task DomainSortOrder_FromDomainManagement_OverridesModuleFirstAppearanceOrder()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1, "SALES"),   // first appearance rank 0
                Module("BETA", "Beta", 2, "FINANCE")     // first appearance rank 1
            });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new List<ModulePageDescriptor> { Page(code, "INDEX", sortOrder: 10) });

        // Platform SortOrder: FINANCE=0, SALES=5 → reverse of module order.
        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepoSorted(("SALES", "Sales", 5), ("FINANCE", "Finance", 0)),
            NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var sales = response.Data!.Single(g => g.ModuleCode == "ALPHA");
        var finance = response.Data!.Single(g => g.ModuleCode == "BETA");
        // FINANCE ranks before SALES purely because its platform SortOrder is lower — not module order.
        Assert.Equal(0, finance.DomainSortOrder);
        Assert.Equal(1, sales.DomainSortOrder);
    }

    // Equal platform SortOrders fall back to module first-appearance as a stable tiebreak.
    [Fact]
    public async Task DomainSortOrder_EqualPlatformSortOrders_FallBackToFirstAppearance()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1, "SALES"),   // first appearance rank 0
                Module("BETA", "Beta", 2, "FINANCE")     // first appearance rank 1
            });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new List<ModulePageDescriptor> { Page(code, "INDEX", sortOrder: 10) });

        // Both SortOrder=3 → tie broken by first-appearance (SALES before FINANCE).
        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepoSorted(("SALES", "Sales", 3), ("FINANCE", "Finance", 3)),
            NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.Equal(0, response.Data!.Single(g => g.ModuleCode == "ALPHA").DomainSortOrder);
        Assert.Equal(1, response.Data!.Single(g => g.ModuleCode == "BETA").DomainSortOrder);
    }

    // Tenant domain-pref SortOrder still wins over the platform ModuleDomain.SortOrder base.
    [Fact]
    public async Task DomainSortOrder_TenantPreference_OverridesPlatformSortOrder()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1, "SALES"),
                Module("BETA", "Beta", 2, "FINANCE")
            });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new List<ModulePageDescriptor> { Page(code, "INDEX", sortOrder: 10) });

        // Platform SortOrder base would put SALES(0) before FINANCE(1). The tenant pref pushes SALES to 9,
        // flipping the order (FINANCE now before SALES) — proving the tenant value overrides the platform base.
        var domainPrefs = DomainPrefsRepo(
            new TenantNavDomainPreference { TenantId = tenantId, DomainCode = "SALES", SortOrder = 9 });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepoSorted(("SALES", "Sales", 0), ("FINANCE", "Finance", 5)),
            NoPrefs(), domainPrefs, new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var sales = response.Data!.Single(g => g.ModuleCode == "ALPHA");
        var finance = response.Data!.Single(g => g.ModuleCode == "BETA");
        Assert.Equal(9, sales.DomainSortOrder);    // tenant override replaces the platform base
        Assert.Equal(1, finance.DomainSortOrder);   // untouched → keeps its platform base rank
        Assert.True(finance.DomainSortOrder < sales.DomainSortOrder); // order flipped from the platform base
    }

    // ── FEAT-NAV-L10N — override flags drive the frontend's "localize default vs render override as-typed" choice ──

    [Fact]
    public async Task ModuleRename_SetsModuleDisplayNameIsOverride_DefaultStaysFalse()
    {
        var tenantId = Guid.NewGuid();
        var (catalog, access, repo) = ThreeModuleScenario(tenantId);

        // ALPHA renamed (tenant free text) → override flag; BETA untouched → default (localizable).
        var prefs = PrefsRepo(
            new TenantNavPreference { TenantId = tenantId, ModuleCode = "ALPHA", DisplayNameOverride = "Custom Alpha" });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D")), prefs, NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var alpha = response.Data!.Single(g => g.ModuleCode == "ALPHA");
        var beta = response.Data!.Single(g => g.ModuleCode == "BETA");
        Assert.True(alpha.ModuleDisplayNameIsOverride);
        Assert.Equal("Custom Alpha", alpha.ModuleDisplayName);
        Assert.False(beta.ModuleDisplayNameIsOverride); // no override → frontend localizes by ModuleCode
    }

    [Fact]
    public async Task NoModulePreference_LeavesModuleDisplayNameIsOverrideFalse()
    {
        var tenantId = Guid.NewGuid();
        var (catalog, access, repo) = ThreeModuleScenario(tenantId);

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object, DomainRepo(("D", "Domain D")), NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.All(response.Data!, g => Assert.False(g.ModuleDisplayNameIsOverride));
        Assert.All(response.Data!, g => Assert.False(g.DomainDisplayNameIsOverride));
    }

    [Fact]
    public async Task DomainRename_SetsDomainDisplayNameIsOverride_UntouchedDomainStaysFalse()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1, "SALES"),
                Module("BETA", "Beta", 2, "FINANCE")
            });

        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new List<ModulePageDescriptor> { Page(code, "INDEX", sortOrder: 10) });

        // FINANCE renamed (tenant free text); SALES untouched.
        var domainPrefs = DomainPrefsRepo(
            new TenantNavDomainPreference { TenantId = tenantId, DomainCode = "FINANCE", DisplayNameOverride = "Money" });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepo(("SALES", "Sales"), ("FINANCE", "Finance")),
            NoPrefs(), domainPrefs, new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        var finance = response.Data!.Single(g => g.ModuleCode == "BETA");
        var sales = response.Data!.Single(g => g.ModuleCode == "ALPHA");
        Assert.True(finance.DomainDisplayNameIsOverride);
        Assert.Equal("Money", finance.DomainDisplayName);
        Assert.False(sales.DomainDisplayNameIsOverride); // no override → frontend localizes by domain code
    }

    [Fact]
    public async Task NoDomainPreference_StampsImplicitCatalogRank()
    {
        var tenantId = Guid.NewGuid();
        var catalog = new Mock<IPlatformCatalogContract>();
        catalog.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AssignableModuleInfo>
            {
                Module("ALPHA", "Alpha", 1, "SALES"),
                Module("BETA", "Beta", 2, "FINANCE")
            });
        var access = new Mock<ITenantModuleAccessService>();
        access.Setup(x => x.HasAccessAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var repo = new Mock<IModulePageDescriptorRepository>();
        repo.Setup(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => new List<ModulePageDescriptor> { Page(code, "INDEX", sortOrder: 10) });

        var handler = new GetTenantNavigationMenuQueryHandler(
            catalog.Object, access.Object, repo.Object,
            DomainRepo(("SALES", "Sales"), ("FINANCE", "Finance")),
            NoPrefs(), NoDomainPrefs(), new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        // First-appearance rank: SALES=0, FINANCE=1 (catalog order preserved).
        Assert.Equal(0, response.Data!.Single(g => g.ModuleCode == "ALPHA").DomainSortOrder);
        Assert.Equal(1, response.Data!.Single(g => g.ModuleCode == "BETA").DomainSortOrder);
    }
}
