using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// MOD-0175's implementation of the recall's in-process port (MOD-0177 pack §8.4, CT-R2 / CT-R3; QUARANTINE 1.3.0): ONE bucket per call
/// through the SAME <c>quarantineStock</c> command the API sends (mode Manual — every stock / lot / reservation check of
/// <c>QuarantineStockHandler</c> runs unchanged), with the origin (MOD-0177 / RECALL_CASE) the hold records as <c>Source = Recall</c>. The
/// actor is the resolved scope user. Nothing here decides for 0175, releases anything or touches a reservation.
/// </summary>
public sealed class QuarantineRecallRequester : IRecallQuarantineRequester
{
    public const string RecallModule = QuarantineContract.RecallOriginModule;
    public const string RecallSourceType = QuarantineContract.SourceTypeRecallCase;

    private readonly IMediator _mediator;
    private readonly IQuarantineRepository _repository;

    public QuarantineRecallRequester(IMediator mediator, IQuarantineRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    public async Task<RecallQuarantineAnswer> QuarantineAsync(RecallQuarantineCall call, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(new QuarantineStockCommand("Manual", call.Buckets.Count == 1 ? call.Buckets[0].LotId : null,
            call.Buckets.Select(b => new BucketInput(b.ItemId, b.SkuId, b.SkuLevel, b.WarehouseId, b.LocationId, b.Quantity, b.LotId)).ToList(), call.Reason, null, call.IdempotencyKey,
            new HoldOrigin(RecallModule, RecallSourceType, call.CaseId.ToString("D"), call.CaseNumber)), cancellationToken);
        if (!response.IsSuccessful)
        {
            return new RecallQuarantineAnswer(response.StatusCode, [], response.ErrorCode, response.Errors.FirstOrDefault(), response.ErrorDetails);
        }

        var holds = (response.Data ?? []).Select(h => new RecallHoldView(h.HoldId, h.HoldNumber, h.Status, h.ReviewState, h.Quantity, h.Lot?.LotId,
            h.Warehouse.Id, h.Location.Id)).ToList();
        return new RecallQuarantineAnswer(response.StatusCode, holds, null, null, null);
    }

    public async Task<IReadOnlyList<RecallHoldView>> HoldsAsync(IReadOnlyCollection<string> holdIds, CancellationToken cancellationToken = default)
    {
        var views = new List<RecallHoldView>();
        foreach (var id in holdIds.Distinct(StringComparer.Ordinal))
        {
            if (Guid.TryParse(id, out var holdId) && await _repository.FindHoldAsync(holdId, cancellationToken) is { } hold) views.Add(View(hold));
        }

        return views;
    }

    public async Task<IReadOnlyList<RecallHoldView>> OpenHoldsOfLotAsync(string lotId, CancellationToken cancellationToken = default)
    {
        var page = await _repository.ListHoldsAsync(new HoldQuery(null, null, null, null, null, null, lotId, null, 1, 500), cancellationToken);
        return page.Items.Where(h => h.Status != HoldStatus.Decided).Select(View).ToList();
    }

    private static RecallHoldView View(QuarantineHold h)
        => new(h.Id.ToString("D"), h.HoldNumber, h.Status.ToString(), h.ReviewState.ToString(), h.Quantity, h.LotId, h.WarehouseId, h.LocationId);
}
