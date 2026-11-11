using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-2 — the source-reversal states on the Quarantine pages (pack §9, §11, §16 AC-25 … AC-30, §23), measured on the SHIPPED files:
/// <list type="bullet">
/// <item>page behaviour (AC-25 queue, AC-26 detail, AC-27 Bloke Stok rows, AC-28 conflict badge, §11 Karar Ver): Node runs the shipped scripts
/// (<c>Quarantine/quarantine-source-reversal.test.mjs</c>, below) with the words of the shipped resx (tr, en, ar);</item>
/// <item>the views (AC-26 banners, AC-27 the <c>Voided</c> choice of the status filter, AC-30 shell + UAS-001) on the real Razor render;</item>
/// <item>the vocabulary (AC-29): the 13 keys of §11 in <c>Quarantine.Keys</c> and in the 7 resx (equal counts), Arabic text in <c>ar</c>, and the
/// three help steps of §23 in 7 languages — QUARANTINEDETAILS / BLOCKEDSTOCK name the accepted limit of an open flagged record (no close action).</item>
/// </list>
/// </summary>
public sealed class QuarantineSourceReversalWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
    private static readonly string Web = TraceViewRenderer.WebRoot;

    /// <summary>The 13 keys of pack §11 (r5: <c>Quarantine.Conflict.Cause.LotChanged</c>).</summary>
    public static readonly string[] NewKeys =
    [
        "Quarantine.HoldState.Voided", "Quarantine.Blocked.Status.Voided", "Quarantine.Workflow.Voided", "Quarantine.History.SourceReversed",
        "Quarantine.History.SourceReversalConflict", "Quarantine.Details.VoidedBanner", "Quarantine.Details.ConflictBanner", "Quarantine.Common.ConflictBadge",
        "Quarantine.Conflict.Cause.Decided", "Quarantine.Conflict.Cause.LinesPosted", "Quarantine.Conflict.Cause.QuantityChanged",
        "Quarantine.Conflict.Cause.OwnMovementReversed", "Quarantine.Conflict.Cause.LotChanged"
    ];

    private readonly WebApplicationFactory<Program> _factory;

    public QuarantineSourceReversalWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static Dictionary<string, string> Words(string lang) => Resx(Path.Combine(Web, "Resources", "Views", "Quarantine", $"Quarantine.{lang}.resx"));

    /// <summary>The script without its comments (the same reading as QuarantineWebTests).</summary>
    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)//.*$", string.Empty);

    private static Dictionary<string, string> Help(string lang) => Resx(Path.Combine(Web, "Resources", $"PageHelpResource.{lang}.resx"));

    // ── AC-25 … AC-28: the page behaviour (Node, the shipped scripts) ───────────────────────────────────────────────

    [Fact]
    public async Task AC25_to_AC28_the_pages_hold_against_the_shipped_scripts()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "Quarantine", "quarantine-source-reversal.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 18$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }

    // ── AC-26 / AC-27: the views ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC27_the_Bloke_Stok_status_filter_offers_Voided_in_the_readers_language()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Blocked.cshtml", null, "quarantine.holds.read");
        var select = Regex.Match(html, @"<select id=""filterStatus""[\s\S]*?</select>").Value;
        Assert.NotEmpty(select);
        var options = Regex.Matches(select, @"<option value=""([^""]*)"">([^<]*)</option>").ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        Assert.Equal(new[] { "", "Closed", "SampleRetained", "Voided", "All" }, options.Keys.ToArray());
        Assert.Equal(Words("en")["Quarantine.Blocked.Status.Voided"], options["Voided"]);
    }

    [Fact]
    public async Task AC26_the_detail_draws_the_two_banner_frames_hidden_for_the_script()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Details.cshtml", Guid.NewGuid().ToString("D"), "quarantine.holds.read");
        Assert.Matches(new Regex(@"<div class=""alert alert-secondary d-none"" id=""qr-d-voided"" role=""status""><div class=""d-flex gap-2""><i class=""bx bx-block mt-1""></i><span></span></div></div>"), html);
        Assert.Matches(new Regex(@"<div class=""alert alert-danger d-none"" id=""qr-d-conflict"" role=""status""><div data-qr-conflict-badge></div><div class=""small mt-1"" data-qr-conflict-text></div></div>"), html);
    }

    // ── AC-29: the words ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AC29_the_13_keys_are_delivered_and_in_all_seven_languages_with_equal_counts()
    {
        var keys = Diten.Web.Views.Quarantine.Quarantine.Keys;
        Assert.All(NewKeys, k => Assert.Contains(k, keys));
        var counts = Languages.ToDictionary(l => l, l => Words(l).Count);
        Assert.Single(counts.Values.Distinct());
        Assert.Equal(keys.Count, counts["en"]);
        var en = Words("en");
        foreach (var lang in Languages)
        {
            var words = Words(lang);
            foreach (var key in NewKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(words.GetValueOrDefault(key)), $"{lang}: {key} is missing");
                if (lang != "en") Assert.NotEqual(en[key], words[key]);
            }

            Assert.All(new[] { "Quarantine.Details.VoidedBanner", "Quarantine.Details.ConflictBanner" }, k =>
                Assert.All(new[] { "{0}", "{1}", "{2}" }, p => Assert.Contains(p, words[k], StringComparison.Ordinal)));
            Assert.Equal(5, NewKeys.Where(k => k.StartsWith("Quarantine.Conflict.Cause.", StringComparison.Ordinal)).Select(k => words[k]).Distinct().Count());
        }

        // ar is written right to left: the new words are Arabic script (the shell sets dir="rtl" for ar)
        var ar = Words("ar");
        Assert.All(NewKeys, k => Assert.Matches(new Regex(@"\p{IsArabic}"), ar[k]));
    }

    /// <summary>Pack §23: one step per page, 7 languages, naming the screen's own words; the open-record limit says "no close action".</summary>
    [Fact]
    public void AC29_the_page_help_steps_explain_Voided_and_the_open_record_limit_in_seven_languages()
    {
        foreach (var lang in Languages)
        {
            var help = Help(lang);
            var words = Words(lang);
            string Step(string key)
            {
                var text = help.GetValueOrDefault(key);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{lang}: {key} is empty");
                return text!;
            }

            Assert.Contains(words["Quarantine.HoldState.Voided"], Step("Help.QUARANTINEQUEUE.Step5"), StringComparison.Ordinal);
            Assert.Contains(words["Quarantine.Common.ConflictBadge"], Step("Help.QUARANTINEDETAILS.Step1"), StringComparison.Ordinal);
            Assert.Contains(words["Quarantine.Blocked.Status.Voided"], Step("Help.BLOCKEDSTOCK.Step1"), StringComparison.Ordinal);
            Assert.Contains(words["Quarantine.Common.ConflictBadge"], Step("Help.BLOCKEDSTOCK.Step1"), StringComparison.Ordinal);
        }

        // the accepted limit (§8.3, F-5@54d736e1): the open flagged record stays open, is counted, and has no close action
        Assert.Contains("kayıt üzerinde kapatma eylemi yoktur", Help("tr")["Help.QUARANTINEDETAILS.Step1"], StringComparison.Ordinal);
        Assert.Contains("açık sayımlarda (KPI, lot kalite durumu, recall) tam miktarıyla görünür", Help("tr")["Help.QUARANTINEDETAILS.Step1"], StringComparison.Ordinal);
        Assert.Contains("kayıt üzerinde kapatma eylemi yoktur", Help("tr")["Help.BLOCKEDSTOCK.Step1"], StringComparison.Ordinal);
        Assert.Contains("there is no close action on the record", Help("en")["Help.QUARANTINEDETAILS.Step1"], StringComparison.Ordinal);
        Assert.Contains("counted with its full quantity in the open counts (KPI, lot quality status, recall)", Help("en")["Help.QUARANTINEDETAILS.Step1"], StringComparison.Ordinal);
        Assert.Contains("there is no close action on the record", Help("en")["Help.BLOCKEDSTOCK.Step1"], StringComparison.Ordinal);
    }

    [Fact]
    public void AC29_the_scripts_hold_no_user_text_of_their_own_for_the_new_states()
    {
        var js = Path.Combine(Web, "wwwroot", "assets", "js", "Quarantine");
        var scripts = new[] { "quarantine-ui.js", "queue.js", "details.js", "blocked.js", "decide.js" }.Select(f => Code(File.ReadAllText(Path.Combine(js, f)))).ToList();
        foreach (var lang in Languages)
        {
            var words = Words(lang);
            foreach (var key in NewKeys.Where(k => !k.EndsWith("Workflow.Voided", StringComparison.Ordinal)))
                Assert.All(scripts, s => Assert.DoesNotContain(words[key], s, StringComparison.Ordinal));
        }

        Assert.Contains("t('Quarantine.Common.ConflictBadge')", scripts[0], StringComparison.Ordinal);
        Assert.Contains("t('Quarantine.Conflict.Cause.' + mark.cause)", scripts[0], StringComparison.Ordinal);
    }

    // ── AC-30: shell and UAS-001 ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Index", "dt-quarantine-holds", null)]
    [InlineData("Details", "qr-d-voided", "model")]
    [InlineData("Blocked", "dt-quarantine-blocked", null)]
    public async Task AC30_the_three_pages_stay_on_the_tenant_shell_and_draw_nothing_without_quarantine_holds_read(string view, string marker, string? model)
    {
        var source = File.ReadAllText(Path.Combine(Web, "Views", "Quarantine", $"{view}.cshtml"));
        Assert.Matches(new Regex(@"(?m)^    Layout = ""_LayoutTenantShell"";\r?$"), source);
        Assert.Contains("@if (!Perms.Has(\"quarantine.holds.read\"))", source, StringComparison.Ordinal);

        var id = model is null ? null : Guid.NewGuid().ToString("D");
        var denied = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Quarantine/{view}.cshtml", id, "quarantine.decisions.create", "quarantine.blocked.dispose");
        Assert.Contains("diten-access-denied", denied, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, denied, StringComparison.Ordinal);
        Assert.DoesNotContain("qr-tabs", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("location.href", denied, StringComparison.Ordinal);

        var allowed = await TraceViewRenderer.RenderAsync(_factory, $"/Views/Quarantine/{view}.cshtml", id, "quarantine.holds.read");
        Assert.Contains(marker, allowed, StringComparison.Ordinal);
    }
}
