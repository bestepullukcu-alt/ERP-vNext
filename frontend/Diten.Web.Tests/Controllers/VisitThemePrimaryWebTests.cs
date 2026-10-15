using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-THEME (K-THEME) — the CRM visit pages take their primary colour from the layout (the Template
/// Customizer writes <c>--bs-primary</c> / <c>--bs-primary-rgb</c> on :root): (1) the guard — none of the 11 visit files
/// carries the old navy / purple family (hex or rgb); (2) the behaviour — with another :root primary, the page tokens of
/// the visit CSS (light and dark blocks) resolve to it, and the presentation's signature ink (the REAL bindCanvas of
/// visit-presentation.js) follows it. The semantic colours (ok / warn / danger, the hospital turquoise) are not touched.
/// </summary>
public sealed class VisitThemePrimaryWebTests
{
    /// <summary>The visit family (W3E-THEME scope). backbone-custom.css and pages/demand-idea-capture.css are another module (XMC-001).</summary>
    public static readonly string[] Scope =
    [
        "css/visit-planning.css", "js/CRM/VisitPlanning/details.js", "js/CRM/VisitWorkspace/visit-workspace.css",
        "js/CRM/VisitPresentation/visit-presentation.css", "js/CRM/VisitPresentation/visit-presentation.js", "js/CRM/VisitReport/visit-report.css",
        "js/CRM/PharmacyReport/pharmacy-report.css", "js/CRM/PotentialThresholds/potential-thresholds.css",
        "js/CRM/VisitEvidenceSettings/visit-evidence-settings.css", "js/CRM/VisitQrCodes/visit-qr.css", "js/CRM/VisitEvidenceFlags/visit-evidence-flags.css"
    ];

    private static readonly string[] OldPrimaryFamily =
    [
        "#0b1a8c", "#e0e2f3", "#c5c9e8", "#4b55a8", "#f3f4fb", "#4655d6", "#2f3163", "#4a4f8c", "#a3aaf5", "#696cff", "#e7e7ff",
        "#5f61e6", "#a5a6ff", "#7b7eff", "#35366a", "#a3a5ff", "#6d70e8"
    ];

    [Fact]
    public void T1_no_visit_file_hard_codes_the_old_navy_or_purple_family()
    {
        foreach (var file in Scope)
        {
            var text = File.ReadAllText(Asset(file));
            foreach (var hex in OldPrimaryFamily)
            {
                Assert.False(text.Contains(hex, StringComparison.OrdinalIgnoreCase), $"{file}: {hex}");
            }

            Assert.False(Regex.IsMatch(text, @"\b11,\s*26,\s*140\b|\b105,\s*108,\s*255\b"), $"{file}: an rgb of the old primary");
            Assert.Contains("--bs-primary", text);                                                   // it takes the layout's colour
        }
    }

    [Theory]
    [InlineData("js/CRM/VisitWorkspace/visit-workspace.css", "--vw-p", "--vw-pl")]
    [InlineData("js/CRM/VisitQrCodes/visit-qr.css", "--vq-p", "--vq-ps")]
    [InlineData("js/CRM/VisitEvidenceFlags/visit-evidence-flags.css", "--vf-p", "--vf-pl")]
    [InlineData("js/CRM/PharmacyReport/pharmacy-report.css", "--ph-p", "--ph-pl")]
    [InlineData("js/CRM/VisitReport/visit-report.css", "--vr-p", "--vr-pl")]
    [InlineData("js/CRM/PotentialThresholds/potential-thresholds.css", "--pt-p", "--pt-pl")]
    [InlineData("js/CRM/VisitEvidenceSettings/visit-evidence-settings.css", "--ve-p", "--ve-pl")]
    [InlineData("js/CRM/VisitPresentation/visit-presentation.css", "--vp-p", "--vp-pl")]
    [InlineData("css/visit-planning.css", "--vp-indigo", "--vp-indigo-soft")]
    public void T2_the_page_tokens_resolve_to_the_layouts_primary_in_light_and_dark(string file, string primary, string soft)
    {
        var node = RunNode("""
            const fs = require('fs');
            const css = fs.readFileSync(process.argv[2], 'utf8');
            const decls = name => [...css.matchAll(new RegExp('(?:^|[;{\\s])' + name.replace(/-/g, '\\-') + '\\s*:\\s*([^;]+);', 'g'))].map(m => m[1].trim());
            const resolve = (value, rootVars, depth) => {
              if ((depth || 0) > 10) return value;
              return value.replace(/var\((--[\w-]+)(?:\s*,\s*([^)]*))?\)/g, (m, n, fb) => {
                if (rootVars[n] != null) return resolve(rootVars[n], rootVars, (depth || 0) + 1);
                const own = decls(n)[0];
                return own != null ? resolve(own, rootVars, (depth || 0) + 1) : (fb || '');
              });
            };
            const rootOf = (hex, rgb) => ({ '--bs-primary': hex, '--bs-primary-rgb': rgb });
            const at = (name, root) => decls(name).map(v => resolve(v, root).replace(/\s+/g, ' '));
            process.stdout.write(JSON.stringify({
              defaultPrimary: at(process.argv[3], rootOf('#696cff', '105, 108, 255')),
              orangePrimary: at(process.argv[3], rootOf('#ff9f43', '255, 159, 67')),
              orangeSoft: at(process.argv[4], rootOf('#ff9f43', '255, 159, 67'))
            }));
            """, Asset(file), primary, soft);
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        string[] A(string name) => r.GetProperty(name).EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.NotEmpty(A("defaultPrimary"));
        Assert.All(A("defaultPrimary"), v => Assert.Contains("#696cff", v));              // every block (light + dark) derives from the layout's colour
        Assert.All(A("orangePrimary"), v => Assert.Contains("#ff9f43", v));               // the customizer's orange → the page follows
        Assert.Equal("#ff9f43", A("orangePrimary")[0]);                                     // the light block IS the primary itself
        Assert.All(A("orangeSoft"), v => Assert.Contains("255, 159, 67", v));              // the soft tone derives from --bs-primary-rgb
    }

    [Fact]
    public void T3_the_presentation_signature_ink_follows_the_layouts_primary()
    {
        var node = RunNode("""
            const src = require('fs').readFileSync(process.argv[2], 'utf8');
            const start = src.indexOf('function bindCanvas()');
            const end = src.indexOf('function syncSignatureButton');
            if (start < 0 || end < 0) throw new Error('bindCanvas not found');
            const root = { '--bs-primary': '#696cff' };
            const handlers = {};
            const ctx = { strokes: [], set strokeStyle(v) { this.strokes.push(v); }, get strokeStyle() { return this.strokes[this.strokes.length - 1]; },
              lineWidth: 0, lineCap: '', beginPath() {}, moveTo() {}, lineTo() {}, stroke() {} };
            const canvas = { width: 700, height: 160, getContext: () => ctx, addEventListener: (ev, fn) => { handlers[ev] = fn; }, setPointerCapture() {},
              getBoundingClientRect: () => ({ left: 0, top: 0, width: 700, height: 160 }) };
            const window = { getComputedStyle: el => ({ getPropertyValue: k => (el === document.documentElement ? root[k] || '' : '') }) };
            const document = { documentElement: {} };
            const el = id => (id === 'vp-sig' ? canvas : null);
            const state = {};
            const bindCanvas = new Function('window', 'document', 'el', 'state', 'syncSignatureButton', src.slice(start, end) + '\nreturn bindCanvas;')(window, document, el, state, () => {});
            bindCanvas();
            const down = () => handlers.pointerdown({ pointerId: 1, clientX: 10, clientY: 10, preventDefault() {} });
            down();
            const first = ctx.strokeStyle;
            root['--bs-primary'] = '#ff9f43'; // the customizer picks orange
            down();
            process.stdout.write(JSON.stringify({ first, second: ctx.strokeStyle }));
            """, Asset("js/CRM/VisitPresentation/visit-presentation.js"));
        if (node is null) { return; }
        var r = JsonDocument.Parse(node).RootElement;
        Assert.Equal("#696cff", r.GetProperty("first").GetString());
        Assert.Equal("#ff9f43", r.GetProperty("second").GetString());
    }

    [Fact]
    public void T4_the_planning_inline_colours_read_the_layouts_primary_and_the_type_colours_stay()
    {
        var js = File.ReadAllText(Asset("js/CRM/VisitPlanning/details.js"));
        Assert.Contains("clinic: 'background:rgba(var(--bs-primary-rgb),.12);color:var(--bs-primary)'", js);
        Assert.Contains("hospital: 'background:#d7f5fc;color:#028aa6'", js);                    // a type colour, not the primary
        Assert.Contains("border-inline-start:3px solid var(--bs-primary) !important;", js);
        Assert.Contains("getComputedStyle(document.documentElement).getPropertyValue('--bs-primary')", js); // the map route
        // the QR print stays black on white (a scanner reads it): the Web draws it with fixed colours
        Assert.Contains("\"#000000\", \"#ffffff\"", File.ReadAllText(Path.Combine(WebRoot(), "Controllers", "CRM", "VisitQrSvg.cs")));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────

    private static string Asset(string relative) => Path.Combine(WebRoot(), "wwwroot", "assets", relative.Replace('/', Path.DirectorySeparatorChar));

    private static string? RunNode(string code, params string[] args)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vtheme-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8 };
            start.ArgumentList.Add(temp);
            foreach (var a in args) start.ArgumentList.Add(a);
            using var process = Process.Start(start);
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

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
