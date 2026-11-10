using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Queries;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.QueryHandlers;

/// <summary><c>getCycleCountSettings</c> (pack §4.4; AC-49): the company's record, or the defaults (0 / 0 / 2 / 2000, version 0).</summary>
public sealed class GetCycleCountSettingsHandler : IRequestHandler<GetCycleCountSettingsQuery, Response<CycleCountSettingsDto>>
{
    private readonly ICycleCountRepository _repository;

    public GetCycleCountSettingsHandler(ICycleCountRepository repository) => _repository = repository;

    public async Task<Response<CycleCountSettingsDto>> Handle(GetCycleCountSettingsQuery request, CancellationToken ct)
        => Response<CycleCountSettingsDto>.Success(CycleCountReads.Settings(await _repository.FindSettingsAsync(ct)));
}

/// <summary>
/// The Variance Trace reads (pack §2.4, §8.13; AC-47). A <c>Count</c> row is a 0181 line with its variance (the caller holds
/// <c>cycle-count.variances.read</c>, so a blind count's quantities are written once it is submitted) and the ledger movement that is its correction BY
/// THE TWO-WAY RULE (<see cref="CycleCountDecisionRules.IsCountMovement"/> — never the line's transaction id alone). A <c>Manual</c> row is a
/// <c>COUNT_*</c> movement on the warehouse's bins in the window that matches no line; one that declares MOD-0181 is flagged
/// <c>sourceMismatch</c>. 0181 only READS the ledger here (Q-21 = A).
/// </summary>
public sealed class CountVarianceReads
{
    private readonly ICycleCountRepository _repository;

    public CountVarianceReads(ICycleCountRepository repository) => _repository = repository;

    /// <summary>The count rows of <paramref name="lines"/> — numbers and the matched correction movements read in two batched Finds.</summary>
    public async Task<List<(DateTimeOffset At, CountVarianceDto Row)>> CountRowsAsync(IReadOnlyList<CycleCountLine> lines, CancellationToken ct)
    {
        var counts = (await _repository.FindManyAsync(lines.Select(l => l.CountId).Distinct().ToList(), ct)).ToDictionary(c => c.Id);
        var keys = lines.Where(l => l.Adjustment is not null).Select(l => l.Adjustment!.IdempotencyKey).ToList();
        var movements = (await _repository.MovementsByKeysAsync(keys, ct)).ToDictionary(m => m.IdempotencyKey, StringComparer.Ordinal);
        return lines.Select(l =>
        {
            counts.TryGetValue(l.CountId, out var count);
            var match = l.Adjustment is { } a && movements.TryGetValue(a.IdempotencyKey, out var m) && CycleCountDecisionRules.IsCountMovement(Facts(m), l) ? m : null;
            return (l.CountedAt ?? l.CreatedAt, new CountVarianceDto
            {
                Source = CycleCountContract.RowSourceCount,
                CountNumber = count?.Number,
                Line = CycleCountReads.Line(l, count is null || CycleCountReads.ShowsExpected(count, callerReadsVariances: true)),
                LedgerTransactionId = match?.TransactionId.ToString("D"),
                LedgerTransactionNumber = match?.TransactionNumber
            });
        }).ToList();
    }

    /// <summary>The manual rows: the window's COUNT_* movements on the warehouse's nodes minus those that ARE a line's correction.</summary>
    public async Task<List<(DateTimeOffset At, CountVarianceDto Row)>> ManualRowsAsync(Guid warehouseId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var nodes = (await _repository.WarehouseNodesAsync(warehouseId, ct)).Append(warehouseId).Distinct().ToList();
        var movements = await _repository.CountMovementsAsync(new CycleCountManualRequest(nodes, from, to), ct);
        var lineIds = movements.Select(m => CycleCountDecisionRules.LineOfKey(m.IdempotencyKey)).OfType<Guid>().Distinct().ToList();
        var lines = (await _repository.FindLinesAsync(lineIds, ct)).ToDictionary(l => l.Id);
        return movements
            .Where(m => !CycleCountDecisionRules.IsCountMovement(Facts(m),
                CycleCountDecisionRules.LineOfKey(m.IdempotencyKey) is { } id && lines.TryGetValue(id, out var line) ? line : null))
            .Select(m => (m.CreatedAt, new CountVarianceDto { Source = CycleCountContract.RowSourceManual, Movement = Manual(m) }))
            .ToList();
    }

    public static CycleCountMovementFacts Facts(CycleCountLedgerMovementRow m)
        => new(m.TransactionId, m.IdempotencyKey, m.SourceModule, m.SourceDocumentId, m.SourceLineId, m.CreatedAt);

    private static CountManualMovementDto Manual(CycleCountLedgerMovementRow m)
    {
        var gain = m.MovementType == MovementType.CountGain;
        var status = gain ? m.ToStockStatus : m.FromStockStatus;
        return new CountManualMovementDto
        {
            TransactionId = m.TransactionId.ToString("D"),
            TransactionNumber = m.TransactionNumber,
            MovementType = WireNames.ToWire(m.MovementType),
            WarehouseId = gain ? m.ToWarehouseId : m.FromWarehouseId,
            LocationId = gain ? m.ToLocationId : m.FromLocationId,
            Item = new CycleCountItemRefDto(m.ItemId.ToString("D"), m.SkuId.ToString("D"), WireNames.ToWire(m.SkuLevel)),
            LotId = m.LotId,
            StockStatus = status is { } s ? WireNames.ToWire(s) : null,
            Quantity = InventoryQuantity.ToWire(m.Quantity),
            UomId = m.UomId,
            SourceModule = m.SourceModule,
            SourceType = m.SourceType,
            SourceDocumentId = m.SourceDocumentId,
            SourceLineId = m.SourceLineId,
            CreatedBy = m.CreatedBy.ToString("D"),
            CreatedAt = CycleCountSupport.Wire(m.CreatedAt),
            TransactionDate = CycleCountSupport.Wire(m.TransactionDate),
            PostingDate = CycleCountSupport.Wire(m.PostingDate),
            SourceMismatch = CycleCountDecisionRules.DeclaresCount(Facts(m))
        };
    }
}

/// <summary>
/// <c>listCountVariances</c> (pack §2.4, §12): <c>source</c> = <c>count</c> (default; the submitted lines, any filter, paged in the store) |
/// <c>manual</c> | <c>all</c> — those two need <c>warehouseId</c> and <c>from</c> &lt; <c>to</c> at most 31 days apart (400 otherwise) and are paged after
/// the classification; newest first.
/// </summary>
public sealed class GetCountVarianceListHandler : IRequestHandler<GetCountVarianceListQuery, Response<CountVariancePageDto>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CountVarianceReads _reads;

    public GetCountVarianceListHandler(ICycleCountRepository repository, CountVarianceReads reads)
    {
        _repository = repository;
        _reads = reads;
    }

    public async Task<Response<CountVariancePageDto>> Handle(GetCountVarianceListQuery request, CancellationToken ct)
    {
        var source = request.Source ?? CycleCountContract.SourceCount;
        if (source is not (CycleCountContract.SourceCount or CycleCountContract.SourceManual or CycleCountContract.SourceAll))
            return Invalid("source must be count, manual or all.");
        if (CycleCountPaging.Parse(request.Page, request.PageSize) is not { } paging) return Invalid("page must be ≥ 1 and pageSize 1-200.");
        var (page, pageSize) = paging;
        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!CycleCountSupport.TryParseId(request.WarehouseId, out var w)) return Invalid("warehouseId must be a canonical UUID.");
            warehouse = w;
        }

        Guid? sku = null;
        if (request.SkuId is not null)
        {
            if (!CycleCountSupport.TryParseId(request.SkuId, out var s)) return Invalid("skuId must be a canonical UUID.");
            sku = s;
        }

        if (!TryInstant(request.From, out var from) || !TryInstant(request.To, out var to)) return Invalid("from / to must be ISO dates or UTC date-times.");
        var statuses = new List<CycleCountLineStatus>();
        foreach (var text in request.Statuses)
        {
            if (!Enum.TryParse<CycleCountLineStatus>(text, ignoreCase: false, out var status) || !Enum.IsDefined(status) || text.Any(char.IsDigit))
                return Invalid("status must be a line status.");
            statuses.Add(status);
        }

        if (source != CycleCountContract.SourceCount)
        {
            if (warehouse is null || from is null || to is null)
                return Invalid("source manual / all needs warehouseId, from and to (at most 31 days).");
            if (to <= from || to.Value - from.Value > TimeSpan.FromDays(CycleCountContract.MaxManualWindowDays))
                return Invalid("from must be before to, at most 31 days apart.");
        }

        if (source == CycleCountContract.SourceCount)
        {
            var set = await _repository.VarianceLinesAsync(new CountVarianceLineQuery(warehouse, statuses, from, to, sku, request.LotId, page, pageSize), ct);
            var rows = await _reads.CountRowsAsync(set.Items, ct);
            return Response<CountVariancePageDto>.Success(new CountVariancePageDto(rows.Select(r => r.Row).ToList(), set.Total, page, pageSize));
        }

        try
        {
            var all = new List<(DateTimeOffset At, CountVarianceDto Row)>();
            if (source == CycleCountContract.SourceAll)
            {
                var set = await _repository.VarianceLinesAsync(new CountVarianceLineQuery(warehouse, statuses, from, to, sku, request.LotId, 1, 1, All: true), ct);
                all.AddRange(await _reads.CountRowsAsync(set.Items, ct));
            }

            all.AddRange((await _reads.ManualRowsAsync(warehouse!.Value, from!.Value, to!.Value, ct))
                .Where(r => (sku is null || r.Row.Movement!.Item.SkuId == sku.Value.ToString("D")) && (request.LotId is null || r.Row.Movement!.LotId == request.LotId)));
            var ordered = all.OrderByDescending(r => r.At).ToList();
            return Response<CountVariancePageDto>.Success(new CountVariancePageDto(
                ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Row).ToList(), ordered.Count, page, pageSize));
        }
        catch (CycleCountInventoryUnavailableException)
        {
            return CycleCountSupport.Fail<CountVariancePageDto>(CycleCountErrorCodes.InventoryUnavailable, "The ledger could not be read.", 503);
        }
    }

    private static Response<CountVariancePageDto> Invalid(string message) => CycleCountSupport.Invalid<CountVariancePageDto>(message);

    /// <summary>A date (<c>yyyy-MM-dd</c> = its UTC midnight) or a UTC date-time; null when absent.</summary>
    private static bool TryInstant(string? raw, out DateTimeOffset? value)
    {
        value = null;
        if (raw is null) return true;
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            value = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return true;
        }

        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            || !raw.EndsWith('Z')) return false;
        value = at;
        return true;
    }
}

/// <summary><c>getCountVariance</c>: one counted line (404 <c>COUNT_LINE_NOT_FOUND</c> before its submit) with its number and its correction movement.</summary>
public sealed class GetCountVarianceByIdHandler : IRequestHandler<GetCountVarianceByIdQuery, Response<CountVarianceDto>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CountVarianceReads _reads;

    public GetCountVarianceByIdHandler(ICycleCountRepository repository, CountVarianceReads reads)
    {
        _repository = repository;
        _reads = reads;
    }

    public async Task<Response<CountVarianceDto>> Handle(GetCountVarianceByIdQuery request, CancellationToken ct)
    {
        var line = await _repository.FindLineByIdAsync(request.LineId, ct);
        if (line?.VarianceQuantity is null)
            return CycleCountSupport.Fail<CountVarianceDto>(CycleCountErrorCodes.LineNotFound, "Cycle count line not found.", 404);
        return Response<CountVarianceDto>.Success(Assert(await _reads.CountRowsAsync([line], ct)).Row);
    }

    private static (DateTimeOffset At, CountVarianceDto Row) Assert(List<(DateTimeOffset At, CountVarianceDto Row)> rows) => rows.Single();
}
