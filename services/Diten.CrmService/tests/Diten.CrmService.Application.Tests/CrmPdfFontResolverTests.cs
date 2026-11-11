using System.Text;
using System.Text.RegularExpressions;
using Diten.CrmService.Application.Features.Knowledge.Path.Release;
using Diten.CrmService.Infrastructure.ContentComposition.Rendering;
using PdfSharp.WPFonts;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// CRM-APPTESTS-FIX-01 (K1) — the CRM PDFs use the embedded Segoe WP family (<see cref="CrmPdfFontResolver"/>), never an
/// OS-installed font: the produced PDF embeds only that family (no Arial, even on Windows where Arial exists), an italic
/// request falls back to the upright face, and the embedded face carries every character the knowledge path renderer writes.
/// </summary>
public sealed class CrmPdfFontResolverTests
{
    // Every non-ASCII character the knowledge path renderer and its Turkish data can put on the page.
    private const string RendererCharacters = "çğıöşüÇĞİÖŞÜ›“”•—∞·";

    [Fact]
    public void The_rendered_pdf_embeds_only_the_crm_font_family_and_never_an_os_font()
    {
        var pdf = new PdfSharpKnowledgePathRevisionRenderer().Render(Model()).Bytes;

        var baseFonts = BaseFonts(pdf);
        Assert.NotEmpty(baseFonts);
        // Subset tag + the face's own name, e.g. "BAUJPT+Segoe#20WP" and "EOJGFW+Segoe#20WP,Bold".
        Assert.All(baseFonts, name => Assert.Matches(@"^[A-Z]{6}\+Segoe#20WP(,Bold)?$", name));
        Assert.DoesNotContain("Arial", Encoding.Latin1.GetString(pdf), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Regular_bold_and_italic_requests_resolve_to_the_embedded_faces()
    {
        var resolver = new CrmPdfFontResolver();

        var regular = resolver.ResolveTypeface(CrmPdfFontResolver.FamilyName, bold: false, italic: false)!.FaceName;
        var bold = resolver.ResolveTypeface(CrmPdfFontResolver.FamilyName, bold: true, italic: false)!.FaceName;
        Assert.Equal(regular, resolver.ResolveTypeface(CrmPdfFontResolver.FamilyName, bold: false, italic: true)!.FaceName);
        Assert.Equal(bold, resolver.ResolveTypeface(CrmPdfFontResolver.FamilyName, bold: true, italic: true)!.FaceName);
        Assert.Equal(FontDataHelper.SegoeWP, resolver.GetFont(regular));
        Assert.Equal(FontDataHelper.SegoeWPBold, resolver.GetFont(bold));
        Assert.Null(resolver.ResolveTypeface("Arial", bold: false, italic: false));
    }

    [Fact]
    public void The_embedded_faces_carry_every_character_the_renderer_writes()
    {
        foreach (var face in new[] { FontDataHelper.SegoeWP, FontDataHelper.SegoeWPBold })
        {
            var mapped = CmapFormat4(face);
            var missing = RendererCharacters.Where(ch => !mapped(ch)).ToArray();
            Assert.True(missing.Length == 0, $"Missing glyphs: {string.Join(", ", missing.Select(ch => $"U+{(int)ch:X4}"))}");
        }
    }

    private static KnowledgePathRenderModel Model() => new(
        "KP-A-v1.0-R1", "KP-A", "Yorgunluk yolu — Türkçe", "1.0", 1, "Almiba", "1", "Türkiye", "Türkçe", "ALMIBA 1 g",
        ["Hekim"],
        [
            new KnowledgePathRenderSlot("Ana dal", "İhtiyaç", 1, 1, null, "ok",
                [new KnowledgePathRenderContent(1, "Şeker ölçümü", "KC-1", "1.0", true)],
                [new KnowledgePathRenderClaim("CLM-1", "Levokarnitin enerji metabolizmasını destekler.", "Diyaliz hastalarında", "1.0")])
        ],
        [new KnowledgePathRenderReviewEntry("approve", "Medikal inceleme", "Dr. Ayşe Kaya", new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero), "Uygun.")],
        "Gülşen Öztürk", new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero), "Çağrı Işık");

    private static List<string> BaseFonts(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/BaseFont\s*/([^\s/<>\[\]()]+)").Select(m => m.Groups[1].Value).ToList();

    // Minimal TrueType 'cmap' reader: the Windows Unicode BMP subtable (platform 3, encoding 1, format 4).
    private static Func<char, bool> CmapFormat4(byte[] font)
    {
        ushort U16(int at) => (ushort)((font[at] << 8) | font[at + 1]);
        uint U32(int at) => ((uint)U16(at) << 16) | U16(at + 2);

        var cmap = -1;
        for (int i = 0, tables = U16(4); i < tables; i++)
        {
            var record = 12 + i * 16;
            if (Encoding.ASCII.GetString(font, record, 4) == "cmap")
            {
                cmap = (int)U32(record + 8);
            }
        }

        Assert.True(cmap >= 0, "The font has no cmap table.");
        var sub = -1;
        for (int i = 0, count = U16(cmap + 2); i < count; i++)
        {
            var record = cmap + 4 + i * 8;
            if (U16(record) == 3 && U16(record + 2) == 1)
            {
                sub = cmap + (int)U32(record + 4);
            }
        }

        Assert.True(sub >= 0 && U16(sub) == 4, "The font has no Windows Unicode BMP (format 4) cmap.");
        var segments = U16(sub + 6) / 2;
        var ends = sub + 14;
        var starts = ends + segments * 2 + 2;
        var deltas = starts + segments * 2;
        var offsets = deltas + segments * 2;

        return ch =>
        {
            for (var s = 0; s < segments; s++)
            {
                if (ch > U16(ends + s * 2))
                {
                    continue;
                }

                var start = U16(starts + s * 2);
                if (ch < start)
                {
                    return false;
                }

                var rangeOffset = U16(offsets + s * 2);
                var glyph = rangeOffset == 0
                    ? (ch + U16(deltas + s * 2)) & 0xFFFF
                    : U16(offsets + s * 2 + rangeOffset + (ch - start) * 2) is var g && g != 0 ? (g + U16(deltas + s * 2)) & 0xFFFF : 0;
                return glyph != 0;
            }

            return false;
        };
    }
}
