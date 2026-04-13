using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Diten.WebUI.Models;

public sealed class InventoryGovernanceViewModel
{
    public IEnumerable<SelectListItem> StatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> AgingBucketOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<InventoryGovernanceExceptionRow> ExceptionRows { get; init; } = Array.Empty<InventoryGovernanceExceptionRow>();
}

public sealed record InventoryGovernanceExceptionRow(
    string Id,
    string Status,
    string LotNo,
    string ProductName,
    string DistributorName,
    decimal StockValue,
    int QtyOnHand,
    int DaysToExpiry,
    string RiskReason,
    string RecommendedAction,
    string? OwnerName,
    int DaysOnHand,
    decimal AvgSalesMonthly,
    string Country,
    string Seller,
    string AgingBucket);

public enum GovernanceStatus
{
    [Display(Name = "Healthy")]
    Healthy,
    [Display(Name = "Monitor")]
    Monitor,
    [Display(Name = "At Risk")]
    AtRisk,
    [Display(Name = "Critical")]
    Critical,
    [Display(Name = "Overstock")]
    Overstock
}

public enum AgingBucket
{
    All,
    [Display(Name = "0–30 days")]
    B0_30,
    [Display(Name = "31–60 days")]
    B31_60,
    [Display(Name = "61–90 days")]
    B61_90,
    [Display(Name = "90+ days")]
    B90Plus
}
