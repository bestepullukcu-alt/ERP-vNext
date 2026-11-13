using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// The sweeper's command (pack §8.6, §21; AC-25): one approved line whose correction is still Pending — the ledger KEY first (Committed → Posted, no
/// second movement; reversed / another payload → IntegrityError), and when nothing holds it the SAME stored request. A line that is no longer Pending
/// writes nothing (200 <c>false</c>); an unknown outcome writes nothing and fails (503 — the next tick tries again).
/// </summary>
public sealed class ReconcilePendingCountAdjustmentHandler : IRequestHandler<ReconcilePendingCountAdjustmentCommand, Response<bool>>
{
    private readonly ICycleCountRepository _repository;
    private readonly CycleCountPostingEngine _engine;

    public ReconcilePendingCountAdjustmentHandler(ICycleCountRepository repository, CycleCountPostingEngine engine)
    {
        _repository = repository;
        _engine = engine;
    }

    public async Task<Response<bool>> Handle(ReconcilePendingCountAdjustmentCommand request, CancellationToken ct)
    {
        var line = await _repository.FindLineByIdAsync(request.LineId, ct);
        var count = line is null ? null : await _repository.FindAsync(line.CountId, ct);
        if (line is null || count is null || line.Status != CycleCountLineStatus.Approved || line.Adjustment?.Status != CycleCountAdjustmentStatus.Pending)
            return Response<bool>.Success(false);

        var result = await _engine.RecoverAsync(count, line, ct);
        if (result.Kind == CycleCountPostingKind.Unknown)
            return CycleCountSupport.Fail<bool>(CycleCountErrorCodes.InventoryUnavailable, "The stock answer was not received; the correction stays pending.", 503);
        var written = await _engine.WriteResultAsync<bool>(count.Id, line.Id, result, ct);
        return Response<bool>.Success(written.Kind == CycleCountLineWriteKind.Written);
    }
}
