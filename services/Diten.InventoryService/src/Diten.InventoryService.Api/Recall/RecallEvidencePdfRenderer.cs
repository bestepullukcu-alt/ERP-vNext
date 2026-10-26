using System.Globalization;
using Diten.InventoryService.Api.Expiry;
using Diten.InventoryService.Application.Features.Recall.Evidence;
using Diten.InventoryService.Application.Features.Recall.Ports;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace Diten.InventoryService.Api.Recall;

/// <summary>
/// WP-0177-R7b — the recall Kanıt Paketi PDF (PDFsharp-MigraDoc 6.1.1, MIT — CT-R14), drawn the 0176 F5 way:
/// <list type="bullet">
/// <item>the SAME embedded font resolver as the expiry pack (<see cref="ExpiryEvidencePdfFontResolver"/> — package bytes, no operating-system
/// font lookup; "İ ş ğ ı ç ö ü" print and extract as such): one font set for the service, nothing new installed;</item>
/// <item>every value is a literal text run (never MigraDoc DDL / RTF / HTML) — record content cannot inject markup; no links, no scripts;</item>
/// <item>the seven sections of the preview in its order; a not-applicable section says so; every page's footer names the company, the subject,
/// the generation time and "page n / m";</item>
/// <item>WP-MVP1-FIX-LIVE2: words from <see cref="RecallEvidenceText"/> (the service resx, shared keys = the screen's); the document is already
/// worded in the PDF's language — tr / en / fr / es / ru (the font's glyphs, measured); a zh / ar selection is drawn in English with a note
/// at the top (FU-0176-PDF-L10N: font + Arabic shaping / right-to-left). The id columns beside a name (<see cref="RecallEvidenceTable.PdfHidden"/>)
/// stay in the CSV and on the screen.</item>
/// </list>
/// A render carries a creation time and a document id, so two renders differ — the export stores THE bytes it hashed.
/// </summary>
public sealed class RecallEvidencePdfRenderer : IRecallEvidencePdfRenderer
{
    public byte[] Render(RecallEvidenceDocument d)
    {
        ExpiryEvidencePdfFontResolver.EnsureInstalled();
        var l = d.Language;
        string L(string key) => RecallEvidenceText.Word(key, l);
        string P(string key) => RecallEvidenceText.Of("RecallPdf." + key, l);

        var pdf = new Document();
        pdf.Info.Title = $"{P("Title")} {d.SubjectLabel}";
        pdf.Info.Author = "Diten Inventory Service (MOD-0177)";
        var normal = pdf.Styles[StyleNames.Normal]!;
        normal.Font.Name = ExpiryEvidencePdfFontResolver.FamilyName;
        normal.Font.Size = 8;

        var section = pdf.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.AddText($"{L("company")}: {d.Company} · {P("Subject")}: {d.SubjectLabel} · {P("Generated")}: {Utc(d.GeneratedAtUtc)} · {P("Page")} ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        var title = section.AddParagraph();
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.AddText(P("Title"));

        if (d.EnglishFallback)
        {
            // the selected language is not drawable with the embedded font (zh / ar — measured): say so, in English, with the requested code
            var note = section.AddParagraph();
            note.Format.Font.Bold = true;
            note.Format.SpaceAfter = Unit.FromPoint(4);
            note.AddText(L("englishFallback"));
            note.AddLineBreak();
            note.AddText(RecallEvidenceText.Format("RecallPdf.RequestedLanguage", l, d.RequestedLanguage));
        }

        var meta = section.AddParagraph();
        meta.Format.SpaceAfter = Unit.FromPoint(8);
        meta.AddText($"{P("Subject")}: {P("Kind." + d.SubjectKind)} · {d.SubjectLabel} · {L("company")}: {d.Company}");
        meta.AddLineBreak();
        meta.AddText($"{P("Generated")}: {Utc(d.GeneratedAtUtc)}");

        foreach (var s in d.Sections)
        {
            var heading = section.AddParagraph();
            heading.Format.Font.Size = 10;
            heading.Format.Font.Bold = true;
            heading.Format.SpaceBefore = Unit.FromPoint(8);
            heading.Format.SpaceAfter = Unit.FromPoint(4);
            heading.AddText(L(s.Key));
            if (!s.Applicable)
            {
                section.AddParagraph().AddText(L(s.Note ?? "notApplicable"));
                continue;
            }

            if (s.Note is not null) section.AddParagraph().AddText(L(s.Note));
            foreach (var t in s.Tables)
            {
                if (s.Tables.Count > 1)
                {
                    var sub = section.AddParagraph();
                    sub.Format.Font.Bold = true;
                    sub.Format.SpaceBefore = Unit.FromPoint(4);
                    sub.AddText(L(t.Key));
                }

                if (t.Rows.Count == 0)
                {
                    section.AddParagraph().AddText(L("none"));
                    continue;
                }

                // the id columns beside a name stay in the CSV / on the screen; the PDF prints the names
                var shown = Enumerable.Range(0, t.Columns.Count).Where(i => t.PdfHidden?.Contains(t.Columns[i]) != true).ToList();
                var table = section.AddTable();
                table.Borders.Width = 0.4;
                table.Borders.Color = Colors.Gray;
                var width = 26.0 / shown.Count; // landscape A4 minus margins ≈ 26.9 cm
                foreach (var _ in shown) table.AddColumn(Unit.FromCentimeter(width));
                var head = table.AddRow();
                head.HeadingFormat = true;
                head.Shading.Color = Colors.WhiteSmoke;
                for (var c = 0; c < shown.Count; c++)
                {
                    var p = head.Cells[c].AddParagraph();
                    p.Format.Font.Bold = true;
                    p.AddText(L(t.Columns[shown[c]]));
                }

                foreach (var values in t.Rows)
                {
                    var row = table.AddRow();
                    for (var c = 0; c < shown.Count && shown[c] < values.Count; c++)
                    {
                        // a field row names its field: the word in the pack's language; every other cell is already worded
                        var cell = t.IsFieldTable && shown[c] == 0 ? L(values[0]) : values[shown[c]];
                        row.Cells[c].AddParagraph().AddText(cell);
                    }
                }
            }
        }

        var renderer = new PdfDocumentRenderer { Document = pdf };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
}
