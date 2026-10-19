using System.Globalization;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.WPFonts;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// WP-0176-F5 — the Kanıt Paketi PDF (PDFsharp-MigraDoc 6.1.1, MIT — CT-R14; the MOD-0022 <c>MigraDocESignaturePdfRenderer</c> pattern, a
/// separate copy in this service: Platform code is not shared or touched).
/// <list type="bullet">
/// <item>Fonts only from the package's embedded Segoe WP bytes through <see cref="ExpiryEvidencePdfFontResolver"/> — no operating-system
/// font lookup, identical on Windows and Linux; the font is embedded (subset, Unicode), so "İ ş ğ ı ç ö ü" print and extract as such
/// (K3 sabotage (h): the resolver stops serving the font → the Turkish-text test is red).</item>
/// <item>Every value is a literal text run (never MigraDoc DDL / RTF / HTML): record content cannot inject markup. No links, no scripts.</item>
/// <item>The four sections of the preview, in its order; the footer of every page names the company, the period, the generation time and
/// "page n / m". WP-MVP1-FIX-LIVE2: words from <see cref="ExpiryEvidenceText"/> (the service resx; shared keys = the screen's) in tr / en / fr /
/// es / ru (the font's glyphs, measured); a zh / ar selection is drawn in English with a note (FU-0176-PDF-L10N). Names beside the ids
/// (company, product, warehouse, signer, policy-change user); unreadable → the id + "(okunamadı)".</item>
/// <item>A render carries a creation time and a random document id: two renders are not byte-identical, which is why the export stores
/// THE bytes it hashed (the same key answers the same file).</item>
/// </list>
/// </summary>
public sealed class ExpiryEvidencePdfRenderer : IExpiryEvidencePdfRenderer
{
    private const string FontFamily = ExpiryEvidencePdfFontResolver.FamilyName;

    public byte[] Render(ExpiryEvidenceDocument d)
    {
        ExpiryEvidencePdfFontResolver.EnsureInstalled();
        var lang = d.Language;
        string P(string key) => ExpiryEvidenceText.Of("ExpiryPdf." + key, lang);
        string S(string key) => ExpiryEvidenceText.Of(key, lang);
        string Type(string code) => ExpiryEvidenceText.Value("Expiry.Type.", code, lang);
        string Status(string code) => ExpiryEvidenceText.Value("Expiry.Status.", code, lang);

        var pdf = new Document();
        pdf.Info.Title = $"{P("Title")} {Day(d.From)} – {Day(d.To)}";
        pdf.Info.Author = "Diten Inventory Service (MOD-0176)";
        var normal = pdf.Styles[StyleNames.Normal]!;
        normal.Font.Name = FontFamily;
        normal.Font.Size = 8;

        var section = pdf.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 7;
        footer.AddText($"{P("Company")}: {d.Company} · {P("Period")}: {Day(d.From)} – {Day(d.To)} · {P("Generated")}: {Utc(d.GeneratedAtUtc)} · {P("Page")} ");
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
            note.AddText(P("EnglishFallback"));
            note.AddLineBreak();
            note.AddText(ExpiryEvidenceText.Format("ExpiryPdf.RequestedLanguage", lang, d.RequestedLanguage));
        }

        var meta = section.AddParagraph();
        meta.Format.SpaceAfter = Unit.FromPoint(8);
        meta.AddText($"{P("Period")}: {Day(d.From)} – {Day(d.To)} ({d.Preset}) · {P("Company")}: {d.Company}");
        meta.AddLineBreak();
        var warehouses = d.WarehouseTexts is { Count: > 0 } texts ? texts : d.WarehouseIds;
        meta.AddText($"{P("Warehouses")}: {(warehouses.Count == 0 ? P("All") : string.Join(", ", warehouses))} · {P("Types")}: "
                     + (d.Types.Count == 0 ? P("All") : string.Join(", ", d.Types.Select(Type))));
        meta.AddLineBreak();
        meta.AddText($"{P("Generated")}: {Utc(d.GeneratedAtUtc)}");

        Heading(section, "1. " + S("Expiry.Evidence.Summary"));
        var k = d.Summary;
        Table(section, [8, 6], [P("Kpi"), P("Value")],
        [
            [P("Compliance"), k.FefoCompliancePct is { } pct ? "%" + pct.ToString("0.0", CultureInfo.InvariantCulture) : "—"],
            [P("Issues"), $"{k.FefoIssues} ({P("Bypassed")}: {k.FefoBypasses})"],
            [P("Critical"), $"{k.Critical.Lots} · {P("Available")} {Q(k.Critical.Available)} · {P("Held")} {Q(k.Critical.Held)}"],
            [P("Approaching"), $"{k.Approaching.Lots} · {P("Available")} {Q(k.Approaching.Available)} · {P("Held")} {Q(k.Approaching.Held)}"],
            [P("Expired"), $"{k.ExpiredOnHand.Lots} · {Q(k.ExpiredOnHand.Quantity)}"],
            [P("Open"), $"{k.OpenExceptions} ({P("Late")}: {k.LateExceptions})"],
            [P("AvgResolution"), k.AvgResolutionHours is { } h ? h.ToString("0.0", CultureInfo.InvariantCulture) + " " + P("Hours") : "—"]
        ]);

        Heading(section, $"2. {S("Expiry.Evidence.Exceptions")} ({d.Exceptions.Count})");
        if (d.Exceptions.Count == 0) Empty(section, P("None"));
        else
            Table(section, [2.2, 2.4, 3.6, 2.0, 2.6, 2.4, 1.8, 2.2, 3.4, 3.4],
                [P("No"), P("Type"), P("Item"), P("Lot"), P("Warehouse"), P("Opened"), P("Status"), P("Resolution"), P("Reason"), P("Signature")],
                d.Exceptions.Select(r => new[] { r.Number, Type(r.Type), r.ItemText ?? r.ItemId, r.LotNumber, r.WarehouseName ?? r.Warehouse ?? "—", r.OpenedAtUtc,
                    Status(r.Status), r.Resolution is null ? "—" : ExpiryEvidenceText.Value("Expiry.Resolution.", r.Resolution, lang), r.ResolutionReason ?? "—",
                    Signature(r) }).ToList());

        Heading(section, $"3. {S("Expiry.Evidence.PolicyChanges")} ({d.PolicyChanges.Count})");
        if (d.PolicyChanges.Count == 0) Empty(section, P("None"));
        else
            Table(section, [3.4, 3.0, 3.0, 9.0, 4.0, 3.6], [P("Field"), P("Old"), P("New"), P("Reason"), P("By"), P("When")],
                d.PolicyChanges.Select((p, i) => new[]
                {
                    ExpiryEvidenceText.Value("Expiry.Policy.Field.", p.Field, lang), Mode(p.OldValue, lang), Mode(p.NewValue, lang), p.Reason,
                    d.PolicyChangedByNames is { } names && i < names.Count ? names[i] : p.ChangedBy.UserId, p.ChangedAtUtc
                }).ToList());

        Heading(section, $"4. {S("Expiry.Evidence.Bypasses")} ({d.Bypasses.Count})");
        if (d.Bypasses.Count == 0) Empty(section, P("None"));
        else
            Table(section, [2.4, 3.6, 2.2, 2.6, 2.8, 2.6, 5.6, 2.0, 2.2], [P("No"), P("Item"), P("Lot"), P("Warehouse"), P("Opened"), P("FirstEligible"), P("Reason"),
                    P("Status"), P("Signature")],
                d.Bypasses.Select(r => new[] { r.Number, r.ItemText ?? r.ItemId, r.LotNumber, r.WarehouseName ?? r.Warehouse ?? "—", r.OpenedAtUtc,
                    r.FirstEligibleLot ?? "—", r.BypassReason ?? "—", Status(r.Status), Signature(r) }).ToList());

        var renderer = new PdfDocumentRenderer { Document = pdf };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>The signature cell: the signer's name at signing (or the id + "(ad okunamadı)") and the signature id.</summary>
    private static string Signature(ExpiryEvidenceCaseRow r)
        => r.SignatureId is null ? "—"
            : r.SignerName is { } name ? (name.Contains(r.SignatureId, StringComparison.Ordinal) ? name : $"{name} · {r.SignatureId}")
            : r.SignatureId;

    /// <summary>A policy value: a mode code worded by the screen's key, any other value (days, hours) as stored.</summary>
    private static string Mode(string? value, string lang)
        => value is null ? "—" : ExpiryEvidenceText.Find("Expiry.Policy.Mode." + value, lang) ?? value;

    private static void Heading(Section section, string text)
    {
        var p = section.AddParagraph();
        p.Format.Font.Size = 10;
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = Unit.FromPoint(8);
        p.Format.SpaceAfter = Unit.FromPoint(4);
        p.AddText(text);
    }

    private static void Empty(Section section, string text) => section.AddParagraph().AddText(text);

    private static void Table(Section section, double[] widthsCm, string[] header, IReadOnlyList<string[]> rows)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.Borders.Color = Colors.Gray;
        foreach (var w in widthsCm) table.AddColumn(Unit.FromCentimeter(w));
        var head = table.AddRow();
        head.HeadingFormat = true;
        head.Shading.Color = Colors.WhiteSmoke;
        for (var i = 0; i < header.Length; i++)
        {
            var p = head.Cells[i].AddParagraph();
            p.Format.Font.Bold = true;
            p.AddText(header[i]);
        }

        foreach (var values in rows)
        {
            var row = table.AddRow();
            for (var i = 0; i < values.Length; i++) row.Cells[i].AddParagraph().AddText(values[i]);
        }
    }

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

    private static string Q(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>
/// The one font family of the Kanıt Paketi PDF, served from the package's embedded Segoe WP bytes (Latin incl. Turkish, Cyrillic). Installed
/// once per process; if another component already installed a resolver, that one is left alone and this one becomes the fallback.
/// </summary>
public sealed class ExpiryEvidencePdfFontResolver : IFontResolver
{
    public const string FamilyName = "DitenExpirySans";

    private const string Regular = "DitenExpirySans-Regular";
    private const string Bold = "DitenExpirySans-Bold";
    private static readonly object Gate = new();
    private static bool _installed;

    public static void EnsureInstalled()
    {
        if (_installed) return;
        lock (Gate)
        {
            if (_installed) return;
            if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new ExpiryEvidencePdfFontResolver();
            else GlobalFontSettings.FallbackFontResolver ??= new ExpiryEvidencePdfFontResolver();
            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        => string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase) ? new FontResolverInfo(bold ? Bold : Regular) : null;

    public byte[]? GetFont(string faceName) => faceName switch
    {
        Regular => FontDataHelper.SegoeWP,
        Bold => FontDataHelper.SegoeWPBold,
        _ => null
    };
}
