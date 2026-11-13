using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — AC-59 (pack §4.4, §8.12, §12; WMS draft §3.6), measured WITHOUT tasks (inspection 02 F-4): storage-condition tags are created,
/// edited and deleted on a node's record; at most one temperature code (400 <c>WMS_STORAGE_CONDITION_INVALID</c> — K3 sabotage (ai)); the
/// effective set of a node is the nearest tagged node of its LOCATION path, answered as <c>effectiveStorageConditions</c> with the "üst
/// düğümden" source (K3 sabotage (ah): read only the node's own tag → red). The in-process read DL-2 uses for a bare bin is measured too.
/// </summary>
public sealed class WarehouseStorageConditionTagTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public WarehouseStorageConditionTagTests(WarehouseApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Tags_are_created_edited_and_deleted_on_the_nodes_record()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);

        var created = await s.CreatedAsync(zone.Id, conditions: ["PROTECT_FROM_LIGHT", "COOL_2_8"]);
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_LIGHT"], Strings(created.GetProperty("acceptedStorageConditions")));
        var own = created.GetProperty("effectiveStorageConditions");
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_LIGHT"], Strings(own.GetProperty("codes")));
        Assert.False(own.TryGetProperty("inheritedFromLocationId", out _), "the node's own tags are not inherited");

        var edited = await Expect(s.UpdateAsync(Id(created), new() { ["acceptedStorageConditions"] = new[] { "FROZEN_BELOW_MINUS_20" } }, ETag(created)), 200);
        Assert.Equal(["FROZEN_BELOW_MINUS_20"], Strings(edited.GetProperty("acceptedStorageConditions")));

        await Expect(s.DeleteAsync(Id(edited), ETag(edited)), 200);
        Assert.Equal("WMS_LOCATION_ROLE_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/location-roles/{Id(edited)}"), 404));
    }

    /// <summary>K3 sabotage (ai): allow two temperature codes → red.</summary>
    [Theory]
    [InlineData("COOL_2_8,FROZEN_BELOW_MINUS_20")]
    [InlineData("AMBIENT_15_25,COOL_2_8,PROTECT_FROM_LIGHT")]
    [InlineData("COOL_2_8,COOL_2_8")]
    [InlineData("COLD")]
    public async Task An_invalid_tag_set_is_400_on_create_and_on_edit(string list)
    {
        var tags = list.Split(',');
        var s = new WarehouseScenario(_factory);
        var created = await Expect(s.CreateAsync(s.Node(LocationType.Bin).Id, ["Receiving"], tags), 400);
        Assert.Equal("WMS_STORAGE_CONDITION_INVALID", created.GetProperty("error").GetProperty("code").GetString());

        var role = await s.CreatedAsync(s.Node(LocationType.Bin).Id, ["Receiving"]);
        Assert.Equal("WMS_STORAGE_CONDITION_INVALID", await ErrorCode(s.UpdateAsync(Id(role), new() { ["acceptedStorageConditions"] = tags }, ETag(role)), 400));
        Assert.Single(s.Stored(Diten.InventoryService.Persistence.Warehouse.WarehouseCollections.LocationRoles));
        Assert.Single(s.Events(Id(role))); // the refused edit wrote nothing
    }

    /// <summary>K3 sabotage (ah): drop the inheritance from the effective-set read (the node's own tag only) → red.</summary>
    [Fact]
    public async Task A_record_without_tags_shows_the_set_inherited_from_its_nearest_tagged_ancestor()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);
        var bin = s.Node(LocationType.Bin, zone);
        await s.CreatedAsync(zone.Id, conditions: ["COOL_2_8"]);

        var receiving = await s.CreatedAsync(bin.Id, ["Receiving"]);
        var effective = receiving.GetProperty("effectiveStorageConditions");
        Assert.Equal(["COOL_2_8"], Strings(effective.GetProperty("codes")));
        Assert.Equal(zone.Id.ToString("D"), effective.GetProperty("inheritedFromLocationId").GetString());
        Assert.Equal(zone.Code, effective.GetProperty("inheritedFromLocationCode").GetString());

        // the list answers the same
        var listed = (await Expect(s.GetAsync("/api/warehouse/location-roles?role=Receiving"), 200)).GetProperty("items")[0];
        Assert.Equal(zone.Id.ToString("D"), listed.GetProperty("effectiveStorageConditions").GetProperty("inheritedFromLocationId").GetString());
    }

    [Fact]
    public async Task The_nearest_tagged_node_wins_and_a_deleted_tag_is_no_longer_inherited()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);
        var binInZone = s.Node(LocationType.Bin, zone);
        var binInStorage = s.Node(LocationType.Bin);
        var storageTag = await s.CreatedAsync(s.Inventory.StorageNode.Id, conditions: ["AMBIENT_15_25"]);
        var zoneTag = await s.CreatedAsync(zone.Id, conditions: ["COOL_2_8", "PROTECT_FROM_MOISTURE"]);

        var inZoneRole = await s.CreatedAsync(binInZone.Id, ["Receiving"]);
        var inZone = inZoneRole.GetProperty("effectiveStorageConditions");
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_MOISTURE"], Strings(inZone.GetProperty("codes")));
        Assert.Equal(zone.Id.ToString("D"), inZone.GetProperty("inheritedFromLocationId").GetString());

        var inStorage = await s.CreatedAsync(binInStorage.Id, ["Receiving"]);
        Assert.Equal(["AMBIENT_15_25"], Strings(inStorage.GetProperty("effectiveStorageConditions").GetProperty("codes")));
        Assert.Equal(s.Inventory.StorageNode.Id.ToString("D"),
            inStorage.GetProperty("effectiveStorageConditions").GetProperty("inheritedFromLocationId").GetString());

        // the zone's tag removed → its bin falls back to the storage location's
        await Expect(s.UpdateAsync(Id(zoneTag), new() { ["roles"] = new[] { "Receiving" }, ["acceptedStorageConditions"] = Array.Empty<string>() }, ETag(zoneTag)), 200);
        var after = (await Expect(s.GetAsync($"/api/warehouse/location-roles/{Id(inZoneRole)}"), 200)).GetProperty("effectiveStorageConditions");
        Assert.Equal(["AMBIENT_15_25"], Strings(after.GetProperty("codes")));

        // the storage location's record deleted → nothing on the path is tagged
        await Expect(s.DeleteAsync(Id(storageTag), ETag(storageTag)), 200);
        var none = await Expect(s.GetAsync($"/api/warehouse/location-roles/{Id(inStorage)}"), 200);
        Assert.False(none.TryGetProperty("effectiveStorageConditions", out _));
    }

    /// <summary>DL-2 reads a bare bin (no record of its own) through the same in-process view: inherited roles and conditions, "üst düğümden".</summary>
    [Fact]
    public async Task A_bare_bin_inherits_the_roles_and_the_conditions_of_its_ancestors()
    {
        var s = new WarehouseScenario(_factory);
        var zone = s.Node(LocationType.Zone);
        var bare = s.Node(LocationType.Bin, zone);
        await s.CreatedAsync(zone.Id, ["Receiving"], ["COOL_2_8"]);

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.LegalEntity, s.User);
        var view = (await scope.ServiceProvider.GetRequiredService<LocationRoleReads>().ViewAsync(bare.Id, CancellationToken.None))!;

        Assert.Equal([WarehouseRole.Receiving], view.EffectiveRoles);
        Assert.Equal(["COOL_2_8"], view.EffectiveConditions!.Codes);
        Assert.Equal(zone.Id, view.EffectiveConditions.SourceLocationId);
        Assert.True(view.EffectiveConditions.Inherited);

        // another company of the tenant does not see the node at all
        using var other = _factory.Services.CreateScope();
        other.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, Guid.NewGuid(), s.User);
        Assert.Null(await other.ServiceProvider.GetRequiredService<LocationRoleReads>().ViewAsync(bare.Id, CancellationToken.None));
    }
}
