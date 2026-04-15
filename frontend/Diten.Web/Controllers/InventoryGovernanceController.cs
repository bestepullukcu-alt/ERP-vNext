using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using Diten.Web.Models;

namespace Diten.Web.Controllers;

public sealed class InventoryGovernanceController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var model = new InventoryGovernanceViewModel
        {
            StatusOptions = BuildStatusOptions(),
            AgingBucketOptions = BuildAgingOptions(),
            ExceptionRows = BuildExceptionRows()
        };

        return View(model);
    }

    private static IEnumerable<SelectListItem> BuildStatusOptions()
    {
        yield return new SelectListItem("All Statuses", "ALL", true);
        foreach (var status in Enum.GetValues<GovernanceStatus>())
        {
            yield return new SelectListItem(GetDisplayName(status), GetStatusValue(status));
        }
    }

    private static IEnumerable<SelectListItem> BuildAgingOptions()
    {
        yield return new SelectListItem("All Aging Buckets", "ALL", true);
        foreach (var bucket in Enum.GetValues<AgingBucket>().Where(x => x != AgingBucket.All))
        {
            yield return new SelectListItem(GetDisplayName(bucket), GetAgingValue(bucket));
        }
    }

    private static string GetDisplayName(Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .FirstOrDefault();
        return display?.Name ?? value.ToString();
    }

    private static string GetStatusValue(GovernanceStatus status)
    {
        return status switch
        {
            GovernanceStatus.Healthy => "Healthy",
            GovernanceStatus.Monitor => "Monitor",
            GovernanceStatus.AtRisk => "AtRisk",
            GovernanceStatus.Critical => "Critical",
            GovernanceStatus.Overstock => "Overstock",
            _ => status.ToString()
        };
    }

    private static string GetAgingValue(AgingBucket bucket)
    {
        return bucket switch
        {
            AgingBucket.B0_30 => "0_30",
            AgingBucket.B31_60 => "31_60",
            AgingBucket.B61_90 => "61_90",
            AgingBucket.B90Plus => "90_PLUS",
            _ => "ALL"
        };
    }

    private static IReadOnlyList<InventoryGovernanceExceptionRow> BuildExceptionRows()
    {
        return new List<InventoryGovernanceExceptionRow>
        {
            new(
                Id: "EX-1001",
                Status: "Healthy",
                LotNo: "LOT-1001",
                ProductName: "Product 1",
                DistributorName: "Distributor X",
                StockValue: 125000m,
                QtyOnHand: 420,
                DaysToExpiry: 54,
                RiskReason: "Slow moving inventory",
                RecommendedAction: "Run targeted promotion",
                OwnerName: "Dana Lee",
                DaysOnHand: 78,
                AvgSalesMonthly: 2300m,
                Country: "US",
                Seller: "Seller A",
                AgingBucket: "0_30"
            ),
            new(
                Id: "EX-2002",
                Status: "AtRisk",
                LotNo: "LOT-2002",
                ProductName: "Product 2",
                DistributorName: "Distributor Y",
                StockValue: 48000m,
                QtyOnHand: 160,
                DaysToExpiry: 22,
                RiskReason: "Expiry risk",
                RecommendedAction: "Coordinate markdown",
                OwnerName: null,
                DaysOnHand: 45,
                AvgSalesMonthly: 1400m,
                Country: "TR",
                Seller: "Seller B",
                AgingBucket: "61_90"
            )
        };
    }
}
