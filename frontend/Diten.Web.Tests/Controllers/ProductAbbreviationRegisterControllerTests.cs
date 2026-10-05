using System.Net;
using System.Security.Claims;
using Diten.Web.Controllers;
using Diten.Web.Models.ProductAbbreviationRegister;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

public sealed class ProductAbbreviationRegisterControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Requester_needs_exact_GlobalProductRead_for_selector_and_request(bool productRead)
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.read", "mdm.product-abbreviations.request",
            productRead ? "mdm.global-products.read" : "unrelated");
        Assert.IsType<ViewResult>(controller.Index());
        var selector = await controller.GlobalProductSelector(default);
        var request = await controller.RequestAllocation(new() { GlobalProductId = Guid.NewGuid(), Abbreviation = "ABC" }, default);
        if (productRead) { Assert.IsType<ContentResult>(selector); Assert.IsType<ContentResult>(request); Assert.Equal(2, wire.Paths.Count); }
        else { AssertDenied(selector); AssertDenied(request); Assert.Empty(wire.Paths); }
    }

    [Fact]
    public async Task Auditor_reads_register_and_evidence_without_selector_permission_or_call()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.read", "mdm.product-abbreviations.audit");
        Assert.IsType<ViewResult>(controller.Index());
        Assert.IsType<ContentResult>(await controller.GetByGlobalProduct(Guid.NewGuid(), default));
        Assert.IsType<ContentResult>(await controller.GetEvidence(Guid.NewGuid(), default));
        AssertDenied(await controller.GlobalProductSelector(default));
        AssertDenied(await controller.RequestAllocation(new(), default));
        Assert.Equal(2, wire.Paths.Count);
        Assert.DoesNotContain(wire.Paths, path => path.Contains("selector"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("mdm.global-products.Read")]
    [InlineData("mdm.product-abbreviations.request")]
    public async Task Nonexact_selector_permission_does_not_proxy(string permission)
    {
        using var wire = new Wire();
        AssertDenied(await Controller(wire, permission).GlobalProductSelector(default));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task Mutation_replay_identity_is_server_owned_tenant_subject_target_and_payload_bound()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.retire");
        var id = Guid.NewGuid();
        Assert.IsType<ContentResult>(await controller.RequestRetirement(id, new(4, "fixture reason"), default));
        await controller.RequestRetirement(id, new(4, "fixture reason"), default);
        Assert.Equal(wire.Keys[0], wire.Keys[1]);
        await controller.RequestRetirement(id, new(5, "fixture reason"), default);
        await controller.RequestRetirement(Guid.NewGuid(), new(4, "fixture reason"), default);
        Assert.NotEqual(wire.Keys[0], wire.Keys[2]);
        Assert.NotEqual(wire.Keys[0], wire.Keys[3]);
        var identity = (ClaimsIdentity)controller.User.Identity!;
        identity.RemoveClaim(identity.FindFirst("sub")!);
        identity.AddClaim(new("sub", Guid.NewGuid().ToString("D")));
        await controller.RequestRetirement(id, new(4, "fixture reason"), default);
        Assert.NotEqual(wire.Keys[0], wire.Keys[4]);
        identity.RemoveClaim(identity.FindFirst("tenant_id")!);
        identity.AddClaim(new("tenant_id", Guid.NewGuid().ToString("D")));
        await controller.RequestRetirement(id, new(4, "fixture reason"), default);
        Assert.NotEqual(wire.Keys[0], wire.Keys[5]);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Backend_failure_status_and_envelope_are_not_reported_as_success(int status)
    {
        using var wire = new Wire { Status = status };
        var controller = Controller(wire, "mdm.product-abbreviations.retire");
        var result = Assert.IsType<ContentResult>(await controller.RequestRetirement(Guid.NewGuid(), new(4, "reason"), default));
        Assert.Equal(status, result.StatusCode);
        Assert.Contains("\"isSuccessful\":false", result.Content);
    }

    [Fact]
    public async Task Duplicate_tenant_and_conflicting_subject_fail_before_transport()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.product-abbreviations.retire");
        var identity = (ClaimsIdentity)controller.User.Identity!;
        identity.AddClaim(new("tenant_id", identity.FindFirst("tenant_id")!.Value));
        Assert.IsType<UnauthorizedObjectResult>(await controller.RequestRetirement(Guid.NewGuid(), new(4, "reason"), default));
        identity.RemoveClaim(identity.FindAll("tenant_id").Last());
        identity.AddClaim(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")));
        Assert.IsType<UnauthorizedResult>(await controller.RequestRetirement(Guid.NewGuid(), new(4, "reason"), default));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public void A_reader_without_permission_gets_a_403_page_inside_the_shell_not_a_redirect()
    {
        // FIX1 S5 (UAS-001) — Forbid() answered 302 to an AccessDeniedPath this app does not have (→ 404).
        using var wire = new Wire();
        var controller = Controller(wire, "mdm.global-products.read");

        var result = controller.Index();

        Assert.Equal("~/Views/MDM/ProductAbbreviationRegister/Index.cshtml", Assert.IsType<ViewResult>(result).ViewName);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task Every_proxy_refuses_a_missing_permission_with_a_coded_403_and_never_calls_the_gateway()
    {
        using var wire = new Wire();
        var controller = Controller(wire, "unrelated");
        var id = Guid.NewGuid();

        AssertDenied(await controller.GetByGlobalProduct(id, default));
        AssertDenied(await controller.ListByGlobalProduct(id, default));
        AssertDenied(await controller.GetEvidence(id, default));
        AssertDenied(await controller.Cancel(id, new(0), default));
        AssertDenied(await controller.Approve(id, new(0), default));
        AssertDenied(await controller.Reject(id, new(0, "r"), default));
        AssertDenied(await controller.InitiateCorrection(id, new(0, "ABC", "r"), default));
        AssertDenied(await controller.RequestRetirement(id, new(0, "r"), default));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task The_page_list_reads_every_entry_of_the_product_through_the_gateway()
    {
        using var wire = new Wire();
        var id = Guid.NewGuid();

        Assert.IsType<ContentResult>(await Controller(wire, "mdm.product-abbreviations.read").ListByGlobalProduct(id, default));

        Assert.Equal($"/api/product-abbreviations/by-global-product/{id:D}/entries", Assert.Single(wire.Paths));
    }

    // ── WP-MDM-ABB-LIST-01: the register list in the list component's server protocol ───────────────────────────

    /// <summary>MDM's one-product entry list as it answers it (PascalCase here: the proxy reads names case-insensitively).</summary>
    private static string Entries(Guid product, params (string Code, string Status, int Version, bool Retiring)[] rows)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            isSuccessful = true,
            data = rows.Select((row, i) => new Dictionary<string, object?>
            {
                ["Id"] = Guid.Parse($"00000000-0000-0000-0000-{i + 1:D12}"),
                ["GlobalProductId"] = product,
                ["Abbreviation"] = row.Code,
                ["LifecycleStatus"] = row.Status,
                ["Version"] = row.Version,
                ["RetirementPending"] = row.Retiring,
                ["CorrectionPending"] = false,
                ["RequestedByCurrentUser"] = false,
                ["RetirementRequestedByCurrentUser"] = false
            })
        });

    private static System.Text.Json.JsonElement Page(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return System.Text.Json.JsonSerializer.SerializeToElement(ok.Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)).GetProperty("data");
    }

    [Fact]
    public async Task The_list_pages_searches_and_sorts_one_products_entries_on_the_server()
    {
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product,
                ("CCC", "ACTIVE", 1, false), ("ABA", "REQUESTED", 0, false), ("BBA", "RETIRED", 3, true),
                ("ABB", "REJECTED", 1, false), ("XYZ", "CANCELLED", 2, false))
        };
        var controller = Controller(wire, "mdm.product-abbreviations.read");

        var page = Page(await controller.List(start: 1, length: 2, search: "b", orderBy: "abbreviation", orderDir: "desc",
            globalProductId: product.ToString("D")));

        Assert.Equal($"/api/product-abbreviations/by-global-product/{product:D}/entries", Assert.Single(wire.Paths));
        Assert.Equal(5, page.GetProperty("total").GetInt32());
        Assert.Equal(3, page.GetProperty("filteredTotal").GetInt32());
        // b-matches by abbreviation: BBA, ABB, ABA — descending, page two of size two starts at the second.
        Assert.Equal(["ABB", "ABA"], page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("abbreviation").GetString()).ToArray());
    }

    [Fact]
    public async Task The_list_sorts_by_the_lifecycle_in_MDMs_own_order()
    {
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product, ("AAA", "RETIRED", 1, false), ("BBB", "REQUESTED", 0, false), ("CCC", "ACTIVE", 1, false))
        };

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(
            start: 0, length: 10, orderBy: "lifecycleStatus", orderDir: "asc", globalProductId: product.ToString("D")));

        Assert.Equal(["BBB", "CCC", "AAA"], page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("abbreviation").GetString()).ToArray());
    }

    [Theory]
    [InlineData("globalProductId", "asc")]
    [InlineData("id", "asc")]
    [InlineData("Abbreviation", "asc")]
    [InlineData("abbreviation", "sideways")]
    public async Task A_sort_outside_the_whitelist_is_refused_with_400_and_never_reaches_the_gateway(string orderBy, string orderDir)
    {
        using var wire = new Wire();

        var refused = Assert.IsType<ObjectResult>(await Controller(wire, "mdm.product-abbreviations.read").List(
            start: 0, length: 10, orderBy: orderBy, orderDir: orderDir, globalProductId: Guid.NewGuid().ToString("D")));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.StatusCode);
        Assert.Contains("ABBREVIATION_LIST_ORDER_INVALID", System.Text.Json.JsonSerializer.Serialize(refused.Value));
        Assert.Empty(wire.Paths);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task A_page_outside_its_bounds_is_refused_with_400(int start, int length)
    {
        using var wire = new Wire();

        var refused = Assert.IsType<ObjectResult>(await Controller(wire, "mdm.product-abbreviations.read").List(
            start: start, length: length, globalProductId: Guid.NewGuid().ToString("D")));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.StatusCode);
        Assert.Contains("ABBREVIATION_LIST_PAGE_INVALID", System.Text.Json.JsonSerializer.Serialize(refused.Value));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task Without_a_product_the_list_is_an_empty_page_and_no_gateway_call()
    {
        using var wire = new Wire();

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(start: 0, length: 10));

        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Equal(0, page.GetProperty("filteredTotal").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task The_list_needs_read_permission_and_passes_MDMs_own_refusal_on()
    {
        using var denied = new Wire();
        AssertDenied(await Controller(denied, "unrelated").List(globalProductId: Guid.NewGuid().ToString("D")));
        Assert.Empty(denied.Paths);

        using var refusing = new Wire { Status = 503, Body = "{\"isSuccessful\":false,\"errors\":[\"ABBREVIATION_PERSISTENCE_UNAVAILABLE\"]}" };
        var passed = Assert.IsType<ContentResult>(await Controller(refusing, "mdm.product-abbreviations.read")
            .List(globalProductId: Guid.NewGuid().ToString("D")));
        Assert.Equal(503, passed.StatusCode);
        Assert.Contains("ABBREVIATION_PERSISTENCE_UNAVAILABLE", passed.Content);
    }

    // ── WP-MDM-ABB-LIST-01 FIX1 ──────────────────────────────────────────────────────────────────────────────

    private static string[] Codes(System.Text.Json.JsonElement page)
        => page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("abbreviation").GetString()!).ToArray();

    [Fact]
    public async Task The_open_entry_answer_is_read_from_every_entry_not_from_the_page_or_the_search()
    {
        // Item 4: the ACTIVE code is on neither the page asked for (page two) nor in the search result ("Z").
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product, ("AAA", "ACTIVE", 1, false), ("ZZA", "REJECTED", 1, false), ("ZZB", "CANCELLED", 1, false),
                ("ZZC", "RETIRED", 2, false))
        };

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(
            start: 2, length: 2, search: "ZZ", globalProductId: product.ToString("D")));

        Assert.Equal(["ZZC"], Codes(page));
        Assert.True(page.GetProperty("hasOpenEntry").GetBoolean());
    }

    [Theory]
    [InlineData("REQUESTED", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("REJECTED", false)]
    [InlineData("CANCELLED", false)]
    [InlineData("RETIRED", false)]
    public async Task Only_an_active_or_requested_code_keeps_the_product_from_a_new_request(string status, bool open)
    {
        var product = Guid.NewGuid();
        using var wire = new Wire { Body = Entries(product, ("AAA", status, 1, false)) };

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(globalProductId: product.ToString("D")));

        Assert.Equal(open, page.GetProperty("hasOpenEntry").GetBoolean());
    }

    [Fact]
    public async Task A_lifecycle_sent_as_its_number_is_read_like_its_name()
    {
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = System.Text.Json.JsonSerializer.Serialize(new
            {
                isSuccessful = true,
                data = new[] { new { id = Guid.NewGuid(), globalProductId = product, abbreviation = "NUM", lifecycleStatus = 1, version = 1 } }
            })
        };

        using var reader = new ReaderCulture("en");
        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(
            search: "in use", globalProductId: product.ToString("D")));

        Assert.True(page.GetProperty("hasOpenEntry").GetBoolean());
        Assert.Equal(["NUM"], Codes(page));
    }

    [Theory]
    [InlineData(499, false)]
    [InlineData(500, true)]
    public async Task A_read_that_came_back_full_is_marked_truncated(int count, bool truncated)
    {
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product, Enumerable.Range(0, count).Select(i => ($"A{i:D3}", "RETIRED", 1, false)).ToArray())
        };

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(globalProductId: product.ToString("D")));

        Assert.Equal(truncated, page.GetProperty("truncated").GetBoolean());
        Assert.Equal(count, page.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("in use", "BBB")]
    [InlineData("WAITING", "AAA")]
    [InlineData("7", "CCC")]
    [InlineData("indeed", "DDD")]
    public async Task The_search_matches_what_the_reader_sees_the_code_the_lifecycle_the_version_and_the_retirement(string term, string expected)
    {
        // Item 7: as the former client-side search did — the lifecycle in the reader's words, the version, yes/no. The
        // fixture words are English: the reader's UI culture is English.
        using var reader = new ReaderCulture("en");
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product, ("AAA", "REQUESTED", 0, false), ("BBB", "ACTIVE", 1, false), ("CCC", "REJECTED", 7, false),
                ("DDD", "RETIRED", 2, true))
        };

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(
            search: term, globalProductId: product.ToString("D")));

        Assert.Equal([expected], Codes(page));
    }

    [Fact]
    public async Task A_turkish_reader_finds_a_dotted_capital_I_by_typing_a_small_i()
    {
        // The case rules are the READER's: under Turkish rules "iptal" is "İptal", which an ordinal comparison never finds.
        using var reader = new ReaderCulture("tr");
        var product = Guid.NewGuid();
        using var wire = new Wire { Body = Entries(product, ("AAA", "CANCELLED", 1, false), ("BBB", "ACTIVE", 1, false)) };
        var words = new Dictionary<string, string>(PageWords) { ["LifecycleCancelled"] = "İptal edildi", ["LifecycleActive"] = "Etkin" };

        var page = Page(await Controller(wire, words, "mdm.product-abbreviations.read").List(
            search: "iptal", globalProductId: product.ToString("D")));

        Assert.Equal(["AAA"], Codes(page));
    }

    /// <summary>The reader's language for one test: the UI culture the page's words are looked up and compared in.</summary>
    private sealed class ReaderCulture : IDisposable
    {
        private readonly (System.Globalization.CultureInfo, System.Globalization.CultureInfo) _previous =
            (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture);

        public ReaderCulture(string name)
            => System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture =
                System.Globalization.CultureInfo.GetCultureInfo(name);

        public void Dispose()
            => (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture) = _previous;
    }

    [Fact]
    public async Task Equal_sort_keys_are_ordered_by_the_code_and_then_by_the_entry_id()
    {
        // Item 8: ThenBy — three entries share the version; two of them also share the code (a cancelled request and its
        // successor). MDM's order is deliberately the reverse of the expected one.
        var product = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var third = Guid.Parse("00000000-0000-0000-0000-000000000003");
        using var wire = new Wire
        {
            Body = System.Text.Json.JsonSerializer.Serialize(new
            {
                isSuccessful = true,
                data = new[]
                {
                    new { id = third, globalProductId = product, abbreviation = "BBB", lifecycleStatus = "ACTIVE", version = 1 },
                    new { id = second, globalProductId = product, abbreviation = "AAA", lifecycleStatus = "REQUESTED", version = 1 },
                    new { id = first, globalProductId = product, abbreviation = "AAA", lifecycleStatus = "CANCELLED", version = 1 }
                }
            })
        };

        var page = Page(await Controller(wire, "mdm.product-abbreviations.read").List(
            orderBy: "version", orderDir: "desc", globalProductId: product.ToString("D")));

        Assert.Equal([first, second, third], page.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToArray());
    }

    [Fact]
    public async Task An_answer_that_is_not_an_entry_list_is_502_and_so_is_an_unreachable_gateway()
    {
        using var unreadable = new Wire { Body = "<html>not json</html>" };
        var bad = Assert.IsType<ObjectResult>(await Controller(unreadable, "mdm.product-abbreviations.read")
            .List(globalProductId: Guid.NewGuid().ToString("D")));
        Assert.Equal(StatusCodes.Status502BadGateway, bad.StatusCode);

        using var wrongShape = new Wire { Body = "{\"isSuccessful\":true,\"data\":{\"items\":[]}}" };
        var shape = Assert.IsType<ObjectResult>(await Controller(wrongShape, "mdm.product-abbreviations.read")
            .List(globalProductId: Guid.NewGuid().ToString("D")));
        Assert.Equal(StatusCodes.Status502BadGateway, shape.StatusCode);

        using var down = new Wire { Unreachable = true };
        var unreachable = Assert.IsType<ObjectResult>(await Controller(down, "mdm.product-abbreviations.read")
            .List(globalProductId: Guid.NewGuid().ToString("D")));
        Assert.Equal(StatusCodes.Status502BadGateway, unreachable.StatusCode);
    }

    [Fact]
    public async Task Without_a_tenant_the_list_is_401_and_never_reaches_the_gateway()
    {
        using var wire = new Wire();

        var result = await Controller(wire, withTenant: false, "mdm.product-abbreviations.read")
            .List(globalProductId: Guid.NewGuid().ToString("D"));

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Empty(wire.Paths);
    }

    [Theory]
    [InlineData("not-a-product")]
    [InlineData("11111111111111111111111111111111")]
    [InlineData("{11111111-1111-1111-1111-111111111111}")]
    public async Task A_product_id_that_is_not_one_is_refused_with_400_not_read_as_no_product(string value)
    {
        using var wire = new Wire();

        var refused = Assert.IsType<ObjectResult>(await Controller(wire, "mdm.product-abbreviations.read")
            .List(globalProductId: value));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.StatusCode);
        Assert.Contains("ABBREVIATION_LIST_PRODUCT_INVALID", System.Text.Json.JsonSerializer.Serialize(refused.Value));
        Assert.Empty(wire.Paths);
    }

    // ── Item 5: the export is every entry of the product as the list shows it ───────────────────────────────────

    private static async Task<string> CsvAsync(IActionResult result)
    {
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Matches(@"^product-abbreviation-register-\d{8}-\d{4}\.csv$", file.FileDownloadName);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, file.FileContents.Take(3).ToArray());
        return await Task.FromResult(System.Text.Encoding.UTF8.GetString(file.FileContents, 3, file.FileContents.Length - 3));
    }

    [Fact]
    public async Task The_export_writes_every_matching_entry_in_the_lists_order_and_columns_not_one_page()
    {
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product, Enumerable.Range(0, 30).Select(i => ($"B{i:D2}", i % 2 == 0 ? "ACTIVE" : "RETIRED", i, i == 3)).ToArray())
        };

        var csv = await CsvAsync(await Controller(wire, "mdm.product-abbreviations.read").Export(
            format: "csv", columns: ["abbreviation,lifecycleStatus", "retirementPending"], search: "gone",
            orderBy: "abbreviation", orderDir: "desc", globalProductId: product.ToString("D")));

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Code,State,Retiring", lines[0]);
        // Every RETIRED entry (15 — more than any page), in the list's order, in the reader's words.
        Assert.Equal(Enumerable.Range(0, 30).Where(i => i % 2 == 1).OrderByDescending(i => i)
            .Select(i => $"B{i:D2},Gone,{(i == 3 ? "Indeed" : "Nope")}").ToArray(), lines.Skip(1).ToArray());
    }

    [Fact]
    public async Task The_export_without_columns_writes_every_exportable_column_and_writes_xlsx_on_request()
    {
        var product = Guid.NewGuid();
        using var wire = new Wire { Body = Entries(product, ("AAA", "ACTIVE", 1, false)) };
        var controller = Controller(wire, "mdm.product-abbreviations.read");

        var csv = await CsvAsync(await controller.Export(globalProductId: product.ToString("D")));
        Assert.Equal("Product,Code,State,Rev,Retiring", csv.Split("\r\n")[0]);
        Assert.Equal($"{product:D},AAA,In use,1,Nope", csv.Split("\r\n")[1]);

        var xlsx = Assert.IsType<FileContentResult>(await controller.Export(format: "xlsx", globalProductId: product.ToString("D")));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.ContentType);
        Assert.Equal((byte)'P', xlsx.FileContents[0]);
    }

    [Fact]
    public async Task A_product_whose_read_came_back_full_is_not_exported_in_part_413()
    {
        var product = Guid.NewGuid();
        using var wire = new Wire
        {
            Body = Entries(product, Enumerable.Range(0, 500).Select(i => ($"A{i:D3}", "RETIRED", 1, false)).ToArray())
        };

        var refused = Assert.IsType<ObjectResult>(await Controller(wire, "mdm.product-abbreviations.read")
            .Export(globalProductId: product.ToString("D")));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, refused.StatusCode);
        Assert.Contains("EXPORT_TOO_LARGE", System.Text.Json.JsonSerializer.Serialize(refused.Value));
    }

    [Theory]
    [InlineData("pdf", null, null, "EXPORT_FORMAT_INVALID")]
    [InlineData("csv", "tenantId", null, "EXPORT_COLUMNS_INVALID")]
    [InlineData("csv", "abbreviation", "id", "ABBREVIATION_LIST_ORDER_INVALID")]
    public async Task An_export_outside_its_whitelists_is_refused_with_400_and_never_reaches_the_gateway(
        string format, string? column, string? orderBy, string code)
    {
        using var wire = new Wire();

        var refused = Assert.IsType<ObjectResult>(await Controller(wire, "mdm.product-abbreviations.read").Export(
            format: format, columns: column is null ? null : [column], orderBy: orderBy, globalProductId: Guid.NewGuid().ToString("D")));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.StatusCode);
        Assert.Contains(code, System.Text.Json.JsonSerializer.Serialize(refused.Value));
        Assert.Empty(wire.Paths);
    }

    [Fact]
    public async Task The_export_needs_read_permission()
    {
        using var wire = new Wire();
        AssertDenied(await Controller(wire, "unrelated").Export(globalProductId: Guid.NewGuid().ToString("D")));
        Assert.Empty(wire.Paths);
    }

    private static void AssertDenied(IActionResult result)
    {
        var denied = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
        Assert.Contains("ABBREVIATION_PERMISSION_DENIED", System.Text.Json.JsonSerializer.Serialize(denied.Value));
    }

    private static ProductAbbreviationRegisterController Controller(Wire wire, params string[] permissions)
        => Controller(wire, withTenant: true, permissions);

    private static ProductAbbreviationRegisterController Controller(Wire wire, IReadOnlyDictionary<string, string> words, params string[] permissions)
        => Controller(wire, withTenant: true, words, permissions);

    private static ProductAbbreviationRegisterController Controller(Wire wire, bool withTenant, params string[] permissions)
        => Controller(wire, withTenant, PageWords, permissions);

    private static ProductAbbreviationRegisterController Controller(
        Wire wire, bool withTenant, IReadOnlyDictionary<string, string> words, params string[] permissions)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString("D")) };
        if (withTenant) claims.Add(new("tenant_id", Guid.NewGuid().ToString("D")));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var context = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "fixture")) };
        // Synthetic forwarding fixture only; no real JWT or credential and no live HTTP destination.
        context.Request.Headers.Cookie = "access_token=synthetic-abb-fixture";
        return new(new HttpClient(wire), new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build(),
            new Localizer<Diten.Web.SharedResource>(), new Localizer<Diten.Web.Views.MDM.ProductAbbreviationRegister.ProductAbbreviationRegisterIndex>(words),
            NullLogger<ProductAbbreviationRegisterController>.Instance)
        { ControllerContext = new() { HttpContext = context } };
    }

    /// <summary>The page's words as a reader sees them (a fixture language: what the search and the file must use).</summary>
    private static readonly Dictionary<string, string> PageWords = new()
    {
        ["LifecycleRequested"] = "Waiting", ["LifecycleActive"] = "In use", ["LifecycleRejected"] = "Turned down",
        ["LifecycleCancelled"] = "Withdrawn", ["LifecycleRetired"] = "Gone", ["Yes"] = "Indeed", ["No"] = "Nope",
        ["GlobalProduct"] = "Product", ["Abbreviation"] = "Code", ["LifecycleStatus"] = "State", ["Version"] = "Rev",
        ["RetirementPending"] = "Retiring", ["ProductAbbreviationRegisterTitle"] = "ABB"
    };

    private sealed class Wire : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> Keys { get; } = [];
        public int Status { get; init; } = 200;
        /// <summary>The gateway cannot be reached: the send throws.</summary>
        public bool Unreachable { get; init; }
        /// <summary>The gateway's answer body; null = the default success / failure envelope.</summary>
        public string? Body { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Paths.Add(request.RequestUri!.AbsolutePath);
            if (request.Headers.TryGetValues("Idempotency-Key", out var keys)) Keys.Add(Assert.Single(keys));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Single(request.Headers.GetValues("X-Tenant-Id"));
            if (Unreachable) throw new HttpRequestException("fixture: gateway unreachable");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status) {
                Content = new StringContent(Body ?? (Status == 200 ? "{\"isSuccessful\":true,\"data\":{}}" : "{\"isSuccessful\":false,\"errors\":[\"fixture-failure\"]}"))
            });
        }
    }
    private sealed class Localizer<T>(IReadOnlyDictionary<string, string>? words = null) : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, words is not null && words.TryGetValue(name, out var word) ? word : name);
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
