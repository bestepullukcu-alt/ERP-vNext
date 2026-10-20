using Diten.BuildingBlocks.Auditing;

namespace Diten.CrmService.Application.Features.Commercial.Pricing;

/// <summary>WP-VW-W6 W6-BE-b — MOD-0156 §4.6.3: the evaluation's reason codes (closed set; the client words them in seven
/// languages — the server never returns a sentence).</summary>
public static class PricingReasonCodes
{
    public const string DiscountSoftExceeded = "discount.soft-exceeded";
    public const string DiscountHardExceeded = "discount.hard-exceeded";
    public const string MfSoftExceeded = "mf.soft-exceeded";
    public const string MfHardExceeded = "mf.hard-exceeded";
    public const string CombinedSoftExceeded = "combined.soft-exceeded";
    public const string CombinedHardExceeded = "combined.hard-exceeded";
    public const string QuantityBelowMin = "quantity.below-min";
    public const string QuantitySoftExceeded = "quantity.soft-exceeded";
    public const string QuantityHardExceeded = "quantity.hard-exceeded";
    public const string SkuRetired = "sku.retired";
    public const string PriceSkuNotInList = "price.sku-not-in-list";
    public const string DiscountNotAllowedQuantityMode = "discount.not-allowed-quantity-mode";

    public static readonly IReadOnlyList<string> All = new[]
    {
        DiscountSoftExceeded, DiscountHardExceeded, MfSoftExceeded, MfHardExceeded, CombinedSoftExceeded, CombinedHardExceeded,
        QuantityBelowMin, QuantitySoftExceeded, QuantityHardExceeded, SkuRetired, PriceSkuNotInList, DiscountNotAllowedQuantityMode
    };
}

/// <summary>WP-VW-W6 W6-BE-b — MOD-0156 §13: the refusal codes (first element of a failed response's errors).</summary>
public static class PricingErrors
{
    public const string Invalid = "pricing_request_invalid";
    public const string NotFound = "pricing_not_found";
    public const string LegalEntityInvalid = "pricing_legal_entity_invalid";
    public const string MdmUnavailable = "pricing_mdm_unavailable";
    public const string SkuInvalid = "pricing_sku_invalid";
    public const string SkuRetired = "pricing_sku_retired";

    public const string PriceListOverlap = "price_list_overlap";
    public const string PriceListNotEditable = "price_list_not_editable";
    public const string PriceListItemInvalid = "price_list_item_invalid";
    public const string PriceListItemDuplicate = "price_list_item_duplicate";
    public const string PriceListEmpty = "price_list_empty";
    public const string PriceListNotPublished = "price_list_not_published";
    public const string PriceListSod = "price_list_sod";

    public const string ImportInvalid = "price_list_import_invalid";
    public const string ImportNotOpen = "price_list_import_not_open";
    public const string ImportNothingMatched = "price_list_import_nothing_matched";

    public const string RuleLimitsInvalid = "pricing_rule_limits_invalid";
    public const string RuleOverlap = "pricing_rule_overlap";
    public const string RuleNotEditable = "pricing_rule_not_editable";
    public const string RuleNotActive = "pricing_rule_not_active";
    public const string RuleOpenDraftExists = "pricing_rule_open_draft_exists";
    public const string RuleSod = "pricing_rule_sod";
    public const string SegmentNotEnabled = "pricing_segment_not_enabled";

    public const string FxRateDuplicate = "fx_rate_duplicate";
    public const string FxRateInvalid = "fx_rate_invalid";

    public const string Stale = "pricing_stale_version";
}

/// <summary>WP-VW-W6 W6-BE-b — every write command of MOD-0156 is audited (F8-a shared forwarder; category Crm, source
/// module MOD-0156; §20.3 AÇIK-2 "denetim kayıtlarını yapalım").</summary>
public static class PricingAudit
{
    public const string SourceModule = "MOD-0156";
    public const string PriceListEntity = "PriceList";
    public const string PriceListImportEntity = "PriceListImport";
    public const string PricingRuleEntity = "PricingRule";
    public const string FxRateEntity = "FxRate";

    public static AuditDescriptor Describe(AuditOperation operation, string entityType, Guid? id)
        => new(AuditCategory.Crm, operation, entityType, SourceModule, id is { } value && value != Guid.Empty ? value : null);
}
