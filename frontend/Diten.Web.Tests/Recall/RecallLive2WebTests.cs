using System.Text.RegularExpressions;
using Diten.Web.Models.Inventory;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-FIX-LIVE2 — the screen findings of the live round (case RC-2026-0001), one test each, against the SHIPPED files:
/// <list type="bullet">
/// <item>4 — Vaka Detayı shows the STORED scope seals rc-v1 signs, by the e-sign dialog's / the pack's names, and the live digest by its own;</item>
/// <item>6 — "Taslak vaka aç" opens the new case's Vaka Detayı (chosen over a toast: the next step — scope review / submit — is there);</item>
/// <item>7 — with no customer in the scope the "İadeyi bağla" modal explains why and draws no bind button;</item>
/// <item>8 — "Müşteri iadesi gir" carries itemText / skuText and the MOD-0173 form reads them for display;</item>
/// <item>the evidence pages ask the service in the PAGE language (one of the 7) — PDF / CSV / preview.</item>
/// </list>
/// The rules are measured by recall-live2.test.mjs (Node); the wiring below is read from the page scripts / views.
/// </summary>
public sealed class RecallLive2WebTests
{
    private static readonly string Web = TraceViewRenderer.WebRoot;

    private static string Script(string module, string file) => Code(File.ReadAllText(Path.Combine(Web, "wwwroot", "assets", "js", module, file)));

    private static string Code(string source) => Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty), @"(?m)^\s*//.*$", string.Empty);

    [Fact]
    public void Finding_6_a_new_case_opens_its_Vaka_Detayi()
    {
        var trace = Script("Recall", "trace.js");
        Assert.Contains("const created = reply.json?.data || reply.json || {};", trace, StringComparison.Ordinal);
        Assert.Contains("view.caseHref(created.caseId)", trace, StringComparison.Ordinal);
        Assert.Contains("window.location.assign(href)", trace, StringComparison.Ordinal);
    }

    [Fact]
    public void Finding_7_no_customer_in_scope_explains_why_and_draws_no_bind_button()
    {
        var modal = File.ReadAllText(Path.Combine(Web, "Views", "Recall", "_LinkReturnModal.cshtml"));
        Assert.Contains("id=\"rc-link-none\"", modal, StringComparison.Ordinal);
        Assert.Contains("@Localizer[\"Recall.Link.NoCustomers\"]", modal, StringComparison.Ordinal);
        Assert.Contains("id=\"rc-link-submit\"", modal, StringComparison.Ordinal);
        var detail = Script("Recall", "case-detail.js");
        // the fields and the button are not drawn (d-none), never merely disabled
        Assert.Contains("const none = view.linkable(customers) === false;", detail, StringComparison.Ordinal);
        Assert.Contains("['rc-link-fields', 'rc-link-reference-box', 'rc-link-submit'].forEach((id) => byId(id)?.classList.toggle('d-none', none));", detail, StringComparison.Ordinal);
        Assert.Contains("byId('rc-link-none')?.classList.toggle('d-none', !none);", detail, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"rc-link-submit[^\n]*disabled"), detail);
        Assert.Contains("Recall.Link.NoCustomers", Diten.Web.Views.Recall.Recall.Keys);
    }

    [Fact]
    public void Finding_4_the_scope_tab_draws_the_stored_seals_by_name()
    {
        var detail = Script("Recall", "case-detail.js");
        Assert.Contains("const seals = view.seals(state.detail, scope);", detail, StringComparison.Ordinal);
        Assert.Contains("data-seal=", detail, StringComparison.Ordinal);
        Assert.Contains("Recall.Scope.SealDiffers", detail, StringComparison.Ordinal);
        foreach (var key in new[] { "Recall.Scope.SubmittedSeal", "Recall.Scope.ApprovedSeal", "Recall.Scope.Seal", "Recall.Scope.SealDiffers" })
            Assert.Contains(key, Diten.Web.Views.Recall.Recall.Keys);
    }

    [Fact]
    public void Finding_8_the_MOD_0173_form_reads_the_product_and_SKU_texts_for_display()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["type"] = "CUSTOMER_RETURN", ["itemId"] = "b1f2c3d4-0000-0000-0000-000000000001", ["itemText"] = "GP-0042 · Aspirin 100 mg",
            ["sku"] = "Lsku:c3d4e5f6-0000-0000-0000-000000000002", ["skuText"] = "LS-0042", ["lotId"] = "d4e5f6a7-0000-4000-8000-000000000042",
            ["toStatus"] = "QUARANTINE"
        });
        var form = StockMovementFormViewModel.ForCreate(query);
        Assert.Equal(("GP-0042 · Aspirin 100 mg", "LS-0042", "CUSTOMER_RETURN"), (form.ItemText, form.SkuText, form.MovementType));
        Assert.Equal("b1f2c3d4-0000-0000-0000-000000000001", form.ItemId);  // the id still posts; the text is display only
        Assert.Contains("query.set('itemText', c.itemLabel)", Script("Recall", "case-view.js"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_evidence_pages_ask_in_the_page_language()
    {
        var recall = Script("Recall", "evidence.js");
        Assert.Contains("view.exportFile(deps, state.subject || current(), b.dataset.evFormat, pageLanguage)", recall, StringComparison.Ordinal);
        Assert.Contains("'Accept-Language': pageLanguage", recall, StringComparison.Ordinal);
        Assert.DoesNotContain("startsWith('tr') ? 'tr' : 'en'", recall, StringComparison.Ordinal);
        var expiry = Script("Expiry", "evidence.js");
        Assert.Contains("'Accept-Language': pageLanguage", expiry, StringComparison.Ordinal);
        Assert.Contains("const reply = await exportIn({ idempotencyKey: key, format, ...scope() }, key);", expiry, StringComparison.Ordinal);
        Assert.Contains("Expiry.Evidence.EnglishFallback", Diten.Web.Views.Expiry.Expiry.Keys);
    }
}

/// <summary>The bridge that makes `dotnet test` run recall-live2.test.mjs (findings 4, 6, 7, 8) against the SHIPPED scripts.</summary>
public sealed class RecallLive2NodeTests
{
    [Fact]
    public async Task The_live_round_screen_rules_hold_against_the_shipped_files()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-live2.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 4$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
