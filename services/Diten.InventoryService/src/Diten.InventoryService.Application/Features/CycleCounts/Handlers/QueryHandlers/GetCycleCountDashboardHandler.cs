using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Handlers.QueryHandlers;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Queries;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.QueryHandlers;

/// <summary>
/// <c>getCycleCountDashboard</c> (pack §8.13, AC-40): every number is counted at read time from the stored counts and lines of the caller's company —
/// no stored total. The period is the last 31 days (MOD-0181's clock). The manual <c>COUNT_*</c> movements are the Variance Trace's own
/// classification (<see cref="CountVarianceReads.ManualRowsAsync"/>, pack §2.4 two-way rule) over the selected warehouse — without a warehouse there is
/// no manual count (the read is warehouse-bound, §2.4).
/// </summary>
public sealed class GetCycleCountDashboardHandler : IRequestHandler<GetCycleCountDashboardQuery, Response<CycleCountDashboardDto>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CountVarianceReads _reads;
    private readonly CycleCountSupport _support;

    public GetCycleCountDashboardHandler(ICycleCountRepository repository, CountVarianceReads reads, CycleCountSupport support)
    {
        _repository = repository;
        _reads = reads;
        _support = support;
    }

    public async Task<Response<CycleCountDashboardDto>> Handle(GetCycleCountDashboardQuery request, CancellationToken ct)
    {
        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!CycleCountSupport.TryParseId(request.WarehouseId, out var id)) return CycleCountSupport.Invalid<CycleCountDashboardDto>("warehouseId must be a canonical UUID.");
            warehouse = id;
        }

        var now = _support.Now();
        var start = now - TimeSpan.FromDays(CycleCountContract.MaxManualWindowDays);
        var counts = await _repository.DashboardCountsAsync(warehouse, start, ct);
        int? manual = null;
        if (warehouse is { } w)
        {
            try
            {
                manual = (await _reads.ManualRowsAsync(w, start, now.AddSeconds(1), ct)).Count;
            }
            catch (Domain.CycleCounting.Repositories.CycleCountInventoryUnavailableException)
            {
                return CycleCountSupport.Fail<CycleCountDashboardDto>(CycleCountErrorCodes.InventoryUnavailable, "The ledger could not be read.", 503);
            }
        }

        return Response<CycleCountDashboardDto>.Success(new CycleCountDashboardDto
        {
            WarehouseId = warehouse?.ToString("D"),
            PeriodDays = CycleCountContract.MaxManualWindowDays,
            PeriodStart = CycleCountSupport.Wire(start),
            OpenCounts = counts.OpenCounts,
            AwaitingApprovalLines = counts.AwaitingApprovalLines,
            RefusedAdjustments = counts.RefusedAdjustments,
            CountedLines = counts.CountedLines,
            ExactLines = counts.ExactLines,
            AccuracyPercent = counts.CountedLines == 0
                ? null
                : Math.Round(counts.ExactLines * 100m / counts.CountedLines, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture),
            ManualCountMovements = manual
        });
    }
}
