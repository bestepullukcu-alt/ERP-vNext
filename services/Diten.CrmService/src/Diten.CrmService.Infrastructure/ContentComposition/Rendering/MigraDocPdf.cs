using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace Diten.CrmService.Infrastructure.ContentComposition.Rendering;

/// <summary>
/// The MigraDoc building blocks shared by the CRM PDF renderers (SCMM-16B content set revision, WP-KP-3 knowledge path
/// revision): one A4 document style, headings, label/value fields, a stable "—" for empty values, file-name sanitising
/// and the bytes. Fonts follow the SCMM-16B note (Windows fonts; Linux font resolver is F-SCMM-16B-FONT).
/// </summary>
internal static class MigraDocPdf
{
    private const string BodyFont = "Arial";

    public static (Document Document, Section Section) Create(string title, string subject)
    {
        var document = new Document();
        document.Info.Title = title;
        document.Info.Subject = subject;

        var normal = document.Styles["Normal"]!;
        normal.Font.Name = BodyFont;
        normal.Font.Size = 10;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        return (document, section);
    }

    public static void AddTitle(Section section, string text)
    {
        var title = section.AddParagraph(text);
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = "6pt";
    }

    public static void AddHeading(Section section, string text)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Size = 13;
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = "10pt";
        p.Format.SpaceAfter = "4pt";
    }

    public static void AddField(Section section, string label, string? value)
    {
        var p = section.AddParagraph();
        p.AddFormattedText($"{label}: ", TextFormat.Bold);
        p.AddText(DisplayText(value));
    }

    public static string DisplayText(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    public static string Sanitize(string? name, string fallback)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string(trimmed.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    public static byte[] ToBytes(Document document)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
