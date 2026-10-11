using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Loads;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

public sealed class LoadIsolationTests
{
    [Fact]
    public async Task TwoTenantsByTwoLegalEntitiesCanReuseIdsWithoutCrossScopeVisibility()
    {
        await using var f = new LoadTestContext();
        var tenants = new[] { f.Tenant, Guid.NewGuid() };
        var entities = new[] { f.LegalEntity, Guid.NewGuid() };
        var created = new List<(Guid Tenant, Guid Le, Guid Load)>();
        foreach (var tenant in tenants)
        foreach (var le in entities)
        {
            var response = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(f.ShipmentId), "same-key", tenant: tenant, le: le);
            Assert.Equal(201, (int)response.Response.StatusCode);
            created.Add((tenant, le, Guid.Parse(response.Body!["loadId"]!.GetValue<string>())));
            var list = await f.Send("GET", "/api/shipment-bundle/loads", tenant: tenant, le: le);
            Assert.Equal(1, list.Body!["total"]!.GetValue<int>());
            Assert.Equal(created[^1].Load.ToString(), list.Body["items"]![0]!["loadId"]!.GetValue<string>());
        }
        Assert.Equal(4, created.Select(x => x.Load).Distinct().Count());
        var before = await f.Count("loads");
        var foreign = await f.Send("POST", $"/api/shipment-bundle/loads/{created[0].Load}/transition", f.TransitionBody("Planned"), "foreign", tenant: tenants[1], le: entities[0]);
        Assert.Equal(404, (int)foreign.Response.StatusCode);
        Assert.Equal("LOAD_NOT_FOUND", foreign.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, await f.Count("loads"));
    }

    [Fact]
    public async Task PermissionScopesAndTrustedHeaderMismatchFailBeforePersistence()
    {
        await using var f = new LoadTestContext();
        foreach (var (method, path, permission) in new[]
        {
            ("GET", "/api/shipment-bundle/loads", "create"),
            ("POST", "/api/shipment-bundle/loads", "read"),
            ("POST", "/api/shipment-bundle/loads/" + Guid.NewGuid() + "/transition", "create")
        })
        {
            using var request = f.Request(method, path, permissions: [permission], key: "unauthorized", body: method == "POST" ? f.CreateBody() : null);
            using var response = await f.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var mismatch = f.Request("POST", "/api/shipment-bundle/loads", body: f.CreateBody(), key: "mismatch");
        mismatch.Headers.Remove("X-Tenant-Id");
        mismatch.Headers.TryAddWithoutValidation("X-Tenant-Id", Guid.NewGuid().ToString());
        using var denied = await f.Client.SendAsync(mismatch);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(0, await f.Count("loads_receipts"));
        Assert.Empty(f.Calls);
    }

    [Fact]
    public async Task AuthenticationAndPostAuthDuplicateSignedClaimsHaveDistinctOutcomes()
    {
        await using var f = new LoadTestContext();
        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/shipment-bundle/loads");
        invalid.Headers.TryAddWithoutValidation("Authorization", "Bearer malformed");
        using var unauthenticated = await f.Client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var token = SignedDuplicateTenantToken(f.Tenant, f.LegalEntity, f.Actor);
        using var duplicate = f.Request("GET", "/api/shipment-bundle/loads");
        duplicate.Headers.Remove("Authorization"); duplicate.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        using var rejected = await f.Client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(0, await f.Count("loads"));
    }

    [Fact]
    public async Task ScopeQueryKeysAreRejectedAfterOneDecodeAndUnknownKeysRemainIgnored()
    {
        await using var f = new LoadTestContext();
        foreach (var key in new[] { "tenantId", "TENANT_ID", "X-Tenant-Id", "legalEntityId", "LEGAL_ENTITY_ID", "X-Legal-Entity-Id", "%74enantId" })
        {
            var result = await f.Send("GET", "/api/shipment-bundle/loads?" + key + "=");
            Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
            Assert.Equal("INVALID_REQUEST", result.Body!["error"]!["code"]!.GetValue<string>());
        }
        var ignored = await f.Send("GET", "/api/shipment-bundle/loads?foo=x&tenantId%5B%5D=x");
        Assert.Equal(HttpStatusCode.OK, ignored.Response.StatusCode);
        Assert.Equal(0, ignored.Body!["total"]!.GetValue<int>());
    }

    private static string SignedDuplicateTenantToken(Guid tenant, Guid le, Guid actor)
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new
        {
            iss = "loads-tests", aud = "loads-tests", exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            tenant_id = tenant.ToString(), tenantIdDuplicate = tenant.ToString(), legal_entity_id = le.ToString(), sub = actor.ToString(),
            permission = new[] { "supplychain.loads.read" }
        }).Replace("tenantIdDuplicate", "tenant_id", StringComparison.Ordinal));
        var input = header + "." + payload;
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(LoadTestFactory.Secret), Encoding.ASCII.GetBytes(input));
        return input + "." + Base64UrlEncoder.Encode(signature);
    }
}
