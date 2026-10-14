using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Queries;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>
/// WP-0176-F4 — the lot rows of the panel and the lists (pack §8.8). One row = one lot in one warehouse with on-hand &gt; 0 (the mockup's
/// lot × warehouse rows); severity from the company horizons (KF-A, not TRACE expiryState): expiry &lt; today → Expired; ≤ CriticalDays →
/// Critical; ≤ ApproachingDays → Approaching; else Ok. Days are UTC calendar days.
/// </summary>
public sealed class ExpiryLotView
{
    private readonly IExpiryRepository _repository;
    private readonly IExpiryReadModel _reads;
    private readonly ILocationRepository _locations;
    private readonly ExpirySupport _support;

    public ExpiryLotView(IExpiryRepository repository, IExpiryReadModel reads, ILocationRepository locations, ExpirySupport support)
    {
        _repository = repository;
        _reads = reads;
        _locations = locations;
        _support = support;
    }

    public sealed record Context(
        DateOnly Today,
        ExpiryPolicy Policy,
        IReadOnlyList<LotStockRow> Rows,
        IReadOnlyDictionary<Guid, Location> Warehouses,
        IReadOnlyDictionary<(string LotId, string? WarehouseId), Guid> ActiveCases);

    public async Task<Context> LoadAsync(string? warehouseId, Guid? skuId, CancellationToken ct)
    {
        var policy = await _repository.FindPolicyAsync(ct) ?? ExpiryPolicyRules.Defaults();
        var rows = (await _reads.LotStockAsync(warehouseId, skuId, ct)).Where(r => r.Lot.ExpiryDate is not null).ToList();
        var ids = rows.Select(r => Guid.TryParse(r.WarehouseId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var warehouses = ids.Count == 0 ? new Dictionary<Guid, Location>() : (await _locations.FindManyAsync(ids, ct)).ToDictionary(l => l.Id);
        return new Context(Today(_support.Now()), policy, rows, warehouses, await _reads.ActiveCasesAsync(ct));
    }

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    public static DateOnly Day(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);

    /// <summary>Expiry day − today (negative = expired).</summary>
    public static int Days(LotStockRow row, DateOnly today) => Day(row.Lot.ExpiryDate!.Value).DayNumber - today.DayNumber;

    public static string Severity(int days, ExpiryPolicy policy)
        => days < 0 ? "Expired" : days <= policy.CriticalDays ? "Critical" : days <= policy.ApproachingDays ? "Approaching" : "Ok";

    public static string Severity(DateTimeOffset? expiry, DateOnly asOf, ExpiryPolicy policy)
        => expiry is { } e ? Severity(Day(e).DayNumber - asOf.DayNumber, policy) : "Ok";

    public static string Wire(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? Wire(DateTimeOffset? value) => value is { } v ? Wire(Day(v)) : null;

    public static LotExpiryRowDto Row(LotStockRow row, Context context)
    {
        var lot = row.Lot;
        var days = Days(row, context.Today);
        int? shelfLife = lot.ManufacturingDate is { } m ? Day(lot.ExpiryDate!.Value).DayNumber - Day(m).DayNumber : null;
        decimal? pct = shelfLife is > 0 ? Math.Max(0m, Math.Round(days * 100m / shelfLife.Value, 0, MidpointRounding.AwayFromZero)) : null;
        var reasons = HeldReasons(row, context.Today);
        return new LotExpiryRowDto(
            lot.Id.ToString("D"),
            lot.LotNumber,
            lot.Status.ToString(),
            new ExpiryItemRefDto(lot.ItemId.ToString("D"), lot.SkuId.ToString("D"), WireNames.ToWire(lot.SkuLevel)),
            Guid.TryParse(row.WarehouseId, out var wid) && context.Warehouses.TryGetValue(wid, out var location)
                ? new ExpiryWarehouseRefDto(row.WarehouseId, location.Code, location.Name)
                : new ExpiryWarehouseRefDto(row.WarehouseId, null, null),
            Wire(lot.ExpiryDate)!,
            Wire(lot.RetestDate),
            Wire(lot.ManufacturingDate),
            days >= 0 ? days : null,
            days < 0 ? -days : null,
            Severity(days, context.Policy),
            pct,
            shelfLife,
            new ExpiryOnHandDto(row.Available, row.Held, row.BaseUomId),
            reasons.Count == 0 ? null : reasons,
            context.ActiveCases.TryGetValue((lot.Id.ToString("D"), row.WarehouseId), out var caseId) ? caseId.ToString("D") : null);
    }

    /// <summary>Why the held part is held (pack §8.2 reason codes): the lot's own state, then the stock statuses of the held quantity.</summary>
    public static IReadOnlyList<string> HeldReasons(LotStockRow row, DateOnly today)
    {
        var reasons = new List<string>();
        var asOf = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (row.Lot.Status == LotStatus.Blocked) reasons.Add("BLOCKED");
        if (LotPostingRules.IsExpiredOn(row.Lot, asOf)) reasons.Add("EXPIRED");
        if (LotPostingRules.IsRetestDueOn(row.Lot, asOf)) reasons.Add("RETEST_DUE");
        if (row.Lot.RetestHeldAtUtc is not null) reasons.Add("HELD");
        if (row.HeldStatuses.Any(s => s is StockStatus.Quarantine or StockStatus.QualityInspection)) reasons.Add("QUARANTINED");
        if (row.HeldStatuses.Contains(StockStatus.Blocked) && !reasons.Contains("BLOCKED")) reasons.Add("BLOCKED");
        return reasons;
    }

    public static string Reason(FefoExclusionReason reason) => reason switch
    {
        FefoExclusionReason.Expired => "EXPIRED",
        FefoExclusionReason.RetestDue => "RETEST_DUE",
        FefoExclusionReason.Blocked => "BLOCKED",
        FefoExclusionReason.Held => "HELD",
        FefoExclusionReason.BelowMinRemaining => "BELOW_MIN_REMAINING",
        FefoExclusionReason.Quarantined => "QUARANTINED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    public static bool TryUuid(string? raw, out string? normalized)
    {
        normalized = null;
        if (raw is null) return true;
        if (!ExpirySupport.TryParseId(raw, out var id)) return false;
        normalized = id.ToString("D");
        return true;
    }

    public static bool TryInt(string? raw, int min, int max, out int? value)
    {
        value = null;
        if (raw is null) return true;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v < min || v > max) return false;
        value = v;
        return true;
    }
}

/// <summary>
/// The pack §8.8 KPI formulas — ONE place for the panel (F4 getDashboard) and the Kanıt Paketi summary (F5 previewEvidence / the files):
/// Kritik / Yaklaşıyor = rows with AVAILABLE on-hand &gt; 0 and 0 ≤ days ≤ horizon, AVAILABLE and held apart (Yaklaşıyor includes Kritik);
/// expired on hand = days &lt; 0, on-hand in any status; compliance = compliant ÷ issues (1 decimal; no issue → null).
/// </summary>
public static class ExpiryKpiFormulas
{
    public sealed record StockFigures(ExpiryLotCountDto Critical, ExpiryLotCountDto Approaching, ExpiryExpiredCountDto Expired);

    public static StockFigures Stock(IReadOnlyList<(LotStockRow Row, int Days)> dated, ExpiryPolicy policy)
    {
        var critical = dated.Where(x => x.Row.Available > 0m && x.Days >= 0 && x.Days <= policy.CriticalDays).ToList();
        var approaching = dated.Where(x => x.Row.Available > 0m && x.Days >= 0 && x.Days <= policy.ApproachingDays).ToList();
        var expired = dated.Where(x => x.Days < 0).ToList();
        return new StockFigures(
            new ExpiryLotCountDto(critical.Count, critical.Sum(x => x.Row.Available), critical.Sum(x => x.Row.Held)),
            new ExpiryLotCountDto(approaching.Count, approaching.Sum(x => x.Row.Available), approaching.Sum(x => x.Row.Held)),
            new ExpiryExpiredCountDto(expired.Count, expired.Sum(x => x.Row.OnHand)));
    }

    public static ExpiryDashboardKpisDto Kpis(StockFigures stock, int issues, int compliant, int open, int late, double? avgResolutionHours) => new(
        issues == 0 ? null : Math.Round(compliant * 100m / issues, 1, MidpointRounding.AwayFromZero),
        issues,
        issues - compliant,
        stock.Critical,
        stock.Approaching,
        stock.Expired,
        open,
        late,
        avgResolutionHours is { } h ? Math.Round((decimal)h, 1, MidpointRounding.AwayFromZero) : null);
}

/// <summary>
/// §4.1 getDashboard (pack §8.8, formulas verbatim): FEFO compliance = compliant ÷ all issue snapshots posted in the period; Kritik /
/// Yaklaşıyor = rows with AVAILABLE on-hand &gt; 0 and today ≤ expiry ≤ today + horizon (Yaklaşıyor includes Kritik); expired on hand =
/// expiry &lt; today and on-hand &gt; 0 in any status; open / late cases; mean resolution hours of the period; on-hand by expiry month for 12
/// months (non-expired, AVAILABLE vs held); top-risk = the 10 nearest non-expired rows within ApproachingDays.
/// </summary>
public sealed class GetExpiryDashboardHandler : IRequestHandler<GetExpiryDashboardQuery, Response<ExpiryDashboardDto>>
{
    private readonly ExpiryLotView _view;
    private readonly IExpiryReadModel _reads;
    private readonly ExpirySupport _support;

    public GetExpiryDashboardHandler(ExpiryLotView view, IExpiryReadModel reads, ExpirySupport support)
    {
        _view = view;
        _reads = reads;
        _support = support;
    }

    public async Task<Response<ExpiryDashboardDto>> Handle(GetExpiryDashboardQuery request, CancellationToken ct)
    {
        if (!ExpiryLotView.TryUuid(request.WarehouseId, out var warehouseId)) return ExpirySupport.Invalid<ExpiryDashboardDto>("warehouseId must be a canonical UUID.");
        if (!ExpiryLotView.TryInt(request.PeriodDays, 1, 366, out var periodDays)) return ExpirySupport.Invalid<ExpiryDashboardDto>("periodDays must be 1-366.");
        var period = periodDays ?? 30;
        var now = _support.Now();
        var context = await _view.LoadAsync(warehouseId, null, ct);
        var policy = context.Policy;
        var dated = context.Rows.Select(r => (Row: r, Days: ExpiryLotView.Days(r, context.Today))).ToList();

        var stock = ExpiryKpiFormulas.Stock(dated, policy);
        var from = now.AddDays(-period);
        var (issues, compliant) = await _reads.FefoIssuesAsync(from, now, warehouseId, ct);
        var exceptions = await _reads.ExceptionFiguresAsync(now, from, warehouseId, ct);

        var firstMonth = new DateOnly(context.Today.Year, context.Today.Month, 1);
        var months = Enumerable.Range(0, 12).Select(i =>
        {
            var month = firstMonth.AddMonths(i);
            var inMonth = dated.Where(x => x.Days >= 0 && ExpiryLotView.Day(x.Row.Lot.ExpiryDate!.Value) is var d && d.Year == month.Year && d.Month == month.Month).ToList();
            return new ExpiryMonthDto(month.ToString("yyyy-MM", CultureInfo.InvariantCulture), inMonth.Sum(x => x.Row.Available), inMonth.Sum(x => x.Row.Held));
        }).ToList();
        var topRisk = dated.Where(x => x.Days >= 0 && x.Days <= policy.ApproachingDays)
            .OrderBy(x => x.Days).ThenBy(x => x.Row.Lot.LotNumber, StringComparer.Ordinal).ThenBy(x => x.Row.WarehouseId, StringComparer.Ordinal)
            .Take(10).Select(x => ExpiryLotView.Row(x.Row, context)).ToList();

        var kpis = ExpiryKpiFormulas.Kpis(stock, issues, compliant, exceptions.Open, exceptions.Late, exceptions.AvgResolutionHours);
        return Response<ExpiryDashboardDto>.Success(new ExpiryDashboardDto(
            ExpiryLotView.Wire(context.Today),
            new ExpiryPeriodDto(ExpiryLotView.Wire(ExpiryLotView.Day(from)), ExpiryLotView.Wire(context.Today)),
            warehouseId,
            kpis,
            months,
            topRisk));
    }
}

/// <summary>
/// §4.2 listApproaching — rows with 0 ≤ days ≤ horizon; the horizon is the caller's (1-730) but never beyond the company's ApproachingDays
/// (KF-A). includeHeld=false keeps rows with AVAILABLE on-hand only; q matches the lot number; sort expiryDate (default) / -expiryDate /
/// lotNumber; paging 1-200.
/// </summary>
public sealed class ListApproachingLotsHandler : IRequestHandler<ListApproachingLotsQuery, Response<LotExpiryPageDto>>
{
    private readonly ExpiryLotView _view;

    public ListApproachingLotsHandler(ExpiryLotView view) => _view = view;

    public async Task<Response<LotExpiryPageDto>> Handle(ListApproachingLotsQuery request, CancellationToken ct)
    {
        if (!ExpiryLotView.TryUuid(request.WarehouseId, out var warehouseId) || !ExpiryLotView.TryUuid(request.SkuId, out var skuId))
            return ExpirySupport.Invalid<LotExpiryPageDto>("warehouseId and skuId must be canonical UUIDs.");
        if (request.Severity is not (null or "Critical" or "Approaching")) return ExpirySupport.Invalid<LotExpiryPageDto>("severity must be Critical or Approaching.");
        if (!ExpiryLotView.TryInt(request.HorizonDays, 1, 730, out var horizonDays)) return ExpirySupport.Invalid<LotExpiryPageDto>("horizonDays must be 1-730.");
        if (request.IncludeHeld is not (null or "true" or "false")) return ExpirySupport.Invalid<LotExpiryPageDto>("includeHeld must be true or false.");
        if (request.Q is { Length: > 100 }) return ExpirySupport.Invalid<LotExpiryPageDto>("q must be at most 100 characters.");
        if (request.Sort is not (null or "expiryDate" or "-expiryDate" or "lotNumber")) return ExpirySupport.Invalid<LotExpiryPageDto>("sort must be expiryDate, -expiryDate or lotNumber.");
        if (ExpiryPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return ExpirySupport.Invalid<LotExpiryPageDto>($"page must be ≥ 1 and pageSize 1-{ExpiryContract.MaxPageSize}.");

        var context = await _view.LoadAsync(warehouseId, skuId is null ? null : Guid.Parse(skuId), ct);
        var horizon = Math.Min(horizonDays ?? context.Policy.ApproachingDays, context.Policy.ApproachingDays);
        var q = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();
        var rows = context.Rows.Select(r => (Row: r, Days: ExpiryLotView.Days(r, context.Today)))
            .Where(x => x.Days >= 0 && x.Days <= horizon)
            .Where(x => request.IncludeHeld != "false" || x.Row.Available > 0m)
            .Where(x => request.Severity is null || ExpiryLotView.Severity(x.Days, context.Policy) == request.Severity)
            .Where(x => q is null || x.Row.Lot.LotNumber.Contains(q, StringComparison.OrdinalIgnoreCase));
        IOrderedEnumerable<(LotStockRow Row, int Days)> ordered = request.Sort switch
        {
            "-expiryDate" => rows.OrderByDescending(x => x.Days).ThenBy(x => x.Row.Lot.LotNumber, StringComparer.Ordinal),
            "lotNumber" => rows.OrderBy(x => x.Row.Lot.LotNumber, StringComparer.Ordinal).ThenBy(x => x.Days),
            _ => rows.OrderBy(x => x.Days).ThenBy(x => x.Row.Lot.LotNumber, StringComparer.Ordinal)
        };
        var all = ordered.ThenBy(x => x.Row.WarehouseId, StringComparer.Ordinal).ToList();
        return Response<LotExpiryPageDto>.Success(new LotExpiryPageDto(
            all.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).Select(x => ExpiryLotView.Row(x.Row, context)).ToList(),
            all.Count, paging.Page, paging.PageSize));
    }
}

/// <summary>§4.3 listExpired — expiry &lt; today and on-hand &gt; 0 in any status; daysSinceExpiry ("Dolduğundan beri"), most expired first.</summary>
public sealed class ListExpiredLotsHandler : IRequestHandler<ListExpiredLotsQuery, Response<LotExpiryPageDto>>
{
    private readonly ExpiryLotView _view;

    public ListExpiredLotsHandler(ExpiryLotView view) => _view = view;

    public async Task<Response<LotExpiryPageDto>> Handle(ListExpiredLotsQuery request, CancellationToken ct)
    {
        if (!ExpiryLotView.TryUuid(request.WarehouseId, out var warehouseId) || !ExpiryLotView.TryUuid(request.SkuId, out var skuId))
            return ExpirySupport.Invalid<LotExpiryPageDto>("warehouseId and skuId must be canonical UUIDs.");
        if (!ExpiryLotView.TryInt(request.MinDaysSinceExpiry, 0, int.MaxValue, out var minDays)) return ExpirySupport.Invalid<LotExpiryPageDto>("minDaysSinceExpiry must be ≥ 0.");
        if (ExpiryPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return ExpirySupport.Invalid<LotExpiryPageDto>($"page must be ≥ 1 and pageSize 1-{ExpiryContract.MaxPageSize}.");

        var context = await _view.LoadAsync(warehouseId, skuId is null ? null : Guid.Parse(skuId), ct);
        var all = context.Rows.Select(r => (Row: r, Days: ExpiryLotView.Days(r, context.Today)))
            .Where(x => x.Days < 0 && -x.Days >= (minDays ?? 0))
            .OrderBy(x => x.Days).ThenBy(x => x.Row.Lot.LotNumber, StringComparer.Ordinal).ThenBy(x => x.Row.WarehouseId, StringComparer.Ordinal)
            .ToList();
        return Response<LotExpiryPageDto>.Success(new LotExpiryPageDto(
            all.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).Select(x => ExpiryLotView.Row(x.Row, context)).ToList(),
            all.Count, paging.Page, paging.PageSize));
    }
}

/// <summary>
/// §4.4 getFefoOrder — read-only (KF-F): the posting hook's candidates (<see cref="IExpiryReadModel.FefoCandidatesAsync"/>) put through the
/// SAME pure <see cref="FefoOrderRule"/> (no second ordering), the SKU minimum from getSku; greedy allocation over AVAILABLE on-hand when a
/// quantity is given (<c>basis: onHandAvailable</c> — reservations are not lot-bound); <c>skuAvailable</c> = the ledger's getAvailability
/// for the SKU in this warehouse. Creates no reservation.
/// </summary>
public sealed class GetFefoOrderHandler : IRequestHandler<GetFefoOrderQuery, Response<FefoOrderDto>>
{
    public const string UnknownSku = "UNKNOWN_SKU";

    private readonly IExpiryReadModel _reads;
    private readonly IExpiryRepository _repository;
    private readonly IProductMasterClient _productMaster;
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenant;
    private readonly ExpirySupport _support;

    public GetFefoOrderHandler(IExpiryReadModel reads, IExpiryRepository repository, IProductMasterClient productMaster, IMediator mediator,
        ITenantContext tenant, ExpirySupport support)
    {
        _reads = reads;
        _repository = repository;
        _productMaster = productMaster;
        _mediator = mediator;
        _tenant = tenant;
        _support = support;
    }

    public async Task<Response<FefoOrderDto>> Handle(GetFefoOrderQuery request, CancellationToken ct)
    {
        if (request.WarehouseId is null || request.ItemId is null || request.SkuId is null || request.SkuLevel is null)
            return ExpirySupport.Invalid<FefoOrderDto>("warehouseId, itemId, skuId and skuLevel are required.");
        if (!ExpiryLotView.TryUuid(request.WarehouseId, out var warehouseId) || !ExpiryLotView.TryUuid(request.ItemId, out var item)
            || !ExpiryLotView.TryUuid(request.SkuId, out var sku))
            return ExpirySupport.Invalid<FefoOrderDto>("warehouseId, itemId and skuId must be canonical UUIDs.");
        if (!WireNames.TryParseSkuLevel(request.SkuLevel, out var level)) return ExpirySupport.Invalid<FefoOrderDto>("skuLevel must be Gsku, Lsku, FinishedGood or Item.");
        decimal? quantity = null;
        if (request.Quantity is not null)
        {
            if (!decimal.TryParse(request.Quantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var q) || q <= 0m)
                return ExpirySupport.Invalid<FefoOrderDto>("quantity must be a number > 0.");
            quantity = q;
        }

        var asOf = ExpiryLotView.Today(_support.Now());
        if (request.AsOf is not null && !DateOnly.TryParseExact(request.AsOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out asOf))
            return ExpirySupport.Invalid<FefoOrderDto>("asOf must be a date yyyy-MM-dd.");

        SkuReference? skuRef;
        try
        {
            skuRef = await _productMaster.GetSkuAsync(Guid.Parse(sku!), level, ct);
        }
        catch (ProductMasterUnavailableException)
        {
            return Response<FefoOrderDto>.Fail(InventoryErrorCodes.ProductMasterUnavailable, "PRODUCT-MASTER could not be reached; the SKU minimum is unknown.", 503);
        }

        if (skuRef is null || (skuRef.ItemId is { } skuItem && !string.Equals(skuItem, item, StringComparison.OrdinalIgnoreCase)))
            return Response<FefoOrderDto>.Fail(UnknownSku, "The SKU is not known to PRODUCT-MASTER for this item.", 404);

        var policy = await _repository.FindPolicyAsync(ct) ?? ExpiryPolicyRules.Defaults();
        var candidates = await _reads.FefoCandidatesAsync(warehouseId!, Guid.Parse(item!), Guid.Parse(sku!), level, ct);
        var order = FefoOrderRule.Order(candidates, new DateTimeOffset(asOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), skuRef.MinRemainingShelfLifeDays);

        var remaining = quantity ?? 0m;
        var eligible = order.Eligible.Select(e =>
        {
            decimal? allocated = null;
            if (quantity is not null)
            {
                allocated = Math.Min(remaining, e.Available);
                remaining -= allocated.Value;
            }

            return new FefoEligibleLotDto(e.Rank, e.Lot.Id.ToString("D"), e.Lot.LotNumber, ExpiryLotView.Wire(e.Lot.ExpiryDate),
                ExpiryLotView.Wire(e.Lot.ManufacturingDate), ExpiryLotView.Wire(e.Lot.RetestDate), e.Available,
                ExpiryLotView.Severity(e.Lot.ExpiryDate, asOf, policy), allocated is > 0m ? allocated : quantity is null ? null : 0m);
        }).ToList();
        var excluded = order.Excluded.Select(x => new FefoExcludedLotDto(x.Lot.Id.ToString("D"), x.Lot.LotNumber, ExpiryLotView.Wire(x.Lot.ExpiryDate),
            ExpiryLotView.Reason(x.Reason), x.OnHand)).ToList();

        var availability = await _mediator.Send(new GetAvailabilityQuery(item, sku, WireNames.ToWire(level), warehouseId, null), ct);
        var skuAvailable = availability.IsSuccessful && decimal.TryParse(availability.Data?.Available, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : 0m;

        return Response<FefoOrderDto>.Success(new FefoOrderDto(
            _tenant.LegalEntityId.ToString("D"), warehouseId!, item!, sku!, WireNames.ToWire(level), ExpiryLotView.Wire(asOf), "onHandAvailable", skuAvailable,
            eligible, excluded, quantity, quantity is null ? null : quantity - remaining, quantity is null ? null : remaining));
    }
}
