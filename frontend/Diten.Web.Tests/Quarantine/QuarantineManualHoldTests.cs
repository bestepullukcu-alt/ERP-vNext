using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Quarantine;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — FU-0175-UI-MANUALHOLD-01: "Stoğu karantinaya al" on the quarantine queue. UAS-001: without quarantine.holds.create the
/// modal and its script are not drawn (absent, not disabled); with it they are, with the UXH-001 "?" help. The rules (mode Manual, one bucket,
/// quantity ≤ on hand, reason required) are measured by quarantine-manualhold.test.mjs against the shipped script.
/// </summary>
public sealed class QuarantineManualHoldTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QuarantineManualHoldTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Without_the_create_key_the_button_modal_and_script_are_not_drawn()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Index.cshtml", null, "quarantine.holds.read");
        Assert.DoesNotContain("id=\"qr-manual-modal\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("manual-hold.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("qr-manual-confirm-template", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"qr-antiforgery\"", html, StringComparison.Ordinal);
    }

    /// <summary>The write needs the page's antiforgery token (QuarantineUi.write reads #qr-antiforgery) — found missing in the CLEAN1 preview (400).</summary>
    [Fact]
    public async Task With_the_create_key_the_page_carries_the_antiforgery_token_the_write_sends()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Index.cshtml", null, "quarantine.holds.read", "quarantine.holds.create");
        Assert.Matches(new Regex(@"<form id=""qr-antiforgery"" class=""d-none""><input name=""__RequestVerificationToken"" type=""hidden"" value=""[^""]+"""), html);
        var ui = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Quarantine", "quarantine-ui.js"));
        Assert.Contains("document.querySelector('#qr-antiforgery input[name=\"__RequestVerificationToken\"]')", ui, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_the_create_key_the_modal_its_help_and_its_script_are_drawn()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Quarantine/Index.cshtml", null, "quarantine.holds.read", "quarantine.holds.create");
        Assert.Contains("id=\"qr-manual-modal\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"qr-manual-confirm-template\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"qr-manual-help-toggle\"", html, StringComparison.Ordinal);
        Assert.Contains("data-help=\"QUARANTINEMANUALHOLD\"", html, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"src=""/assets/js/Quarantine/manual-hold\.js"), html);
        // the queue script draws the toolbar button only for QUARANTINELOT (quarantine.holds.create) and when the modal is on the page
        var queue = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Quarantine", "queue.js"));
        Assert.Contains("addNewText: ui.can('QUARANTINELOT') && ui.byId('qr-manual-modal') ? t('Quarantine.ManualHold.Action') : ''", queue, StringComparison.Ordinal);
        foreach (var key in new[] { "Quarantine.ManualHold.Action", "Quarantine.ManualHold.ConfirmTitle", "Help.QUARANTINEMANUALHOLD.Purpose", "Help.QUARANTINEMANUALHOLD.Step2" })
            Assert.Contains(key, Diten.Web.Views.Quarantine.Quarantine.Keys);
    }

    [Fact]
    public async Task The_manual_hold_rules_hold_against_the_shipped_script()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "quarantine-manualhold.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 4$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
