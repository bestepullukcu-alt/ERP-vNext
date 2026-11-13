using System.Security.Claims;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Api.CentralAudit;
using Diten.CrmService.Application.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/// <summary>
/// WP-CRM-SYNC-S0B B0b (CT decision D-2) + B0c (D-3) — measured on the production <see cref="CrmCentralAuditScope"/> and
/// <see cref="CrmCentralAudit"/>: a command that runs with NO HTTP context (the pre-order transmission retry scheduler) is
/// filed as the system, never as a tenant user with an empty actor; with a context nothing changed. The three added
/// helpers name operations Platform knows.
/// <para>Unit tests only (no host, no mongod). The same rule on the REAL CRM host:
/// <see cref="CrmCentralAuditPipelineTests.A_command_run_by_a_background_job_is_recorded_as_the_system_in_the_pinned_tenant"/>.</para>
/// </summary>
public sealed class CrmCentralAuditScopeTests
{
    private static CrmCentralAuditScope Scope(Guid? tenant, HttpContext? context)
    {
        var tenantContext = new TenantContext();
        if (tenant is { } id)
        {
            tenantContext.SetTenant(id);
        }

        return new CrmCentralAuditScope(tenantContext, new HttpContextAccessor { HttpContext = context });
    }

    private static DefaultHttpContext Request(params Claim[] claims)
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };

    [Fact]
    public void B0b_without_an_http_context_the_scope_is_the_system_with_no_actor_in_the_pinned_tenant()
    {
        var tenant = Guid.NewGuid();

        var scope = Scope(tenant, context: null);

        Assert.Equal(CentralAuditActorTypes.System, scope.ActorType);
        Assert.Null(scope.ActorId);
        Assert.Null(scope.ActorDisplayName);
        Assert.True(scope.IsResolved);
        Assert.Equal(tenant, scope.TenantId);
        Assert.Equal(Guid.Empty, scope.CorrelationId);
    }

    [Fact]
    public void B0b_without_an_http_context_and_without_a_tenant_nothing_is_resolved()
    {
        var scope = Scope(tenant: null, context: null);

        Assert.False(scope.IsResolved);
        Assert.Equal(Guid.Empty, scope.TenantId);
    }

    [Fact]
    public void B0b_with_an_http_context_and_no_actor_type_the_scope_is_still_the_tenant_user_with_its_sub_and_name()
    {
        var (tenant, user) = (Guid.NewGuid(), Guid.NewGuid());

        var scope = Scope(tenant, Request(new Claim("sub", user.ToString()), new Claim("tenant_id", tenant.ToString()), new Claim("name", "Test User")));

        Assert.Equal(CentralAuditActorTypes.TenantUser, scope.ActorType);
        Assert.Equal(user, scope.ActorId);
        Assert.Equal("Test User", scope.ActorDisplayName);
        Assert.True(scope.IsResolved);
        Assert.Equal(tenant, scope.TenantId);
    }

    [Theory]
    [InlineData("platform_admin", CentralAuditActorTypes.PlatformAdministrator)]
    [InlineData("partner_admin", CentralAuditActorTypes.PartnerAdministrator)]
    [InlineData("anything-else", CentralAuditActorTypes.TenantUser)]
    public void B0b_with_an_http_context_the_actor_type_claim_is_read_as_before(string claim, string expected)
    {
        var scope = Scope(Guid.NewGuid(), Request(new Claim("actor_type", claim)));

        Assert.Equal(expected, scope.ActorType);
    }

    /// <summary>An anonymous request is still a request: it is NOT the system (only "no HTTP context at all" is).</summary>
    [Fact]
    public void B0b_an_http_context_without_any_claim_is_not_the_system()
    {
        var scope = Scope(Guid.NewGuid(), new DefaultHttpContext());

        Assert.Equal(CentralAuditActorTypes.TenantUser, scope.ActorType);
        Assert.Null(scope.ActorId);
    }

    [Theory]
    [InlineData("Deactivate", 5)]
    [InlineData("Revoke", 9)]
    [InlineData("Export", 10)]
    public void B0c_the_added_helpers_name_a_platform_operation_under_crm(string operation, int platformValue)
    {
        var metadata = operation switch
        {
            "Deactivate" => CrmCentralAudit.Deactivate("Thing"),
            "Revoke" => CrmCentralAudit.Revoke("Thing"),
            _ => CrmCentralAudit.Export("Thing")
        };

        Assert.Equal(operation, metadata.Operation);
        Assert.Equal(platformValue, CentralAuditPlatformValues.Operations[metadata.Operation]);
        Assert.Equal((CrmCentralAudit.Category, "Thing", CrmCentralAudit.SourceModule), (metadata.Category, metadata.EntityType, metadata.SourceModule));
        Assert.True(CentralAuditPlatformValues.IsKnown(metadata));
    }
}
