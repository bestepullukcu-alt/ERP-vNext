using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Primitives;
using Xunit;
using static Diten.Web.Tests.Recall.RecallDataGateTests;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-0177-R7c — the recall bands on MOD-0173 / 0174 / 0175 screens, KR-H on the 0173 form (CT decision A), the R7a "Müşteri iadesi gir"
/// link and the 375 px guard:
/// <list type="bullet">
/// <item>each band container is DRAWN only for the reader its read needs (recall.lots.status.read; the /LotTrace link recall.trace.run) —
/// the rules inside are measured by recall-r7c.test.mjs;</item>
/// <item>CUSTOMER_RETURN: the new-movement form only (FormTypes), held target statuses only, AVAILABLE refused by the form's own check;
/// ManualTypes — and so "Düzelt" (IsCorrectable) and the correction screen — unchanged; the other types' target statuses unchanged;</item>
/// <item>the form opens pre-filled from the address (type / lotId / toStatus), an invalid value is ignored, ForEntry reads none of them;</item>
/// <item>375 px: no Recall page / band carries a fixed width wider than the phone's content box, every table scrolls in its own box.</item>
/// </list>
/// </summary>
public sealed class RecallR7cWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Lot = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private readonly WebApplicationFactory<Program> _factory;

    public RecallR7cWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string Code(string source)
        => Regex.Replace(Regex.Replace(Regex.Replace(source, @"@\*[\s\S]*?\*@", string.Empty), @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    // ── the bands: drawn only for the reader whose key their read needs ─────────────────────────────────────────

    public static readonly TheoryData<string, string, string[], string> Bands = new()
    {
        // view, band marker, the page's own keys, the band's key
        { "/Views/Traceability/Lots/Details.cshtml", "data-recall-lot-badge", ["trace.lots.read", "inventory.stock.read"], RecallKeys.LotsStatusRead },
        { "/Views/Traceability/LotTrace/Index.cshtml", "data-recall-lottrace", ["trace.lots.read"], RecallKeys.TraceRun },
        { "/Views/Quarantine/Decide.cshtml", "data-recall-release-band", ["quarantine.holds.read", "quarantine.decisions.create"], RecallKeys.LotsStatusRead },
        { "/Views/Inventory/StockMovements/Create.cshtml", "data-recall-exit-band data-recall-refusal-only", ["inventory.stock.read", "inventory.movements.create", "trace.lots.read"], "-" }
    };

    private static object? ModelFor(string view) => view switch
    {
        "/Views/Inventory/StockMovements/Create.cshtml" => StockMovementFormViewModel.ForCreate(new QueryCollection()),
        "/Views/Quarantine/Decide.cshtml" => Lot,
        _ => LotPageViewModel.From(Lot)
    };

    [Theory]
    [MemberData(nameof(Bands))]
    public async Task A_band_is_drawn_only_for_the_reader_its_read_needs(string view, string marker, string[] pageKeys, string bandKey)
    {
        if (bandKey == "-")
        {
            // the movement form: without the status key only the refusal box (the server's 422 is worded the same); with it the live band
            var refusalOnly = await TraceViewRenderer.RenderAsync(_factory, view, ModelFor(view), pageKeys);
            Assert.Contains(marker, refusalOnly, StringComparison.Ordinal);
            var live = await TraceViewRenderer.RenderAsync(_factory, view, ModelFor(view), [.. pageKeys, RecallKeys.LotsStatusRead]);
            Assert.Contains("data-recall-exit-band role=\"alert\"", live, StringComparison.Ordinal);
            Assert.DoesNotContain("data-recall-refusal-only", live, StringComparison.Ordinal);
            return;
        }

        var without = await TraceViewRenderer.RenderAsync(_factory, view, ModelFor(view), pageKeys);
        Assert.DoesNotContain(marker, without, StringComparison.Ordinal);
        var with = await TraceViewRenderer.RenderAsync(_factory, view, ModelFor(view), [.. pageKeys, bandKey]);
        Assert.Contains(marker, with, StringComparison.Ordinal);
        Assert.Contains("recall-bands.js", with, StringComparison.Ordinal);
        Assert.Contains("id=\"recall-band-config\"", with, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_quarantine_record_carries_the_recall_source_slot_and_the_band_config_names_the_readers_keys()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Details.cshtml", Lot, "quarantine.holds.read");
        Assert.Contains("data-recall-hold-source", html, StringComparison.Ordinal);
        var config = JsonDocument.Parse(Regex.Match(html, @"<script id=""recall-band-config"" type=""application/json"">(?<j>[\s\S]*?)</script>").Groups["j"].Value).RootElement;
        Assert.False(config.GetProperty("perms").GetProperty("cases").GetBoolean()); // no recall.cases.read → no case link
        var linked = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Details.cshtml", Lot, "quarantine.holds.read", RecallKeys.CasesRead);
        var config2 = JsonDocument.Parse(Regex.Match(linked, @"<script id=""recall-band-config"" type=""application/json"">(?<j>[\s\S]*?)</script>").Groups["j"].Value).RootElement;
        Assert.True(config2.GetProperty("perms").GetProperty("cases").GetBoolean());
        Assert.All(Diten.Web.Views.Recall.Recall.BandKeys, k => Assert.False(string.IsNullOrWhiteSpace(config2.GetProperty("l10n").GetProperty(k).GetString()), k));
    }

    [Fact]
    public async Task The_customer_return_link_is_drawn_only_for_a_holder_of_the_0173_create_key()
    {
        var without = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/CaseDetails.cshtml", Lot, RecallKeys.CasesRead);
        Assert.DoesNotContain("rc-enter-return", without, StringComparison.Ordinal);
        var with = await TraceViewRenderer.RenderAsync(_factory, "/Views/Recall/CaseDetails.cshtml", Lot, RecallKeys.CasesRead, "inventory.movements.create");
        Assert.Contains("id=\"rc-enter-return\"", with, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_status_door_forwards_getLotRecallStatus_unchanged_and_refuses_anything_but_a_lot_id()
    {
        var body = "{\"lotId\":\"" + Lot + "\",\"underRecall\":true,\"cases\":[{\"number\":\"RC-2026-0009\",\"status\":\"Active\",\"sameCompany\":false}]}";
        var gateway = new FakeGateway { Answer = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") } };
        var result = Assert.IsType<ContentResult>(await Build(gateway, [RecallKeys.LotsStatusRead]).LotRecallStatus(Lot, CancellationToken.None));
        Assert.Equal(body, result.Content);
        Assert.Equal($"/api/recall/lots/{Lot}/status", Assert.Single(gateway.Requests));
        var bad = new FakeGateway();
        Assert.Equal(400, Status(await Build(bad, [RecallKeys.LotsStatusRead]).LotRecallStatus("../cases", CancellationToken.None)));
        Assert.Empty(bad.Requests);
    }

    // ── KR-H on the 0173 form (owner step MOD-0173) ──────────────────────────────────────────────────────────────

    [Fact]
    public void ManualTypes_and_therefore_Duzelt_and_the_correction_screen_are_unchanged()
    {
        string[] seven = ["OPENING_BALANCE", "COUNT_GAIN", "COUNT_LOSS", "SCRAP", "WRITE_OFF", "WAREHOUSE_TRANSFER", "LOCATION_TRANSFER"];
        Assert.Equal(seven, MovementRules.ManualTypes);
        Assert.Equal([.. seven, "CUSTOMER_RETURN"], MovementRules.FormTypes);

        // "Düzelt" (IsCorrectable) for every type a manual row can carry: exactly the seven — a CUSTOMER_RETURN row gets none
        string[] every = [.. seven, "CUSTOMER_RETURN", "GOODS_RECEIPT_PO", "GOODS_ISSUE_SALES", "STATUS_TRANSFER", "REVERSAL"];
        var correctable = every.Where(type => InventoryListAdapter.IsCorrectable(new TransactionItemDto
        {
            TransactionId = Guid.NewGuid().ToString("D"), MovementType = type, SourceModule = InventoryListAdapter.ManualSourceModule
        })).ToList();
        Assert.Equal(seven, correctable);

        // the correction screen accepts the seven, not a customer return
        QueryCollection Query(string type) => new(new Dictionary<string, StringValues>
        {
            ["id"] = Guid.NewGuid().ToString("D"), ["type"] = type, ["itemId"] = Guid.NewGuid().ToString("D"), ["skuId"] = Guid.NewGuid().ToString("D"),
            ["skuLevel"] = InventoryListAdapter.SkuLevels[0], ["quantity"] = "1", ["uomId"] = "EA"
        });
        Assert.All(seven, type => Assert.NotNull(CorrectionOriginalViewModel.From(Query(type))));
        Assert.Null(CorrectionOriginalViewModel.From(Query("CUSTOMER_RETURN")));
    }

    [Fact]
    public void A_customer_return_names_only_a_held_status_and_the_other_types_keep_theirs()
    {
        Assert.Equal(["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE"], MovementRules.TargetStatusesOf("OPENING_BALANCE"));
        Assert.Equal(["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE"], MovementRules.TargetStatusesOf("COUNT_GAIN"));
        Assert.Equal(["QUARANTINE", "QUALITY_INSPECTION", "BLOCKED"], MovementRules.TargetStatusesOf("CUSTOMER_RETURN"));
        Assert.DoesNotContain("AVAILABLE", MovementRules.TargetStatusesOf("CUSTOMER_RETURN"));
        Assert.All(MovementRules.ManualTypes.Except(["OPENING_BALANCE", "COUNT_GAIN"]), type => Assert.Empty(MovementRules.TargetStatusesOf(type)));
        Assert.Equal(["AVAILABLE", "QUALITY_INSPECTION", "QUARANTINE"], MovementRules.InboundStatuses); // unchanged

        MovementSubmitViewModel Form(string status, string type = "CUSTOMER_RETURN") => new()
        {
            MovementType = type, ItemId = Guid.NewGuid().ToString("D"), Sku = "Lsku:" + Guid.NewGuid().ToString("D"), Quantity = "2", UomId = "EA",
            TransactionDate = "2026-10-16T08:00:00Z", PostingDate = "2026-10-16T08:00:00Z", ToWarehouseId = "WH-1", ToLocationId = "BIN-1", ToStockStatus = status, LotId = Lot
        };
        // the form's server-side check (MOD-0173 Web): AVAILABLE refused at the field; nothing would be posted
        var available = MovementRules.Check(Form("AVAILABLE"), MovementRules.FormTypes, "k");
        Assert.Equal("ValRequired", available.Errors["toStockStatus"]);
        Assert.Null(available.Payload);
        foreach (var held in new[] { "QUARANTINE", "QUALITY_INSPECTION", "BLOCKED" })
        {
            var ok = MovementRules.Check(Form(held), MovementRules.FormTypes, "k");
            Assert.True(ok.IsValid, held);
            Assert.Equal(("CUSTOMER_RETURN", held, Lot), ((string)ok.Payload!["movementType"]!, (string)ok.Payload["toStockStatus"]!, (string)ok.Payload["lotId"]!));
        }

        // the correction screen's set (ManualTypes) does not take it
        Assert.False(MovementRules.Check(Form("QUARANTINE"), MovementRules.ManualTypes, "k").IsValid);
        // OPENING_BALANCE may not name BLOCKED (BLOCKED belongs to CUSTOMER_RETURN alone)
        Assert.False(MovementRules.Check(Form("BLOCKED", "OPENING_BALANCE"), MovementRules.FormTypes, "k").IsValid);
    }

    [Fact]
    public async Task The_form_draws_each_status_with_its_types_and_the_exit_band_beside_the_source()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Inventory/StockMovements/Create.cshtml", StockMovementFormViewModel.ForCreate(new QueryCollection()),
            "inventory.stock.read", "inventory.movements.create", "trace.lots.read");
        string Types(string status) => Regex.Match(html, $@"<option value=""{status}"" data-types=""(?<t>[^""]*)""").Groups["t"].Value;
        Assert.Equal("OPENING_BALANCE COUNT_GAIN", Types("AVAILABLE"));
        Assert.Equal("OPENING_BALANCE COUNT_GAIN CUSTOMER_RETURN", Types("QUALITY_INSPECTION"));
        Assert.Equal("OPENING_BALANCE COUNT_GAIN CUSTOMER_RETURN", Types("QUARANTINE"));
        Assert.Equal("CUSTOMER_RETURN", Types("BLOCKED"));
        Assert.Contains("<option value=\"CUSTOMER_RETURN\">", html, StringComparison.Ordinal);
        var source = html.IndexOf("data-matrix=\"source\"", StringComparison.Ordinal);
        var band = html.IndexOf("data-recall-exit-band", StringComparison.Ordinal);
        var target = html.IndexOf("data-matrix=\"target-warehouse\"", StringComparison.Ordinal);
        Assert.True(source < band && band < target, "the exit band sits in the source block");
    }

    [Fact]
    public void The_form_opens_pre_filled_from_the_address_and_ignores_an_invalid_value()
    {
        QueryCollection Q(params (string K, string V)[] p) => new(p.ToDictionary(x => x.K, x => new StringValues(x.V)));
        var ok = StockMovementFormViewModel.ForCreate(Q(("type", "CUSTOMER_RETURN"), ("lotId", Lot), ("toStatus", "QUARANTINE")));
        Assert.Equal(("CUSTOMER_RETURN", Lot, "QUARANTINE"), (ok.MovementType, ok.LotId, ok.ToStockStatus));
        Assert.Contains("CUSTOMER_RETURN", ok.AllowedTypes);

        Assert.Null(StockMovementFormViewModel.ForCreate(Q(("type", "CUSTOMER_RETURN"), ("toStatus", "AVAILABLE"))).ToStockStatus); // not a held status
        Assert.Null(StockMovementFormViewModel.ForCreate(Q(("type", "CUSTOMER_RETURN"), ("lotId", "LOT-TYPED-7"))).LotId);          // not a registered lot id
        Assert.Null(StockMovementFormViewModel.ForCreate(Q(("type", "STATUS_TRANSFER"))).MovementType);                              // not a form type
        Assert.Null(StockMovementFormViewModel.ForCreate(Q(("toStatus", "QUARANTINE"))).ToStockStatus);                             // no type → no status

        var entry = StockMovementFormViewModel.ForEntry(Q(("type", "OPENING_BALANCE"), ("lotId", Lot), ("toStatus", "QUARANTINE")));
        Assert.Equal(("OPENING_BALANCE", (string?)null, (string?)null), (entry.MovementType, entry.LotId, entry.ToStockStatus)); // ForEntry unchanged
        Assert.DoesNotContain("CUSTOMER_RETURN", entry.AllowedTypes);
        Assert.Null(StockMovementFormViewModel.ForEntry(Q(("type", "CUSTOMER_RETURN"))).MovementType);
    }

    [Fact]
    public void The_LOT_UNDER_RECALL_refusal_carries_the_case_number_only()
    {
        var details = new Dictionary<string, object?> { ["recallCaseNumber"] = "RC-2026-0009", ["recallCaseId"] = Guid.NewGuid().ToString("D"), ["lotId"] = Lot };
        var kept = StockMovementsController.RecallRefusalDetails("LOT_UNDER_RECALL", details)!;
        Assert.Equal(["recallCaseNumber"], kept.Keys);
        Assert.Equal("RC-2026-0009", kept["recallCaseNumber"]);
        Assert.Null(StockMovementsController.RecallRefusalDetails("INSUFFICIENT_STOCK", details));
    }

    // ── 375 px (D-21) ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The content box of a 375 px phone with the 16 px gutters: nothing fixed wider fits.</summary>
    private const double PhoneContent = 375 - 2 * 16;

    public static IEnumerable<object[]> PhoneFiles()
        => Directory.EnumerateFiles(Path.Combine(Web, "Views", "Recall"), "*.cshtml")
            .Concat(Directory.EnumerateFiles(Path.Combine(Web, "Views", "Shared", "Recall"), "*.cshtml"))
            .Concat(Directory.EnumerateFiles(Path.Combine(Web, "wwwroot", "assets", "js", "Recall"), "*.js"))
            .Select(f => new object[] { Path.GetRelativePath(Web, f).Replace('\\', '/') });

    [Theory]
    [MemberData(nameof(PhoneFiles))]
    public void At_375_px_no_recall_page_or_band_has_a_fixed_width_wider_than_the_phone_and_every_table_scrolls_in_its_own_box(string file)
    {
        var code = Code(File.ReadAllText(Path.Combine(Web, file)));
        foreach (Match m in Regex.Matches(code, @"(?<!max-)width\s*:\s*(?<n>\d+(?:\.\d+)?)(?<u>px|rem|em)"))
        {
            var px = double.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture) * (m.Groups["u"].Value == "px" ? 1 : 16);
            Assert.True(px <= PhoneContent, $"{file}: '{m.Value}' = {px} px is wider than the 375 px phone's content box ({PhoneContent} px)");
        }

        var at = -1;
        while ((at = code.IndexOf("<table", at + 1, StringComparison.Ordinal)) >= 0)
        {
            var before = code[..at];
            var box = before[before.LastIndexOf("<div", StringComparison.Ordinal)..];
            Assert.True(box.Contains("table-responsive", StringComparison.Ordinal), $"{file}: a table outside a table-responsive box (offset {at})");
        }
    }
}

/// <summary>The bridge that makes `dotnet test` run the R7c Node tests (recall-r7c.test.mjs) against the SHIPPED band / link scripts.</summary>
public sealed class RecallR7cNodeTests
{
    [Fact]
    public async Task The_band_and_link_scripts_hold_against_the_shipped_files()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-r7c.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 7$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
