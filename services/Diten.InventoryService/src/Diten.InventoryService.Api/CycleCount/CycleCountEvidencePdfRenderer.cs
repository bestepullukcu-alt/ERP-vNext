using Diten.InventoryService.Api.Expiry;
using Diten.InventoryService.Application.Features.CountEvidenceExports;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace Diten.InventoryService.Api.CycleCounting;

/// <summary>
/// MOD-0181 DL-5 — the Evidence Pack PDF (PDFsharp-MigraDoc, MIT — the MOD-0176 / MOD-0177 renderer pattern, an own copy).
/// <list type="bullet">
/// <item>the SAME embedded font as the expiry and recall packs (<see cref="ExpiryEvidencePdfFontResolver"/> — package bytes, no operating-system font
/// lookup, identical on Windows and Linux; called, never changed): one font set for the service, nothing new installed;</item>
/// <item>every value is a literal text run (never MigraDoc DDL / RTF / HTML): record content cannot inject markup; no links, no scripts;</item>
/// <item>the content is <see cref="CountEvidenceSections"/> — the CSV's too (Q-18 = A); words from <see cref="CycleCountEvidenceText"/> in the
/// document's language (en / tr / fr / es / ru — the font's glyphs); a zh / ar request is drawn in English with a note and the requested code at the
/// top (pack §4.5);</item>
/// <item>a render carries a creation time and a random document id: two renders are not byte-identical, which is why the export stores THE bytes it
/// hashed.</item>
/// </list>
/// </summary>
public sealed class CycleCountEvidencePdfRenderer : ICycleCountEvidencePdfRenderer
{
    public byte[] Render(CountEvidenceDocument d)
    {
        ExpiryEvidencePdfFontResolver.EnsureInstalled();
        var lang = d.Language;
        string P(string key) => CycleCountEvidenceText.Of("CountPdf." + key, lang);

        var pdf = new Document();
        pdf.Info.Title = $"{CountEvidenceSections.Title(d)} {d.Header.Number}";
        pdf.Info.Author = "Diten Inventory Service (MOD-0181)";
        var normal = pdf.Styles[StyleNames.Normal]!;
        normal.Font.Name = ExpiryEvidencePdfFontResolver.FamilyName;
        normal.Font.Size = 7.5;

        var section = pdf.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.AddText($"{P("Count")}: {d.Header.Number} · {P("Company")}: {d.Company} · {P("Generated")}: {CountEvidenceSections.Utc(d.GeneratedAt)} · {P("Page")} ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        var title = section.AddParagraph();
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.AddText(CountEvidenceSections.Title(d));

        if (d.EnglishFallback)
        {
            // the requested language is not drawable with the embedded font (zh / ar): say so, in English, with the requested code
            var note = section.AddParagraph();
            note.Format.Font.Bold = true;
            note.Format.SpaceAfter = Unit.FromPoint(4);
            note.AddText(P("EnglishFallback"));
            note.AddLineBreak();
            note.AddText(CycleCountEvidenceText.Format("CountPdf.RequestedLanguage", lang, d.RequestedLanguage));
        }

        var meta = section.AddParagraph();
        meta.Format.SpaceAfter = Unit.FromPoint(8);
        foreach (var (label, value) in CountEvidenceSections.Meta(d))
        {
            meta.AddText($"{label}: {value}");
            meta.AddLineBreak();
        }

        foreach (var s in CountEvidenceSections.Build(d))
        {
            var heading = section.AddParagraph();
            heading.Format.Font.Size = 10;
            heading.Format.Font.Bold = true;
            heading.Format.SpaceBefore = Unit.FromPoint(8);
            heading.Format.SpaceAfter = Unit.FromPoint(4);
            heading.AddText(s.Title);
            if (s.Rows.Count == 0)
            {
                section.AddParagraph().AddText(P("None"));
                continue;
            }

            var table = section.AddTable();
            table.Borders.Width = 0.4;
            table.Borders.Color = Colors.Gray;
            var width = Unit.FromCentimeter(27.3 / s.Header.Length);
            foreach (var _ in s.Header) table.AddColumn(width);
            var head = table.AddRow();
            head.HeadingFormat = true;
            head.Shading.Color = Colors.WhiteSmoke;
            for (var i = 0; i < s.Header.Length; i++)
            {
                var p = head.Cells[i].AddParagraph();
                p.Format.Font.Bold = true;
                p.AddText(s.Header[i]);
            }

            foreach (var values in s.Rows)
            {
                var row = table.AddRow();
                for (var i = 0; i < values.Length; i++) row.Cells[i].AddParagraph().AddText(values[i]);
            }
        }

        var renderer = new PdfDocumentRenderer { Document = pdf };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }
}
