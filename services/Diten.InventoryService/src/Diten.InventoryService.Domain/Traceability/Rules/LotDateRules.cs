using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>
/// Lot dates (pack §12 rows 5–7) and the derived expiry signals (v1.1 <c>expiryState</c>, <c>retestDue</c>). Dates are
/// day-precision UTC midnights. "Today" is always passed in, so the rules are clock-free and testable.
/// </summary>
public static class LotDateRules
{
    /// <summary>"90 gün içinde" — the orange warning of the approved mockup.</summary>
    public const int ExpiringSoonDays = 90;

    public static string? Validate(DateOnly? manufacturing, DateOnly? expiry, DateOnly? retest, DateOnly today)
    {
        if (manufacturing is { } m && m > today)
            return "manufacturingDate cannot be in the future.";
        if (manufacturing is { } mf && expiry is { } e && e <= mf)
            return "expiryDate must be after manufacturingDate.";
        if (retest is { } r && manufacturing is { } rm && r < rm)
            return "retestDate cannot be before manufacturingDate.";
        if (retest is { } rt && expiry is { } re && rt > re)
            return "retestDate cannot be after expiryDate.";
        return null;
    }

    public static string ExpiryState(DateOnly? expiry, DateOnly today)
    {
        if (expiry is not { } e) return "None";
        if (e < today) return "Expired";
        return e.DayNumber - today.DayNumber <= ExpiringSoonDays ? "ExpiringSoon" : "Ok";
    }

    public static bool RetestDue(DateOnly? retest, DateOnly today) => retest is { } r && r < today;

    /// <summary>B-3: the expiry date has passed (expiryDate &lt; today) and the lot is not Expired yet.</summary>
    public static bool ShouldExpire(LotStatus status, DateOnly? expiry, DateOnly today)
        => status != LotStatus.Expired && expiry is { } e && e < today;

    /// <summary>"warn" filter: expired, expiring within 90 days, or retest due.</summary>
    public static bool HasWarning(DateOnly? expiry, DateOnly? retest, DateOnly today)
        => ExpiryState(expiry, today) is "Expired" or "ExpiringSoon" || RetestDue(retest, today);
}
