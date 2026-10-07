using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers.MasterData;
using Diten.Web.Models.MasterData;
using Diten.Web.Views.MasterData.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// P-2026-10-07-BRAND-FIX3 — the product edit form on the production web code: the real ProductsController with MDM as a
/// recording, path-routed handler, and the real Edit view executed by the application's own Razor engine. The form never
/// drops the product's current brand (R-4), the linkability rule is MDM's alone (R-5), and the save carries the version it
/// was made on (R-3).
/// </summary>
public sealed class ProductsWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private static readonly Guid ProductId = Guid.Parse("e0e0e0e0-1111-2222-3333-444444444444");
    private static readonly Guid CurrentBrandId = Guid.Parse("f1f1f1f1-1111-2222-3333-444444444444");
    private static readonly Guid LinkableBrandId = Guid.Parse("f2f2f2f2-1111-2222-3333-444444444444");
    private static readonly Guid UnlinkableBrandId = Guid.Parse("f3f3f3f3-1111-2222-3333-444444444444");
    private const int LoadedVersion = 7;
    private const string Editor = "mdm.products.update";
    private const string BrandReader = "mdm.brands.read";

    private readonly WebApplicationFactory<Program> _factory;

    public ProductsWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── R-4 — the current brand is never dropped, even when MDM would not link it any more ────────────────────────────

    [Fact]
    public async Task Fix3_R4_the_edit_page_offers_the_current_unlinkable_brand_selected_and_marked_not_linkable()
    {
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: false));

        var html = await RenderEditAsync(wire, Editor, BrandReader);

        var option = OptionOf(html, CurrentBrandId);
        Assert.Contains("selected", option.Attributes);
        Assert.Contains("B-OLD", option.Text);
        Assert.Contains("Old Brand", option.Text);
        Assert.EndsWith("(not linkable)", option.Text.Trim());
        // The brands MDM would link are offered too, without the mark.
        Assert.DoesNotContain("(not linkable)", OptionOf(html, LinkableBrandId).Text);
    }

    [Fact]
    public async Task Fix3_R4_saving_without_touching_the_brand_puts_the_same_brandId_and_the_loaded_version()
    {
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: false));
        var html = await RenderEditAsync(wire, Editor, BrandReader);

        var posted = PostedForm(html);
        Assert.Equal(CurrentBrandId.ToString(), posted.BrandId);
        Assert.Equal(LoadedVersion.ToString(CultureInfo.InvariantCulture), posted.ExpectedVersion);

        var put = await PostEditAsync(wire, posted, Editor, BrandReader);

        Assert.Equal(CurrentBrandId, put.GetProperty("brandId").GetGuid());
        Assert.Equal(LoadedVersion, put.GetProperty("expectedVersion").GetInt32());
    }

    [Fact]
    public async Task Fix3_R4_an_editor_without_brand_read_sees_only_the_current_brand_read_only_and_the_brand_list_is_never_asked()
    {
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: false));

        var html = await RenderEditAsync(wire, Editor);

        var select = Regex.Match(html, "<select id=\"BrandIdReadOnly\"(?<attrs>[^>]*)>(?<inner>[\\s\\S]*?)</select>");
        Assert.True(select.Success, "the read-only brand select is not on the page");
        Assert.Contains("disabled", select.Groups["attrs"].Value);
        var options = Regex.Matches(select.Groups["inner"].Value, "<option[^>]*value=\"(?<value>[^\"]*)\"");
        Assert.Equal(CurrentBrandId.ToString(), Assert.Single(options).Groups["value"].Value);
        Assert.Contains("B-OLD", select.Groups["inner"].Value);
        Assert.Contains("Old Brand", select.Groups["inner"].Value);
        Assert.Equal(CurrentBrandId.ToString(), HiddenValue(html, "BrandId"));
        Assert.DoesNotMatch("<select[^>]*name=\"BrandId\"", html);
        Assert.DoesNotContain(wire.Urls, x => x.Contains("/api/mdm/brands", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fix3_R4_an_editor_without_brand_read_saves_the_same_brandId_and_the_version_and_never_asks_the_brand_list()
    {
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: false));
        var html = await RenderEditAsync(wire, Editor);

        var posted = PostedForm(html);
        Assert.Equal(CurrentBrandId.ToString(), posted.BrandId);

        var put = await PostEditAsync(wire, posted, Editor);

        Assert.Equal(CurrentBrandId, put.GetProperty("brandId").GetGuid());
        Assert.Equal(LoadedVersion, put.GetProperty("expectedVersion").GetInt32());
        Assert.DoesNotContain(wire.Urls, x => x.Contains("/api/mdm/brands", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fix3_R4_a_brand_less_product_still_opens_on_No_brand_and_saves_without_one()
    {
        var wire = Mdm(ProductJson(brandId: null, linkable: false));
        var html = await RenderEditAsync(wire, Editor, BrandReader);

        var brandSelect = Regex.Match(html, "<select[^>]*name=\"BrandId\"[^>]*>(?<inner>[\\s\\S]*?)</select>");
        Assert.True(brandSelect.Success, "the BrandId select is not on the page");
        var noBrand = Regex.Match(brandSelect.Groups["inner"].Value, "<option(?<attrs>[^>]*)value=\"\"(?<rest>[^>]*)>(?<text>[^<]*)</option>");
        Assert.True(noBrand.Success);
        Assert.Equal("No brand", WebUtility.HtmlDecode(noBrand.Groups["text"].Value));
        Assert.DoesNotContain("(not linkable)", WebUtility.HtmlDecode(html));
        var posted = PostedForm(html);
        Assert.Equal(string.Empty, posted.BrandId);

        var put = await PostEditAsync(wire, posted, Editor, BrandReader);

        Assert.Equal(JsonValueKind.Null, put.GetProperty("brandId").ValueKind);
        Assert.Equal(LoadedVersion, put.GetProperty("expectedVersion").GetInt32());
    }

    [Fact]
    public async Task Fix3_R3_a_version_conflict_re_renders_the_form_with_the_refusal_and_the_current_brand_kept()
    {
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: false),
            put: (HttpStatusCode.Conflict,
                "{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"version_conflict: The product has changed since it was loaded.\"]}"));
        var controller = Controller(wire, Editor, BrandReader);

        var result = await controller.Edit(ProductId, new ProductEditViewModel
        {
            ProductId = ProductId, ProductCode = "P-1", ProductName = "Product", ProductStatus = "active",
            BrandId = CurrentBrandId, EffectiveFrom = DateTimeOffset.Now, ExpectedVersion = LoadedVersion - 1
        }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/MasterData/Products/Edit.cshtml", view.ViewName);
        Assert.NotEmpty(controller.ModelState[string.Empty]!.Errors);
        var model = Assert.IsType<ProductEditViewModel>(view.Model);
        var kept = Assert.Single(model.BrandOptions, x => x.BrandId == CurrentBrandId);
        Assert.False(kept.IsLinkable);
    }

    // ── R-5 — the linkability rule lives once, in MDM ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fix3_R5_a_brand_MDM_marks_not_linkable_is_not_offered_unless_it_is_the_current_brand()
    {
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: false));

        var result = await Controller(wire, Editor, BrandReader).Edit(ProductId, CancellationToken.None);

        var model = Assert.IsType<ProductEditViewModel>(Assert.IsType<ViewResult>(result).Model);
        var offered = model.BrandOptions.Select(x => x.BrandId).ToList();
        Assert.Contains(LinkableBrandId, offered);
        Assert.Contains(CurrentBrandId, offered);
        Assert.DoesNotContain(UnlinkableBrandId, offered);
        // The list item has an "active" status and a window around today — only MDM's isLinkable:false keeps it out.
        Assert.Contains(wire.Urls, x => x.Contains("/api/mdm/brands?", StringComparison.Ordinal));
    }

    [Fact]
    public void Fix3_R5_the_web_has_no_copy_of_the_linkability_rule()
    {
        var methods = typeof(ProductsController).GetMethods(
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.DoesNotContain(methods, x => x.Name.Contains("IsLinkableOn", StringComparison.Ordinal));
    }

    // ── the new sentence in every tenant language ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public void Fix3_the_not_linkable_suffix_exists_in_every_language_and_is_not_English_outside_en(string culture)
    {
        var localizer = _factory.Services.GetRequiredService<IStringLocalizer<ProductsIndex>>();
        string english;
        using (new CultureScope("en")) english = localizer["BrandNotLinkableSuffix"].Value;
        LocalizedString value;
        using (new CultureScope(culture)) value = localizer["BrandNotLinkableSuffix"];

        Assert.False(value.ResourceNotFound, $"BrandNotLinkableSuffix missing in {culture}");
        Assert.False(string.IsNullOrWhiteSpace(value.Value), $"BrandNotLinkableSuffix empty in {culture}");
        if (culture != "en")
        {
            Assert.NotEqual(english, value.Value);
        }
    }

    // ── P-2026-10-07-BRAND-FIX4 (F3) — a failed save never drops the brand on its re-render ──────────────────────────────

    [Fact]
    public async Task Fix4_F3_a_failed_put_whose_product_and_brand_reads_also_fail_keeps_the_posted_brand_for_a_brand_reader()
    {
        var outage = Outage();
        var controller = Controller(outage, Editor, BrandReader);

        var result = await controller.Edit(ProductId, PostedEdit(CurrentBrandId), CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProductEditViewModel>(view.Model);
        Assert.False(model.BrandReadOnly);
        var kept = Assert.Single(model.BrandOptions, x => x.BrandId == CurrentBrandId);
        Assert.True(kept.NameUnreadable);

        var html = await RenderAsync("/Views/MasterData/Products/Edit.cshtml", [Editor, BrandReader], model, controller.ModelState);
        var option = OptionOf(html, CurrentBrandId);
        Assert.Contains("selected", option.Attributes);
        Assert.Equal($"{CurrentBrandId:D} (could not be read)", option.Text.Trim());
        var posted = PostedForm(html);
        Assert.Equal(CurrentBrandId.ToString(), posted.BrandId);

        var put = await PostEditAsync(Mdm(ProductJson(CurrentBrandId, linkable: true)), posted, Editor, BrandReader);

        Assert.Equal(CurrentBrandId, put.GetProperty("brandId").GetGuid());
        Assert.Equal(LoadedVersion, put.GetProperty("expectedVersion").GetInt32());
    }

    [Fact]
    public async Task Fix4_F3_a_failed_put_whose_product_read_also_fails_keeps_the_posted_brand_read_only_for_an_editor_without_brand_read()
    {
        var outage = Outage();
        var controller = Controller(outage, Editor);

        var result = await controller.Edit(ProductId, PostedEdit(CurrentBrandId), CancellationToken.None);

        var model = Assert.IsType<ProductEditViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.BrandReadOnly);
        Assert.Equal(CurrentBrandId, Assert.Single(model.BrandOptions).BrandId);

        var html = await RenderAsync("/Views/MasterData/Products/Edit.cshtml", [Editor], model, controller.ModelState);
        var select = Regex.Match(html, "<select id=\"BrandIdReadOnly\"(?<attrs>[^>]*)>(?<inner>[\\s\\S]*?)</select>");
        Assert.True(select.Success, "the read-only brand select is not on the page");
        Assert.Contains("disabled", select.Groups["attrs"].Value);
        Assert.Contains("(could not be read)", WebUtility.HtmlDecode(select.Groups["inner"].Value));
        Assert.Equal(CurrentBrandId.ToString(), HiddenValue(html, "BrandId"));
        var posted = PostedForm(html);
        Assert.Equal(CurrentBrandId.ToString(), posted.BrandId);
        Assert.DoesNotContain(outage.Urls, x => x.Contains("/api/mdm/brands", StringComparison.Ordinal));

        var put = await PostEditAsync(Mdm(ProductJson(CurrentBrandId, linkable: true)), posted, Editor);

        Assert.Equal(CurrentBrandId, put.GetProperty("brandId").GetGuid());
    }

    [Fact]
    public async Task Fix4_F3_a_re_render_without_the_contract_still_keeps_the_posted_brand()
    {
        var controller = Controller(Outage(contractDown: true), Editor, BrandReader);

        var result = await controller.Edit(ProductId, PostedEdit(CurrentBrandId), CancellationToken.None);

        var model = Assert.IsType<ProductEditViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("BrandProductContractUnavailable", model.ContractError);
        Assert.Equal(CurrentBrandId, Assert.Single(model.BrandOptions).BrandId);
    }

    // ── FIX4 (K3) — a product refusal is a sentence in the reader's language ──────────────────────────────────────────

    /// <summary>Every reason code a PRODUCT write can return (MDM BrandProductReasonCodes, product side).</summary>
    private static readonly string[] ProductReasonCodes =
    [
        "product_not_found", "product_code_duplicate", "brand_not_found", "brand_archived", "brand_not_linkable",
        "record_archived", "code_immutable", "invalid_product_status", "invalid_product_type", "invalid_dosage_form",
        "invalid_unit_of_measure", "invalid_atc_code", "archived_status_not_assignable", "invalid_effective_window",
        "external_reference_primary_conflict", "external_reference_duplicate", "external_reference_in_use",
        "indication_ref_duplicate", "version_conflict", "expected_version_required", "store_unavailable",
        "write_outcome_unknown", "field_invalid"
    ];

    private static readonly string[] RefusedStatuses = ["400", "403", "404", "409", "502", "503", "504", "Other"];

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public void Fix4_K3_every_product_reason_code_and_status_has_a_sentence_in_every_language_and_is_not_English_outside_en(string culture)
    {
        var localizer = _factory.Services.GetRequiredService<IStringLocalizer<ProductsIndex>>();
        var keys = ProductReasonCodes.Select(x => $"Reason_{x}")
            .Concat(RefusedStatuses.Select(x => $"RefusedStatus_{x}"))
            .Append("BrandNotReadableSuffix");
        foreach (var key in keys)
        {
            string english;
            using (new CultureScope("en")) english = localizer[key].Value;
            LocalizedString value;
            using (new CultureScope(culture)) value = localizer[key];

            Assert.False(value.ResourceNotFound, $"{key} missing in {culture}");
            Assert.False(string.IsNullOrWhiteSpace(value.Value), $"{key} empty in {culture}");
            if (key.StartsWith("Reason_", StringComparison.Ordinal))
            {
                Assert.DoesNotContain(key["Reason_".Length..], value.Value);
            }

            if (culture != "en")
            {
                Assert.NotEqual(english, value.Value);
            }

            if (key == "Reason_field_invalid")
            {
                Assert.Contains("{0}", value.Value);
            }
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "version_conflict", "The product has changed since it was loaded.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "write_outcome_unknown", "The write was sent but its outcome could not be confirmed.")]
    public async Task Fix4_K3_a_product_refusal_reaches_the_re_rendered_form_as_its_sentence_never_the_code_or_MDMs_english(
        HttpStatusCode status, string code, string mdmEnglish)
    {
        var localizer = _factory.Services.GetRequiredService<IStringLocalizer<ProductsIndex>>();
        var wire = Mdm(ProductJson(CurrentBrandId, linkable: true),
            put: (status, $"{{\"isSuccessful\":false,\"statusCode\":{(int)status},\"errors\":[\"{code}: {mdmEnglish}\"]}}"));
        var controller = Controller(wire, localizer, Editor, BrandReader);

        string expected, english;
        IActionResult result;
        using (new CultureScope("en")) english = localizer[$"Reason_{code}"].Value;
        using (new CultureScope("tr"))
        {
            expected = localizer[$"Reason_{code}"].Value;
            result = await controller.Edit(ProductId, PostedEdit(CurrentBrandId), CancellationToken.None);
        }

        Assert.NotEqual(english, expected);
        var errors = controller.ModelState[string.Empty]!.Errors.Select(x => x.ErrorMessage).ToList();
        Assert.Equal(expected, Assert.Single(errors));
        var model = Assert.IsType<ProductEditViewModel>(Assert.IsType<ViewResult>(result).Model);

        var html = WebUtility.HtmlDecode(
            await RenderAsync("/Views/MasterData/Products/Edit.cshtml", [Editor, BrandReader], model, controller.ModelState));
        Assert.Contains(expected, html);
        Assert.DoesNotContain($"{code}:", html);
        Assert.DoesNotContain(mdmEnglish, html);
    }

    // ── FIX4 (K4) — one calendar-day rule on the web; the create default is the UTC day ───────────────────────────────

    [Theory]
    [InlineData("2026-10-07T00:00:00+03:00", "2026-10-06T22:00:00-05:00", true)]
    [InlineData("2026-10-07T20:00:00-05:00", "2026-10-07T01:00:00+03:00", false)]
    [InlineData("2026-10-07T00:00:00+00:00", "2026-10-07T00:00:00+00:00", false)]
    public void Fix4_K4_the_web_compares_the_entered_calendar_days_never_their_UTC_days(string from, string to, bool refused)
    {
        var start = DateTimeOffset.Parse(from, CultureInfo.InvariantCulture);
        var end = DateTimeOffset.Parse(to, CultureInfo.InvariantCulture);

        Assert.Equal(refused, CalendarDay.EndsBeforeStart(start, end));
        var model = new ProductEditViewModel { ProductCode = "P", ProductName = "P", EffectiveFrom = start, EffectiveTo = end };
        var results = model.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(model)).ToList();
        Assert.Equal(refused, results.Any(x => x.ErrorMessage == "EffectiveToBeforeEffectiveFrom"));
    }

    [Fact]
    public void Fix4_K4_the_calendar_day_of_a_value_is_the_day_it_was_entered_in_its_own_offset()
    {
        Assert.Equal(new DateTime(2026, 10, 7), CalendarDay.Of(DateTimeOffset.Parse("2026-10-07T00:00:00+03:00", CultureInfo.InvariantCulture)));
        Assert.Equal(new DateTime(2026, 10, 6), CalendarDay.Of(DateTimeOffset.Parse("2026-10-06T22:00:00-05:00", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Fix4_K4_the_product_create_form_defaults_to_the_UTC_day_at_midnight_plus_00_00()
    {
        var before = DateTimeOffset.UtcNow.UtcDateTime.Date;

        var result = await Controller(Mdm(ProductJson(null, linkable: true)), "mdm.products.create").Create(CancellationToken.None);

        var after = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var model = Assert.IsType<ProductEditViewModel>(Assert.IsType<ViewResult>(result).Model);
        var from = Assert.NotNull(model.EffectiveFrom);
        Assert.Equal(TimeSpan.Zero, from.Offset);
        Assert.Equal(TimeSpan.Zero, from.TimeOfDay);
        Assert.Contains(from.DateTime, new[] { before, after });
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>FIX4 — the form as posted with a brand and the loaded version.</summary>
    private static ProductEditViewModel PostedEdit(Guid brandId) => new()
    {
        ProductId = ProductId, ProductCode = "P-1", ProductName = "Product", ProductStatus = "active",
        BrandId = brandId, EffectiveFrom = CalendarDay.TodayUtc(), ExpectedVersion = LoadedVersion
    };

    /// <summary>
    /// FIX4 (F3) — MDM in trouble: the PUT answers 502, the product read, the brand list and the brand read answer 503;
    /// the contract answers (or not, with <paramref name="contractDown"/>).
    /// </summary>
    private static RecordingHandler Outage(bool contractDown = false)
    {
        const string contract = "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"isReady\":true,"
                                + "\"features\":{\"supportsProductManagement\":true,\"supportsBrandProductReference\":true},"
                                + "\"vocabulary\":{\"productStatuses\":[\"draft\",\"active\"],\"productTypes\":[],\"dosageForms\":[],\"unitsOfMeasure\":[]}}}";
        const string unavailable = "{\"isSuccessful\":false,\"statusCode\":503,\"errors\":[\"store_unavailable: MDM is not reachable.\"]}";
        return new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Put) return (HttpStatusCode.BadGateway, "{}");
            if (!contractDown && request.RequestUri!.AbsolutePath.Contains("contract", StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, contract);
            }

            return (HttpStatusCode.ServiceUnavailable, unavailable);
        });
    }

    private sealed record Form(string BrandId, string ExpectedVersion);

    private sealed record Option(string Attributes, string Text);

    private static Option OptionOf(string html, Guid brandId)
    {
        var match = Regex.Match(html, $"<option(?<attrs>[^>]*value=\"{brandId}\"[^>]*)>(?<text>[^<]*)</option>");
        Assert.True(match.Success, $"no option for {brandId}");
        return new Option(match.Groups["attrs"].Value, WebUtility.HtmlDecode(match.Groups["text"].Value));
    }

    /// <summary>What the browser posts for the brand and the version: the selected option of the enabled BrandId select, or
    /// the hidden BrandId input (a disabled select is not posted), and the hidden ExpectedVersion.</summary>
    private static Form PostedForm(string html)
    {
        string brandId;
        var select = Regex.Match(html, "<select(?![^>]*disabled)[^>]*name=\"BrandId\"[^>]*>(?<inner>[\\s\\S]*?)</select>");
        if (select.Success)
        {
            var options = Regex.Matches(select.Groups["inner"].Value, "<option[^>]*>");
            var chosen = options.FirstOrDefault(x => x.Value.Contains("selected=\"selected\"", StringComparison.Ordinal)) ?? options[0];
            brandId = Regex.Match(chosen.Value, "value=\"(?<v>[^\"]*)\"").Groups["v"].Value;
        }
        else
        {
            brandId = HiddenValue(html, "BrandId");
        }

        return new Form(brandId, HiddenValue(html, "ExpectedVersion"));
    }

    private static string HiddenValue(string html, string name)
    {
        var input = Regex.Match(html, $"<input(?=[^>]*type=\"hidden\")(?=[^>]*name=\"{name}\")[^>]*>");
        Assert.True(input.Success, $"no hidden {name} on the page");
        return Regex.Match(input.Value, "value=\"(?<v>[^\"]*)\"").Groups["v"].Value;
    }

    private async Task<string> RenderEditAsync(RecordingHandler wire, params string[] permissions)
    {
        var controller = Controller(wire, permissions);
        var result = await controller.Edit(ProductId, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/MasterData/Products/Edit.cshtml", view.ViewName);
        return await RenderAsync("/Views/MasterData/Products/Edit.cshtml", permissions, view.Model);
    }

    private static async Task<JsonElement> PostEditAsync(RecordingHandler wire, Form posted, params string[] permissions)
    {
        var controller = Controller(wire, permissions);
        var model = new ProductEditViewModel
        {
            ProductCode = "P-1", ProductName = "Product", ProductStatus = "active", EffectiveFrom = DateTimeOffset.Now,
            BrandId = posted.BrandId.Length == 0 ? null : Guid.Parse(posted.BrandId),
            ExpectedVersion = int.Parse(posted.ExpectedVersion, CultureInfo.InvariantCulture)
        };
        var before = wire.Bodies.Count;

        var result = await controller.Edit(ProductId, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var body = Assert.Single(wire.Bodies.Skip(before));
        Assert.Equal($"{Gateway}/api/mdm/products/{ProductId}", wire.Puts.Last());
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static string ProductJson(Guid? brandId, bool linkable)
    {
        var linked = brandId is { } id
            ? $",\"brandId\":\"{id}\",\"linkedBrand\":{{\"brandId\":\"{id}\",\"brandCode\":\"B-OLD\",\"brandName\":\"Old Brand\",\"isLinkable\":{(linkable ? "true" : "false")}}}"
            : string.Empty;
        return "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{"
               + $"\"productId\":\"{ProductId}\",\"productCode\":\"P-1\",\"productName\":\"Product\",\"productStatus\":\"active\""
               + $",\"effectiveFrom\":\"2026-01-01T00:00:00+00:00\",\"version\":{LoadedVersion},\"isArchived\":false"
               + ",\"indicationRefs\":[],\"externalReferences\":[]"
               + linked + "}}";
    }

    private static RecordingHandler Mdm(string productJson, (HttpStatusCode Status, string Body)? put = null)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-30).ToString("O");
        var brands = "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"totalCount\":2,\"items\":["
                     + $"{{\"brandId\":\"{LinkableBrandId}\",\"brandCode\":\"B-NEW\",\"brandName\":\"New Brand\",\"brandStatus\":\"active\",\"effectiveFrom\":\"{from}\",\"isLinkable\":true}},"
                     + $"{{\"brandId\":\"{UnlinkableBrandId}\",\"brandCode\":\"B-NOPE\",\"brandName\":\"Refused Brand\",\"brandStatus\":\"active\",\"effectiveFrom\":\"{from}\",\"isLinkable\":false}}"
                     + "]}}";
        const string contract = "{\"isSuccessful\":true,\"statusCode\":200,\"data\":{\"isReady\":true,"
                                + "\"features\":{\"supportsProductManagement\":true,\"supportsBrandProductReference\":true},"
                                + "\"vocabulary\":{\"productStatuses\":[\"draft\",\"active\"],\"productTypes\":[],\"dosageForms\":[],\"unitsOfMeasure\":[]}}}";
        return new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put)
            {
                return put ?? (HttpStatusCode.OK, "{\"isSuccessful\":true,\"statusCode\":200,\"data\":null}");
            }

            if (path.Contains("contract", StringComparison.Ordinal)) return (HttpStatusCode.OK, contract);
            if (path == "/api/mdm/brands") return (HttpStatusCode.OK, brands);
            if (path == $"/api/mdm/products/{ProductId}") return (HttpStatusCode.OK, productJson);
            return (HttpStatusCode.NotFound, "{}");
        });
    }

    private static ProductsController Controller(RecordingHandler wire, params string[] permissions)
        => Controller(wire, new EchoLocalizer<ProductsIndex>(), permissions);

    private static ProductsController Controller(
        RecordingHandler wire, IStringLocalizer<ProductsIndex> localizer, params string[] permissions)
    {
        var context = new DefaultHttpContext { User = Principal(permissions) };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-product-edit";
        return new ProductsController(new HttpClient(wire), new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build(),
            new EchoLocalizer<Diten.Web.SharedResource>(), localizer, NullLogger<ProductsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new NullTempDataProvider())
        };
    }

    private static ClaimsPrincipal Principal(IEnumerable<string> permissions) => new(new ClaimsIdentity(
        new[]
        {
            new Claim("tenant_id", Guid.NewGuid().ToString("D")),
            new Claim("actor_type", "tenant_user"),
            new Claim("sub", Guid.NewGuid().ToString("D"))
        }.Concat(permissions.Select(x => new Claim("permission", x))), "fixture"));

    private async Task<string> RenderAsync(string path, string[] permissions, object? model, ModelStateDictionary? modelState = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = services, User = Principal(permissions) };
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var engine = services.GetRequiredService<IRazorViewEngine>();
        var view = engine.GetView(executingFilePath: null, viewPath: path, isMainPage: true);
        var page = engine.GetPage(executingFilePath: null, pagePath: path).Page
                   ?? throw new InvalidOperationException($"Could not load {path}.");
        // Edit.cshtml's <form asp-action> asks a router for its URL; outside a request pipeline a fixed one answers.
        var routeData = new RouteData { Values = { ["controller"] = "Products", ["action"] = "Edit" } };
        routeData.Routers.Add(new FixedRouter());
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor(), modelState ?? new ModelStateDictionary());
        var viewData = new ViewDataDictionary(services.GetRequiredService<IModelMetadataProvider>(), actionContext.ModelState)
        {
            Model = model
        };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, view.View!, viewData, tempData, writer, new HtmlHelperOptions())
        {
            ExecutingFilePath = path
        };
        page.ViewContext = viewContext;
        services.GetRequiredService<IRazorPageActivator>().Activate(page, viewContext);
        await page.ExecuteAsync();
        return writer.ToString();
    }

    private sealed class FixedRouter : IRouter
    {
        public VirtualPathData GetVirtualPath(VirtualPathContext context) => new(this, $"/MasterData/Products/{ProductId}/Edit");
        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }

    private sealed class EchoLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    /// <summary>MDM, answered by path; every URL and every PUT body recorded as sent.</summary>
    private sealed class RecordingHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public List<string> Puts { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.OriginalString);
            if (request.Method == HttpMethod.Put)
            {
                Puts.Add(request.RequestUri.OriginalString);
                Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            }

            var (status, body) = respond(request);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
