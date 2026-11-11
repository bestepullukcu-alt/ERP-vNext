using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — the pure rules (pack §4.4, §8.7, §8.12, §12, §22), on the production <see cref="LocationRoleRules"/> /
/// <see cref="WarehouseRecordCodeRules"/>. The API tests measure the same rules end to end; these pin the edges.
/// </summary>
public sealed class WarehouseRulesTests
{
    private static readonly Guid Bin = Guid.NewGuid();
    private static readonly Guid Zone = Guid.NewGuid();
    private static readonly Guid Storage = Guid.NewGuid();

    // ── storage-condition tags (AC-59, sabotage (ai)) ────────────────────────────────────────────────────

    [Theory]
    [InlineData(new string[0], ConditionTagProblem.None)]
    [InlineData(new[] { "COOL_2_8", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE" }, ConditionTagProblem.None)]
    [InlineData(new[] { "AMBIENT_15_25" }, ConditionTagProblem.None)]
    [InlineData(new[] { "COOL_2_8", "FROZEN_BELOW_MINUS_20" }, ConditionTagProblem.MoreThanOneTemperature)]
    [InlineData(new[] { "AMBIENT_15_25", "COOL_2_8" }, ConditionTagProblem.MoreThanOneTemperature)]
    [InlineData(new[] { "COOL_2_8", "COOL_2_8" }, ConditionTagProblem.Duplicate)]
    [InlineData(new[] { "cool_2_8" }, ConditionTagProblem.Unknown)]
    [InlineData(new[] { "DEEP_FROZEN" }, ConditionTagProblem.Unknown)]
    public void A_tag_set_holds_known_codes_once_and_at_most_one_temperature(string[] codes, ConditionTagProblem expected)
        => Assert.Equal(expected, LocationRoleRules.CheckConditionTags(codes));

    [Fact]
    public void The_tag_codes_are_the_five_product_master_codes_three_of_them_temperatures()
    {
        Assert.Equal(["AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE"], LocationRoleRules.KnownStorageConditions);
        Assert.Equal(["AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20"], LocationRoleRules.TemperatureConditions);
    }

    // ── roles (AC-17) ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Receiving_and_Staging_never_share_a_node_and_a_role_set_does_not_repeat()
    {
        Assert.Equal(RoleSetProblem.None, LocationRoleRules.CheckRoles([WarehouseRole.Receiving]));
        Assert.Equal(RoleSetProblem.None, LocationRoleRules.CheckRoles([]));
        Assert.Equal(RoleSetProblem.ReceivingWithStaging, LocationRoleRules.CheckRoles([WarehouseRole.Staging, WarehouseRole.Receiving]));
        Assert.Equal(RoleSetProblem.Duplicate, LocationRoleRules.CheckRoles([WarehouseRole.Receiving, WarehouseRole.Receiving]));
    }

    [Theory]
    [InlineData(LocationType.StorageLocation, true)]
    [InlineData(LocationType.Zone, true)]
    [InlineData(LocationType.Bin, true)]
    [InlineData(LocationType.Site, false)]
    [InlineData(LocationType.Warehouse, false)]
    public void Only_a_storage_location_zone_or_bin_takes_a_role(LocationType type, bool allowed)
        => Assert.Equal(allowed, LocationRoleRules.IsRoleLocationType(type));

    [Fact]
    public void A_role_conflicts_with_the_opposite_role_of_an_ancestor_or_of_a_node_below()
    {
        Assert.Equal(WarehouseRole.Staging, LocationRoleRules.InheritedConflict([WarehouseRole.Staging], [WarehouseRole.Receiving], []));
        Assert.Equal(WarehouseRole.Receiving, LocationRoleRules.InheritedConflict([WarehouseRole.Receiving], [], [WarehouseRole.Staging]));
        Assert.Null(LocationRoleRules.InheritedConflict([WarehouseRole.Receiving], [WarehouseRole.Receiving], [WarehouseRole.Receiving]));
        Assert.Null(LocationRoleRules.InheritedConflict([], [WarehouseRole.Receiving], [WarehouseRole.Staging]));
    }

    [Fact]
    public void A_node_holds_its_own_roles_and_every_ancestors()
    {
        var path = new[]
        {
            new LocationRoleNode(Bin, [], []), new LocationRoleNode(Zone, [WarehouseRole.Receiving], []), new LocationRoleNode(Storage, [], [])
        };
        Assert.Equal([WarehouseRole.Receiving], LocationRoleRules.EffectiveRoles(path));
    }

    // ── effective accepted set (AC-59, sabotage (ah)) ────────────────────────────────────────────────────

    [Fact]
    public void The_effective_set_is_the_nearest_tagged_node_of_the_path()
    {
        var path = new[]
        {
            new LocationRoleNode(Bin, [WarehouseRole.Receiving], []),
            new LocationRoleNode(Zone, [], ["COOL_2_8", "PROTECT_FROM_LIGHT"]),
            new LocationRoleNode(Storage, [], ["AMBIENT_15_25"])
        };

        var effective = LocationRoleRules.EffectiveConditions(path)!;
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_LIGHT"], effective.Codes);
        Assert.Equal(Zone, effective.SourceLocationId);
        Assert.True(effective.Inherited);
    }

    [Fact]
    public void A_node_with_its_own_tags_is_not_inherited_and_an_untagged_path_has_no_set()
    {
        var own = LocationRoleRules.EffectiveConditions([new LocationRoleNode(Bin, [], ["FROZEN_BELOW_MINUS_20"]), new LocationRoleNode(Zone, [], ["COOL_2_8"])])!;
        Assert.Equal(["FROZEN_BELOW_MINUS_20"], own.Codes);
        Assert.Equal(Bin, own.SourceLocationId);
        Assert.False(own.Inherited);

        Assert.Null(LocationRoleRules.EffectiveConditions([new LocationRoleNode(Bin, [WarehouseRole.Receiving], []), new LocationRoleNode(Zone, [], [])]));
    }

    // ── storage-condition fit (pack §8.12; consumed by DL-2 — the SkuReference seam feeds it) ───────────

    [Fact]
    public void Unknown_product_conditions_are_never_read_as_none()
    {
        Assert.Equal(StorageConditionFit.Unknown, LocationRoleRules.Fit(null, null));
        Assert.Equal(StorageConditionFit.Unknown, LocationRoleRules.Fit(null, new EffectiveStorageConditions(["COOL_2_8"], Zone, true)));
    }

    [Fact]
    public void An_unlabelled_place_takes_only_a_product_without_conditions_or_with_ambient_alone()
    {
        Assert.Equal(StorageConditionFit.Fits, LocationRoleRules.Fit([], null));
        Assert.Equal(StorageConditionFit.Fits, LocationRoleRules.Fit(["AMBIENT_15_25"], null));
        Assert.Equal(StorageConditionFit.Mismatch, LocationRoleRules.Fit(["COOL_2_8"], null));
        Assert.Equal(StorageConditionFit.Mismatch, LocationRoleRules.Fit(["PROTECT_FROM_LIGHT"], null));
    }

    [Fact]
    public void Every_product_condition_must_be_in_the_effective_set_an_unknown_code_included()
    {
        var cool = new EffectiveStorageConditions(["COOL_2_8", "PROTECT_FROM_LIGHT"], Zone, true);
        Assert.Equal(StorageConditionFit.Fits, LocationRoleRules.Fit(["COOL_2_8"], cool));
        Assert.Equal(StorageConditionFit.Fits, LocationRoleRules.Fit(["COOL_2_8", "PROTECT_FROM_LIGHT"], cool));
        Assert.Equal(StorageConditionFit.Mismatch, LocationRoleRules.Fit(["AMBIENT_15_25"], cool));
        Assert.Equal(StorageConditionFit.Mismatch, LocationRoleRules.Fit(["COOL_2_8", "PROTECT_FROM_MOISTURE"], cool));
        Assert.Equal(StorageConditionFit.Mismatch, LocationRoleRules.Fit(["COOL_2_8", "ULTRA_COLD_NEW"], cool));
    }

    // ── record codes (§8.7, §22, AC-16 format part) ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("GMG", true)]
    [InlineData("AB", true)]
    [InlineData("ABCDE", true)]
    [InlineData("gmg1", false)]
    [InlineData("G", false)]
    [InlineData("ABCDEF", false)]
    [InlineData("GMĞ", false)]
    [InlineData("GM1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_company_prefix_is_two_to_five_capital_ascii_letters(string? code, bool valid)
        => Assert.Equal(valid, WarehouseRecordCodeRules.IsValidCompanyCode(code));

    [Fact]
    public void A_number_has_at_least_six_digits_and_grows_past_999999()
    {
        Assert.Equal("GMG-PUT-000001", WarehouseRecordCodeRules.Format("GMG", "PUT", 1));
        Assert.Equal("GMG-PUT-999999", WarehouseRecordCodeRules.Format("GMG", "PUT", 999_999));
        Assert.Equal("GMG-PUT-1000000", WarehouseRecordCodeRules.Format("GMG", "PUT", 1_000_000));
        Assert.All(new[] { WarehouseRecordCodeRules.Format("AB", "PKG", 42) }, n => Assert.True(n.All(char.IsAscii)));
        Assert.Throws<ArgumentException>(() => WarehouseRecordCodeRules.Format("gmg", "PUT", 1));
        Assert.Throws<ArgumentException>(() => WarehouseRecordCodeRules.Format("GMG", "XYZ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WarehouseRecordCodeRules.Format("GMG", "PUT", 0));
    }
}
