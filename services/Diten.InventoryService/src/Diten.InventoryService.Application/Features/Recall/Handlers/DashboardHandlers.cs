using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Recall.Trace;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

// RECALL-BUNDLE 1.5.0 (WP-0177-R6) — DashboardView and its members. Null members are omitted on the wire.

public sealed record RecallDashboardOpenCasesDto(int Total, IReadOnlyDictionary<string, int> ByClass);

public sealed record RecallDashboardDrillPointDto(
    string RunId, string Number, RecallLotRefDto Lot, string EndedAtUtc, int DurationMinutes, int TargetHours, bool OnTarget);

public sealed record RecallDashboardDrillChartDto(int TargetHours, IReadOnlyList<RecallDashboardDrillPointDto> Runs);

public sealed record RecallDashboardLotCoverageDto(int WindowDays, string FromUtc, string ToUtc, int WithLot, int Total, decimal? Percent);

public sealed record RecallDashboardGapMovementDto(string TransactionId, string? Number, string PostingDate, string SkuId, decimal Quantity, string BaseUomId);

public sealed record RecallDashboardTraceGapsDto(int WindowDays, int Count, IReadOnlyList<RecallDashboardGapMovementDto> Movements);

public sealed record RecallDashboardReturnedDto(string CaseId, string Number, string SkuId, string BaseUomId, decimal Quantity);

public sealed record RecallDashboardDto(
    string AsOfUtc,
    string LegalEntityId,
    RecallDashboardOpenCasesDto OpenCases,
    RecallDashboardDrillPointDto? LastDrill,
    RecallDashboardDrillChartDto DrillChart,
    RecallDashboardLotCoverageDto LotCoverage,
    RecallDashboardTraceGapsDto TraceGaps,
    IReadOnlyList<RecallDashboardReturnedDto> ReturnedOpenCases,
    Drills.RecallDrillFrequencyWarningDto FrequencyWarning,
    IReadOnlyList<RecallCaseRefDto> OpenCaseRows);

/// <summary>getDashboard (R6) — the Hazırlık Paneli of the caller's company. Reads only.</summary>
public sealed record GetRecallDashboardQuery : IRequest<Response<RecallDashboardDto>>;

/// <summary>
/// Pack §8.9 — every KPI of the panel, each from its own source and ALWAYS in the caller's company (the repositories and the dashboard
/// source are bound to Tenant + LegalEntity; there is no tenant-wide read here):
/// <list type="bullet">
/// <item>Açık gerçek vaka — count of cases not Closed / Cancelled, split by class (<c>recall_cases</c>).</item>
/// <item>Son tatbikat süresi — the newest COMPLETED run's duration vs the CURRENT <c>DrillTargetHours</c> (Hedefte / Hedef aşıldı).</item>
/// <item>Tatbikat grafiği — the last 12 completed runs, oldest first, + the target line.</item>
/// <item>Lot kapsamı (90 gün) — outbound movements (reversals netted) of lot-controlled SKUs with a lot ÷ all of them; a SKU whose lot
///   control is unknown counts as lot-controlled (R2-6, the conservative side). A typed lot text is a lot.</item>
/// <item>İzleme boşluğu (90 gün) — those movements WITHOUT a lot (the trace's <c>LOTLESS_OUTBOUND</c> rule), with the movement numbers.</item>
/// <item>Geri dönen (açık vakalar) — Σ linked return quantity per open case per SKU + base unit (never across units).</item>
/// <item>Sıklık uyarısı — days since the last completed run &gt; <c>DrillFrequencyWarningDays</c>, or no completed run (R5's rule).</item>
/// </list>
/// </summary>
public sealed class GetRecallDashboardHandler : IRequestHandler<GetRecallDashboardQuery, Response<RecallDashboardDto>>
{
    private readonly IRecallRepository _cases;
    private readonly IRecallDrillRepository _drills;
    private readonly IRecallDashboardSource _ledger;
    private readonly IRecallTraceSource _master;
    private readonly RecallSupport _support;

    public GetRecallDashboardHandler(IRecallRepository cases, IRecallDrillRepository drills, IRecallDashboardSource ledger, IRecallTraceSource master,
        RecallSupport support)
    {
        _cases = cases;
        _drills = drills;
        _ledger = ledger;
        _master = master;
        _support = support;
    }

    public async Task<Response<RecallDashboardDto>> Handle(GetRecallDashboardQuery request, CancellationToken ct)
    {
        var now = _support.Now();
        var settings = await _cases.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();

        // Açık gerçek vaka + the table + Geri dönen
        var open = (await _cases.OpenCasesAsync(ct)).Where(c => RecallDashboardRules.IsOpen(c.Status)).ToList();
        var byClass = new Dictionary<string, int>(StringComparer.Ordinal) { ["1"] = 0, ["2"] = 0, ["3"] = 0 };
        foreach (var c in open) byClass[c.Class.ToString(System.Globalization.CultureInfo.InvariantCulture)] = byClass.GetValueOrDefault(c.Class.ToString(System.Globalization.CultureInfo.InvariantCulture)) + 1;
        var shown = open.Take(RecallDashboardRules.OpenCaseRowsShown).ToList();
        var items = (await _cases.ScopeItemsOfCasesAsync(shown.Select(c => c.Id).ToList(), ct)).ToLookup(i => (i.CaseId, i.Revision));
        var rows = shown.Select(c => RecallMapping.Ref(c, items[(c.Id, c.ScopeRevision)].ToList())).ToList();
        var numbers = open.ToDictionary(c => c.Id, c => c.Number);
        var returned = (await _cases.ReturnLinksOfCasesAsync(open.Select(c => c.Id).ToList(), ct))
            .GroupBy(l => (l.CaseId, l.SkuId, l.BaseUomId))
            .OrderBy(g => numbers[g.Key.CaseId], StringComparer.Ordinal).ThenBy(g => g.Key.SkuId).ThenBy(g => g.Key.BaseUomId, StringComparer.Ordinal)
            .Select(g => new RecallDashboardReturnedDto(g.Key.CaseId.ToString("D"), numbers[g.Key.CaseId], g.Key.SkuId.ToString("D"), g.Key.BaseUomId, g.Sum(l => l.Quantity)))
            .ToList();

        // Son tatbikat + grafik + sıklık
        var completed = await _drills.LastCompletedRunsAsync(RecallDashboardRules.ChartRuns, ct);
        var points = completed.Where(r => r.DurationMinutes is not null && r.EndedAtUtc is not null)
            .Select(r => new RecallDashboardDrillPointDto(r.Id.ToString("D"), r.Number, new RecallLotRefDto(r.LotId, r.LotNumber), RecallSupport.Wire(r.EndedAtUtc!.Value),
                r.DurationMinutes!.Value, settings.DrillTargetHours, RecallDashboardRules.OnTarget(r.DurationMinutes.Value, settings.DrillTargetHours)))
            .ToList();
        var last = await _drills.LastCompletedAsync(ct);
        var (days, warning) = RecallDrillRules.Frequency(last?.CompletedAtUtc, now, settings.DrillFrequencyWarningDays);

        // Lot kapsamı + İzleme boşluğu (90 gün)
        var from = now.AddDays(-RecallDashboardRules.WindowDays);
        var window = Net(await _ledger.OutboundInWindowAsync(from, now, ct)).Where(t => TraceRules.Outbound.Contains(t.Type)).ToList();
        var controlled = new Dictionary<(Guid, SkuLevel), bool>();
        foreach (var sku in window.Select(t => (t.SkuId, t.SkuLevel)).Distinct())
        {
            controlled[sku] = await _master.LotControlledAsync(sku.SkuId, sku.SkuLevel, ct) != false;
        }

        var tracked = window.Where(t => controlled[(t.SkuId, t.SkuLevel)]).ToList();
        var lotless = tracked.Where(t => string.IsNullOrEmpty(t.LotId))
            .OrderByDescending(t => t.PostingDate).ThenByDescending(t => t.Number, StringComparer.Ordinal).ToList();
        var withLot = tracked.Count - lotless.Count;

        return Response<RecallDashboardDto>.Success(new RecallDashboardDto(
            RecallSupport.Wire(now),
            _support.LegalEntityId.ToString("D"),
            new RecallDashboardOpenCasesDto(open.Count, byClass),
            points.FirstOrDefault(),
            new RecallDashboardDrillChartDto(settings.DrillTargetHours, points.AsEnumerable().Reverse().ToList()),
            new RecallDashboardLotCoverageDto(RecallDashboardRules.WindowDays, RecallSupport.Wire(from), RecallSupport.Wire(now), withLot, tracked.Count,
                RecallDashboardRules.CoveragePercent(withLot, tracked.Count)),
            new RecallDashboardTraceGapsDto(RecallDashboardRules.WindowDays, lotless.Count,
                lotless.Take(RecallDashboardRules.GapMovementsShown)
                    .Select(t => new RecallDashboardGapMovementDto(t.Id, t.Number, RecallSupport.Wire(t.PostingDate), t.SkuId.ToString("D"), t.BaseQuantity, t.BaseUomId)).ToList()),
            returned,
            new Drills.RecallDrillFrequencyWarningDto(warning, days, RecallSupport.Wire(last?.CompletedAtUtc), settings.DrillFrequencyWarningDays),
            rows));
    }

    /// <summary>A reversed movement and its REVERSAL both leave the count (the trace engine's netting, pack §8.2 step 6).</summary>
    private static List<TraceTransaction> Net(IEnumerable<TraceTransaction> rows)
    {
        var all = rows.ToList();
        var reversed = all.Where(t => t.Type == MovementType.Reversal && t.ReversesTransactionId is not null)
            .Select(t => t.ReversesTransactionId!).ToHashSet(StringComparer.Ordinal);
        return all.Where(t => t.Type != MovementType.Reversal && !reversed.Contains(t.Id)).ToList();
    }
}
