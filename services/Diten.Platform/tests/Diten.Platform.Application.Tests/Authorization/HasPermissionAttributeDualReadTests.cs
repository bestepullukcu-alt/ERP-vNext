using System.Security.Claims;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class HasPermissionAttributeDualReadTests
{
    // ── Dual-read: canonical requirement satisfied by canonical OR legacy alias ──

    [Fact]
    public async Task Canonical_claim_satisfies_canonical_requirement()
    {
        Assert.True(await IsAllowedAsync("platform.administrators.read", "tenant_user", "platform.administrators.read"));
    }

    [Fact]
    public async Task Legacy_pascal_alias_claim_satisfies_canonical_requirement()
    {
        Assert.True(await IsAllowedAsync("platform.administrators.read", "tenant_user", "Platform.Administrators.Read"));
    }

    [Fact]
    public async Task Legacy_modules_alias_claim_satisfies_canonical_requirement()
    {
        Assert.True(await IsAllowedAsync("platform.organization-units.read", "tenant_user", "Modules.OrganizationUnit.Read"));
    }

    [Fact]
    public async Task Legacy_verb_alias_claim_satisfies_read_requirement()
    {
        Assert.True(await IsAllowedAsync("platform.tenants.quotas.read", "tenant_user", "platform.tenants.quotas.view"));
    }

    // ── Fail-closed ──

    [Fact]
    public async Task No_permission_claim_is_denied()
    {
        Assert.False(await IsAllowedAsync("platform.administrators.read", "tenant_user"));
    }

    [Fact]
    public async Task Unknown_claim_is_denied()
    {
        Assert.False(await IsAllowedAsync("platform.administrators.read", "tenant_user", "some.other.permission"));
    }

    [Fact]
    public async Task Unauthenticated_request_is_denied()
    {
        Assert.False(await IsAllowedAsync("platform.administrators.read", actorType: null, authenticated: false));
    }

    // ── No auto-upgrade: a legacy requirement only accepts itself, never the canonical or other aliases ──

    [Fact]
    public async Task Legacy_requirement_is_not_upgraded_unrelated_claim_denied()
    {
        // Requirement is a legacy alias; the resolver does not expand it, so an unrelated canonical claim cannot satisfy it.
        Assert.False(await IsAllowedAsync("Platform.Administrators.Read", "tenant_user", "platform.subscription-plans.read"));
    }

    [Fact]
    public async Task Legacy_requirement_is_satisfied_by_its_own_form()
    {
        // Sanity: a not-yet-migrated controller declaring the legacy key still enforces against a matching claim.
        Assert.True(await IsAllowedAsync("Platform.Administrators.Read", "tenant_user", "Platform.Administrators.Read"));
    }

    // ── Bypass preserved (actor reaches the endpoint without a permission claim) ──

    [Fact]
    public async Task Platform_admin_actor_bypasses_permission_check()
    {
        Assert.True(await IsAllowedAsync("platform.administrators.read", "platform_admin"));
    }

    [Fact]
    public async Task Partner_admin_actor_bypasses_permission_check()
    {
        Assert.True(await IsAllowedAsync("platform.administrators.read", "partner_admin"));
    }

    private static async Task<bool> IsAllowedAsync(
        string requirement,
        string? actorType,
        params string[] permissionClaims)
        => await IsAllowedAsync(requirement, actorType, authenticated: true, permissionClaims);

    private static async Task<bool> IsAllowedAsync(
        string requirement,
        string? actorType,
        bool authenticated,
        params string[] permissionClaims)
    {
        var attribute = new HasPermissionAttribute(requirement);

        var claims = new List<Claim>();
        if (actorType is not null)
        {
            claims.Add(new Claim("actor_type", actorType));
        }

        claims.AddRange(permissionClaims.Select(p => new Claim("permission", p)));

        // A non-null authenticationType makes the identity authenticated; null leaves it anonymous.
        var identity = authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity(claims);
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = user,
            // Empty provider → GetService<IPlatformAdministratorRepository>() returns null, so the admin-lifecycle
            // side-effect branch is skipped (it is not exercised or altered by this slice).
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var filterContext = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        await attribute.OnAuthorizationAsync(filterContext);

        // No result set means the request is allowed to proceed; a ForbidResult/UnauthorizedResult means denied.
        return filterContext.Result is null;
    }
}
