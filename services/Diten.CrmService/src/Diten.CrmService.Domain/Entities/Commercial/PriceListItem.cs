namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.1: one product of a price list (own collection — the TR official list has thousands of
/// rows). The SKU is product-master v1.1 (<c>skuId</c> + the level the server resolved) with a snapshot for display; at
/// least one of the three tiers is set; unique per (list, SKU). Frozen once the list is published.
/// </summary>
public sealed class PriceListItem : EntityBase
{
    public Guid PriceListId { get; set; }

    public Guid SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? SkuCode { get; set; }
    public string? SkuName { get; set; }
    public string? Gtin { get; set; }

    public decimal? DepotPrice { get; set; }
    public decimal? PharmacistPurchasePrice { get; set; }
    public decimal? PublicPrice { get; set; }

    /// <summary>0–100.</summary>
    public decimal VatRate { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>The price of a tier (<see cref="PriceTiers"/>); null when the tier is not set.</summary>
    public decimal? PriceOf(string tier) => tier switch
    {
        PriceTiers.Depot => DepotPrice,
        PriceTiers.Public => PublicPrice,
        _ => PharmacistPurchasePrice
    };
}
