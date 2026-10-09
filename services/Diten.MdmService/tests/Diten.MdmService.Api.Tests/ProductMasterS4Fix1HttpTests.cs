using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX1 (P-2026-10-09-FU04-S4-FIX1) — the review's findings, over HTTP on the host's throwaway mongod:
/// R3 a repeated id answered once, R4 the search in the store (one bounded window per source, measured by Mongo's own
/// profiler; text is text, not a pattern), R5 the 64 KB body, unreadable data as a coded 503, the input rules and the
/// §16.3 mapping of every stored value. B1 and the scope cases are in ProductMasterS4HttpTests.
/// </summary>
public sealed partial class ProductMasterS4HttpTests
{
    // ------------------------------------------------------------------------------------------- R3

    // The sabotage target "a repeated id is not de-duplicated".
    [Fact]
    public async Task S4_FIX1_R3_A_repeated_id_is_answered_once_where_it_first_appears()
    {
        var world = await SeedWorldAsync();
        var a = world.Lsku.Id.ToString("D");
        var x = Guid.NewGuid().ToString("D");

        var body = await OkAsync(await Reader().PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { a, a, x, x } })), "bulkReadSkus");

        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(a, items[0].GetProperty("skuId").GetString());
        // draft.10: every row says what it resolves to.
        Assert.Equal(world.Product.Id.ToString("D"), items[0].GetProperty("itemId").GetString());
        Assert.Equal("GlobalProduct", items[0].GetProperty("itemKind").GetString());
        Assert.Equal([x], Strings(body.GetProperty("notFound")));
    }

    // ------------------------------------------------------------------------------------------- R4

    // The sabotage target "the paging goes back to memory": with 5,000 stockable items the search asks the store for one
    // window per source — Mongo's own profiler counts the aggregations (by their comment) and measures each answer.
    [Fact]
    public async Task S4_FIX1_R4_The_search_asks_the_store_for_one_bounded_window_per_source()
    {
        await SeedWorldAsync();
        await host.Database.GetCollection<Item>("mdm_items").InsertManyAsync(
            Enumerable.Range(0, 5000).Select(index => BulkItem(index)));

        var (allSources, allPage) = await ProfiledAsync($"{Base}/stockable-items?pageSize=25");
        var (itemsOnly, _) = await ProfiledAsync($"{Base}/stockable-items?skuLevel=Item&page=2&pageSize=25");

        Assert.Equal(25, allPage.GetProperty("items").GetArrayLength());
        Assert.True(allPage.GetProperty("totalCount").GetInt64() >= 5004);
        Assert.Equal(4, allSources.Count);
        Assert.Single(itemsOnly);
        Assert.All(allSources.Concat(itemsOnly), entry =>
            Assert.True(entry.GetValue("responseLength", 0).ToInt64() < 64 * 1024,
                $"an aggregation answered {entry.GetValue("responseLength", 0)} bytes — more than one window"));
    }

    private async Task<(List<BsonDocument> Profile, JsonElement Page)> ProfiledAsync(string path)
    {
        var database = host.Database;
        var profileCollection = database.GetCollection<BsonDocument>("system.profile");
        // Only what THIS call profiled: entries after the newest one already there.
        var latest = await profileCollection.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(new BsonDocument("ts", -1)).Limit(1).FirstOrDefaultAsync();
        var since = latest?["ts"].ToUniversalTime() ?? DateTime.MinValue;
        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "profile", 2 },
            { "filter", new BsonDocument("command.comment", ProductMasterReadStore.SearchComment) }
        });
        JsonElement page;
        try
        {
            page = await OkAsync(await Reader().GetAsync(path), "searchStockableItems");
        }
        finally
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 0));
        }
        var profile = await profileCollection
            .Find(new BsonDocument
            {
                { "command.comment", ProductMasterReadStore.SearchComment },
                { "ts", new BsonDocument("$gt", since) }
            })
            .ToListAsync();
        return (profile, page);
    }

    private Item BulkItem(int index) => new()
    {
        TenantId = _tenant, CanonicalCode = $"IT-9{index:D11}", CodeReservationId = Guid.NewGuid(),
        CreationCommandId = "bulk-" + Guid.NewGuid().ToString("N"), Name = $"Bulk excipient {index:D5}",
        NameNormalized = $"BULK EXCIPIENT {index:D5}", NameKey = "EXCIPIENT|" + Guid.NewGuid().ToString("N"),
        MaterialType = ItemMaterialType.Excipient, BaseUomCode = "KGM", StorageConditionCodes = ["AMBIENT_15_25"],
        LotControlled = true, Stockable = true, ShelfLifeDays = 365, LifecycleStatus = ItemLifecycleStatus.Active
    };

    // q is text: a regular-expression character matches itself, never a pattern (no injection, no 500).
    [Fact]
    public async Task S4_FIX1_R4_q_is_text_not_a_pattern()
    {
        var literal = await SeedItemAsync("Glyceryl .* stearate");
        var other = await SeedItemAsync("Glyceryl monostearate");
        var client = Reader();

        var dotStar = Ids(await OkAsync(await client.GetAsync(
            $"{Base}/stockable-items?q={Uri.EscapeDataString(".*")}"), "searchStockableItems"));
        Assert.Equal([literal.Id.ToString("D")], dotStar);
        Assert.DoesNotContain(other.Id.ToString("D"), dotStar);
        foreach (var text in new[] { "(", "[", "\\", "$where", "^" })
            await OkAsync(await client.GetAsync($"{Base}/stockable-items?q={Uri.EscapeDataString(text)}"), "searchStockableItems");
    }

    // The window is bounded: page × pageSize ≤ 1,000 (proposed to the contract); past it 400 INVALID_PARAMETER.
    [Fact]
    public async Task S4_FIX1_R4_The_search_window_is_bounded()
    {
        await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?page=40&pageSize=25"), "searchStockableItems");
        await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?page=10&pageSize=100"), "searchStockableItems");
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?page=41&pageSize=25"), 400, "INVALID_PARAMETER",
            "searchStockableItems");
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?page=1000000&pageSize=100"), 400,
            "INVALID_PARAMETER", "searchStockableItems");
    }

    // ------------------------------------------------------------------------------------------- R5

    // The sabotage target "the body limit is gone": a POST body past 64 KB is 413 REQUEST_TOO_LARGE in the contract's
    // error shape — declared (Content-Length) or streamed (chunked) — and a body under it is still answered.
    [Theory]
    [InlineData("skus/bulk-read", true)]
    [InlineData("skus/bulk-read", false)]
    [InlineData("validate", true)]
    [InlineData("validate", false)]
    public async Task S4_FIX1_R5_A_body_past_64KB_is_413(string path, bool declared)
    {
        var padding = new string(' ', ProductMasterContract.MaximumBodyBytes);
        var body = Encoding.UTF8.GetBytes("{\"skuIds\":[\"" + Guid.NewGuid().ToString("D") + "\"],\"refs\":[]" + padding + "}");
        HttpContent content = declared ? new ByteArrayContent(body) : new UnsizedContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await Reader().PostAsync($"{Base}/{path}", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(ProductMasterContractSchema.V11.ValidateAgainst("Error", document.RootElement));
        Assert.Equal("REQUEST_TOO_LARGE", document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task S4_FIX1_R5_A_body_just_under_64KB_is_answered()
    {
        var head = "{\"skuIds\":[\"" + Guid.NewGuid().ToString("D") + "\"]";
        var body = head + new string(' ', ProductMasterContract.MaximumBodyBytes - head.Length - 1) + "}";
        Assert.Equal(ProductMasterContract.MaximumBodyBytes, Encoding.UTF8.GetByteCount(body));
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        await OkAsync(await Reader().PostAsync($"{Base}/skus/bulk-read", content), "bulkReadSkus");
    }

    // A content without a length: the test server streams it like a chunked request.
    private sealed class UnsizedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            stream.WriteAsync(bytes, 0, bytes.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    // ------------------------------------------------------------------------------------------- unreadable data

    // The sabotage target "an unreadable document answers 500": a record the serializer cannot map is a coded 503
    // PRODUCT_MASTER_UNAVAILABLE.
    [Fact]
    public async Task S4_FIX1_An_unreadable_record_is_a_coded_503_not_a_500()
    {
        var world = await SeedWorldAsync();
        await host.Database.GetCollection<BsonDocument>("mdm_gskus").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(world.Gsku.Id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("PackQuantity", new BsonDocument("not", "a number"))));

        await ErrorAsync(await Reader().GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Gsku"), 503,
            "PRODUCT_MASTER_UNAVAILABLE", null);
    }

    // One unreadable row never fails the whole search: it is skipped and logged by name; the other rows answer.
    [Fact]
    public async Task S4_FIX1_An_unreadable_row_is_skipped_by_the_search_and_logged()
    {
        var world = await SeedWorldAsync();
        var broken = await SeedItemAsync("Broken legacy row");
        await host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(broken.Id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("Name", 42)));

        var ids = Ids(await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems"));

        Assert.Contains(world.Item.Id.ToString("D"), ids);
        Assert.DoesNotContain(broken.Id.ToString("D"), ids);
        Assert.Contains(host.Logs.Entries, entry => entry.Message.Contains("PRODUCT_MASTER_SEARCH_ROW_UNREADABLE", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------- inputs

    [Fact]
    public async Task S4_FIX1_A_bulk_read_without_skuIds_is_400_INVALID_PARAMETER()
    {
        await ErrorAsync(await Reader().PostAsync($"{Base}/skus/bulk-read", Json(new { ids = new[] { "x" } })), 400,
            "INVALID_PARAMETER", "bulkReadSkus");
    }

    [Fact]
    public async Task S4_FIX1_A_validate_with_no_ref_is_400()
    {
        await ErrorAsync(await Reader().PostAsync($"{Base}/validate", Json(new { refs = Array.Empty<object>() })), 400,
            "BATCH_LIMIT_EXCEEDED", "validateRefs");
    }

    // ------------------------------------------------------------------------------------------- deleted records (CT T4)

    // CT sabotage T4 survived S4: the item store's live filter could go and every test stayed green. A record written
    // as deleted (IsDeleted = true) answers EXACTLY what a random id answers, on every path — items, and the product
    // kinds (GP / GSKU / LSKU / Finished Good carry the same field; each repository's own live rule is measured here).
    [Theory]
    [InlineData("item")]
    [InlineData("gsku")]
    [InlineData("lsku")]
    [InlineData("finished-good")]
    [InlineData("global-product")]
    public async Task S4_FIX1_T4_A_deleted_record_answers_as_a_random_id(string kind)
    {
        var world = await SeedWorldAsync();
        var (collection, id) = kind switch
        {
            "item" => ("mdm_items", world.Item.Id),
            "gsku" => ("mdm_gskus", world.Gsku.Id),
            "lsku" => ("mdm_lskus", world.Lsku.Id),
            "finished-good" => ("mdm_finished_goods", world.FinishedGood.Id),
            _ => ("mdm_global_products", world.Product.Id)
        };
        await host.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("IsDeleted", true)));
        var client = Reader();
        var random = Guid.NewGuid();

        foreach (var path in new[]
                 {
                     "products/{0}", "items/{0}", "skus/{0}", "skus/{0}?level=Item", "skus/{0}?level=Gsku",
                     "skus/{0}?level=Lsku", "skus/{0}?level=FinishedGood", "skus/{0}/product", "skus/{0}/uom"
                 })
        {
            var deleted = await client.GetAsync($"{Base}/" + string.Format(path, id.ToString("D")));
            var unknown = await client.GetAsync($"{Base}/" + string.Format(path, random.ToString("D")));
            Assert.True(deleted.StatusCode == unknown.StatusCode, $"{path}: {(int)deleted.StatusCode} vs {(int)unknown.StatusCode}");
            Assert.Equal(await ErrorCodeAsync(unknown), await ErrorCodeAsync(deleted));
        }
        Assert.DoesNotContain(id.ToString("D"),
            Ids(await OkAsync(await client.GetAsync($"{Base}/stockable-items?pageSize=100"), "searchStockableItems")));
        var bulk = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { id.ToString("D") } })), "bulkReadSkus");
        Assert.Equal([id.ToString("D")], Strings(bulk.GetProperty("notFound")));
        var validate = await OkAsync(await client.PostAsync($"{Base}/validate", Json(new
        {
            refs = new object[] { new { itemId = id.ToString("D") }, new { skuId = id.ToString("D") } }
        })), "validateRefs");
        Assert.All(validate.GetProperty("results").EnumerateArray(), x => Assert.False(x.GetProperty("exists").GetBoolean()));
    }

    // ------------------------------------------------------------------------------------------- draft.11 first examples

    // Draft.11: each operation's FIRST example is the full v1.1 shape. The server's real answer carries the same field
    // names (top level, and the first row of a list) — names, not values. A difference is a finding, not a test to bend.
    [Fact]
    public async Task S4_FIX1_Every_answer_carries_the_field_names_of_its_operations_first_example()
    {
        var world = await SeedWorldAsync();
        var client = Reader();
        var answers = new (string Operation, HttpResponseMessage Response)[]
        {
            ("getProduct", await client.GetAsync($"{Base}/products/{world.Product.Id:D}")),
            ("getSku", await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}?level=Lsku")),
            ("resolveSkuProduct", await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}/product?level=Lsku")),
            ("getSkuUom", await client.GetAsync($"{Base}/skus/{world.Lsku.Id:D}/uom")),
            ("getMaterialItem", await client.GetAsync($"{Base}/items/{world.Item.Id:D}")),
            ("searchStockableItems", await client.GetAsync($"{Base}/stockable-items")),
            ("bulkReadSkus", await client.PostAsync($"{Base}/skus/bulk-read",
                Json(new { skuIds = new[] { world.Lsku.Id.ToString("D"), Guid.NewGuid().ToString("D") } }))),
            ("validateRefs", await client.PostAsync($"{Base}/validate",
                Json(new { refs = new object[] { new { itemId = world.Product.Id.ToString("D") } } })))
        };
        var findings = new List<string>();
        foreach (var (operation, response) in answers)
        {
            var answer = await OkAsync(response, operation);
            var example = FirstExample(operation);
            Compare(operation, example, answer, findings);
        }
        Assert.True(findings.Count == 0, string.Join("\n", findings));
    }

    private static JsonElement FirstExample(string operationId)
    {
        foreach (var path in ProductMasterContractSchema.V11.Root.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
            if (operation.Value.ValueKind == JsonValueKind.Object
                && operation.Value.TryGetProperty("operationId", out var id) && id.GetString() == operationId)
                return operation.Value.GetProperty("responses").GetProperty("200").GetProperty("content")
                    .GetProperty("application/json").GetProperty("examples").EnumerateObject().First().Value
                    .GetProperty("value");
        throw new InvalidOperationException(operationId);
    }

    private static void Compare(string at, JsonElement example, JsonElement answer, List<string> findings)
    {
        var expected = example.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var actual = answer.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(actual))
            findings.Add($"{at}: example only [{string.Join(",", expected.Except(actual))}] · answer only [{string.Join(",", actual.Except(expected))}]");
        foreach (var property in example.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0
                && property.Value[0].ValueKind == JsonValueKind.Object
                && answer.TryGetProperty(property.Name, out var rows) && rows.ValueKind == JsonValueKind.Array
                && rows.GetArrayLength() > 0)
                Compare($"{at}.{property.Name}[0]", property.Value[0], rows[0], findings);
    }

    // ------------------------------------------------------------------------------------------- §16.3, every stored value

    [Fact]
    public void S4_FIX1_Every_stored_status_maps_to_the_one_published_list()
    {
        var published = ProductMasterContractSchema.PublishedStatuses;
        foreach (var status in Enum.GetValues<ItemLifecycleStatus>())
        foreach (var kind in new ItemActivationKind?[] { null, ItemActivationKind.Activation, ItemActivationKind.Reactivation })
            Assert.Contains(ProductMasterContract.Status(status, kind), published);
        foreach (var status in Enum.GetValues<ProductIdentityLifecycleStatus>())
        {
            var answer = ProductMasterContract.Status(status);
            Assert.Contains(answer, published);
            Assert.NotEqual("Deactivated", answer);
        }
    }
}
