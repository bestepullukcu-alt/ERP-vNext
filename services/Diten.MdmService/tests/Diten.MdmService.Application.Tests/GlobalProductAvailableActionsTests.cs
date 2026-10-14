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
