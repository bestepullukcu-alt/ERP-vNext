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
/// model's language. A value the model does not carry is not printed — no label, no "—". Arabic is right-aligned.
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

    public PreOrderRenderedDocument Render(PreOrderDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var page = new Page(model.Language);
        var (document, section) = MigraDocPdf.Create(model.OrderCode, page.Word("Title_" + model.Copy));
        page.Title(section, page.Word("Title_" + model.Copy));
        page.Field(section, "OrderNo", model.OrderCode);
        page.Field(section, "OrderDate", page.Date(model.OrderDate));
        page.Field(section, "Delivery", page.Date(model.DeliveryDate));
        page.Field(section, "Seller", model.SellerName);
        page.Field(section, "Depot", model.DepotName);
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

        page.Field(section, "SignedBy", model.SignerName is null ? null : $"{model.SignerName} · {page.Moment(model.SignedAt)}".TrimEnd(' ', '·'));
        if (model.AutoApproved)
        {
            page.Line(section, page.Word("AutoApproved"));
        }
        else
        {
            page.Field(section, "ApprovedBy", model.ApprovedBy is null ? null : $"{model.ApprovedBy} · {page.Moment(model.ApprovedAt)}".TrimEnd(' ', '·'));
        }

        page.Field(section, "Note", model.Note);
        page.Line(section, page.Word("Disclaimer"), bold: true);
        if (model.TransmissionAttempt is { } attempt)
        {
            page.Line(section, string.Format(page.Culture, page.Word("Trace"), attempt));
        }

        return new PreOrderRenderedDocument(MigraDocPdf.ToBytes(document), $"{MigraDocPdf.Sanitize(model.OrderCode, "pre-order")}-{model.Copy}.pdf", page.Text);
    }

    /// <summary>The line table: a column is printed only when some line has a value for it. The template decides nothing
    /// about discounts: the depot copy's model carries none (PreOrderDocumentRules.Build — the ONE place of that rule).</summary>
    private static void Lines(Page page, Section section, PreOrderDocumentModel model)
    {
        var columns = new List<(string Key, Func<PreOrderDocumentLine, string?> Value, string Width)>
        {
            ("ColProduct", l => l.Form is null ? l.Product : $"{l.Product} · {l.Form}", "5cm")
        };
        void Optional(string key, Func<PreOrderDocumentLine, string?> value, string width)
        {
            if (model.Lines.Any(l => value(l) is not null))
            {
                columns.Add((key, value, width));
            }
        }

        Optional("ColBarcode", l => l.Barcode, "2.6cm");
        columns.Add(("ColQty", l => l.Quantity.ToString(page.Culture), "1.2cm"));
        Optional("ColMf", l => l.Mf, "1.3cm");
        Optional("ColFree", l => l.FreeQuantity > 0 ? l.FreeQuantity.ToString(page.Culture) : null, "1.3cm");
        Optional("ColDiscount", l => l.DiscountPct is { } d ? page.Pct(d) : null, "1.6cm");

        if (model.Priced)
        {
            Optional("ColUnitPrice", l => l.UnitPrice is { } p ? p.ToString("N2", page.Culture) : null, "2cm");
            Optional("ColVat", l => l.VatRate is { } v ? page.Pct(v) : null, "1.3cm");
            Optional("ColLineTotal", l => l.NetAmount is { } n ? n.ToString("N2", page.Culture) : null, "2.2cm");
        }

        if (page.RightToLeft)
        {
            columns.Reverse();
        }

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
                row.Cells[i].AddParagraph(page.Text1(columns[i].Value(line) ?? string.Empty));
                row.Cells[i].Format.Alignment = page.Alignment;
            }
        }
    }

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

        public string? Moment(DateTimeOffset? at) => at?.UtcDateTime.ToString("g", Culture);

        public string Money(decimal amount, string? currency) => $"{amount.ToString("N2", Culture)} {currency}".Trim();

        public string Pct(decimal value) => value.ToString("0.#", Culture) + " %";
    }
}
