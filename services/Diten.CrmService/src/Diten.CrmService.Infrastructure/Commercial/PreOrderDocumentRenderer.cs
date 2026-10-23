using System.Globalization;
using System.Resources;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Infrastructure.ContentComposition.Rendering;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace Diten.CrmService.Infrastructure.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-c2 — THE depot document template (MOD-0168 §4.7), PDFsharp-MigraDoc 6.1.1: one A4 page per copy, for the
/// transmission PDF and the preview alike (two templates are forbidden). Everything it prints comes from
/// <see cref="PreOrderDocumentModel"/>; every word from <c>Resources/PreOrderDocument.*.resx</c> (seven languages) in the
/// model's language. A header value the model does not carry is not printed — no label, no "—". Arabic is right-aligned.
/// <para>W6-FIX-c (mockup E): the seller's letterhead (name, address, tax id — V-13); the eight line columns, always there,
/// a missing cell "—" (V-14); list subtotal · discount · net · VAT · total (V-15); delivery date, currency, depot e-mail
/// (V-16); times in the selling company's country zone, named (V-17). The table fits the page: its column widths add up
/// to at most the printable width (<see cref="ContentWidthCm"/>) and a cell wraps inside its column.</para>
/// <para>Every printed string is also returned in reading order (<see cref="PreOrderRenderedDocument.Text"/>): PDFsharp
/// writes glyph ids into the file, so this is what a test reads to see what the page says.</para>
/// <para>Limits of PDFsharp 6.1 (reported, not hidden): no Arabic letter joining / bidi shaping (the words are right-aligned
/// in logical order) and no glyph fallback — a CJK text needs a CJK font on the host (Windows: Microsoft YaHei).</para>
/// </summary>
public sealed class PreOrderDocumentRenderer : IPreOrderDocumentRenderer
{
    private static readonly ResourceManager Words =
        new("Diten.CrmService.Infrastructure.Resources.PreOrderDocument", typeof(PreOrderDocumentRenderer).Assembly);

    /// <summary>The seven tenant languages the template is written in.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" };

    /// <summary>The printed size of the signature image (the bytes are embedded as stored).</summary>
    private const string SignatureWidth = "5cm";

    /// <summary>A4 is 21 cm wide; the side margins are 1.8 cm each.</summary>
    public const double PageWidthCm = 21.0;

    public const double SideMarginCm = 1.8;

    public static double ContentWidthCm => PageWidthCm - 2 * SideMarginCm;

    /// <summary>A line cell without a value (V-14).</summary>
    public const string Empty = "—";

    /// <summary>The line columns (V-14) and their widths in cm: the price columns only in the priced mode (the quantity mode
    /// prints no amount, MOD-0168 §4.2). Public so a test can hold the widths against the page.</summary>
    public static IReadOnlyList<(string Key, double WidthCm)> LineColumns(bool priced)
    {
        var columns = new List<(string, double)> { ("ColProduct", 4.0), ("ColBarcode", 2.5), ("ColQty", 1.1), ("ColMf", 1.7), ("ColDiscount", 1.3) };
        if (priced)
        {
            columns.AddRange([("ColUnitPrice", 2.0), ("ColVat", 1.2), ("ColLineTotal", 2.3)]);
        }

        return columns;
    }

    public PreOrderRenderedDocument Render(PreOrderDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var page = new Page(model.Language);
        var (document, section) = MigraDocPdf.Create(model.OrderCode, page.Word("Title_" + model.Copy));
        section.PageSetup.LeftMargin = Unit.FromCentimeter(SideMarginCm);
        section.PageSetup.RightMargin = Unit.FromCentimeter(SideMarginCm);
        page.Title(section, page.Word("Title_" + model.Copy));

        // V-13 the letterhead: the selling company
        page.Line(section, model.SellerName, bold: true);
        page.Line(section, model.SellerAddress);
        page.Field(section, "SellerTaxId", model.SellerTaxNumber);

        page.Field(section, "OrderNo", model.OrderCode);
        page.Field(section, "OrderDate", page.Date(model.OrderDate));
        page.Field(section, "Delivery", page.Date(model.DeliveryDate));
        page.Field(section, "Currency", model.Priced ? model.Currency : null);
        page.Field(section, "Depot", model.DepotName);
        page.Field(section, "DepotEmail", model.DepotEmail);
        page.Field(section, "Rep", model.RepName);

        page.Heading(section, page.Word("Pharmacy"));
        page.Line(section, model.PharmacyName);
        page.Field(section, "Address", model.PharmacyAddress);
        page.Field(section, "Gln", model.PharmacyGln);
        page.Field(section, "PharmacyCode", model.PharmacyCode);
        page.Field(section, "TaxNumber", model.PharmacyTaxNumber);

        Lines(page, section, model);

        if (model.Priced && model.Totals is { } totals)
        {
            page.Field(section, "ListSubtotal", page.Money(totals.ListSubtotal, model.Currency));
            page.Field(section, "DiscountAmount", page.Money(totals.DiscountAmount, model.Currency));
            page.Field(section, "TotalNet", page.Money(totals.Net, model.Currency));
            page.Field(section, "TotalVat", page.Money(totals.Vat, model.Currency));
            page.Field(section, "TotalGross", page.Money(totals.Gross, model.Currency));
            if (totals.EffectiveDiscountPct is { } effective)
            {
                page.Field(section, "EffectiveDiscount", page.Pct(effective));
            }

            if (model.Fx is { } fx && fx.LocalGross is { } local)
            {
                page.Field(section, "LocalEquivalent", string.Format(page.Culture, page.Word("FxLine"),
                    local.ToString("N2", page.Culture), fx.LocalCurrency, fx.Rate.ToString("0.####", page.Culture), page.Date(fx.RateDate), fx.Source));
            }
        }
        else
        {
            page.Line(section, page.Word("QuantityMode"));
        }

        page.Heading(section, page.Word("Signature"));
        if (model.SignatureImage is { Length: > 0 } image)
        {
            var picture = section.AddImage("base64:" + Convert.ToBase64String(image));
            picture.Width = SignatureWidth;
            picture.LockAspectRatio = true;
        }

        page.Field(section, "SignedBy", model.SignerName is null ? null : $"{model.SignerName} · {page.Moment(model.SignedAt, model.TimeZone)}".TrimEnd(' ', '·'));
        if (model.AutoApproved)
        {
            page.Line(section, page.Word("AutoApproved"));
        }
        else
        {
            page.Field(section, "ApprovedBy", model.ApprovedBy is null ? null : $"{model.ApprovedBy} · {page.Moment(model.ApprovedAt, model.TimeZone)}".TrimEnd(' ', '·'));
        }

        page.Field(section, "Note", model.Note);
        page.Line(section, page.Word("Disclaimer"), bold: true);
        if (model.TransmissionAttempt is { } attempt)
        {
            page.Line(section, string.Format(page.Culture, page.Word("Trace"), attempt));
        }

        return new PreOrderRenderedDocument(MigraDocPdf.ToBytes(document), $"{MigraDocPdf.Sanitize(model.OrderCode, "pre-order")}-{model.Copy}.pdf", page.Text);
    }

    /// <summary>The line table (V-14): the columns of <see cref="LineColumns"/>, always there; a cell without a value is
    /// "—". The MF cell carries the free boxes ("10+2 (+6)"). The template decides nothing about discounts: what a copy
    /// shows is in its model (PreOrderDocumentRules.Build — the ONE place of that rule).</summary>
    private static void Lines(Page page, Section section, PreOrderDocumentModel model)
    {
        var values = new Dictionary<string, Func<PreOrderDocumentLine, string?>>
        {
            ["ColProduct"] = l => l.Form is null ? l.Product : $"{l.Product} · {l.Form}",
            ["ColBarcode"] = l => l.Barcode,
            ["ColQty"] = l => l.Quantity.ToString(page.Culture),
            ["ColMf"] = l => l.Mf is null ? null : l.FreeQuantity > 0 ? $"{l.Mf} (+{l.FreeQuantity.ToString(page.Culture)})" : l.Mf,
            ["ColDiscount"] = l => l.DiscountPct is { } d and > 0m ? page.Pct(d) : null,
            ["ColUnitPrice"] = l => l.UnitPrice is { } p ? p.ToString("N2", page.Culture) : null,
            ["ColVat"] = l => l.VatRate is { } v ? page.Pct(v) : null,
            ["ColLineTotal"] = l => l.NetAmount is { } n ? n.ToString("N2", page.Culture) : null
        };
        var columns = LineColumns(model.Priced).Select(c => (c.Key, Value: values[c.Key], Width: Unit.FromCentimeter(c.WidthCm))).ToList();

        if (page.RightToLeft)
        {
            columns.Reverse();
        }

        Gap(section);
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Format.Font.Size = 8;
        foreach (var column in columns)
        {
            table.AddColumn(column.Width);
        }

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        for (var i = 0; i < columns.Count; i++)
        {
            header.Cells[i].AddParagraph(page.Text1(page.Word(columns[i].Key)));
            header.Cells[i].Format.Alignment = page.Alignment;
        }

        foreach (var line in model.Lines)
        {
            var row = table.AddRow();
            for (var i = 0; i < columns.Count; i++)
            {
                row.Cells[i].AddParagraph(page.Text1(columns[i].Value(line) is { Length: > 0 } value ? value : Empty));
                row.Cells[i].Format.Alignment = page.Alignment;
            }
        }

        Gap(section);
    }

    /// <summary>A little air above and below the table (an empty paragraph prints no text).</summary>
    private static void Gap(Section section) => section.AddParagraph().Format.Font.Size = 4;

    /// <summary>One page's language, culture, alignment and the printed text (in reading order).</summary>
    private sealed class Page
    {
        private readonly List<string> _text = new();

        public Page(string language)
        {
            Language = SupportedLanguages.Contains(language) ? language : "en";
            Culture = CultureInfo.GetCultureInfo(Language);
        }

        public string Language { get; }
        public CultureInfo Culture { get; }
        public bool RightToLeft => Language == "ar";
        public ParagraphAlignment Alignment => RightToLeft ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        public IReadOnlyList<string> Text => _text;

        public string Word(string key) => Words.GetString(key, Culture) ?? key;

        public string Text1(string value)
        {
            if (value.Length > 0)
            {
                _text.Add(value);
            }

            return value;
        }

        public void Title(Section section, string text)
        {
            MigraDocPdf.AddTitle(section, Text1(text));
            section.LastParagraph!.Format.Alignment = Alignment;
        }

        public void Heading(Section section, string text)
        {
            MigraDocPdf.AddHeading(section, Text1(text));
            section.LastParagraph!.Format.Alignment = Alignment;
        }

        /// <summary>A label and its value; nothing at all when the value is missing.</summary>
        public void Field(Section section, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var label = Word(key);
            var p = section.AddParagraph();
            p.Format.Alignment = Alignment;
            p.AddFormattedText($"{label}: ", TextFormat.Bold);
            p.AddText(value.Trim());
            _text.Add($"{label}: {value.Trim()}");
        }

        public void Line(Section section, string? text, bool bold = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var p = section.AddParagraph();
            p.Format.Alignment = Alignment;
            p.Format.SpaceBefore = "4pt";
            if (bold)
            {
                p.AddFormattedText(text.Trim(), TextFormat.Bold);
            }
            else
            {
                p.AddText(text.Trim());
            }

            _text.Add(text.Trim());
        }

        public string? Date(DateOnly? date) => date?.ToString("d", Culture);

        /// <summary>V-17 — the moment as the model gives it (the selling company's country wall clock) and the zone's name.</summary>
        public string? Moment(DateTimeOffset? at, string zone) => at is { } moment ? $"{moment.DateTime.ToString("g", Culture)} ({zone})" : null;

        public string Money(decimal amount, string? currency) => $"{amount.ToString("N2", Culture)} {currency}".Trim();

        public string Pct(decimal value) => value.ToString("0.#", Culture) + " %";
    }
}
