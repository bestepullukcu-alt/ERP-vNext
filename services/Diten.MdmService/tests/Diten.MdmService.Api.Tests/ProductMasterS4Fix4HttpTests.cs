using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX4 (P-2026-10-10-FU04-S4-FIX4) — the placeholder of an unreadable row stands where the store's sort
/// put it, in the four-source merge too: by its code's BSON type bracket (missing / null / number &lt; text &lt; other),
/// then its code. Pages stay the same rows every request sees.
/// </summary>
public sealed partial class ProductMasterS4HttpTests
{
    // The default search merges four sources; an unreadable item between a GSKU and an LSKU keeps its slot.
    // Codes in order: GSKU C1, broken item C2, LSKU C3, FG C4, item C5. pageSize 2 → [C1], [C3, C4], [C5].
    // The sabotage target "the placeholders are put last" (and per-source paging would break it too).
    [Fact]
    public async Task S4_FIX4_A_placeholder_keeps_its_slot_among_the_four_sources()
    {
        var world = await SeedWorldAsync();
        var broken = await SeedItemAsync("Broken among four");
        var prefix = "Q" + Random.Shared.Next(100_000, 999_999).ToString("D6") + "-";
        await SetCodeAsync("mdm_gskus", world.Gsku.Id, prefix + "C1");
        await SetCodeAsync("mdm_items", broken.Id, prefix + "C2");
        await SetCodeAsync("mdm_lskus", world.Lsku.Id, prefix + "C3");
        await SetCodeAsync("mdm_finished_goods", world.FinishedGood.Id, prefix + "C4");
        await SetCodeAsync("mdm_items", world.Item.Id, prefix + "C5");
        await BreakItemAsync(broken.Id);

        var pages = new List<string[]>();
        for (var page = 1; page <= 3; page++)
            pages.Add(Ids(await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?pageSize=2&page={page}"),
                "searchStockableItems")));

        Assert.Equal([world.Gsku.Id.ToString("D")], pages[0]);
        Assert.Equal([world.Lsku.Id.ToString("D"), world.FinishedGood.Id.ToString("D")], pages[1]);
        Assert.Equal([world.Item.Id.ToString("D")], pages[2]);
    }

    // A placeholder whose code is missing sorts before every text code; one whose code is an array of documents sorts
    // after them — as the store sorts. Codes: [missing], C1, C2, C3, [{…}]. pageSize 2 → [C1], [C2, C3], then a page of
    // only the array-coded placeholder: 503. The sabotage target "the bracket is dropped (ordinal again)".
    [Fact]
    public async Task S4_FIX4_Missing_and_array_codes_sort_where_the_store_puts_them()
    {
        var items = await OrderedItemsAsync(3);
        var missing = await SeedItemAsync("Code missing");
        var arrayCoded = await SeedItemAsync("Code is an array");
        await host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(IdFilter(missing.Id),
            new BsonDocument("$unset", new BsonDocument("CanonicalCode", "")));
        await host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(IdFilter(arrayCoded.Id),
            new BsonDocument("$set", new BsonDocument("CanonicalCode", new BsonArray { new BsonDocument("x", 1) })));

        Assert.Equal([items[0].ToString("D")], Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=1"), "searchStockableItems")));
        Assert.Equal([items[1].ToString("D"), items[2].ToString("D")], Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=2"), "searchStockableItems")));
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=3"), 503,
            "PRODUCT_MASTER_UNAVAILABLE", null);
    }

    // A window that ends right before a placeholder: the page answers in full and nothing is skipped or logged for it;
    // the next page meets the placeholder. Codes C1, C2, [broken C3], C4; pageSize 2.
    [Fact]
    public async Task S4_FIX4_A_window_ending_just_before_a_placeholder_answers_in_full()
    {
        var items = await OrderedItemsAsync(4);
        await BreakItemAsync(items[2]);
        var before = host.Logs.Entries.Count;

        Assert.Equal([items[0].ToString("D"), items[1].ToString("D")], Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=1"), "searchStockableItems")));
        Assert.DoesNotContain(host.Logs.Entries.Skip(before),
            entry => entry.Message.Contains(items[2].ToString("D"), StringComparison.Ordinal));

        Assert.Equal([items[3].ToString("D")], Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=2"), "searchStockableItems")));
        Assert.Contains(host.Logs.Entries.Skip(before),
            entry => entry.Message.Contains(items[2].ToString("D"), StringComparison.Ordinal));
    }

    private Task SetCodeAsync(string collection, Guid id, string code) =>
        host.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(IdFilter(id),
            new BsonDocument("$set", new BsonDocument("CanonicalCode", code)));

    private static BsonDocument IdFilter(Guid id) => new("_id", new BsonBinaryData(id, GuidRepresentation.Standard));
}
