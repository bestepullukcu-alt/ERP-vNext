using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Items;
using Diten.Web.Views.MasterDataManagement.Items;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0290-FU04 S5 — the Items screen's proxy, measured on the real controller with MDM as a recording handler:
/// the list component's request becomes MDM's page query and MDM's page { items, total } the component's envelope;
/// the create carries the server-made idempotency key and only MDM's eleven fields; update and lifecycle carry the
/// expected version (and the reason); every refusal is a sentence, 422 stays 422, an unknown code is flagged.
/// </summary>
public sealed class ItemsProxyTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private static readonly Guid ItemId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    // ── List ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_request_becomes_MDMs_page_query_and_total_becomes_total_and_filteredTotal()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK,
            "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"items\":[{\"id\":\"" + ItemId + "\",\"canonicalCode\":\"IT-000000000003\",\"availableActions\":[\"DETAILS\"]}],\"total\":41,\"page\":3,\"pageSize\":20}}");
        var controller = Controller(wire, ["mdm.items.read"],
            "?start=40&length=20&search=IT%2000&orderBy=name&orderDir=desc&draw=7&materialType=EXCIPIENT&status=Active");

        var result = await controller.List(CancellationToken.None);

        // orderBy / orderDir are not passed: MDM sorts by code only and has no sort parameter.
        Assert.Equal($"{Gateway}/api/mdm/items?page=3&pageSize=20&q=IT%2000&materialType=EXCIPIENT&status=Active",
            Assert.Single(wire.Requests).Url);
        using var body = JsonDocument.Parse(Assert.IsType<ContentResult>(result).Content!);
        var data = body.RootElement.GetProperty("data");
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("IT-000000000003", Assert.Single(data.GetProperty("items").EnumerateArray()).GetProperty("canonicalCode").GetString());
        Assert.Equal(41, data.GetProperty("total").GetInt64());
        Assert.Equal(41, data.GetProperty("filteredTotal").GetInt64());
    }

    [Theory]
    [InlineData("?start=0&length=500", "page=1&pageSize=100")]
    [InlineData("?start=-5&length=0", "page=1&pageSize=20")]
    [InlineData("", "page=1&pageSize=20")]
    [InlineData("?start=250&length=100", "page=3&pageSize=100")]
    [InlineData("?start=9223372036854775807&length=1", "page=1000000&pageSize=1")]
    public async Task The_page_size_is_capped_at_100_and_the_page_never_passes_MDMs_bound(string query, string expected)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[],\"total\":0}}");

        await Controller(wire, ["mdm.items.read"], query).List(CancellationToken.None);

        Assert.Equal($"{Gateway}/api/mdm/items?{expected}", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task A_page_without_its_total_is_a_gateway_failure_not_an_empty_list()
    {
        // MDM's GSKU page says totalCount; the item page says total — a page with neither is broken.
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[],\"totalCount\":0}}");

        var result = await Controller(wire, ["mdm.items.read"], "?start=0&length=20").List(CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task The_request_carries_the_cookie_token_as_bearer_and_the_tenant_from_the_principal()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[],\"total\":0}}");
        var controller = Controller(wire, ["mdm.items.read"], "?tenantId=evil");

        await controller.List(CancellationToken.None);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("Bearer synthetic-items-proxy", request.Authorization);
        Assert.Equal(controller.User.FindFirstValue("tenant_id"), request.TenantId);
        Assert.DoesNotContain("evil", request.Url);
    }

    // ── Export (every matching row, BL-452) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_export_reads_every_page_with_the_same_filters_and_writes_one_csv_of_the_visible_columns()
    {
        var page1 = "{\"data\":{\"items\":[" + string.Join(",", Enumerable.Range(1, 100).Select(i =>
            $"{{\"canonicalCode\":\"IT-{i:D3}\",\"name\":\"=cmd\",\"materialType\":\"EXCIPIENT\",\"lifecycleStatus\":3}}")) + "],\"total\":101}}";
        var page2 = "{\"data\":{\"items\":[{\"canonicalCode\":\"IT-101\",\"name\":\"Last\",\"materialType\":\"EXCIPIENT\",\"lifecycleStatus\":4}],\"total\":101}}";
        var wire = new RecordingHandler(HttpStatusCode.OK, page1, page2);
        var controller = Controller(wire, ["mdm.items.read"],
            "?format=csv&columns=canonicalCode,name,lifecycleStatus,secret&search=x&materialType=EXCIPIENT");

        var file = Assert.IsType<FileContentResult>(await controller.Export(CancellationToken.None));

        Assert.Equal(new[]
        {
            $"{Gateway}/api/mdm/items?page=1&pageSize=100&q=x&materialType=EXCIPIENT",
            $"{Gateway}/api/mdm/items?page=2&pageSize=100&q=x&materialType=EXCIPIENT"
        }, wire.Requests.Select(r => r.Url));
        var lines = Encoding.UTF8.GetString(file.FileContents).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => line.Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        Assert.Equal(102, lines.Length);
        // Localized headers (the echo localizer answers the key), an unknown column dropped, a formula never written.
        Assert.Equal("\"CanonicalCode\",\"Name\",\"LifecycleStatus\"", lines[0]);
        Assert.Equal("\"IT-001\",\"'=cmd\",\"LifecycleActive\"", lines[1]);
        Assert.Equal("\"IT-101\",\"Last\",\"LifecycleDeactivated\"", lines[^1]);
    }

    // K6 (CT sabotage survivor: the neutralising cut to '=' only, nothing went red). Every cell a spreadsheet would run as
    // a formula gets a leading apostrophe in the CSV the export endpoint writes; plain text is written as it is.
    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("\rcmd", "'\rcmd")]
    [InlineData("Plain name", "Plain name")]
    public async Task The_export_neutralises_every_formula_lead_and_leaves_plain_text(string name, string written)
    {
        var page = "{\"data\":{\"items\":[{\"canonicalCode\":\"IT-001\",\"name\":" + JsonSerializer.Serialize(name) + "}],\"total\":1}}";
        var wire = new RecordingHandler(HttpStatusCode.OK, page);

        var file = Assert.IsType<FileContentResult>(
            await Controller(wire, ["mdm.items.read"], "?format=csv&columns=canonicalCode,name").Export(CancellationToken.None));

        var csv = Encoding.UTF8.GetString(file.FileContents).TrimStart('\uFEFF');
        Assert.StartsWith("\"CanonicalCode\",\"Name\"", csv);
        Assert.Contains("\"IT-001\",\"" + written + "\"", csv);
    }

    [Fact]
    public async Task An_export_past_50000_rows_is_refused_with_413()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{\"data\":{\"items\":[],\"total\":50001}}");

        var result = await Controller(wire, ["mdm.items.read"], "?format=csv").Export(CancellationToken.None);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ── Create / update ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_create_sends_the_server_made_idempotency_key_and_only_MDMs_eleven_fields()
    {
        var wire = new RecordingHandler(HttpStatusCode.Created,
            "{\"isSuccessful\":true,\"statusCode\":201,\"data\":\"" + ItemId + "\"}");
        var controller = Controller(wire, ["mdm.items.create"], "");
        Assert.IsType<ViewResult>(controller.Create());
        var token = Assert.IsType<string>(controller.ViewData["ItemFormAttemptToken"]);

        var result = await controller.CreateItem(new ItemWriteInput
        {
            Name = " Paracetamol ", MaterialType = "active_ingredient", BaseUomCode = " kgm ",
            StorageConditionCodes = ["cool_2_8"], LotControlled = true, Stockable = false, ShelfLifeDays = 365,
            UomConversions = [new ItemUomConversionInput { FromUom = "kgm", ToUom = "grm", Numerator = "1000", Denominator = "1" }],
            FormAttemptToken = token
        }, CancellationToken.None);

        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, answer.StatusCode);
        using var sent = JsonDocument.Parse(Assert.Single(wire.Requests).Body!);
        var names = sent.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Equal(new HashSet<string>
        {
            "name", "description", "materialType", "baseUomCode", "uomConversions", "storageConditionCodes", "lotControlled",
            "stockable", "shelfLifeDays", "retestDays", "minRemainingShelfLifeDays", "idempotencyKey"
        }, names);
        Assert.Matches("^[0-9A-F]{64}$", sent.RootElement.GetProperty("idempotencyKey").GetString());
        Assert.Equal("ACTIVE_INGREDIENT", sent.RootElement.GetProperty("materialType").GetString());
        Assert.Equal("KGM", sent.RootElement.GetProperty("baseUomCode").GetString());
        Assert.Equal("COOL_2_8", Assert.Single(sent.RootElement.GetProperty("storageConditionCodes").EnumerateArray()).GetString());
        using var reply = JsonDocument.Parse(JsonSerializer.Serialize(answer.Value));
        Assert.Equal(ItemId, reply.RootElement.GetProperty("data").GetProperty("id").GetGuid());
        // The next attempt is a new key: a second item is a second form attempt, never the same one replayed.
        Assert.NotEqual(token, reply.RootElement.GetProperty("formAttemptToken").GetString());
    }

    [Fact]
    public async Task The_same_form_attempt_sends_the_same_key_and_a_foreign_token_is_refused_before_MDM()
    {
        var wire = new RecordingHandler(HttpStatusCode.Created,
            "{\"isSuccessful\":true,\"statusCode\":201,\"data\":\"" + ItemId + "\"}",
            "{\"isSuccessful\":true,\"statusCode\":201,\"data\":\"" + ItemId + "\"}");
        var controller = Controller(wire, ["mdm.items.create"], "");
        controller.Create();
        var token = (string)controller.ViewData["ItemFormAttemptToken"]!;
        var input = new ItemWriteInput { Name = "A", MaterialType = "EXCIPIENT", BaseUomCode = "KGM", FormAttemptToken = token };

        await controller.CreateItem(input, CancellationToken.None);
        await controller.CreateItem(input, CancellationToken.None);
        var refused = await controller.CreateItem(new ItemWriteInput { Name = "A", FormAttemptToken = "forged" }, CancellationToken.None);

        var keys = wire.Requests.Select(r => JsonDocument.Parse(r.Body!).RootElement.GetProperty("idempotencyKey").GetString()).ToArray();
        Assert.Equal(2, keys.Length);
        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<BadRequestObjectResult>(refused).StatusCode);
    }

    [Fact]
    public async Task A_create_answered_202_keeps_the_same_attempt_token_and_says_it_is_not_finished()
    {
        var wire = new RecordingHandler(HttpStatusCode.Accepted,
            "{\"isSuccessful\":false,\"statusCode\":202,\"errors\":[\"ITEM_BINDING_RECONCILIATION_REQUIRED\"]}");
        var controller = Controller(wire, ["mdm.items.create"], "");
        controller.Create();
        var token = (string)controller.ViewData["ItemFormAttemptToken"]!;

        var result = Assert.IsType<ObjectResult>(await controller.CreateItem(
            new ItemWriteInput { Name = "A", MaterialType = "EXCIPIENT", BaseUomCode = "KGM", FormAttemptToken = token },
            CancellationToken.None));

        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        using var reply = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(token, reply.RootElement.GetProperty("formAttemptToken").GetString());
        Assert.Equal("RefusalCreatePending", Assert.Single(reply.RootElement.GetProperty("errors").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task The_update_sends_the_expected_version_with_the_fields_and_without_a_version_MDM_is_never_asked()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, ItemDtoEnvelope(version: 5, status: 1));
        var controller = Controller(wire, ["mdm.items.update"], "");

        var refused = await controller.UpdateItem(ItemId, new ItemWriteInput { Name = "B" }, CancellationToken.None);
        var result = await controller.UpdateItem(ItemId, new ItemWriteInput
        {
            Name = "B", MaterialType = "EXCIPIENT", BaseUomCode = "KGM", ExpectedVersion = 4, FormAttemptToken = "ignored"
        }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(refused).StatusCode);
        var request = Assert.Single(wire.Requests);
        Assert.Equal("PUT", request.Method);
        Assert.Equal($"{Gateway}/api/mdm/items/{ItemId:D}", request.Url);
        using var sent = JsonDocument.Parse(request.Body!);
        Assert.Equal(4, sent.RootElement.GetProperty("expectedVersion").GetInt32());
        Assert.False(sent.RootElement.TryGetProperty("formAttemptToken", out _));
        Assert.False(sent.RootElement.TryGetProperty("idempotencyKey", out _));
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, string, string> LifecycleCases => new()
    {
        { "Deactivate", "mdm.items.deactivate", "deactivate" },
        { "Retire", "mdm.items.retire", "retire" },
        { "RequestReactivation", "mdm.items.submit", "reactivation-requests" }
    };

    [Theory]
    [MemberData(nameof(LifecycleCases))]
    public async Task A_lifecycle_request_posts_the_expected_version_and_the_reason_to_MDM(string action, string permission, string suffix)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, ItemDtoEnvelope(version: 8, status: 4));
        var controller = Controller(wire, [permission], "");

        var result = await Invoke(controller, action, ItemId, 7, "  Supplier stopped  ");

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(wire.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal($"{Gateway}/api/mdm/items/{ItemId:D}/{suffix}", request.Url);
        using var sent = JsonDocument.Parse(request.Body!);
        Assert.Equal(7, sent.RootElement.GetProperty("expectedVersion").GetInt32());
        Assert.Equal("Supplier stopped", sent.RootElement.GetProperty("reason").GetString());
    }

    [Theory]
    [MemberData(nameof(LifecycleCases))]
    public async Task A_lifecycle_request_without_a_reason_or_a_version_never_reaches_MDM(string action, string permission, string suffix)
    {
        _ = suffix;
        var wire = new RecordingHandler(HttpStatusCode.OK, "{}");
        var controller = Controller(wire, [permission], "");

        var noReason = await Invoke(controller, action, ItemId, 7, "  ");
        var noVersion = await Invoke(controller, action, ItemId, null, "reason");
        var lineBreak = await Invoke(controller, action, ItemId, 7, "a\nb");

        foreach (var result in new[] { noReason, noVersion, lineBreak })
            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Fact]
    public async Task The_submit_posts_the_expected_version_without_a_reason()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, ItemDtoEnvelope(version: 2, status: 2));

        await Controller(wire, ["mdm.items.submit"], "").Submit(ItemId, 1, CancellationToken.None);

        using var sent = JsonDocument.Parse(Assert.Single(wire.Requests).Body!);
        Assert.Equal(1, sent.RootElement.GetProperty("expectedVersion").GetInt32());
        Assert.False(sent.RootElement.TryGetProperty("reason", out _));
    }

    [Fact]
    public void Every_write_endpoint_validates_the_antiforgery_token()
    {
        foreach (var name in new[] { "CreateItem", "UpdateItem", "Submit", "RequestReactivation", "Deactivate", "Retire" })
        {
            var method = typeof(ItemsController).GetMethod(name)!;
            Assert.True(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null, name);
        }
    }

    // ── Refusals ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_activation_approval_refusal_is_a_503_sentence_in_the_readers_language()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        var wire = new RecordingHandler(HttpStatusCode.ServiceUnavailable,
            "{\"isSuccessful\":false,\"statusCode\":503,\"errors\":[\"ITEM_ACTIVATION_APPROVAL_UNAVAILABLE\"]}");
        using var scope = factory.Services.CreateScope();
        var controller = Controller(wire, ["mdm.items.submit"], "",
            scope.ServiceProvider.GetRequiredService<IStringLocalizer<ItemsIndex>>());

        var result = Assert.IsType<ObjectResult>(await controller.Submit(ItemId, 1, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        var text = JsonSerializer.Serialize(result.Value);
        Assert.Contains("Activation approval is not available yet", text);
        Assert.DoesNotContain("ITEM_ACTIVATION_APPROVAL_UNAVAILABLE", text);
        Assert.DoesNotContain("unmapped", text);
    }

    [Theory]
    [InlineData(422, "[\"UOM_CONVERSION_INVALID\",\"SAME_UOM\"]", "RefusalUomConversionSameUnit")]
    [InlineData(422, "[\"ITEM_STORAGE_CONDITION_CONFLICT\",\"MORE_THAN_ONE_TEMPERATURE\"]", "RefusalStorageTwoTemperatures")]
    [InlineData(422, "[\"STOCK_BEHAVIOUR_INCOMPLETE\"]", "RefusalStockBehaviourIncomplete")]
    [InlineData(409, "[\"ITEM_NAME_CONFLICT\"]", "RefusalNameConflict")]
    [InlineData(503, "[\"ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE\",\"STOCK_CHECK_BUDGET_EXHAUSTED\"]", "RefusalRetireStockCheckUnavailable")]
    [InlineData(400, "[\"INVALID_PARAMETER\"]", "RefusalListParameter")]
    public async Task Every_refusal_is_its_sentence_and_its_status_is_kept(int status, string errors, string sentenceKey)
    {
        var wire = new RecordingHandler((HttpStatusCode)status, $"{{\"isSuccessful\":false,\"statusCode\":{status},\"errors\":{errors}}}");

        var result = Assert.IsType<ObjectResult>(await Controller(wire, ["mdm.items.read"], "").Detail(ItemId, CancellationToken.None));

        Assert.Equal(status, result.StatusCode);
        using var reply = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(sentenceKey, Assert.Single(reply.RootElement.GetProperty("errors").EnumerateArray()).GetString());
        Assert.False(reply.RootElement.TryGetProperty("unmapped", out _));
    }

    [Fact]
    public async Task A_named_detail_is_said_by_its_label_and_a_value_shaped_detail_is_never_a_code()
    {
        var wire = new RecordingHandler(HttpStatusCode.Conflict,
            "{\"errors\":[\"ITEM_CRITICAL_FIELD_LOCKED\",\"ShelfLifeDays\"]}",
            "{\"errors\":[\"ITEM_REFERENCE_VALUE_INVALID\",\"item-uom:XYZ,storage-condition:HOT:NOT_SELECTABLE\"]}");
        var controller = Controller(wire, ["mdm.items.read"], "");

        var locked = JsonSerializer.Serialize(Assert.IsType<ObjectResult>(await controller.Detail(ItemId, CancellationToken.None)).Value);
        var reference = JsonSerializer.Serialize(Assert.IsType<ObjectResult>(await controller.Detail(ItemId, CancellationToken.None)).Value);

        // The echo localizer formats nothing: the named sentence key is what was chosen.
        Assert.Contains("RefusalCriticalFieldLockedNamed", locked);
        Assert.Contains("RefusalReferenceValueInvalidNamed", reference);
        Assert.DoesNotContain("unmapped", locked + reference);
    }

    [Fact]
    public async Task An_unknown_code_is_the_status_sentence_flagged_unmapped_and_never_shown()
    {
        var wire = new RecordingHandler(HttpStatusCode.UnprocessableEntity, "{\"errors\":[\"ITEM_SOMETHING_NEW\"]}");

        var result = Assert.IsType<ObjectResult>(await Controller(wire, ["mdm.items.read"], "").Detail(ItemId, CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        var text = JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"unmapped\":true", text);
        Assert.Contains("ErrorValidation", text);
        Assert.DoesNotContain("ITEM_SOMETHING_NEW", text);
    }

    [Fact]
    public void The_code_rule_is_the_gsku_bridges_rule()
    {
        Assert.True(ItemsController.IsCode("ITEM_NOT_FOUND"));
        Assert.False(ItemsController.IsCode("KGM"));
        Assert.False(ItemsController.IsCode("ShelfLifeDays"));
        Assert.False(ItemsController.IsCode("item-uom:XYZ"));
        Assert.False(ItemsController.IsCode("_X_"));
    }

    // ── Access (UAS-001) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_json_endpoint_without_its_key_answers_a_403_body_and_never_reaches_MDM()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, "{}");
        var reader = Controller(wire, ["mdm.items.read"], "");
        var nobody = Controller(wire, ["mdm.gskus.read"], "");

        var results = new (string Name, IActionResult Result)[]
        {
            ("List", await nobody.List(CancellationToken.None)),
            ("Export", await nobody.Export(CancellationToken.None)),
            ("Detail", await nobody.Detail(ItemId, CancellationToken.None)),
            ("Create", await reader.CreateItem(new ItemWriteInput(), CancellationToken.None)),
            ("Update", await reader.UpdateItem(ItemId, new ItemWriteInput { ExpectedVersion = 1 }, CancellationToken.None)),
            ("Submit", await reader.Submit(ItemId, 1, CancellationToken.None)),
            ("Reactivate", await reader.RequestReactivation(ItemId, 1, "r", CancellationToken.None)),
            ("Deactivate", await reader.Deactivate(ItemId, 1, "r", CancellationToken.None)),
            ("Retire", await reader.Retire(ItemId, 1, "r", CancellationToken.None)),
            ("Options (read key)", await reader.Options(CancellationToken.None)),
            ("Options (other read key)", await nobody.Options(CancellationToken.None))
        };

        foreach (var (name, result) in results)
            Assert.True(Assert.IsType<ObjectResult>(result).StatusCode == StatusCodes.Status403Forbidden, name);
        Assert.Empty(wire.Requests);
    }

    // The keys a page needs, comma-separated (S5-FIX1: the Edit page needs the update AND the read key).
    public static TheoryData<string, string, string> PageCases => new()
    {
        { "Index", "mdm.items.read", ItemsController.IndexView },
        { "Create", "mdm.items.create", ItemsController.CreateView },
        { "Edit", "mdm.items.update,mdm.items.read", ItemsController.EditView },
        { "Details", "mdm.items.read", ItemsController.DetailsView }
    };

    [Theory]
    [MemberData(nameof(PageCases))]
    public void A_page_without_its_key_answers_403_with_its_own_view_never_a_redirect(string page, string permission, string view)
    {
        var denied = Controller(new RecordingHandler(HttpStatusCode.OK, "{}"), ["mdm.gskus.read"], "");
        var allowed = Controller(new RecordingHandler(HttpStatusCode.OK, "{}"), permission.Split(','), "");

        var deniedResult = InvokePage(denied, page);
        var allowedResult = InvokePage(allowed, page);

        Assert.Equal(StatusCodes.Status403Forbidden, denied.Response.StatusCode);
        Assert.Equal(view, Assert.IsType<ViewResult>(deniedResult).ViewName);
        Assert.Null(denied.ViewData["ItemFormAttemptToken"]);
        Assert.NotEqual(StatusCodes.Status403Forbidden, allowed.Response.StatusCode);
        Assert.Equal(view, Assert.IsType<ViewResult>(allowedResult).ViewName);
    }

    [Theory]
    [MemberData(nameof(PageCases))]
    public async Task With_the_layout_the_unauthorized_face_draws_nothing_of_the_page_and_the_permitted_reader_gets_it(
        string page, string permission, string view)
    {
        var path = view.TrimStart('~');
        var values = page == "Index" ? null : new Dictionary<string, object?>();
        var keys = permission.Split(',');
        var denied = await LayoutRenderer.RenderAsync(factory, path, "Items", "mdm.gskus.read", values);
        var reader = await LayoutRenderer.RenderAsync(factory, path, "Items", keys[0], values, keys[1..]);

        Assert.Contains("diten-access-denied", denied);
        foreach (var marker in new[] { "items-l10n", "Items/index.js", "dt-items", "skeleton-loader", "itemForm", "itemDetails", "filterMaterialType" })
            Assert.DoesNotContain(marker, denied);

        Assert.DoesNotContain("diten-access-denied", reader);
        Assert.Contains("items-l10n", reader);
        Assert.Contains("Items/index.js", reader);
        Assert.Contains(page switch
        {
            "Index" => "id=\"dt-items\"",
            "Details" => "id=\"itemDetails\"",
            _ => "id=\"itemForm\""
        }, reader);
    }

    // S5-FIX1 — sabotage target "the Edit page does not ask the read key": an update key alone draws no form skeleton
    // (the form reads the item before it can be edited). 403 with the page's own view and the access-denied face.
    [Fact]
    public async Task The_Edit_page_without_the_read_key_answers_403_and_draws_no_form()
    {
        var updateOnly = Controller(new RecordingHandler(HttpStatusCode.OK, "{}"), ["mdm.items.update"], "");

        var result = Assert.IsType<ViewResult>(updateOnly.Edit(ItemId));
        var page = await LayoutRenderer.RenderAsync(factory, ItemsController.EditView.TrimStart('~'), "Items", "mdm.items.update",
            new Dictionary<string, object?>());

        Assert.Equal(StatusCodes.Status403Forbidden, updateOnly.Response.StatusCode);
        Assert.Equal(ItemsController.EditView, result.ViewName);
        Assert.Contains("diten-access-denied", page);
        foreach (var marker in new[] { "itemForm", "items-l10n", "Items/index.js", "reference-option-labels" })
            Assert.DoesNotContain(marker, page);
    }

    // ── Options (S5b, BL-600) ───────────────────────────────────────────────────────────────────────────────────

    private const string OptionsAnswer =
        "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"catalogVersions\":{\"materialTypes\":\"MT-V3\",\"uoms\":\"ITEM-UNIVERSAL-V2\",\"storageConditions\":\"SC-V1\",\"extra\":\"dropped\"}," +
        "\"materialTypes\":[{\"code\":\"EXCIPIENT\",\"displayName\":\"Excipient\",\"sortOrder\":2,\"attributes\":{}}," +
        "{\"code\":\"ACTIVE_INGREDIENT\",\"displayName\":\"API\",\"sortOrder\":1,\"attributes\":{}}]," +
        "\"uoms\":[{\"code\":\"KGM\",\"displayName\":\"kilogram\",\"sortOrder\":5,\"attributes\":{\"DimensionCode\":\"MASS\",\"MaximumDecimalPrecision\":\"3\"}}," +
        "{\"code\":\"C62\",\"displayName\":\"one\",\"sortOrder\":1,\"attributes\":{\"DimensionCode\":\"COUNT\"}}]," +
        "\"storageConditions\":[{\"code\":\"COOL_2_8\",\"displayName\":\"Cool\",\"sortOrder\":2,\"attributes\":{\"ConditionKind\":\"TEMPERATURE\",\"TemperatureMinC\":\"2\",\"TemperatureMaxC\":\"8\"}}," +
        "{\"code\":\"PROTECT_FROM_LIGHT\",\"displayName\":\"Light\",\"sortOrder\":4,\"attributes\":{\"ConditionKind\":\"PROTECTION\",\"ProtectFromLight\":null}}]}}";

    [Theory]
    [InlineData("mdm.items.create")]
    [InlineData("mdm.items.update")]
    [InlineData("mdm.gskus.update")]
    [InlineData("mdm.lskus.create")]
    [InlineData("mdm.finished-goods.create")]
    public async Task The_options_read_is_open_to_each_form_key_and_forwards_the_callers_token_and_tenant(string permission)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, OptionsAnswer);
        var controller = Controller(wire, [permission], "?tenantId=evil");

        var result = Assert.IsType<ContentResult>(await controller.Options(CancellationToken.None));

        var request = Assert.Single(wire.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal($"{Gateway}/api/mdm/items/options", request.Url);
        Assert.Equal("Bearer synthetic-items-proxy", request.Authorization);
        Assert.Equal(controller.User.FindFirstValue("tenant_id"), request.TenantId);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    [Fact]
    public async Task The_options_are_passed_on_as_codes_in_their_order_with_their_attributes_never_the_english_names()
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, OptionsAnswer);

        var result = Assert.IsType<ContentResult>(await Controller(wire, ["mdm.items.create"], "").Options(CancellationToken.None));

        using var body = JsonDocument.Parse(result.Content!);
        var data = body.RootElement.GetProperty("data");
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        // S5b-FIX2: one catalog version per list, passed on as catalogVersions — only its three strings.
        var versions = data.GetProperty("catalogVersions");
        Assert.Equal("MT-V3", versions.GetProperty("materialTypes").GetString());
        Assert.Equal("ITEM-UNIVERSAL-V2", versions.GetProperty("uoms").GetString());
        Assert.Equal("SC-V1", versions.GetProperty("storageConditions").GetString());
        Assert.Equal(3, versions.EnumerateObject().Count());
        Assert.False(data.TryGetProperty("catalogVersion", out _));
        Assert.Equal(new[] { "ACTIVE_INGREDIENT", "EXCIPIENT" }, data.GetProperty("materialTypes").EnumerateArray().Select(o => o.GetProperty("code").GetString()!).ToArray());
        Assert.Equal(new[] { "C62", "KGM" }, data.GetProperty("uoms").EnumerateArray().Select(o => o.GetProperty("code").GetString()!).ToArray());
        var cool = data.GetProperty("storageConditions").EnumerateArray().First();
        Assert.Equal("TEMPERATURE", cool.GetProperty("attributes").GetProperty("ConditionKind").GetString());
        Assert.Equal("MASS", data.GetProperty("uoms")[1].GetProperty("attributes").GetProperty("DimensionCode").GetString());
        // The screen labels by stable code (seven languages); MDM's English display name is not passed on.
        Assert.DoesNotContain("displayName", result.Content!);
        Assert.DoesNotContain("kilogram", result.Content!);
    }

    [Theory]
    [InlineData("mdm.items.read")]
    [InlineData("mdm.gskus.read")]
    [InlineData("mdm.finished-goods.read")]
    public async Task The_options_read_without_a_form_key_is_a_403_sentence_and_never_reaches_MDM(string permission)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, OptionsAnswer);

        var result = Assert.IsType<ObjectResult>(await Controller(wire, [permission], "").Options(CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Contains("ErrorForbidden", JsonSerializer.Serialize(result.Value));
        Assert.Empty(wire.Requests);
    }

    // An outage is a refusal by name with its status — never an empty list (no data, no items).
    [Theory]
    [InlineData(503, "REFERENCE_PROVIDER_UNAVAILABLE", 503, "RefusalOptionsUnavailable")]
    [InlineData(503, "REFERENCE_PROVIDER_TIMEOUT", 503, "RefusalOptionsTimeout")]
    [InlineData(504, "REFERENCE_PROVIDER_TIMEOUT", 504, "RefusalOptionsTimeout")]
    [InlineData(503, "UNTRUSTED_ANSWER", 503, "RefusalOptionsUnavailable")]
    [InlineData(503, "REFERENCE_PROVIDER_CONFIGURATION_INVALID", 503, "RefusalOptionsUnavailable")]
    [InlineData(503, "ITEM_REFERENCE_PROVIDER_NOT_AVAILABLE", 503, "RefusalOptionsUnavailable")]
    [InlineData(503, "REFERENCE_SOMETHING_PLATFORM_ADDS", 503, "RefusalOptionsUnavailable")]
    [InlineData(401, "REFERENCE_UNAUTHENTICATED", 401, "ErrorUnauthorized")]
    [InlineData(403, "FORBIDDEN", 403, "ErrorForbidden")]
    public async Task An_options_refusal_keeps_its_status_and_is_its_sentence_never_an_empty_list(int status, string code, int expected,
        string sentenceKey)
    {
        var wire = new RecordingHandler((HttpStatusCode)status,
            $"{{\"isSuccessful\":false,\"statusCode\":{status},\"errors\":[\"{code}\"]}}");

        var result = Assert.IsType<ObjectResult>(await Controller(wire, ["mdm.gskus.update"], "").Options(CancellationToken.None));

        Assert.Equal(expected, result.StatusCode);
        var text = JsonSerializer.Serialize(result.Value);
        using var reply = JsonDocument.Parse(text);
        Assert.Equal(sentenceKey, Assert.Single(reply.RootElement.GetProperty("errors").EnumerateArray()).GetString());
        Assert.False(reply.RootElement.TryGetProperty("data", out _));
        Assert.DoesNotContain(code, text);
    }

    [Theory]
    [InlineData("{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"materialTypes\":[{\"code\":\"EXCIPIENT\"}],\"uoms\":[],\"storageConditions\":[{\"code\":\"COOL_2_8\"}]}}")]
    [InlineData("{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"materialTypes\":[{\"code\":\"EXCIPIENT\"}],\"uoms\":[{\"code\":\"kg <b>\"}],\"storageConditions\":[{\"code\":\"COOL_2_8\"}]}}")]
    [InlineData("{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"uoms\":[{\"code\":\"KGM\"}]}}")]
    // S5b-FIX2: a code with a trailing newline is not code-shaped ("$" would have let "KGM\n" through; the pattern ends \z).
    [InlineData("{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"materialTypes\":[{\"code\":\"EXCIPIENT\"}],\"uoms\":[{\"code\":\"KGM\\n\"}],\"storageConditions\":[{\"code\":\"COOL_2_8\"}]}}")]
    [InlineData("{\"isSuccessful\":false,\"statusCode\":200}")]
    [InlineData("not json")]
    public async Task Lists_that_cannot_be_offered_are_a_gateway_failure_never_an_empty_list(string answer)
    {
        var wire = new RecordingHandler(HttpStatusCode.OK, answer);

        var result = Assert.IsType<ObjectResult>(await Controller(wire, ["mdm.items.create"], "").Options(CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.DoesNotContain("\"data\"", JsonSerializer.Serialize(result.Value));
    }

    // S5b-FIX2: a list whose code ends in a newline ("KGM\n") is answered 502 by name — the case pinned on its own so a
    // revert of the \z anchor is red under this test's name.
    [Fact]
    public async Task A_code_with_a_trailing_newline_is_not_code_shaped_and_the_options_answer_is_502()
    {
        var answer = OptionsAnswer.Replace("\"code\":\"KGM\"", "\"code\":\"KGM\\n\"", StringComparison.Ordinal);
        Assert.Contains("\"KGM\\n\"", answer, StringComparison.Ordinal);
        var wire = new RecordingHandler(HttpStatusCode.OK, answer);

        var result = Assert.IsType<ObjectResult>(await Controller(wire, ["mdm.items.create"], "").Options(CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.DoesNotContain("\"data\"", JsonSerializer.Serialize(result.Value));
    }

    // S5b-FIX2: catalogVersions missing, not an object, or with a non-string version never fails the lists.
    [Theory]
    [InlineData("", null, null, null, true)]
    [InlineData("\"catalogVersions\":\"v7\",", null, null, null, true)]
    [InlineData("\"catalogVersions\":{\"materialTypes\":7,\"uoms\":\"U-V2\",\"storageConditions\":null},", null, "U-V2", null, false)]
    public async Task An_odd_or_missing_catalog_versions_is_null_and_the_lists_are_still_offered(string versionsJson,
        string? materialTypes, string? uoms, string? storageConditions, bool wholeNull)
    {
        var answer = OptionsAnswer[..OptionsAnswer.IndexOf("\"catalogVersions\"", StringComparison.Ordinal)] + versionsJson
            + OptionsAnswer[OptionsAnswer.IndexOf("\"materialTypes\":[", StringComparison.Ordinal)..];
        var wire = new RecordingHandler(HttpStatusCode.OK, answer);

        var result = Assert.IsType<ContentResult>(await Controller(wire, ["mdm.items.create"], "").Options(CancellationToken.None));

        using var body = JsonDocument.Parse(result.Content!);
        var data = body.RootElement.GetProperty("data");
        Assert.Equal(2, data.GetProperty("uoms").GetArrayLength());
        var versions = data.GetProperty("catalogVersions");
        if (wholeNull)
        {
            Assert.Equal(JsonValueKind.Null, versions.ValueKind);
            return;
        }
        Assert.Equal(materialTypes, versions.GetProperty("materialTypes").GetString());
        Assert.Equal(uoms, versions.GetProperty("uoms").GetString());
        Assert.Equal(storageConditions, versions.GetProperty("storageConditions").GetString());
    }

    [Fact]
    public async Task An_unreachable_or_slow_gateway_is_the_lists_sentence_with_503_or_504()
    {
        var down = await Controller(new ThrowingHandler(new HttpRequestException("down")), ["mdm.items.create"], "").Options(CancellationToken.None);
        var slow = await Controller(new ThrowingHandler(new TaskCanceledException("slow")), ["mdm.items.create"], "").Options(CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(down).StatusCode);
        Assert.Contains("RefusalOptionsUnavailable", JsonSerializer.Serialize(((ObjectResult)down).Value));
        Assert.Equal(StatusCodes.Status504GatewayTimeout, Assert.IsType<ObjectResult>(slow).StatusCode);
        Assert.Contains("RefusalOptionsTimeout", JsonSerializer.Serialize(((ObjectResult)slow).Value));
    }

    // Sabotage target "the form falls back to a code input when the list is missing": the rendered item form (production
    // Razor with its layout) offers every list field as a select — none of them is a text box a code could be typed in.
    [Theory]
    [InlineData("Create", "mdm.items.create")]
    [InlineData("Edit", "mdm.items.update,mdm.items.read")]
    public async Task The_item_form_offers_every_list_as_a_select_never_as_a_code_input(string page, string permission)
    {
        var keys = permission.Split(',');
        var view = page == "Create" ? ItemsController.CreateView : ItemsController.EditView;
        var html = await LayoutRenderer.RenderAsync(factory, view.TrimStart('~'), "Items", keys[0], new Dictionary<string, object?>(), keys[1..]);

        foreach (var name in new[] { "MaterialType", "BaseUomCode", "StorageConditionCodes" })
        {
            Assert.Matches($"<select[^>]*name=\"{name}\"", html);
            Assert.DoesNotMatch($"<input[^>]*name=\"{name}\"", html);
        }
        Assert.Matches("<select[^>]*name=\"StorageConditionCodes\"[^>]*multiple|<select[^>]*multiple[^>]*name=\"StorageConditionCodes\"", html);
        Assert.Matches("<select[^>]*js-conversion-from", html);
        Assert.Matches("<select[^>]*js-conversion-to", html);
        Assert.DoesNotMatch("<input[^>]*js-conversion-(from|to)", html);
        // The labels by code (seven languages) are on the page for the script; the material types are no fixed list.
        Assert.Contains("id=\"reference-option-labels\"", html);
        Assert.DoesNotContain("value=\"EXCIPIENT\"", html);
        // S5b-FIX2: the Create form's own "no item until the list arrives" reason reaches the page with the labels.
        Assert.Contains("ptionsUnavailableCreate\":", html, StringComparison.Ordinal);
    }

    // S5c: the Details page carries the unit / storage-condition names by code (seven languages) for the script that shows
    // the record's units and storage conditions by name — the same vocabulary as the forms.
    [Fact]
    public async Task S5c_The_details_page_carries_the_unit_and_storage_names_by_code()
    {
        var html = await LayoutRenderer.RenderAsync(factory, ItemsController.DetailsView.TrimStart('~'), "Items", "mdm.items.read",
            new Dictionary<string, object?>());

        Assert.Contains("id=\"reference-option-labels\"", html);
        Assert.Contains("Uom_KGM", html);
        Assert.Contains("Storage_COOL_2_8", html);
    }

    // ── Fixture ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static string ItemDtoEnvelope(int version, int status) =>
        "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"id\":\"" + ItemId + "\",\"version\":" + version
        + ",\"lifecycleStatus\":" + status + "}}";

    private static Task<IActionResult> Invoke(ItemsController controller, string action, Guid id, int? version, string? reason) =>
        action switch
        {
            "Deactivate" => controller.Deactivate(id, version, reason, CancellationToken.None),
            "Retire" => controller.Retire(id, version, reason, CancellationToken.None),
            _ => controller.RequestReactivation(id, version, reason, CancellationToken.None)
        };

    private static IActionResult InvokePage(ItemsController controller, string page) => page switch
    {
        "Index" => controller.Index(),
        "Create" => controller.Create(),
        "Edit" => controller.Edit(ItemId),
        _ => controller.Details(ItemId)
    };

    private static ItemsController Controller(HttpMessageHandler wire, string[] permissions, string query,
        IStringLocalizer<ItemsIndex>? localizer = null)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("sub", "21111111-2222-3333-4444-555555555555"),
                .. permissions.Select(permission => new Claim("permission", permission))
            ], "fixture"))
        };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-items-proxy";
        context.Request.QueryString = new QueryString(query);
        return new ItemsController(new HttpClient(wire), gateway, new EphemeralDataProtectionProvider(),
            localizer ?? new EchoLocalizer(), NullLogger<ItemsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed record Recorded(string Method, string Url, string? Body, string? Authorization, string? TenantId);

    private sealed class RecordingHandler(HttpStatusCode status, params string[] bodies) : HttpMessageHandler
    {
        private int _answered;
        public List<Recorded> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Recorded(request.Method.Method, request.RequestUri!.OriginalString,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null));
            var body = bodies[Math.Min(_answered++, bodies.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class EchoLocalizer : IStringLocalizer<ItemsIndex>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
