using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>addCycleCountLine</c> — found stock (pack §4.2, §8.9, §12; AC-16). Order: shape (400: SKU level, <c>COUNT_QUANTITY_INVALID</c>) → key replay →
/// count (404) → count state Released / Counting (409) → the bin is one of the count's <c>ScopeBinIds</c> and, when <c>ScopeSkuIds</c> is set, the SKU
/// one of them (422 <c>COUNT_LINE_OUT_OF_SCOPE</c> — a SKU outside the filter has no expected quantity and would drift forever, SB1/F-7) →
/// PRODUCT-MASTER behaviour (503 / 422) → lot: a lot-controlled SKU names one (400 <c>COUNT_LOT_REQUIRED</c>), and it is a lot REGISTERED in
/// MOD-0174 for this SKU (422 <c>COUNT_LOT_UNKNOWN</c>, Q-10 = A; a lot on a SKU that is not lot-controlled is 422 <c>LOT_NOT_ALLOWED</c>) →
/// a serial-controlled SKU is counted whole (400). The line: <c>Origin = Found</c>, <c>ExpectedQuantity = 0</c>, status AVAILABLE (Q-8 = A),
/// born <c>Counted</c>. A key the count already has is 409 <c>COUNT_LINE_DUPLICATE</c> (the line key index). Nothing is written on a refusal.
/// </summary>
public sealed class AddCountLineHandler : CycleCountLineCommandHandler, IRequestHandler<AddCountLineCommand, Response<CycleCountLineDto>>
{
    private readonly CycleCountProductFlags _products;
    private readonly ILotRepository _lots;

    public AddCountLineHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountProductFlags products, ILotRepository lots)
        : base(repository, support)
    {
        _products = products;
        _lots = lots;
    }

    public async Task<Response<CycleCountLineDto>> Handle(AddCountLineCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpAddCycleCountLine;
        var input = request.Line;
        if (!WireNames.TryParseSkuLevel(input.SkuLevel, out var level))
            return CycleCountSupport.Invalid<CycleCountLineDto>("item.skuLevel must be Gsku, Lsku, FinishedGood or Item.");
        if (!CycleCountRules.TryParseCounted(input.CountedQuantity, out var counted))
            return QuantityInvalid("countedQuantity must be a decimal string ≥ 0 with at most 6 fraction digits.");
        if (!TryScanRef(input.ScanRef, out var scanRef))
            return CycleCountSupport.Invalid<CycleCountLineDto>($"scanRef must be at most {CycleCountContract.MaxScanRefLength} characters.");
        var lotText = CycleCountSupport.Trimmed(input.LotId);

        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("countId", request.CountId.ToString("D")),
            ("locationId", input.LocationId.ToString("D")), ("itemId", input.ItemId.ToString("D")), ("skuId", input.SkuId.ToString("D")),
            ("skuLevel", WireNames.ToWire(level)), ("lotId", lotText), ("countedQuantity", InventoryQuantity.ToWire(counted)), ("scanRef", scanRef));
        if (await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var count = await Repository.FindAsync(request.CountId, ct);
        if (count is null) return CycleCountSupport.NotFound<CycleCountLineDto>();
        if (NotCounting(count) is { } notCounting) return notCounting;
        if (!count.ScopeBinIds.Contains(input.LocationId))
            return OutOfScope("The location is not a bin of this count's scope.", ("locationId", input.LocationId.ToString("D")));
        if (count.ScopeSkuIds.Count > 0 && !count.ScopeSkuIds.Contains(input.SkuId))
            return OutOfScope("The SKU is outside this count's SKU filter.", ("skuId", input.SkuId.ToString("D")));

        var (behaviour, refusal) = await _products.LineBehaviourAsync<CycleCountLineDto>(input.ItemId, input.SkuId, level, ct);
        if (refusal is not null) return refusal;
        if (behaviour!.LotControlled && lotText is null)
            return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.LotRequired, "lotId is required: the SKU is lot-controlled.", 400);
        if (!behaviour.LotControlled && lotText is not null)
            return CycleCountSupport.Fail<CycleCountLineDto>(InventoryErrorCodes.LotNotAllowed, "lotId is not allowed: the SKU is not lot-controlled.", 422,
                new Dictionary<string, object?> { ["lotId"] = lotText });
        if (lotText is not null && !await IsRegisteredLotOfAsync(lotText, input.SkuId, level, ct))
            return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.LotUnknown, "lotId does not name a lot registered for this SKU.", 422,
                new Dictionary<string, object?> { ["lotId"] = lotText });
        if (behaviour.SerialControlled && !CycleCountRules.IsWhole(counted))
            return QuantityInvalid("A serial-controlled SKU is counted in whole units.");
        // DL-3 (pack §8.5, r6 SB2/F-2): a recount's snapshot saw this key with stock but it was not selected — its balance is in no expected quantity
        if (count.RecountExcludedKeys.Any(k => k.LocationId == input.LocationId && k.SkuId == input.SkuId && k.SkuLevel == level
                                               && string.Equals(k.LotId, lotText, StringComparison.Ordinal) && k.StockStatus == StockStatus.Available))
            return OutOfScope("The recount's snapshot holds this key but it was not selected for recount.", ("lotId", lotText ?? string.Empty));

        for (var attempt = 1; attempt <= CycleCountContract.MaxHeadAttempts; attempt++)
        {
            if (attempt > 1)
            {
                count = await Repository.FindAsync(request.CountId, ct);
                if (count is null) return CycleCountSupport.NotFound<CycleCountLineDto>();
                if (NotCounting(count) is { } stopped) return stopped;
            }

            var now = Support.Now();
            var found = new CycleCountLine
            {
                Id = Guid.NewGuid(), CreatedAt = now, Version = 1, CountId = count!.Id, WarehouseId = count.WarehouseId, LocationId = input.LocationId,
                ItemId = input.ItemId, SkuId = input.SkuId, SkuLevel = level, LotId = lotText, StockStatus = StockStatus.Available,
                BaseUomId = behaviour.BaseUomId, Origin = CycleCountLineOrigin.Found, ExpectedQuantity = 0m, SerialControlled = behaviour.SerialControlled,
                CountedQuantity = counted, CountedBy = Support.UserId, CountedAt = now, ScanRef = scanRef, Status = CycleCountLineStatus.Counted
            };
            var (head, lineAdded) = Head(count, 1, CycleCountEventKind.LineAdded, now, found.Id, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["locationId"] = input.LocationId.ToString("D"), ["itemId"] = input.ItemId.ToString("D"), ["skuId"] = input.SkuId.ToString("D"),
                ["skuLevel"] = WireNames.ToWire(level), ["lotId"] = lotText, ["countedQuantity"] = InventoryQuantity.ToWire(counted), ["scanRef"] = scanRef
            }, op, request.IdempotencyKey, fingerprint);

            try
            {
                var outcome = await Repository.InTransactionAsync(async (session, tx) =>
                {
                    if (!await session.ReplaceCountAsync(head, count.Version, tx)) return new CycleCountWriteOutcome(0, found.Id);
                    await session.InsertLinesAsync([found], tx);
                    await session.InsertEventsAsync([lineAdded], tx);
                    return new CycleCountWriteOutcome(1, found.Id);
                }, ct);
                if (outcome.Affected == 1)
                    return Response<CycleCountLineDto>.Success(CycleCountReads.Line(found, CycleCountReads.ShowsExpected(head, callerReadsVariances: false)), 201);
            }
            catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Line)
            {
                return await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct)
                       ?? CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.LineDuplicate,
                           "The count already has a line for this bin, SKU, level, lot and status.", 409);
            }
            catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Event)
            {
                // a concurrent twin of this request committed first — answered below
            }

            if (await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } committed) return committed;
        }

        return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.Conflict, "The count kept changing while the line was added; retry.", 409);
    }

    /// <summary>Q-10 = A: the lot is a MOD-0174 registered lot (canonical id) of this tenant and of this SKU — a typed text or another SKU's lot is not.</summary>
    private async Task<bool> IsRegisteredLotOfAsync(string lotId, Guid skuId, SkuLevel level, CancellationToken ct)
        => LotPostingRules.IsRegisteredLotForm(lotId, out var id) && await _lots.FindAsync(id, ct) is { } lot && lot.SkuId == skuId && lot.SkuLevel == level;

    private static Response<CycleCountLineDto> OutOfScope(string message, (string Name, string Value) detail)
        => CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.LineOutOfScope, message, 422,
            new Dictionary<string, object?> { [detail.Name] = detail.Value });
}
