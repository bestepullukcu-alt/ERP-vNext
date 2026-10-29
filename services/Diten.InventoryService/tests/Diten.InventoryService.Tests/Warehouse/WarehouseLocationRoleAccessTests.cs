using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — AC-13 and AC-14 on the location-role endpoints (pack §8.1, §14.3, F-11), through the REAL host and middleware.
/// <list type="bullet">
/// <item>AC-13: another tenant's record is 404 (K3 sabotage (p): drop the tenant predicate → red); another company's record of the same tenant is
/// 404 on every endpoint and absent from the list (K3 sabotage (d): drop the company predicate → red); an <c>X-Legal-Entity-Id</c> the token /
/// membership does not allow is 403 <c>CROSS_LE_FORBIDDEN</c>; a scope member in a body or query is 400.</item>
/// <item>AC-14: no token 401; a token without the key 403 on every endpoint (K3 sabotage (q) is measured by <c>AuthorizationGuardTests</c>, which
/// scans every action of the service, this controller included).</item>
/// </list>
/// </summary>
public sealed class WarehouseLocationRoleAccessTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public WarehouseLocationRoleAccessTests(WarehouseApiFactory factory) => _factory = factory;

    private static IEnumerable<Func<WarehouseScenario, string, string?, Task<HttpResponseMessage>>> EveryEndpoint()
    {
        yield return (s, id, token) => s.GetAsync($"/api/warehouse/location-roles/{id}", token);
        yield return (s, id, token) => s.UpdateAsync(id, new() { ["note"] = "x" }, "W/\"1\"", token: token);
        yield return (s, id, token) => s.DeleteAsync(id, "W/\"1\"", token: token);
    }

    /// <summary>K3 sabotage (p): drop the tenant predicate of the role repository → red.</summary>
    [Fact]
    public async Task Another_tenants_record_is_404_on_every_endpoint()
    {
        var owner = new WarehouseScenario(_factory);
        var role = await owner.CreatedAsync(owner.Node(LocationType.Bin).Id, ["Receiving"]);
        var stranger = new WarehouseScenario(_factory);

        foreach (var call in EveryEndpoint())
        {
            Assert.Equal("WMS_LOCATION_ROLE_NOT_FOUND", await ErrorCode(call(stranger, Id(role), null), 404));
        }

        var rows = (await Expect(stranger.BulkDeleteAsync([(Id(role), 1)]), 200)).GetProperty("items");
        Assert.Equal("NotFound", rows[0].GetProperty("result").GetString());
        Assert.Equal(0, (await Expect(stranger.GetAsync("/api/warehouse/location-roles"), 200)).GetProperty("total").GetInt64());
        Assert.False(Assert.Single(owner.Stored(WarehouseCollections.LocationRoles))["IsDeleted"].AsBoolean);
        Assert.Single(owner.Events(Id(role)));
    }

    /// <summary>F-11 — K3 sabotage (d): drop the company predicate of the role repository → red.</summary>
    [Fact]
    public async Task Another_companys_record_of_the_same_tenant_is_404_and_never_listed()
    {
        var owner = new WarehouseScenario(_factory);
        var role = await owner.CreatedAsync(owner.Node(LocationType.Bin).Id, ["Receiving"]);
        var otherCompany = Guid.NewGuid();
        var token = TestTokens.Mint(owner.Tenant, otherCompany, owner.User, [Read, Manage]);

        foreach (var call in EveryEndpoint())
        {
            Assert.Equal("WMS_LOCATION_ROLE_NOT_FOUND", await ErrorCode(call(owner, Id(role), token), 404));
        }

        Assert.Equal("NotFound", (await Expect(owner.BulkDeleteAsync([(Id(role), 1)], token: token), 200)).GetProperty("items")[0].GetProperty("result").GetString());
        Assert.Equal(0, (await Expect(owner.GetAsync("/api/warehouse/location-roles", token), 200)).GetProperty("total").GetInt64());
        Assert.Equal(1, (await Expect(owner.GetAsync("/api/warehouse/location-roles"), 200)).GetProperty("total").GetInt64());
        Assert.False(Assert.Single(owner.Stored(WarehouseCollections.LocationRoles))["IsDeleted"].AsBoolean);
    }

    [Fact]
    public async Task A_company_header_the_caller_may_not_use_is_403_cross_le_forbidden()
    {
        var s = new WarehouseScenario(_factory);
        var token = TestTokens.Mint(s.Tenant, null, s.User, [Read, Manage]); // no company claim → the header decides, verified with MDM
        var foreign = Guid.NewGuid();                                        // not registered for this tenant
        Assert.Equal("CROSS_LE_FORBIDDEN", await ErrorCode(s.GetAsync("/api/warehouse/location-roles", token,
            r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", foreign.ToString("D"))), 403));
        Assert.Equal("CROSS_LE_FORBIDDEN", await ErrorCode(s.GetAsync("/api/warehouse/location-roles", null,
            r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", foreign.ToString("D"))), 403)); // header ≠ the token's company
    }

    [Fact]
    public async Task A_scope_member_in_a_body_or_query_is_400()
    {
        var s = new WarehouseScenario(_factory);
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.Send(HttpMethod.Post, "/api/warehouse/location-roles", new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "scope-key-0001", ["locationId"] = s.Node(LocationType.Bin).Id.ToString("D"), ["roles"] = new[] { "Receiving" },
            ["tenantId"] = Guid.NewGuid().ToString("D")
        }, "scope-key-0001", null), 400));
        Assert.Equal(400, (int)(await s.GetAsync($"/api/warehouse/location-roles?legalEntityId={Guid.NewGuid():D}")).StatusCode);
        Assert.Empty(s.Stored(WarehouseCollections.LocationRoles));
    }

    [Fact]
    public async Task No_token_is_401_and_a_token_without_the_key_is_403_on_every_endpoint()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var bin = s.Node(LocationType.Bin).Id;
        var none = s.TokenFor(s.User, "inventory.stock.read");
        var readOnly = s.TokenFor(s.User, Read);

        Func<string?, Task<HttpResponseMessage>>[] writes =
        [
            t => s.CreateAsync(bin, ["Receiving"], token: t),
            t => s.UpdateAsync(Id(role), new() { ["note"] = "x" }, ETag(role), token: t),
            t => s.DeleteAsync(Id(role), ETag(role), token: t),
            t => s.BulkDeleteAsync([(Id(role), 1)], token: t)
        ];
        Func<string?, Task<HttpResponseMessage>>[] reads =
        [
            t => s.GetAsync("/api/warehouse/location-roles", t),
            t => s.GetAsync($"/api/warehouse/location-roles/{Id(role)}", t)
        ];

        foreach (var call in writes.Concat(reads))
        {
            using (var anonymous = await call(string.Empty)) Assert.Equal(401, (int)anonymous.StatusCode);
            using (var unkeyed = await call(none)) Assert.Equal(403, (int)unkeyed.StatusCode);
        }

        foreach (var call in writes)
        {
            using var reader = await call(readOnly);
            Assert.Equal(403, (int)reader.StatusCode);
        }

        foreach (var call in reads)
        {
            using var reader = await call(readOnly);
            Assert.Equal(200, (int)reader.StatusCode);
        }

        var stored = Assert.Single(s.Stored(WarehouseCollections.LocationRoles));
        Assert.Equal(1, stored["Version"].AsInt32); // nothing a refused caller sent was written
    }
}
