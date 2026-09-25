using System.Reflection;
using System.Security.Claims;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Perms = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions;

namespace Diten.CrmService.Application.Tests.PlannedVisit;

/// <summary>
/// WP-MOB-B03 — PlannedVisitsController canonical RBAC. Every action's [Authorize]/[HasPermission] attributes are read
/// off the PRODUCTION controller and evaluated through the PRODUCTION <see cref="PermissionPolicyProvider"/> +
/// <see cref="PermissionAuthorizationHandler"/> via ASP.NET's <see cref="PolicyEvaluator"/> — the same pipeline the
/// authorization middleware runs — so 401 / 403 / allowed and the read/manage/confirm split are measured, not restated.
/// </summary>
public sealed class PlannedVisitRbacTests
{
    private const string Base = "api/crm/planned-visits";

    // Route + verb + canonical key per action. Routes are asserted too so the RBAC change provably left them intact.
    public static TheoryData<string, string, string, string> ActionMap => new()
    {
        { nameof(PlannedVisitsController.Contract), "GET", Base + "/contract", Perms.Read },
        { nameof(PlannedVisitsController.List), "GET", Base, Perms.Read },
        { nameof(PlannedVisitsController.Get), "GET", Base + "/{plannedVisitId:guid}", Perms.Read },
        { nameof(PlannedVisitsController.Create), "POST", Base, Perms.Manage },
        { nameof(PlannedVisitsController.Update), "PUT", Base + "/{plannedVisitId:guid}", Perms.Manage },
        { nameof(PlannedVisitsController.Cancel), "POST", Base + "/{plannedVisitId:guid}/cancel", Perms.Manage },
        { nameof(PlannedVisitsController.Archive), "POST", Base + "/{plannedVisitId:guid}/archive", Perms.Manage },
        { nameof(PlannedVisitsController.Confirm), "POST", Base + "/{plannedVisitId:guid}/confirm", Perms.Confirm },
    };

    [Theory]
    [MemberData(nameof(ActionMap))]
    public void Action_keeps_route_and_verb_and_enforces_canonical_key(string action, string verb, string route, string permission)
    {
        var method = Action(action);
        var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();

        Assert.Equal(route, http.Template);
        Assert.Equal([verb], http.HttpMethods);
        Assert.Equal([permission], method.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission));
    }

    [Fact]
    public void Every_public_action_is_covered_and_no_territory_fallback_remains()
    {
        var actions = typeof(PlannedVisitsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(ActionMap.Count(), actions.Length);
        Assert.All(actions, m => Assert.All(
            m.GetCustomAttributes<HasPermissionAttribute>(),
            a => Assert.StartsWith("crm.planned-visit.", a.Permission, StringComparison.Ordinal)));
        Assert.NotNull(typeof(PlannedVisitsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [MemberData(nameof(ActionMap))]
    public async Task Anonymous_is_challenged_401(string action, string verb, string route, string permission)
    {
        _ = (verb, route, permission);
        var result = await Evaluate(action, Anonymous());
        Assert.True(result.Challenged);
    }

    [Theory]
    [MemberData(nameof(ActionMap))]
    public async Task Authenticated_without_the_key_is_forbidden_403(string action, string verb, string route, string permission)
    {
        _ = (verb, route, permission);
        // Holding the OLD territory fallback no longer opens the endpoint.
        var result = await Evaluate(action, User("crm.territory.read", "crm.territory.model.manage"));
        Assert.True(result.Forbidden);
    }

    [Theory]
    [MemberData(nameof(ActionMap))]
    public async Task Authenticated_with_the_key_is_allowed(string action, string verb, string route, string permission)
    {
        _ = (verb, route);
        var result = await Evaluate(action, User(permission));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Read_only_caller_cannot_create_update_cancel_archive_or_confirm()
    {
        var reader = User(Perms.Read);
        Assert.True((await Evaluate(nameof(PlannedVisitsController.List), reader)).Succeeded);
        foreach (var write in new[] { "Create", "Update", "Cancel", "Archive", "Confirm" })
        {
            Assert.True((await Evaluate(write, reader)).Forbidden, write);
        }
    }

    [Fact]
    public async Task Manage_only_caller_cannot_confirm_SoD()
    {
        var author = User(Perms.Manage);
        Assert.True((await Evaluate(nameof(PlannedVisitsController.Create), author)).Succeeded);
        Assert.True((await Evaluate(nameof(PlannedVisitsController.Confirm), author)).Forbidden);
    }

    [Fact]
    public async Task Confirm_only_caller_cannot_manage_or_read()
    {
        var confirmer = User(Perms.Confirm);
        Assert.True((await Evaluate(nameof(PlannedVisitsController.Confirm), confirmer)).Succeeded);
        Assert.True((await Evaluate(nameof(PlannedVisitsController.Create), confirmer)).Forbidden);
        Assert.True((await Evaluate(nameof(PlannedVisitsController.List), confirmer)).Forbidden);
    }

    [Fact]
    public async Task Tenant_mismatch_on_planned_visits_is_refused_before_the_controller()
    {
        var jwtTenant = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", jwtTenant.ToString()), new Claim("permission", Perms.Read)], "Bearer"))
        };
        context.Request.Path = "/" + Base;
        context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<TenantResolutionMiddleware>.Instance);
        await middleware.InvokeAsync(context, new TenantContext());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    // ---------- helpers ----------

    private static MethodInfo Action(string name)
        => typeof(PlannedVisitsController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(params string[] permissions)
        => new(new ClaimsIdentity(
            permissions.Select(p => new Claim("permission", p)).Append(new Claim("sub", "rep-1")), "Bearer"));

    private static async Task<PolicyAuthorizationResult> Evaluate(string action, ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();
        await using var provider = services.BuildServiceProvider();

        // Class-level [Authorize] + action-level [HasPermission], combined exactly as the endpoint would be.
        var authorizeData = typeof(PlannedVisitsController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(Action(action).GetCustomAttributes<AuthorizeAttribute>())
            .Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorizeData);

        var httpContext = new DefaultHttpContext { User = user, RequestServices = provider };
        var authenticate = user.Identity?.IsAuthenticated == true
            ? AuthenticateResult.Success(new AuthenticationTicket(user, "Bearer"))
            : AuthenticateResult.NoResult();

        return await provider.GetRequiredService<IPolicyEvaluator>()
            .AuthorizeAsync(policy!, authenticate, httpContext, resource: null);
    }
}
