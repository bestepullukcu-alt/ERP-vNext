using Diten.ProcurementService.Api.Controllers;
using Diten.ProcurementService.Api.ModuleRegistration;
using Diten.ProcurementService.Infrastructure.Authorization;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers;

/// <summary>
/// MOD-0140 DL-3 (§16 Revizyon 8 F-1, §14 "Kataloğa giriş", K-2) — on the PRODUCTION manifest provider: the keys of endpoints
/// without a screen enter the Auth catalogue as ApiPermissions (eligibility.read, master.update; Scope Tenant), and
/// <c>read-tenant-wide</c> appears NOWHERE in the manifest (DL-3's deferred part adds it once Auth's ExplicitGrantOnly is on
/// main and deployed first). Every key the [HasAnyPermission] endpoints accept is declared.
/// </summary>
public sealed class SupplierManifestTests
{
    private const string TenantWide = "procurement.suppliers.read-tenant-wide";

    [Fact]
    public void Eligibility_read_and_master_update_are_api_permissions_with_tenant_scope()
    {
        var manifest = new SupplierOnboardingManifestProvider().GetManifest();
        var api = (manifest.ApiPermissions ?? []).ToDictionary(p => p.PermissionKey, p => p.Scope, StringComparer.Ordinal);

        Assert.Equal("Tenant", api["procurement.suppliers.eligibility.read"]);
        Assert.Equal("Tenant", api["procurement.suppliers.master.update"]);
    }

    [Fact]
    public void Read_tenant_wide_is_not_declared_anywhere_in_the_manifest()
    {
        var manifest = new SupplierOnboardingManifestProvider().GetManifest();
        var keys = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey)
            .Concat(manifest.Pages.Select(p => p.RequiredPermission))
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey));

        Assert.DoesNotContain(TenantWide, keys);
    }

    [Fact]
    public void Every_key_an_eligibility_endpoint_accepts_is_declared()
    {
        var manifest = new SupplierOnboardingManifestProvider().GetManifest();
        var declared = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey)
            .Concat(manifest.Pages.Select(p => p.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);
        var accepted = typeof(SuppliersController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes(typeof(HasAnyPermissionAttribute), false).Cast<HasAnyPermissionAttribute>())
            .SelectMany(a => a.Permissions)
            .Distinct()
            .ToList();

        Assert.Equal(3, typeof(SuppliersController).GetMethods().Count(m => m.GetCustomAttributes(typeof(HasAnyPermissionAttribute), false).Length > 0));
        Assert.All(accepted, key => Assert.Contains(key, declared));
    }
}
