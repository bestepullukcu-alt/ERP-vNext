using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Application.Features.Navigation;
using Diten.Platform.Application.Features.Navigation.Handlers;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.SelfRegistration;
using Diten.Platform.Application.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

/// <summary>
/// DCP-004 "Decision amendment 2026-09-15" (BL-410), point 2 — Görev Merkezi is in EVERY tenant user's menu.
///
/// <para><b>Chain measured.</b> The REAL <see cref="WorkAggregationManifestProvider"/> → the catalog row and page
/// descriptor as the reconcile writes them (production <see cref="ModuleCatalogCodeNormalizer"/> and
/// <see cref="ModulePageDescriptorNormalizer"/>) → the REAL <see cref="TenantModuleAccessService"/> for a customer
/// tenant with NO entitlement row and no plan → the REAL <see cref="GetTenantNavigationMenuQueryHandler"/>. The menu
/// row it returns is what the sidebar and Ctrl+K gate per user; for a user with zero permission claims that gate is
/// "show only rows with no required permission", measured on the Web side in
/// <c>Diten.Web.Tests/Navigation/WorkCenterMenuWithoutPermissionsTests</c>.</para>
///
/// <para><b>Copied, and why.</b> The Mongo filter behind <c>GetAssignableAsync</c> (Active and tenant-assignable,
/// <c>ModuleCatalogRepository.cs</c>) and the reconcile's field mapping are restated here, because running Mongo or
/// the full reconcile transaction for a menu question would bury it. Both are cited at the line they copy.</para>
/// </summary>
public sealed class WorkCenterNavigationForEveryTenantUserTests
{
    private static readonly string WorkCenterCode =
        ModuleCatalogCodeNormalizer.Normalize(new WorkAggregationManifestProvider().GetManifest().ModuleCode);

    [Fact]
    public async Task A_tenant_with_no_entitlement_row_gets_Gorev_Merkezi_and_the_page_asks_no_key()
    {
        var menu = await MenuFor(new WorkAggregationManifestProvider().GetManifest());

        var group = Assert.Single(menu, g => g.ModuleCode == WorkCenterCode);
        var item = Assert.Single(group.Items);
        Assert.Equal("/WorkCenterNext", item.RoutePath);
        // A zero-permission user holds no key, so ANY key on this row hides the entry from them in both surfaces.
        Assert.True(string.IsNullOrWhiteSpace(item.RequiredPermission),
            $"The Görev Merkezi menu row requires '{item.RequiredPermission}'; users without it lose the entry.");
    }

    [Fact]
    public async Task Without_baseline_the_same_tenant_gets_no_entry_so_baseline_is_what_opens_it()
    {
        // Non-vacuity: the entitlement wall is real for this tenant, so the entry above is the baseline's doing.
        var manifest = new WorkAggregationManifestProvider().GetManifest() with { IsBaseline = false };

        var menu = await MenuFor(manifest);

        Assert.DoesNotContain(menu, g => g.ModuleCode == WorkCenterCode);
    }

    private static async Task<IReadOnlyList<NavigationModuleGroupDto>> MenuFor(ModuleManifestDocument manifest)
    {
        var tenantId = Guid.NewGuid(); // an ordinary customer tenant: no entitlement row, no subscription plan
        var code = ModuleCatalogCodeNormalizer.Normalize(manifest.ModuleCode);

        // As RegisterModuleManifestCommandHandler seeds the catalog row on first registration (:289-305).
        var catalogItem = new ModuleCatalogItem
        {
            ModuleCode = code,
            ModuleName = manifest.ModuleName,
            DisplayName = manifest.DisplayName,
            Domain = manifest.Domain,
            Service = manifest.Service,
            Status = ModuleCatalogStatus.Active,
            ModuleVersion = manifest.ModuleVersion,
            IsTenantAssignable = manifest.IsTenantAssignable,
            SortOrder = manifest.SortOrder,
            Icon = manifest.Icon,
            IsBaseline = manifest.IsBaseline
        };

        // As the reconcile writes a new page descriptor (:404-420), through the production normalizer.
        var pages = manifest.Pages.Select(page => new ModulePageDescriptor
        {
            TenantId = Guid.Empty,
            ModuleCode = code,
            PageCode = page.PageCode,
            DisplayName = page.DisplayName.Trim(),
            RoutePath = page.RoutePath,
            RequiredPermission = ModulePageDescriptorNormalizer.NormalizeOptionalPermission(page.RequiredPermission),
            ParentPageCode = page.ParentPageCode,
            IsNavigationVisible = page.IsNavigationVisible,
            Status = ModulePageStatus.Active,
            SortOrder = page.SortOrder
        }).ToList();

        var catalog = new Mock<IModuleCatalogRepository>();
        // ModuleCatalogRepository.GetAssignableAsync (:190-195): Active AND IsTenantAssignable.
        catalog.Setup(x => x.GetAssignableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { catalogItem }
                .Where(i => i.Status == ModuleCatalogStatus.Active && i.IsTenantAssignable)
                .ToList());
        catalog.Setup(x => x.GetByCodeAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(catalogItem);

        var entitlements = new Mock<ITenantModuleEntitlementRepository>();
        entitlements.Setup(x => x.GetByTenantAndModuleAsync(tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantModuleEntitlement>());
        var subscriptions = new Mock<ITenantSubscriptionRepository>();
        subscriptions.Setup(x => x.GetCurrentByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantSubscription?)null);

        var pageRepository = new Mock<IModulePageDescriptorRepository>();
        pageRepository.Setup(x => x.GetByModuleAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(pages);

        var domains = new Mock<IModuleDomainRepository>();
        domains.Setup(x => x.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ModuleDomain>());
        var preferences = new Mock<ITenantNavPreferenceRepository>();
        preferences.Setup(x => x.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantNavPreference>());
        var domainPreferences = new Mock<ITenantNavDomainPreferenceRepository>();
        domainPreferences.Setup(x => x.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantNavDomainPreference>());

        var handler = new GetTenantNavigationMenuQueryHandler(
            new PlatformCatalogContract(catalog.Object),
            new TenantModuleAccessService(
                entitlements.Object, catalog.Object, subscriptions.Object, new Mock<ISubscriptionPlanRepository>().Object),
            pageRepository.Object,
            domains.Object,
            preferences.Object,
            domainPreferences.Object,
            new Mock<ITenantContext>().Object);

        var response = await handler.Handle(new GetTenantNavigationMenuQuery(tenantId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        return response.Data!;
    }
}
