using System.Globalization;
using Diten.Platform.Application.Features.ESignature.Handlers;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.WPFonts;

namespace Diten.Platform.Infrastructure.Services.ESignature;

/// <summary>
/// MOD-0022 — the export PDF (KS-6, CT-D11: server-side PDFsharp-MigraDoc 6.1.1, the version CrmService already ships).
///
/// <para>CONFIGURATION SAFETY (CT-D11 review, E2 evidence):</para>
/// <list type="bullet">
/// <item>Fonts come only from the package's own embedded Segoe WP data (<see cref="FontDataHelper"/>) through
/// <see cref="ESignaturePdfFontResolver"/>: no operating-system font lookup, no file-system read, identical on Windows and
/// Linux (CrmService's renderer has no resolver and depends on installed fonts — F-SCMM-16B-FONT).</item>
/// <item>Every text is added as a literal MigraDoc text run — never parsed as MigraDoc DDL, RTF or HTML — so record
/// content cannot inject markup. No hyperlinks, no JavaScript, no attachments, no form fields, no encryption options.</item>
/// <item>The drawn PNG is embedded from memory (<c>base64:</c> image source) and only when it still matches its recorded
/// hash; nothing is read from a path a record could name.</item>
/// <item>The output carries a creation time and a random document ID, so two renders are not byte-identical; that is why
/// the SHA-256 of THE bytes sent is what the export log records.</item>
/// </list>
/// <para>Known limit: Segoe WP covers Latin (incl. Turkish) and Cyrillic; Arabic and Chinese meaning texts print without
/// glyph shaping — the CSV export carries them exactly.</para>
/// </summary>
public sealed class MigraDocESignaturePdfRenderer : IESignaturePdfRenderer
{
    private const string FontFamily = ESignaturePdfFontResolver.FamilyName;

    public byte[] Render(ESignatureExportDocument document)
    {
        ESignaturePdfFontResolver.EnsureInstalled();

        var pdf = new Document();
        pdf.Info.Title = $"e-İmza denetim dışa aktarımı {document.ExportNumber}";
        pdf.Info.Author = "Diten e-Signature Service (MOD-0022)";
        var normal = pdf.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontFamily;
        normal.Font.Size = 9;

        var section = pdf.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);

        var title = section.AddParagraph();
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.AddText("e-İmza Denetim Dışa Aktarımı");

        var meta = section.AddParagraph();
        meta.Format.SpaceAfter = Unit.FromPoint(10);
        meta.AddText($"Dışa aktarım: {document.ExportNumber} · {Utc(document.AtUtc)} · {document.ByName}");
        meta.AddLineBreak();
        meta.AddText($"Filtre: {document.FilterText} · Kayıt sayısı: {document.Rows.Count}");

        foreach (var row in document.Rows)
        {
            AddSignatureBlock(section, row);
        }

        if (document.Rows.Count == 0)
        {
            section.AddParagraph().AddText("Filtreye uyan imza yok.");
        }

        var renderer = new PdfDocumentRenderer { Document = pdf };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void AddSignatureBlock(Section section, ESignatureExportRow row)
    {
        var r = row.Record;
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.AddColumn(Unit.FromCentimeter(5.2));
        table.AddColumn(Unit.FromCentimeter(12.4));
        table.KeepTogether = true;

        var head = table.AddRow();
        head.Shading.Color = Colors.WhiteSmoke;
        head.Cells[0].MergeRight = 1;
        var heading = head.Cells[0].AddParagraph();
        heading.Format.Font.Bold = true;
        heading.AddText($"{r.SignatureNumber} · {r.SubjectDisplay}");

        var body = table.AddRow();
        body.Height = Unit.FromCentimeter(2.4);
        if (row.Png is { Length: > 0 } png)
        {
            var image = body.Cells[0].AddImage("base64:" + Convert.ToBase64String(png));
            image.LockAspectRatio = true;
            image.Width = Unit.FromCentimeter(4.8);
        }
        else
        {
            body.Cells[0].AddParagraph().AddText("Görsel imza yok · yalnız ad soyad");
        }

        var details = body.Cells[1].AddParagraph();
        Line(details, "İmzalayan", $"{r.SignerNameAtSign} ({r.SignerLogin})");
        Line(details, "İmza anlamı", $"{r.MeaningCode} — {r.MeaningTextAtSign}");
        Line(details, "Tarih-saat", Utc(r.SignedAtUtc));
        Line(details, "Kayıt", $"{r.Module} · {r.SubjectType} · {r.SubjectId}");
        Line(details, "Şirket", ESignatureExportText.Company(r)); // VIS1: a tenant-level record says so
        Line(details, "Doğrulama", row.VerificationStatus);
        Line(details, "Doğrulama özeti", r.SignatureDigest, last: true);

        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(6);
    }

    private static void Line(Paragraph paragraph, string label, string value, bool last = false)
    {
        paragraph.AddFormattedText(label + ": ", TextFormat.Bold);
        paragraph.AddText(value);
        if (!last)
        {
            paragraph.AddLineBreak();
        }
    }

    private static string Utc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
}

/// <summary>
/// The one font family the e-signature PDF uses, served from the package's embedded Segoe WP bytes. Installed once per
/// process; if another component already installed a resolver, that resolver is left alone and asked first.
/// </summary>
public sealed class ESignaturePdfFontResolver : IFontResolver
{
    public const string FamilyName = "DitenESignSans";

    private const string Regular = "DitenESignSans-Regular";
    private const string Bold = "DitenESignSans-Bold";
    private static readonly object Gate = new();
    private static bool _installed;

    public static void EnsureInstalled()
    {
        if (_installed)
        {
            return;
        }

        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new ESignaturePdfFontResolver();
            }
            else
            {
                GlobalFontSettings.FallbackFontResolver ??= new ESignaturePdfFontResolver();
            }

            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase)
            ? new FontResolverInfo(bold ? Bold : Regular)
            : null;

    public byte[]? GetFont(string faceName) => faceName switch
    {
        Regular => FontDataHelper.SegoeWP,
        Bold => FontDataHelper.SegoeWPBold,
        _ => null
    };
}
