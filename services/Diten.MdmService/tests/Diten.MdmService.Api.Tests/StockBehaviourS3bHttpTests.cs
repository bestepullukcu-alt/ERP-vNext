using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Tests.Shared;
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

        // S3b FIX2 (R-3): on an approved GSKU the days come with (or after) the shelf life.
        var first = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version,
            new { shelfLifeDays = 730, retestDays = 365, minRemainingShelfLifeDays = 180 });
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
            new { shelfLifeDays = 3650, retestDays = 0, minRemainingShelfLifeDays = 3650 });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // The sabotage target "the LSKU's retestDays does not come from the GSKU".
    [Fact]
    public async Task S3b_The_lsku_and_the_finished_good_read_retest_and_minimum_remaining_from_their_gsku()
    {
        var records = await SeedAsync();
        await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { shelfLifeDays = 730, retestDays = 365, minRemainingShelfLifeDays = 180 });
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
        var fill = await PutGskuAsync(records.Gsku.Id, legacy.Version, new { shelfLifeDays = 730, retestDays = 90 });
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

        // (S3b FIX2: a second field that needs no shelf life — on an approved GSKU the days now follow the shelf life.)
        var response = await PutGskuAsync(records.Gsku.Id, version, new { lotControlled = (bool?)null, shelfLifeDays = 365 });

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

    // ------------------------------------------------------------------------------------------- S3b FIX1

    // R2: the action list says what the gate will say. A flagless draft offers no SUBMIT but STOCK_FLAGS_REQUIRED (the
    // screen's reason); once the flags are set, SUBMIT. The sabotage target "SUBMIT visible on a flagless draft".
    [Fact]
    public async Task S3b_FIX1_A_flagless_gsku_draft_offers_the_reason_instead_of_submit()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());

        var before = await GskuActionsAsync(gsku);
        Assert.Contains(StockFlagGate.SubmitNeedsStockFlagsAction, before);
        Assert.DoesNotContain("SUBMIT", before);

        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version, new { lotControlled = true })).StatusCode);
        var after = await GskuActionsAsync(gsku);
        Assert.Contains("SUBMIT", after);
        Assert.DoesNotContain(StockFlagGate.SubmitNeedsStockFlagsAction, after);
    }

    [Fact]
    public async Task S3b_FIX1_A_flagless_lsku_draft_offers_the_reason_instead_of_submit_on_its_detail_and_in_the_list()
    {
        var records = await SeedAsync();
        var lsku = await LskuDraftAsync(records.Gsku.Id, "TR");

        foreach (var actions in new[] { await LskuActionsAsync(lsku), await LskuListActionsAsync(lsku) })
        {
            Assert.Contains(StockFlagGate.SubmitNeedsStockFlagsAction, actions);
            Assert.DoesNotContain("SUBMIT", actions);
        }

        // Its own serial flag alone is not enough: the GSKU's lot is part of the same rule.
        Assert.Equal(HttpStatusCode.OK, (await PutLskuAsync(lsku, (await LskuAsync(lsku)).Version, new { serialControlled = false })).StatusCode);
        Assert.DoesNotContain("SUBMIT", await LskuActionsAsync(lsku));
        Assert.Equal(HttpStatusCode.OK,
            (await PutGskuAsync(records.Gsku.Id, (await GskuAsync(records.Gsku.Id)).Version, new { lotControlled = false })).StatusCode);
        foreach (var actions in new[] { await LskuActionsAsync(lsku), await LskuListActionsAsync(lsku) })
        {
            Assert.Contains("SUBMIT", actions);
            Assert.DoesNotContain(StockFlagGate.SubmitNeedsStockFlagsAction, actions);
        }
    }

    // CT J3 (S3b FIX2): the LSKU's list asks the LSKU rule — its OWN serial flag and its GSKU's lot — both ways round.
    // The sabotage target "the action list asks only the parent's lot".
    [Theory]
    [InlineData("own-serial-missing")]
    [InlineData("parent-lot-missing")]
    public async Task S3b_FIX2_An_lsku_draft_missing_either_flag_offers_the_reason_not_submit(string missing)
    {
        var records = await SeedAsync();
        if (missing == "own-serial-missing")
            Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true })).StatusCode);
        var lsku = await LskuDraftAsync(records.Gsku.Id, "TR");
        if (missing == "parent-lot-missing")
            Assert.Equal(HttpStatusCode.OK, (await PutLskuAsync(lsku, (await LskuAsync(lsku)).Version, new { serialControlled = true })).StatusCode);

        foreach (var actions in new[] { await LskuActionsAsync(lsku), await LskuListActionsAsync(lsku) })
        {
            Assert.Contains(StockFlagGate.SubmitNeedsStockFlagsAction, actions);
            Assert.DoesNotContain("SUBMIT", actions);
        }
    }

    // R-2 (S3b FIX2): the days rule runs only when the request sets a day field. An older approved GSKU already outside
    // it (shelf life 100, retest 200 — possible before FIX1) still takes a flag's first fill; a day edit still meets it.
    // The sabotage target "the rule runs on untouched fields".
    [Fact]
    public async Task S3b_FIX2_An_older_record_outside_the_days_rule_still_takes_a_flag_fill()
    {
        var records = await SeedAsync();
        await host.Database.GetCollection<BsonDocument>("mdm_gskus").UpdateOneAsync(IdFilter(records.Gsku.Id),
            new BsonDocument("$set", new BsonDocument { { "ShelfLifeDays", 100 }, { "RetestDays", 200 } }));

        var flag = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true });
        Assert.True(flag.StatusCode == HttpStatusCode.OK, await flag.Content.ReadAsStringAsync());

        var day = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version + 1, new { minRemainingShelfLifeDays = 50 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, day.StatusCode);
        Assert.Equal(StockBehaviourRules.DaysInvalid, await FirstErrorAsync(day));
    }

    // R-3 (S3b FIX2, CT decision): on an approved GSKU without a shelf life, a first fill of the retest / minimum
    // remaining days is refused and names its fields — once filled they are locked, and a shelf life entered later
    // below them could never be set. With the shelf life (before, or in the same request) it goes; a draft is free.
    // The sabotage target "a first fill without a shelf life is accepted".
    [Theory]
    [InlineData(30, null, new[] { "retestDays" })]
    [InlineData(null, 30, new[] { "minRemainingShelfLifeDays" })]
    [InlineData(30, 30, new[] { "retestDays", "minRemainingShelfLifeDays" })]
    public async Task S3b_FIX2_An_approved_gsku_takes_the_days_only_after_its_shelf_life(int? retest, int? minimum, string[] fields)
    {
        var records = await SeedAsync();
        Assert.Null(records.Gsku.ShelfLifeDays);

        var refused = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version,
            new { retestDays = retest, minRemainingShelfLifeDays = minimum });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var errors = await ErrorsAsync(refused);
        Assert.Equal([StockBehaviourRules.DaysRequireShelfLife, .. fields], errors);
        Assert.Equal(records.Gsku.Version, (await GskuAsync(records.Gsku.Id)).Version);

        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { shelfLifeDays = 100 })).StatusCode);
        var after = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version + 1, new { retestDays = retest, minRemainingShelfLifeDays = minimum });
        Assert.True(after.StatusCode == HttpStatusCode.OK, await after.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task S3b_FIX2_A_gsku_draft_takes_the_days_without_a_shelf_life()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        var response = await PutGskuAsync(gsku, version, new { retestDays = 30, minRemainingShelfLifeDays = 10 });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // R3: the gate speaks only to someone who may see and submit the record. A flagless draft submitted by another
    // tenant, out of the subject's product scope, or without the submit key gets exactly what it got before S3b —
    // nothing in the answer says the record exists or what it lacks. The sabotage target "the gate before the scope".
    [Theory]
    [InlineData("foreign-tenant", HttpStatusCode.NotFound)]
    [InlineData("out-of-scope", HttpStatusCode.NotFound)]
    [InlineData("no-permission", HttpStatusCode.Forbidden)]
    public async Task S3b_FIX1_The_gsku_gate_answers_after_tenant_scope_and_permission(string door, HttpStatusCode expected)
    {
        var product = await SeedProductAsync();
        var (gsku, version) = await GskuDraftAsync(product);
        var token = await RefusedTokenAsync(door, "mdm.gskus.submit");

        var response = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version }, token);

        await AssertRefusedWithoutStockFlagsAsync(response, expected);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await GskuAsync(gsku)).LifecycleStatus);
    }

    [Theory]
    [InlineData("foreign-tenant", HttpStatusCode.NotFound)]
    [InlineData("out-of-scope", HttpStatusCode.NotFound)]
    [InlineData("no-permission", HttpStatusCode.Forbidden)]
    public async Task S3b_FIX1_The_lsku_gate_answers_after_tenant_scope_and_permission(string door, HttpStatusCode expected)
    {
        var records = await SeedAsync();
        var lsku = await LskuDraftAsync(records.Gsku.Id, "TR");
        var version = (await LskuAsync(lsku)).Version;
        var token = await RefusedTokenAsync(door, "mdm.lskus.submit");

        var response = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, token);

        await AssertRefusedWithoutStockFlagsAsync(response, expected);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await LskuAsync(lsku)).LifecycleStatus);
    }

    // Cross-field: with the shelf life known, the retest and the minimum remaining shelf life fit inside it — in one
    // request, and against a shelf life already on the record. The sabotage target "the cross-field rule removed".
    [Theory]
    [InlineData(100, 101, null)]
    [InlineData(100, null, 101)]
    [InlineData(100, 101, 101)]
    public async Task S3b_FIX1_Days_beyond_the_shelf_life_sent_with_it_are_422(int shelf, int? retest, int? minimum)
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());

        var response = await PutGskuAsync(gsku, version, new { shelfLifeDays = shelf, retestDays = retest, minRemainingShelfLifeDays = minimum });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(StockBehaviourRules.DaysInvalid, await FirstErrorAsync(response));
        Assert.Equal(version, (await GskuAsync(gsku)).Version);
    }

    [Fact]
    public async Task S3b_FIX1_Days_are_measured_against_the_shelf_life_already_on_the_record()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version, new { shelfLifeDays = 100 })).StatusCode);

        var beyond = await PutGskuAsync(gsku, version + 1, new { retestDays = 101 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, beyond.StatusCode);
        Assert.Equal(StockBehaviourRules.DaysInvalid, await FirstErrorAsync(beyond));

        var equal = await PutGskuAsync(gsku, version + 1, new { retestDays = 100, minRemainingShelfLifeDays = 100 });
        Assert.True(equal.StatusCode == HttpStatusCode.OK, await equal.Content.ReadAsStringAsync());
        // A shorter shelf life under the days already set is the same mistake from the other side.
        var shorter = await PutGskuAsync(gsku, version + 2, new { shelfLifeDays = 99 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, shorter.StatusCode);
        Assert.Equal(StockBehaviourRules.DaysInvalid, await FirstErrorAsync(shorter));
    }

    [Fact]
    public async Task S3b_FIX1_Without_a_shelf_life_the_days_only_keep_their_own_bounds()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        var response = await PutGskuAsync(gsku, version, new { retestDays = 3650, minRemainingShelfLifeDays = 3650 });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // R-06: on a draft the new fields stay free (O-8) — set, changed, set again.
    [Fact]
    public async Task S3b_FIX1_On_a_gsku_draft_retest_and_minimum_remaining_change_freely()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        foreach (var (retest, minimum) in new[] { (30, 10), (60, 20), (15, 5) })
        {
            var response = await PutGskuAsync(gsku, version++, new { retestDays = retest, minRemainingShelfLifeDays = minimum });
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var stored = await GskuAsync(gsku);
            Assert.Equal(retest, stored.RetestDays);
            Assert.Equal(minimum, stored.MinRemainingShelfLifeDays);
        }
    }

    // R-06: the fields belong to the GSKU only — the LSKU and Finished Good doors do not take them.
    [Theory]
    [InlineData("lsku", "retestDays")]
    [InlineData("lsku", "minRemainingShelfLifeDays")]
    [InlineData("finished-good", "retestDays")]
    [InlineData("finished-good", "minRemainingShelfLifeDays")]
    public async Task S3b_FIX1_The_lsku_and_finished_good_doors_refuse_the_gsku_days(string level, string field)
    {
        var records = await SeedAsync();
        var path = level == "lsku"
            ? $"/api/lskus/{records.Lsku.Id:D}/stock-behaviour"
            : $"/api/finished-goods/{records.FinishedGood.Id:D}/stock-behaviour";

        var response = await SendAsync(HttpMethod.Put, path, new Dictionary<string, object> { ["expectedVersion"] = 1, [field] = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var lsku = await host.Database.GetCollection<BsonDocument>("mdm_lskus").Find(IdFilter(records.Lsku.Id)).SingleAsync();
        Assert.False(lsku.Contains("RetestDays") || lsku.Contains("MinRemainingShelfLifeDays"));
        var finishedGood = await host.Database.GetCollection<BsonDocument>("mdm_finished_goods").Find(IdFilter(records.FinishedGood.Id)).SingleAsync();
        Assert.False(finishedGood.Contains("RetestDays") || finishedGood.Contains("MinRemainingShelfLifeDays"));
    }

    private async Task<string> RefusedTokenAsync(string door, string submitKey)
    {
        switch (door)
        {
            case "foreign-tenant":
                return host.Token(Guid.NewGuid(), _maker, MakerPermissions);
            case "out-of-scope":
                await EnforceScopeForAnotherProductAsync();
                return MakerToken();
            case "no-permission":
                return host.Token(_tenant, _maker, MakerPermissions.Where(x => x != submitKey).ToArray());
            default:
                throw new ArgumentOutOfRangeException(nameof(door), door, null);
        }
    }

    private static async Task AssertRefusedWithoutStockFlagsAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
        Assert.DoesNotContain("STOCK_FLAGS", text, StringComparison.Ordinal);
        Assert.DoesNotContain("lotControlled", text, StringComparison.Ordinal);
        Assert.DoesNotContain("serialControlled", text, StringComparison.Ordinal);
    }

    // The tenant enforces product scope; the subject's Legal Entity sees one other product only.
    private async Task EnforceScopeForAnotherProductAsync()
    {
        var legalEntity = Guid.NewGuid();
        await host.Database.GetCollection<LegalEntity>("mdm_legal_entities").InsertOneAsync(new LegalEntity
        {
            Id = legalEntity, TenantId = _tenant, Code = "LE-" + legalEntity.ToString("N")[..8], LegalName = "Scope",
            OperationalStatus = LegalEntityOperationalStatus.Active, IsDeleted = false
        });
        host.Scopes.Answer(_tenant, legalEntity);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, await SeedProductAsync(), Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private async Task<string[]> GskuActionsAsync(Guid gsku) =>
        Actions((await DataAsync(await host.Client(MakerToken()).GetAsync($"/api/gskus/{gsku:D}"))).GetProperty("availableActions"));

    private async Task<string[]> LskuActionsAsync(Guid lsku) =>
        Actions((await DataAsync(await host.Client(MakerToken()).GetAsync($"/api/lskus/{lsku:D}"))).GetProperty("availableActions"));

    private async Task<string[]> LskuListActionsAsync(Guid lsku)
    {
        var page = await DataAsync(await host.Client(MakerToken()).GetAsync("/api/lskus?pageSize=100"));
        return Actions(page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == lsku)
            .GetProperty("availableActions"));
    }

    private static string[] Actions(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data");
    }

    // ------------------------------------------------------------------------------------------- S5 (S3b remainder)

    // D-14 (CT decision): a draft may hold retest / minimum-remaining days without its shelf life, but is not SENT that
    // way (approved, they lock — R-3's trap). The gate and the action list ask the same rule.
    // The sabotage target "the D-14 rule removed".
    [Fact]
    public async Task S5_A_gsku_draft_with_days_but_no_shelf_life_is_not_sent()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version, new { lotControlled = true, retestDays = 30 })).StatusCode);

        var refused = await SubmitGskuAsync(gsku, version + 1);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var errors = await ErrorsAsync(refused);
        Assert.Equal([StockBehaviourRules.DaysRequireShelfLife, "retestDays"], errors);
        Assert.Contains(StockFlagGate.SubmitNeedsStockFlagsAction, await GskuActionsAsync(gsku));
        Assert.DoesNotContain("SUBMIT", await GskuActionsAsync(gsku));

        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version + 1, new { shelfLifeDays = 30 })).StatusCode);
        Assert.Contains("SUBMIT", await GskuActionsAsync(gsku));
        var accepted = await SubmitGskuAsync(gsku, version + 2);
        Assert.True(accepted.StatusCode == HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());
    }

    // D-14: a draft whose stored days exceed its shelf life (written before the rule) is not sent either; the refusal
    // names the days and the bound.
    [Fact]
    public async Task S5_A_gsku_draft_whose_days_exceed_its_shelf_life_is_not_sent()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version, new { lotControlled = true })).StatusCode);
        await host.Database.GetCollection<BsonDocument>("mdm_gskus").UpdateOneAsync(IdFilter(gsku),
            new BsonDocument("$set", new BsonDocument { { "ShelfLifeDays", 20 }, { "MinRemainingShelfLifeDays", 25 } }));

        var refused = await SubmitGskuAsync(gsku, version + 1);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var errors = await ErrorsAsync(refused);
        Assert.Equal([StockBehaviourRules.DaysInvalid, "minRemainingShelfLifeDays", "shelfLifeDaysAtLeast=25"], errors);
    }

    // D-15: the way out of an older approved record whose days were filled before its shelf life — a shelf life below
    // them is refused and the refusal says the bound; one at or above it goes.
    [Fact]
    public async Task S5_An_older_record_with_days_and_no_shelf_life_gets_its_shelf_life_at_the_named_bound()
    {
        var records = await SeedAsync();
        await host.Database.GetCollection<BsonDocument>("mdm_gskus").UpdateOneAsync(IdFilter(records.Gsku.Id),
            new BsonDocument("$set", new BsonDocument("RetestDays", 200)));

        var below = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { shelfLifeDays = 100 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, below.StatusCode);
        var errors = await ErrorsAsync(below);
        Assert.Equal([StockBehaviourRules.DaysInvalid, "retestDays", "shelfLifeDaysAtLeast=200"], errors);

        var enough = await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { shelfLifeDays = 365 });
        Assert.True(enough.StatusCode == HttpStatusCode.OK, await enough.Content.ReadAsStringAsync());
        Assert.Equal(365, (await GskuAsync(records.Gsku.Id)).ShelfLifeDays);
    }

    // S5 (CT decision 3): the detail answers carry the "Stock behaviour" section — values, sources, stockable and what is
    // missing, and the O-8 locks (none on a Draft; a filled scalar on an approved record).
    [Fact]
    public async Task S5_The_gsku_detail_carries_its_stock_behaviour_with_locks_and_what_is_missing()
    {
        var records = await SeedAsync();
        var before = await StockBehaviourAsync($"/api/gskus/{records.Gsku.Id:D}");
        Assert.False(before.GetProperty("stockable").GetBoolean());
        Assert.Equal(["baseUomCode", "shelfLifeDays", "storageConditionCodes", "lotControlled", "stockable"],
            Strings(before.GetProperty("missing")));
        Assert.Empty(Strings(before.GetProperty("lockedFields")));
        Assert.Equal(["uomConversions", "storageConditionCodes"], Strings(before.GetProperty("addOnlyFields")));
        Assert.Equal("C62", before.GetProperty("derivedPackConversion").GetProperty("toUom").GetString());

        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(records.Gsku.Id, records.Gsku.Version,
            new { lotControlled = true, shelfLifeDays = 365, retestDays = 30 })).StatusCode);
        var after = await StockBehaviourAsync($"/api/gskus/{records.Gsku.Id:D}");
        Assert.Equal(["shelfLifeDays", "retestDays", "lotControlled"], Strings(after.GetProperty("lockedFields")));
        Assert.Equal(365, after.GetProperty("shelfLifeDays").GetInt32());
        Assert.Equal("Own", after.GetProperty("shelfLifeDaysSource").GetString());
        Assert.True(after.GetProperty("editable").GetBoolean());
    }

    [Fact]
    public async Task S5_A_gsku_drafts_filled_stock_fields_are_not_locked()
    {
        var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
        Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version, new { lotControlled = true, shelfLifeDays = 365 })).StatusCode);

        var section = await StockBehaviourAsync($"/api/gskus/{gsku:D}");
        Assert.Empty(Strings(section.GetProperty("lockedFields")));
        Assert.Empty(Strings(section.GetProperty("addOnlyFields")));
    }

    // O-3: the LSKU's own field is the serial flag; the rest is its GSKU's, read now, with its source.
    [Fact]
    public async Task S5_The_lsku_detail_reads_the_gskus_stock_values_and_owns_only_the_serial_flag()
    {
        var records = await SeedAsync(completeGsku: true);
        await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true });
        var section = await StockBehaviourAsync($"/api/lskus/{records.Lsku.Id:D}");

        Assert.Equal("Gsku", section.GetProperty("lotControlledSource").GetString());
        Assert.True(section.GetProperty("lotControlled").GetBoolean());
        Assert.Equal(730, section.GetProperty("shelfLifeDays").GetInt32());
        Assert.Equal(JsonValueKind.Null, section.GetProperty("serialControlled").ValueKind);
        Assert.Equal(["serialControlled"], Strings(section.GetProperty("missing")));
        Assert.Empty(Strings(section.GetProperty("lockedFields")));

        await PutLskuAsync(records.Lsku.Id, records.Lsku.Version, new { serialControlled = false });
        section = await StockBehaviourAsync($"/api/lskus/{records.Lsku.Id:D}");
        Assert.True(section.GetProperty("stockable").GetBoolean());
        Assert.Equal(["serialControlled"], Strings(section.GetProperty("lockedFields")));
    }

    // O-14 / D-2: the Finished Good's base unit is its GSKU's; its shelf life is its own or, without one, the GSKU's.
    [Fact]
    public async Task S5_The_finished_good_detail_reads_its_base_unit_and_fallback_shelf_life_from_its_gsku()
    {
        var records = await SeedAsync(completeGsku: true);
        var section = await StockBehaviourAsync($"/api/finished-goods/{records.FinishedGood.Id:D}");

        Assert.Equal("C62", section.GetProperty("baseUomCode").GetString());
        Assert.Equal("Gsku", section.GetProperty("baseUomSource").GetString());
        Assert.Equal(730, section.GetProperty("shelfLifeDays").GetInt32());
        Assert.Equal("Gsku", section.GetProperty("shelfLifeDaysSource").GetString());
        Assert.Contains("gtin", Strings(section.GetProperty("missing")));
    }

    // The section rides the detail's own tenant / scope path: another tenant's read is still "none".
    [Fact]
    public async Task S5_The_stock_section_is_never_read_across_tenants()
    {
        var records = await SeedAsync();
        var foreign = host.Client(host.Token(Guid.NewGuid(), _maker, MakerPermissions));
        foreach (var path in new[] { $"/api/gskus/{records.Gsku.Id:D}", $"/api/lskus/{records.Lsku.Id:D}" })
        {
            var response = await foreign.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("stockBehaviour", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    private async Task<JsonElement> StockBehaviourAsync(string path)
    {
        var token = host.Token(_tenant, _maker, [.. MakerPermissions, "mdm.finished-goods.read"]);
        var response = await host.Client(token).GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("stockBehaviour");
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    // ------------------------------------------------------------------------------------------- S5-FIX1

    // (1) "Edit" is offered exactly where the write accepts it: editable:true ⇒ the write is not a 409, editable:false ⇒
    // it is. The status rule, a Finished Good's draft cancellation and an operation holding the record are read from the
    // write's own predicates. The sabotage targets "the FG cancellation condition removed" and "the busy condition
    // removed".
    [Theory]
    [InlineData("gsku-draft-free", true)]
    [InlineData("gsku-approved", true)]
    [InlineData("gsku-draft-busy", false)]
    [InlineData("lsku-draft-busy", false)]
    [InlineData("fg-draft-cancelled", false)]
    [InlineData("fg-draft-free", true)]
    // S5b-FIX2 (1): every hold the write reads, each alone.
    [InlineData("fg-draft-busy", false)]
    [InlineData("gsku-active-op", false)]
    [InlineData("gsku-retirement-op", false)]
    [InlineData("lsku-active-op", false)]
    [InlineData("lsku-retirement-request", false)]
    // S5b-FIX3 (2): the LSKU's free rows — an LSKU that is never held must be editable (a "held" that is always true
    // went unseen before).
    [InlineData("lsku-approved", true)]
    [InlineData("lsku-draft-free", true)]
    public async Task S5_FIX1_Editable_is_exactly_what_the_stock_write_accepts(string shape, bool expected)
    {
        var (path, write) = await StockShapeAsync(shape);

        var editable = (await StockBehaviourAsync(path)).GetProperty("editable").GetBoolean();
        var response = await write();

        Assert.Equal(expected, editable);
        var text = await response.Content.ReadAsStringAsync();
        if (editable) Assert.True(response.StatusCode != HttpStatusCode.Conflict, text);
        else
        {
            Assert.True(response.StatusCode == HttpStatusCode.Conflict, text);
            // FIX3 minor: the 409 is the hold's own code, never a concurrency conflict.
            Assert.Contains(shape == "fg-draft-cancelled" ? StockBehaviourStoreCodes.RecordNotEditable
                : StockBehaviourStoreCodes.BlockedByOperation, text, StringComparison.Ordinal);
            Assert.DoesNotContain(StockBehaviourStoreCodes.ConcurrencyConflict, text, StringComparison.Ordinal);
        }
    }

    // (1) the stated cases by name.
    [Fact]
    public async Task S5_FIX1_A_cancelled_finished_good_draft_is_not_editable()
    {
        var (path, _) = await StockShapeAsync("fg-draft-cancelled");
        Assert.False((await StockBehaviourAsync(path)).GetProperty("editable").GetBoolean());
    }

    [Theory]
    [InlineData("gsku-draft-busy")]
    [InlineData("lsku-draft-busy")]
    public async Task S5_FIX1_A_draft_held_by_its_submit_is_not_editable(string shape)
    {
        var (path, _) = await StockShapeAsync(shape);
        Assert.False((await StockBehaviourAsync(path)).GetProperty("editable").GetBoolean());
    }

    // Minor: a read refused by the legal-entity scope carries no stock block (only the cross-tenant case was tested).
    [Fact]
    public async Task S5_FIX1_A_read_refused_by_the_product_scope_carries_no_stock_block()
    {
        var records = await SeedAsync();
        await EnforceScopeForAnotherProductAsync();
        var token = host.Token(_tenant, _maker, [.. MakerPermissions, "mdm.finished-goods.read"]);
        foreach (var path in new[] { $"/api/gskus/{records.Gsku.Id:D}", $"/api/lskus/{records.Lsku.Id:D}",
                     $"/api/finished-goods/{records.FinishedGood.Id:D}" })
        {
            var response = await host.Client(token).GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("stockBehaviour", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    private async Task<(string Path, Func<Task<HttpResponseMessage>> Write)> StockShapeAsync(string shape)
    {
        switch (shape)
        {
            case "gsku-draft-free":
            {
                var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
                return ($"/api/gskus/{gsku:D}", () => PutGskuAsync(gsku, version, new { lotControlled = true }));
            }
            case "gsku-approved":
            {
                var records = await SeedAsync();
                return ($"/api/gskus/{records.Gsku.Id:D}",
                    () => PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true }));
            }
            case "gsku-draft-busy":
            {
                // A submit whose engine start never answered: the draft is held by it (its submit state is in flight).
                var (gsku, version) = await GskuDraftAsync(await SeedProductAsync());
                Assert.Equal(HttpStatusCode.OK, (await PutGskuAsync(gsku, version, new { lotControlled = true })).StatusCode);
                host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Timeout;
                await SubmitGskuAsync(gsku, version + 1);
                var now = await GskuAsync(gsku);
                Assert.Equal(ProductIdentityLifecycleStatus.Draft, now.LifecycleStatus);
                return ($"/api/gskus/{gsku:D}", () => PutGskuAsync(gsku, now.Version, new { retestDays = 5 }));
            }
            case "lsku-draft-busy":
            {
                var records = await SeedAsync(completeGsku: true);
                Assert.Equal(HttpStatusCode.OK,
                    (await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true })).StatusCode);
                var lsku = await LskuDraftAsync(records.Gsku.Id, "TR");
                Assert.Equal(HttpStatusCode.OK,
                    (await PutLskuAsync(lsku, (await LskuAsync(lsku)).Version, new { serialControlled = true })).StatusCode);
                host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Timeout;
                await SubmitLskuAsync(lsku, (await LskuAsync(lsku)).Version);
                var now = await LskuAsync(lsku);
                Assert.Equal(ProductIdentityLifecycleStatus.Draft, now.LifecycleStatus);
                return ($"/api/lskus/{lsku:D}", () => PutLskuAsync(lsku, now.Version, new { serialControlled = false }));
            }
            case "fg-draft-cancelled":
            {
                var records = await SeedAsync(completeGsku: true);
                await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").UpdateOneAsync(x => x.Id == records.FinishedGood.Id,
                    Builders<FinishedGood>.Update.Set(x => x.DraftCancellation, new FinishedGoodDraftCancellation
                    {
                        OperationId = Guid.NewGuid(), Fingerprint = "f", IntentId = Guid.NewGuid(), IdempotencyKey = "k",
                        EvidenceHash = "h", ReasonCode = "MAKER_CANCEL", RequesterSubjectId = _maker,
                        BaseVersion = records.FinishedGood.Version, RequestedAtUtc = DateTimeOffset.UtcNow
                    }));
                return ($"/api/finished-goods/{records.FinishedGood.Id:D}",
                    () => PutFinishedGoodAsync(records.FinishedGood.Id, records.FinishedGood.Version, new { lotControlled = true }));
            }
            case "fg-draft-free":
            {
                var records = await SeedAsync(completeGsku: true);
                return ($"/api/finished-goods/{records.FinishedGood.Id:D}",
                    () => PutFinishedGoodAsync(records.FinishedGood.Id, records.FinishedGood.Version, new { lotControlled = true }));
            }
            case "lsku-approved":
            {
                var records = await SeedAsync();
                return ($"/api/lskus/{records.Lsku.Id:D}",
                    () => PutLskuAsync(records.Lsku.Id, records.Lsku.Version, new { serialControlled = true }));
            }
            case "lsku-draft-free":
            {
                var records = await SeedAsync(completeGsku: true);
                Assert.Equal(HttpStatusCode.OK,
                    (await PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true })).StatusCode);
                var lsku = await LskuDraftAsync(records.Gsku.Id, "TR");
                var now = await LskuAsync(lsku);
                Assert.Equal(ProductIdentityLifecycleStatus.Draft, now.LifecycleStatus);
                return ($"/api/lskus/{lsku:D}", () => PutLskuAsync(lsku, now.Version, new { serialControlled = true }));
            }
            case "fg-draft-busy":
            {
                // An unfinished identity operation of the Finished Good (its submit is under way).
                var records = await SeedAsync(completeGsku: true);
                var operationId = Guid.NewGuid();
                await host.Database.GetCollection<FinishedGoodIdentityWorkflowOperation>(
                        Diten.MdmService.Persistence.Repositories.FinishedGoodIdentityWorkflowOperationRepository.CollectionName)
                    .InsertOneAsync(new FinishedGoodIdentityWorkflowOperation
                    {
                        Id = Guid.NewGuid(), TenantId = _tenant, OperationId = operationId,
                        FinishedGoodId = records.FinishedGood.Id, GskuId = records.Gsku.Id, MakerSubjectId = _maker,
                        ExpectedFinishedGoodVersion = records.FinishedGood.Version,
                        StartIdempotencyKey = "start-" + operationId.ToString("N"), OperationFingerprint = new string('c', 64),
                        Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted
                    });
                return ($"/api/finished-goods/{records.FinishedGood.Id:D}",
                    () => PutFinishedGoodAsync(records.FinishedGood.Id, records.FinishedGood.Version, new { lotControlled = true }));
            }
            case "gsku-active-op":
            case "gsku-retirement-op":
            {
                var records = await SeedAsync();
                var update = shape == "gsku-active-op"
                    ? Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, new GskuActiveLifecycleOperationBinding(
                        GskuLifecycleOperationKind.Correction, Guid.NewGuid(), records.Gsku.Version))
                    : Builders<Gsku>.Update.Set(x => x.RetirementOperationId, Guid.NewGuid());
                await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == records.Gsku.Id, update);
                return ($"/api/gskus/{records.Gsku.Id:D}",
                    () => PutGskuAsync(records.Gsku.Id, records.Gsku.Version, new { lotControlled = true }));
            }
            case "lsku-active-op":
            {
                var records = await SeedAsync();
                await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == records.Lsku.Id,
                    Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation, new LskuActiveLifecycleOperationBinding(
                        LskuLifecycleOperationKind.Retirement, Guid.NewGuid(), records.Lsku.Version)));
                return ($"/api/lskus/{records.Lsku.Id:D}",
                    () => PutLskuAsync(records.Lsku.Id, records.Lsku.Version, new { serialControlled = true }));
            }
            case "lsku-retirement-request":
            {
                // An active retirement request of the LSKU (decided, not yet completed — the lock already given back).
                var records = await SeedAsync();
                var operationId = Guid.NewGuid();
                await host.Database.GetCollection<LskuRetirementRequestOperation>(
                        Diten.MdmService.Persistence.Repositories.LskuRetirementRequestOperationRepository.CollectionName)
                    .InsertOneAsync(new LskuRetirementRequestOperation
                    {
                        Id = Guid.NewGuid(), TenantId = _tenant, OperationId = operationId, LskuId = records.Lsku.Id,
                        BaseLskuVersion = records.Lsku.Version, IsActive = true,
                        Checkpoint = LskuRetirementRequestCheckpoint.DecisionApplied,
                        StartIdempotencyKey = "start-" + operationId.ToString("N"), OperationFingerprint = new string('c', 64)
                    });
                return ($"/api/lskus/{records.Lsku.Id:D}",
                    () => PutLskuAsync(records.Lsku.Id, records.Lsku.Version, new { serialControlled = true }));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
        }
    }

    private Task<HttpResponseMessage> PutFinishedGoodAsync(Guid finishedGood, int version, object fields) =>
        SendAsync(HttpMethod.Put, $"/api/finished-goods/{finishedGood:D}/stock-behaviour", Merge(version, fields));

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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await host.Client(token ?? MakerToken()).SendAsync(request);
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

/// <summary>MOD-0290-FU04 S3b — the gate tests above meet UNSET flags: measured on their compiled class.</summary>
public sealed class StockBehaviourS3bGuardTests
{
    // The refusals above must meet UNSET flags: this class never takes the older flows' flag fill (S3bSubmitFlags).
    // S3b FIX1: measured on the compiled class, not its text — every method body of this class and of its compiler-made
    // nested types (async state machines, lambdas) is read instruction by instruction; no call, delegate or field read
    // may land on S3bSubmitFlags, whatever the source spells (an alias, a using static, a helper in between).
    // S3b FIX2: in a class of its own — the guard names S3bSubmitFlags (typeof), and the class it reads must not.
    [Fact]
    public void S3b_The_gate_tests_never_take_the_legacy_flag_fill()
    {
        // S3b FIX2: a typeof(S3bSubmitFlags) (the way to it by reflection) counts too; interface / virtual calls are
        // followed by the reader. A member found by a string at run time is the reader's stated limit.
        var reached = IlReferences.MembersReachedFrom(typeof(StockBehaviourS3bHttpTests))
            .Where(x => x == typeof(S3bSubmitFlags) || x.DeclaringType == typeof(S3bSubmitFlags)).Select(x => x.Name).ToArray();
        Assert.Empty(reached);
        // The reader is measured too: the older flows' own hook does reach the fill.
        Assert.Contains(IlReferences.MembersReachedFrom(typeof(GskuLifecycleHttpTests)),
            x => x.DeclaringType == typeof(S3bSubmitFlags));
    }
}
