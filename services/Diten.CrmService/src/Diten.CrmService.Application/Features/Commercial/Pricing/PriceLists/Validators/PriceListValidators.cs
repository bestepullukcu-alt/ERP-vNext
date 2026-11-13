using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Validators;

/// <summary>MOD-0156 §13.1 — the header of a list.</summary>
public static class PriceListValidator
{
    /// <summary>The first problem of a header, as (code, message); null = valid. <paramref name="create"/> requires every field.</summary>
    public static (string Code, string Message)? Check(PriceListInput? input, bool create)
    {
        if (input is null)
        {
            return (PricingErrors.Invalid, "A body is required.");
        }

        var name = PricingCommon.Clean(input.Name);
        if ((create || input.Name is not null) && (name is null || name.Length > PricingLimits.NameMax))
        {
            return (PricingErrors.Invalid, $"The name is required (≤ {PricingLimits.NameMax} characters).");
        }

        if (create && (input.LegalEntityId is null || input.LegalEntityId == Guid.Empty))
        {
            return (PricingErrors.LegalEntityInvalid, "The selling legal entity is required.");
        }

        if ((create || input.Currency is not null) && !PricingCommon.IsCurrency(PricingCommon.NormalizeCurrency(input.Currency)))
        {
            return (PricingErrors.Invalid, "The currency is required (ISO 4217, three letters).");
        }

        if ((create || input.Kind is not null) && !PricingCommon.In(PricingCommon.Clean(input.Kind)?.ToLowerInvariant(), PriceListKinds.All))
        {
            return (PricingErrors.Invalid, "The kind is official or company.");
        }

        if (create && input.ValidFrom is null)
        {
            return (PricingErrors.Invalid, "The first day (validFrom) is required.");
        }

        return null;
    }

    public static (string Code, string Message)? CheckPeriod(DateOnly validFrom, DateOnly? validTo)
        => validTo is { } to && to < validFrom ? (PricingErrors.Invalid, "validTo cannot be before validFrom.") : null;
}

/// <summary>
/// EK-1 — ONE check for an item, whether typed by hand or imported (the import uses it row by row): at least one tier,
/// every tier ≥ 0 with at most four decimals, VAT 0–100. The SKU checks (exists, not Retired, unique in the list) are the
/// handler's, against MDM and the list — also shared.
/// </summary>
public static class PriceListItemValidator
{
    public static string? CheckPrices(decimal? depot, decimal? pharmacist, decimal? publicPrice, decimal? vat)
    {
        if (depot is null && pharmacist is null && publicPrice is null)
        {
            return "At least one price tier (depot, pharmacist purchase, public) is required.";
        }

        foreach (var price in new[] { depot, pharmacist, publicPrice })
        {
            if (price is { } p && (p < 0m || PricingCommon.Decimals(p) > PricingLimits.PriceDecimals))
            {
                return $"A price cannot be negative and has at most {PricingLimits.PriceDecimals} decimals.";
            }
        }

        if (vat is null || vat < 0m || vat > 100m)
        {
            return "The VAT rate is required (0–100).";
        }

        return null;
    }
}
