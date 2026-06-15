using System.Security.Claims;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

// Slice 1A (D-5) — enforcement semantics for the canonical Legal Entity permission key.
// The MDM handler does a single-value exact match (no alias seam in Slice 1A).
public sealed class PermissionAuthorizationHandlerTests
{
    private const string Canonical = "mdm.legal-entities.read";

    [Fact]
    public async Task Canonical_grant_allows_access()
    {
        var user = AuthenticatedUser(new Claim("permission", Canonical));

        Assert.True(await EvaluateAsync(user, Canonical));
    }

    [Fact]
    public async Task No_grant_denies_access()
    {
        // Authenticated, but carries no matching permission claim → fail-closed deny.
        var user = AuthenticatedUser(new Claim("permission", "mdm.legal-entities.delete"));

        Assert.False(await EvaluateAsync(user, Canonical));
    }

    [Fact]
    public async Task Platform_admin_bypass_allows_access_without_grant()
    {
        var user = AuthenticatedUser(new Claim("actor_type", "platform_admin"));

        Assert.True(await EvaluateAsync(user, Canonical));
    }

    [Fact]
    public async Task Legacy_key_grant_does_not_satisfy_canonical_requirement()
    {
        // D-5 regression: in Slice 1A there is no alias resolution, so a grant of the old
        // PascalCase key must NOT satisfy the canonical requirement (no faked dual-read).
        var user = AuthenticatedUser(new Claim("permission", "Modules.LegalEntity.Read"));

        Assert.False(await EvaluateAsync(user, Canonical));
    }

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal user, string requiredPermission)
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(requiredPermission);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));
}
