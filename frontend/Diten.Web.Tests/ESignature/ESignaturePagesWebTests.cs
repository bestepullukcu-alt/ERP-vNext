using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.ESignature;

/// <summary>
/// MOD-0022 E5W2 — the six e-signature pages of pack §11.1 (Requests, Verification, AuditExport, Settings/Policy, Settings/Meanings,
/// Settings/SampleApprovals) and the E5W1 follow-ups, measured on the real Razor render and on the files the browser gets:
/// <list type="bullet">
/// <item>surface: every page is a tenant-shell route under /ESignature served by the controller and nav-visible in the manifest at
/// the same route; UAS-001 draws only the access explanation without the page key, the page with it;</item>
/// <item>lists: golden slim (createList, the table's data mode = the script's, filters + Save View);</item>
/// <item>decisions: reject / invalidate / revert are the red reasoned confirm (≥ 10); an invalidation also asks the password
/// (ticket intent invalidate) and empties it; a sample owner gets no decision button on their own sample (K3 sabotage (b));</item>
/// <item>export: the proxy returns the file as bytes with its name and export number, never cached (CT-D20);</item>
/// <item>O-E5W1-3: WorkCenter hands the step's signable action code to the dialog; O-E5W1-2: the İmzam history is a slim list.</item>
/// </list>
/// </summary>
public sealed class ESignaturePagesWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string Js = Path.Combine(Web, "wwwroot", "assets", "js");

    /// <summary>page code, Web route, view, page key, a skeleton id only the drawn page has, the list script (null = no list).</summary>
    public static readonly TheoryData<string, string, string, string, string, string?> Pages = new()
    {
        { "ESIGN_REQUESTS", "/ESignature/Requests", "/Views/ESignature/Requests.cshtml", "esign.requests.read", "dt-esign-requests", "requests.js" },
        { "ESIGN_VERIFICATION", "/ESignature/Verification", "/Views/ESignature/Verification.cshtml", "esign.signatures.read", "esignVerifyForm", null },
        { "ESIGN_AUDITEXPORT", "/ESignature/AuditExport", "/Views/ESignature/AuditExport.cshtml", "esign.signatures.export", "dt-esign-export", "audit-export.js" },
        { "ESIGN_POLICY", "/ESignature/Settings/Policy", "/Views/ESignature/Settings/Policy.cshtml", "esign.policy.read", "esignPolicyRules", null },
        { "ESIGN_MEANINGS", "/ESignature/Settings/Meanings", "/Views/ESignature/Settings/Meanings.cshtml", "esign.meanings.read", "dt-esign-meanings", "meanings.js" },
        { "ESIGN_SAMPLEAPPROVALS", "/ESignature/Settings/SampleApprovals", "/Views/ESignature/Settings/SampleApprovals.cshtml", "esign.samples.approve", "dt-esign-approvals", "sample-approvals.js" }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ESignaturePagesWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    /// <summary>Code without // and /* */ comments.</summary>
    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    private static string Manifest() => Read(TraceViewRenderer.RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "ESignature",
        "SelfRegistration", "ESignManifestProvider.cs");

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    // ── surface ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task UAS_001_without_the_page_key_only_the_access_explanation_is_drawn(string code, string route, string view, string key, string skeleton, string? script)
    {
        // every OTHER e-signature key, but not this page's
        var others = new[] { "esign.requests.read", "esign.signatures.read", "esign.signatures.export", "esign.policy.read", "esign.meanings.read", "esign.samples.approve",
            "esign.signatures.sign", "esign.my-samples.manage" }.Where(k => k != key).ToArray();
        var html = await TraceViewRenderer.RenderAsync(_factory, view, null, others);
        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain(skeleton, html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dt-standard", html, StringComparison.Ordinal);
        Assert.DoesNotContain("inlineFilterHost", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
        Assert.True(code.Length > 0 && route.Length > 0 && (script is null || script.Length > 0));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task With_the_page_key_the_page_is_drawn_in_the_tenant_shell(string code, string route, string view, string key, string skeleton, string? script)
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, view, null, key);
        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains($"id=\"{skeleton}\"", html, StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine([Web, .. view.TrimStart('/').Split('/')]));
        Assert.Contains("Layout = \"_LayoutTenantShell\"", source, StringComparison.Ordinal);
        Assert.Contains($"@if (!Perms.Has(\"{key}\"))", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/Platform", source, StringComparison.Ordinal);
        if (script is not null)
        {
            // golden slim list: the table's data mode is the script's, filters and the Save View are there
            var mode = Regex.Match(html, $@"id=""{skeleton}"" data-dt-standard=""v2"" data-dt-data-mode=""(?<m>server|client)""").Groups["m"].Value;
            Assert.NotEmpty(mode);
            var js = Code(Read(Js, "ESignature", script));
            Assert.Contains("DitenDataTable.createList(", js, StringComparison.Ordinal);
            Assert.Contains($"dataMode: '{mode}'", js, StringComparison.Ordinal);
            Assert.Matches(@"savedView: \{\s*moduleKey: 'ESignature',\s*pageKey: '\w+'", js);
            Assert.Matches(@"filters: \{\s*hostId: 'inlineFilterHost',\s*collapseId: 'inlineFilterCollapse'", js);
            Assert.Contains("id=\"inlineFilterHost\"", html, StringComparison.Ordinal);
            Assert.Contains("id=\"btnFilterApply\"", html, StringComparison.Ordinal);
        }

        Assert.True(code.Length > 0 && route.Length > 0);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public void The_controller_serves_the_route_the_manifest_declares_nav_visible(string code, string route, string view, string key, string skeleton, string? script)
    {
        var routes = typeof(ESignatureController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>().Select(a => (Method: m, a.Template)))
            .Where(x => x.Template is not null && !x.Template.Contains('{'))
            .ToDictionary(x => "/ESignature/" + x.Template, x => x.Method, StringComparer.Ordinal);
        Assert.True(routes.ContainsKey(route), $"{route} is not served");
        Assert.Matches($@"new ModuleManifestPage\(""{code}"", ""[^""]+"", ""{Regex.Escape(route)}"", ESignPermissions\.\w+, null, true,", Manifest());
        Assert.Contains(view.Replace("/Views", "~/Views"), Read(Web, "Controllers", "ESignatureController.cs"), StringComparison.Ordinal);
        Assert.True(key.Length > 0 && skeleton.Length > 0 && (script is null || script.Length > 0));
    }

    [Fact]
    public void Every_e_signature_page_has_its_nav_label_in_all_seven_languages_next_to_the_ESIGN_group()
    {
        var pages = new[] { "ESIGNREQUESTS", "ESIGNVERIFICATION", "ESIGNAUDITEXPORT", "ESIGNMYSIGNATURE", "ESIGNPOLICY", "ESIGNMEANINGS", "ESIGNSAMPLEAPPROVALS" };
        foreach (var lang in Languages)
        {
            var file = Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx");
            var shared = Resx(file);
            Assert.All(pages, p => Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault("Nav.Page." + p)), $"{lang}: Nav.Page.{p}"));
            if (lang != "en")
            {
                var en = Resx(Path.Combine(Web, "Resources", "SharedResource.en.resx"));
                Assert.All(pages, p => Assert.NotEqual(en["Nav.Page." + p], shared["Nav.Page." + p]));
            }

            // the new keys sit together with the ESIGN group (pack §11.6), not at the end of the file
            var names = XDocument.Load(file).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToList();
            var at = names.IndexOf("Nav.Page.ESIGNMYSIGNATURE");
            Assert.Equal(pages.Where(p => p != "ESIGNMYSIGNATURE").Select(p => "Nav.Page." + p), names.Skip(at + 1).Take(6));
            Assert.Equal("Nav.Module.ESIGN", names[at - 1]);
        }
    }

    [Fact]
    public void The_dynamic_vocabulary_families_exist_for_every_wire_value()
    {
        var keys = Diten.Web.Views.ESignature.ESignature.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var family in new[]
                 {
                     ("ESign.Verify.Status.", new[] { "valid", "changed", "invalidated", "tampered", "subjectUnresolvable" }),
                     ("ESign.Requests.Status.", new[] { "waiting", "signed", "rejected", "expired", "cancelled" }),
                     ("ESign.Approvals.Decision.", new[] { "approved", "rejected" }),
                     ("ESign.Meanings.Lang.", new[] { "tr", "en", "fr", "es", "zh", "ar", "ru" }),
                     ("ESign.Module.", new[] { "workflow", "inventory", "documentmanagement" })
                 })
        {
            Assert.All(family.Item2, v => Assert.Contains(family.Item1 + v, keys));
        }

        // the Settings views (a sub-folder E5W1's scan does not enter) ask only for delivered keys
        var asked = Directory.EnumerateFiles(Path.Combine(Web, "Views", "ESignature", "Settings"), "*.cshtml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<![A-Za-z])Localizer\[""(?<k>ESign\.[^""]+)""\]").Select(m => m.Groups["k"].Value)).Distinct().ToList();
        Assert.True(asked.Count > 40);
        Assert.All(asked, k => Assert.True(keys.Contains(k), $"{k} is asked for but not delivered"));
    }

    // ── decisions ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_invalidate_and_revert_are_the_red_reasoned_confirm_of_at_least_ten_characters()
    {
        var pages = Code(Read(Js, "ESignature", "esign-pages.js"));
        Assert.Contains("const REASON_MIN = 10;", pages, StringComparison.Ordinal);
        Assert.Matches(@"global\.showConfirm\(title,[\s\S]*?type: type \|\| 'danger',[\s\S]*?showInput: true,\s*inputType: 'textarea',[\s\S]*?inputValidator: \(value\) => \(String\(value \|\| ''\)\.trim\(\)\.length < REASON_MIN", pages);

        foreach (var (script, call) in new[]
                 {
                     ("requests.js", "/reject`"),
                     ("verification.js", "/invalidate`"),
                     ("policy.js", "/revert`"),
                     ("sample-approvals.js", "/reject`")
                 })
        {
            var js = Code(Read(Js, "ESignature", script));
            var reasonAt = js.IndexOf("await P.reason({", StringComparison.Ordinal);
            var callAt = js.IndexOf(call, StringComparison.Ordinal);
            Assert.True(reasonAt > 0 && callAt > reasonAt, $"{script}: the reason is asked before {call}");
            Assert.Matches(@"await P\.reason\(\{[\s\S]*?type: 'danger'", js);
            Assert.Contains("if (!reason) return;", js, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_invalidation_asks_the_password_with_intent_invalidate_and_empties_it_after_every_attempt()
    {
        var js = Code(Read(Js, "ESignature", "verification.js"));
        Assert.Contains("E.call('POST', '/ESignature/auth/reauth', { password, intent: 'invalidate' })", js, StringComparison.Ordinal);
        Assert.Matches(@"\} finally \{\s*input\.value = '';\s*password = null;", js);
        Assert.DoesNotMatch(@"localStorage|sessionStorage", js);
        var view = Read(Web, "Views", "ESignature", "Verification.cshtml");
        Assert.Contains("id=\"esignInvalidatePassword\" name=\"esign-invalidate-password\" autocomplete=\"current-password\"", view, StringComparison.Ordinal);
        Assert.Matches(@"@if \(Perms\.Has\(""esign\.signatures\.invalidate""\)\)\s*\{[\s\S]*?id=""esignInvalidatePwd""", view);
    }

    /// <summary>K3 sabotage (b): drop the own-sample check → red. The server refuses a self-decision anyway (E3, 403).</summary>
    [Fact]
    public void A_sample_owner_gets_no_decision_button_on_their_own_sample()
    {
        var js = Code(Read(Js, "ESignature", "sample-approvals.js"));
        Assert.Matches(@"const ownSample = \(row\) => !!me && String\(\(row\.owner && row\.owner\.userId\) \|\| ''\)\.toLowerCase\(\) === me;", js);
        Assert.Matches(@"function renderDecision\(row\) \{\s*if \(ownSample\(row\)\) \{\s*return `<span[^`]*data-esign-own-sample[^`]*`;\s*\}", js);
        Assert.Matches(@"async function approve\(\{ row \}\) \{\s*if \(!row\?\.id \|\| ownSample\(row\)\) return;", js);
        Assert.Matches(@"async function reject\(\{ row \}\) \{\s*if \(!row\?\.id \|\| ownSample\(row\)\) return;", js);
    }

    // ── export ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_export_file_comes_back_as_bytes_with_its_name_and_number_and_is_never_cached()
    {
        var controller = Code(Read(Web, "Controllers", "ESignatureController.cs"));
        Assert.Contains("private static readonly string[] FileTypes = [\"application/pdf\", \"text/csv\", \"application/octet-stream\"];", controller, StringComparison.Ordinal);
        Assert.Matches(@"if \(reply\.Ok && FileTypes\.Any\([\s\S]*?Response\.Headers\.CacheControl = ""no-store"";[\s\S]*?Response\.Headers\[ExportNumberHeader\] = reply\.ExportNumber;[\s\S]*?FileDownloadName = reply\.FileName", controller);
        Assert.Contains("var bytes = await response.Content.ReadAsByteArrayAsync(", controller, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"_logger\.\w+\([^;]*\bbody\b", controller);

        var js = Code(Read(Js, "ESignature", "audit-export.js"));
        Assert.Contains("const blob = await res.blob();", js, StringComparison.Ordinal);
        Assert.Contains("res.headers.get('X-Export-Number')", js, StringComparison.Ordinal);
        Assert.Contains("res.headers.get('content-disposition')", js, StringComparison.Ordinal);
        Assert.Matches(@"body: JSON\.stringify\(\{ format, filter: exportFilter\(\), idempotencyKey: E\.newKey\(\) \}\)", js);
    }

    // ── follow-ups ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WorkCenter_hands_the_steps_signable_action_code_to_the_dialog()
    {
        var app = Code(Read(Js, "WorkCenterNext", "app.js"));
        Assert.Matches(@"global\.ESign\.open\(\{\s*module: 'workflow',\s*subjectType: 'workflow-task',\s*subjectId: item\.id,\s*meaningCode: action\.signatureMeaning \|\| undefined,\s*actionCode: action\.signatureActionCode \|\| undefined,", app);
        Assert.Contains("signatureActionCode: action.signatureActionCode || null,", Code(Read(Js, "WorkCenterNext", "mock-data.js")), StringComparison.Ordinal);
        // the dialog asks the effective policy for the action (E5W1 — unchanged)
        Assert.Contains("if (o.actionCode) q.set('actionCode', o.actionCode);", Code(Read(Js, "ESignature", "esign-dialog.js")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_MySignature_history_is_a_golden_slim_client_list_with_filters_and_a_saved_view()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/ESignature/MySignature.cshtml", null, "esign.my-samples.manage");
        Assert.Contains("id=\"dt-esign-samples\" data-dt-standard=\"v2\" data-dt-data-mode=\"client\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"filterSampleStatus\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"filterSampleKind\"", html, StringComparison.Ordinal);
        var js = Code(Read(Js, "ESignature", "my-signature.js"));
        Assert.Matches(@"global\.DitenDataTable\.createList\(\{\s*tableEl: el,\s*dataMode: 'client',", js);
        Assert.Matches(@"savedView: \{\s*moduleKey: 'ESignature',\s*pageKey: 'MySignature',", js);
        Assert.DoesNotContain("new global.DataTable(", js, StringComparison.Ordinal);
    }
}
