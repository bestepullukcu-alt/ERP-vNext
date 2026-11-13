namespace Diten.CrmService.Application.Features.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §14: the <c>commercial.*</c> keys (PKS-001, three lowercase-dotted segments). DEFINITION ONLY:
/// the role seed / grant is F-RBAC (W6-CFG). The in-process contracts (§4.6.1) check no key — their caller guards itself.
/// </summary>
public static class PricingPermissions
{
    public const string PriceListsRead = "commercial.price-lists.read";
    public const string PriceListsCreate = "commercial.price-lists.create";
    public const string PriceListsUpdate = "commercial.price-lists.update";
    public const string PriceListsImport = "commercial.price-lists.import";
    public const string PriceListsSubmit = "commercial.price-lists.submit";
    public const string PriceListsPublish = "commercial.price-lists.publish";
    public const string PriceListsExport = "commercial.price-lists.export";

    public const string PricingRulesRead = "commercial.pricing-rules.read";
    public const string PricingRulesCreate = "commercial.pricing-rules.create";
    public const string PricingRulesUpdate = "commercial.pricing-rules.update";
    public const string PricingRulesSubmit = "commercial.pricing-rules.submit";
    public const string PricingRulesPublish = "commercial.pricing-rules.publish";

    public const string FxRatesRead = "commercial.fx-rates.read";
    public const string FxRatesCreate = "commercial.fx-rates.create";
    public const string FxRatesUpdate = "commercial.fx-rates.update";
    public const string FxRatesImport = "commercial.fx-rates.import";

    /// <summary>Every key, in §14 order — the manifest declares exactly these.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        PriceListsRead, PriceListsCreate, PriceListsUpdate, PriceListsImport, PriceListsSubmit, PriceListsPublish, PriceListsExport,
        PricingRulesRead, PricingRulesCreate, PricingRulesUpdate, PricingRulesSubmit, PricingRulesPublish,
        FxRatesRead, FxRatesCreate, FxRatesUpdate, FxRatesImport
    };
}
