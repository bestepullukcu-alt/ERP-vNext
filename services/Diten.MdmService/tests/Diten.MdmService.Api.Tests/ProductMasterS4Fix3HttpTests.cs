using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX3 (P-2026-10-10-FU04-S4-FIX3) — R1: an unreadable row keeps its place in the search's order, so
/// pages stay consistent. No readable row is lost from every page, a page past the last is 200-empty, and the 503 is
/// only for a page whose slice holds placeholders and nothing readable. totalCount counts matching records, an unreadable
/// one included (CT decision (b), draft.14). The skipped rows of a request are logged in one line.
/// </summary>
public sealed partial class ProductMasterS4HttpTests
{
    // Scenario A: 10 matching rows, 1 unreadable, pageSize 10. Page 1 shows the 9; page 2 (and past it) is 200-empty —
    // FIX2 answered 503 there, because its unreadable count was the whole window's.
    // The sabotage targets "the slice is cut over readable rows only" and "the 503 rule is the window's again".
    [Fact]
    public async Task S4_FIX3_R1_Past_the_last_page_is_200_empty_with_an_unreadable_row()
    {
        var items = await OrderedItemsAsync(10);
        await BreakItemAsync(items[3]);

        var first = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=10&page=1"),
            "searchStockableItems");
        Assert.Equal(items.Where((_, i) => i != 3).Select(x => x.ToString("D")), Ids(first));
        // CT decision (b): totalCount is the number of matching records — the unreadable one included.
        Assert.Equal(10, first.GetProperty("totalCount").GetInt64());
        foreach (var page in new[] { 2, 3 })
        {
            var past = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=10&page={page}"),
                "searchStockableItems");
            Assert.Empty(Ids(past));
            Assert.Equal(10, past.GetProperty("totalCount").GetInt64());
        }
    }

    // Scenario B: 11 rows in code order, the 5th unreadable. Page 1 shows 9 (the 5th's slot is the placeholder's), page 2
    // shows the 11th — in FIX2 the 11th row was on no page at all.
    [Fact]
    public async Task S4_FIX3_R1_No_readable_row_slides_out_of_every_page()
    {
        var items = await OrderedItemsAsync(11);
        await BreakItemAsync(items[4]);

        var first = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=10&page=1"),
            "searchStockableItems");
        var second = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=10&page=2"),
            "searchStockableItems");

        Assert.Equal(items.Take(10).Where((_, i) => i != 4).Select(x => x.ToString("D")), Ids(first));
        Assert.Equal([items[10].ToString("D")], Ids(second));
        Assert.Equal(11, second.GetProperty("totalCount").GetInt64());
    }

    // A page whose slice is only placeholders is still the 503 (a systematic failure is not an empty catalogue): page 2
    // of pageSize 2 over [ok, ok, broken, broken, ok] is 503; pages 1 and 3 answer.
    [Fact]
    public async Task S4_FIX3_R1_A_page_of_only_unreadable_rows_is_503_and_its_neighbours_answer()
    {
        var items = await OrderedItemsAsync(5);
        await BreakItemAsync(items[2]);
        await BreakItemAsync(items[3]);

        Assert.Equal([items[0].ToString("D"), items[1].ToString("D")], Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=1"), "searchStockableItems")));
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=2"), 503,
            "PRODUCT_MASTER_UNAVAILABLE", null);
        Assert.Equal([items[4].ToString("D")], Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?skuLevel=Item&pageSize=2&page=3"), "searchStockableItems")));
    }

    // GSKU / LSKU / Finished Good rows can be unreadable too (FIX2 measured the item only): each is skipped and logged,
    // the readable one of its level is listed, and a page of only the unreadable one is the 503.
    [Theory]
    [InlineData("Gsku")]
    [InlineData("Lsku")]
    [InlineData("FinishedGood")]
    public async Task S4_FIX3_R1_An_unreadable_product_row_is_skipped_at_every_level(string level)
    {
        var good = await SeedWorldAsync();
        var bad = await SeedWorldAsync();
        var (collection, id, field, value, goodId) = level switch
        {
            // Fields the match accepts and the reader cannot read: the GSKU's pack unit, the LSKU's market, the FG's flag.
            "Gsku" => ("mdm_gskus", bad.Gsku.Id, "PackUomCode", (BsonValue)42, good.Gsku.Id),
            "Lsku" => ("mdm_lskus", bad.Lsku.Id, "MarketCode", (BsonValue)42, good.Lsku.Id),
            _ => ("mdm_finished_goods", bad.FinishedGood.Id, "SerialControlled", (BsonValue)"yes", good.FinishedGood.Id)
        };
        await host.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument(field, value)));

        var all = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel={level}&pageSize=100"),
            "searchStockableItems");
        Assert.Equal([goodId.ToString("D")], Ids(all));
        Assert.Equal(2, all.GetProperty("totalCount").GetInt64());
        Assert.Contains(host.Logs.Entries, entry => entry.Message.Contains("PRODUCT_MASTER_SEARCH_ROW_UNREADABLE", StringComparison.Ordinal)
                                                    && entry.Message.Contains(id.ToString("D"), StringComparison.Ordinal));

        // pageSize 1: one page answers the readable row, the other is the unreadable one's — 503; past them 200-empty.
        var statuses = new List<int>();
        for (var page = 1; page <= 2; page++)
            statuses.Add((int)(await Reader().GetAsync($"{Base}/stockable-items?skuLevel={level}&pageSize=1&page={page}")).StatusCode);
        Assert.Equal([200, 503], statuses.Order());
        Assert.Empty(Ids(await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel={level}&pageSize=1&page=3"),
            "searchStockableItems")));
    }

    // Minor: one log line per request, with the count and at most 20 ids — not one line per row.
    // The sabotage target "a log line per row".
    [Fact]
    public async Task S4_FIX3_The_skipped_rows_of_a_request_are_one_log_line_with_at_most_20_ids()
    {
        var items = await OrderedItemsAsync(30);
        var broken = items.Take(25).ToArray();
        foreach (var id in broken) await BreakItemAsync(id);
        var before = host.Logs.Entries.Count;

        var page = await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item&pageSize=100"),
            "searchStockableItems");

        Assert.Equal(items.Skip(25).Select(x => x.ToString("D")), Ids(page));
        var lines = host.Logs.Entries.Skip(before)
            .Where(entry => broken.Any(id => entry.Message.Contains(id.ToString("D"), StringComparison.Ordinal)))
            .ToArray();
        var line = Assert.Single(lines);
        Assert.Contains("PRODUCT_MASTER_SEARCH_ROW_UNREADABLE: 25 row(s)", line.Message, StringComparison.Ordinal);
        Assert.Equal(20, broken.Count(id => line.Message.Contains(id.ToString("D"), StringComparison.Ordinal)));
    }

    // Items of this tenant with codes in a known order (the seed's codes are random; they are rewritten in order).
    private async Task<Guid[]> OrderedItemsAsync(int count)
    {
        var prefix = "IT-" + Random.Shared.Next(100_000, 999_999).ToString("D6");
        var ids = new Guid[count];
        for (var i = 0; i < count; i++)
        {
            ids[i] = (await SeedItemAsync($"Paged item {i:D2}")).Id;
            await host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(
                new BsonDocument("_id", new BsonBinaryData(ids[i], GuidRepresentation.Standard)),
                new BsonDocument("$set", new BsonDocument("CanonicalCode", $"{prefix}{i:D6}")));
        }
        return ids;
    }

    // The item's code stays readable (its place in the order); its name does not.
    private Task BreakItemAsync(Guid id) =>
        host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("Name", 42)));
}
