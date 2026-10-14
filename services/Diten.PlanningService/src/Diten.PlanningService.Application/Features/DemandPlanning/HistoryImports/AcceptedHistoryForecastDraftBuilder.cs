using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

// Internal orchestration. The caller supplies a scoped cycle and an explicitly selected,
// versioned method policy; no client-provided eligibility or history is trusted.
internal sealed class AcceptedHistoryForecastDraftBuilder(IAcceptedWeeklyHistoryReader historyReader)
{
    internal async Task<DemandRevisionDraft?> BuildAsync(PlanningCycle cycle,
        Guid actorId, Guid skuId, string skuLevel, string warehouseId,
        ForecastMethodEligibility.Method selectedMethod,
        ForecastMethodEligibility.FixturePolicy policy,
        string selectionReason, DateTimeOffset occurredAt,
        CancellationToken cancellationToken,
        FixtureForecastEngine.MethodSpecification? specification = null)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(policy);
        if (cycle.TenantId == Guid.Empty || cycle.LegalEntityId == Guid.Empty ||
            cycle.Id == Guid.Empty || cycle.IsDeleted || actorId == Guid.Empty ||
            skuId == Guid.Empty || string.IsNullOrWhiteSpace(warehouseId) ||
            string.IsNullOrWhiteSpace(selectionReason) ||
            string.IsNullOrWhiteSpace(policy.Version) ||
            cycle.HorizonStart.DayOfWeek != DayOfWeek.Monday ||
            cycle.AsOfDate >= cycle.HorizonStart || occurredAt == default)
            throw new ArgumentException("Forecast Draft requires a scoped cycle and explicit method selection.");

        var weekCount = selectedMethod is ForecastMethodEligibility.Method.SeasonalNaive or
            ForecastMethodEligibility.Method.HoltWinters ? policy.SeasonalWeeks : policy.GeneralWeeks;
        if (selectedMethod == ForecastMethodEligibility.Method.BiasCorrectedCroston)
            weekCount = policy.IntermittentWeeks;
        if (weekCount is < 26 or > 104)
            throw new ArgumentException("Method policy history window is unsupported.");

        var history = await historyReader.ReadAsync(cycle.TenantId, cycle.LegalEntityId,
            skuId, skuLevel, warehouseId, cycle.HorizonStart, weekCount, cancellationToken);
        if (history.TenantId != cycle.TenantId || history.LegalEntityId != cycle.LegalEntityId ||
            history.SkuId != skuId || history.SkuLevel != skuLevel ||
            history.WarehouseId != warehouseId || history.AsOfWeekStart != cycle.HorizonStart ||
            history.Weeks.Count != weekCount)
            throw new InvalidDataException("Accepted history does not match the selected cycle and series.");
        var readiness = AcceptedHistoryForecastReadiness.Assess(history);
        if (readiness.Route != AcceptedHistoryRoute.ForecastCandidate)
            return null;

        var fixture = new ForecastMethodEligibility.VerifiedHistoryFixture(
            new ForecastMethodEligibility.SeriesKey(cycle.TenantId,
                cycle.LegalEntityId, skuId, warehouseId),
            history.Weeks.Select(week => new ForecastMethodEligibility.FixtureWeek(
                week.WeekStart, week.State switch
                {
                    AcceptedWeekState.VerifiedDemand => ForecastMethodEligibility.WeekEvidence.VerifiedDemand,
                    AcceptedWeekState.VerifiedZeroDemand => ForecastMethodEligibility.WeekEvidence.VerifiedZeroDemand,
                    _ => throw new InvalidDataException("Untrusted history cannot enter forecasting.")
                }, week.Events.Select(item => new ForecastMethodEligibility.FixtureDemandEvent(
                    item.AcceptedRowId,
                    new ForecastMethodEligibility.SeriesKey(cycle.TenantId,
                        cycle.LegalEntityId, skuId, warehouseId),
                    item.OccurredOn, item.Quantity)).ToArray())).ToArray());
        var result = AssertSingle(FixtureForecastEngine.Compare(fixture, policy,
            cycle.HorizonStart, [selectedMethod], specification is null ? [] : [specification]));
        if (result.Forecast52.Count != 52 || result.Forecast52.Any(value => value < 0))
            throw new InvalidDataException("Method did not produce a valid 52-week forecast.");
        var provenance = new MethodWeekProvenance(Guid.NewGuid(),
            selectedMethod.ToString(), specification?.Variant ??
                (selectedMethod == ForecastMethodEligibility.Method.SeasonalNaive
                    ? "annual-lag-52" : "last-value"), policy.Version,
            history.Weeks[^1].WeekStart, Fingerprint(history), result.Parameters,
            readiness.UnitVerificationPending);
        var series = new VerifiedDraftSeries(skuId, warehouseId,
            readiness.UomId ?? throw new InvalidDataException("Accepted unit is missing."),
            cycle.Weeks.Select((week, index) => new ManualDraftWeekInput(
                week.Number, week.WeekStart, week.WeekEnd,
                DraftWeekValueKind.Known, result.Forecast52[index])).ToArray(), provenance);
        return ManualDraftFactory.Create(cycle, cycle.TenantId, cycle.LegalEntityId,
            actorId, selectionReason, occurredAt, [series]).Data ??
            throw new InvalidDataException("Forecast Draft failed locked-cycle validation.");
    }

    private static FixtureForecastEngine.CandidateResult AssertSingle(
        IReadOnlyList<FixtureForecastEngine.CandidateResult> candidates) =>
        candidates.Count == 1 ? candidates[0] :
            throw new InvalidDataException("Explicit method selection produced no unique candidate.");

    private static string Fingerprint(AcceptedSeriesHistory history)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        void Field(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        Field("MOD0188-ACCEPTED-HISTORY-1");
        Field(history.TenantId.ToString("D"));
        Field(history.LegalEntityId.ToString("D"));
        Field(history.SkuId.ToString("D"));
        Field(history.SkuLevel); Field(history.WarehouseId);
        Field(history.AsOfWeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        foreach (var week in history.Weeks)
        {
            Field(week.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Field(week.State.ToString()); Field(week.UomId ?? string.Empty);
            Field(week.UnitVerificationPending ? "1" : "0");
            Field(week.CoverageFileSha256 ?? string.Empty);
            Field(week.Events.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var item in week.Events.OrderBy(item => item.AcceptedRowId))
            {
                Field(item.AcceptedRowId.ToString("D"));
                Field(item.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                Field(item.Quantity.ToString("G29", CultureInfo.InvariantCulture));
            }
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
