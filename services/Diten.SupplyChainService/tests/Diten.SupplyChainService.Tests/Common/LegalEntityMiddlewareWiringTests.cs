using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Diten.SupplyChainService.Api.Features.Claims;
using Diten.SupplyChainService.Api.Features.Returns;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
using Diten.SupplyChainService.Infrastructure.Features.Returns;
using Xunit;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// R-2 (PR #134) wiring proof. LegalEntityScopeValidatorTests prove the validator decides correctly; these prove the
/// middlewares ACT on the decision — a validator nobody consults is K23's shape. No Mongo: the middleware is reached
/// directly, so R-2's rejection facts are provable on an unprovisioned machine.
///
/// The two modules here carry the two different NotReferenceable conventions, deliberately: Returns answers a scope
/// mismatch with 404 RETURN_NOT_FOUND, Claims with 403 FORBIDDEN. Each keeps its own, which is where its retired
/// claim-versus-header branch sent a mismatch.
/// </summary>
public sealed class LegalEntityMiddlewareWiringTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa");
    private static readonly Guid LegalEntity = Guid.Parse("bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("cccccccc-3333-4333-8333-cccccccccccc");

    // The token carries tenant_id and sub only. R-2 removed legal_entity_id from it, and the middlewares'
    // UniqueSignedContextFields no longer requires it — asserted here by minting a token that simply lacks it.
    private static string Token()
    {
        var payload = JsonSerializer.Serialize(new { tenant_id = Tenant, sub = Actor });
        var segment = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "Bearer e30." + segment + ".synthetic";
    }

    private static DefaultHttpContext Context(string path, string permission, bool sendLegalEntityHeader = true)
    {
        var http = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        http.RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        http.Request.Path = path;
        http.Request.Method = "GET";
        http.Request.Headers.Authorization = Token();
        http.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();
        http.Request.Headers["X-Tenant-Id"] = Tenant.ToString();
        if (sendLegalEntityHeader) http.Request.Headers["X-Legal-Entity-Id"] = LegalEntity.ToString();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenant_id", Tenant.ToString()),
            new Claim("sub", Actor.ToString()),
            new Claim("permission", permission),
        ], "wiring"));
        return http;
    }

    private static async Task<(int Status, bool Next, StubLegalEntityScopeValidator Stub)> Returns(
        LegalEntityScopeOutcome outcome, bool sendLegalEntityHeader = true)
    {
        var http = Context("/api/shipment-bundle/returns", "supplychain.returns.read", sendLegalEntityHeader);
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ReturnPermissionAttribute("supplychain.returns.read")), "wiring"));
        var next = false;
        var stub = new StubLegalEntityScopeValidator(outcome);
        var middleware = new ReturnContextMiddleware(_ => { next = true; return Task.CompletedTask; },
            NullLogger<ReturnContextMiddleware>.Instance);
        await middleware.InvokeAsync(http, new ReturnRequestContext(), new RequestContext(), stub);
        return (http.Response.StatusCode, next, stub);
    }

    private static async Task<(int Status, bool Next, StubLegalEntityScopeValidator Stub)> Claims(
        LegalEntityScopeOutcome outcome, bool sendLegalEntityHeader = true)
    {
        var http = Context("/api/shipment-bundle/claims", ClaimPermissions.Read, sendLegalEntityHeader);
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ClaimPermissionAttribute(ClaimPermissions.Read)), "wiring"));
        var next = false;
        var stub = new StubLegalEntityScopeValidator(outcome);
        var middleware = new ClaimContextMiddleware(_ => { next = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(http, new ClaimRequestContext(), new RequestContext(), stub);
        return (http.Response.StatusCode, next, stub);
    }

    [Fact]
    public async Task Valid_LetsTheRequestThrough_AndTheMiddlewareAskedWithTheTokenTenantAndHeaderLegalEntity()
    {
        var (status, next, stub) = await Returns(LegalEntityScopeOutcome.Valid);
        Assert.Equal(200, status);
        Assert.True(next);
        var call = Assert.Single(stub.Calls);
        // Tenant comes from the JWT, LegalEntityId from the header — the whole point of R-2.
        Assert.Equal(Tenant, call.TenantId);
        Assert.Equal(LegalEntity, call.LegalEntityId);
        Assert.StartsWith("Bearer ", call.Authorization);
    }

    [Fact]
    public async Task Returns_NotReferenceable_Is404_AndNeverReachesTheHandler()
    {
        var (status, next, _) = await Returns(LegalEntityScopeOutcome.NotReferenceable);
        Assert.Equal(404, status);
        Assert.False(next);
    }

    [Fact]
    public async Task Returns_Unavailable_Is503_AndNeverReachesTheHandler()
    {
        var (status, next, _) = await Returns(LegalEntityScopeOutcome.Unavailable);
        Assert.Equal(503, status);
        Assert.False(next);
    }

    // Claims answers a scope mismatch with 403, not 404, and keeps that convention here.
    [Fact]
    public async Task Claims_NotReferenceable_Is403_AndNeverReachesTheHandler()
    {
        var (status, next, _) = await Claims(LegalEntityScopeOutcome.NotReferenceable);
        Assert.Equal(403, status);
        Assert.False(next);
    }

    [Fact]
    public async Task Claims_Unavailable_Is503_AndNeverReachesTheHandler()
    {
        var (status, next, _) = await Claims(LegalEntityScopeOutcome.Unavailable);
        Assert.Equal(503, status);
        Assert.False(next);
    }

    // With the claim gone there is nothing to fall back to, so a missing header is a request fault and the
    // middleware must not even ask MDM — asking would mean it had resolved a legal entity from somewhere else.
    [Theory]
    [InlineData("returns")]
    [InlineData("claims")]
    public async Task MissingLegalEntityHeader_Is400_AndMdmIsNeverAsked(string module)
    {
        var (status, next, stub) = module == "returns"
            ? await Returns(LegalEntityScopeOutcome.Valid, sendLegalEntityHeader: false)
            : await Claims(LegalEntityScopeOutcome.Valid, sendLegalEntityHeader: false);
        Assert.Equal(400, status);
        Assert.False(next);
        Assert.Empty(stub.Calls);
    }
}
