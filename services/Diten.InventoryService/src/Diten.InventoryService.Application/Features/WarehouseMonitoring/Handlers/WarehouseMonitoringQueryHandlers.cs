using System.Collections;
using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseMonitoring.Queries;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.WarehouseMonitoring.Handlers;

/// <summary>
/// The S3 monitoring reads (pack §8.23, DL-7) — read-only, the selected company only (every read port starts from Tenant + LegalEntity). The
/// exception box is DERIVED at read time (<see cref="WarehouseExceptionRules"/>); a source that cannot be read (recall, the INVENTORY balance)
/// makes the read 503 — never "no exception" (fail-closed). No answer carries a ship-to.
/// </summary>
public sealed class WarehouseMonitoringQueryHandlers :
    IRequestHandler<GetWarehouseExceptionListQuery, Response<WarehouseExceptionPageDto>>,
    IRequestHandler<GetWarehouseActivityListQuery, Response<WarehouseActivityPageDto>>,
    IRequestHandler<GetWarehouseIntegrationStatusQuery, Response<WarehouseIntegrationStatusDto>>
{
    /// <summary>§8.23: the Activity Trace range is at most 31 days.</summary>
    public const int MaxActivityDays = 31;

    /// <summary>The deepest row an Activity Trace page may reach (page × pageSize) — beyond it the range is to be narrowed (400).</summary>
    public const int MaxActivityDepth = 10_000;

    /// <summary>The two §8.23 detail members written as null when they do not apply.</summary>
    private static readonly HashSet<string> NullableDetails = new(StringComparer.Ordinal) { "stagingDifference", "differenceDeferredBy" };

    private readonly IWarehouseMonitoringReads _reads;
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly IWarehouseTaskRepository _tasks;
    private readonly IWarehouseStockReader _stock;
    private readonly IRecallLotStatusReader _recall;
    private readonly WarehouseSupport _support;
    private readonly WarehousePutawayOptions _putaway;
    private readonly WarehouseOutboundOptions _outbound;

    public WarehouseMonitoringQueryHandlers(IWarehouseMonitoringReads reads, IOutboundShipmentRepository outbounds, IWarehouseTaskRepository tasks,
        IWarehouseStockReader stock, IRecallLotStatusReader recall, WarehouseSupport support, IOptions<WarehousePutawayOptions> putaway,
        IOptions<WarehouseOutboundOptions> outbound)
    {
        _reads = reads;
        _outbounds = outbounds;
        _tasks = tasks;
        _stock = stock;
        _recall = recall;
        _support = support;
        _putaway = putaway.Value;
        _outbound = outbound.Value;
    }

    // ── GET /exceptions ───────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<WarehouseExceptionPageDto>> Handle(GetWarehouseExceptionListQuery request, CancellationToken ct)
    {
        WarehouseExceptionKind? kind = null;
        if (request.Kind is not null)
        {
            if (!Enum.TryParse<WarehouseExceptionKind>(request.Kind, false, out var k) || !Enum.IsDefined(k) || request.Kind != k.ToString())
                return WarehouseSupport.Invalid<WarehouseExceptionPageDto>($"kind must be one of {string.Join(", ", Enum.GetNames<WarehouseExceptionKind>())}.");
            kind = k;
        }

        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var id)) return WarehouseSupport.Invalid<WarehouseExceptionPageDto>("warehouseId must be a UUID.");
            warehouse = id;
        }

        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging)
            return WarehouseSupport.Invalid<WarehouseExceptionPageDto>("page must be ≥ 1 and pageSize 1-200.");

        bool Wants(WarehouseExceptionKind k) => kind is null || kind == k;
        var now = _support.Now();
        var records = new List<WarehouseExceptionRecord>();

        // every task with an open confirmation of the company (the Pending ones also defer a Staging difference, whatever the warehouse filter)
        var openTasks = await _reads.TasksWithOpenConfirmationsAsync(null, ct);
        if (Wants(WarehouseExceptionKind.PostingRefused) || Wants(WarehouseExceptionKind.PostingPendingLong) || Wants(WarehouseExceptionKind.PostingIntegrityError))
        {
            var longPending = TimeSpan.FromMinutes(_putaway.LongPendingMinutes);
            records.AddRange(openTasks.Where(t => warehouse is null || t.WarehouseId == warehouse).SelectMany(t => WarehouseExceptionRules.OfTask(t, now, longPending)));
        }

        WarehouseExceptionKind[] outboundKinds =
        [
            WarehouseExceptionKind.DispatchNotSeen, WarehouseExceptionKind.ReservationReleasePending, WarehouseExceptionKind.ShortDispatchUnresolved,
            WarehouseExceptionKind.AllocationOrphanSuspected, WarehouseExceptionKind.ReservedLotUnderRecall, WarehouseExceptionKind.AllocationStuck
        ];
        if (outboundKinds.Any(Wants))
        {
            var outbounds = await _reads.ExceptionOutboundsAsync(warehouse, ct);

            IReadOnlySet<string> recalled = new HashSet<string>(StringComparer.Ordinal);
            if (Wants(WarehouseExceptionKind.ReservedLotUnderRecall))
            {
                var lotIds = outbounds.Where(o => WarehouseExceptionRules.RecallStatuses.Contains(o.Status)).SelectMany(o => o.AllAllocations())
                    .Select(a => a.LotId).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
                if (lotIds.Count > 0)
                {
                    try
                    {
                        recalled = await _recall.UnderRecallAsync(lotIds, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        return Unavailable<WarehouseExceptionPageDto>("The recall status of the reserved lots could not be read.");
                    }
                }
            }

            StagingInputs? staging = null;
            if (Wants(WarehouseExceptionKind.ShortDispatchUnresolved))
            {
                var nodes = outbounds.Where(o => o.Status == OutboundStatus.Shipped && o.ShortDispatchUnresolved.Count > 0).Select(o => o.StagingLocationId).Distinct().ToList();
                if (nodes.Count > 0)
                {
                    try
                    {
                        staging = await StagingAsync(nodes, openTasks, ct);
                    }
                    catch (WarehouseInventoryUnavailableException)
                    {
                        return Unavailable<WarehouseExceptionPageDto>("The Staging stock could not be read.");
                    }
                }
            }

            var thresholds = new OutboundExceptionThresholds(TimeSpan.FromHours(_outbound.DispatchNotSeenHours), TimeSpan.FromSeconds(2 * _outbound.AllocationLeaseSeconds));
            var empty = new StagingInputs(new Dictionary<Guid, IReadOnlyList<StagingBalance>>(), [], new Dictionary<Guid, IReadOnlyDictionary<int, decimal>>(), [], []);
            records.AddRange(outbounds.SelectMany(o => WarehouseExceptionRules.OfOutbound(o, now, thresholds, recalled,
                (node, sku) => WarehouseExceptionRules.StagingDifference(node, sku, staging ?? empty))));
        }

        var listed = records.Where(r => Wants(r.Kind)).OrderBy(r => r.OpenedAtUtc).ThenBy(r => r.ExceptionId, StringComparer.Ordinal).ToList();
        var page = listed.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).Select(Dto).ToList();
        return Response<WarehouseExceptionPageDto>.Success(new WarehouseExceptionPageDto(page, listed.Count, paging.Page, paging.PageSize));
    }

    private async Task<StagingInputs> StagingAsync(IReadOnlyList<Guid> nodes, IReadOnlyList<WarehouseTask> openTasks, CancellationToken ct)
    {
        var rows = await _stock.RowsAtAsync(nodes, ct);
        var balances = nodes.ToDictionary(n => n,
            n => (IReadOnlyList<StagingBalance>)rows.Where(r => r.LocationId == n).Select(r => new StagingBalance(r.SkuId, r.LotId, r.OnHand)).ToList());
        var nodeOutbounds = await _reads.OutboundsAtStagingAsync(nodes, ct);
        var bound = nodeOutbounds.Where(o => !WarehouseExceptionRules.UnboundStatuses.Contains(o.Status)).Select(o => o.Id).ToList();
        var pickTasks = await _outbounds.PickTasksOfManyAsync(bound, ct);
        var picked = pickTasks.Where(t => t.OutboundId is not null).GroupBy(t => t.OutboundId!.Value)
            .ToDictionary(g => g.Key, g => OutboundFlowRules.PickedByAllocation(g));
        var openFrom = await _tasks.OpenTasksFromAsync(nodes, ct);
        var pending = openTasks.Where(t => t.Confirmations.Any(c => c.Status == ConfirmationStatus.Pending)).ToList();
        return new StagingInputs(balances, nodeOutbounds, picked, openFrom, pending);
    }

    private static WarehouseExceptionDto Dto(WarehouseExceptionRecord r)
    {
        var details = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in r.Details)
        {
            var wire = Wire(value);
            if (wire is not null || NullableDetails.Contains(key)) details[key] = wire;
        }

        return new WarehouseExceptionDto(r.ExceptionId, r.Kind.ToString(), r.TaskId?.ToString("D"), r.OutboundId?.ToString("D"), r.WarehouseId.ToString("D"), r.Code, details,
            PutawayTaskSupport.Wire(r.OpenedAtUtc), "Open");
    }

    /// <summary>A detail value on the wire: decimals as quantity text, times UTC, ids "D", lot differences as objects; null stays null.</summary>
    private static object? Wire(object? value) => value switch
    {
        null => null,
        decimal d => InventoryQuantity.ToWire(d),
        DateTimeOffset t => PutawayTaskSupport.Wire(t),
        Guid g => g.ToString("D"),
        string s => s,
        int i => i,
        StagingLotDifference x => Lot(x),
        IEnumerable<StagingLotDifference> lots => lots.Select(Lot).ToList(),
        IEnumerable<string> texts => texts.ToList(),
        IEnumerable e => e.Cast<object?>().Select(Wire).ToList(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    private static Dictionary<string, object?> Lot(StagingLotDifference x)
    {
        var lot = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (x.LotId is not null) lot["lotId"] = x.LotId;
        lot["onHand"] = InventoryQuantity.ToWire(x.OnHand);
        lot["bound"] = InventoryQuantity.ToWire(x.Bound);
        lot["difference"] = InventoryQuantity.ToWire(x.Difference);
        return lot;
    }

    // ── GET /activity ─────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<WarehouseActivityPageDto>> Handle(GetWarehouseActivityListQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.From) || string.IsNullOrWhiteSpace(request.To))
            return WarehouseSupport.Invalid<WarehouseActivityPageDto>("from and to are required (date-time).");
        if (!TryInstant(request.From, out var from) || !TryInstant(request.To, out var to))
            return WarehouseSupport.Invalid<WarehouseActivityPageDto>("from and to must be RFC 3339 date-times.");
        if (to < from) return WarehouseSupport.Invalid<WarehouseActivityPageDto>("to must not be before from.");
        if (to - from > TimeSpan.FromDays(MaxActivityDays)) return WarehouseSupport.Invalid<WarehouseActivityPageDto>($"The range must be at most {MaxActivityDays} days.");

        WarehouseActivitySource? source = null;
        if (request.Kind is not null)
        {
            if (!Enum.TryParse<WarehouseActivitySource>(request.Kind, false, out var s) || !Enum.IsDefined(s) || request.Kind != s.ToString())
                return WarehouseSupport.Invalid<WarehouseActivityPageDto>($"kind must be one of {string.Join(", ", Enum.GetNames<WarehouseActivitySource>())}.");
            source = s;
        }

        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var id)) return WarehouseSupport.Invalid<WarehouseActivityPageDto>("warehouseId must be a UUID.");
            warehouse = id;
        }

        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging)
            return WarehouseSupport.Invalid<WarehouseActivityPageDto>("page must be ≥ 1 and pageSize 1-200.");
        if ((long)paging.Page * paging.PageSize > MaxActivityDepth)
            return WarehouseSupport.Invalid<WarehouseActivityPageDto>($"page × pageSize must be at most {MaxActivityDepth}; narrow the range.");

        var page = await _reads.ActivityAsync(new WarehouseActivityQuery(from, to, warehouse, source, paging.Page, paging.PageSize), ct);
        var items = page.Items.Select(r => new WarehouseActivityDto(r.Source.ToString(), r.Kind, PutawayTaskSupport.Wire(r.AtUtc), r.RecordId.ToString("D"), r.RecordNumber,
            r.Seq, r.WarehouseId?.ToString("D"), r.ActorUserId is { } actor ? new TaskPersonDto(actor.ToString("D"), r.ActorName) : null, r.System,
            r.Data.Where(d => d.Value is not null).ToDictionary(d => d.Key, d => d.Value!, StringComparer.Ordinal))).ToList();
        return Response<WarehouseActivityPageDto>.Success(new WarehouseActivityPageDto(items, page.Total, paging.Page, paging.PageSize, PutawayTaskSupport.Wire(from),
            PutawayTaskSupport.Wire(to)));
    }

    private static bool TryInstant(string raw, out DateTimeOffset value)
        => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value)
           && raw.Contains('T', StringComparison.Ordinal);

    // ── GET /integration/status ───────────────────────────────────────────────────────────────────────────

    public async Task<Response<WarehouseIntegrationStatusDto>> Handle(GetWarehouseIntegrationStatusQuery request, CancellationToken ct)
    {
        var now = _support.Now();
        var s = await _reads.IntegrationAsync(ct);
        long? age = s.OldestPendingAtUtc is { } oldest ? Math.Max(0L, (long)(now - oldest).TotalSeconds) : null;
        return Response<WarehouseIntegrationStatusDto>.Success(new WarehouseIntegrationStatusDto(
            new IntegrationEventQueueDto(s.OutboxPending, s.OutboxDelivered, age, PutawayTaskSupport.Wire(s.OldestPendingAtUtc)),
            new IntegrationAwaitingDispatchDto(s.ReadyToShip, PutawayTaskSupport.Wire(s.OldestReadyAtUtc)),
            new IntegrationLastSyncDto(PutawayTaskSupport.Wire(s.LastShippedAtUtc)),
            new IntegrationPendingConfirmationsDto(s.PendingPutawayConfirmations, s.PendingPickConfirmations, s.PendingPutawayConfirmations + s.PendingPickConfirmations),
            PutawayTaskSupport.Wire(now)));
    }

    private static Response<T> Unavailable<T>(string message)
        => WarehouseSupport.Fail<T>(InventoryErrorCodes.InventoryUnavailable, message + " Nothing is listed — try again.", 503);
}
