using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Security;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class PlatformTenantWorkflowDefinitionRequestExecutorTests
{
    [Fact]
    public async Task Anonymous_request_fails_before_target_lookup()
    {
        var tenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext());

        var result = await executor.ExecuteAsync(
            new DefaultHttpContext(), Guid.NewGuid(), CancellationToken.None,
            _ => Task.FromResult<IActionResult>(new OkResult()), Failure);

        Assert.Equal(401, Assert.IsType<ObjectResult>(result).StatusCode);
        tenants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Active_target_executes_inside_exact_scope_and_restores_platform_context()
    {
        var target = Guid.NewGuid();
        var prior = Guid.NewGuid();
        var context = new TenantContext();
        context.SetPlatformContext(prior);
        var tenants = Repository(new Tenant
        {
            Code = "PILOT", Slug = "pilot", Name = "Pilot", DisplayName = "Pilot", Domain = "pilot.local",
            Status = TenantStatus.Active
        }, target);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, context);
        var observed = Guid.Empty;

        var result = await executor.ExecuteAsync(
            Http(PlatformAdminClaims()), target, CancellationToken.None,
            _ => { observed = context.TenantId; return Task.FromResult<IActionResult>(new OkResult()); },
            Failure);

        Assert.IsType<OkResult>(result);
        Assert.Equal(target, observed);
        Assert.True(context.IsPlatformContext);
        Assert.Equal(prior, context.TargetTenantId);
    }

    [Theory]
    [InlineData("tenant_user")]
    [InlineData("partner_admin")]
    [InlineData("service")]
    public async Task Non_platform_admin_fails_before_target_lookup(string actorType)
    {
        var tenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext());
        var result = await executor.ExecuteAsync(
            Http([new("actor_type", actorType), new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D"))]),
            Guid.NewGuid(), CancellationToken.None, _ => Task.FromResult<IActionResult>(new OkResult()), Failure);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        tenants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Duplicate_actor_claim_invalid_subject_and_tenant_header_fail_before_lookup()
    {
        foreach (var claims in new[]
        {
            PlatformAdminClaims().Append(new Claim("actor_type", "platform_admin")).ToArray(),
            new[] { new Claim("actor_type", "platform_admin"), new Claim(JwtRegisteredClaimNames.Sub, "bad") }
        })
        {
            var tenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
            var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext());
            var result = await executor.ExecuteAsync(Http(claims), Guid.NewGuid(), CancellationToken.None,
                _ => Task.FromResult<IActionResult>(new OkResult()), Failure);
            Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
            tenants.VerifyNoOtherCalls();
        }

        var headerTenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        var http = Http(PlatformAdminClaims());
        http.Request.Headers.Append("X-Tenant-Id", Guid.NewGuid().ToString("D"));
        var headerResult = await new PlatformTenantWorkflowDefinitionRequestExecutor(headerTenants.Object, new TenantContext())
            .ExecuteAsync(http, Guid.NewGuid(), CancellationToken.None,
                _ => Task.FromResult<IActionResult>(new OkResult()), Failure);
        Assert.Equal(400, Assert.IsType<ObjectResult>(headerResult).StatusCode);
        headerTenants.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Actor_Type", "platform_admin")]
    [InlineData("actor_type", "PLATFORM_ADMIN")]
    [InlineData("ACTOR_TYPE", "PLATFORM_ADMIN")]
    public async Task Non_canonical_actor_claim_name_or_value_fails_before_lookup(string claimType, string actorType)
    {
        var tenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext());

        var result = await executor.ExecuteAsync(
            Http([new(claimType, actorType), new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D"))]),
            Guid.NewGuid(), CancellationToken.None,
            _ => Task.FromResult<IActionResult>(new OkResult()), Failure);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        tenants.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Duplicate_subject_claim_fails_before_lookup()
    {
        var tenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext());
        var claims = PlatformAdminClaims()
            .Append(new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")))
            .ToArray();

        var result = await executor.ExecuteAsync(
            Http(claims), Guid.NewGuid(), CancellationToken.None,
            _ => Task.FromResult<IActionResult>(new OkResult()), Failure);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        tenants.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TenantStatus.Provisioning)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Deactivated)]
    public async Task Inactive_target_returns_conflict_without_dispatch(TenantStatus status)
    {
        var target = Guid.NewGuid();
        var tenants = Repository(new Tenant
        {
            Code = "TARGET", Slug = "target", Name = "Target", DisplayName = "Target", Domain = "target.local",
            Status = status
        }, target);
        var dispatched = false;
        var result = await new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext())
            .ExecuteAsync(Http(PlatformAdminClaims()), target, CancellationToken.None,
                _ => { dispatched = true; return Task.FromResult<IActionResult>(new OkResult()); }, Failure);
        Assert.Equal(409, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.False(dispatched);
    }

    [Fact]
    public async Task Missing_and_system_targets_fail_closed_without_scope()
    {
        var missing = Guid.NewGuid();
        var tenants = new Mock<ITenantRegistryRepository>();
        tenants.Setup(x => x.GetByIdAsync(missing, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);
        var context = new TenantContext();
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, context);

        var missingResult = await executor.ExecuteAsync(Http(PlatformAdminClaims()), missing, CancellationToken.None,
            _ => Task.FromResult<IActionResult>(new OkResult()), Failure);
        var systemResult = await executor.ExecuteAsync(Http(PlatformAdminClaims()), SystemTenantRules.PlatformSystemTenantId,
            CancellationToken.None, _ => Task.FromResult<IActionResult>(new OkResult()), Failure);

        Assert.Equal(404, Assert.IsType<ObjectResult>(missingResult).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>(systemResult).StatusCode);
        Assert.False(context.IsResolved);
    }

    [Fact]
    public async Task Deleted_target_returns_not_found_without_dispatch()
    {
        var target = Guid.NewGuid();
        var tenant = new Tenant
        {
            Code = "DELETED", Slug = "deleted", Name = "Deleted", DisplayName = "Deleted",
            Domain = "deleted.local", Status = TenantStatus.Active, IsDeleted = true
        };
        var tenants = Repository(tenant, target);
        var dispatched = false;

        var result = await new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, new TenantContext())
            .ExecuteAsync(Http(PlatformAdminClaims()), target, CancellationToken.None,
                _ => { dispatched = true; return Task.FromResult<IActionResult>(new OkResult()); }, Failure);

        Assert.Equal(404, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.False(dispatched);
    }

    [Fact]
    public async Task Controlled_failure_restores_unresolved_context()
    {
        var target = Guid.NewGuid();
        var context = new TenantContext();
        var tenants = Repository(new Tenant
        {
            Code = "TARGET", Slug = "target", Name = "Target", DisplayName = "Target", Domain = "target.local",
            Status = TenantStatus.Active
        }, target);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, context);

        var result = await executor.ExecuteAsync(
            Http(PlatformAdminClaims()), target, CancellationToken.None,
            _ => Task.FromResult<IActionResult>(new ConflictResult()), Failure);

        Assert.IsType<ConflictResult>(result);
        Assert.False(context.IsResolved);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_restores_prior_context()
    {
        var target = Guid.NewGuid();
        var prior = Guid.NewGuid();
        var context = new TenantContext();
        context.SetTenant(prior);
        var tenants = Repository(new Tenant
        {
            Code = "TARGET", Slug = "target", Name = "Target", DisplayName = "Target", Domain = "target.local",
            Status = TenantStatus.Active
        }, target);
        var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            Http(PlatformAdminClaims()), target, cancellation.Token,
            token => Task.FromCanceled<IActionResult>(token), Failure));

        Assert.Equal(prior, context.TenantId);
        Assert.False(context.IsPlatformContext);
    }

    [Fact]
    public async Task Exception_and_cancellation_restore_prior_tenant_context()
    {
        var target = Guid.NewGuid();
        var prior = Guid.NewGuid();
        var tenants = Repository(new Tenant
        {
            Code = "TARGET", Slug = "target", Name = "Target", DisplayName = "Target", Domain = "target.local",
            Status = TenantStatus.Active
        }, target);
        foreach (var exception in new Exception[] { new InvalidOperationException("boom"), new OperationCanceledException() })
        {
            var context = new TenantContext();
            context.SetTenant(prior);
            var executor = new PlatformTenantWorkflowDefinitionRequestExecutor(tenants.Object, context);
            await Assert.ThrowsAsync(exception.GetType(), () => executor.ExecuteAsync(
                Http(PlatformAdminClaims()), target, CancellationToken.None,
                _ => Task.FromException<IActionResult>(exception), Failure));
            Assert.Equal(prior, context.TenantId);
            Assert.False(context.IsPlatformContext);
        }
    }

    private static Mock<ITenantRegistryRepository> Repository(Tenant tenant, Guid id)
    {
        typeof(Tenant).GetProperty(nameof(Tenant.Id))!.SetValue(tenant, id);
        var mock = new Mock<ITenantRegistryRepository>();
        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        return mock;
    }

    private static DefaultHttpContext Http(IEnumerable<Claim> claims) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
    };

    private static Claim[] PlatformAdminClaims() =>
        [new("actor_type", "platform_admin"), new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D"))];

    private static IActionResult Failure(int status, string reason) => new ObjectResult(reason) { StatusCode = status };
}
