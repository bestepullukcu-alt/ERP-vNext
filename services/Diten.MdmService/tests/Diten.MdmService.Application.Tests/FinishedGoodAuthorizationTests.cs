using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodAuthorizationTests
{
    [Theory]
    [InlineData(nameof(FinishedGoodsController.GetAll), "mdm.finished-goods.read")]
    [InlineData(nameof(FinishedGoodsController.GetById), "mdm.finished-goods.read")]
    [InlineData(nameof(FinishedGoodsController.GetGskuSelector), "mdm.finished-goods.create")]
    [InlineData(nameof(FinishedGoodsController.CreateDraft), "mdm.finished-goods.create")]
    // FG FIX1 (review m8): the audited Draft cancel undoes a creation — the create key (LSKU precedent).
    [InlineData(nameof(FinishedGoodsController.CancelDraft), "mdm.finished-goods.create")]
    // FG FIX1 item 2: the operator repair of leaked admissions — the recover key, as for LSKU.
    [InlineData(nameof(FinishedGoodsController.RepairChildAdmissions), "mdm.product-identity.lifecycle-operations.recover")]
    public void Every_endpoint_fails_closed_on_the_exact_named_permission(string methodName, string permission)
    {
        var method = typeof(FinishedGoodsController).GetMethod(methodName)!;
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());

        Assert.Equal($"Permission:{permission}", attribute.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Controller_declares_exactly_its_permission_candidates_and_no_manage_key()
    {
        const string prefix = "Permission:";
        var permissions = typeof(FinishedGoodsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>())
            .Select(attribute => attribute.Policy![prefix.Length..])
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            ["mdm.finished-goods.create", "mdm.finished-goods.read", "mdm.product-identity.lifecycle-operations.recover"],
            permissions.OrderBy(permission => permission, StringComparer.Ordinal));
        Assert.DoesNotContain(permissions, permission => permission.Contains("manage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Controller_itself_requires_authentication_and_has_no_anonymous_escape_hatch()
    {
        var type = typeof(FinishedGoodsController);

        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
