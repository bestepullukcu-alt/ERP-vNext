using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-12-API (CAND-CAP-0011) — ClaimsController surface tests (reflection over attributes, mirroring the knowledge
/// controller attribute tests). Asserts the class is authorized, each action carries the right HTTP verb + canonical
/// route + HasPermission key (Read / Manage / Approve), and that no delete / patch endpoint exists.
/// </summary>
public sealed class ClaimsControllerTests
{
    private const string Base = "api/crm/content-composition/claims";

    [Fact]
    public void Controller_requires_authorization()
        => Assert.NotNull(typeof(ClaimsController).GetCustomAttribute<AuthorizeAttribute>());

    [Fact]
    public void No_delete_or_patch_endpoint_exists()
    {
        foreach (var method in typeof(ClaimsController).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.Empty(method.GetCustomAttributes<HttpDeleteAttribute>());
            Assert.Empty(method.GetCustomAttributes<HttpPatchAttribute>());
        }
    }

    [Theory]
    [InlineData(nameof(ClaimsController.List), "GET", Base, ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.Get), "GET", Base + "/{claimId:guid}", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.Create), "POST", Base, ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.Update), "PUT", Base + "/{claimId:guid}", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.Approve), "POST", Base + "/{claimId:guid}/approve", ClaimPermissions.Approve)]
    [InlineData(nameof(ClaimsController.Archive), "POST", Base + "/{claimId:guid}/archive", ClaimPermissions.Manage)]
    public void Action_has_expected_verb_route_and_permission(string action, string verb, string route, string permission)
    {
        var method = typeof(ClaimsController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;

        var (template, methods) = verb switch
        {
            "GET" => (method.GetCustomAttribute<HttpGetAttribute>()?.Template,
                      method.GetCustomAttribute<HttpGetAttribute>()?.HttpMethods),
            "POST" => (method.GetCustomAttribute<HttpPostAttribute>()?.Template,
                       method.GetCustomAttribute<HttpPostAttribute>()?.HttpMethods),
            "PUT" => (method.GetCustomAttribute<HttpPutAttribute>()?.Template,
                      method.GetCustomAttribute<HttpPutAttribute>()?.HttpMethods),
            _ => (null, null)
        };

        Assert.NotNull(methods);                       // the expected verb attribute is present
        Assert.Equal(route, template);                 // canonical route
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()?.Permission); // HasPermission key
    }
}
