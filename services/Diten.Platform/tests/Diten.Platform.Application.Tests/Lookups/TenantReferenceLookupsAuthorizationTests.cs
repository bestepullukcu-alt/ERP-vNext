using System.Reflection;
using Diten.Platform.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Lookups;

// MOD-0220 fix — the universal ISO reference lookups (countries, currencies) must stay TENANT-readable (the LE
// wizard needs them) while the rest of the platform lookup surface stays platform-admin-only. This guard pins the
// İş3 boundary for the fix: authenticated-but-not-PlatformActor, and scoped to exactly countries/currencies (no
// catch-all that could leak arbitrary platform lookups to tenants).
public sealed class TenantReferenceLookupsAuthorizationTests
{
    private static readonly Type Controller = typeof(TenantReferenceLookupsController);

    [Fact]
    public void Requires_authentication_but_is_not_locked_to_the_platform_actor_policy()
    {
        var authAttributes = Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();

        Assert.NotEmpty(authAttributes); // still requires an authenticated caller
        // NOT [Authorize(Policy = "PlatformActor")] — that policy 403s tenant_user actors (the bug we fixed).
        Assert.DoesNotContain(authAttributes, a => string.Equals(a.Policy, "PlatformActor", StringComparison.Ordinal));
        // A plain [Authorize] (no policy) → any authenticated actor, platform_admin OR tenant_user.
        Assert.Contains(authAttributes, a => string.IsNullOrEmpty(a.Policy));
    }

    [Fact]
    public void Exposes_only_the_universal_country_and_currency_reference_keys()
    {
        var routes = Controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(new[] { "countries", "currencies" }.OrderBy(x => x), routes.OrderBy(x => x));
        // No parameterized/catch-all route → the tenant relaxation can't be abused to read arbitrary lookups.
        Assert.DoesNotContain(routes, r => r.Contains('{'));
    }
}
