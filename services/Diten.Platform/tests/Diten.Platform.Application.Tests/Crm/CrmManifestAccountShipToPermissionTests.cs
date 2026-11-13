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

// WP-0149-SHIPTO-DL3b — MOD-0149 §32.3 / AC-ST-10, CRM-ACCOUNT-LOOKUP 1.1. CrmService enforces crm.account-ship-to.read on
// U-4 (GET /api/crm/accounts/{id}/ship-to); only a key the module manifest declares reaches the Auth permission catalog.
// The key has no UI, so the CRM manifest declares it as an API-only permission with Tenant scope — on no page and no
// action (the WP-0149-AR-DL1-FU01 precedent, CrmManifestAccountLookupPermissionTests). Every assertion runs against the
// PRODUCTION CrmManifestProvider and, for the sync path, the PRODUCTION RegisterModuleManifestCommandHandler. The CRM
// page list is NOT pinned here: the pages are only searched for the key.
public sealed class CrmManifestAccountShipToPermissionTests
{
    private const string Key = "crm.account-ship-to.read";
    private const string LookupKey = "crm.account.lookup-validation";

    private static readonly ModuleManifestDocument Manifest = new CrmManifestProvider().GetManifest();

    [Fact]
    public void AC_ST_10_the_ship_to_key_is_declared_exactly_once_as_a_Tenant_api_permission_with_a_display_name()
    {
        var declared = Assert.Single(
            Manifest.ApiPermissions ?? [], p => string.Equals(p.PermissionKey, Key, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(Key, declared.PermissionKey);
        Assert.Equal("Tenant", declared.Scope); // ordinal: "tenant" is not the catalog's scope value
        Assert.False(string.IsNullOrWhiteSpace(declared.DisplayName));
    }

    [Fact]
    public void AC_ST_10_the_ship_to_key_is_on_no_page_and_no_page_action()
    {
        Assert.NotEmpty(Manifest.Pages); // the search below is not vacuous
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

    /// <summary>The lookup key (U-1 / U-3) stays its own entry: the ship-to key is added next to it, not instead of it.</summary>
    [Fact]
    public void AC_ST_10_the_lookup_key_is_still_declared_once_beside_the_ship_to_key()
    {
        var lookup = Assert.Single(Manifest.ApiPermissions ?? [], p => p.PermissionKey == LookupKey);

        Assert.Equal("Tenant", lookup.Scope);
    }

    // The registration path: pushing the real CRM manifest through the real reconcile sends the key to the Auth catalog
    // sync exactly once, owned by CRM, with Tenant scope — and creates no catalog page or action for it.
    [Fact]
    public async Task AC_ST_10_registering_the_CRM_manifest_syncs_the_ship_to_key_to_the_Auth_catalog_with_Tenant_scope()
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
        var call = Assert.Single(synced, s => string.Equals(s.Key, Key, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(Key, call.Key);
        Assert.Equal("CRM", call.ModuleCode);
        Assert.Equal("Tenant", call.Scope);
        Assert.Equal(Manifest.Pages.Count, createdPages.Count);
        Assert.DoesNotContain(createdPages, p => string.Equals(p.RequiredPermission, Key, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(createdActions, a => string.Equals(a.PermissionKey, Key, StringComparison.OrdinalIgnoreCase));
    }
}
