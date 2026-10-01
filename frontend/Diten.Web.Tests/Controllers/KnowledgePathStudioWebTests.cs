using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.KnowledgePaths;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-KP-UI-1 — the Knowledge Path Studio on the Web layer: the UAS-001 gate (plain 403, no page), the lookups (only
/// bindable chains, country / language names, content language / product / publish / slot reasons, claim usability
/// reasons with the path-language text), the proxy allowlist, the 13 KP-1 error codes as user text, the 7-language
/// KnowledgePathStudio family + JS ↔ resx parity, and that no raw country / language / type / status code reaches the
/// page. Labels come from the REAL resx (ResourceManager) so a missing key is a failure, not an echo.
/// </summary>
public sealed class KnowledgePathStudioWebTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid PathId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LegacyPathId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid SubjectId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherProduct = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid ChainId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TypeNeed = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid TypeEvidence = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid TypeFatigue = Guid.Parse("50000000-0000-0000-0000-000000000003");
    private static readonly Guid ProfileId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid ContentTr = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid ContentEn = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid ContentDraft = Guid.Parse("70000000-0000-0000-0000-000000000003");
    private static readonly Guid ContentOther = Guid.Parse("70000000-0000-0000-0000-000000000004");
    private static readonly Guid StepId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid ClaimOk = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid ClaimDraft = Guid.Parse("90000000-0000-0000-0000-000000000002");
    private static readonly Guid ClaimNoTr = Guid.Parse("90000000-0000-0000-0000-000000000003");
    private static readonly Guid ClaimNoText = Guid.Parse("90000000-0000-0000-0000-000000000004");
    private static readonly Guid VersionOk = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid VersionDraft = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid VersionNoText = Guid.Parse("a0000000-0000-0000-0000-000000000003");

    private const string Read = "crm.knowledge.path.read";
    private const string Manage = "crm.knowledge.path.manage";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ============================================================ UAS-001 gate

    [Fact]
    public async Task Without_read_permission_every_page_is_a_plain_403_and_never_a_view()
    {
        var controller = Controller(new StubGateway());
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(controller.Index()).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(controller.Workspace(PathId)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(controller.Details(PathId)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(controller.Create()).StatusCode);

        var list = await controller.StudioPathList(default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(list).StatusCode);
        var workspace = await controller.StudioWorkspace(PathId, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(workspace).StatusCode);
    }

    [Fact]
    public void With_read_permission_the_list_and_the_workspace_render_and_details_redirects_to_the_workspace()
    {
        var controller = Controller(new StubGateway(), Read);
        Assert.IsType<ViewResult>(controller.Index());
        var workspace = Assert.IsType<ViewResult>(controller.Workspace(PathId));
        Assert.Equal(PathId.ToString(), workspace.ViewData["PathId"]);
        Assert.Equal(false, workspace.ViewData["CanManage"]);
        var details = Assert.IsType<RedirectToActionResult>(controller.Details(PathId));
        Assert.Equal(nameof(KnowledgePathsController.Workspace), details.ActionName);
        // Creating needs manage, reading is not enough.
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(controller.Create()).StatusCode);
    }

    // ============================================================ lookups

    [Fact]
    public async Task Chain_lookup_lists_only_published_branched_chains_with_names()
    {
        var gateway = new StubGateway();
        var data = Data(await Controller(gateway, Read).ChainTemplateLookup(null, default));

        var chain = Assert.Single(data.EnumerateArray());
        Assert.Equal(ChainId.ToString(), chain.GetProperty("id").GetString());
        Assert.Equal("Almiba HD chain", chain.GetProperty("name").GetString());
        Assert.Equal("Almiba", chain.GetProperty("subjectName").GetString());
        Assert.Equal("ALMIBA 1 g", chain.GetProperty("productName").GetString());
        Assert.Equal(["Nephrology"], chain.GetProperty("audiences").EnumerateArray().Select(a => a.GetString()!).ToArray());
        var branches = chain.GetProperty("branches").EnumerateArray().ToList();
        Assert.Equal(["Fatigue", "Main"], branches.Select(b => b.GetProperty("name").GetString()!).ToArray()); // SortOrder
        Assert.Equal("Need", branches[1].GetProperty("steps")[0].GetProperty("name").GetString());
        Assert.Contains(gateway.Requests, r => r.Contains("concept-chain-templates?status=published&includeArchived=false"));
    }

    [Fact]
    public async Task Country_lookup_gives_icu_names_and_native_language_names_never_bare_codes()
    {
        using var _ = new UiCulture("tr");
        var data = Data(await Controller(new StubGateway(), Read).CountryLookup(default)).EnumerateArray().ToList();

        var tr = data.Single(c => c.GetProperty("code").GetString() == "TR");
        Assert.Equal("Türkiye", tr.GetProperty("name").GetString());
        var turkish = tr.GetProperty("languageDetails")[0];
        Assert.Equal("Türkçe", turkish.GetProperty("nativeName").GetString());
        var uz = data.Single(c => c.GetProperty("code").GetString() == "UZ");
        Assert.NotEqual("UZ", uz.GetProperty("name").GetString());
        Assert.Equal(["uz", "ru"], uz.GetProperty("languages").EnumerateArray().Select(l => l.GetString()!).ToArray());
        Assert.All(uz.GetProperty("languageDetails").EnumerateArray(),
            l => Assert.NotEqual(l.GetProperty("code").GetString(), l.GetProperty("nativeName").GetString()));
    }

    [Fact]
    public async Task Country_lookup_is_503_reference_set_unavailable_when_country_codes_cannot_be_read()
    {
        var result = await Controller(new StubGateway { CountriesDown = true }, Read).CountryLookup(default);
        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, problem.StatusCode);
        Assert.Contains("reference_set_unavailable", JsonSerializer.Serialize(problem.Value));
    }

    [Fact]
    public async Task Path_contents_explain_why_a_content_cannot_be_added_with_labels_not_codes()
    {
        using var _ = new UiCulture("tr");
        var data = Data(await Controller(new StubGateway(), Read).PathContentLookup(PathId, "BR1", TypeNeed, default))
            .EnumerateArray().ToDictionary(c => c.GetProperty("contentId").GetString()!);

        var ok = data[ContentTr.ToString()];
        Assert.True(ok.GetProperty("addable").GetBoolean());
        Assert.Equal("Sunum", ok.GetProperty("typeLabel").GetString());          // never "presentation"
        Assert.Equal("Türkçe", ok.GetProperty("languageName").GetString());      // never "tr"
        Assert.Equal("Yayında", ok.GetProperty("statusLabel").GetString());      // never "published"
        Assert.Equal("core-message", ok.GetProperty("defaultStepType").GetString());

        Assert.Equal(("language", "dil farklı"), Reason(data[ContentEn.ToString()]));
        Assert.Equal(("not_published", "yayında değil"), Reason(data[ContentDraft.ToString()]));
        Assert.Equal(("product", "ürün farklı"), Reason(data[ContentOther.ToString()]));
        Assert.Equal("Klinik özet", data[ContentDraft.ToString()].GetProperty("typeLabel").GetString());
    }

    [Fact]
    public async Task Path_contents_on_a_full_slot_are_not_addable_slot_full()
    {
        using var _ = new UiCulture("tr");
        // BR1 / Evidence is at its maximum (count 1 / max 1) in the path's chain conformance.
        var data = Data(await Controller(new StubGateway(), Read).PathContentLookup(PathId, "BR1", TypeEvidence, default))
            .EnumerateArray().ToDictionary(c => c.GetProperty("contentId").GetString()!);
        Assert.Equal(("slot_full", "adım dolu"), Reason(data[ContentTr.ToString()]));
    }

    [Fact]
    public async Task Path_claims_resolve_the_path_country_version_text_and_usability_reason()
    {
        using var _ = new UiCulture("tr");
        var gateway = new StubGateway();
        var json = JsonOf(await Controller(gateway, Read).PathClaimLookup(PathId, null, default));
        Assert.False(json.GetProperty("disabled").GetBoolean());
        var options = json.GetProperty("options").EnumerateArray().ToDictionary(o => o.GetProperty("code").GetString()!);

        var ok = options["CLM-OK"];
        Assert.True(ok.GetProperty("usable").GetBoolean());
        Assert.Equal("Levokarnitin enerji metabolizmasını destekler.", ok.GetProperty("text").GetString());
        Assert.Equal("Diyaliz hastalarında", ok.GetProperty("qualifier").GetString());
        Assert.Equal("1.0", ok.GetProperty("countryVersion").GetString());
        Assert.Equal("Onaylı", ok.GetProperty("statusLabel").GetString());
        Assert.False(ok.GetProperty("addable").GetBoolean());                  // already on the path
        Assert.Equal("zaten yolda", ok.GetProperty("addReasonLabel").GetString());

        Assert.Equal(("not_approved", "Yolun ülkesinde onaylı değil"), ClaimReason(options["CLM-DRAFT"]));
        Assert.Equal(("no_country_version", "Yolun ülkesinde sürümü yok"), ClaimReason(options["CLM-NOTR"]));
        Assert.Equal(("language_mismatch", "Yolun dilinde metni yok"), ClaimReason(options["CLM-NOTEXT"]));
        Assert.True(options["CLM-DRAFT"].GetProperty("addable").GetBoolean()); // unusable may still be placed (KP-3 gates)

        Assert.Single(gateway.Requests, r => r.Contains("/claims/coverage?productId=" + ProductId));
    }

    [Fact]
    public async Task Path_claim_search_matches_code_name_or_path_language_text()
    {
        var byText = JsonOf(await Controller(new StubGateway(), Read).PathClaimLookup(PathId, "metabolizma", default));
        Assert.Equal(["CLM-OK"], byText.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("code").GetString()!).ToArray());
        var byCode = JsonOf(await Controller(new StubGateway(), Read).PathClaimLookup(PathId, "notr", default));
        Assert.Equal(["CLM-NOTR"], byCode.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("code").GetString()!).ToArray());
    }

    [Fact]
    public async Task Path_claims_without_claim_permission_are_disabled_not_empty()
    {
        var json = JsonOf(await Controller(new StubGateway { CoverageStatus = 403 }, Read).PathClaimLookup(PathId, null, default));
        Assert.True(json.GetProperty("disabled").GetBoolean());
        Assert.Equal("ClaimPermissionMissing", json.GetProperty("reason").GetString());
    }

    // ============================================================ list + workspace (no raw codes)

    [Fact]
    public async Task Studio_list_rows_carry_names_product_chain_and_the_legacy_mark()
    {
        using var _ = new UiCulture("tr");
        var rows = Data(await Controller(new StubGateway(), Read).StudioPathList(default)).EnumerateArray()
            .ToDictionary(r => r.GetProperty("pathId").GetString()!);

        var chained = rows[PathId.ToString()];
        Assert.False(chained.GetProperty("isLegacyUnapproved").GetBoolean());
        Assert.Equal(("Türkiye", "Türkçe"), (chained.GetProperty("countryName").GetString(), chained.GetProperty("languageName").GetString()));
        Assert.Equal("ALMIBA 1 g", chained.GetProperty("productName").GetString());
        Assert.Equal(("Almiba HD chain", "1.2"), (chained.GetProperty("chainName").GetString(), chained.GetProperty("chainVersion").GetString()));
        Assert.Equal("Taslak", chained.GetProperty("statusLabel").GetString());

        var legacy = rows[LegacyPathId.ToString()];
        Assert.True(legacy.GetProperty("isLegacyUnapproved").GetBoolean());
        Assert.Equal("ALMIBA 1 g", legacy.GetProperty("productName").GetString()); // via its subject
        Assert.Equal(JsonValueKind.Null, legacy.GetProperty("chainName").ValueKind);
        Assert.Equal("Yayında", legacy.GetProperty("statusLabel").GetString());
    }

    [Fact]
    public async Task Workspace_model_has_branches_slots_items_and_the_field_order_without_raw_codes()
    {
        using var _ = new UiCulture("tr");
        var model = Data(await Controller(new StubGateway(), Read, Manage).StudioWorkspace(PathId, default));

        Assert.True(model.GetProperty("canEdit").GetBoolean());
        Assert.False(model.GetProperty("canBind").GetBoolean());
        var context = model.GetProperty("context");
        Assert.Equal(("Türkiye", "Türkçe", "ALMIBA 1 g"), (context.GetProperty("countryName").GetString(),
            context.GetProperty("languageNativeName").GetString(), context.GetProperty("productName").GetString()));

        var branches = model.GetProperty("branches").EnumerateArray().ToList();
        Assert.Equal(["Fatigue", "Main"], branches.Select(b => b.GetProperty("name").GetString()!).ToArray());
        var evidence = branches[1].GetProperty("slots")[1];
        Assert.Equal(("Evidence", 1, 1, "Tamam", true),
            (evidence.GetProperty("name").GetString(), evidence.GetProperty("min").GetInt32(), evidence.GetProperty("max").GetInt32(),
             evidence.GetProperty("statusLabel").GetString(), evidence.GetProperty("isFull").GetBoolean()));
        var items = evidence.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["content", "claim"], items.Select(i => i.GetProperty("kind").GetString()!).ToArray()); // by position
        Assert.Equal(("Sunum", "Türkçe", "Yayında"), (items[0].GetProperty("typeLabel").GetString(),
            items[0].GetProperty("languageName").GetString(), items[0].GetProperty("statusLabel").GetString()));
        Assert.Equal("Levokarnitin enerji metabolizmasını destekler.", items[1].GetProperty("text").GetString());
        Assert.Equal("Eksik", branches[1].GetProperty("slots")[0].GetProperty("statusLabel").GetString());

        var sequence = model.GetProperty("sequence").EnumerateArray().ToList();
        Assert.Equal("Almiba sunumu", Assert.Single(sequence).GetProperty("title").GetString());

        // No raw code is offered for display: every *Label / *Name field is a label.
        var page = JsonSerializer.Serialize(model);
        Assert.DoesNotContain("\"typeLabel\":\"presentation\"", page);
        Assert.DoesNotContain("\"languageName\":\"tr\"", page);
        Assert.DoesNotContain("\"countryName\":\"TR\"", page);
        Assert.DoesNotContain("\"statusLabel\":\"draft\"", page);
        Assert.DoesNotContain("\"statusLabel\":\"approved\"", page);
    }

    [Fact]
    public async Task A_legacy_draft_path_workspace_is_read_only_offers_bind_and_lists_its_steps()
    {
        var model = Data(await Controller(new StubGateway(), Read, Manage).StudioWorkspace(LegacyPathId, default));
        Assert.True(model.GetProperty("isLegacyUnapproved").GetBoolean());
        Assert.False(model.GetProperty("canEdit").GetBoolean());
        Assert.True(model.GetProperty("canBind").GetBoolean());
        Assert.Equal(JsonValueKind.Null, model.GetProperty("context").ValueKind);
        Assert.Single(model.GetProperty("legacySteps").EnumerateArray());
    }

    // ============================================================ proxy allowlist

    [Fact]
    public void The_proxy_surface_is_exactly_the_allowlist()
    {
        var routes = typeof(KnowledgePathsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Select(a => $"{a.HttpMethods.Single()} {a.Template}"))
            .Where(r => r.Contains(" api/"))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();
        string[] expected =
        [
            "GET api/audience-profiles", "GET api/concept-nodes", "GET api/contents", "GET api/contract",
            "GET api/lookups/chain-templates", "GET api/lookups/countries", "GET api/lookups/path-claims",
            "GET api/lookups/path-contents", "GET api/paths", "GET api/paths/{pathId:guid}",
            "GET api/paths/{pathId:guid}/steps", "GET api/studio/paths", "GET api/studio/paths/{pathId:guid}",
            "GET api/subjects", "GET api/topics",
            "POST api/paths", "POST api/paths/{pathId:guid}/archive", "POST api/paths/{pathId:guid}/bind-chain",
            "POST api/paths/{pathId:guid}/claims", "POST api/paths/{pathId:guid}/claims/{claimId:guid}/arrange",
            "POST api/paths/{pathId:guid}/claims/{claimId:guid}/remove", "POST api/paths/{pathId:guid}/new-version",
            "POST api/paths/{pathId:guid}/publish", "POST api/paths/{pathId:guid}/steps",
            "POST api/paths/{pathId:guid}/steps/{stepId:guid}/archive", "PUT api/paths/{pathId:guid}/steps/{stepId:guid}",
            // WP-KP-UI-2 — KP-2 / KP-3 proxies + the studio review reads.
            "GET api/paths/{pathId:guid}/revisions", "GET api/paths/{pathId:guid}/revisions/{revisionId:guid}",
            "GET api/paths/{pathId:guid}/revisions/{revisionId:guid}/artifact", "GET api/paths/{pathId:guid}/review-history",
            "GET api/paths/{pathId:guid}/usage", "GET api/studio/paths/{pathId:guid}/claim-suggestions",
            "GET api/studio/paths/{pathId:guid}/diff", "GET api/studio/paths/{pathId:guid}/mapping",
            "GET api/studio/paths/{pathId:guid}/review", "GET api/studio/paths/{pathId:guid}/revisions/{revisionId:guid}/view",
            "GET api/studio/paths/{pathId:guid}/usage",
            "POST api/paths/{pathId:guid}/revisions/{revisionId:guid}/decision",
            "POST api/paths/{pathId:guid}/revisions/{revisionId:guid}/notes",
            "POST api/paths/{pathId:guid}/revisions/{revisionId:guid}/notes/{noteId:guid}/resolve",
            "POST api/paths/{pathId:guid}/revisions/{revisionId:guid}/release",
            "POST api/paths/{pathId:guid}/revisions/{revisionId:guid}/render",
            "POST api/paths/{pathId:guid}/revisions/{revisionId:guid}/withdraw",
            "POST api/paths/{pathId:guid}/submit-review", "POST api/paths/{pathId:guid}/withdraw-review"
        ];
        Assert.Equal(expected.OrderBy(r => r, StringComparer.Ordinal), routes);
    }

    [Fact]
    public async Task Studio_writes_need_manage_and_forward_to_the_kp1_endpoints()
    {
        var body = JsonDocument.Parse("""{"claimId":"90000000-0000-0000-0000-000000000001"}""").RootElement;
        var readOnly = await Controller(new StubGateway(), Read).AddClaim(PathId, body, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(readOnly).StatusCode);

        var gateway = new StubGateway();
        var controller = Controller(gateway, Read, Manage);
        await controller.AddClaim(PathId, body, default);
        await controller.ArrangeClaim(PathId, ClaimOk, JsonDocument.Parse("""{"position":2}""").RootElement, default);
        await controller.RemoveClaim(PathId, ClaimOk, default);
        await controller.BindChain(LegacyPathId, JsonDocument.Parse("""{"chainTemplateId":"x"}""").RootElement, default);
        await controller.CreatePath(JsonDocument.Parse("""{"pathName":"P"}""").RootElement, default);
        Assert.Equal(
        [
            $"POST /api/crm/knowledge/paths/{PathId}/claims",
            $"POST /api/crm/knowledge/paths/{PathId}/claims/{ClaimOk}/arrange",
            $"POST /api/crm/knowledge/paths/{PathId}/claims/{ClaimOk}/remove",
            $"POST /api/crm/knowledge/paths/{LegacyPathId}/bind-chain",
            "POST /api/crm/knowledge/paths"
        ], gateway.Writes.Select(w => w.Method + " " + new Uri(w.Url).AbsolutePath));

        var tenant = await controller.CreatePath(JsonDocument.Parse("""{"tenantId":"x"}""").RootElement, default);
        Assert.IsType<BadRequestObjectResult>(tenant);
    }

    // ============================================================ error codes → user text

    [Fact]
    public void The_thirteen_kp1_error_codes_are_the_crm_codes()
    {
        // Anchored to production: the CRM domain constants (KP-1), not a copy of the list.
        var entities = Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities");
        string Const(string file, string type, string name)
        {
            var body = Regex.Match(File.ReadAllText(Path.Combine(entities, file)), $@"class {type}\s*\{{(?<b>.*?)\n\}}", RegexOptions.Singleline).Groups["b"].Value;
            return Regex.Match(body, $"const string {name}\\s*=\\s*\"(?<c>[^\"]+)\"").Groups["c"].Value;
        }

        var crm = new[]
        {
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "ChainTemplateInvalid"),
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "ChainTemplateRequired"),
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "ChainSubjectMismatch"),
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "PathIdentityLocked"),
            Const("ContentSet.cs", "ChainContextErrors", "CountryInvalid"),
            Const("ContentSet.cs", "ChainContextErrors", "LanguageNotInCountry"),
            Const("ContentSet.cs", "ChainContextErrors", "ReferenceSetUnavailable"),
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "ChainSlotInvalid"),
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "ChainSlotFull"),
            Const("KnowledgePath.cs", "KnowledgePathStudioErrors", "ChainSlotMoveForbidden"),
            Const("ContentSet.cs", "ChainContextErrors", "ComponentLanguageMismatch"),
            Const("KnowledgeContent.cs", "KnowledgeContentClaimErrors", "ClaimProductMismatch"),
            Const("KnowledgeContent.cs", "KnowledgeContentClaimErrors", "ClaimRefDuplicate")
        };
        Assert.Equal(13, crm.Distinct().Count());
        Assert.Equal(crm, KnowledgePathsController.StudioErrorCodes);
    }

    [Fact]
    public void Every_error_code_has_a_user_text_in_seven_languages_and_the_client_maps_it()
    {
        var script = Script("studio-common.js");
        foreach (var code in KnowledgePathsController.StudioErrorCodes)
        {
            Assert.Matches($@"\b{Regex.Escape(code)}: \(\) => t\('Err_{Regex.Escape(code)}'\)", script);
            foreach (var language in Languages)
            {
                var values = Values(language);
                Assert.True(values.TryGetValue("Err_" + code, out var text) && !string.IsNullOrWhiteSpace(text), $"{language}: Err_{code}");
                Assert.DoesNotContain(code, text);   // the user text never repeats the raw code
            }
        }
    }

    // ============================================================ L10n

    [Fact]
    public void Studio_family_has_the_same_non_empty_keys_in_seven_languages_without_echo()
    {
        var reference = Values("en").Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(reference.Count > 150);
        foreach (var language in Languages)
        {
            var values = Values(language);
            Assert.True(reference.SetEquals(values.Keys),
                $"{language}: missing [{string.Join(", ", reference.Except(values.Keys))}] extra [{string.Join(", ", values.Keys.Except(reference))}]");
            Assert.All(values, kv =>
            {
                Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{language}: '{kv.Key}' is empty");
                Assert.NotEqual(kv.Key, kv.Value);
            });
        }
    }

    [Theory]
    [InlineData("studio-common.js")]
    [InlineData("create.js")]
    [InlineData("workspace.js")]
    [InlineData("index.js")]
    public void Every_key_a_studio_script_reads_exists_in_the_family(string script)
    {
        var used = Regex.Matches(Script(script), @"\bt\('([A-Za-z0-9_\-]+)'").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(used);
        var keys = Values("en");
        Assert.All(used, key => Assert.True(keys.ContainsKey(key), $"{script} reads '{key}' which KnowledgePathStudio.en.resx lacks"));
    }

    [Fact]
    public void Every_view_key_exists_in_the_family()
    {
        var keys = Values("en");
        // WP-KP-UI-2 — the bind modal became the legacy wizard; the review tabs / modals / reviewer page are studio views too.
        foreach (var view in new[] { "Create.cshtml", "Workspace.cshtml", "_LegacyWizard.cshtml", "_ReviewTabs.cshtml", "_SubmitModal.cshtml",
                     "Review.cshtml", "Index.cshtml", "_DataTable.cshtml", "_Filter.cshtml" })
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "CRM", "KnowledgePaths", view));
            var localizer = text.Contains("IHtmlLocalizer<KnowledgePathStudio> StudioLocalizer") ? "StudioLocalizer" : "Localizer";
            var used = Regex.Matches(text, $@"(?<![A-Za-z]){localizer}\[""([A-Za-z0-9_\-]+)""\]").Select(m => m.Groups[1].Value).Distinct();
            Assert.All(used, key => Assert.True(keys.ContainsKey(key), $"{view} reads '{key}' which KnowledgePathStudio.en.resx lacks"));
        }
    }

    [Fact]
    public void Dynamic_label_families_cover_every_crm_value()
    {
        var keys = Values("en");
        var domain = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.CrmService", "src", "Diten.CrmService.Domain", "Entities", "KnowledgeContent.cs"));
        var types = Regex.Matches(Regex.Match(domain, @"class KnowledgeContentTypes\s*\{(?<b>.*?)\n\}", RegexOptions.Singleline).Groups["b"].Value,
            "const string \\w+ = \"(?<c>[^\"]+)\"").Select(m => m.Groups["c"].Value).ToList();
        Assert.True(types.Count >= 15);
        Assert.All(types, type => Assert.True(keys.ContainsKey("CType_" + type), $"CType_{type} missing"));
        Assert.All(["draft", "review", "approved", "published", "inactive", "archived", "other"], s => Assert.True(keys.ContainsKey("Status_" + s)));
        Assert.All(["draft", "in-review", "approved", "review-required", "inactive", "archived", "other"], s => Assert.True(keys.ContainsKey("ClaimState_" + s)));
        Assert.All(["not_approved", "no_country_version", "language_mismatch"], s => Assert.True(keys.ContainsKey("Reason_" + s)));
        Assert.All(["not_published", "language", "product", "slot_full", "on_path"], s => Assert.True(keys.ContainsKey("AddReason_" + s)));
        Assert.All(["ok", "under", "over"], s => Assert.True(keys.ContainsKey("Conf_" + s)));
        Assert.All(["ClaimNoProduct", "ClaimPermissionMissing", "ClaimOptionsUnavailable"], s => Assert.True(keys.ContainsKey(s)));
    }

    [Fact]
    public void The_studio_bridge_merges_pascal_case_and_styles_use_theme_variables_and_logical_properties()
    {
        var common = Script("studio-common.js");
        Assert.Contains("toPascal(k)", common);
        Assert.Contains("window.L10n = Object.assign", common);
        var workspace = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "CRM", "KnowledgePaths", "Workspace.cshtml"));
        var style = Regex.Match(workspace, "<style>(?<s>.*?)</style>", RegexOptions.Singleline).Groups["s"].Value;
        Assert.DoesNotMatch("#[0-9A-Fa-f]{3,6}\\b", style);                     // no fixed hex colour (dark theme)
        Assert.DoesNotMatch(@"(margin|padding|border)-(left|right)", style);    // logical properties (RTL)
        Assert.Contains("try { window.localStorage", common);                    // storage guarded
    }

    // ============================================================ helpers

    private static (string?, string?) Reason(JsonElement row) =>
        (row.GetProperty("reason").GetString(), row.GetProperty("reasonLabel").GetString());

    private static (string?, string?) ClaimReason(JsonElement row)
    {
        Assert.False(row.GetProperty("usable").GetBoolean());
        return (row.GetProperty("reason").GetString(), row.GetProperty("reasonLabel").GetString());
    }

    private static JsonElement JsonOf(IActionResult result)
    {
        var value = result switch
        {
            JsonResult json => json.Value,
            ObjectResult obj => obj.Value,
            _ => throw new InvalidOperationException(result.GetType().Name)
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();
    }

    private static JsonElement Data(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return JsonDocument.Parse(JsonSerializer.Serialize(ok.Value)).RootElement.GetProperty("data").Clone();
    }

    private static KnowledgePathsController Controller(StubGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var factory = new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var controller = new KnowledgePathsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            new StringLocalizer<KnowledgePathStudio>(factory), NullLogger<KnowledgePathsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        controller.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        return controller;
    }

    private static string Script(string name) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM", "KnowledgePaths", name));

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "KnowledgePaths", $"KnowledgePathStudio.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
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

    private sealed class UiCulture : IDisposable
    {
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

        public UiCulture(string name)
        {
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            CultureInfo.CurrentCulture = new CultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _ui;
            CultureInfo.CurrentCulture = _culture;
        }
    }

    // ---------------- gateway stub (the CRM / BRD shapes KP-1 returns) ----------------

    private sealed class StubGateway : HttpMessageHandler
    {
        public bool CountriesDown { get; init; }
        public int CoverageStatus { get; init; } = 200;
        public List<string> Requests { get; } = [];
        public List<(string Method, string Url, string Body)> Writes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(url);
            if (request.Method != HttpMethod.Get)
            {
                Writes.Add((request.Method.Method, url, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
                return Reply(200, "{\"data\":true}");
            }

            if (path.EndsWith("/COUNTRY_CODES/published-values"))
                return CountriesDown ? Reply(503, "{}") : Reply(200, Data(new { items = new[] { new { valueCode = "TR" }, new { valueCode = "UZ" } } }));
            if (path.EndsWith("/country-content-languages/published-values"))
                return Reply(200, Data(new
                {
                    items = new object[]
                    {
                        new { valueCode = "TR", attributes = new { Languages = "tr" } },
                        new { valueCode = "UZ", attributes = new { Languages = "uz,ru" } }
                    }
                }));
            if (path == $"/api/crm/knowledge/paths/{PathId}") return Reply(200, Data(ChainedPath()));
            if (path == $"/api/crm/knowledge/paths/{LegacyPathId}") return Reply(200, Data(LegacyPath()));
            if (path == "/api/crm/knowledge/paths")
                return Reply(200, Data(new { items = url.Contains("pathCode=") ? Array.Empty<object>() : PathRows() }));
            if (path == $"/api/crm/knowledge/concept-chain-templates/{ChainId}") return Reply(200, Data(Template(ChainId, "published", true)));
            if (path == "/api/crm/knowledge/concept-chain-templates")
                return Reply(200, Data(new
                {
                    items = new[]
                    {
                        Template(ChainId, "published", true),
                        Template(Guid.NewGuid(), "published", false),          // flat (no branches)
                        Template(Guid.NewGuid(), "draft", true),               // not published
                        Template(Guid.NewGuid(), "published", true, emptyBranch: true),
                        Template(Guid.NewGuid(), "published", true, archived: true)
                    }
                }));
            if (path == "/api/crm/knowledge/subjects")
                return Reply(200, Data(new
                {
                    items = new[]
                    {
                        new
                        {
                            subjectId = SubjectId, subjectCode = "ALM", subjectName = "Almiba",
                            externalReferences = new object[]
                            {
                                new { sourceSystem = "global-product", externalId = OtherProduct.ToString(), externalName = "Other", isPrimary = false },
                                new { sourceSystem = "global-product", externalId = ProductId.ToString(), externalCode = "ALMIBA", externalName = "ALMIBA 1 g", isPrimary = true }
                            }
                        }
                    }
                }));
            if (path == "/api/crm/knowledge/concept-types")
                return Reply(200, Data(new
                {
                    items = new[]
                    {
                        new { conceptTypeId = TypeNeed, conceptTypeName = "Need" },
                        new { conceptTypeId = TypeEvidence, conceptTypeName = "Evidence" },
                        new { conceptTypeId = TypeFatigue, conceptTypeName = "Fatigue step" }
                    }
                }));
            if (path == "/api/crm/knowledge/audience-profiles")
                return Reply(200, Data(new { items = new[] { new { audienceProfileId = ProfileId, profileName = "Nephrology" } } }));
            if (path == "/api/crm/knowledge/contents") return Reply(200, Data(new { items = Contents() }));
            if (path.StartsWith("/api/crm/knowledge/contents/"))
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                var content = Contents().FirstOrDefault(c => JsonSerializer.Serialize(c).Contains(id));
                return content is null ? Reply(404, "{}") : Reply(200, Data(content));
            }
            if (path == "/api/crm/content-composition/claims/coverage")
                return CoverageStatus != 200 ? Reply(CoverageStatus, "{}") : Reply(200, Data(new { rows = CoverageRows() }));
            if (path == $"/api/crm/content-composition/claims/country-versions/{VersionOk}")
                return Reply(200, Data(new
                {
                    texts = new[] { new { languageCode = "tr", text = "Levokarnitin enerji metabolizmasını destekler." }, new { languageCode = "en", text = "Supports energy metabolism." } },
                    qualifiers = new[] { new { languageCode = "tr", text = "Diyaliz hastalarında" } }
                }));
            if (path == $"/api/crm/content-composition/claims/country-versions/{VersionDraft}")
                return Reply(200, Data(new { texts = new[] { new { languageCode = "tr", text = "Taslak metin" } }, qualifiers = Array.Empty<object>() }));
            if (path == $"/api/crm/content-composition/claims/country-versions/{VersionNoText}")
                return Reply(200, Data(new { texts = new[] { new { languageCode = "en", text = "English only" } }, qualifiers = Array.Empty<object>() }));
            return Reply(200, "{\"data\":{\"items\":[]}}");
        }

        private static string Data(object data) => JsonSerializer.Serialize(new { data });

        private static HttpResponseMessage Reply(int status, string body) =>
            new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static object Template(Guid id, string status, bool branched, bool emptyBranch = false, bool archived = false) => new
        {
            conceptChainTemplateId = id, subjectId = SubjectId, chainCode = "TPL-ALM", chainName = id == ChainId ? "Almiba HD chain" : "Other chain",
            chainVersion = "1.2", status, isArchived = archived, forWhomAudienceProfileIds = new[] { ProfileId },
            branches = !branched ? Array.Empty<object>() : new object[]
            {
                new
                {
                    branchCode = "BR1", branchName = "Main", sortOrder = 1,
                    steps = new object[]
                    {
                        new { conceptTypeId = TypeNeed, minSelection = 1, maxSelection = (int?)2 },
                        new { conceptTypeId = TypeEvidence, minSelection = 1, maxSelection = (int?)1 }
                    }
                },
                new
                {
                    branchCode = "BR2", branchName = "Fatigue", sortOrder = 0,
                    steps = emptyBranch ? Array.Empty<object>() : new object[] { new { conceptTypeId = TypeFatigue, minSelection = 0, maxSelection = (int?)null } }
                }
            }
        };

        private static object[] PathRows() =>
        [
            new { pathId = PathId, pathCode = "KP-2026-AAA111", pathName = "Almiba detay", pathVersion = "1.0", pathStatus = "draft",
                  subjectId = SubjectId, countryCode = "TR", languageCode = "tr", chainTemplateId = ChainId, isLegacyUnapproved = false, isArchived = false },
            new { pathId = LegacyPathId, pathCode = "KP-114", pathName = "Eski yol", pathVersion = "1.0", pathStatus = "published",
                  subjectId = SubjectId, countryCode = (string?)null, languageCode = "tr", chainTemplateId = (Guid?)null, isLegacyUnapproved = true, isArchived = false }
        ];

        private static object ChainedPath() => new
        {
            pathId = PathId, pathCode = "KP-2026-AAA111", pathName = "Almiba detay", pathVersion = "1.0", pathStatus = "draft",
            subjectId = SubjectId, countryCode = "TR", languageCode = "tr", isLegacyUnapproved = false, isStepSetFrozen = false, isArchived = false,
            chainTemplate = new { id = ChainId, code = "TPL-ALM", name = "Almiba HD chain", version = "1.2" },
            derivedContext = new
            {
                productId = ProductId, productCode = "ALMIBA", productName = "ALMIBA 1 g",
                audiences = new[] { new { audienceProfileId = ProfileId, profileCode = "AUDP-NEF", profileName = "Nephrology" } }
            },
            steps = new object[]
            {
                new
                {
                    stepId = StepId, stepOrder = 10, stepCode = "S-1", stepTitle = "Almiba sunumu", stepType = "core-message",
                    contentId = ContentTr, isRequired = true, estimatedDurationMinutes = 3, isArchived = false,
                    branchConditions = Array.Empty<object>(),
                    arrangement = new { chainStepId = TypeEvidence, branchCode = "BR1", position = 0 }
                }
            },
            claims = new object[]
            {
                new
                {
                    claimId = ClaimOk, claimCode = "CLM-OK", name = "Metabolizma", text = "Levokarnitin enerji metabolizmasını destekler.",
                    qualifier = "Diyaliz hastalarında", countryVersion = "1.0", status = "approved", usable = true, reason = (string?)null,
                    arrangement = new { chainStepId = TypeEvidence, branchCode = "BR1", position = 1 }
                }
            },
            chainConformance = new object[]
            {
                new { branchCode = "BR2", chainStepId = TypeFatigue, count = 0, min = 0, max = (int?)null, status = "ok" },
                new { branchCode = "BR1", chainStepId = TypeNeed, count = 0, min = 1, max = (int?)2, status = "under" },
                new { branchCode = "BR1", chainStepId = TypeEvidence, count = 1, min = 1, max = (int?)1, status = "ok" }
            }
        };

        private static object LegacyPath() => new
        {
            pathId = LegacyPathId, pathCode = "KP-114", pathName = "Eski yol", pathVersion = "1.0", pathStatus = "draft",
            subjectId = SubjectId, languageCode = "tr", isLegacyUnapproved = true, isStepSetFrozen = false, isArchived = false,
            steps = new object[]
            {
                new { stepId = Guid.NewGuid(), stepOrder = 10, stepCode = "S10", stepTitle = "Giriş", contentId = ContentTr, isArchived = false }
            },
            claims = Array.Empty<object>(),
            chainConformance = Array.Empty<object>()
        };

        private static object[] Contents() =>
        [
            new { contentId = ContentTr, contentCode = "KC-1", contentTitle = "Almiba sunumu", contentType = "presentation", contentStatus = "published", languageCode = "tr", productId = (Guid?)ProductId, subjectId = SubjectId, isArchived = false },
            new { contentId = ContentEn, contentCode = "KC-2", contentTitle = "Almiba deck (EN)", contentType = "presentation", contentStatus = "published", languageCode = "en", productId = (Guid?)ProductId, subjectId = SubjectId, isArchived = false },
            new { contentId = ContentDraft, contentCode = "KC-3", contentTitle = "Klinik özet taslağı", contentType = "clinical-summary", contentStatus = "draft", languageCode = "tr", productId = (Guid?)ProductId, subjectId = SubjectId, isArchived = false },
            new { contentId = ContentOther, contentCode = "KC-4", contentTitle = "Başka ürün", contentType = "brochure", contentStatus = "published", languageCode = "tr", productId = (Guid?)OtherProduct, subjectId = SubjectId, isArchived = false }
        ];

        private static object[] CoverageRows() =>
        [
            new { claimId = ClaimOk, claimCode = "CLM-OK", claimName = "Metabolizma", kind = "core", coreVersion = "2.0", coreStatus = "approved",
                  cells = new object[] { new { countryCode = "TR", state = "approved", versionId = (Guid?)VersionOk, version = "1.0" } } },
            new { claimId = ClaimDraft, claimCode = "CLM-DRAFT", claimName = "Taslak", kind = "core", coreVersion = "1.0", coreStatus = "approved",
                  cells = new object[] { new { countryCode = "TR", state = "draft", versionId = (Guid?)VersionDraft, version = "0.1" } } },
            new { claimId = ClaimNoTr, claimCode = "CLM-NOTR", claimName = "Özbek", kind = "core", coreVersion = "1.0", coreStatus = "approved",
                  cells = new object[] { new { countryCode = "UZ", state = "approved", versionId = (Guid?)Guid.NewGuid(), version = "1.0" } } },
            new { claimId = ClaimNoText, claimCode = "CLM-NOTEXT", claimName = "Metinsiz", kind = "core", coreVersion = "1.0", coreStatus = "approved",
                  cells = new object[] { new { countryCode = "TR", state = "approved", versionId = (Guid?)VersionNoText, version = "1.0" } } }
        ];
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
