using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W4 · W4-FIX-b (F-5) — a product WITHOUT approved indication × profile rows is not asked the indication potential:
/// (1) report-core.js in Node — the context takes the server's pre-fill flag <c>productSections[].potentialAsked</c> and the
/// products that have rows; <c>potentialAsked</c>, the completeness and the preview follow them; the flag is never sent back;
/// (2) the wizard in a fake DOM — the B card shows the note only (no "no patients" button, no required star); (3) the note in
/// seven languages.
/// </summary>
public sealed class VwW4FixBPotentialWebTests
{
    [Fact]
    public void F5_core_without_indication_rows_asks_nothing_requires_nothing_previews_nothing()
    {
        var node = RunNode("const C = require('" + Path.Combine(ScriptDir(), "report-core.js").Replace("\\", "/") + "');\n" + """
            const A = 'a', B = 'b';
            const section = (id, flag, potential) => ({ productId: id, potentialAsked: flag, potential: potential || null, shareBucketCode: 'half' });
            const prefill = sections => ({
              reportKind: 'doctor', body: { productSections: sections },
              potentials: [{ productId: A, isDue: true, indicationTotal: 12, potentialClass: 'medium', opportunityCode: 'grow', threshold: { lowBelow: 10, highAbove: 30 } },
                           { productId: B, isDue: true, indicationTotal: null, potentialClass: null, threshold: { lowBelow: 10, highAbove: 30 } }],
              indicationRows: [{ productId: A, items: [] , reason: 'no_concept_graph' }, { productId: B, items: [{ indicationNodeId: 'i', profileNodeId: 'p' }] }],
              profile: { totalMonthlyPatients: 40, totalIsDue: false }, competitors: []
            });
            const pf = prefill([section(A, false), section(B, true)]);
            const ctx = C.contextOf(pf);
            const potentialMissing = (body, c) => C.missingDoctor(body, c, []) ;
            const body = { productSections: [section(A, false, { rows: [], noPatientsInIndications: null, updated: true }), section(B, true)] };
            // the old server (no flag): due comes from isDue, rows from indicationRows
            const old = C.contextOf({ reportKind: 'doctor', body: { productSections: [{ productId: A }, { productId: B }] }, potentials: pf.potentials, indicationRows: pf.indicationRows });
            const noList = C.contextOf({ reportKind: 'doctor', body: { productSections: [{ productId: A }] }, potentials: pf.potentials });
            const sent = C.requestBody('pv', { productSections: [section(A, false)] }, 1);
            console.log(JSON.stringify({
              has: [C.hasIndications(ctx, A), C.hasIndications(ctx, B), C.hasIndications(noList, A)],
              asked: [C.potentialAsked(body.productSections[0], ctx), C.potentialAsked(body.productSections[1], ctx),
                      C.potentialAsked(section(A, false), old), C.potentialAsked(section(B, false), old), C.potentialAsked(section(A, false), noList)],
              missing: C.missing({ general: {}, productSections: body.productSections, doctor: {}, safety: {} }, ctx, {})
                .filter(m => m.field === 'potential').map(m => m.productId),
              preview: [C.preview(section(A, false), pf, ctx), C.preview(section(B, true, { rows: [{ indicationNodeId: 'i', profileNodeId: 'p', monthlyPatients: 35 }] }), pf, ctx)],
              sentHasFlag: Object.prototype.hasOwnProperty.call(sent.productSections[0], 'potentialAsked')
            }));
            """);
        if (node is null) return;
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal([false, true, true], r.GetProperty("has").EnumerateArray().Select(x => x.GetBoolean()).ToArray());
        // row-less: not asked even when "updated"; with rows: the server flag; old server: isDue + rows; no list: the old rule
        Assert.Equal([false, true, false, true, true], r.GetProperty("asked").EnumerateArray().Select(x => x.GetBoolean()).ToArray());
        Assert.Equal(["b"], r.GetProperty("missing").EnumerateArray().Select(x => x.GetString()!).ToArray());
        var preview = r.GetProperty("preview").EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.Null, preview[0].GetProperty("total").ValueKind);          // "—"
        Assert.Equal(JsonValueKind.Null, preview[0].GetProperty("potentialClass").ValueKind);
        Assert.Equal(JsonValueKind.Null, preview[0].GetProperty("opportunity").ValueKind);
        Assert.Equal("high", preview[1].GetProperty("potentialClass").GetString());           // rows keep the old preview (35 > 30)
        Assert.False(r.GetProperty("sentHasFlag").GetBoolean());                              // a server member, never sent back
    }

    [Fact]
    public void F5_wizard_shows_the_note_only_for_a_product_without_indication_rows()
    {
        var smoke = Path.Combine(Path.GetDirectoryName(WebRoot())!, "Diten.Web.Tests", "Js", "visit-report-smoke.js").Replace("\\", "/");
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var noInd = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = 'noind'; require('" + smoke + "');");
        var doctor = RunNode("process.argv[2] = '" + crm + "'; process.argv[3] = 'doctor'; require('" + smoke + "');");
        if (noInd is null || doctor is null) return;

        var b = Group(JsonDocument.Parse(noInd).RootElement.GetProperty("productA").GetString()!);
        Assert.Contains("data-f5=\"no-indications\"", b);
        Assert.Contains("Endikasyon listesi tanımlı değil", b);
        Assert.DoesNotContain("Bu endikasyonlarda hasta görmüyor", b);              // no "no patients" button
        Assert.DoesNotContain("data-act=\"ind\"", b);                              // no indication checkboxes
        Assert.DoesNotContain("pot-update", b);                                    // nothing to update
        var label = b[b.IndexOf("Endikasyon başına hasta", StringComparison.Ordinal)..b.IndexOf("data-f5", StringComparison.Ordinal)];
        Assert.DoesNotContain("vr-req", label);                                    // no required star

        // a product WITH rows (the existing doctor scenario) still asks: the indication rows + the "no patients" button
        var withRows = Group(JsonDocument.Parse(doctor).RootElement.GetProperty("productA").GetString()!);
        Assert.Contains("data-act=\"ind\"", withRows);
        Assert.Contains("Bu endikasyonlarda hasta görmüyor", withRows);
        Assert.DoesNotContain("data-f5", withRows);
    }

    [Fact]
    public void F5_the_note_is_in_seven_languages_and_points_to_the_profile_step()
    {
        var dir = Path.Combine(WebRoot(), "Resources", "Views", "CRM", "VisitReport");
        foreach (var culture in new[] { "", ".en", ".tr", ".fr", ".es", ".zh", ".ar", ".ru" })
        {
            var value = XDocument.Load(Path.Combine(dir, "VisitReportIndex" + culture + ".resx")).Root!.Elements("data")
                .Single(d => (string)d.Attribute("name")! == "NoIndicationList").Element("value")!.Value;
            Assert.False(string.IsNullOrWhiteSpace(value), culture);
            Assert.NotEqual("NoIndicationList", value);
        }

        Assert.Contains("profil adımında", XDocument.Load(Path.Combine(dir, "VisitReportIndex.tr.resx")).Root!.Elements("data")
            .Single(d => (string)d.Attribute("name")! == "NoIndicationList").Element("value")!.Value);
    }

    /// <summary>The B card of the product step (from its letter to the next card).</summary>
    private static string Group(string html)
    {
        var start = html.IndexOf("<span class=\"vr-letter\">B</span>", StringComparison.Ordinal);
        Assert.True(start > -1, "no B card");
        var end = html.IndexOf("<span class=\"vr-letter\">C</span>", start, StringComparison.Ordinal);
        return end > -1 ? html[start..end] : html[start..];
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "w4fb-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string ScriptDir() => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "VisitReport");

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
