using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3b (P-2026-10-09-FU04-S3B) over HTTP on the host's throwaway mongod — the stock
/// team's two conditions on O-1:
/// <list type="bullet">
/// <item>R-06: the GSKU carries <c>retestDays</c> and <c>minRemainingShelfLifeDays</c> (S3's edit door, S3's O-8 rule);
/// the LSKU and the Finished Good read them from their GSKU; a GSKU document written before S3b reads them as null.</item>
/// <item>R-04: a draft is sent for approval only with the lot / serial flags of its level set; an older approved record
/// without them is published <c>stockable: false</c> until they are filled.</item>
/// </list>
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class StockBehaviourS3bHttpTests(MdmHttpHost host)
{
    private static readonly string[] MakerPermissions =
    [
        "mdm.gskus.read", "mdm.gskus.create", "mdm.gskus.update", "mdm.gskus.submit",
        "mdm.lskus.read", "mdm.lskus.create", "mdm.lskus.submit",
        "mdm.finished-goods.create", "mdm.product-master.read"
    ];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _maker = Guid.NewGuid();

    // ------------------------------------------------------------------------------------------- R-06 on the GSKU

    [Fact]
    public async Task S3b_The_gskus_retest_and_minimum_remaining_follow_the_o8_rule()
    {
        var records = await SeedAsync();

        var first = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { retestDays = 365, minRemainingShelfLifeDays = 180 });
        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var stored = await GskuAsync(records.Gsku.Id);
        Assert.Equal(365, stored.RetestDays);
        Assert.Equal(180, stored.MinRemainingShelfLifeDays);
        Assert.Equal(records.Gsku.Version + 1, stored.Version);

        // An approved GSKU: a filled value is locked (its correction is the critical-field flow, a later slice).
        var change = await PutGskuAsync(records.Gsku.Id, stored.Version, new { retestDays = 400 });
        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Equal(StockBehaviourStoreCodes.FieldLocked, await FirstErrorAsync(change));
        Assert.Equal(365, (await GskuAsync(records.Gsku.Id)).RetestDays);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(3651, null)]
    [InlineData(null, -1)]
    [InlineData(null, 3651)]
    public async Task S3b_Days_outside_0_to_3650_are_422(int? retest, int? minimum)
    {
        var records = await SeedAsync();

        var response = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version,
            new { retestDays = retest, minRemainingShelfLifeDays = minimum });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(StockBehaviourRules.DaysInvalid, await FirstErrorAsync(response));
        Assert.Equal(records.Gsku.Version, (await GskuAsync(records.Gsku.Id)).Version);
    }

    [Fact]
    public async Task S3b_The_bounds_0_and_3650_are_accepted()
    {
        var records = await SeedAsync();
        var response = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version,
            new { retestDays = 0, minRemainingShelfLifeDays = 3650 });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // The sabotage target "the LSKU's retestDays does not come from the GSKU".
    [Fact]
    public async Task S3b_The_lsku_and_the_finished_good_read_retest_and_minimum_remaining_from_their_gsku()
    {
        var records = await SeedAsync();
        await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { retestDays = 365, minRemainingShelfLifeDays = 180 });
        var client = host.Client(MakerToken());

        foreach (var (id, level) in new[] { (records.Gsku.Id, "Gsku"), (records.Lsku.Id, "Lsku"), (records.FinishedGood.Id, "FinishedGood") })
        {
            var sku = await JsonAsync(await client.GetAsync($"/api/product-master/skus/{id:D}?level={level}"));
            Assert.Equal(365, sku.GetProperty("retestDays").GetInt32());
            Assert.Equal(180, sku.GetProperty("minRemainingShelfLifeDays").GetInt32());
        }
        // Read at read time, never copied onto the children.
        var lsku = await host.Database.GetCollection<BsonDocument>("mdm_lskus").Find(IdFilter(records.Lsku.Id)).SingleAsync();
        Assert.False(lsku.Contains("RetestDays"));
    }

    // The sabotage target "an older GSKU document without the fields cannot be read": a document written before S3b has
    // neither field; it reads them as null, on the store and on the contract, and takes a first fill.
    [Fact]
    public async Task S3b_A_gsku_document_without_the_new_fields_reads_them_as_null_and_takes_a_first_fill()
    {
        var records = await SeedAsync();
        var raw = host.Database.GetCollection<BsonDocument>("mdm_gskus");
        await raw.UpdateOneAsync(IdFilter(records.Gsku.Id),
            new BsonDocument("$unset", new BsonDocument { { "RetestDays", "" }, { "MinRemainingShelfLifeDays", "" } }));
        var document = await raw.Find(IdFilter(records.Gsku.Id)).SingleAsync();
        Assert.False(document.Contains("RetestDays"));
        Assert.False(document.Contains("MinRemainingShelfLifeDays"));

        var legacy = await GskuAsync(records.Gsku.Id);
        Assert.Null(legacy.RetestDays);
        Assert.Null(legacy.MinRemainingShelfLifeDays);
        var sku = await JsonAsync(await host.Client(MakerToken()).GetAsync($"/api/product-master/skus/{records.Gsku.Id:D}?level=Gsku"));
        Assert.Equal(JsonValueKind.Null, sku.GetProperty("retestDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, sku.GetProperty("minRemainingShelfLifeDays").ValueKind);

        // A first fill lands on a field that is MISSING (not only null) — the store's Exists(false) | Eq(null) condition.
        var fill = await PutGskuAsync(records.Gsku.Id, legacy.Version, new { retestDays = 90 });
        Assert.True(fill.StatusCode == HttpStatusCode.OK, await fill.Content.ReadAsStringAsync());
        Assert.Equal(90, (await GskuAsync(records.Gsku.Id)).RetestDays);
    }

    // ------------------------------------------------------------------------------------------- R-04 approval gate

    // The sabotage target "a GSKU is sent for approval with an unset lot flag".
    [Fact]
    public async Task S3b_A_gsku_draft_without_its_lot_flag_is_not_sent_for_approval()
    {
        var product = await SeedProductAsync();
        var (gsku, version) = await GskuDraftAsync(product);

        var refused = await SubmitGskuAsync(gsku, version);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal([StockFlagGate.Required, "lotControlled"], await ErrorsAsync(refused));
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GskuAsync(gsku)).LifecycleStatus);
        Assert.Throws<InvalidOperationException>(() => host.Engine.Open("gsku", gsku.ToString("D")));

        var fill = await PutGskuAsync(gsku, version, new { lotControlled = false });
        Assert.True(fill.StatusCode == HttpStatusCode.OK, await fill.Content.ReadAsStringAsync());
        var accepted = await SubmitGskuAsync(gsku, version + 1);
        Assert.True(accepted.StatusCode == HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());
    }

    // The sabotage targets "an LSKU is sent for approval with its serial flag unset" and "… with its GSKU's lot unset".
    [Fact]
    public async Task S3b_An_lsku_draft_is_sent_for_approval_only_with_its_serial_and_its_gskus_lot_set()
    {
        var records = await SeedAsync();
        // The GSKU predates S3b: approved, no lot flag.
        Assert.Null(records.Gsku.LotControlled);
        var lsku = await LskuDraftAsync(records.Gsku.Id, "TR");

        var both = await SubmitLskuAsync(lsku, (await LskuAsync(lsku)).Version);
        Assert.Equal(HttpStatusCode.Conflict, both.StatusCode);
        Assert.Equal([StockFlagGate.Required, "serialControlled", "gsku.lotControlled"], await ErrorsAsync(both));

        var serial = await PutLskuAsync(lsku, (await LskuAsync(lsku)).Version, new { serialControlled = true });
        Assert.True(serial.StatusCode == HttpStatusCode.OK, await serial.Content.ReadAsStringAsync());
        var gskuLot = await SubmitLskuAsync(lsku, (await LskuAsync(lsku)).Version);
        Assert.Equal(HttpStatusCode.Conflict, gskuLot.StatusCode);
        Assert.Equal([StockFlagGate.Required, "gsku.lotControlled"], await ErrorsAsync(gskuLot));
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await LskuAsync(lsku)).LifecycleStatus);

        // The approved GSKU's lot is a first fill (no approval), then the LSKU goes.
        var lot = await PutGskuAsync(records.Gsku.Id, (await GskuAsync(records.Gsku.Id)).Version, new { lotControlled = true });
        Assert.True(lot.StatusCode == HttpStatusCode.OK, await lot.Content.ReadAsStringAsync());
        var accepted = await SubmitLskuAsync(lsku, (await LskuAsync(lsku)).Version);
        Assert.True(accepted.StatusCode == HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());
    }

    // The sabotage target "a Finished Good is approved with its serial flag unset". No Finished Good identity submit
    // exists yet (measured in S3b); the rule it will call is held here.
    [Theory]
    [InlineData(true, true, new string[0])]
    [InlineData(null, true, new[] { "lotControlled" })]
    [InlineData(true, null, new[] { "serialControlled" })]
    [InlineData(null, null, new[] { "lotControlled", "serialControlled" })]
    public void S3b_A_finished_good_needs_both_flags_for_approval(bool? lot, bool? serial, string[] missing)
    {
        var finishedGood = new FinishedGood { LotControlled = lot, SerialControlled = serial };
        Assert.Equal(missing, StockFlagGate.MissingForFinishedGood(finishedGood));
    }

    [Fact]
    public void S3b_A_gsku_needs_no_serial_flag()
    {
        Assert.Empty(StockFlagGate.MissingForGsku(new Gsku { LotControlled = false }));
    }

    // A flag cannot be emptied by the edit door: null is "not sent". With the record fenced while a request is open
    // (STOCK_BEHAVIOUR_EDIT_BLOCKED_BY_OPERATION, S3), the flags checked at submit are the flags the decision lands on.
    [Fact]
    public async Task S3b_The_edit_door_never_empties_a_flag()
    {
        var records = await SeedAsync();
        await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true });
        var version = (await GskuAsync(records.Gsku.Id)).Version;

        var response = await PutGskuAsync(records.Gsku.Id, version, new { lotControlled = (bool?)null, retestDays = 30 });

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.True((await GskuAsync(records.Gsku.Id)).LotControlled);
    }

    // ------------------------------------------------------------------------------------------- older approved records

    // The sabotage target "an older approved record without its flag is published stockable: true": it reads
    // stockable: false (its status untouched) until the flag is filled; then its own value comes back.
    [Fact]
    public async Task S3b_An_older_approved_record_without_its_flag_is_not_stockable_until_the_flag_is_filled()
    {
        var records = await SeedAsync(completeGsku: true);
        var client = host.Client(MakerToken());

        var gsku = await JsonAsync(await client.GetAsync($"/api/product-master/skus/{records.Gsku.Id:D}?level=Gsku"));
        var lsku = await JsonAsync(await client.GetAsync($"/api/product-master/skus/{records.Lsku.Id:D}?level=Lsku"));
        Assert.Equal("Active", gsku.GetProperty("lifecycleStatus").GetString());
        Assert.False(gsku.GetProperty("stockable").GetBoolean());
        Assert.False(lsku.GetProperty("stockable").GetBoolean());

        await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true });
        gsku = await JsonAsync(await client.GetAsync($"/api/product-master/skus/{records.Gsku.Id:D}?level=Gsku"));
        Assert.True(gsku.GetProperty("stockable").GetBoolean());
        // The LSKU still has no serial flag of its own.
        lsku = await JsonAsync(await client.GetAsync($"/api/product-master/skus/{records.Lsku.Id:D}?level=Lsku"));
        Assert.False(lsku.GetProperty("stockable").GetBoolean());

        await PutLskuAsync(records.Lsku.Id, records.Lsku.Version, new { serialControlled = false });
        lsku = await JsonAsync(await client.GetAsync($"/api/product-master/skus/{records.Lsku.Id:D}?level=Lsku"));
        Assert.True(lsku.GetProperty("stockable").GetBoolean());
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await GskuAsync(records.Gsku.Id)).LifecycleStatus);
    }

    // The refusals above must meet UNSET flags: this class never takes the older flows' flag fill (S3bSubmitFlags).
    [Fact]
    public void S3b_The_gate_tests_never_take_the_legacy_flag_fill()
    {
        var source = File.ReadAllText(Path.Combine(TestSourceDirectory(), "StockBehaviourS3bHttpTests.cs"));
        Assert.DoesNotContain("S3bSubmitFlags" + ".", source, StringComparison.Ordinal);
    }

    private static string TestSourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.GetDirectoryName(path)!;

    // ------------------------------------------------------------------------------------------- helpers

    private string MakerToken() => host.Token(_tenant, _maker, MakerPermissions);

    private sealed record Records(Gsku Gsku, Lsku Lsku, FinishedGood FinishedGood);

    // An approved GSKU written before S3b (no lot flag, no retest fields), its approved LSKU (no serial flag) and a draft
    // Finished Good. completeGsku: every other stock field filled and Stockable = true.
    private async Task<Records> SeedAsync(bool completeGsku = false)
    {
        var product = await SeedProductAsync();
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _tenant, GlobalProductId = product, RevisionIdentifier = "REV-001",
            CreationCommandId = "REV:" + Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenant, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = "GS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
            PackQuantity = 30m, PackUomCode = "C62", LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 2,
            BaseUomCode = completeGsku ? "C62" : null,
            ShelfLifeDays = completeGsku ? 730 : null,
            StorageConditionCodes = completeGsku ? ["AMBIENT_15_25"] : null,
            Stockable = completeGsku ? true : null
        };
        var lsku = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = _tenant, GskuId = gsku.Id,
            CanonicalCode = "LS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), MarketCode = "M" + Guid.NewGuid().ToString("N")[..6],
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 1
        };
        var finishedGood = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = _tenant, GskuId = gsku.Id,
            CanonicalCode = "FG-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.Draft, Version = 1
        };
        await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        await host.Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        await host.Database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
        await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").InsertOneAsync(finishedGood);
        return new(gsku, lsku, finishedGood);
    }

    private async Task<Guid> SeedProductAsync()
    {
        var id = Guid.NewGuid();
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = _tenant, CanonicalCode = $"GP-{id:N}"[..16],
            GlobalProductName = $"Product {id:N}", GlobalProductNameNormalized = $"PRODUCT {id:N}".ToUpperInvariant(),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        });
        return id;
    }

    private async Task<(Guid Id, int Version)> GskuDraftAsync(Guid product)
    {
        var created = await SendAsync(HttpMethod.Post, "/api/gskus/drafts",
            new { globalProductId = product, packQuantity = 1m, packUomCode = "C62" });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(created)).GetProperty("data");
        return (data.GetProperty("gskuId").GetGuid(), data.GetProperty("version").GetInt32());
    }

    private async Task<Guid> LskuDraftAsync(Guid gsku, string market)
    {
        var created = await SendAsync(HttpMethod.Post, "/api/lskus/drafts", new { gskuId = gsku, marketCode = market });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(created)).GetProperty("data").GetProperty("lskuId").GetGuid();
    }

    private Task<HttpResponseMessage> SubmitGskuAsync(Guid gsku, int version) =>
        SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version });

    private Task<HttpResponseMessage> SubmitLskuAsync(Guid lsku, int version) =>
        SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version });

    private Task<HttpResponseMessage> PutGskuAsync(Guid gsku, int version, object fields) =>
        SendAsync(HttpMethod.Put, $"/api/gskus/{gsku:D}/stock-behaviour", Merge(version, fields));

    private Task<HttpResponseMessage> PutLskuAsync(Guid lsku, int version, object fields) =>
        SendAsync(HttpMethod.Put, $"/api/lskus/{lsku:D}/stock-behaviour", Merge(version, fields));

    private static Dictionary<string, object?> Merge(int version, object fields)
    {
        var body = JsonSerializer.SerializeToNode(fields)!.AsObject()
            .ToDictionary(x => x.Key, x => (object?)x.Value?.DeepClone());
        body["expectedVersion"] = version;
        return body;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await host.Client(MakerToken()).SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static async Task<string[]> ErrorsAsync(HttpResponseMessage response) =>
        (await Envelope.ReadAsync(response)).GetProperty("errors").EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response) =>
        Envelope.FirstError(await Envelope.ReadAsync(response));

    private static BsonDocument IdFilter(Guid id) => new("_id", new BsonBinaryData(id, GuidRepresentation.Standard));

    private Task<Gsku> GskuAsync(Guid id) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();

    private Task<Lsku> LskuAsync(Guid id) =>
        host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();
}
