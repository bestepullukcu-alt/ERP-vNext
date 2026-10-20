using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Validators;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports.Validators;

/// <summary>MOD-0156 §13.1 — the upload: xlsx / csv, ≤ 5 MB, the mapped columns, the list header (the currency is required,
/// DESIGN-GAP §3).</summary>
public static class UploadPriceListImportValidator
{
    public static (string Code, string Message)? Check(PriceListImportUpload? upload)
    {
        if (upload is null)
        {
            return (PricingErrors.ImportInvalid, "A file and its list header are required.");
        }

        if (PriceListValidator.Check(new PriceListInput(upload.Name ?? upload.FileName, upload.LegalEntityId, upload.Currency, upload.Kind,
                upload.ValidFrom, upload.ValidTo), create: true) is { } header)
        {
            return header;
        }

        if (PriceListValidator.CheckPeriod(upload.ValidFrom!.Value, upload.ValidTo) is { } period)
        {
            return period;
        }

        var extension = Path.GetExtension(upload.FileName ?? string.Empty).ToLowerInvariant();
        if (extension is not (".xlsx" or ".csv"))
        {
            return (PricingErrors.ImportInvalid, "The file is .xlsx or .csv.");
        }

        if (upload.Content is not { Length: > 0 } content || content.Length > PricingLimits.ImportMaxBytes)
        {
            return (PricingErrors.ImportInvalid, "The file is required and at most 5 MB.");
        }

        var columns = upload.Columns;
        if (columns is null
            || (PricingCommon.Clean(columns.Gtin) is null && PricingCommon.Clean(columns.SkuCode) is null)
            || (PricingCommon.Clean(columns.DepotPrice) is null && PricingCommon.Clean(columns.PharmacistPurchasePrice) is null
                && PricingCommon.Clean(columns.PublicPrice) is null)
            || PricingCommon.Clean(columns.VatRate) is null)
        {
            return (PricingErrors.ImportInvalid, "Map the GTIN or the SKU code column, at least one price column and the VAT column.");
        }

        return null;
    }
}
