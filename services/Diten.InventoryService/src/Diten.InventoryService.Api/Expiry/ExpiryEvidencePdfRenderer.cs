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
/// "page n / m". Labels in Turkish or English (the reader's language; other languages → English — evidence open point).</item>
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
        var l = Labels.For(d.Language);

        var pdf = new Document();
        pdf.Info.Title = $"{l.Title} {Day(d.From)} – {Day(d.To)}";
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
        footer.AddText($"{l.Company}: {d.LegalEntityId} · {l.Period}: {Day(d.From)} – {Day(d.To)} · {l.Generated}: {Utc(d.GeneratedAtUtc)} · {l.Page} ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        var title = section.AddParagraph();
        title.Format.Font.Size = 14;
        title.Format.Font.Bold = true;
        title.AddText(l.Title);

        var meta = section.AddParagraph();
        meta.Format.SpaceAfter = Unit.FromPoint(8);
        meta.AddText($"{l.Period}: {Day(d.From)} – {Day(d.To)} ({d.Preset}) · {l.Company}: {d.LegalEntityId}");
        meta.AddLineBreak();
        meta.AddText($"{l.Warehouses}: {(d.WarehouseIds.Count == 0 ? l.All : string.Join(", ", d.WarehouseIds))} · {l.Types}: "
                     + (d.Types.Count == 0 ? l.All : string.Join(", ", d.Types.Select(l.Type))));
        meta.AddLineBreak();
        meta.AddText($"{l.Generated}: {Utc(d.GeneratedAtUtc)}");

        Heading(section, "1. " + l.Summary);
        var k = d.Summary;
        Table(section, [8, 6], [l.Kpi, l.Value],
        [
            [l.Compliance, k.FefoCompliancePct is { } pct ? "%" + pct.ToString("0.0", CultureInfo.InvariantCulture) : "—"],
            [l.Issues, $"{k.FefoIssues} ({l.Bypasses}: {k.FefoBypasses})"],
            [l.Critical, $"{k.Critical.Lots} · {l.Available} {Q(k.Critical.Available)} · {l.Held} {Q(k.Critical.Held)}"],
            [l.Approaching, $"{k.Approaching.Lots} · {l.Available} {Q(k.Approaching.Available)} · {l.Held} {Q(k.Approaching.Held)}"],
            [l.Expired, $"{k.ExpiredOnHand.Lots} · {Q(k.ExpiredOnHand.Quantity)}"],
            [l.Open, $"{k.OpenExceptions} ({l.Late}: {k.LateExceptions})"],
            [l.AvgResolution, k.AvgResolutionHours is { } h ? h.ToString("0.0", CultureInfo.InvariantCulture) + " h" : "—"]
        ]);

        Heading(section, $"2. {l.Exceptions} ({d.Exceptions.Count})");
        if (d.Exceptions.Count == 0) Empty(section, l.None);
        else
            Table(section, [2.4, 2.6, 2.6, 2.0, 2.8, 1.8, 2.4, 5.4, 4.0], [l.No, l.TypeColumn, l.Lot, l.Warehouse, l.Opened, l.StatusColumn, l.Resolution, l.Reason, l.Signature],
                d.Exceptions.Select(r => new[] { r.Number, l.Type(r.Type), r.LotNumber, r.Warehouse ?? "—", r.OpenedAtUtc, l.Status(r.Status),
                    r.Resolution is null ? "—" : l.Resolutions(r.Resolution), r.ResolutionReason ?? "—", r.SignatureId ?? "—" }).ToList());

        Heading(section, $"3. {l.PolicyChanges} ({d.PolicyChanges.Count})");
        if (d.PolicyChanges.Count == 0) Empty(section, l.None);
        else
            Table(section, [3.4, 3.0, 3.0, 9.0, 4.0, 3.6], [l.Field, l.Old, l.New, l.Reason, l.By, l.When],
                d.PolicyChanges.Select(p => new[] { p.Field, p.OldValue ?? "—", p.NewValue ?? "—", p.Reason, p.ChangedBy.UserId, p.ChangedAtUtc }).ToList());

        Heading(section, $"4. {l.FefoBypasses} ({d.Bypasses.Count})");
        if (d.Bypasses.Count == 0) Empty(section, l.None);
        else
            Table(section, [2.4, 2.8, 2.2, 3.0, 2.8, 7.2, 2.4, 3.2], [l.No, l.Lot, l.Warehouse, l.Opened, l.FirstEligible, l.Reason, l.StatusColumn, l.Signature],
                d.Bypasses.Select(r => new[] { r.Number, r.LotNumber, r.Warehouse ?? "—", r.OpenedAtUtc, r.FirstEligibleLot ?? "—", r.BypassReason ?? "—",
                    l.Status(r.Status), r.SignatureId ?? "—" }).ToList());

        var renderer = new PdfDocumentRenderer { Document = pdf };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

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

    /// <summary>The PDF's own words (tr / en). The wire codes stay codes in the CSV; the PDF names them.</summary>
    private sealed record Labels(
        string Title, string Company, string Period, string Generated, string Page, string Warehouses, string Types, string All, string Summary,
        string Kpi, string Value, string Compliance, string Issues, string Bypasses, string Critical, string Approaching, string Expired, string Open,
        string Late, string AvgResolution, string Available, string Held, string Exceptions, string PolicyChanges, string FefoBypasses, string None,
        string No, string TypeCol, string Lot, string Warehouse, string Opened, string Status_, string Resolution, string Reason, string Signature,
        string Field, string Old, string New, string By, string When, string FirstEligible,
        IReadOnlyDictionary<string, string> TypeNames, IReadOnlyDictionary<string, string> StatusNames, IReadOnlyDictionary<string, string> ResolutionNames)
    {
        public string Status(string code) => StatusNames.GetValueOrDefault(code, code);

        public string Type(string code) => TypeNames.GetValueOrDefault(code, code);

        public string Resolutions(string code) => ResolutionNames.GetValueOrDefault(code, code);

        public static Labels For(string language) => language == "tr" ? Tr : En;

        private static readonly Labels Tr = new(
            "FEFO / Son Kullanma — Kanıt Paketi", "Şirket", "Dönem", "Oluşturulma", "Sayfa", "Depolar", "Türler", "Tümü", "Özet göstergeler",
            "Gösterge", "Değer", "FEFO uyum oranı", "FEFO kapsamındaki çıkışlar", "FEFO dışı", "Kritik (kullanılabilir stok)", "Yaklaşıyor (kullanılabilir stok)",
            "Süresi dolmuş stok", "Açık istisnalar", "SLA'yı geçen", "Ortalama çözüm süresi", "Kullanılabilir", "Bekleyen", "Dönemin istisnaları",
            "Dönemdeki politika değişiklikleri", "FEFO dışı çıkışlar", "Kayıt yok.", "No", "Tür", "Lot", "Depo", "Açılış (UTC)", "Durum", "Çözüm",
            "Gerekçe", "İmza kimliği", "Alan", "Eski", "Yeni", "Değiştiren", "Zaman (UTC)", "İlk uygun lot",
            new Dictionary<string, string>
            {
                ["FEFO_BYPASS"] = "FEFO dışı çıkış", ["BELOW_MIN_REMAINING"] = "Min. kalan raf ömrü altı giriş",
                ["EXPIRY_DATE_OVERRIDE"] = "SKT sapması", ["EXPIRED_ON_HAND"] = "Süresi dolmuş stok"
            },
            new Dictionary<string, string>
            {
                ["Open"] = "Açık", ["InReview"] = "İncelemede", ["Accepted"] = "Kabul edildi", ["Fixed"] = "Düzeltildi", ["Closed"] = "Kapandı",
                ["Cancelled"] = "İptal"
            },
            new Dictionary<string, string> { ["AcceptedWithReason"] = "Gerekçeyle kabul", ["Fixed"] = "Düzeltildi", ["Cancelled"] = "İptal" });

        private static readonly Labels En = new(
            "FEFO / Expiry — Evidence Pack", "Company", "Period", "Generated", "Page", "Warehouses", "Types", "All", "Summary indicators",
            "Indicator", "Value", "FEFO compliance", "Issues in FEFO scope", "bypassed", "Critical (available stock)", "Approaching (available stock)",
            "Expired stock on hand", "Open exceptions", "past SLA", "Mean resolution time", "Available", "Held", "Exceptions of the period",
            "Policy changes in the period", "FEFO bypasses", "No records.", "No", "Type", "Lot", "Warehouse", "Opened (UTC)", "Status", "Resolution",
            "Reason", "Signature id", "Field", "Old", "New", "Changed by", "Time (UTC)", "First eligible lot",
            new Dictionary<string, string>
            {
                ["FEFO_BYPASS"] = "FEFO bypass", ["BELOW_MIN_REMAINING"] = "Receipt below minimum remaining shelf life",
                ["EXPIRY_DATE_OVERRIDE"] = "Expiry date override", ["EXPIRED_ON_HAND"] = "Expired stock on hand"
            },
            new Dictionary<string, string>
            {
                ["Open"] = "Open", ["InReview"] = "In review", ["Accepted"] = "Accepted", ["Fixed"] = "Fixed", ["Closed"] = "Closed", ["Cancelled"] = "Cancelled"
            },
            new Dictionary<string, string> { ["AcceptedWithReason"] = "Accepted with reason", ["Fixed"] = "Fixed", ["Cancelled"] = "Cancelled" });

        public string TypeColumn => TypeCol;

        public string StatusColumn => Status_;
    }
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
