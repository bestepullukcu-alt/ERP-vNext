using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-CL-FE-5 — knowledge content ↔ claim binding on the Web layer: the claim-options rows (core + country versions,
/// usable / reason, language mismatch, no product ⇒ empty, CRM refused ⇒ disabled), the write payload (ClaimRefs is
/// ALWAYS a list — [] clears, null would keep), the edit round-trip, CRM claim error codes → Claims-section field errors,
/// the detail "Linked claims" rows and the 7-language KnowledgeIndex keys.
/// </summary>
public sealed class KnowledgeClaimRefsTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid ProductId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ClaimA = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid ClaimB = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid VersionTr = Guid.Parse("c0000000-0000-0000-0000-00000000000a");
    private static readonly Guid VersionUz = Guid.Parse("c0000000-0000-0000-0000-00000000000b");
    private static readonly Guid ContentId = Guid.Parse("d0000000-0000-0000-0000-00000000000c");

    private static readonly string Coverage = JsonSerializer.Serialize(new
    {
        data = new
        {
            countries = new[] { new { countryCode = "TR" }, new { countryCode = "UZ" }, new { countryCode = "DE" } },
            rows = new object[]
            {
                new
                {
                    claimId = ClaimA, claimCode = "CLM-A", claimName = "Rapid relief", kind = "core", coreVersion = "2.0",
                    coreStatus = "approved",
                    cells = new object[]
                    {
                        new { countryCode = "TR", state = "approved", versionId = (Guid?)VersionTr, version = "1.0" },
                        new { countryCode = "UZ", state = "draft", versionId = (Guid?)VersionUz, version = "0.1" },
                        new { countryCode = "DE", state = "closed", versionId = (Guid?)null, version = (string?)null }
                    }
                },
                new
                {
                    claimId = ClaimB, claimCode = "CLM-B", claimName = "Long lasting", kind = "core", coreVersion = "1.0",
                    coreStatus = "draft", cells = Array.Empty<object>()
                }
            },
            expiringWindowDays = 30
        }
    });

    private const string Languages =
        """{"data":{"items":[{"valueCode":"TR","attributes":{"Languages":"tr"}},{"valueCode":"UZ","attributes":{"Languages":"uz,ru"}}]}}""";

    // ============================================================ claim-options

    [Fact]
    public async Task Claim_options_list_a_core_row_and_a_row_per_country_version()
    {
        var gateway = new StubGateway();
        var options = await ClaimOptionsAsync(gateway, "tr");

        Assert.Equal(4, options.Count); // A core, A·TR, A·UZ, B core — the closed DE cell has no version
        var core = options.Single(o => o.GetProperty("claimId").GetString() == ClaimA.ToString()
                                       && o.GetProperty("countryVersionId").ValueKind == JsonValueKind.Null);
        Assert.Equal("CLM-A", core.GetProperty("claimCode").GetString());
        Assert.Equal("Rapid relief", core.GetProperty("claimName").GetString());
        Assert.Equal("2.0", core.GetProperty("version").GetString());
        Assert.True(core.GetProperty("usable").GetBoolean());

        var tr = Row(options, VersionTr);
        Assert.Equal("TR", tr.GetProperty("countryCode").GetString());
        Assert.Equal("1.0", tr.GetProperty("version").GetString());
        Assert.Equal("approved", tr.GetProperty("status").GetString());
        Assert.Equal(["tr"], tr.GetProperty("languages").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.False(string.IsNullOrWhiteSpace(tr.GetProperty("countryName").GetString()));
        Assert.True(tr.GetProperty("usable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, tr.GetProperty("reason").ValueKind);

        Assert.Contains(gateway.Requests, r => r.Contains($"/claims/coverage?productId={ProductId}"));
        Assert.Single(gateway.Requests, r => r.Contains("/claims/coverage")); // ONE coverage call, no per-version read
    }

    [Fact]
    public async Task Claim_options_flag_unapproved_rows_not_approved()
    {
        var options = await ClaimOptionsAsync(new StubGateway(), "uz");

        var uz = Row(options, VersionUz);
        Assert.False(uz.GetProperty("usable").GetBoolean());
        Assert.Equal("not_approved", uz.GetProperty("reason").GetString());

        var coreB = options.Single(o => o.GetProperty("claimId").GetString() == ClaimB.ToString());
        Assert.False(coreB.GetProperty("usable").GetBoolean());
        Assert.Equal("not_approved", coreB.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Claim_options_flag_a_country_without_the_content_language_as_language_mismatch()
    {
        var options = await ClaimOptionsAsync(new StubGateway(), "en");

        var tr = Row(options, VersionTr);
        Assert.False(tr.GetProperty("usable").GetBoolean());
        Assert.Equal("language_mismatch", tr.GetProperty("reason").GetString());
        // The core row has no language rule on CRM — it stays usable.
        var coreA = options.Single(o => o.GetProperty("claimId").GetString() == ClaimA.ToString()
                                        && o.GetProperty("countryVersionId").ValueKind == JsonValueKind.Null);
        Assert.True(coreA.GetProperty("usable").GetBoolean());
    }

    [Fact]
    public async Task Claim_options_without_a_product_are_empty_and_never_call_crm()
    {
        var gateway = new StubGateway();
        var json = await InvokeClaimOptionsAsync(gateway, null, "tr");

        Assert.False(json.GetProperty("disabled").GetBoolean());
        Assert.True(json.GetProperty("requiresProduct").GetBoolean());
        Assert.Empty(json.GetProperty("options").EnumerateArray());
        Assert.DoesNotContain(gateway.Requests, r => r.Contains("/claims/coverage"));
    }

    [Theory]
    [InlineData(403, "ClaimPermissionMissing")]
    [InlineData(404, "ClaimEndpointMissing")]
    [InlineData(503, "ClaimOptionsUnavailable")]
    [InlineData(0, "ClaimOptionsUnavailable")] // gateway unreachable
    public async Task Claim_options_when_crm_refuses_are_disabled_with_a_reason_never_silently_empty(int status, string reason)
    {
        var gateway = new StubGateway { CoverageStatus = status };
        var json = await InvokeClaimOptionsAsync(gateway, ProductId, "tr");

        Assert.True(json.GetProperty("disabled").GetBoolean());
        Assert.Equal(reason, json.GetProperty("reason").GetString());
        Assert.False(json.TryGetProperty("options", out _));
    }

    [Fact]
    public async Task Claim_options_need_knowledge_read()
    {
        var result = await Controller(new StubGateway(), "crm.claim.read").ClaimOptions(ProductId, "tr", default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ============================================================ payload

    [Fact]
    public async Task Create_posts_the_selected_claim_refs()
    {
        var gateway = new StubGateway();
        var model = Model();
        model.ClaimRefs =
        [
            new() { ClaimId = ClaimA, ClaimCode = "CLM-A" },
            new() { ClaimId = ClaimA, ClaimCode = "CLM-A", CountryVersionId = VersionTr, CountryCode = "tr" }
        ];

        await Controller(gateway, "crm.knowledge.manage").Create(model, default);

        var refs = PostedClaimRefs(gateway);
        Assert.Equal(2, refs.Count);
        Assert.Equal("CLM-A", refs[0].GetProperty("claimCode").GetString());
        Assert.Equal(ClaimA.ToString(), refs[0].GetProperty("claimId").GetString());
        Assert.Equal(JsonValueKind.Null, refs[0].GetProperty("countryVersionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, refs[0].GetProperty("countryCode").ValueKind);
        Assert.Equal(VersionTr.ToString(), refs[1].GetProperty("countryVersionId").GetString());
        Assert.Equal("TR", refs[1].GetProperty("countryCode").GetString());
    }

    [Fact]
    public async Task Create_and_edit_post_an_empty_list_not_null_when_nothing_is_selected()
    {
        var create = new StubGateway();
        await Controller(create, "crm.knowledge.manage").Create(Model(), default);
        Assert.Empty(PostedClaimRefs(create));

        var edit = new StubGateway();
        await Controller(edit, "crm.knowledge.manage").Edit(ContentId, Model(), default);
        Assert.Contains(edit.Posts, p => p.Method == "PUT");
        Assert.Empty(PostedClaimRefs(edit)); // [] clears on CRM; a missing / null ClaimRefs would KEEP the stored refs
    }

    [Fact]
    public async Task Edit_round_trips_the_stored_claim_refs()
    {
        var gateway = new StubGateway();
        var view = Assert.IsType<ViewResult>(await Controller(gateway, "crm.knowledge.manage").Edit(ContentId, default));
        var model = Assert.IsType<KnowledgeContentEditViewModel>(view.Model);

        Assert.Equal(2, model.ClaimRefs.Count);
        Assert.Equal(ClaimA, model.ClaimRefs[1].ClaimId);
        Assert.Equal(VersionTr, model.ClaimRefs[1].CountryVersionId);
        Assert.Equal("TR", model.ClaimRefs[1].CountryCode);
        Assert.True(model.ClaimRefs[1].ClaimNeedsReview);

        var post = new StubGateway();
        await Controller(post, "crm.knowledge.manage").Edit(ContentId, model, default);
        var refs = PostedClaimRefs(post);
        Assert.Equal(2, refs.Count);
        Assert.Equal(ClaimB.ToString(), refs[0].GetProperty("claimId").GetString());
        Assert.Equal(JsonValueKind.Null, refs[0].GetProperty("countryVersionId").ValueKind);
        Assert.Equal(VersionTr.ToString(), refs[1].GetProperty("countryVersionId").GetString());
        Assert.Equal("TR", refs[1].GetProperty("countryCode").GetString());
    }

    // ============================================================ error codes → field errors

    [Theory]
    [MemberData(nameof(CrmClaimErrorCodes))]
    public async Task Every_crm_claim_error_code_is_a_claims_field_error_not_a_summary_error(string code)
    {
        var gateway = new StubGateway { WriteStatus = 409, WriteErrors = [code, "Claim 'CLM-A' (TR) — raw English message."] };
        var controller = Controller(gateway, "crm.knowledge.manage");
        var model = Model();

        Assert.IsType<ViewResult>(await controller.Create(model, default));

        var error = Assert.Single(model.ClaimRefErrors);
        Assert.Equal(code, error.Code);
        Assert.Equal("CLM-A (TR)", error.Subject);
        Assert.True(controller.ModelState.TryGetValue(string.Empty, out var summary) is false || summary!.Errors.Count == 0);
        Assert.Equal(code, Assert.Single(controller.ModelState[nameof(KnowledgeContentEditViewModel.ClaimRefs)]!.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_indexed_claim_error_names_the_posted_ref_and_other_errors_stay_in_the_summary()
    {
        var gateway = new StubGateway
        {
            WriteStatus = 400,
            WriteErrors = ["claim_product_mismatch", "ClaimRefs[1]: the claim is about another product than this content."]
        };
        var controller = Controller(gateway, "crm.knowledge.manage");
        var model = Model();
        model.ClaimRefs =
        [
            new() { ClaimId = ClaimB, ClaimCode = "CLM-B" },
            new() { ClaimId = ClaimA, ClaimCode = "CLM-A", CountryVersionId = VersionTr, CountryCode = "TR" }
        ];
        await controller.Create(model, default);
        Assert.Equal("CLM-A (TR)", Assert.Single(model.ClaimRefErrors).Subject);

        var other = new StubGateway { WriteStatus = 400, WriteErrors = ["ContentCode already exists."] };
        var second = Controller(other, "crm.knowledge.manage");
        var plain = Model();
        await second.Create(plain, default);
        Assert.Empty(plain.ClaimRefErrors);
        Assert.Equal("ContentCode already exists.", Assert.Single(second.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    // ============================================================ details card

    [Fact]
    public async Task Details_lists_linked_claims_with_names_versions_and_links_for_a_claim_reader()
    {
        var view = Assert.IsType<ViewResult>(
            await Controller(new StubGateway(), "crm.knowledge.read", "crm.claim.read").Details(ContentId, default));
        var linked = Assert.IsType<KnowledgeContentPageViewModel>(view.Model).LinkedClaims;

        Assert.Equal(2, linked.Count);
        Assert.Equal("Long lasting", linked[0].ClaimName);
        Assert.Equal($"/CRM/Claims/Edit/{ClaimB}", linked[0].Href);
        Assert.Equal("draft", linked[0].Status);
        Assert.Equal("Rapid relief", linked[1].ClaimName);
        Assert.Equal("1.0", linked[1].Version);
        Assert.Equal("review-required", linked[1].Status); // CRM's detail status wins over the coverage cell
        Assert.Equal($"/CRM/Claims/CountryVersions/{VersionTr}/Edit", linked[1].Href);
        Assert.True(linked[1].Ref.ClaimNeedsReview);
    }

    [Fact]
    public async Task Details_without_claim_read_shows_plain_text_and_never_reads_coverage()
    {
        var gateway = new StubGateway();
        var view = Assert.IsType<ViewResult>(await Controller(gateway, "crm.knowledge.read").Details(ContentId, default));
        var linked = Assert.IsType<KnowledgeContentPageViewModel>(view.Model).LinkedClaims;

        Assert.Equal(2, linked.Count);
        Assert.All(linked, l => Assert.Null(l.Href));
        Assert.Equal("CLM-A", linked[1].Ref.ClaimCode);
        Assert.DoesNotContain(gateway.Requests, r => r.Contains("/claims/coverage"));
    }

    // ============================================================ L10n (7 languages)

    private static readonly string[] Cultures = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // Real translations that happen to equal the English word.
    private static readonly HashSet<(string, string)> SameAsEnglish = [("fr", "ClaimStatus_inactive")];

    private static readonly string[] NewKeys =
    [
        "ClaimsSection", "ClaimRefsLabel", "ClaimRefsPlaceholder", "ClaimRefsHelp", "ClaimRefsProductFirst", "ClaimRefsNone",
        "ClaimRefsLoading", "ClaimOptionsUnavailable", "ClaimPermissionMissing", "ClaimEndpointMissing", "ClaimCore",
        "ClaimStatus_approved", "ClaimStatus_in-review", "ClaimStatus_draft", "ClaimStatus_review-required",
        "ClaimStatus_inactive", "ClaimStatus_archived", "ClaimReason_not_approved", "ClaimReason_language_mismatch",
        "ClaimRefNotInList", "ClaimRefsUnusableWarning", "ClaimRefsStaleWarning", "LinkedClaims", "NoLinkedClaims",
        "ClaimNeedsReview"
    ];

    public static TheoryData<string> CrmClaimErrorCodes()
    {
        var data = new TheoryData<string>();
        foreach (var code in ErrorCodesFromSource()) data.Add(code);
        return data;
    }

    private static string[] ErrorCodesFromSource()
    {
        // Anchored to production: the CRM domain constants, not a copy of the list.
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src",
            "Diten.CrmService.Domain", "Entities", "KnowledgeContent.cs"));
        var block = Regex.Match(source, @"class KnowledgeContentClaimErrors\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        return Regex.Matches(block, "=\\s*\"(?<code>[^\"]+)\"").Select(m => m.Groups["code"].Value).ToArray();
    }

    [Fact]
    public void Crm_claim_error_code_source_is_found()
    {
        Assert.Equal(10, ErrorCodesFromSource().Length);
    }

    [Fact]
    public void Every_new_key_and_every_crm_claim_error_is_translated_in_all_7_languages()
    {
        var keys = NewKeys.Concat(ErrorCodesFromSource().Select(code => "ClaimError_" + code)).ToList();
        var english = Values("en");
        foreach (var culture in Cultures)
        {
            var values = Values(culture);
            foreach (var key in keys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"KnowledgeIndex.{culture}.resx is missing '{key}'.");
                Assert.NotEqual(key, value);
                if (culture != "en" && !SameAsEnglish.Contains((culture, key)))
                    Assert.True(value != english[key], $"KnowledgeIndex.{culture}.resx '{key}' is the English text.");
            }
        }
    }

    [Fact]
    public void The_form_and_details_views_use_only_translated_keys()
    {
        var views = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "CRM", "Knowledge");
        var markup = File.ReadAllText(Path.Combine(views, "_Form.cshtml")) + File.ReadAllText(Path.Combine(views, "Details.cshtml"));
        // Keys read through the localizer, plus the JS bridge key list of the Claims section. A composed key's prefix
        // (ClaimError_ / ClaimStatus_) is covered by the 7-language test above.
        var bridge = Regex.Match(markup, @"claimL10nKeys = new\[\]\s*\{(?<keys>[^}]*)\}").Groups["keys"].Value;
        Assert.False(string.IsNullOrWhiteSpace(bridge), "The Claims section L10n bridge key list was not found.");
        var used = Regex.Matches(markup, "Localizer\\[\"(?<k>[^\"]+)\"\\]").Select(m => m.Groups["k"].Value)
            .Concat(Regex.Matches(bridge, "\"(?<k>[^\"]+)\"").Select(m => m.Groups["k"].Value))
            .Where(k => k.StartsWith("Claim", StringComparison.Ordinal) || k.EndsWith("LinkedClaims", StringComparison.Ordinal))
            .Where(k => !k.EndsWith("_", StringComparison.Ordinal))
            .Distinct().ToList();
        Assert.NotEmpty(used);
        var tr = Values("tr");
        Assert.All(used, k => Assert.True(tr.ContainsKey(k), $"'{k}' is used by a view but missing in KnowledgeIndex.tr.resx."));
    }

    private static Dictionary<string, string> Values(string culture)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Knowledge",
            $"KnowledgeIndex.{culture}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
    }

    // ============================================================ helpers

    private static JsonElement Row(List<JsonElement> options, Guid versionId) =>
        options.Single(o => o.GetProperty("countryVersionId").GetString() == versionId.ToString());

    private static async Task<List<JsonElement>> ClaimOptionsAsync(StubGateway gateway, string language)
    {
        var json = await InvokeClaimOptionsAsync(gateway, ProductId, language);
        Assert.False(json.GetProperty("disabled").GetBoolean());
        return json.GetProperty("options").EnumerateArray().Select(x => x.Clone()).ToList();
    }

    private static async Task<JsonElement> InvokeClaimOptionsAsync(StubGateway gateway, Guid? productId, string language)
    {
        var result = await Controller(gateway, "crm.knowledge.read").ClaimOptions(productId, language, default);
        var value = Assert.IsType<JsonResult>(result).Value;
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();
    }

    private static List<JsonElement> PostedClaimRefs(StubGateway gateway)
    {
        var body = gateway.Posts.Last().Body;
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("claimRefs", out var refs), "ClaimRefs is missing from the payload.");
        Assert.Equal(JsonValueKind.Array, refs.ValueKind);
        return refs.EnumerateArray().Select(x => x.Clone()).ToList();
    }

    private static KnowledgeContentEditViewModel Model() => new()
    {
        ContentCode = "KC-2026-0001", ContentTitle = "Leaflet", ContentType = "brochure", ContentStatus = "draft",
        SubjectId = Guid.NewGuid(), ProductId = ProductId, LanguageCode = "tr", ContentVersion = "1.0",
        EffectiveFrom = DateTimeOffset.UtcNow, Source = "manual", Url = "https://example.test/leaflet.pdf"
    };

    private static string ContentDetail() => JsonSerializer.Serialize(new
    {
        data = new
        {
            contentId = ContentId, contentCode = "KC-1", contentTitle = "Leaflet", contentType = "brochure",
            contentStatus = "draft", subjectId = Guid.Empty, languageCode = "tr", contentVersion = "1.0",
            effectiveFrom = DateTimeOffset.UtcNow, source = "manual",
            claimRefs = new object[]
            {
                new { claimCode = "CLM-B", claimId = ClaimB, countryVersionId = (Guid?)null, countryCode = (string?)null,
                      claimStatus = "draft", countryVersionStatus = (string?)null, claimNeedsReview = false },
                new { claimCode = "CLM-A", claimId = ClaimA, countryVersionId = (Guid?)VersionTr, countryCode = "TR",
                      claimStatus = "approved", countryVersionStatus = "review-required", claimNeedsReview = true }
            }
        }
    });

    private static KnowledgeController Controller(StubGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new KnowledgeController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<KnowledgeController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class StubGateway : HttpMessageHandler
    {
        public int CoverageStatus { get; init; } = 200;
        public int WriteStatus { get; init; } = 201;
        public string[] WriteErrors { get; init; } = [];
        public List<string> Requests { get; } = [];
        public List<(string Method, string Body)> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add(uri);
            if (request.Method != HttpMethod.Get && uri.Contains("/api/crm/knowledge/contents"))
            {
                Posts.Add((request.Method.Method, await request.Content!.ReadAsStringAsync(ct)));
                var body = WriteStatus < 300
                    ? JsonSerializer.Serialize(new { data = ContentId, isSuccessful = true, statusCode = WriteStatus })
                    : JsonSerializer.Serialize(new { data = (object?)null, isSuccessful = false, statusCode = WriteStatus, errors = WriteErrors });
                return Reply(WriteStatus, body);
            }

            if (uri.Contains("/claims/coverage"))
            {
                if (CoverageStatus == 0) throw new HttpRequestException("gateway down");
                return Reply(CoverageStatus, CoverageStatus == 200 ? Coverage : "{\"errors\":[\"x\"]}");
            }

            if (uri.Contains("/country-content-languages/")) return Reply(200, Languages);
            if (uri.EndsWith($"/api/crm/knowledge/contents/{ContentId}", StringComparison.OrdinalIgnoreCase))
                return Reply(200, ContentDetail());
            return Reply(200, "{\"data\":{\"items\":[]}}");
        }

        private static HttpResponseMessage Reply(int status, string body) =>
            new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
