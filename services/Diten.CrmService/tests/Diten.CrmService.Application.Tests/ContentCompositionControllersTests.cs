using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Features.ContentComposition.ContentScopes;
using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — ContentScopes / ContentSets controller surface (reflection over attributes, mirroring the
/// claim controller test). Asserts each class is [Authorize], carries the right verb + canonical route + HasPermission
/// key (content-scope / content-set read|manage), and exposes no delete / patch endpoint (closing is Archive).
/// </summary>
public sealed class ContentCompositionControllersTests
{
    private const string ScopeBase = "api/crm/content-composition/content-scopes";
    private const string SetBase = "api/crm/content-composition/content-sets";

    [Fact]
    public void Controllers_require_authorization()
    {
        Assert.NotNull(typeof(ContentScopesController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(typeof(ContentSetsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void No_delete_or_patch_endpoint_exists()
    {
        foreach (var controller in new[] { typeof(ContentScopesController), typeof(ContentSetsController) })
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.Empty(method.GetCustomAttributes<HttpDeleteAttribute>());
                Assert.Empty(method.GetCustomAttributes<HttpPatchAttribute>());
            }
        }
    }

    // WP-SB-1R — the ContentScope is retired: its create / update / archive endpoints are gone, only the reads remain.
    [Fact]
    public void Content_scope_controller_is_read_only()
    {
        var writes = typeof(ContentScopesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpPostAttribute>().Any() || m.GetCustomAttributes<HttpPutAttribute>().Any())
            .Select(m => m.Name)
            .ToList();
        Assert.Empty(writes);
    }

    [Theory]
    [InlineData(typeof(ContentScopesController), nameof(ContentScopesController.List), "GET", ScopeBase, ContentScopePermissions.Read)]
    [InlineData(typeof(ContentScopesController), nameof(ContentScopesController.Get), "GET", ScopeBase + "/{contentScopeId:guid}", ContentScopePermissions.Read)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.List), "GET", SetBase, ContentSetPermissions.Read)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.Get), "GET", SetBase + "/{contentSetId:guid}", ContentSetPermissions.Read)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.Create), "POST", SetBase, ContentSetPermissions.Manage)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.Clone), "POST", SetBase + "/{contentSetId:guid}/clone", ContentSetPermissions.Manage)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.Update), "PUT", SetBase + "/{contentSetId:guid}", ContentSetPermissions.Manage)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.Archive), "POST", SetBase + "/{contentSetId:guid}/archive", ContentSetPermissions.Manage)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.AddComponent), "POST", SetBase + "/{contentSetId:guid}/components", ContentSetPermissions.Manage)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.AddClaim), "POST", SetBase + "/{contentSetId:guid}/claims", ContentSetPermissions.Manage)]
    [InlineData(typeof(ContentSetsController), nameof(ContentSetsController.ApplyEligibility), "POST", SetBase + "/{contentSetId:guid}/apply-eligibility", ContentSetPermissions.Manage)]
    public void Action_has_expected_verb_route_and_permission(Type controller, string action, string verb, string route, string permission)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;

        var (template, methods) = verb switch
        {
            "GET" => (method.GetCustomAttribute<HttpGetAttribute>()?.Template, method.GetCustomAttribute<HttpGetAttribute>()?.HttpMethods),
            "POST" => (method.GetCustomAttribute<HttpPostAttribute>()?.Template, method.GetCustomAttribute<HttpPostAttribute>()?.HttpMethods),
            "PUT" => (method.GetCustomAttribute<HttpPutAttribute>()?.Template, method.GetCustomAttribute<HttpPutAttribute>()?.HttpMethods),
            _ => (null, null)
        };

        Assert.NotNull(methods);
        Assert.Equal(route, template);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
    }
}
