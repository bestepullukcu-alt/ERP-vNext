using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity.Rules;

/// <summary>
/// MOD-0155 FU06B — <b>Activity Time Budget</b>: how long ONE visit takes, in minutes.
/// <para><b>Pure by design</b>, exactly like <see cref="CycleCapacityCalculator"/>: no <c>HttpClient</c>, no
/// repository, no <c>ITenantContext</c>, no <c>DateTime.UtcNow</c>. It takes a capacity and the promo / non-promo
/// content-item counts of one visit and returns a duration. That is what lets the formula be tested exhaustively.</para>
///
/// <para><b>The formula (normative).</b> WP-CAP-MODEL moved it into the domain
/// (<c>CycleCapacity.VisitMinutes</c>) so the capacity calculator and this read model share ONE definition:</para>
/// <code>
/// visitDurationMinutes = (promoCount    × capacity.PromoProductTime)
///                      + (nonPromoCount × capacity.NonPromoProductTime)
///                      + capacity.ReportMinutesForVisit()   // typical row: ReportMinutesPerVisit; legacy row: ReportDuration
/// </code>
///
/// <para><b>It REUSES FU06's existing root fields</b> (<c>PromoProductTime</c> / <c>NonPromoProductTime</c> /
/// <c>ReportDuration</c>) as the single source of truth — FU06B introduces no per-item field of its own
/// (D-COLLISION = REUSE). Product time is FLAT (a fixed rate per item) and is multiplied by the visit's ACTUAL
/// content count; the report charge is added once per visit.</para>
///
/// <para><b>What is deliberately NOT here.</b> Travel time is the route optimiser's (MOD-0155 FU03) and is not a
/// parameter. The between-visit buffer (<c>capacity.BetweenVisitTimeMinutes</c>) does NOT enter a single visit's
/// duration either — it is applied BETWEEN consecutive visits by the packing engine (MOD-0155 FU05), so it is stored
/// but never read here.</para>
///
/// <para><b>This is a read model, not the capacity number.</b> It never references <see cref="CycleCapacityCalculator"/>
/// and never mutates the capacity; it only READS the three reused fields. FU06's <c>TotalVisitNumber</c> is entirely
/// unaffected (AC-ADD-1/2). Nothing computed here is ever persisted — writing a visit's duration is the consumer's job
/// (MOD-0155 FU04), not this FU's.</para>
/// </summary>
public static class ActivityTimeBudgetCalculator
{
    /// <summary>
    /// The duration of ONE visit that presents <paramref name="promoCount"/> promoted and
    /// <paramref name="nonPromoCount"/> non-promoted content items.
    /// <para>Negative counts are clamped to zero (a caller cannot make a visit shorter than its report charge), and the
    /// arithmetic is carried in <c>long</c> then clamped to <see cref="int.MaxValue"/> so a nonsensically large count
    /// degrades rather than overflowing — the same guard <see cref="CycleCapacityCalculator"/> uses.</para>
    /// </summary>
    public static int VisitDuration(CapacityEntity capacity, int promoCount, int nonPromoCount)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        return capacity.VisitMinutes(promoCount, nonPromoCount);
    }
}
