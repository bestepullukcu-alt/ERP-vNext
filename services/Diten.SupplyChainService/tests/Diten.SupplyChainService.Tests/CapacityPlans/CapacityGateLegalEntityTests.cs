using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Api.Features.CapacityPlans;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.SupplyChainService.Tests.CapacityPlans;

/// <summary>
/// G8 M3 (2026-10-18). The Capacity gate's MDM outcomes were untested — the HTTP host always stubs Valid, and its
/// "foreign legal entity" 404 is a repository fact. These drive the production <see cref="CapacityContextMiddleware"/>
/// directly with each validator outcome: anything but Valid refuses before the handler (fail-closed), NotReferenceable as
/// the module's own not-found (discloses nothing), Unavailable as 503; and MDM is asked with the header's legal entity.
/// </summary>
public sealed class CapacityGateLegalEntityTests
{
    [Theory]
    [InlineData(LegalEntityScopeOutcome.NotReferenceable, 404, "UNKNOWN_CAPACITY_PLAN")]
    [InlineData(LegalEntityScopeOutcome.Unavailable, 503, "DEPENDENCY_UNAVAILABLE")]
    public async Task Every_legal_entity_MDM_does_not_confirm_is_refused_before_the_handler(LegalEntityScopeOutcome outcome, int status, string code)
    {
        var (http, tenant, legalEntity) = Request();
        var validator = new StubLegalEntityScopeValidator(outcome);
        var reached = false;

        await new CapacityContextMiddleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(http, new CapacityRequestContext(), new RequestContext(), validator);

        Assert.False(reached);
        Assert.Equal(status, http.Response.StatusCode);
        Assert.Equal(code, ErrorCode(http));
        var call = Assert.Single(validator.Calls);
        Assert.Equal((tenant, legalEntity), (call.TenantId, call.LegalEntityId));
    }

    [Fact]
    public async Task A_confirmed_legal_entity_reaches_the_handler_in_the_header_scope()
    {
        var (http, tenant, legalEntity) = Request();
        var context = new CapacityRequestContext();
        var reached = false;

        await new CapacityContextMiddleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(http, context, new RequestContext(), StubLegalEntityScopeValidator.Valid);

        Assert.True(reached);
        Assert.Equal((tenant, legalEntity), (context.Scope.TenantId, context.Scope.LegalEntityId));
    }

    private static (DefaultHttpContext Http, Guid Tenant, Guid LegalEntity) Request()
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        http.Response.Body = new MemoryStream();
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new CapacityPermissionAttribute(CapacityPermissions.Read)), "capacity-gate-test"));
        // The token AuthService issues since R-2: tenant, actor, permissions — no legal_entity_id.
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", tenant.ToString()), new Claim("sub", actor.ToString()),
            new Claim("permission", CapacityPermissions.Read)
        }, "test"));
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { tenant_id = tenant, sub = actor }))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        http.Request.Headers.Authorization = $"Bearer e30.{payload}.sig";
        http.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
        http.Request.Headers["X-Tenant-Id"] = tenant.ToString();
        http.Request.Headers["X-Legal-Entity-Id"] = legalEntity.ToString();
        http.Request.Method = HttpMethods.Get;
        return (http, tenant, legalEntity);
    }

    private static string? ErrorCode(DefaultHttpContext http)
    {
        http.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(http.Response.Body);
        return document.RootElement.GetProperty("error").GetProperty("code").GetString();
    }
}
