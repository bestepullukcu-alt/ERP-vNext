namespace Diten.PlanningService.Application.Features.Mrp;

public enum MrpWeekQuality { Reliable, Missing, Unknown, OutOfStock }

public sealed record MrpWeekSafetyEvidence(bool Found, string? PolicyRevisionId = null,
    long? RecordVersion = null, string? Source = null);

public sealed record MrpWeek(DateOnly WeekStart, decimal GrossDemand,
    decimal ApprovedSafetyStock, MrpWeekQuality Quality,
    MrpWeekSafetyEvidence? SafetyEvidence = null);

public sealed record MrpInput(
    Guid TenantId, Guid LegalEntityId, Guid SkuId, Guid WarehouseId, string BaseUom,
    string DemandRevisionId, long DemandStateVersion, string DemandChecksum,
    DateTimeOffset InventoryAsOf,
    string SafetyPolicyRevisionId, string LeadTimeRevisionId, int LeadTimeDays,
    decimal OpeningAvailableStock, IReadOnlyList<MrpWeek> Weeks);

public sealed record MrpWeekResult(
    DateOnly WeekStart, decimal OpeningStock, decimal GrossDemand, decimal ApprovedSafetyStock,
    decimal ProposedLotForLotQuantity, decimal ProjectedClosingStock,
    MrpWeekSafetyEvidence? SafetyEvidence = null, string? DegradedReason = null);

public sealed record MrpResult(
    Guid TenantId, Guid LegalEntityId, Guid SkuId, Guid WarehouseId, string BaseUom,
    string DemandRevisionId, long DemandStateVersion, string DemandChecksum,
    DateTimeOffset InventoryAsOf,
    string SafetyPolicyRevisionId, string LeadTimeRevisionId, int LeadTimeDays,
    bool IsDegraded, string DegradedReason, IReadOnlyList<MrpWeekResult> Weeks);

public static class MrpNetting
{
    public const string OpenOrdersUnavailable = "DEGRADED: acik siparisler hesaba katilmadi";
    public const string SafetyStockUnavailable = "DEGRADED: onayli guvenlik stogu hesaba katilmadi";

    public static MrpResult Calculate(MrpInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty || input.LegalEntityId == Guid.Empty ||
            input.SkuId == Guid.Empty || input.WarehouseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.BaseUom) ||
            string.IsNullOrWhiteSpace(input.DemandRevisionId) || input.DemandStateVersion <= 0 ||
            string.IsNullOrWhiteSpace(input.DemandChecksum) || input.InventoryAsOf == default ||
            string.IsNullOrWhiteSpace(input.LeadTimeRevisionId) || input.LeadTimeDays <= 0 ||
            input.OpeningAvailableStock < 0 || input.Weeks is null || input.Weeks.Count == 0)
            throw new ArgumentException("Complete, scoped, versioned fixture inputs are required.", nameof(input));

        var weeklySafety = input.Weeks.Any(week => week.SafetyEvidence is not null);
        if (weeklySafety && input.Weeks.Any(week => week.SafetyEvidence is null))
            throw new ArgumentException("Each week needs safety-stock evidence.", nameof(input));
        var safetyUnavailable = weeklySafety
            ? input.Weeks.Any(week => week.SafetyEvidence is { Found: false })
            : string.IsNullOrWhiteSpace(input.SafetyPolicyRevisionId);
        if (!weeklySafety && safetyUnavailable && input.Weeks.Any(week => week.ApprovedSafetyStock != 0))
            throw new ArgumentException("Unverified safety stock cannot enter netting.", nameof(input));
        var results = new List<MrpWeekResult>(input.Weeks.Count);
        var opening = input.OpeningAvailableStock;
        DateOnly? previous = null;
        foreach (var week in input.Weeks)
        {
            if (week.Quality != MrpWeekQuality.Reliable ||
                week.WeekStart.DayOfWeek != DayOfWeek.Monday ||
                (previous is not null && week.WeekStart != previous.Value.AddDays(7)) ||
                week.GrossDemand < 0 || week.ApprovedSafetyStock < 0)
                throw new ArgumentException("Weeks must be complete consecutive ISO weeks with nonnegative quantities.", nameof(input));
            if (week.SafetyEvidence is { } evidence &&
                (evidence.Found
                    ? string.IsNullOrWhiteSpace(evidence.PolicyRevisionId) ||
                      evidence.RecordVersion is null or <= 0 ||
                      string.IsNullOrWhiteSpace(evidence.Source)
                    : week.ApprovedSafetyStock != 0 || evidence.PolicyRevisionId is not null ||
                      evidence.RecordVersion is not null || evidence.Source is not null))
                throw new ArgumentException("Safety-stock quantity and weekly provenance disagree.", nameof(input));

            try
            {
                var need = checked(week.GrossDemand + week.ApprovedSafetyStock - opening);
                var proposal = Math.Max(0m, need);
                var closing = checked(opening + proposal - week.GrossDemand);
                results.Add(new(week.WeekStart, opening, week.GrossDemand,
                    week.ApprovedSafetyStock, proposal, closing, week.SafetyEvidence,
                    week.SafetyEvidence is { Found: false } ? SafetyStockUnavailable : null));
                opening = closing;
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException("MRP quantity exceeds decimal range; no proposal is produced.",
                    nameof(input), exception);
            }

            previous = week.WeekStart;
        }

        return new(input.TenantId, input.LegalEntityId, input.SkuId, input.WarehouseId,
            input.BaseUom, input.DemandRevisionId, input.DemandStateVersion,
            input.DemandChecksum, input.InventoryAsOf, input.SafetyPolicyRevisionId,
            input.LeadTimeRevisionId, input.LeadTimeDays, true,
            safetyUnavailable ? $"{OpenOrdersUnavailable}; {SafetyStockUnavailable}" : OpenOrdersUnavailable,
            results);
    }
}
