using Diten.InventoryService.Domain.Quarantine.Enums;

namespace Diten.InventoryService.Domain.Quarantine.Rules;

/// <summary>
/// MOD-0175-FU01 (pack §8.5): the ONE open / terminal predicate of a hold. A hold is open while it waits for (or is under) a
/// decision; <see cref="HoldStatus.Decided"/> and <see cref="HoldStatus.Voided"/> are terminal. Every count, filter and lock of
/// the module asks this class — a <c>HoldStatus.Decided</c> comparison anywhere else would count a voided hold as open
/// (AC-18, source-scanned). Blocked records keep their <c>== BlockedStatus.Open</c> checks: Voided is not Open there.
/// </summary>
public static class QuarantineRecordStates
{
    /// <summary>The open hold statuses (a Mongo <c>In</c> filter uses this list).</summary>
    public static readonly IReadOnlyList<HoldStatus> OpenHoldStatuses = [HoldStatus.Open, HoldStatus.DecisionPending];

    public static bool IsOpen(HoldStatus status) => status is HoldStatus.Open or HoldStatus.DecisionPending;

    public static bool IsTerminal(HoldStatus status) => status is HoldStatus.Decided or HoldStatus.Voided;

    /// <summary>The wire form (a <c>HoldRef.status</c> value).</summary>
    public static bool IsTerminal(string status) => Enum.TryParse<HoldStatus>(status, out var parsed) && IsTerminal(parsed);
}
