using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.ESignature;

/// <summary>
/// WP-MVP1-ESIGN-HELP (SEC-ESIGN1 item 6, CT decision B) — every e-signature page explains itself with the module's EXISTING inline
/// info note (<c>alert alert-info … role="note"</c> + <c>bx-info-circle</c>, the pattern of MySignature.cshtml's statement), no new
/// component:
/// <list type="bullet">
/// <item>the seven page-help keys exist in en and tr (the Platform languages) with real text — not empty, not a placeholder, en and tr
/// not the same sentence, at least two sentences;</item>
/// <item>the rendered page draws the note with that text (only with the page key — without it UAS-001 draws the explanation alone).</item>
/// </list>
/// </summary>
public sealed class ESignaturePageHelpWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string Web = TraceViewRenderer.WebRoot;

    /// <summary>view, page key (UAS-001), page-help resx key.</summary>
    public static readonly TheoryData<string, string, string> Pages = new()
    {
        { "/Views/ESignature/MySignature.cshtml", "esign.my-samples.manage", "ESign.Help.Mine" },
        { "/Views/ESignature/Requests.cshtml", "esign.requests.read", "ESign.Help.Requests" },
        { "/Views/ESignature/Verification.cshtml", "esign.signatures.read", "ESign.Help.Verify" },
        { "/Views/ESignature/AuditExport.cshtml", "esign.signatures.export", "ESign.Help.Export" },
        { "/Views/ESignature/Settings/Policy.cshtml", "esign.policy.read", "ESign.Help.Policy" },
        { "/Views/ESignature/Settings/Meanings.cshtml", "esign.meanings.read", "ESign.Help.Meanings" },
        { "/Views/ESignature/Settings/SampleApprovals.cshtml", "esign.samples.approve", "ESign.Help.Approvals" }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ESignaturePageHelpWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static string? Value(string lang, string key)
        => XDocument.Load(Path.Combine(Web, "Resources", "Views", "ESignature", $"ESignature.{lang}.resx")).Root!
            .Elements("data").SingleOrDefault(d => (string)d.Attribute("name")! == key)?.Element("value")?.Value;

    [Theory]
    [MemberData(nameof(Pages))]
    public void The_page_help_text_is_real_in_en_and_tr(string view, string pageKey, string key)
    {
        Assert.Contains(key, Diten.Web.Views.ESignature.ESignature.Keys);
        var en = Value("en", key);
        var tr = Value("tr", key);
        foreach (var (lang, text) in new[] { ("en", en), ("tr", tr) })
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{lang}: {key} is missing or empty ({view})");
            Assert.True(text!.Length >= 120, $"{lang}: {key} is too short to explain the page ({text.Length} chars)");
            Assert.True(Regex.Matches(text, @"[.!?](\s|$)").Count >= 2, $"{lang}: {key} must be at least two sentences");
            Assert.DoesNotMatch(@"(?i)\b(todo|tbd|lorem|placeholder|xxx)\b", text);
        }

        Assert.NotEqual(en, tr);
        Assert.StartsWith("esign.", pageKey, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task The_rendered_page_draws_the_help_note_with_its_text(string view, string pageKey, string key)
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, view, null, pageKey); // the renderer's culture is en
        var note = Regex.Match(html, @"<div class=""alert alert-info d-flex gap-2 align-items-start"" role=""note"" id=""esignPageHelp"">\s*<i class=""bx bx-info-circle fs-5""></i><span>(?<t>[^<]*)</span>");
        Assert.True(note.Success, $"{view}: the page-help note (#esignPageHelp, the inline info-note pattern) is not drawn");
        Assert.Equal(HtmlEncoder.Default.Encode(Value("en", key)!), note.Groups["t"].Value);

        var source = File.ReadAllText(Path.Combine([Web, .. view.TrimStart('/').Split('/')]));
        Assert.Contains($"@Localizer[\"{key}\"]", source, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Without_the_page_key_the_help_note_is_not_drawn(string view, string pageKey, string key)
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, view, null, "esign.unrelated.read");
        Assert.Contains("diten-access-denied", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"esignPageHelp\"", html, StringComparison.Ordinal);
        Assert.True(pageKey.Length > 0 && key.Length > 0);
    }
}
