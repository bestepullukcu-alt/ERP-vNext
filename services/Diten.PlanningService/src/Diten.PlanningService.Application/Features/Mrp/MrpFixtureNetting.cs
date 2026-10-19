namespace Diten.PlanningService.Application.Features.Mrp;

public enum MrpFixtureWeekQuality { Reliable, Missing, Unknown, OutOfStock }

public sealed record MrpFixtureWeek(DateOnly WeekStart, decimal GrossDemand,
    decimal ApprovedSafetyStock, MrpFixtureWeekQuality Quality);

public sealed record MrpFixtureInput(
    Guid TenantId, Guid LegalEntityId, Guid SkuId, Guid WarehouseId, string BaseUom,
    string DemandRevisionId, long DemandStateVersion, string InventorySnapshotVersion,
    string SafetyPolicyRevisionId, string LeadTimeRevisionId, int LeadTimeDays,
    decimal OpeningAvailableStock, IReadOnlyList<MrpFixtureWeek> Weeks);

public sealed record MrpFixtureWeekResult(
    DateOnly WeekStart, decimal OpeningStock, decimal GrossDemand, decimal ApprovedSafetyStock,
    decimal ProposedLotForLotQuantity, decimal ProjectedClosingStock);

public sealed record MrpFixtureResult(
    Guid TenantId, Guid LegalEntityId, Guid SkuId, Guid WarehouseId, string BaseUom,
    string DemandRevisionId, long DemandStateVersion, string InventorySnapshotVersion,
    string SafetyPolicyRevisionId, string LeadTimeRevisionId, int LeadTimeDays,
    bool IsDegraded, string DegradedReason, IReadOnlyList<MrpFixtureWeekResult> Weeks);

public static class MrpFixtureNetting
{
    public const string OpenOrdersUnavailable = "DEGRADED: acik siparisler hesaba katilmadi";

    public static MrpFixtureResult Calculate(MrpFixtureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.TenantId == Guid.Empty || input.LegalEntityId == Guid.Empty ||
            input.SkuId == Guid.Empty || input.WarehouseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.BaseUom) ||
            string.IsNullOrWhiteSpace(input.DemandRevisionId) || input.DemandStateVersion <= 0 ||
            string.IsNullOrWhiteSpace(input.InventorySnapshotVersion) ||
            string.IsNullOrWhiteSpace(input.SafetyPolicyRevisionId) ||
            string.IsNullOrWhiteSpace(input.LeadTimeRevisionId) || input.LeadTimeDays <= 0 ||
            input.OpeningAvailableStock < 0 || input.Weeks is null || input.Weeks.Count == 0)
            throw new ArgumentException("Complete, scoped, versioned fixture inputs are required.", nameof(input));

        var results = new List<MrpFixtureWeekResult>(input.Weeks.Count);
        var opening = input.OpeningAvailableStock;
        DateOnly? previous = null;
        foreach (var week in input.Weeks)
        {
            if (week.Quality != MrpFixtureWeekQuality.Reliable ||
                week.WeekStart.DayOfWeek != DayOfWeek.Monday ||
                (previous is not null && week.WeekStart != previous.Value.AddDays(7)) ||
                week.GrossDemand < 0 || week.ApprovedSafetyStock < 0)
                throw new ArgumentException("Weeks must be complete consecutive ISO weeks with nonnegative quantities.", nameof(input));

            try
            {
                var need = checked(week.GrossDemand + week.ApprovedSafetyStock - opening);
                var proposal = Math.Max(0m, need);
                var closing = checked(opening + proposal - week.GrossDemand);
                results.Add(new(week.WeekStart, opening, week.GrossDemand,
                    week.ApprovedSafetyStock, proposal, closing));
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
            input.InventorySnapshotVersion, input.SafetyPolicyRevisionId,
            input.LeadTimeRevisionId, input.LeadTimeDays, true, OpenOrdersUnavailable, results);
    }
}
