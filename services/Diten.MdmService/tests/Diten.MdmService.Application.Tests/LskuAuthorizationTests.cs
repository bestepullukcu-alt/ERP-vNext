using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuAuthorizationTests
{
    [Theory]
    [InlineData(nameof(LskusController.GetAll), "mdm.lskus.read")]
    [InlineData(nameof(LskusController.GetById), "mdm.lskus.read")]
    [InlineData(nameof(LskusController.GetCreateOptions), "mdm.lskus.create")]
    [InlineData(nameof(LskusController.CreateDraft), "mdm.lskus.create")]
    public void Every_endpoint_demands_the_exact_permission(string methodName, string permission)
    {
        var method = typeof(LskusController).GetMethod(methodName)!;
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal($"Permission:{permission}", attribute.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Controller_requires_authentication_and_declares_only_read_and_create_permissions()
    {
        Assert.NotNull(typeof(LskusController).GetCustomAttribute<AuthorizeAttribute>());
        var policies = typeof(LskusController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>())
            .Select(attribute => attribute.Policy)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(["Permission:mdm.lskus.create", "Permission:mdm.lskus.read"],
            policies.OrderBy(policy => policy, StringComparer.Ordinal));
    }
}
