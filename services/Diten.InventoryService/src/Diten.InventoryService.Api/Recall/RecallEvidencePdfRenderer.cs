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
/// <item>labels tr or en (<see cref="RecallEvidenceLabels"/>; any other language → en — the FU-0176-PDF-L10N limit); values stay the records'
/// codes / ids (as in the 0176 pack).</item>
/// </list>
/// A render carries a creation time and a document id, so two renders differ — the export stores THE bytes it hashed.
/// </summary>
public sealed class RecallEvidencePdfRenderer : IRecallEvidencePdfRenderer
{
    public byte[] Render(RecallEvidenceDocument d)
    {
        ExpiryEvidencePdfFontResolver.EnsureInstalled();
        var l = d.Language;
        string L(string key) => RecallEvidenceLabels.Of(key, l);

        var pdf = new Document();
        pdf.Info.Title = $"{L("title")} {d.SubjectLabel}";
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
        footer.AddText($"{L("company")}: {d.LegalEntityId:D} · {L("subject")}: {d.SubjectLabel} · {L("generated")}: {Utc(d.GeneratedAtUtc)} · {L("page")} ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        var title = section.AddParagraph();
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.AddText(L("title"));

        var meta = section.AddParagraph();
        meta.Format.SpaceAfter = Unit.FromPoint(8);
        meta.AddText($"{L("subject")}: {L("kind." + d.SubjectKind)} · {d.SubjectLabel} · {L("company")}: {d.LegalEntityId:D}");
        meta.AddLineBreak();
        meta.AddText($"{L("generated")}: {Utc(d.GeneratedAtUtc)}");

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

                var table = section.AddTable();
                table.Borders.Width = 0.4;
                table.Borders.Color = Colors.Gray;
                var width = 26.0 / t.Columns.Count; // landscape A4 minus margins ≈ 26.9 cm
                foreach (var _ in t.Columns) table.AddColumn(Unit.FromCentimeter(width));
                var head = table.AddRow();
                head.HeadingFormat = true;
                head.Shading.Color = Colors.WhiteSmoke;
                for (var i = 0; i < t.Columns.Count; i++)
                {
                    var p = head.Cells[i].AddParagraph();
                    p.Format.Font.Bold = true;
                    p.AddText(L(t.Columns[i]));
                }

                foreach (var values in t.Rows)
                {
                    var row = table.AddRow();
                    for (var i = 0; i < t.Columns.Count && i < values.Count; i++)
                    {
                        // a summary row names a field: its label in the reader's language; every other cell is the record's own value
                        var cell = t.Key == "summary" && i == 0 ? L(values[i]) : values[i];
                        row.Cells[i].AddParagraph().AddText(cell);
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
