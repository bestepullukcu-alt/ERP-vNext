using System.Text;
using ClosedXML.Excel;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Queries;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Queries;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.2 import: file → match (code, then GTIN) → preview with the diff against the list in
/// force → convert ONLY the matched rows into a draft (unmatched / Retired / duplicate / bad-price rows are never published),
/// once; the real file parser (csv and xlsx); MDM down is 503 with nothing written; tenant isolation (404).
/// </summary>
public sealed class PriceListImportTests : IDisposable
{
    private const string UroGtin = "8690000000041";
    private readonly PricingWorld _w = new();

    public PriceListImportTests()
        => _w.Skus.ByGtin[UroGtin] = new PricingSku(PricingWorld.Sku("URO"), "Lsku", "LS-URO", "ÜROFLEKS 0,4 mg 30 kapsül", "Active", UroGtin);

    public void Dispose() => _w.Dispose();

    private static readonly PriceListFileColumns Columns = new("Barkod", "Ürün Kodu", null, "Eczacı Alış", null, "KDV");

    private const string Csv =
        "Barkod;Ürün Kodu;Eczacı Alış;KDV\n" +
        ";LS-TUT;430.00;10\n" +          // matched, price changed (412.60 → 430.00)
        ";ls-alm;186.40;10\n" +          // matched by code (case-insensitive), unchanged
        $"{UroGtin};;254.30;10\n" +       // matched by GTIN, unchanged
        ";LS-XYZ;10.00;10\n" +           // unmatched
        ";LS-AL5;90.00;10\n" +           // Retired in MDM
        ";LS-TUT;431.00;10\n" +          // the same SKU twice
        ";LS-GLU;-5;10\n";               // a bad price (GLU then counts as removed from the list in force)

    private PriceListImportUpload Upload(string fileName = "kasim.csv", byte[]? content = null, PriceListFileColumns? columns = null)
        => new("TR Resmî · Kasım", PricingWorld.LeTr, "TRY", "official", _w.Today.AddDays(12), null, fileName,
            content ?? Encoding.UTF8.GetBytes(Csv), columns ?? Columns);

    private static string Code<T>(Response<T> r) => r.Errors?.FirstOrDefault() ?? string.Empty;

    [Fact]
    public async Task I1_the_preview_counts_every_row_status_and_the_diff_against_the_list_in_force()
    {
        var inForce = _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today.AddDays(-19), PricingWorld.TrPrices, 10m);

        var uploaded = await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing);

        Assert.True(uploaded.IsSuccessful, string.Join(" | ", uploaded.Errors ?? []));
        var i = uploaded.Data!;
        Assert.Equal((PriceListImportStatuses.Validated, 7, 3, 1, 1, 1, 1), (i.Status, i.TotalRows, i.Matched, i.Unmatched, i.Retired, i.Duplicate, i.InvalidPrice));
        // the file carries only the pharmacist tier: ALM and URO lose their depot / public prices, which is a change too
        Assert.Equal((inForce.Id, 0, 3, 1), (i.ComparedPriceListId!.Value, i.Added, i.Changed, i.Removed));
        Assert.Equal(64, i.FileSha256.Length);

        var preview = (await _w.Send(new GetPriceListImportQuery(i.Id, null, null, null, null))).Data!;
        var tut = preview.Rows.Single(r => r.RowStatus == PriceListImportRowStatuses.Matched && r.SkuCode == "LS-TUT");
        Assert.Equal((PriceListDiffKinds.Changed, (decimal?)412.60m), (tut.DiffKind, tut.PreviousPharmacistPurchasePrice));
        Assert.Equal("LS-URO", preview.Rows.Single(r => r.SourceGtin == UroGtin).SkuCode);
        Assert.Equal("LS-GLU", preview.Rows.Single(r => r.RowStatus == PriceListImportRowStatuses.Removed).SkuCode);
        var problems = (await _w.Send(new GetPriceListImportQuery(i.Id, "unmatched,retired", null, null, null))).Data!;
        Assert.Equal(new[] { "LS-AL5", "LS-XYZ" }, problems.Rows.Select(r => r.SourceCode!).OrderBy(x => x));
    }

    [Fact]
    public async Task I2_convert_makes_a_draft_from_the_matched_rows_only_and_happens_once()
    {
        var inForce = _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today.AddDays(-19), PricingWorld.TrPrices, 10m);
        var import = (await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing)).Data!;

        var converted = await _w.Send(new ConvertPriceListImportCommand(import.Id), PricingWorld.Marketing);

        Assert.True(converted.IsSuccessful, string.Join(" | ", converted.Errors ?? []));
        var draft = converted.Data!;
        Assert.Equal((PriceListStatuses.Draft, 3, (Guid?)inForce.Id, (Guid?)import.Id), (draft.Status, draft.ItemCount, draft.PreviousPriceListId, draft.SourceImportId));
        var items = (await _w.Send(new GetPriceListItemsQuery(draft.Id, null, null, null, null))).Data!.Items;
        Assert.Equal(new[] { "LS-ALM", "LS-TUT", "LS-URO" }, items.Select(x => x.SkuCode!).OrderBy(x => x));
        Assert.Equal(430.00m, items.Single(x => x.SkuCode == "LS-TUT").PharmacistPurchasePrice);  // the first TUT row, never the duplicate
        Assert.DoesNotContain(_w.Items.Store, x => x.PriceListId == draft.Id && x.SkuId == PricingWorld.Sku("AL5"));

        var again = await _w.Send(new ConvertPriceListImportCommand(import.Id), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.ImportNotOpen, 409), (Code(again), again.StatusCode));
        Assert.Equal(PriceListImportStatuses.Converted, _w.Imports.Store.Single().Status);
        Assert.Equal(1, _w.Lists.Store.Count(l => l.SourceImportId == import.Id));
    }

    [Fact]
    public async Task I3_an_xlsx_file_is_read_by_its_header_names()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Fiyatlar");
        sheet.Cell(1, 1).Value = "KDV";
        sheet.Cell(1, 2).Value = "ürün kodu";                 // the header is matched case- and space-insensitively
        sheet.Cell(1, 3).Value = "Eczacı  Alış";
        sheet.Cell(2, 1).Value = 10;
        sheet.Cell(2, 2).Value = "LS-TUT";
        sheet.Cell(2, 3).Value = 412.60;
        sheet.Cell(4, 1).Value = 10;                          // row 3 is empty and skipped
        sheet.Cell(4, 2).Value = "LS-ALM";
        sheet.Cell(4, 3).Value = 186.40;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var uploaded = await _w.Send(new UploadPriceListImportCommand(Upload("kasim.xlsx", stream.ToArray(), new PriceListFileColumns(null, "Ürün Kodu", null, "Eczacı Alış", null, "KDV"))),
            PricingWorld.Marketing);

        Assert.True(uploaded.IsSuccessful, string.Join(" | ", uploaded.Errors ?? []));
        Assert.Equal((2, 2, (Guid?)null, 2), (uploaded.Data!.TotalRows, uploaded.Data.Matched, uploaded.Data.ComparedPriceListId, uploaded.Data.Added));
    }

    [Fact]
    public async Task I4_a_bad_upload_is_400_and_MDM_down_is_503_with_nothing_written()
    {
        var wrongType = await _w.Send(new UploadPriceListImportCommand(Upload("kasim.pdf")), PricingWorld.Marketing);
        var missingColumn = await _w.Send(new UploadPriceListImportCommand(Upload(columns: Columns with { VatRate = "Vergi" })), PricingWorld.Marketing);
        var noPrice = await _w.Send(new UploadPriceListImportCommand(Upload(columns: Columns with { PharmacistPurchasePrice = null })), PricingWorld.Marketing);
        var headerOnly = await _w.Send(new UploadPriceListImportCommand(Upload(content: Encoding.UTF8.GetBytes("Barkod;Ürün Kodu;Eczacı Alış;KDV\n"))), PricingWorld.Marketing);
        _w.Skus.Down = true;
        var down = await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing);

        Assert.All(new[] { wrongType, missingColumn, noPrice, headerOnly }, r => Assert.Equal((PricingErrors.ImportInvalid, 400), (Code(r), r.StatusCode)));
        Assert.Equal((PricingErrors.MdmUnavailable, 503), (Code(down), down.StatusCode));
        Assert.Empty(_w.Imports.Store);
        Assert.Empty(_w.Imports.Rows);
    }

    [Fact]
    public async Task I5_nothing_matched_cannot_be_converted_and_a_discarded_import_is_closed()
    {
        var none = (await _w.Send(new UploadPriceListImportCommand(Upload(content: Encoding.UTF8.GetBytes("Barkod;Ürün Kodu;Eczacı Alış;KDV\n;LS-XYZ;1;10\n"))),
            PricingWorld.Marketing)).Data!;
        var convertNone = await _w.Send(new ConvertPriceListImportCommand(none.Id), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.ImportNothingMatched, 409), (Code(convertNone), convertNone.StatusCode));

        var import = (await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing)).Data!;
        Assert.Equal(PriceListImportStatuses.Discarded, (await _w.Send(new DiscardPriceListImportCommand(import.Id), PricingWorld.Marketing)).Data!.Status);
        Assert.Equal(409, (await _w.Send(new ConvertPriceListImportCommand(import.Id), PricingWorld.Marketing)).StatusCode);
        Assert.Empty(_w.Lists.Store);
    }

    [Fact]
    public async Task I6_another_tenants_import_is_404()
    {
        var import = (await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing)).Data!;
        _w.Imports.Store.Single().TenantId = PricingWorld.OtherTenant;

        Assert.Equal(404, (await _w.Send(new GetPriceListImportQuery(import.Id, null, null, null, null))).StatusCode);
        Assert.Equal(404, (await _w.Send(new ConvertPriceListImportCommand(import.Id), PricingWorld.Marketing)).StatusCode);
        Assert.Equal(404, (await _w.Send(new DiscardPriceListImportCommand(import.Id), PricingWorld.Marketing)).StatusCode);
    }

    [Fact]
    public async Task I7_upload_convert_and_discard_are_audited_once_each()
    {
        var import = (await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing)).Data!;
        await _w.Send(new GetPriceListImportQuery(import.Id, null, null, null, null));
        await _w.Send(new ConvertPriceListImportCommand(import.Id), PricingWorld.Marketing);
        var second = (await _w.Send(new UploadPriceListImportCommand(Upload()), PricingWorld.Marketing)).Data!;
        await _w.Send(new DiscardPriceListImportCommand(second.Id), PricingWorld.Marketing);

        Assert.Equal(new[] { "UploadPriceListImportCommand", "ConvertPriceListImportCommand", "UploadPriceListImportCommand", "DiscardPriceListImportCommand" },
            _w.Audit.Bodies.Select(b => System.Text.Json.JsonDocument.Parse(b).RootElement.GetProperty("requestType").GetString()));
        Assert.Contains(_w.Log.Store, e => e.EntityType == PricingChangeEntityTypes.PriceList && e.Action == PricingChangeActions.Imported);
    }
}
