using System.Reflection;
using System.Security.Claims;
using Diten.Platform.API.Controllers.ESignature;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ESignature;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// WP-MVP1-SEC-ESIGN1 item 4 — the filter-level facts:
/// <list type="bullet">
/// <item><see cref="HasPermissionAttribute"/> is <c>AllowMultiple = true</c> and two of them on one action are two filters that must
/// BOTH pass — AND, not OR (measured by running the action's own attribute list the way MVC runs authorization filters: the first
/// one that sets a result short-circuits);</item>
/// <item><see cref="HasAnyPermissionAttribute"/> lets ANY one declared key through, with HasPermission's own rules: no key → 403,
/// anonymous → 401, a platform_admin / partner_admin actor passes (the same bypass);</item>
/// <item>A1 (<see cref="HasPermissionReflector"/>) collects every key of a [HasAnyPermission].</item>
/// </list>
/// </summary>
public sealed class HasAnyPermissionAttributeTests
{
    private const string A = "esign.meanings.read";
    private const string B = "esign.signatures.sign";

    private sealed class Probe : ControllerBase
    {
        [HttpGet("both")]
        [HasPermission(A)]
        [HasPermission(B)]
        public IActionResult Both() => Ok();

        [HttpGet("one")]
        [HasPermission(A)]
        public IActionResult One() => Ok();

        [HttpGet("either")]
        [HasAnyPermission(A, B)]
        public IActionResult Either() => Ok();
    }

    private static AuthorizationFilterContext Context(bool authenticated, string? actorType, params string[] keys)
    {
        var claims = keys.Select(k => new Claim("permission", k)).ToList();
        if (actorType is not null) claims.Add(new Claim("actor_type", actorType));
        var identity = authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity(claims);
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity), RequestServices = new ServiceCollection().BuildServiceProvider() };
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());
    }

    /// <summary>Runs the action's authorization attributes in declaration order, stopping at the first result (the MVC pipeline).</summary>
    private static async Task<IActionResult?> RunAsync(string action, AuthorizationFilterContext context)
    {
        foreach (var filter in typeof(Probe).GetMethod(action)!.GetCustomAttributes(true).OfType<IAsyncAuthorizationFilter>())
        {
            await filter.OnAuthorizationAsync(context);
            if (context.Result is not null) return context.Result;
        }

        return null;
    }

    private static int? Status(IActionResult? result) => result switch
    {
        null => null,
        UnauthorizedResult => 401,
        ObjectResult o => o.StatusCode,
        _ => -1
    };

    [Fact]
    public void HasPermission_allows_multiple_and_the_two_are_separate_filters()
    {
        Assert.True(typeof(HasPermissionAttribute).GetCustomAttribute<AttributeUsageAttribute>()!.AllowMultiple);
        Assert.Equal(2, typeof(Probe).GetMethod(nameof(Probe.Both))!.GetCustomAttributes<HasPermissionAttribute>().Count());
    }

    [Theory]
    [InlineData(A)]
    [InlineData(B)]
    public async Task Two_HasPermission_attributes_are_AND_one_key_is_not_enough(string key)
    {
        Assert.Equal(403, Status(await RunAsync(nameof(Probe.Both), Context(true, "tenant_user", key))));
        Assert.Null(await RunAsync(nameof(Probe.Both), Context(true, "tenant_user", A, B)));
    }

    [Theory]
    [InlineData(A)]
    [InlineData(B)]
    public async Task HasAnyPermission_lets_any_one_declared_key_through(string key)
        => Assert.Null(await RunAsync(nameof(Probe.Either), Context(true, "tenant_user", key)));

    [Fact]
    public async Task HasAnyPermission_without_any_declared_key_is_403()
        => Assert.Equal(403, Status(await RunAsync(nameof(Probe.Either), Context(true, "tenant_user", "esign.policy.read"))));

    [Fact]
    public async Task HasAnyPermission_anonymous_is_401()
        => Assert.Equal(401, Status(await RunAsync(nameof(Probe.Either), Context(false, null, A, B))));

    // MVP-1 SYNC3: main's BL-521 FIX1 narrowed the actor rule (a partner passes only an endpoint that admits partners), so
    // the pin is "the same decision as HasPermission" for each actor, not a fixed answer; platform_admin still passes.
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task HasAnyPermission_keeps_the_platform_actor_rule_of_HasPermission(string actor)
    {
        var single = Status(await RunAsync(nameof(Probe.One), Context(true, actor)));
        Assert.Equal(single, Status(await RunAsync(nameof(Probe.Either), Context(true, actor))));
        if (actor == "platform_admin") Assert.Null(single);
    }

    [Fact]
    public void A_HasAnyPermission_needs_at_least_one_key()
    {
        Assert.Throws<ArgumentException>(() => new HasAnyPermissionAttribute());
        Assert.Throws<ArgumentException>(() => new HasAnyPermissionAttribute(A, " "));
    }

    [Fact]
    public void A1_registers_every_key_of_the_two_esign_HasAnyPermission_actions()
    {
        var keys = HasPermissionReflector.CollectPermissionKeys(typeof(ESignatureSamplesController).Assembly);
        Assert.Contains(ESignPermissions.MySamplesManage, keys);
        Assert.Contains(ESignPermissions.SamplesApprove, keys);
        Assert.Contains(ESignPermissions.SignaturesRead, keys);
        Assert.Contains(ESignPermissions.MeaningsRead, keys);
        Assert.Contains(ESignPermissions.SignaturesSign, keys);

        var image = typeof(ESignatureSamplesController).GetMethod(nameof(ESignatureSamplesController.Image))!.GetCustomAttribute<HasAnyPermissionAttribute>()!;
        Assert.Equal([ESignPermissions.MySamplesManage, ESignPermissions.SamplesApprove, ESignPermissions.SignaturesRead], image.Permissions);
        var meanings = typeof(ESignatureSettingsController).GetMethod(nameof(ESignatureSettingsController.ListMeanings))!.GetCustomAttribute<HasAnyPermissionAttribute>()!;
        Assert.Equal([ESignPermissions.MeaningsRead, ESignPermissions.SignaturesSign], meanings.Permissions);
    }
}
