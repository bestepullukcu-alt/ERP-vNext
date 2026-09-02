using System.Security.Claims;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.Catalog;

public sealed class CatalogSecurityContractTests
{
    private const string Permission = "management-governance.process-modeling.architectures.create";

    [Fact]
    public void Missing_or_invalid_authenticated_identity_is_401()
    {
        AssertStatus(401, new ClaimsPrincipal(new ClaimsIdentity()), Guid.NewGuid().ToString(), Permission, "key", true);

        var tenant = Guid.NewGuid();
        var principal = Principal(tenant, Guid.Empty, Permission);
        AssertStatus(401, principal, tenant.ToString(), Permission, "key", true);
    }

    [Fact]
    public void Tenant_header_conflict_is_400_before_permission_evaluation()
    {
        var principal = Principal(Guid.NewGuid(), Guid.NewGuid());
        AssertStatus(400, principal, Guid.NewGuid().ToString(), Permission, "key", true);
        AssertStatus(400, principal, "not-a-guid", Permission, "key", true);
    }

    [Fact]
    public void Own_tenant_permission_denial_is_403_and_exact_permission_is_required()
    {
        var tenant = Guid.NewGuid();
        var principal = Principal(tenant, Guid.NewGuid(), Permission + ".broader");
        AssertStatus(403, principal, tenant.ToString(), Permission, "key", true);
    }

    [Fact]
    public void Mutation_requires_idempotency_key_but_query_does_not()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var principal = Principal(tenant, actor, Permission);

        AssertStatus(400, principal, tenant.ToString(), Permission, null, true);
        var query = ProcessModelingLocalTestSecurity.Resolve(principal, tenant.ToString(), Permission, null, false);
        Assert.Equal(tenant, query.TenantId);
        Assert.Equal(actor, query.ActorId);
        Assert.Empty(query.IdempotencyKey);
    }

    [Fact]
    public void Trusted_context_is_derived_only_from_authenticated_claims()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var principal = Principal(tenant, actor, Permission);

        var resolved = ProcessModelingLocalTestSecurity.Resolve(principal, tenant.ToString(), Permission, " replay-key ", true);

        Assert.Equal(tenant, resolved.TenantId);
        Assert.Equal(actor, resolved.ActorId);
        Assert.Equal("replay-key", resolved.IdempotencyKey);
    }

    private static ClaimsPrincipal Principal(Guid tenant, Guid actor, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenant.ToString()),
            new(ClaimTypes.NameIdentifier, actor.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "catalog-local-test"));
    }

    private static void AssertStatus(
        int expected,
        ClaimsPrincipal principal,
        string? tenantHeader,
        string permission,
        string? idempotencyKey,
        bool mutation)
    {
        var exception = Assert.Throws<ProcessModelingLocalTestSecurityException>(() =>
            ProcessModelingLocalTestSecurity.Resolve(principal, tenantHeader, permission, idempotencyKey, mutation));
        Assert.Equal(expected, exception.StatusCode);
    }
}
