using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.ESignature;

/// <summary>
/// WP-MVP1-ESIGN-HELP (SEC-ESIGN1 item 6, CT decision B) explained every e-signature page with an inline info note (<c>#esignPageHelp</c>,
/// <c>ESign.Help.*</c>). WP-MVP1-FU-CLEAN2 (UXH-001) MOVED that explanation into the shell's "?" panel (<c>Help.{CODE}.*</c> in
/// <c>PageHelpResource</c>, 7 languages — the pages are on the tenant shell): the note is no longer drawn and its keys are gone, so the page
/// never says the same thing twice. The panel's text itself is measured by <see cref="Help.MvpOnePageHelpTests"/>; this class keeps the
/// e-signature side of the move: the real page renders no note, with or without its key (UAS-001), and the panel text is real in en and tr.
/// </summary>
public sealed class ESignaturePageHelpWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;

    /// <summary>view, page key (UAS-001), page code of the "?" panel.</summary>
    public static readonly TheoryData<string, string, string> Pages = new()
    {
        { "/Views/ESignature/MySignature.cshtml", "esign.my-samples.manage", "ESIGNMYSIGNATURE" },
        { "/Views/ESignature/Requests.cshtml", "esign.requests.read", "ESIGNREQUESTS" },
        { "/Views/ESignature/Verification.cshtml", "esign.signatures.read", "ESIGNVERIFICATION" },
        { "/Views/ESignature/AuditExport.cshtml", "esign.signatures.export", "ESIGNAUDITEXPORT" },
        { "/Views/ESignature/Settings/Policy.cshtml", "esign.policy.read", "ESIGNPOLICY" },
        { "/Views/ESignature/Settings/Meanings.cshtml", "esign.meanings.read", "ESIGNMEANINGS" },
        { "/Views/ESignature/Settings/SampleApprovals.cshtml", "esign.samples.approve", "ESIGNSAMPLEAPPROVALS" }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ESignaturePageHelpWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string? Value(string lang, string key)
        => XDocument.Load(Path.Combine(Web, "Resources", $"PageHelpResource.{lang}.resx")).Root!
            .Elements("data").SingleOrDefault(d => (string)d.Attribute("name")! == key)?.Element("value")?.Value;

    [Theory]
    [MemberData(nameof(Pages))]
    public void The_panel_text_is_real_in_en_and_tr(string view, string pageKey, string code)
    {
        var en = Value("en", $"Help.{code}.Purpose");
        var tr = Value("tr", $"Help.{code}.Purpose");
        foreach (var (lang, text) in new[] { ("en", en), ("tr", tr) })
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{lang}: Help.{code}.Purpose is missing or empty ({view})");
            Assert.True(text!.Length >= 40, $"{lang}: Help.{code}.Purpose is too short to explain the page ({text.Length} chars)");
            Assert.DoesNotMatch(@"(?i)\b(todo|tbd|lorem|placeholder|xxx)\b", text);
            Assert.False(string.IsNullOrWhiteSpace(Value(lang, $"Help.{code}.Step1")), $"{lang}: Help.{code}.Step1 is missing");
        }

        Assert.NotEqual(en, tr);
        Assert.StartsWith("esign.", pageKey, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task The_rendered_page_no_longer_draws_the_in_page_note(string view, string pageKey, string code)
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, view, null, pageKey);
        Assert.DoesNotContain("id=\"esignPageHelp\"", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"ESign\.Help\."), html);

        var source = File.ReadAllText(Path.Combine([Web, .. view.TrimStart('/').Split('/')]));
        Assert.DoesNotContain("ESign.Help.", source, StringComparison.Ordinal);
        Assert.Contains($"ViewData[\"PageCode\"] = \"{code}\";", source, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Without_the_page_key_only_the_access_explanation_is_drawn(string view, string pageKey, string code)
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, view, null, "esign.unrelated.read");
        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"esignPageHelp\"", html, StringComparison.Ordinal);
        Assert.True(pageKey.Length > 0 && code.Length > 0);
    }
}
