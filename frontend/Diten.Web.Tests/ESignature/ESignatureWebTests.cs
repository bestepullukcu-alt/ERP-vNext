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
/// MOD-0022 E5W1 — the Web side of the e-signature service (pack §11, §16 E5 + the E4 designer item), measured on the files the
/// browser gets and on the real Razor render:
/// <list type="bullet">
/// <item>vocabulary: the 7 ESignature resx carry exactly <c>ESignature.Keys</c> with real text, every key a view or script asks for is
/// delivered; the nav keys and the designer keys are in all 7 languages;</item>
/// <item>surface: İmzam is a tenant-shell page under /ESignature (never /Platform, KS-0), UAS-001 draws only the explanation, the user
/// menu link is gated by the page key, the manifest route equals the Web route;</item>
/// <item>secrets: the password inputs are never pre-filled and use autocomplete current-password, the password is emptied in a
/// <c>finally</c> after every attempt (K3 sabotage (c)), nothing is written to browser storage, the proxy never logs a body;</item>
/// <item>WorkCenter: a <c>signatureRequired</c> approve opens the dialog BEFORE dispatching and a cancelled dialog dispatches nothing
/// (K3 sabotage (b)); the designers write AND read back <c>requirements.signatureRequired / signatureMeaning</c>.</item>
/// </list>
/// </summary>
public sealed class ESignatureWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["ar", "en", "es", "fr", "ru", "tr", "zh"];
    private static readonly string Web = TraceViewRenderer.WebRoot;
    private static readonly string Js = Path.Combine(Web, "wwwroot", "assets", "js");

    /// <summary>Values that are the same word in a language and in English on purpose (standard reference, file format, "OK").</summary>
    private static readonly HashSet<string> SameAsEnglishAllowed = new(StringComparer.Ordinal)
    {
        "ESign.Dialog.StandardRef", "ESign.Mine.Format.png", "ESign.Dialog.Success.Ok", "ESign.Module.workflow", "ESign.Mine.Id",
        "ESign.Mine.Format.pngAndVector", "ESign.Mine.Format.vector", "ESign.Mine.Col.Actions", "ESign.Mine.UsedCount", "ESign.Mine.Color"
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ESignatureWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    /// <summary>Code without // and /* */ comments — the guards read what the code does, not what its comments name.</summary>
    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    // ── vocabulary ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Seven_languages_carry_exactly_the_ESignature_keys_with_real_text()
    {
        var folder = Path.Combine(Web, "Resources", "Views", "ESignature");
        Assert.Equal(Languages.Select(l => $"ESignature.{l}.resx"), Directory.EnumerateFiles(folder, "ESignature.*.resx").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal));
        var keys = Diten.Web.Views.ESignature.ESignature.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(keys.Count > 150, $"too few keys: {keys.Count}");
        var en = Resx(Path.Combine(folder, "ESignature.en.resx"));
        foreach (var lang in Languages)
        {
            var values = Resx(Path.Combine(folder, $"ESignature.{lang}.resx"));
            Assert.Equal(keys, values.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList());
            Assert.All(values, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"{lang}: {kv.Key} is empty"));
            if (lang != "en")
            {
                var copies = values.Where(kv => kv.Value == en[kv.Key] && !SameAsEnglishAllowed.Contains(kv.Key)).Select(kv => kv.Key).ToList();
                Assert.True(copies.Count == 0, $"{lang} copies English: {string.Join(", ", copies)}");
            }
        }

        // every ESIGN_* code of the frozen contract has its sentence (pack §11.6 ESign.Errors.{ESIGN_*})
        // located, not hard-pathed (Mod0174TraceRouteTests pattern) — the docs tree moves, the contract file name does not
        var contract = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(TraceViewRenderer.RepoRoot(), "docs"), "esign.openapi.yaml", SearchOption.AllDirectories).Single());
        var codes = Regex.Matches(contract, @"ESIGN_[A-Z_]*[A-Z]").Select(m => m.Value).Distinct().ToList();
        Assert.True(codes.Count >= 30);
        Assert.All(codes, code => Assert.Contains($"ESign.Errors.{code}", keys));
    }

    [Fact]
    public void Every_key_a_view_or_script_asks_for_is_delivered()
    {
        var keys = Diten.Web.Views.ESignature.ESignature.Keys.ToHashSet(StringComparer.Ordinal);
        var views = Directory.EnumerateFiles(Path.Combine(Web, "Views", "ESignature"), "*.cshtml")
            .Concat(Directory.EnumerateFiles(Path.Combine(Web, "Views", "Shared", "ESignature"), "*.cshtml"));
        var askedByViews = views.SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<![A-Za-z])Localizer\[""(?<k>ESign\.[^""]+)""\]").Select(m => m.Groups["k"].Value)).Distinct().ToList();
        var askedByScripts = Directory.EnumerateFiles(Path.Combine(Js, "ESignature"), "*.js")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bt\('(?<k>ESign\.[A-Za-z.]*[A-Za-z])'\s*[,)]").Select(m => m.Groups["k"].Value)).Distinct().ToList();
        Assert.True(askedByViews.Count > 60 && askedByScripts.Count > 40);
        Assert.All(askedByViews.Concat(askedByScripts), k => Assert.True(keys.Contains(k), $"{k} is asked for but not delivered"));

        // the dynamic families the scripts build (VisualMode / AppType / Kind / Format / Status) exist for every wire value
        foreach (var family in new[]
                 {
                     ("ESign.Dialog.VisualMode.", new[] { "none", "optional", "savedAccepted", "drawRequired" }),
                     ("ESign.Dialog.AppType.", new[] { "none", "drawn", "sample" }),
                     ("ESign.Mine.Kind.", new[] { "drawn", "uploaded" }),
                     ("ESign.Mine.Format.", new[] { "png", "pngAndVector", "vector" }),
                     ("ESign.Mine.Status.", new[] { "pending", "active", "inactive", "rejected", "withdrawn" })
                 })
        {
            Assert.All(family.Item2, v => Assert.Contains(family.Item1 + v, keys));
        }
    }

    [Fact]
    public void Nav_and_designer_keys_are_in_all_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var shared = Resx(Path.Combine(Web, "Resources", $"SharedResource.{lang}.resx"));
            Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault("Nav.Module.ESIGN")), $"{lang}: Nav.Module.ESIGN");
            Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault("Nav.Page.ESIGNMYSIGNATURE")), $"{lang}: Nav.Page.ESIGNMYSIGNATURE");
            var workflow = Resx(Path.Combine(Web, "Resources", "Views", "Platform", "Workflow", $"WorkflowIndex.{lang}.resx"));
            foreach (var key in new[] { "Workflow.Designer.SignatureRequired", "Workflow.Designer.SignatureMeaning", "Workflow.Designer.SignatureMeaningSelect",
                         "Workflow.Designer.SignatureMeaningRequired", "Workflow.Designer.SignatureHint", "Workflow.Designer.SignatureMeaningsUnavailable" })
            {
                Assert.False(string.IsNullOrWhiteSpace(workflow.GetValueOrDefault(key)), $"{lang}: {key}");
            }
        }
    }

    // ── surface (KS-0, UAS-001) ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UAS_001_without_the_page_key_only_the_access_explanation_is_drawn()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/ESignature/MySignature.cshtml", null, "esign.signatures.sign", "esign.signatures.read");
        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        foreach (var skeleton in new[] { "esignMineCard", "dt-esign-samples", "esignSaveSample", "esignSamplePwd", "esignMinePad" })
        {
            Assert.DoesNotContain(skeleton, html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Forbidden", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_the_page_key_the_page_is_drawn_in_the_tenant_shell()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/ESignature/MySignature.cshtml", null, "esign.my-samples.manage");
        Assert.DoesNotContain("diten-access-denied", html, StringComparison.Ordinal);
        Assert.Contains("id=\"dt-esign-samples\" data-dt-standard=\"v2\" data-dt-data-mode=\"client\"", html, StringComparison.Ordinal);
        Assert.Contains("data-esign-pad", html, StringComparison.Ordinal);
        Assert.Contains("id=\"esign-l10n\"", html, StringComparison.Ordinal);
        Assert.Contains("Layout = \"_LayoutTenantShell\"", Read(Web, "Views", "ESignature", "MySignature.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Routes_are_tenant_routes_and_the_manifest_points_at_the_route_the_Web_serves()
    {
        var route = typeof(ESignatureController).GetCustomAttribute<RouteAttribute>()!.Template;
        Assert.Equal("ESignature", route);
        Assert.NotNull(typeof(ESignatureController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        var page = typeof(ESignatureController).GetMethod(nameof(ESignatureController.MySignature))!.GetCustomAttribute<HttpGetAttribute>()!.Template;
        Assert.Equal("/ESignature/MySignature", $"/{route}/{page}");

        var manifest = Read(TraceViewRenderer.RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "ESignature", "SelfRegistration", "ESignManifestProvider.cs");
        Assert.Matches(@"new ModuleManifestPage\(""ESIGN_MYSIGNATURE"", ""[^""]+"", ""/ESignature/MySignature"", ESignPermissions\.MySamplesManage, null, true,", manifest);
        Assert.DoesNotContain("\"/Platform", manifest, StringComparison.Ordinal);

        // the proxy forwards to the e-signature API and the two AuthService re-auth operations, nowhere else
        var controller = Code(Read(Web, "Controllers", "ESignatureController.cs"));
        var targets = Regex.Matches(controller, @"""(?<p>/api/[^""{]+)").Select(m => m.Groups["p"].Value).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "/api/tenant-auth/esign/reauth", "/api/tenant-auth/esign/ticket", "/api/v1/esign", "/api/v1/esign/" }, targets);
        Assert.DoesNotContain("/Platform", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void The_user_menu_link_is_the_only_layout_change_and_is_gated_by_the_page_key()
    {
        var layout = Read(Web, "Views", "Shared", "_LayoutTenantShell.cshtml");
        Assert.Matches(@"@if \(Perms\.Has\(""esign\.my-samples\.manage""\)\)\s*\{\s*<li>\s*<a class=""dropdown-item d-flex align-items-center"" href=""/ESignature/MySignature""", layout);
    }

    // ── secrets ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_password_is_never_kept_and_is_emptied_after_every_attempt()
    {
        var scripts = Directory.EnumerateFiles(Path.Combine(Js, "ESignature"), "*.js").ToDictionary(f => Path.GetFileName(f), f => Code(File.ReadAllText(f)));
        Assert.All(scripts, s =>
        {
            Assert.DoesNotContain("localStorage", s.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("sessionStorage", s.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("indexedDB", s.Value, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"console\.\w+\([^)]*(password|ticket)", s.Value);
            Assert.DoesNotMatch(@"(location\.(href|search)|URLSearchParams)[^;]*(password|ticket)", s.Value);
        });

        // the dialog: the re-auth call is wrapped so the input is emptied whatever the answer (K3 sabotage (c))
        Assert.Matches(@"auth = usePassword\s*\?\s*await call\('POST', `\$\{AUTH\}/reauth`[\s\S]*?\}\s*finally\s*\{\s*clearPassword\(\);\s*\}", scripts["esign-dialog.js"]);
        Assert.Matches(@"function clearPassword\(\)\s*\{\s*const input = \$\('esignPassword'\);\s*if \(input\) input\.value = '';", scripts["esign-dialog.js"]);
        Assert.Matches(@"function finish\(value\)[\s\S]*?clearPassword\(\);", scripts["esign-dialog.js"]);
        // İmzam: the sample password, the same way
        Assert.Matches(@"auth = await E\.call\('POST', '/ESignature/auth/reauth'[\s\S]*?\}\s*finally\s*\{\s*clearSamplePassword\(\);", scripts["my-signature.js"]);

        // the password inputs: never pre-filled, current-password, not in the URL
        foreach (var view in new[] { Path.Combine(Web, "Views", "Shared", "ESignature", "_ESignatureDialog.cshtml"), Path.Combine(Web, "Views", "ESignature", "MySignature.cshtml") })
        {
            var inputs = Regex.Matches(File.ReadAllText(view), @"<input type=""password""[^>]*>").Select(m => m.Value).ToList();
            Assert.Single(inputs);
            Assert.All(inputs, i =>
            {
                Assert.Contains("autocomplete=\"current-password\"", i, StringComparison.Ordinal);
                Assert.DoesNotContain(" value=", i, StringComparison.Ordinal);
            });
        }

        // the proxy logs method / path / status only — never the body that carries the password or the ticket
        var controller = Code(Read(Web, "Controllers", "ESignatureController.cs"));
        Assert.DoesNotMatch(@"_logger\.\w+\([^;]*\bbody\b", controller);
    }

    // ── WorkCenter + designers ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_signature_required_approve_signs_before_it_is_dispatched_and_a_cancel_dispatches_nothing()
    {
        var app = Code(Read(Js, "WorkCenterNext", "app.js"));
        Assert.Matches(@"if \(action\.code !== 'approve' \|\| action\.signatureRequired !== true\) return \{ needed: false, signatureId: null \};", app);
        Assert.Matches(@"global\.ESign\.open\(\{\s*module: 'workflow',\s*subjectType: 'workflow-task',\s*subjectId: item\.id,\s*meaningCode: action\.signatureMeaning", app);
        Assert.Matches(@"decision: 'approve'", app);

        var submit = Regex.Match(app, @"const submitRealTransition = async \([\s\S]*?\n    \};").Value;
        Assert.NotEmpty(submit);
        var signAt = submit.IndexOf("await signatureForApproval(item, action)", StringComparison.Ordinal);
        var cancelAt = submit.IndexOf("if (signature.needed && !signature.signatureId)", StringComparison.Ordinal);
        var dispatchAt = submit.IndexOf("WorkCenterNextApi.dispatchAction", StringComparison.Ordinal);
        Assert.True(signAt > 0 && cancelAt > signAt && dispatchAt > cancelAt, "sign → cancel guard → dispatch must be in this order");
        Assert.Matches(@"if \(signature\.signatureId\) body\.signatureId = signature\.signatureId;", submit);

        // the projection's flags reach the action model; the host pages carry the dialog
        Assert.Contains("signatureRequired: action.signatureRequired === true", Code(Read(Js, "WorkCenterNext", "mock-data.js")), StringComparison.Ordinal);
        foreach (var view in new[] { "Index.cshtml", "Details.cshtml" })
        {
            var host = Read(Web, "Views", "WorkCenterNext", view);
            Assert.True(host.IndexOf("_ESignatureDialog.cshtml", StringComparison.Ordinal) is > 0 and var at && at < host.IndexOf("WorkCenterNext/app.js", StringComparison.Ordinal), view);
        }
    }

    [Fact]
    public void Both_designers_write_and_read_back_the_signature_step_flag()
    {
        var form = Code(Read(Js, "Platform", "Workflow", "designer-page.js"));
        Assert.Matches(@"step\.requirements\.signatureRequired = true;\s*step\.requirements\.signatureMeaning = qv\(card, '\.wf-dz-signaturemeaning'\);", form);
        var visual = Code(Read(Js, "Platform", "Workflow", "workflow.bpmn-designer.js"));
        Assert.Matches(@"step\.requirements\.signatureRequired = true;\s*step\.requirements\.signatureMeaning = p\.signatureMeaning", visual);
        Assert.Matches(@"signatureRequired: source\.requirements\?\.signatureRequired === true,\s*signatureMeaning: source\.requirements\?\.signatureMeaning \|\| ''", visual);
        Assert.Contains("/ESignature/api/meanings?activeOnly=true", visual, StringComparison.Ordinal);
        Assert.Contains("wf-dz-signaturerequired", Read(Web, "Views", "Platform", "Workflow", "Designer.cshtml"), StringComparison.Ordinal);
        Assert.Contains("wf-vd-signaturerequired", Read(Web, "Views", "Platform", "Workflow", "VisualDesigner.cshtml"), StringComparison.Ordinal);
    }
}
