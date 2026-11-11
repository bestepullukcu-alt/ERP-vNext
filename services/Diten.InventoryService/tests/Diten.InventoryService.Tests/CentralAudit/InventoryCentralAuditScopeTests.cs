using System.Security.Claims;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Api.CentralAudit;
using Diten.InventoryService.Application.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.InventoryService.Tests.CentralAudit;

/// <summary>
/// BL-501 — Inventory's scope adapter. The tenant / company / user are the RESOLVED scope's; the name is the token's, in
/// either spelling: the JWT names (<c>given_name</c>, <c>family_name</c>) or the names the bearer handler maps them to
/// (<see cref="ClaimTypes.GivenName"/>, <see cref="ClaimTypes.Surname"/>) — the end-to-end probe found the record without a
/// name when only the first spelling was read.
/// </summary>
public sealed class InventoryCentralAuditScopeTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    [Theory]
    [InlineData("given_name", "family_name")]
    [InlineData(ClaimTypes.GivenName, ClaimTypes.Surname)]
    public void The_name_is_read_in_either_claim_spelling(string given, string family)
    {
        var scope = Scope(new Claim(given, "Ayşe"), new Claim(family, "Denetçi"));

        Assert.Equal("Ayşe Denetçi", scope.ActorDisplayName);
    }

    [Fact]
    public void Tenant_company_and_user_come_from_the_resolved_scope_and_the_actor_type_from_the_token()
    {
        var scope = Scope(new Claim("actor_type", "tenant_user"), new Claim("tenant_id", Guid.NewGuid().ToString()));

        Assert.True(scope.IsResolved);
        Assert.Equal(Tenant, scope.TenantId);
        Assert.Equal(Company, scope.CompanyId);
        Assert.Equal(User, scope.ActorId);
        Assert.Equal(CentralAuditActorTypes.TenantUser, scope.ActorType);
        Assert.Null(scope.ActorDisplayName);
    }

    [Fact]
    public void Without_a_correlation_middleware_the_correlation_is_empty_not_an_exception()
    {
        Assert.Equal(Guid.Empty, Scope().CorrelationId);
    }

    private static InventoryCentralAuditScope Scope(params Claim[] claims)
    {
        var tenant = new TenantContext();
        tenant.SetScope(Tenant, Company, User);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return new InventoryCentralAuditScope(tenant, new CorrelationContext(), http);
    }
}
