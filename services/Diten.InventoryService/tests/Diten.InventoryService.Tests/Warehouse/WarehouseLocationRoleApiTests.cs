using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Persistence.Warehouse;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — AC-17, the task-free part (pack §4.4, §12, §16; inspection 02 F-4): create / edit / soft delete / bulk delete of location
/// roles through the REAL host; one live record per node; several roles on a node but never Receiving + Staging; the conflict with an
/// ancestor's or a lower node's role (F-13); the node rules (type, active, company). The open-task rule (AC-58) needs tasks — DL-2.
/// </summary>
public sealed class WarehouseLocationRoleApiTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public WarehouseLocationRoleApiTests(WarehouseApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_role_record_is_created_read_and_listed_with_its_node_and_warehouse()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);

        using var created = await s.CreateAsync(zone.Id, ["Receiving"], note: "  Mal kabul alanı  ");
        Assert.Equal(201, (int)created.StatusCode);
        Assert.Equal("W/\"1\"", created.Headers.ETag!.ToString());
        var role = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.Clone();
        Assert.Equal(zone.Id.ToString("D"), role.GetProperty("locationId").GetString());
        Assert.Equal(s.Inventory.Warehouse, role.GetProperty("warehouseId").GetString());
        Assert.Equal(s.LegalEntity.ToString("D"), role.GetProperty("legalEntityId").GetString());
        Assert.Equal(zone.Code, role.GetProperty("locationCode").GetString());
        Assert.Equal("Zone", role.GetProperty("locationType").GetString());
        Assert.Equal(["Receiving"], Strings(role.GetProperty("roles")));
        Assert.Empty(Strings(role.GetProperty("acceptedStorageConditions")));
        Assert.Equal("Mal kabul alanı", role.GetProperty("note").GetString());
        Assert.Equal(1, role.GetProperty("version").GetInt32());
        Assert.Equal("v1", role.GetProperty("contractVersion").GetString());
        Assert.False(role.TryGetProperty("effectiveStorageConditions", out _), "an untagged path answers no effective set (no null member)");

        using var read = await s.GetAsync($"/api/warehouse/location-roles/{Id(role)}");
        Assert.Equal(200, (int)read.StatusCode);
        Assert.Equal("W/\"1\"", read.Headers.ETag!.ToString());

        var page = await Expect(s.GetAsync("/api/warehouse/location-roles"), 200);
        Assert.Equal(1, page.GetProperty("total").GetInt64());
        Assert.Equal(Id(role), Id(page.GetProperty("items")[0]));
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(25, page.GetProperty("pageSize").GetInt32());

        var stored = Assert.Single(s.Stored(WarehouseCollections.LocationRoles));
        Assert.Equal(s.Tenant.ToString(), stored["TenantId"].AsString);
        Assert.Equal(s.User.ToString(), stored["CreatedBy"].AsString);
        Assert.False(stored["IsDeleted"].AsBoolean);
        Assert.Equal(["Receiving"], stored["Roles"].AsBsonArray.Select(v => v.AsString)); // role names, not numbers
    }

    [Fact]
    public async Task A_node_may_hold_several_roles_or_only_conditions_but_not_nothing()
    {
        var s = new WarehouseScenario(_factory);

        var conditionsOnly = await s.CreatedAsync(s.Node(LocationType.Zone).Id, conditions: ["COOL_2_8"]);
        Assert.Empty(Strings(conditionsOnly.GetProperty("roles")));
        var both = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Staging"], ["AMBIENT_15_25"]);
        Assert.Equal(["Staging"], Strings(both.GetProperty("roles")));

        Assert.Equal("WMS_STORAGE_CONDITION_INVALID", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin).Id), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving", "Receiving"]), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Putaway"]), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving"], note: new string('x', 501)), 400));
        Assert.Equal(2, s.Stored(WarehouseCollections.LocationRoles).Count);
    }

    [Fact]
    public async Task One_live_record_per_node_and_a_deleted_one_frees_the_node()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        var first = await s.CreatedAsync(bin.Id, ["Receiving"]);

        var exists = await Expect(s.CreateAsync(bin.Id, conditions: ["COOL_2_8"]), 409);
        Assert.Equal("WMS_LOCATION_ROLE_EXISTS", exists.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(Id(first), exists.GetProperty("error").GetProperty("details").GetProperty("roleId").GetString());

        await Expect(s.DeleteAsync(Id(first), ETag(first)), 200);
        var again = await s.CreatedAsync(bin.Id, conditions: ["COOL_2_8"]);
        Assert.NotEqual(Id(first), Id(again));
    }

    [Fact]
    public async Task Receiving_and_Staging_on_one_node_is_409_on_create_and_on_update()
    {
        var s = new WarehouseScenario(_factory);
        Assert.Equal("WMS_LOCATION_ROLE_CONFLICT", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving", "Staging"]), 409));

        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        Assert.Equal("WMS_LOCATION_ROLE_CONFLICT", await ErrorCode(s.UpdateAsync(Id(role), new() { ["roles"] = new[] { "Receiving", "Staging" } }, ETag(role)), 409));
    }

    /// <summary>F-13 / AC-17 — K3 sabotage (u): drop the inherited-conflict check → red.</summary>
    [Fact]
    public async Task A_node_under_a_Receiving_ancestor_cannot_be_Staging_and_neither_can_one_above_a_Staging_node()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);
        var bin = s.Node(LocationType.Bin, zone);
        await s.CreatedAsync(zone.Id, ["Receiving"]);

        var below = await Expect(s.CreateAsync(bin.Id, ["Staging"]), 409);
        Assert.Equal("WMS_LOCATION_ROLE_INHERITED_CONFLICT", below.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Staging", below.GetProperty("error").GetProperty("details").GetProperty("role").GetString());
        await s.CreatedAsync(bin.Id, ["Receiving"]); // the same role below is no conflict

        // the other direction: a Staging bin first, then its zone / storage location may not become Receiving
        var zone2 = s.Node(LocationType.Zone);
        var staging = s.Node(LocationType.Bin, zone2);
        await s.CreatedAsync(staging.Id, ["Staging"]);
        Assert.Equal("WMS_LOCATION_ROLE_INHERITED_CONFLICT", await ErrorCode(s.CreateAsync(zone2.Id, ["Receiving"]), 409));
        Assert.Equal("WMS_LOCATION_ROLE_INHERITED_CONFLICT", await ErrorCode(s.CreateAsync(s.Inventory.StorageNode.Id, ["Receiving"]), 409));

        // … also by an update of a conditions-only record
        var tagged = await s.CreatedAsync(zone2.Id, conditions: ["COOL_2_8"]);
        Assert.Equal("WMS_LOCATION_ROLE_INHERITED_CONFLICT",
            await ErrorCode(s.UpdateAsync(Id(tagged), new() { ["roles"] = new[] { "Receiving" } }, ETag(tagged)), 409));

        // another warehouse's Staging bin is not below this storage location
        var other = new WarehouseScenario(_factory);
        await other.CreatedAsync(other.Node(LocationType.Bin, other.Inventory.Storage2Node).Id, ["Staging"]);
        await other.CreatedAsync(other.Inventory.StorageNode.Id, ["Receiving"]);
    }

    /// <summary>
    /// A LOCATION move (K-07) may take a node under another warehouse: the conflict scan reads the CURRENT paths (not the warehouse stored on the
    /// record) and the answer names the node's current warehouse.
    /// </summary>
    [Fact]
    public async Task A_node_moved_under_another_warehouse_still_conflicts_there_and_answers_its_new_warehouse()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);
        var bin = s.Node(LocationType.Bin, zone);
        var staging = await s.CreatedAsync(bin.Id, ["Staging"]);
        Assert.Equal(s.Inventory.Warehouse, staging.GetProperty("warehouseId").GetString());

        // the zone (with its bin) moved under Storage2 of Warehouse2 — the materialized paths rewritten as LOCATION does (K-07)
        var locations = s.Factory.Collection(Diten.InventoryService.Persistence.InventoryCollections.Locations);
        var storage2 = s.Inventory.Storage2Node;
        BsonArray Path(params Guid[] ids) => new(ids.Select(i => i.ToString()));
        await locations.UpdateOneAsync(new BsonDocument("_id", zone.Id.ToString()), new BsonDocument("$set", new BsonDocument
            { ["ParentId"] = storage2.Id.ToString(), ["AncestorIds"] = Path([.. storage2.AncestorIds, storage2.Id]) }));
        await locations.UpdateOneAsync(new BsonDocument("_id", bin.Id.ToString()), new BsonDocument("$set", new BsonDocument
            { ["AncestorIds"] = Path([.. storage2.AncestorIds, storage2.Id, zone.Id]) }));

        Assert.Equal("WMS_LOCATION_ROLE_INHERITED_CONFLICT", await ErrorCode(s.CreateAsync(storage2.Id, ["Receiving"]), 409));
        Assert.Equal(s.Inventory.Warehouse2, (await Expect(s.GetAsync($"/api/warehouse/location-roles/{Id(staging)}"), 200)).GetProperty("warehouseId").GetString());
        await s.CreatedAsync(s.Inventory.StorageNode.Id, ["Receiving"]); // nothing Staging is below the old storage location any more
    }

    [Fact]
    public async Task A_role_needs_an_active_storage_location_zone_or_bin_of_the_company()
    {
        var s = new WarehouseScenario(_factory);
        Assert.Equal("WMS_LOCATION_TYPE_NOT_ALLOWED", await ErrorCode(s.CreateAsync(s.Inventory.WarehouseNode.Id, ["Receiving"]), 422));
        Assert.Equal("WMS_LOCATION_TYPE_NOT_ALLOWED", await ErrorCode(s.CreateAsync(s.Inventory.SiteNode.Id, ["Receiving"]), 422));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(s.Node(LocationType.Bin, status: LocationStatus.Inactive).Id, ["Receiving"]), 422));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(Guid.NewGuid(), ["Receiving"]), 422));
        var (_, foreignBin) = s.Inventory.AddForeignCompanyBin();
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(Guid.Parse(foreignBin), ["Receiving"]), 422));
        await s.CreatedAsync(s.Inventory.StorageNode.Id, conditions: ["AMBIENT_15_25"]);
        Assert.Single(s.Stored(WarehouseCollections.LocationRoles));
    }

    [Fact]
    public async Task An_update_changes_roles_conditions_and_note_under_If_Match_and_never_the_node()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Zone).Id, ["Receiving"], note: "eski");

        using var updated = await s.UpdateAsync(Id(role), new() { ["acceptedStorageConditions"] = new[] { "PROTECT_FROM_LIGHT", "COOL_2_8" }, ["note"] = null }, ETag(role));
        Assert.Equal(200, (int)updated.StatusCode);
        Assert.Equal("W/\"2\"", updated.Headers.ETag!.ToString());
        var after = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(["Receiving"], Strings(after.GetProperty("roles")));                    // not sent → kept
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_LIGHT"], Strings(after.GetProperty("acceptedStorageConditions")));
        Assert.False(after.TryGetProperty("note", out _));                                    // sent as null → cleared

        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.UpdateAsync(Id(role), new() { ["note"] = "x" }, ETag(role)), 409)); // stale (AC-12 pattern)
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.Send(HttpMethod.Put, $"/api/warehouse/location-roles/{Id(role)}",
            new Dictionary<string, object?> { ["idempotencyKey"] = "key-no-if-match-1", ["note"] = "x" }, "key-no-if-match-1", null), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.UpdateAsync(Id(role), new() { ["locationId"] = Guid.NewGuid().ToString("D") }, "W/\"2\""), 400));
        Assert.Equal("WMS_LOCATION_ROLE_NOT_FOUND", await ErrorCode(s.UpdateAsync(Guid.NewGuid().ToString("D"), new() { ["note"] = "x" }, "W/\"1\""), 404));
    }

    [Fact]
    public async Task A_delete_is_soft_and_the_record_is_gone_for_every_read()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);

        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.DeleteAsync(Id(role), "W/\"7\""), 409));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.Send(HttpMethod.Delete, $"/api/warehouse/location-roles/{Id(role)}", null, "delete-key-1", null), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.Send(HttpMethod.Delete, $"/api/warehouse/location-roles/{Id(role)}", null, null, ETag(role)), 400));

        using var deleted = await s.DeleteAsync(Id(role), ETag(role));
        Assert.Equal(200, (int)deleted.StatusCode);
        Assert.Equal("W/\"2\"", deleted.Headers.ETag!.ToString());

        var stored = Assert.Single(s.Stored(WarehouseCollections.LocationRoles));
        Assert.True(stored["IsDeleted"].AsBoolean);
        Assert.NotEqual(BsonNull.Value, stored["DeletedAt"]);
        Assert.Equal("WMS_LOCATION_ROLE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/location-roles/{Id(role)}"), 404));
        Assert.Equal(0, (await Expect(s.GetAsync("/api/warehouse/location-roles"), 200)).GetProperty("total").GetInt64());
        Assert.Equal("WMS_LOCATION_ROLE_NOT_FOUND", await ErrorCode(s.DeleteAsync(Id(role), "W/\"2\""), 404));
    }

    [Fact]
    public async Task A_bulk_delete_answers_each_row_deleted_conflict_or_not_found()
    {
        var s = new WarehouseScenario(_factory);
        var a = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var b = await s.CreatedAsync(s.Node(LocationType.Bin).Id, conditions: ["COOL_2_8"]);
        var stale = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Staging"]);
        var unknown = Guid.NewGuid().ToString("D");

        var result = await Expect(s.BulkDeleteAsync([(Id(a), 1), (Id(b), 1), (Id(stale), 5), (unknown, 1)]), 200);
        var rows = result.GetProperty("items").EnumerateArray().ToDictionary(r => r.GetProperty("roleId").GetString()!, r => r);
        Assert.Equal("Deleted", rows[Id(a)].GetProperty("result").GetString());
        Assert.Equal(2, rows[Id(a)].GetProperty("version").GetInt32());
        Assert.Equal("Deleted", rows[Id(b)].GetProperty("result").GetString());
        Assert.Equal("Conflict", rows[Id(stale)].GetProperty("result").GetString());
        Assert.False(rows[Id(stale)].TryGetProperty("version", out _));
        Assert.Equal("NotFound", rows[unknown].GetProperty("result").GetString());

        var live = s.Stored(WarehouseCollections.LocationRoles).Where(r => !r["IsDeleted"].AsBoolean).Select(r => r["_id"].AsString).ToList();
        Assert.Equal([Id(stale)], live);

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.BulkDeleteAsync([]), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.BulkDeleteAsync([(Id(stale), 1), (Id(stale), 1)]), 400));
    }

    [Fact]
    public async Task The_list_filters_by_warehouse_and_role_and_refuses_malformed_values()
    {
        var s = new WarehouseScenario(_factory);
        await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        await s.CreatedAsync(s.Node(LocationType.Bin).Id, conditions: ["COOL_2_8"]);
        await s.CreatedAsync(s.Node(LocationType.Bin, s.Inventory.Storage2Node).Id, ["Receiving"]);

        Assert.Equal(2, (await Expect(s.GetAsync("/api/warehouse/location-roles?role=Receiving"), 200)).GetProperty("total").GetInt64());
        Assert.Equal(2, (await Expect(s.GetAsync($"/api/warehouse/location-roles?warehouseId={s.Inventory.Warehouse}"), 200)).GetProperty("total").GetInt64());
        Assert.Equal(1, (await Expect(s.GetAsync($"/api/warehouse/location-roles?warehouseId={s.Inventory.Warehouse2}&role=Receiving"), 200))
            .GetProperty("total").GetInt64());
        var paged = await Expect(s.GetAsync("/api/warehouse/location-roles?page=2&pageSize=2"), 200);
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(3, paged.GetProperty("total").GetInt64());

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/location-roles?role=receiving"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/location-roles?warehouseId=abc"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/location-roles?pageSize=201"), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/location-roles?page=0"), 400));
    }
}
