using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.CycleCapacities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-SB-3-UIa — the strategy template form on the SB-3a contract, measured on the REAL controllers and assets:
/// <list type="bullet">
/// <item>a product line round-trips its <c>role</c> + <c>journeyId</c> (and keeps every older field) from the CRM read,
/// through the Edit page's <c>ProductLinesJson</c>, into the CRM write;</item>
/// <item><c>api/line-journeys</c> offers only the tenant's PUBLISHED journeys whose subject's primary global product is
/// the line's product (draft / archived / another product never), with the JWT tenant;</item>
/// <item>template-level content bindings can no longer be added, only removed;</item>
/// <item>the SB-3a refusals are shown, localised, under the line they name;</item>
/// <item>the cycle capacity form posts the per-visit product ceilings only when filled (1..10).</item>
/// </list>
/// </summary>
public sealed class StrategyTemplateRolesJourneysWebTests
{
    private const string GatewayUrl = "http://gateway.test";
    private const string Manage = "crm.strategy-template.manage";
    private const string JourneyRead = "crm.knowledge.content-engagement-journey.read";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid TemplateId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid ProductB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private static readonly Guid JourneyA = Guid.Parse("c0000000-0000-0000-0000-00000000000a");
    private static readonly Guid SubjectA = Guid.Parse("d0000000-0000-0000-0000-00000000000a");
    private static readonly Guid SubjectB = Guid.Parse("d0000000-0000-0000-0000-00000000000b");
    private static readonly Guid SubjectNoPrimary = Guid.Parse("d0000000-0000-0000-0000-0000000000cc");
    private static readonly Guid GskuA = Guid.Parse("e0000000-0000-0000-0000-00000000000a");

    // ============================================================ ProductLinesJson round trip

    [Fact]
    public async Task A_product_line_round_trips_role_and_journey_and_keeps_its_older_fields()
    {
        var gateway = new Gateway((method, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/strategy-templates/contract") => (HttpStatusCode.OK, ContractJson),
            _ when method == HttpMethod.Get && uri.EndsWith($"/api/crm/strategy-templates/{TemplateId}") => (HttpStatusCode.OK, DetailJson),
            _ when method == HttpMethod.Put => (HttpStatusCode.OK, """{"data":true,"isSuccessful":true}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = StrategyController(gateway, Manage);

        // Read: the Edit page seeds ProductLinesJson from the CRM detail.
        var view = Assert.IsType<ViewResult>(await controller.Edit(TemplateId, default));
        var model = Assert.IsType<StrategyTemplateEditViewModel>(view.Model);
        var seeded = JsonDocument.Parse(model.ProductLinesJson!).RootElement[0];
        Assert.Equal("non-promo", seeded.GetProperty("role").GetString());
        Assert.Equal(JourneyA, seeded.GetProperty("journeyId").GetGuid());

        // Write: the same JSON (what form.js posts back untouched) reaches the CRM update.
        await controller.Edit(TemplateId, model, default);
        var put = Assert.Single(gateway.Requests, r => r.Method == HttpMethod.Put);
        var line = JsonDocument.Parse(put.Body!).RootElement.GetProperty("productLines")[0];
        Assert.Equal("non-promo", line.GetProperty("role").GetString());
        Assert.Equal(JourneyA, line.GetProperty("journeyId").GetGuid());
        // … and nothing older was lost on the way.
        Assert.Equal(ProductA, line.GetProperty("globalProductId").GetGuid());
        Assert.Equal("GP-A", line.GetProperty("globalProductCodeDisplay").GetString());
        Assert.Equal(60m, line.GetProperty("lineWeightPercentage").GetDecimal());
        Assert.Equal("sku-allocated", line.GetProperty("skuAllocationMode").GetString());
        Assert.Equal(GskuA, line.GetProperty("skuAllocations")[0].GetProperty("gskuId").GetGuid());
        Assert.Equal(10, line.GetProperty("sortOrder").GetInt32());
        Assert.Equal("keep me", line.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task A_new_line_posted_by_the_form_reaches_the_crm_create_with_role_and_journey()
    {
        var gateway = new Gateway((method, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/strategy-templates/contract") => (HttpStatusCode.OK, ContractJson),
            _ when method == HttpMethod.Post => (HttpStatusCode.OK, $$"""{"data":"{{TemplateId}}","isSuccessful":true}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = StrategyController(gateway, Manage);
        var model = NewModel($$"""[{"globalProductId":"{{ProductB}}","globalProductCodeDisplay":"GP-B","skuAllocationMode":"product-only","lineWeightPercentage":100,"skuAllocations":[],"sortOrder":0,"notes":null,"role":"promo","journeyId":"{{JourneyA}}"}]""");

        await controller.Create(model, default);

        var post = Assert.Single(gateway.Requests, r => r.Method == HttpMethod.Post);
        var line = JsonDocument.Parse(post.Body!).RootElement.GetProperty("productLines")[0];
        Assert.Equal("promo", line.GetProperty("role").GetString());
        Assert.Equal(JourneyA, line.GetProperty("journeyId").GetGuid());
    }

    // ============================================================ api/line-journeys

    [Fact]
    public async Task Line_journeys_offer_only_published_journeys_of_the_lines_product()
    {
        var gateway = JourneyGateway();
        var controller = StrategyController(gateway, JourneyRead);

        var data = Data(await controller.LineJourneys(ProductA, default));

        Assert.True(data.GetProperty("productFilterApplied").GetBoolean());
        // Only J-A-PUB: J-A-DRAFT is a draft, J-A-ARCH archived, J-B-PUB tells product B, J-NOPRIMARY's subject has a
        // non-primary global-product link only (not "the chain's product").
        Assert.Equal(["J-A-PUB"], data.GetProperty("items").EnumerateArray().Select(j => j.GetProperty("journeyCode").GetString() ?? "").ToArray());
        var item = data.GetProperty("items")[0];
        Assert.Equal(JourneyA, item.GetProperty("journeyId").GetGuid());
        Assert.Equal("tr", item.GetProperty("languageCode").GetString());
    }

    [Fact]
    public async Task Line_journeys_read_the_tenant_from_the_token_and_ask_crm_for_published_journeys_only()
    {
        var gateway = JourneyGateway();
        var controller = StrategyController(gateway, JourneyRead);
        // A tenant smuggled in the query string never reaches the CRM: the reads are composed here, not forwarded.
        controller.ControllerContext.HttpContext.Request.QueryString = new QueryString($"?productId={ProductA}&tenantId={Guid.NewGuid()}");

        await controller.LineJourneys(ProductA, default);

        Assert.Equal(
            [
                $"{GatewayUrl}/api/crm/knowledge/content-engagement-journeys?status=published&includeArchived=false",
                $"{GatewayUrl}/api/crm/knowledge/subjects?includeArchived=false"
            ],
            gateway.Requests.Select(r => r.Uri).ToArray());
        Assert.All(gateway.Requests, r => Assert.Equal(TenantId.ToString(), r.Tenant));
    }

    [Fact]
    public async Task Line_journeys_without_a_product_are_an_empty_list_and_cost_no_gateway_call()
    {
        var gateway = JourneyGateway();
        var controller = StrategyController(gateway, JourneyRead);

        var data = Data(await controller.LineJourneys(null, default));

        Assert.Empty(data.GetProperty("items").EnumerateArray());
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Line_journeys_for_a_product_without_a_published_journey_are_empty()
    {
        var controller = StrategyController(JourneyGateway(), JourneyRead);

        var data = Data(await controller.LineJourneys(Guid.NewGuid(), default));

        Assert.Empty(data.GetProperty("items").EnumerateArray());
        Assert.True(data.GetProperty("productFilterApplied").GetBoolean());
    }

    [Fact]
    public async Task Line_journeys_say_so_when_the_product_match_cannot_be_read()
    {
        // No crm.knowledge.subject.read: published journeys come back unfiltered and flagged — CRM checks at save.
        var gateway = JourneyGateway(subjectsStatus: HttpStatusCode.Forbidden);
        var controller = StrategyController(gateway, JourneyRead);

        var data = Data(await controller.LineJourneys(ProductA, default));

        Assert.False(data.GetProperty("productFilterApplied").GetBoolean());
        var codes = data.GetProperty("items").EnumerateArray().Select(j => j.GetProperty("journeyCode").GetString()).ToArray();
        Assert.DoesNotContain("J-A-DRAFT", codes);
        Assert.DoesNotContain("J-A-ARCH", codes);
        Assert.Contains("J-B-PUB", codes);
    }

    [Fact]
    public async Task Line_journeys_need_the_journey_read_permission()
    {
        var gateway = JourneyGateway();
        var controller = StrategyController(gateway, "crm.strategy-template.read");

        var result = await controller.LineJourneys(ProductA, default);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    // ============================================================ retired template-level content bindings

    [Fact]
    public void The_form_has_no_way_left_to_add_a_template_level_content_binding()
    {
        var script = FormScript();
        Assert.DoesNotContain("state.contents.push(", script);
        Assert.DoesNotContain("js-content-check", script);
        var view = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "StrategyTemplates", "_Form.cshtml"));
        Assert.DoesNotContain("AddContentBinding", view);
        // A retired binding can still be REMOVED on a draft (data-row="content" → splice) and the list posts what is left.
        Assert.Contains("row.dataset.row === 'content') { state.contents.splice(index, 1)", script);
        Assert.Contains("el('ContentBindingsJson').value = frozen ? '' : JSON.stringify(state.contents)", script);
    }

    [Fact]
    public async Task A_removed_retired_binding_is_no_longer_posted()
    {
        var keep = Guid.NewGuid();
        var gateway = new Gateway((method, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/strategy-templates/contract") => (HttpStatusCode.OK, ContractJson),
            _ when method == HttpMethod.Put => (HttpStatusCode.OK, """{"data":true,"isSuccessful":true}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = StrategyController(gateway, Manage);
        var model = NewModel("[]");
        // Two bindings were stored; the author removed one, so form.js posts the remaining one only.
        model.ContentBindingsJson = $$"""[{"contentRefType":"knowledge-path","contentRefId":"{{keep}}","sortOrder":0,"notes":null}]""";

        await controller.Edit(TemplateId, model, default);

        var put = Assert.Single(gateway.Requests, r => r.Method == HttpMethod.Put);
        var bindings = JsonDocument.Parse(put.Body!).RootElement.GetProperty("contentBindings");
        Assert.Equal([keep], bindings.EnumerateArray().Select(b => b.GetProperty("contentRefId").GetGuid()).ToArray());
    }

    [Fact]
    public void A_new_line_is_promo_with_a_journey_slot_and_reads_both_back()
    {
        var script = FormScript();
        Assert.Contains("role: lineRoles[0] || null, journeyId: null", script);
        Assert.Contains(".js-line-role", script);
        Assert.Contains(".js-line-journey", script);
        Assert.Contains("line-journeys?productId=", script);
        Assert.Equal("promo", new StrategyTemplateEditViewModel().ProductLineRoles[0]);
        Assert.Equal(["promo", "non-promo"], new StrategyTemplateEditViewModel().ProductLineRoles);
    }

    // ============================================================ SB-3a refusals → under the line, localised

    [Fact]
    public async Task A_line_refusal_is_anchored_to_the_line_it_names_and_never_shown_as_a_code()
    {
        var gateway = new Gateway((method, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/strategy-templates/contract") => (HttpStatusCode.OK, ContractJson),
            _ when method == HttpMethod.Put => (HttpStatusCode.BadRequest,
                """{"isSuccessful":false,"statusCode":400,"errors":["product_line_journey_required","Product line 'GP-B' needs the product's published engagement journey."]}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = StrategyController(gateway, Manage);
        var model = NewModel($$"""
            [{"globalProductId":"{{ProductA}}","globalProductCodeDisplay":"GP-A","role":"promo","journeyId":"{{JourneyA}}"},
             {"globalProductId":"{{ProductB}}","globalProductCodeDisplay":"GP-B","role":"promo","journeyId":null}]
            """);

        var view = Assert.IsType<ViewResult>(await controller.Edit(TemplateId, model, default));

        var errors = JsonSerializer.Deserialize<List<StrategyTemplateFormError>>(
            ((StrategyTemplateEditViewModel)view.Model!).FormErrorsJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var error = Assert.Single(errors);
        Assert.Equal("Err_product_line_journey_required", error.Key);
        Assert.Equal("line", error.Scope);
        Assert.Equal(1, error.LineIndex);
        // The raw code / English runtime text is not dumped into the summary.
        Assert.DoesNotContain(controller.ModelState[string.Empty]?.Errors ?? [], e => e.ErrorMessage.Contains("product_line_journey_required"));
    }

    [Theory]
    [InlineData("journey_product_mismatch", "Engagement journey 'J-X' tells another product than this line ('GP-A').", "line", 0)]
    [InlineData("journey_not_published", "Engagement journey 'J-B' is not published; a line can only be told with a published journey.", "line", 1)]
    [InlineData("content_binding_type_retired", "A new 'knowledge-path' binding is no longer accepted on a strategy template.", "bindings", null)]
    [InlineData("bindings_frozen", "The bindings of an active strategy template are frozen.", "form", null)]
    public async Task Every_sb3a_refusal_lands_where_it_belongs(string code, string message, string scope, int? lineIndex)
    {
        var body = JsonSerializer.Serialize(new { isSuccessful = false, statusCode = 409, errors = new[] { code, message } });
        var gateway = new Gateway((method, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/strategy-templates/contract") => (HttpStatusCode.OK, ContractJson),
            _ when method == HttpMethod.Put => (HttpStatusCode.Conflict, body),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = StrategyController(gateway, Manage);
        var model = NewModel($$"""
            [{"globalProductId":"{{ProductA}}","globalProductCodeDisplay":"GP-A","role":"promo","journeyId":"{{JourneyA}}","journeyCode":"J-A"},
             {"globalProductId":"{{ProductB}}","globalProductCodeDisplay":"GP-B","role":"non-promo","journeyId":"{{Guid.NewGuid()}}","journeyCode":"J-B"}]
            """);

        var view = Assert.IsType<ViewResult>(await controller.Edit(TemplateId, model, default));

        var error = Assert.Single(JsonSerializer.Deserialize<List<StrategyTemplateFormError>>(
            ((StrategyTemplateEditViewModel)view.Model!).FormErrorsJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        Assert.Equal("Err_" + code, error.Key);
        Assert.Equal(scope, error.Scope);
        Assert.Equal(lineIndex, error.LineIndex);
    }

    [Fact]
    public async Task An_unknown_refusal_still_reaches_the_summary_with_the_runtime_text()
    {
        var gateway = new Gateway((method, uri) => uri switch
        {
            _ when uri.EndsWith("/api/crm/strategy-templates/contract") => (HttpStatusCode.OK, ContractJson),
            _ when method == HttpMethod.Put => (HttpStatusCode.BadRequest, """{"errors":["line_weight_total_invalid","Line weights must total 100.00."]}"""),
            _ => (HttpStatusCode.NotFound, "")
        });
        var controller = StrategyController(gateway, Manage);

        var view = Assert.IsType<ViewResult>(await controller.Edit(TemplateId, NewModel("[]"), default));

        Assert.Null(((StrategyTemplateEditViewModel)view.Model!).FormErrorsJson);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage == "Line weights must total 100.00.");
    }

    [Fact]
    public void A_posted_form_error_list_is_never_trusted()
    {
        var property = typeof(StrategyTemplateEditViewModel).GetProperty(nameof(StrategyTemplateEditViewModel.FormErrorsJson))!;
        Assert.NotNull(property.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ModelBinding.BindNeverAttribute>());
    }

    // ============================================================ L10n — 7 languages, bridged

    [Fact]
    public void Every_sb3a_refusal_has_a_localised_text_in_all_seven_languages_and_on_the_bridge()
    {
        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "StrategyTemplates", "_IndexL10n.cshtml"));
        foreach (var code in StrategyTemplateErrorMap.Scopes.Keys)
        {
            var key = StrategyTemplateErrorMap.KeyFor(code);
            Assert.Contains($"\"{key}\"", bridge);
            foreach (var language in Languages)
            {
                var values = Resx("StrategyTemplates", "StrategyTemplatesIndex", language);
                Assert.True(values.TryGetValue(key, out var value), $"{language}: missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: {key} is empty");
                Assert.NotEqual(key, value);
                Assert.DoesNotContain(code, value);
            }
        }
    }

    [Fact]
    public void The_new_strategy_template_keys_exist_in_seven_languages_without_echo_and_are_bridged()
    {
        var bridge = File.ReadAllText(Path.Combine(WebRoot(), "Views", "CRM", "StrategyTemplates", "_IndexL10n.cshtml"));
        var script = FormScript();
        // Every new key form.js reads by name (L.X / L['X'] / L['Prefix_' + v] families listed explicitly).
        var used = Regex.Matches(script, @"\bL\.((?:ProductCol|LineJourney|LineRole|LineNoPublished|LineOpen|NoRetired|RetiredBinding|RoleSplit|RecipeJourneys)[A-Za-z_]*)")
            .Select(m => m.Groups[1].Value)
            .Concat(new[]
            {
                "LineRole_promo", "LineRole_non-promo", "JourneyWarning_journey_language_not_in_country",
                "JourneyWarning_journey_not_published", "JourneyWarning_journey_not_found", "RetiredBindingsHelp",
                "LinesWithoutJourneyTpl", "LineJourneyMissingBadge", "JourneyStatus_draft", "JourneyStatus_review",
                "JourneyStatus_approved", "JourneyStatus_published", "JourneyStatus_inactive", "JourneyStatus_archived"
            })
            .Distinct()
            .ToList();
        Assert.True(used.Count >= 20, string.Join(", ", used));

        var reference = Resx("StrategyTemplates", "StrategyTemplatesIndex", "en").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var values = Resx("StrategyTemplates", "StrategyTemplatesIndex", language);
            Assert.True(reference.SetEquals(values.Keys), $"{language}: key set differs from en");
            foreach (var key in used)
            {
                Assert.True(values.TryGetValue(key, out var value), $"{language}: missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}: {key} is empty");
                Assert.NotEqual(key, value);
            }
        }

        Assert.All(used, key => Assert.Contains($"\"{key}\"", bridge));
    }

    [Fact]
    public void Turkish_texts_keep_their_diacritics()
    {
        var tr = Resx("StrategyTemplates", "StrategyTemplatesIndex", "tr");
        Assert.Equal("Bu ürün için yayında etkileşim yolculuğu yok.", tr["LineNoPublishedJourney"]);
        Assert.Equal("Eski bağ — ziyarette kullanılmıyor; ürün satırındaki yolculuk geçerli.", tr["RetiredBindingsHelp"]);
        Assert.Equal("Ziyaret başına en fazla promo ürün", Resx("CycleCapacities", "CycleCapacitiesIndex", "tr")["MaxPromoProducts"]);
    }

    // ============================================================ cycle capacity — max promo / non-promo per visit

    [Fact]
    public async Task Capacity_posts_both_ceilings_when_filled()
    {
        var gateway = new Gateway((method, _) => method == HttpMethod.Put ? (HttpStatusCode.NoContent, "") : (HttpStatusCode.NotFound, ""));
        var controller = CapacityController(gateway);

        await controller.Edit(TemplateId, CapacityModel(4, 2), default);

        var body = JsonDocument.Parse(Assert.Single(gateway.Requests, r => r.Method == HttpMethod.Put).Body!).RootElement;
        Assert.Equal(4, body.GetProperty("maxPromoProducts").GetInt32());
        Assert.Equal(2, body.GetProperty("maxNonPromoProducts").GetInt32());
    }

    [Fact]
    public async Task Capacity_does_not_post_an_empty_ceiling_so_the_stored_value_is_kept()
    {
        var gateway = new Gateway((method, _) => method == HttpMethod.Put ? (HttpStatusCode.NoContent, "") : (HttpStatusCode.NotFound, ""));
        var controller = CapacityController(gateway);

        await controller.Edit(TemplateId, CapacityModel(5, null), default);

        var body = JsonDocument.Parse(Assert.Single(gateway.Requests, r => r.Method == HttpMethod.Put).Body!).RootElement;
        Assert.Equal(5, body.GetProperty("maxPromoProducts").GetInt32());
        Assert.False(body.TryGetProperty("maxNonPromoProducts", out _));
    }

    [Fact]
    public void Capacity_ceilings_are_optional_one_to_ten_and_start_at_three()
    {
        foreach (var name in new[] { nameof(CycleCapacityEditViewModel.MaxPromoProducts), nameof(CycleCapacityEditViewModel.MaxNonPromoProducts) })
        {
            var property = typeof(CycleCapacityEditViewModel).GetProperty(name)!;
            Assert.Equal(typeof(int?), property.PropertyType);
            Assert.Null(property.GetCustomAttribute<RequiredAttribute>());
            var range = property.GetCustomAttribute<RangeAttribute>()!;
            Assert.Equal(1, range.Minimum);
            Assert.Equal(10, range.Maximum);
        }

        Assert.Equal(3, CycleCapacityEditViewModel.DefaultMaxProductsPerVisit);
    }

    [Fact]
    public async Task Capacity_out_of_range_refusal_is_shown_under_the_field_it_names()
    {
        var gateway = new Gateway((method, _) => method == HttpMethod.Put
            ? (HttpStatusCode.BadRequest, """{"errors":["MaxNonPromoProducts must be between 1 and 10.","max_products_out_of_range"]}""")
            : (HttpStatusCode.NotFound, ""));
        var controller = CapacityController(gateway);

        await controller.Edit(TemplateId, CapacityModel(3, 11), default);

        var field = controller.ModelState[nameof(CycleCapacityEditViewModel.MaxNonPromoProducts)]!;
        Assert.Equal("L:Err_max_products_out_of_range", Assert.Single(field.Errors).ErrorMessage);
        Assert.DoesNotContain(controller.ModelState[string.Empty]?.Errors ?? [], e => e.ErrorMessage.Contains("max_products_out_of_range"));
    }

    [Fact]
    public void Capacity_new_keys_exist_in_seven_languages()
    {
        foreach (var language in Languages)
        {
            var values = Resx("CycleCapacities", "CycleCapacitiesIndex", language);
            foreach (var key in new[] { "MaxPromoProducts", "MaxNonPromoProducts", "MaxProductsHint", "Err_max_products_out_of_range" })
            {
                Assert.True(values.TryGetValue(key, out var value), $"{language}: missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(value));
                Assert.NotEqual(key, value);
            }
        }
    }

    // ============================================================ fixtures

    private static string ContractJson => """
        {"isSuccessful":true,"data":{"isReady":true,"features":{"supportsStrategyTemplateDefinition":true},
         "vocabularies":{"subjectTypes":["contact"]},"limits":{"maxProductLines":50,"requiredAllocationTotal":100}}}
        """;

    private static string DetailJson => $$$"""
        {"isSuccessful":true,"data":{
          "templateId":"{{{TemplateId}}}","templateCode":"play-1","templateName":"Play 1","subjectType":"contact",
          "templateStatus":"draft","templateVersion":1,"effectiveFrom":"2026-10-01T00:00:00+00:00",
          "segmentBindings":[],"frequencyIntent":{"mode":"none"},
          "productLines":[{"lineId":"{{{Guid.NewGuid()}}}","globalProductId":"{{{ProductA}}}","globalProductCodeDisplay":"GP-A",
            "lineWeightPercentage":60,"skuAllocationMode":"sku-allocated",
            "skuAllocations":[{"allocationId":"{{{Guid.NewGuid()}}}","gskuId":"{{{GskuA}}}","percentage":100,"sortOrder":0}],
            "totalPercentage":100,"sortOrder":10,"notes":"keep me",
            "role":"non-promo","journeyId":"{{{JourneyA}}}","journeyCode":"J-A","journeyName":"Journey A",
            "journeyStatus":"published","journeyMissing":false,"journeyWarnings":[]}],
          "contentBindings":[],"areBindingsFrozen":false,"isArchived":false,"version":3,
          "promoLineCount":0,"nonPromoLineCount":1,"linesWithoutJourneyCount":0}}
        """;

    private static Gateway JourneyGateway(HttpStatusCode subjectsStatus = HttpStatusCode.OK) => new((_, uri) => uri switch
    {
        _ when uri.Contains("/api/crm/knowledge/content-engagement-journeys") => (HttpStatusCode.OK, $$$"""
            {"isSuccessful":true,"data":{"total":5,"items":[
              {"journeyId":"{{{JourneyA}}}","journeyCode":"J-A-PUB","journeyName":"A","subjectId":"{{{SubjectA}}}","languageCode":"tr","journeyStatus":"published","isArchived":false},
              {"journeyId":"{{{Guid.NewGuid()}}}","journeyCode":"J-A-DRAFT","journeyName":"A draft","subjectId":"{{{SubjectA}}}","journeyStatus":"draft","isArchived":false},
              {"journeyId":"{{{Guid.NewGuid()}}}","journeyCode":"J-A-ARCH","journeyName":"A old","subjectId":"{{{SubjectA}}}","journeyStatus":"published","isArchived":true},
              {"journeyId":"{{{Guid.NewGuid()}}}","journeyCode":"J-B-PUB","journeyName":"B","subjectId":"{{{SubjectB}}}","journeyStatus":"published","isArchived":false},
              {"journeyId":"{{{Guid.NewGuid()}}}","journeyCode":"J-NOPRIMARY","journeyName":"N","subjectId":"{{{SubjectNoPrimary}}}","journeyStatus":"published","isArchived":false}]}}
            """),
        _ when uri.Contains("/api/crm/knowledge/subjects") => (subjectsStatus, $$$"""
            {"isSuccessful":true,"data":{"total":3,"items":[
              {"subjectId":"{{{SubjectA}}}","externalReferences":[{"sourceSystem":"global-product","externalId":"{{{ProductA}}}","isPrimary":true}]},
              {"subjectId":"{{{SubjectB}}}","externalReferences":[{"sourceSystem":"global-product","externalId":"{{{ProductB}}}","isPrimary":true}]},
              {"subjectId":"{{{SubjectNoPrimary}}}","externalReferences":[{"sourceSystem":"global-product","externalId":"{{{ProductA}}}","isPrimary":false}]}]}}
            """),
        _ => (HttpStatusCode.NotFound, "")
    });

    private static StrategyTemplateEditViewModel NewModel(string productLinesJson) => new()
    {
        TemplateId = TemplateId,
        TemplateCode = "play-1",
        TemplateName = "Play 1",
        SubjectType = "contact",
        EffectiveFrom = DateTimeOffset.Parse("2026-10-01T00:00:00+00:00"),
        SegmentBindingsJson = "[]",
        FrequencyIntentJson = """{"mode":"none"}""",
        ProductLinesJson = productLinesJson,
        ContentBindingsJson = "[]"
    };

    private static CycleCapacityEditViewModel CapacityModel(int? maxPromo, int? maxNonPromo) => new()
    {
        CycleCapacityId = TemplateId,
        CyclePeriodId = Guid.NewGuid(),
        CalendarCountryCode = "TR",
        DailyWorkMinutes = 480,
        PromoProductTime = 10,
        NonPromoProductTime = 5,
        TravelingTime = 60,
        ReportDuration = 5,
        QuizDuration = 0,
        BetweenVisitTimeMinutes = 5,
        MaxPromoProducts = maxPromo,
        MaxNonPromoProducts = maxNonPromo,
        ExpectedVersion = 1
    };

    private static JsonElement Data(IActionResult result)
    {
        var value = Assert.IsAssignableFrom<ObjectResult>(result).Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .RootElement.GetProperty("data");
    }

    private static StrategyTemplatesController StrategyController(Gateway gateway, params string[] permissions)
    {
        var controller = new StrategyTemplatesController(new HttpClient(gateway), Configuration(), new KeyLocalizer(),
            NullLogger<StrategyTemplatesController>.Instance);
        Attach(controller, permissions);
        return controller;
    }

    private static CycleCapacitiesController CapacityController(Gateway gateway)
    {
        var controller = new CycleCapacitiesController(new HttpClient(gateway), Configuration(),
            NullLogger<CycleCapacitiesController>.Instance, new CapacityLocalizer());
        Attach(controller, "crm.cycle-capacity.manage");
        return controller;
    }

    private static void Attach(Controller controller, params string[] permissions)
    {
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();

    private static string FormScript() => File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM",
        "StrategyTemplates", "form.js"));

    private static Dictionary<string, string> Resx(string folder, string family, string language)
    {
        var path = Path.Combine(WebRoot(), "Resources", "Views", "CRM", folder, $"{family}.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    private sealed class Gateway(Func<HttpMethod, string, (HttpStatusCode Status, string Body)> route) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, string? Tenant, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, uri,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null, body));
            var (status, payload) = route(request.Method, uri);
            return new HttpResponseMessage(status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class CapacityLocalizer : IStringLocalizer<CycleCapacitiesIndex>
    {
        public LocalizedString this[string name] => new(name, "L:" + name);
        public LocalizedString this[string name, params object[] arguments] => new(name, "L:" + name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
