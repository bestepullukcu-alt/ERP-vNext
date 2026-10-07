using Diten.MdmService.Application.Contracts.Inventory;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.Inventory;
using Diten.MdmService.Infrastructure.ReferenceData;
using Diten.MdmService.Persistence.Configurations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.MdmService.Application.Tests.ItemMaster;

/// <summary>
/// MOD-0290-FU04 S1 (P-2026-10-07-FU04-S1): the item's store-free rules — the §8.1 lifecycle table, the §4.4 pair rules,
/// the O-8 / O-9 / O-25 Stockable rule, the O-8 lock, the audit ordinal ranges (CT decision 3), forward compatibility
/// and the two fail-closed providers.
/// </summary>
public sealed class ItemMasterUnitTests
{
    // ------------------------------------------------------------------------------------------- §8.1 lifecycle table

    private static readonly (ItemLifecycleStatus From, ItemTransition Move)[] Allowed =
    [
        (ItemLifecycleStatus.Draft, ItemTransition.Update),
        (ItemLifecycleStatus.Active, ItemTransition.Update),
        (ItemLifecycleStatus.Deactivated, ItemTransition.Update),
        (ItemLifecycleStatus.Draft, ItemTransition.Submit),
        (ItemLifecycleStatus.PendingActivation, ItemTransition.Withdraw),
        (ItemLifecycleStatus.PendingActivation, ItemTransition.ApproveActivation),
        (ItemLifecycleStatus.PendingActivation, ItemTransition.RejectActivation),
        (ItemLifecycleStatus.Active, ItemTransition.Deactivate),
        (ItemLifecycleStatus.Deactivated, ItemTransition.RequestReactivation),
        (ItemLifecycleStatus.Deactivated, ItemTransition.Retire)
    ];

    [Fact]
    public void The_lifecycle_allows_exactly_the_packs_table_and_nothing_else()
    {
        foreach (var from in Enum.GetValues<ItemLifecycleStatus>())
        foreach (var move in Enum.GetValues<ItemTransition>())
            Assert.True(ItemLifecycle.Allows(from, move) == Allowed.Contains((from, move)), $"{from} --{move}-->");
    }

    [Fact]
    public void Retired_is_final_and_Active_never_retires_directly()
    {
        Assert.All(Enum.GetValues<ItemTransition>(), move => Assert.False(ItemLifecycle.Allows(ItemLifecycleStatus.Retired, move)));
        Assert.False(ItemLifecycle.Allows(ItemLifecycleStatus.Active, ItemTransition.Retire));
    }

    [Theory]
    [InlineData(ItemTransition.Withdraw, ItemActivationKind.Activation, ItemLifecycleStatus.Draft)]
    [InlineData(ItemTransition.Withdraw, ItemActivationKind.Reactivation, ItemLifecycleStatus.Deactivated)]
    [InlineData(ItemTransition.RejectActivation, ItemActivationKind.Activation, ItemLifecycleStatus.Draft)]
    [InlineData(ItemTransition.RejectActivation, ItemActivationKind.Reactivation, ItemLifecycleStatus.Deactivated)]
    [InlineData(ItemTransition.ApproveActivation, ItemActivationKind.Reactivation, ItemLifecycleStatus.Active)]
    public void A_refused_or_withdrawn_reactivation_returns_to_Deactivated_a_first_activation_to_Draft(
        ItemTransition move, ItemActivationKind kind, ItemLifecycleStatus expected) =>
        Assert.Equal(expected, ItemLifecycle.Target(ItemLifecycleStatus.PendingActivation, move, kind));

    [Fact]
    public void A_move_the_table_refuses_has_no_target() =>
        Assert.Throws<InvalidOperationException>(() =>
            ItemLifecycle.Target(ItemLifecycleStatus.Active, ItemTransition.Retire, null));

    // ------------------------------------------------------------------------------------------- §4.4 pair rules

    [Theory]
    [InlineData("KGM", "GRM", "1000", "1", null)]
    [InlineData("KGM", "GRM", "0.001", "0.000001", null)]
    [InlineData("KGM", "KGM", "1", "1", "SAME_UOM")]
    [InlineData("KGM", "GRM", "1e3", "1", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("KGM", "GRM", "+1000", "1", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("KGM", "GRM", "0", "1", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("KGM", "GRM", "-1", "1", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("KGM", "GRM", "1", "0.000", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("KGM", "GRM", "1,5", "1", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("KGM", "GRM", ".5", "1", "FACTOR_NOT_A_POSITIVE_DECIMAL")]
    [InlineData("", "GRM", "1", "1", "UOM_REQUIRED")]
    public void A_conversion_is_a_positive_decimal_text_between_two_different_units(
        string from, string to, string numerator, string denominator, string? expected) =>
        Assert.Equal(expected, ItemRules.ConversionShapeFailure([Conversion(from, to, numerator, denominator)]));

    [Fact]
    public void A_unit_pair_is_published_once_in_either_direction() =>
        Assert.Equal("PAIR_REPEATED", ItemRules.ConversionShapeFailure(
            [Conversion("XBX", "C62", "10", "1"), Conversion("C62", "XBX", "1", "10")]));

    // ------------------------------------------------------------------------------------------- O-8 / O-9 / O-25

    [Theory]
    [InlineData(ItemMaterialType.ActiveIngredient, true, 0, null, null, true)]
    [InlineData(ItemMaterialType.ActiveIngredient, true, 1, null, null, true)]
    [InlineData(ItemMaterialType.ActiveIngredient, true, 1, 365, null, false)]
    [InlineData(ItemMaterialType.Excipient, true, 1, null, 180, false)]
    [InlineData(ItemMaterialType.Intermediate, true, 0, 365, null, true)]
    [InlineData(ItemMaterialType.PackagingPrimary, true, 0, null, null, false)]
    [InlineData(ItemMaterialType.PackagingSecondary, true, 0, null, null, false)]
    [InlineData(ItemMaterialType.RawMaterial, false, 0, null, null, false)]
    public void Stockable_needs_storage_and_a_shelf_life_or_retest_except_on_packaging(
        ItemMaterialType type, bool stockable, int storageCount, int? shelfLife, int? retest, bool incomplete) =>
        Assert.Equal(incomplete, ItemRules.StockableIncomplete(Values(type, stockable,
            Enumerable.Range(0, storageCount).Select(_ => "AMBIENT_15_25").ToList(), shelfLife, retest)));

    // ------------------------------------------------------------------------------------------- O-8 lock

    [Fact]
    public void On_an_active_item_an_empty_locked_field_is_filled_once_and_a_filled_one_never_changes()
    {
        var current = new Item
        {
            MaterialType = ItemMaterialType.RawMaterial, BaseUomCode = "KGM", LotControlled = true, Stockable = false,
            ShelfLifeDays = null, RetestDays = 180, StorageConditionCodes = []
        };
        var firstFill = Values(ItemMaterialType.RawMaterial, false, ["COOL_2_8"], 365, 180);
        Assert.Null(ItemRules.LockedFieldChanged(current, firstFill));
        Assert.Equal(nameof(Item.RetestDays), ItemRules.LockedFieldChanged(current,
            Values(ItemMaterialType.RawMaterial, false, [], null, 200)));
        Assert.Equal(nameof(Item.BaseUomCode), ItemRules.LockedFieldChanged(current,
            Values(ItemMaterialType.RawMaterial, false, [], null, 180) with { BaseUomCode = "GRM" }));
        Assert.Equal(nameof(Item.MaterialType), ItemRules.LockedFieldChanged(current,
            Values(ItemMaterialType.Excipient, false, [], null, 180)));
        Assert.Equal(nameof(Item.Stockable), ItemRules.LockedFieldChanged(current,
            Values(ItemMaterialType.RawMaterial, true, [], null, 180)));
        current.StorageConditionCodes = ["COOL_2_8"];
        Assert.Equal(nameof(Item.StorageConditionCodes), ItemRules.LockedFieldChanged(current,
            Values(ItemMaterialType.RawMaterial, false, ["AMBIENT_15_25"], null, 180)));
    }

    // ------------------------------------------------------------------------------------------- CT decision 3

    [Fact]
    public void Reserved_ranges_hold_no_value_outside_their_owner()
    {
        var values = Enum.GetValues<ProductAuditOperation>().Select(value => (int)value).ToArray();
        Assert.Equal(values.Length, values.Distinct().Count());
        // 87–99: the Finished Good lifecycle's (unused); 100–119: the Item's (S1 adds none — path b).
        Assert.DoesNotContain(values, value => value is >= 87 and <= 119);
    }

    // ------------------------------------------------------------------------------------------- §8.4 B forward compatibility

    [Fact]
    public void An_item_document_written_by_a_later_version_still_reads()
    {
        ItemMasterClassMaps.Register();
        Assert.True(BsonClassMap.LookupClassMap(typeof(Item)).IgnoreExtraElements);
        Assert.True(BsonClassMap.LookupClassMap(typeof(ItemUomConversion)).IgnoreExtraElements);
        var document = new BsonDocument
        {
            { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "Name", "Amoxicillin trihydrate" },
            { "LaterSliceField", "kept by a newer binary" },
            { "UomConversions", new BsonArray { new BsonDocument { { "FromUom", "KGM" }, { "ToUom", "GRM" },
                { "Numerator", "1000" }, { "Denominator", "1" }, { "Derived", true } } } }
        };
        var item = BsonSerializer.Deserialize<Item>(document);
        Assert.Equal("Amoxicillin trihydrate", item.Name);
        Assert.Single(item.UomConversions);
        // A legacy document without the later fields reads with their defaults (live: IsDeleted false).
        Assert.False(item.IsDeleted);
        Assert.Null(item.MinRemainingShelfLifeDays);
    }

    // ------------------------------------------------------------------------------------------- fail-closed providers

    [Fact]
    public async Task Without_the_item_list_provider_every_resolution_is_an_outage()
    {
        var result = await new UnavailableItemReferenceResolver().ResolveLatestAsync([new("item-uom", "KGM")]);
        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(UnavailableItemReferenceResolver.ProviderNotAvailable, result.FailureCode);
    }

    [Fact]
    public async Task Without_the_tenant_wide_stock_query_the_stock_check_is_unavailable()
    {
        var check = await new UnavailableInventoryStockAvailabilityClient().CheckNonZeroStockAsync(Guid.NewGuid(), "Item");
        Assert.Equal(InventoryStockCheckOutcome.Unavailable, check.Outcome);
        Assert.Equal(UnavailableInventoryStockAvailabilityClient.TenantWideQueryMissing, check.Cause);
    }

    private static ItemUomConversion Conversion(string from, string to, string numerator, string denominator) =>
        new() { FromUom = from, ToUom = to, Numerator = numerator, Denominator = denominator };

    private static ItemFieldValues Values(ItemMaterialType type, bool stockable, List<string> storage, int? shelfLife,
        int? retest) =>
        new("Item", null, type, "KGM", [], storage, true, stockable, shelfLife, retest, null);
}
