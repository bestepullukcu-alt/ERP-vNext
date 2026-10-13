using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Application.Features.Traceability.Services;

/// <summary>
/// WP-0174-P2 — the 422 code, message and details of a <see cref="LotPostingViolation"/>, shared by the pre-check
/// (<c>MovementMasterDataValidator</c>) and the in-transaction re-check (ledger repository), so the two always answer alike.
/// </summary>
public static class LotPostingErrors
{
    public static (string Code, string Message, IReadOnlyDictionary<string, object?> Details) For(LotPostingViolation violation, Lot lot)
    {
        var details = new Dictionary<string, object?>
        {
            ["lotId"] = lot.Id.ToString("D"),
            ["lotNumber"] = lot.LotNumber,
            ["lotStatus"] = lot.Status.ToString()
        };
        return violation switch
        {
            LotPostingViolation.SkuMismatch => (InventoryErrorCodes.LotSkuMismatch, "The lot belongs to another SKU than the movement.", details),
            LotPostingViolation.Blocked => (InventoryErrorCodes.LotBlocked,
                "The lot is Blocked: only SCRAP, WRITE_OFF and REVERSAL may move its stock (CT-D4).", details),
            LotPostingViolation.Expired => (InventoryErrorCodes.LotExpired,
                "The lot is expired: only SCRAP, WRITE_OFF and REVERSAL may move its stock (CT-D4).", details),
            _ => throw new ArgumentOutOfRangeException(nameof(violation), violation, null)
        };
    }

    public static IReadOnlyDictionary<string, object?> SerialDetails(IReadOnlyList<string> serials)
        => new Dictionary<string, object?> { ["serialIds"] = serials.Take(20).ToArray(), ["count"] = serials.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) };
}
