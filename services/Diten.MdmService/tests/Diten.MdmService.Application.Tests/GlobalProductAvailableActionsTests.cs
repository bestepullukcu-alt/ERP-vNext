using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using System.Reflection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductAvailableActionsTests
{
    [Fact]
    public void Request_correction_is_server_projected_only_for_approved_unbound_authorized_product()
    {
        var product = new GlobalProduct { LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var authorized = new Actor(ProductIdentityLifecyclePermissions.GlobalProductRequestCorrection);

        Assert.Contains("REQUEST_CORRECTION",
            Actions(product, authorized));

        product.ActiveLifecycleOperation = new(GlobalProductLifecycleOperationKind.Correction, Guid.NewGuid(), 0);
        Assert.DoesNotContain("REQUEST_CORRECTION",
            Actions(product, authorized));

        product.ActiveLifecycleOperation = null;
        Assert.DoesNotContain("REQUEST_CORRECTION",
            Actions(product, new Actor()));
    }

    [Fact]
    public void Request_retirement_is_server_projected_only_for_approved_unbound_authorized_product()
    {
        var product = new GlobalProduct { LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var authorized = new Actor(ProductIdentityLifecyclePermissions.GlobalProductRequestRetirement);

        Assert.Contains("REQUEST_RETIREMENT", Actions(product, authorized));

        product.ActiveLifecycleOperation = new(GlobalProductLifecycleOperationKind.Retirement, Guid.NewGuid(), 0);
        Assert.DoesNotContain("REQUEST_RETIREMENT", Actions(product, authorized));

        product.ActiveLifecycleOperation = null;
        Assert.DoesNotContain("REQUEST_RETIREMENT", Actions(product, new Actor()));
    }

    // MOD-0290-FU05 slice 2: CHANGE_BRAND only for an approved product, with no lifecycle operation, to the update-brand key.
    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.Draft)]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public void Change_brand_is_server_projected_only_for_approved_unbound_authorized_product(ProductIdentityLifecycleStatus notApproved)
    {
        var product = new GlobalProduct { LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var authorized = new Actor(ProductIdentityLifecyclePermissions.GlobalProductUpdateBrand);

        Assert.Contains("CHANGE_BRAND", Actions(product, authorized));
        Assert.DoesNotContain("CHANGE_BRAND", Actions(product, new Actor("mdm.global-products.update")));

        product.ActiveLifecycleOperation = new(GlobalProductLifecycleOperationKind.Correction, Guid.NewGuid(), 0);
        Assert.DoesNotContain("CHANGE_BRAND", Actions(product, authorized));

        product.ActiveLifecycleOperation = null;
        product.LifecycleStatus = notApproved;
        Assert.DoesNotContain("CHANGE_BRAND", Actions(product, authorized));
    }

    private static IReadOnlyList<string> Actions(
        GlobalProduct product,
        IProductIdentityLifecycleActorContext actor) =>
        Assert.IsAssignableFrom<IReadOnlyList<string>>(
            typeof(GetGlobalProductByIdHandler)
                .GetMethod("BuildAvailableActions", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [product, actor]));

    private sealed class Actor(params string[] permissions) : IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId)
        {
            subjectId = Guid.NewGuid();
            return true;
        }

        public bool HasPermission(string permission) => permissions.Contains(permission, StringComparer.Ordinal);
    }
}
