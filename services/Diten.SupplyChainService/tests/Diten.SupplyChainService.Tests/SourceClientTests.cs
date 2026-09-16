using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Infrastructure.SourceIntake;
using Xunit;

namespace Diten.SupplyChainService.Tests;

public sealed class SourceClientTests
{
    private static readonly Guid Item = Guid.Parse("b1f2c3d4-0000-0000-0000-000000000001");
    private static readonly Guid Sku = Guid.Parse("c3d4e5f6-0000-0000-0000-000000000002");
    private const string Detail = """
        {"outboundId":"OUT-9001","warehouseId":"wh-01","orderRef":"SO-7788","status":"ReadyToShip",
        "shipTo":{"name":"Acme Pharma Depo","country":"tr","city":"Istanbul","postalCode":"34000","line1":"Org. San. Böl. 5"},
        "lines":[{"orderLineId":"1","itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","lotNumber":"L2026-0455","quantity":"100.000","uomId":"BOX"}],
        "packages":[{"packageId":"PKG-1","weightKg":"12.500","dimensionsCm":"40x30x20"}],"readyAt":"2026-09-15T08:00:00Z"}
        """;

    private static TrustedSourceContext Context()
    {
        var scope = new ShipmentScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        return TrustedSourceContext.FromValidatedPrincipal(Principal(scope), scope, "opaque-test-token", Guid.NewGuid());
    }
    private static ClaimsPrincipal Principal(ShipmentScope scope, string? authentication = "validated-test") => new(new ClaimsIdentity([
        new("tenant_id", scope.TenantId.ToString()), new("legal_entity_id", scope.LegalEntityId.ToString()),
        new("sub", scope.ActorId.ToString()), new("permission", "supplychain.shipments.create")], authentication));

    [Fact]
    public async Task WarehouseFrozenDetailAndPagePreserveFactsAndForwardTrustedScope()
    {
        var context = Context();
        var handler = new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.DoesNotContain("movements", request.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(context.Scope.TenantId.ToString(), request.Headers.GetValues("X-Tenant-Id").Single());
            Assert.Equal(context.Scope.LegalEntityId.ToString(), request.Headers.GetValues("X-Legal-Entity-Id").Single());
            Assert.Equal(context.CorrelationId.ToString(), request.Headers.GetValues("X-Correlation-Id").Single());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(Response(request.RequestUri!.AbsolutePath.EndsWith("OUT-9001", StringComparison.Ordinal) ? Detail :
                "{\"items\":[" + Detail + "],\"contractVersion\":\"v1\"}"));
        });
        var client = new WarehouseReadClient(Http(handler));
        var detail = await client.GetAsync(context, "OUT-9001");
        Assert.True(detail.Success);
        Assert.Equal("100.000", detail.Value.GetProperty("lines")[0].GetProperty("quantity").GetString());
        Assert.Equal("L2026-0455", detail.Value.GetProperty("lines")[0].GetProperty("lotNumber").GetString());
        Assert.True((await client.ListAsync(context, "ReadyToShip", "wh-01")).Success);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(404, "DEPENDENCY_HTTP_404")]
    [InlineData(503, "DEPENDENCY_HTTP_503")]
    [InlineData(200, "SCHEMA_INCOMPATIBLE")]
    public async Task ExplicitWarehouseFailures(int status, string code)
    {
        var result = await new WarehouseReadClient(Http(new Handler((_, _) => Task.FromResult(Response("{broken", status)))))
            .GetAsync(Context(), "OUT-9001");
        Assert.False(result.Success);
        Assert.Equal(code, result.ErrorCode);
    }

    [Fact]
    public async Task WarehouseTimeoutAndStrictNullableGapAreExplicit()
    {
        var timeout = new WarehouseReadClient(Http(new Handler((_, _) => throw new TaskCanceledException())));
        Assert.Equal("DEPENDENCY_TIMEOUT", (await timeout.GetAsync(Context(), "OUT-9001")).ErrorCode);
        var cursor = new WarehouseReadClient(Http(new Handler((_, _) => Task.FromResult(Response("{\"items\":[],\"nextCursor\":null}")))));
        Assert.Equal("SCHEMA_INCOMPATIBLE", (await cursor.ListAsync(Context())).ErrorCode);
        var invalidQuantity = new WarehouseReadClient(Http(new Handler((_, _) => Task.FromResult(Response(Detail.Replace("100.000", "١٠٠.٠٠٠", StringComparison.Ordinal))))));
        Assert.Equal("SCHEMA_INCOMPATIBLE", (await invalidQuantity.GetAsync(Context(), "OUT-9001")).ErrorCode);
    }

    [Fact]
    public void TrustedContextRejectsMissingUnauthenticatedAndConflictingScopes()
    {
        var scope = new ShipmentScope(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<UnauthorizedAccessException>(() => TrustedSourceContext.FromValidatedPrincipal(Principal(scope, null), scope, "token"));
        Assert.Throws<UnauthorizedAccessException>(() => TrustedSourceContext.FromValidatedPrincipal(Principal(scope), scope with { LegalEntityId = Guid.NewGuid() }, "token"));
        Assert.Throws<UnauthorizedAccessException>(() => TrustedSourceContext.FromValidatedPrincipal(new ClaimsPrincipal(new ClaimsIdentity([], "test")), scope, "token"));
        Assert.Throws<UnauthorizedAccessException>(() => TrustedSourceContext.FromValidatedPrincipal(Principal(scope), scope, "token", Guid.Empty));
        var duplicate = Principal(scope);
        ((ClaimsIdentity)duplicate.Identity!).AddClaim(new Claim("tenant_id", scope.TenantId.ToString()));
        Assert.Throws<UnauthorizedAccessException>(() => TrustedSourceContext.FromValidatedPrincipal(duplicate, scope, "token"));
    }

    [Fact]
    public async Task InventoryReadOnlyScopeAndFailureIsolation()
    {
        var context = Context();
        var reference = new InventoryReference(Item, Sku, "Lsku", "wh-01");
        var row = JsonSerializer.Serialize(new { itemId = Item, skuId = Sku, skuLevel = "Lsku", legalEntityId = context.Scope.LegalEntityId, onHand = "100.000", reserved = "30.000", available = "70.000" });
        var handler = new Handler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(context.Scope.TenantId.ToString(), request.Headers.GetValues("X-Tenant-Id").Single());
            Assert.Contains("warehouseId=wh-01", request.RequestUri!.Query);
            return Task.FromResult(Response(request.RequestUri.AbsolutePath.EndsWith("balance", StringComparison.Ordinal)
                ? "{\"rows\":[" + row + "],\"total\":1,\"contractVersion\":\"v1\"}" : row));
        });
        var client = new InventoryReadClient(Http(handler));
        Assert.True((await client.AvailabilityAsync(context, reference)).Success);
        Assert.True((await client.BalanceAsync(context, reference)).Success);
        Assert.Equal(2, handler.Calls);
        Assert.Equal("INVALID_INVENTORY_REFERENCE", (await client.AvailabilityAsync(context, reference with { ItemId = Guid.Empty })).ErrorCode);
        Assert.Equal(2, handler.Calls);
        foreach (var status in new[] { 404, 503 })
        {
            var unavailable = new InventoryReadClient(Http(new Handler((_, _) => Task.FromResult(Response("{}", status)))));
            Assert.Equal("DEPENDENCY_HTTP_" + status, (await unavailable.AvailabilityAsync(context, reference)).ErrorCode);
        }
        var wrongScope = new InventoryReadClient(Http(new Handler((_, _) => Task.FromResult(Response(row.Replace(context.Scope.LegalEntityId.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal))))));
        Assert.Equal("SCHEMA_INCOMPATIBLE", (await wrongScope.AvailabilityAsync(context, reference)).ErrorCode);
        var timeout = new InventoryReadClient(Http(new Handler((_, _) => throw new TaskCanceledException())));
        Assert.Equal("DEPENDENCY_TIMEOUT", (await timeout.BalanceAsync(context, reference)).ErrorCode);
    }

    private static HttpClient Http(Handler handler) => new(handler) { BaseAddress = new Uri("http://frozen-contract-mock.test") };
    private static HttpResponseMessage Response(string json, int status = 200) => new((HttpStatusCode)status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }
}
