using System.Security.Claims;
using Diten.ManagementGovernanceService.Api.Controllers;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingUserAcceptance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Envelope = Diten.ManagementGovernanceService.Application.Features.Dws.Response<object>;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.UserAcceptance;

public sealed class UserAcceptanceControllerTests
{
    [Fact]
    public async Task Detail_is_tenant_first_and_foreign_is_404_before_permission_denial()
    {
        var tenant = Guid.NewGuid();
        var store = new StubQueries(null);
        var controller = Controller(store, tenant, []);

        var result = await controller.Get(Guid.NewGuid(), default);

        var response = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(tenant, store.SeenTenant);
        Assert.Contains("process_model_not_found", Assert.IsType<Envelope>(response.Value).Errors);
    }

    [Fact]
    public async Task Detail_returns_403_only_for_visible_own_tenant_record_without_exact_permission()
    {
        var tenant = Guid.NewGuid();
        var model = new ProcessModelSummary(Guid.NewGuid(), Guid.NewGuid(), "P2P", "Procure", null, 1, null, Guid.NewGuid(), 1);
        var controller = Controller(new StubQueries(model), tenant, ["MANAGEMENT-GOVERNANCE.PROCESS-MODELING.MODELS.READ"]);

        var result = await controller.Get(model.Id, default);

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, ((ObjectResult)result).StatusCode);
    }

    [Fact]
    public void Improvement_mismatch_is_400_before_authority_unavailable()
    {
        var tenant = Guid.NewGuid();
        var controller = new PerformanceImprovementController { ControllerContext = Context(tenant, [], Guid.NewGuid()) };
        var result = Assert.IsType<BadRequestObjectResult>(controller.RootUnavailable());
        Assert.Contains("process_model_bad_request", Assert.IsType<Envelope>(result.Value).Errors);
    }

    private static ProcessModelingController Controller(StubQueries store, Guid tenant, IReadOnlyList<string> permissions) =>
        new(store) { ControllerContext = Context(tenant, permissions, tenant) };

    private static ControllerContext Context(Guid tenant, IReadOnlyList<string> permissions, Guid headerTenant)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString("D")), new("tenant_id", tenant.ToString("D")) };
        claims.AddRange(permissions.Select(x => new Claim("permission", x)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        http.Request.Headers["X-Tenant-Id"] = headerTenant.ToString("D");
        return new ControllerContext { HttpContext = http };
    }

    private sealed class StubQueries(ProcessModelSummary? result) : IProcessModelUserAcceptanceQueries
    {
        public Guid SeenTenant { get; private set; }
        public Task<IReadOnlyList<ProcessModelSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProcessModelSummary>>([]);
        public Task<ProcessModelSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        {
            SeenTenant = tenantId;
            return Task.FromResult(result);
        }
    }
}
