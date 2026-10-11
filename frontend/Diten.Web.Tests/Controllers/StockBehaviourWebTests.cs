using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.FinishedGoods;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Diten.Web.Views.MasterDataManagement.StockBehaviour;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0290-FU04 S5 (O-12) — the "Stock behaviour" section and its own edit offcanvas on the GSKU, Finished Good and
/// LSKU screens, measured on the production code: the real views with their layout (UAS-001 — nothing of it is in the
/// page without the level's stock-edit key, all of it with the key), the real proxy actions with MDM as a scripted
/// handler (only the level's changed fields reach MDM, with a "D" GUID Idempotency-Key derived per form attempt; a 422
/// stays a 422 with its sentence; STOCK_DAYS_INVALID says the shelf-life bound N), and the seven resx files.
/// </summary>
public sealed class StockBehaviourWebTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid Id = Guid.Parse("7b9a4d8e-0f4c-4a52-9f61-2f4b1c7d9e01");
    private static readonly Regex DGuid = new("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");

    // Every piece of the stock-behaviour surface a page can carry.
    private static readonly string[] Markers =
    [
        "id=\"sb-section\"", "id=\"offcanvasStockBehaviour\"", "id=\"formStockBehaviour\"", "id=\"stock-behaviour-l10n\"",
        "StockBehaviour/stock-behaviour.js"
    ];

    // ---- UAS-001 (CT decision, literal): not drawn at all without the key; all of it with the key ----

    [Theory]
    [InlineData("Gskus", "mdm.gskus.read", "mdm.gskus.update")]
    [InlineData("FinishedGoods", "mdm.finished-goods.read", "mdm.finished-goods.create")]
    [InlineData("Lskus", "mdm.lskus.read", "mdm.lskus.create")]
    public async Task Without_the_stock_edit_key_the_section_and_its_offcanvas_are_not_in_the_page(
        string folder, string readKey, string editKey)
    {
        var view = $"/Views/MasterDataManagement/{folder}/Index.cshtml";
        var reader = await LayoutRenderer.RenderAsync(factory, view, folder, readKey);
        var editor = await LayoutRenderer.RenderAsync(factory, view, folder, readKey, null, editKey);

        foreach (var marker in Markers)
        {
            Assert.DoesNotContain(marker, reader, StringComparison.Ordinal);
            Assert.Contains(marker, editor, StringComparison.Ordinal);
        }
        // The reader still has the register and its quick view — only the stock surface is absent (no placeholder).
        Assert.Contains("id=\"offcanvasDetailsPreview\"", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("data-sb-", reader, StringComparison.Ordinal);
        // The module is loaded before the page script that creates it.
        Assert.True(editor.IndexOf("StockBehaviour/stock-behaviour.js", StringComparison.Ordinal)
                    < editor.IndexOf($"{folder}/index.js", StringComparison.Ordinal));
    }

    // Each level's own form (pack §4.2, O-3, O-14): the LSKU only its serial flag, the Finished Good no base unit but
    // its GTIN, the GSKU no serial and no GTIN.
    [Theory]
    [InlineData("Gskus", "mdm.gskus.read", "mdm.gskus.update",
        "baseUomCode,uomConversions,shelfLifeDays,retestDays,minRemainingShelfLifeDays,storageConditionCodes,lotControlled,stockable")]
    [InlineData("FinishedGoods", "mdm.finished-goods.read", "mdm.finished-goods.create",
        "uomConversions,shelfLifeDays,storageConditionCodes,lotControlled,serialControlled,stockable,gtin")]
    [InlineData("Lskus", "mdm.lskus.read", "mdm.lskus.create", "serialControlled")]
    public async Task Each_level_offers_only_its_own_fields(string folder, string readKey, string editKey, string fields)
    {
        var page = await LayoutRenderer.RenderAsync(factory, $"/Views/MasterDataManagement/{folder}/Index.cshtml", folder,
            readKey, null, editKey);

        var inputs = Regex.Matches(page, "data-sb-input=\"([A-Za-z]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(fields.Split(','), inputs);
    }

    // S5b (BL-600) — sabotage target "the form falls back to a code input when the list is missing": the stock forms
    // offer units and storage conditions as selects (filled from the item options), never a typed-code box; each list
    // has its reason slot for "the lists cannot be loaded", and the labels by code reach the module in its words.
    [Theory]
    [InlineData("Gskus", "mdm.gskus.read", "mdm.gskus.update", "baseUomCode,storageConditionCodes")]
    [InlineData("FinishedGoods", "mdm.finished-goods.read", "mdm.finished-goods.create", "storageConditionCodes")]
    public async Task The_stock_forms_offer_units_and_storage_conditions_as_selects_never_as_code_inputs(
        string folder, string readKey, string editKey, string listFields)
    {
        var page = await LayoutRenderer.RenderAsync(factory, $"/Views/MasterDataManagement/{folder}/Index.cshtml", folder,
            readKey, null, editKey);

        foreach (var field in listFields.Split(','))
        {
            Assert.Matches($"<select[^>]*data-sb-input=\"{field}\"", page);
            Assert.DoesNotMatch($"<input[^>]*data-sb-input=\"{field}\"", page);
        }
        Assert.Matches("<select[^>]*multiple[^>]*data-sb-input=\"storageConditionCodes\"", page);
        Assert.Contains("data-sb-options-reason", page, StringComparison.Ordinal);
        Assert.Matches("\"[Ss]torage_COOL_2_8\":", page);
        Assert.Matches("\"[Uu]om_KGM\":", page);
        Assert.Matches("\"[Tt]emperatureOnlyOne\":", page);
    }

    // ---- the proxies ----

    [Fact]
    public async Task The_gsku_save_forwards_only_the_known_fields_normalised_with_a_d_guid_idempotency_key()
    {
        var mdm = new Capture(Success());
        var result = await Gsku(mdm, Form(("BaseUomCode", " c62 "),
            ("UomConversions", "[{\"fromUom\":\"xbx\",\"toUom\":\"C62\",\"numerator\":\"10\",\"denominator\":\"1\"}]"),
            ("ShelfLifeDays", "730"), ("StorageConditionCodes", "COOL_2_8, protect_from_light"), ("LotControlled", "true"),
            ("RetestDays", "0"))).SetStockBehaviour(Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var sent = Assert.Single(mdm.Sent);
        Assert.Equal(HttpMethod.Put, sent.Method);
        Assert.Equal($"{Gateway}/api/gskus/{Id:D}/stock-behaviour", sent.Url);
        Assert.Equal(
            "{\"expectedVersion\":3,\"baseUomCode\":\"C62\",\"uomConversions\":[{\"fromUom\":\"XBX\",\"toUom\":\"C62\"," +
            "\"numerator\":\"10\",\"denominator\":\"1\"}],\"shelfLifeDays\":730,\"storageConditionCodes\":[\"COOL_2_8\"," +
            "\"PROTECT_FROM_LIGHT\"],\"lotControlled\":true,\"retestDays\":0}",
            sent.Body);
        Assert.Matches(DGuid, sent.IdempotencyKey);
        Assert.Equal(36, sent.IdempotencyKey.Length);
    }

    // One form attempt repeated with the same values is the same request (MDM's replay); another attempt or other
    // values are another request.
    [Fact]
    public async Task The_idempotency_key_is_the_same_for_a_repeat_of_the_attempt_and_new_for_another()
    {
        var attempt = Guid.NewGuid().ToString("D");
        var actor = Guid.NewGuid().ToString("D");
        var tenant = Guid.NewGuid().ToString("D");
        async Task<string> KeyOf(string formAttempt, string shelfLife)
        {
            var mdm = new Capture(Success());
            var controller = Gsku(mdm, Form(("ShelfLifeDays", shelfLife)), formAttempt, actor, tenant);
            await controller.SetStockBehaviour(Id, CancellationToken.None);
            return Assert.Single(mdm.Sent).IdempotencyKey;
        }

        var first = await KeyOf(attempt, "730");
        Assert.Equal(first, await KeyOf(attempt, "730"));
        Assert.NotEqual(first, await KeyOf(Guid.NewGuid().ToString("D"), "730"));
        Assert.NotEqual(first, await KeyOf(attempt, "731"));
    }

    // K5 (CT sabotage survivor: the ACTOR part removed from the derivation, nothing went red). The key MDM receives is
    // bound to who asks, in which tenant, at which version: the same attempt with the same body by another actor, in
    // another tenant or at another expected version is another request — never a replay of someone else's.
    [Fact]
    public async Task The_idempotency_key_sent_to_mdm_differs_by_actor_tenant_and_expected_version()
    {
        var attempt = Guid.NewGuid().ToString("D");
        var actor = Guid.NewGuid().ToString("D");
        var tenant = Guid.NewGuid().ToString("D");
        async Task<string> KeyOf(string who, string where, string version)
        {
            var mdm = new Capture(Success());
            var form = Form(("ShelfLifeDays", "730"));
            form["ExpectedVersion"] = version;
            await Gsku(mdm, form, attempt, who, where).SetStockBehaviour(Id, CancellationToken.None);
            return Assert.Single(mdm.Sent).IdempotencyKey;
        }

        var first = await KeyOf(actor, tenant, "3");
        Assert.Matches(DGuid, first);
        Assert.Equal(first, await KeyOf(actor, tenant, "3"));
        Assert.NotEqual(first, await KeyOf(Guid.NewGuid().ToString("D"), tenant, "3"));
        Assert.NotEqual(first, await KeyOf(actor, Guid.NewGuid().ToString("D"), "3"));
        Assert.NotEqual(first, await KeyOf(actor, tenant, "4"));
    }

    // K5 extension (S5b-FIX2): the key is bound to WHICH record, at WHICH level, for WHICH action too. Each is varied
    // alone (same actor, tenant, attempt, version and body — the bodies are asserted equal) and the key MDM receives
    // changes. Sabotage target: recordId.ToString("D") removed from StockBehaviourWrite.OperationId.
    [Fact]
    public async Task The_idempotency_key_sent_to_mdm_differs_by_record_id_alone()
    {
        var attempt = Guid.NewGuid().ToString("D");
        var actor = Guid.NewGuid().ToString("D");
        var tenant = Guid.NewGuid().ToString("D");
        async Task<SentRequest> SentFor(Guid record)
        {
            var mdm = new Capture(Success());
            await Gsku(mdm, Form(("ShelfLifeDays", "730")), attempt, actor, tenant).SetStockBehaviour(record, CancellationToken.None);
            return Assert.Single(mdm.Sent);
        }

        var first = await SentFor(Id);
        var again = await SentFor(Id);
        var other = await SentFor(Guid.Parse("0d6c1e2f-3a4b-4c5d-8e6f-7a8b9c0d1e2f"));
        Assert.Equal(first.IdempotencyKey, again.IdempotencyKey);
        Assert.Equal(first.Body, other.Body);
        Assert.Matches(DGuid, other.IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, other.IdempotencyKey);
    }

    [Fact]
    public async Task The_idempotency_key_sent_to_mdm_differs_by_level_alone()
    {
        var attempt = Guid.NewGuid().ToString("D");
        var actor = Guid.NewGuid().ToString("D");
        var tenant = Guid.NewGuid().ToString("D");
        var lskuMdm = new Capture(Success());
        var finishedGoodMdm = new Capture(Success());

        await Lsku(lskuMdm, Form(("SerialControlled", "true")), "mdm.lskus.create", attempt, actor, tenant)
            .SetStockBehaviour(Id, CancellationToken.None);
        await FinishedGood(finishedGoodMdm, Form(("SerialControlled", "true")), "mdm.finished-goods.create", attempt, actor, tenant)
            .SetStockBehaviour(Id, CancellationToken.None);

        var lsku = Assert.Single(lskuMdm.Sent);
        var finishedGood = Assert.Single(finishedGoodMdm.Sent);
        Assert.Equal("{\"expectedVersion\":3,\"serialControlled\":true}", lsku.Body);
        Assert.Equal(lsku.Body, finishedGood.Body);
        Assert.NotEqual(lsku.IdempotencyKey, finishedGood.IdempotencyKey);
    }

    // The two Finished Good actions (stock-behaviour, gtin) never carry the same body, so the action alone cannot be
    // varied through the proxy: the key MDM receives for the GTIN request is measured, shown to be exactly the production
    // derivation for (finished-good, gtin), and the same derivation with only the action changed gives another key.
    [Fact]
    public async Task The_idempotency_key_sent_to_mdm_differs_by_action_alone()
    {
        var attempt = Guid.NewGuid();
        var actor = Guid.NewGuid().ToString("D");
        var tenant = Guid.NewGuid();
        var mdm = new Capture(Success());

        await FinishedGood(mdm, Form(("Gtin", "04012345678901")), "mdm.finished-goods.create", attempt.ToString("D"), actor,
            tenant.ToString("D")).AssignGtin(Id, CancellationToken.None);

        var sent = Assert.Single(mdm.Sent);
        var write = new Diten.Web.Services.MasterData.StockBehaviourWrite.Request(3, attempt, sent.Body);
        Assert.Equal(sent.IdempotencyKey, Diten.Web.Services.MasterData.StockBehaviourWrite
            .OperationId(tenant, actor, "finished-good", "gtin", Id, write).ToString("D"));
        Assert.NotEqual(sent.IdempotencyKey, Diten.Web.Services.MasterData.StockBehaviourWrite
            .OperationId(tenant, actor, "finished-good", "stock-behaviour", Id, write).ToString("D"));
    }

    // A form the level does not accept never reaches MDM: an unknown field (an identity field, the LSKU's own flag on a
    // GSKU), no change at all, a malformed value, no form attempt, and a caller without the stock-edit key.
    [Theory]
    [InlineData("PackQuantity", "2", 400)]
    [InlineData("SerialControlled", "true", 400)]
    [InlineData("LotControlled", "yes", 400)]
    [InlineData("BaseUomCode", "C 62", 400)]
    [InlineData("ShelfLifeDays", "-1", 400)]
    [InlineData("UomConversions", "[]", 400)]
    [InlineData("UomConversions", "[{\"fromUom\":\"XBX\",\"toUom\":\"C62\",\"numerator\":\"1e3\",\"denominator\":\"1\"}]", 400)]
    [InlineData("StorageConditionCodes", "COOL_2_8,COOL_2_8", 400)]
    public async Task A_form_the_gsku_does_not_accept_never_reaches_mdm(string field, string value, int status)
    {
        var result = await Gsku(new Unreachable(), Form((field, value))).SetStockBehaviour(Id, CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task No_change_no_attempt_or_no_key_never_reaches_mdm()
    {
        Assert.Equal(400, Status(await Gsku(new Unreachable(), Form()).SetStockBehaviour(Id, CancellationToken.None)));
        Assert.Equal(400, Status(await Gsku(new Unreachable(), Form(("ShelfLifeDays", "10")), formAttempt: "not-a-guid")
            .SetStockBehaviour(Id, CancellationToken.None)));
        Assert.Equal(403, Status(await Gsku(new Unreachable(), Form(("ShelfLifeDays", "10")), permission: "mdm.gskus.read")
            .SetStockBehaviour(Id, CancellationToken.None)));
        Assert.Equal(403, Status(await Lsku(new Unreachable(), Form(("SerialControlled", "true")), "mdm.lskus.read")
            .SetStockBehaviour(Id, CancellationToken.None)));
        Assert.Equal(403, Status(await FinishedGood(new Unreachable(), Form(("Gtin", "04012345678901")), "mdm.finished-goods.read")
            .AssignGtin(Id, CancellationToken.None)));
    }

    // O-3: the LSKU sends its serial flag and only that — and must send it.
    [Fact]
    public async Task The_lsku_save_sends_its_serial_flag_only_and_requires_it()
    {
        var mdm = new Capture(Success());
        var result = await Lsku(mdm, Form(("SerialControlled", "true"))).SetStockBehaviour(Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var sent = Assert.Single(mdm.Sent);
        Assert.Equal($"{Gateway}/api/lskus/{Id:D}/stock-behaviour", sent.Url);
        Assert.Equal("{\"expectedVersion\":3,\"serialControlled\":true}", sent.Body);
        Assert.Matches(DGuid, sent.IdempotencyKey);

        Assert.Equal(400, Status(await Lsku(new Unreachable(), Form(("LotControlled", "true")))
            .SetStockBehaviour(Id, CancellationToken.None)));
    }

    // O-14 / O-15: the Finished Good's stock fields (no base unit) and its GTIN, each its own request.
    [Fact]
    public async Task The_finished_good_sends_its_stock_fields_and_its_gtin_as_two_requests()
    {
        var mdm = new Capture(Success(), Success());
        var stock = await FinishedGood(mdm, Form(("ShelfLifeDays", "365"), ("SerialControlled", "false")))
            .SetStockBehaviour(Id, CancellationToken.None);
        var gtin = await FinishedGood(mdm, Form(("Gtin", "04012345678901"))).AssignGtin(Id, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(stock).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(gtin).StatusCode);
        Assert.Equal($"{Gateway}/api/finished-goods/{Id:D}/stock-behaviour", mdm.Sent[0].Url);
        Assert.Equal("{\"expectedVersion\":3,\"shelfLifeDays\":365,\"serialControlled\":false}", mdm.Sent[0].Body);
        Assert.Equal($"{Gateway}/api/finished-goods/{Id:D}/gtin", mdm.Sent[1].Url);
        Assert.Equal("{\"expectedVersion\":3,\"gtin\":\"04012345678901\"}", mdm.Sent[1].Body);
        Assert.All(mdm.Sent, sent => Assert.Matches(DGuid, sent.IdempotencyKey));
        Assert.NotEqual(mdm.Sent[0].IdempotencyKey, mdm.Sent[1].IdempotencyKey);

        // A base unit is the GSKU's (O-14): not a Finished Good field.
        Assert.Equal(400, Status(await FinishedGood(new Unreachable(), Form(("BaseUomCode", "C62")))
            .SetStockBehaviour(Id, CancellationToken.None)));
    }

    // D-17: a rule refusal (422) stays a 422 and is said as its sentence on every level — never a 502, never the code.
    // The sabotage target "422 → 502".
    [Fact]
    public async Task A_422_refusal_stays_a_422_with_its_sentence_on_every_level()
    {
        var gsku = Read(await Gsku(new Capture(Refusal(HttpStatusCode.UnprocessableEntity, "STOCK_BEHAVIOUR_INCOMPLETE")),
            Form(("Stockable", "true"))).SetStockBehaviour(Id, CancellationToken.None));
        var lsku = Read(await Lsku(new Capture(Refusal(HttpStatusCode.UnprocessableEntity, "STOCK_BEHAVIOUR_INCOMPLETE")),
            Form(("SerialControlled", "true"))).SetStockBehaviour(Id, CancellationToken.None));
        var fg = Read(await FinishedGood(new Capture(Refusal(HttpStatusCode.UnprocessableEntity, "FG_SHELF_LIFE_EXCEEDS_GSKU")),
            Form(("ShelfLifeDays", "999"))).SetStockBehaviour(Id, CancellationToken.None));
        var gtin = Read(await FinishedGood(new Capture(Refusal(HttpStatusCode.Conflict, "FG_GTIN_CONFLICT")),
            Form(("Gtin", "04012345678901"))).AssignGtin(Id, CancellationToken.None));

        Assert.Equal((422, "refusal:RefusalStockIncomplete"), (gsku.Status, Assert.Single(gsku.Errors)));
        Assert.Equal((422, "refusal:RefusalStockIncomplete"), (lsku.Status, Assert.Single(lsku.Errors)));
        Assert.Equal((422, "refusal:RefusalStockFgShelfLifeExceedsGsku"), (fg.Status, Assert.Single(fg.Errors)));
        Assert.Equal((409, "refusal:RefusalStockGtinConflict"), (gtin.Status, Assert.Single(gtin.Errors)));
        foreach (var answer in new[] { gsku, lsku, fg, gtin })
        {
            Assert.DoesNotContain("unmapped", answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("STOCK_", answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("FG_", answer.Body, StringComparison.Ordinal);
        }
    }

    // D-15: the days that exceed the shelf life and the bound it must reach — said with N, the way out of a record whose
    // days came first. Without the bound it is the 0–3650 sentence. The sabotage target "the bound dropped".
    [Theory]
    [InlineData(new[] { "STOCK_DAYS_INVALID", "retestDays", "shelfLifeDaysAtLeast=45" },
        "localized:StockDaysExceedShelfLife[localized:StockDayRetest|45]")]
    [InlineData(new[] { "STOCK_DAYS_INVALID", "retestDays", "minRemainingShelfLifeDays", "shelfLifeDaysAtLeast=120" },
        "localized:StockDaysExceedShelfLife[localized:StockDayRetest + localized:StockDayMinRemaining|120]")]
    [InlineData(new[] { "STOCK_DAYS_INVALID" }, "refusal:RefusalStockDaysInvalid")]
    public async Task Stock_days_invalid_says_the_shelf_life_must_be_at_least_n_days(string[] errors, string sentence)
    {
        var answer = Read(await Gsku(new Capture(Refusal(HttpStatusCode.UnprocessableEntity, errors)),
            Form(("RetestDays", "45"))).SetStockBehaviour(Id, CancellationToken.None));

        Assert.Equal(422, answer.Status);
        Assert.Equal(sentence, Assert.Single(answer.Errors));
        Assert.DoesNotContain("shelfLifeDaysAtLeast", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("unmapped", answer.Body, StringComparison.Ordinal);
    }

    // A code with no sentence on the LSKU / Finished Good stock write: the status's sentence, flagged, the raw code never.
    [Fact]
    public async Task An_unmapped_code_on_the_lsku_stock_write_is_the_status_sentence_flagged()
    {
        var answer = Read(await Lsku(new Capture(Refusal(HttpStatusCode.Conflict, "SOMETHING_NEW_HERE")),
            Form(("SerialControlled", "true"))).SetStockBehaviour(Id, CancellationToken.None));

        Assert.Equal((409, "ErrorConflict"), (answer.Status, Assert.Single(answer.Errors)));
        Assert.Contains("\"unmapped\":true", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("SOMETHING_NEW_HERE", answer.Body, StringComparison.Ordinal);
    }

    // ---- seven languages ----

    [Fact]
    public void The_stock_behaviour_words_are_in_all_seven_languages()
    {
        var english = Resx("StockBehaviour", "StockBehaviourText", "en");
        Assert.All(StockBehaviourText.ScriptKeys, key => Assert.True(english.ContainsKey(key), $"{key} missing in en"));
        foreach (var language in Languages)
        {
            var file = Resx("StockBehaviour", "StockBehaviourText", language);
            Assert.True(english.Keys.ToHashSet().SetEquals(file.Keys), $"StockBehaviourText.{language}.resx differs from en");
            Assert.All(file, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{language}:{entry.Key} is empty"));
            var gsku = Resx("Gskus", "GskusIndex", language);
            Assert.True(gsku.TryGetValue("StockDaysExceedShelfLife", out var days)
                        && days.Contains("{0}", StringComparison.Ordinal) && days.Contains("{1}", StringComparison.Ordinal),
                $"StockDaysExceedShelfLife missing or without {{0}} / {{1}} in {language}");
        }
    }

    // S5b-FIX2 label drift guard: every unit (item-uom) and storage condition (storage-condition) Platform's contract
    // fixture enumerates has its label (Uom_<code> / Storage_<code>) in all seven StockBehaviourText resx files — the
    // ones _ReferenceOptionLabels.cshtml reads. Read from Platform's own fixture (read-only), not a copy of its codes.
    // INTENTIONALLY RED when ITEM-UNIVERSAL-V2 (IU, CodeStandard, item-uom's new version) merges into that fixture,
    // until the new codes' labels are added here in seven languages (and to StockBehaviourText.UomCodes).
    [Fact]
    public void Every_unit_and_storage_condition_of_platforms_contract_has_its_label_in_all_seven_languages()
    {
        var fixture = Path.Combine(RepoRoot(), "services", "Diten.Platform", "tests", "Diten.Platform.Application.Tests",
            "BusinessReferenceData", "Fixtures", "item-universal-enumerate.contract.json");
        using var contract = JsonDocument.Parse(File.ReadAllText(fixture));
        string[] CodesOf(string set) => contract.RootElement.GetProperty("response").GetProperty("data").GetProperty("sets")
            .EnumerateArray().Single(s => s.GetProperty("set_code").GetString() == set)
            .GetProperty("values").EnumerateArray().Select(v => v.GetProperty("value_code").GetString()!).ToArray();
        var expected = CodesOf("item-uom").Select(code => "Uom_" + code)
            .Concat(CodesOf("storage-condition").Select(code => "Storage_" + code)).ToArray();
        Assert.True(expected.Length >= 17, $"Only {expected.Length} codes read — the fixture's shape changed.");

        foreach (var language in Languages)
        {
            var file = Resx("StockBehaviour", "StockBehaviourText", language);
            var missing = expected.Where(key => !file.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)).ToArray();
            Assert.True(missing.Length == 0, $"StockBehaviourText.{language}.resx misses: {string.Join(", ", missing)}");
        }
        // The partial serializes exactly StockBehaviourText.ItemFormOptionKeys: a label the resx has but the key list
        // does not name never reaches the page.
        Assert.All(expected, key => Assert.Contains(key, StockBehaviourText.ItemFormOptionKeys));
    }

    // S5b-FIX2: the item Create form's own reason (no item without the lists) is read by the page in seven languages.
    [Fact]
    public void The_create_forms_options_unavailable_sentence_is_in_all_seven_languages()
    {
        Assert.Contains("OptionsUnavailableCreate", StockBehaviourText.ItemFormOptionKeys);
        Assert.Equal("The item cannot be created until the list arrives.",
            Resx("StockBehaviour", "StockBehaviourText", "en")["OptionsUnavailableCreate"]);
        foreach (var language in Languages)
        {
            var file = Resx("StockBehaviour", "StockBehaviourText", language);
            Assert.True(file.TryGetValue("OptionsUnavailableCreate", out var text) && !string.IsNullOrWhiteSpace(text),
                $"OptionsUnavailableCreate missing in {language}");
            Assert.NotEqual(file["OptionsUnavailable"], text);
        }
    }

    // The LSKU and Finished Good proxies say the stock codes with the GSKU bridge's sentences: none of the codes their
    // writes can answer may carry a field list ({0}) — it would never be filled there.
    [Fact]
    public void The_sentences_the_lsku_and_finished_good_writes_use_name_no_fields()
    {
        var codes = new[]
        {
            "STOCK_BEHAVIOUR_FORBIDDEN", "STOCK_BEHAVIOUR_INVALID", "STOCK_REFERENCE_LIST_UNAVAILABLE", "FG_GSKU_BASE_UOM_MISSING",
            "FG_GSKU_SHELF_LIFE_MISSING", "UOM_CONVERSION_INVALID", "ITEM_STORAGE_CONDITION_CONFLICT",
            "STOCK_STORAGE_CONDITION_INVALID", "STOCK_UOM_INVALID", "STOCK_BEHAVIOUR_INCOMPLETE", "STOCK_SHELF_LIFE_INVALID",
            "FG_SHELF_LIFE_EXCEEDS_GSKU", "FG_GTIN_INVALID", "STOCK_BEHAVIOUR_RECORD_NOT_FOUND",
            "STOCK_BEHAVIOUR_EDIT_BLOCKED_BY_OPERATION", "STOCK_BEHAVIOUR_RECORD_NOT_EDITABLE", "STOCK_BEHAVIOUR_FIELD_LOCKED",
            "STOCK_BEHAVIOUR_CONCURRENCY_CONFLICT", "IDEMPOTENCY_KEY_CONFLICT", "FG_GTIN_CONFLICT",
            "STOCK_BEHAVIOUR_WRITE_CONTRACT_INVALID", "IDEMPOTENCY_KEY_INVALID", "LSKU_NOT_FOUND", "FINISHED_GOOD_NOT_FOUND"
        };
        foreach (var language in Languages)
        {
            var sentences = Resx("Gskus", "GskuRefusals", language);
            foreach (var code in codes)
            {
                Assert.True(GskuRefusalSentences.Keys.TryGetValue(code, out var key), $"{code} has no sentence key");
                Assert.True(sentences.TryGetValue(key, out var text) && !text.Contains("{0}", StringComparison.Ordinal),
                    $"{code} → {key} missing or naming fields in {language}");
            }
        }
    }

    // ---- fixtures ----

    private static Dictionary<string, StringValues> Form(params (string Field, string Value)[] fields)
    {
        var form = new Dictionary<string, StringValues>(StringComparer.Ordinal) { ["ExpectedVersion"] = "3" };
        foreach (var (field, value) in fields) form[field] = value;
        return form;
    }

    private static int Status(IActionResult result) => Assert.IsAssignableFrom<ObjectResult>(result).StatusCode!.Value;

    private static GskusController Gsku(HttpMessageHandler mdm, Dictionary<string, StringValues> form,
        string? formAttempt = null, string? actor = null, string? tenant = null, string permission = "mdm.gskus.update") =>
        new(new HttpClient(mdm), Configuration(), new EphemeralDataProtectionProvider(),
            new FormatLocalizer<Diten.Web.SharedResource>("localized"), new FormatLocalizer<GskusIndex>("localized"),
            NullLogger<GskusController>.Instance, new FormatLocalizer<GskuRefusals>("refusal"))
        { ControllerContext = new ControllerContext { HttpContext = Context(permission, form, formAttempt, actor, tenant) } };

    private static LskusController Lsku(HttpMessageHandler mdm, Dictionary<string, StringValues> form,
        string permission = "mdm.lskus.create", string? formAttempt = null, string? actor = null, string? tenant = null) =>
        new(new HttpClient(mdm), Configuration(), new EphemeralDataProtectionProvider(),
            new FormatLocalizer<LskusIndex>(null), NullLogger<LskusController>.Instance, new FormatLocalizer<GskuRefusals>("refusal"))
        { ControllerContext = new ControllerContext { HttpContext = Context(permission, form, formAttempt, actor, tenant) } };

    private static FinishedGoodsController FinishedGood(HttpMessageHandler mdm, Dictionary<string, StringValues> form,
        string permission = "mdm.finished-goods.create", string? formAttempt = null, string? actor = null, string? tenant = null) =>
        new(new HttpClient(mdm), Configuration(), new EphemeralDataProtectionProvider(),
            new FormatLocalizer<Diten.Web.SharedResource>("localized"), new FormatLocalizer<FinishedGoodsIndex>("localized"),
            NullLogger<FinishedGoodsController>.Instance, new FormatLocalizer<GskuRefusals>("refusal"))
        { ControllerContext = new ControllerContext { HttpContext = Context(permission, form, formAttempt, actor, tenant) } };

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static DefaultHttpContext Context(string permission, Dictionary<string, StringValues> form,
        string? formAttempt = null, string? actor = null, string? tenant = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", tenant ?? Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", actor ?? Guid.NewGuid().ToString("D")),
                new Claim("permission", permission)
            ], "fixture"))
        };
        // Synthetic fixture credential: only seen by the scripted handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-stock-behaviour";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        form["FormAttemptId"] = formAttempt ?? Guid.NewGuid().ToString("D");
        form["__RequestVerificationToken"] = "synthetic-csrf";
        context.Request.Form = new FormCollection(form);
        return context;
    }

    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            data = new { recordId = Id, version = 4, changed = true, isReplay = false },
            isSuccessful = true,
            statusCode = 200,
            errors = Array.Empty<string>()
        }), Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Refusal(HttpStatusCode status, params string[] errors) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { isSuccessful = false, statusCode = (int)status, errors }),
            Encoding.UTF8, "application/json")
    };

    private static (int Status, string[] Errors, string Body) Read(IActionResult result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var body = JsonSerializer.Serialize(objectResult.Value);
        using var document = JsonDocument.Parse(body);
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToArray();
        return (objectResult.StatusCode!.Value, errors, body);
    }

    private static Dictionary<string, string> Resx(string folder, string file, string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                folder, $"{file}.{language}.resx")).Root!.Elements("data")
            .ToDictionary(x => x.Attribute("name")!.Value, x => x.Element("value")?.Value ?? string.Empty);

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }

    private sealed record SentRequest(HttpMethod Method, string Url, string Body, string IdempotencyKey);

    // MDM as a script: each call is recorded (method, URL, body, Idempotency-Key) and answered in order.
    private sealed class Capture(params HttpResponseMessage[] answers) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _answers = new(answers);
        public List<SentRequest> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent.Add(new SentRequest(request.Method, request.RequestUri!.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : string.Empty));
            return _answers.Dequeue();
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new InvalidOperationException("a refused form must not reach MDM");
    }

    // Says each key back (with a prefix); the days sentence shows its two format items, the joins are visible.
    private sealed class FormatLocalizer<T>(string? prefix) : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, Text(name));
        public LocalizedString this[string name, params object[] arguments] => new(name, Text(name));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];

        private string Text(string name)
        {
            if (name == "ListJoinTwo") return "{0} + {1}";
            if (name == "ListSeparator") return "; ";
            var text = prefix is null ? name : $"{prefix}:{name}";
            return name == "StockDaysExceedShelfLife" ? text + "[{0}|{1}]" : text;
        }
    }
}
