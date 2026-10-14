using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>
/// Lot lifecycle (K-E, pack §12 S / R / E, CT-D8):
/// <list type="bullet">
/// <item>a person may move Draft → Released, Released → Blocked, Blocked → Released — nothing else;</item>
/// <item>Expired is the system's (B-3): never a target, never a source of a manual change;</item>
/// <item>Blocked needs an explicit acknowledgement of the impact;</item>
/// <item>every manual status change and every edit of a non-Draft lot needs a reason of at least ten characters.</item>
/// </list>
/// </summary>
public static class LotStatusRules
{
    public const int MinReasonLength = 10;

    public static bool IsReasonValid(string? reason) => reason is not null && reason.Trim().Length >= MinReasonLength;

    public static bool CanTransition(LotStatus from, LotStatus to) => (from, to) is
        (LotStatus.Draft, LotStatus.Released) or
        (LotStatus.Released, LotStatus.Blocked) or
        (LotStatus.Blocked, LotStatus.Released);

    public static bool RequiresAcknowledgement(LotStatus to) => to == LotStatus.Blocked;

    /// <summary>CT-D8: any lot that is not Draft is edited only with a reason (Blocked and Expired included).</summary>
    public static bool EditNeedsReason(LotStatus status) => status != LotStatus.Draft;

    /// <summary>A Blocked or Expired lot cannot take part in a new movement (pack §12; CT-D4 exceptions live in the ledger).</summary>
    public static bool IsUsable(LotStatus status) => status is LotStatus.Draft or LotStatus.Released;
}
