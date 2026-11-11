using Diten.InventoryService.Domain.Enums;
using MongoDB.Bson;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — the role rows of pack §21 and the enlistment rule of §8.8, measured platform-less on the BL-501 outbox
/// (<c>central_audit_outbox</c>) and on the local history (<c>wms_location_role_events</c>):
/// <list type="bullet">
/// <item>create / update / delete = 1 forwarded central row (<c>Pending</c>, <c>ConfirmedByCommand</c>; Category <c>MasterData</c>, Operation
/// Create / Update / Delete, EntityType <c>WarehouseLocationRole</c>, SourceModule <c>PUTAWAY-PICKING-PACKING</c>, EntityId = the role) / 0
/// <c>Abandoned</c> / 1 local row carrying the before / after values;</item>
/// <item>bulk delete of n rows = 1 central row without EntityId / n local rows; none deleted = 0 / 0 / 0;</item>
/// <item>a replay and a reused key = 0 / 0 / 0; a stale If-Match = 0 / 0 / 0 — no <c>Abandoned</c> row either, because a 0-record write never
/// enlists (K3 sabotage (bk): enlist / write the event on 0 records → an <c>Abandoned</c> row and an event appear → red).</item>
/// </list>
/// </summary>
public sealed class WarehouseLocationRoleAuditTests : IClassFixture<WarehouseApiFactory>
{
    private const string Create = "CreateWarehouseLocationRoleCommand";
    private const string Update = "UpdateWarehouseLocationRoleCommand";
    private const string Delete = "DeleteWarehouseLocationRoleCommand";
    private const string BulkDelete = "BulkDeleteWarehouseLocationRoleCommand";

    private readonly WarehouseApiFactory _factory;

    public WarehouseLocationRoleAuditTests(WarehouseApiFactory factory) => _factory = factory;

    private static void AssertForwarded(BsonDocument row, string operation, string? entityId)
    {
        Assert.Contains(row["Status"].ToString(), new[] { "Pending", "Delivered" });
        Assert.True(row["ConfirmedByCommand"].AsBoolean);
        Assert.Equal("MasterData", row["Category"].AsString);
        Assert.Equal(operation, row["Operation"].AsString);
        Assert.Equal("WarehouseLocationRole", row["EntityType"].AsString);
        Assert.Equal("PUTAWAY-PICKING-PACKING", row["SourceModule"].AsString);
        if (entityId is null) Assert.True(!row.Contains("EntityId") || row["EntityId"].IsBsonNull);
        else Assert.Equal(entityId, row["EntityId"].ToString());
    }

    [Fact]
    public async Task Create_update_and_delete_each_leave_one_central_row_and_one_history_row_with_the_values()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        var role = await s.CreatedAsync(bin.Id, ["Receiving"], ["COOL_2_8"], "ilk not");
        var updated = await Expect(s.UpdateAsync(Id(role), new() { ["acceptedStorageConditions"] = new[] { "FROZEN_BELOW_MINUS_20" } }, ETag(role)), 200);
        await Expect(s.DeleteAsync(Id(role), ETag(updated)), 200);

        AssertForwarded(Assert.Single(s.CentralRows(Create)), "Create", Id(role));
        AssertForwarded(Assert.Single(s.CentralRows(Update)), "Update", Id(role));
        AssertForwarded(Assert.Single(s.CentralRows(Delete)), "Delete", Id(role));

        var events = s.Events(Id(role));
        Assert.Equal(["Created", "Updated", "Deleted"], events.Select(e => e["Kind"].AsString));
        Assert.Equal([1, 2, 3], events.Select(e => e["Seq"].AsInt32));
        Assert.All(events, e => Assert.Equal(s.User.ToString(), e["ActorUserId"].AsString));

        var created = events[0];
        Assert.Equal(bin.Id.ToString(), created["After"]["LocationId"].AsString);
        Assert.Equal(["Receiving"], created["After"]["Roles"].AsBsonArray.Select(v => v.AsString));
        Assert.Equal("ilk not", created["After"]["Note"].AsString);
        Assert.True(!created.Contains("IdempotencyKey") || created["IdempotencyKey"].IsBsonNull, "the create key lives on the record (pack §4.4)");

        var edit = events[1];
        Assert.Equal(["COOL_2_8"], edit["Before"]["AcceptedStorageConditions"].AsBsonArray.Select(v => v.AsString));
        Assert.Equal(["FROZEN_BELOW_MINUS_20"], edit["After"]["AcceptedStorageConditions"].AsBsonArray.Select(v => v.AsString));
        Assert.Equal("updateLocationRole", edit["RequestOp"].AsString);
        Assert.False(string.IsNullOrEmpty(edit["RequestFingerprint"].AsString));

        var removal = events[2];
        Assert.Equal("deleteLocationRole", removal["RequestOp"].AsString);
        Assert.Equal(["FROZEN_BELOW_MINUS_20"], removal["Before"]["AcceptedStorageConditions"].AsBsonArray.Select(v => v.AsString));
    }

    [Fact]
    public async Task A_bulk_delete_of_n_rows_is_one_central_row_without_an_entity_and_n_history_rows()
    {
        var s = new WarehouseScenario(_factory);
        var a = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var b = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        await Expect(s.BulkDeleteAsync([(Id(a), 1), (Id(b), 1)], "bulk-audit-key-1"), 200);

        AssertForwarded(Assert.Single(s.CentralRows(BulkDelete)), "Delete", null);
        foreach (var id in new[] { Id(a), Id(b) })
        {
            var deleted = s.Events(id).Last();
            Assert.Equal("Deleted", deleted["Kind"].AsString);
            Assert.Equal("bulkDeleteLocationRoles", deleted["RequestOp"].AsString);
            Assert.Equal("bulk-audit-key-1", deleted["IdempotencyKey"].AsString);
        }
    }

    [Fact]
    public async Task A_bulk_delete_that_deletes_nothing_leaves_nothing()
    {
        var s = new WarehouseScenario(_factory);
        var a = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        await Expect(s.BulkDeleteAsync([(Id(a), 4), (Guid.NewGuid().ToString("D"), 1)]), 200);

        Assert.Empty(s.CentralRows(BulkDelete));
        Assert.Single(s.Events(Id(a)));
    }

    [Fact]
    public async Task A_replay_and_a_reused_key_leave_nothing()
    {
        var s = new WarehouseScenario(_factory);
        var bin = s.Node(LocationType.Bin);
        var role = await Expect(s.CreateAsync(bin.Id, ["Receiving"], key: "audit-create-01"), 201);
        await Expect(s.CreateAsync(bin.Id, ["Receiving"], key: "audit-create-01"), 200);
        await Expect(s.CreateAsync(bin.Id, ["Staging"], key: "audit-create-01"), 400);
        await Expect(s.UpdateAsync(Id(role), new() { ["note"] = "n" }, ETag(role), "audit-update-01"), 200);
        await Expect(s.UpdateAsync(Id(role), new() { ["note"] = "n" }, ETag(role), "audit-update-01"), 200);
        await Expect(s.UpdateAsync(Id(role), new() { ["note"] = "m" }, ETag(role), "audit-update-01"), 400);

        Assert.Single(s.CentralRows(Create));
        Assert.Single(s.CentralRows(Update));
        Assert.Equal(2, s.Events(Id(role)).Count);
    }

    /// <summary>K3 sabotage (bk): enlist and write the event on a 0-record write → an Abandoned central row and a history row appear → red.</summary>
    [Fact]
    public async Task A_stale_If_Match_leaves_no_central_row_not_even_an_abandoned_one_and_no_history_row()
    {
        var s = new WarehouseScenario(_factory);
        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        var stale = ETag(role);
        await Expect(s.UpdateAsync(Id(role), new() { ["note"] = "ilk" }, stale), 200);

        // the race the If-Match pre-check cannot see: the record changes between the handler's read and its write
        _factory.AroundTransaction[s.Tenant] = async run =>
        {
            await s.Factory.Collection(Diten.InventoryService.Persistence.Warehouse.WarehouseCollections.LocationRoles).UpdateOneAsync(
                new BsonDocument("_id", Id(role)), new BsonDocument("$inc", new BsonDocument("Version", 1)));
            return await run();
        };
        try
        {
            Assert.Equal("WMS_CONFLICT", await ErrorCode(s.UpdateAsync(Id(role), new() { ["note"] = "ikinci" }, "W/\"2\""), 409));
        }
        finally
        {
            _factory.AroundTransaction.TryRemove(s.Tenant, out _);
        }

        Assert.Equal("WMS_CONFLICT", await ErrorCode(s.UpdateAsync(Id(role), new() { ["note"] = "üçüncü" }, stale), 409));
        Assert.Single(s.CentralRows(Update));                       // the first, successful update only
        Assert.DoesNotContain(s.CentralRows(Update), r => r["Status"].ToString() == "Abandoned");
        Assert.Equal(2, s.Events(Id(role)).Count);
    }
}
