using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>recordCycleCountLine</c> (pack §4.2, §8.3, §12; AC-14). Order: quantity shape (400 <c>COUNT_QUANTITY_INVALID</c> — negative, more than six
/// fraction digits, not a decimal string) → key replay → count (404) → count state Released / Counting (409) → line (404
/// <c>COUNT_LINE_NOT_FOUND</c>) → line If-Match (409) → line state Open / Counted (409) → a serial-controlled line takes whole numbers (400).
/// The line becomes <c>Counted</c> with <c>CountedBy</c> = the caller and <c>CountedAt</c> = MOD-0181's clock (never from the body); the first entry
/// takes the count to <c>Counting</c>. The <c>LineCounted</c> event carries the counted quantity only (F-13) — before and after.
/// </summary>
public sealed class RecordCountLineHandler : CycleCountLineCommandHandler, IRequestHandler<RecordCountLineCommand, Response<CycleCountLineDto>>
{
    public RecordCountLineHandler(ICycleCountRepository repository, CycleCountSupport support) : base(repository, support)
    {
    }

    public async Task<Response<CycleCountLineDto>> Handle(RecordCountLineCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpRecordCycleCountLine;
        if (!CycleCountRules.TryParseCounted(request.Entry.CountedQuantity, out var counted))
            return QuantityInvalid("countedQuantity must be a decimal string ≥ 0 with at most 6 fraction digits.");
        if (!TryScanRef(request.Entry.ScanRef, out var scanRef))
            return CycleCountSupport.Invalid<CycleCountLineDto>($"scanRef must be at most {CycleCountContract.MaxScanRefLength} characters.");

        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("countId", request.CountId.ToString("D")), ("lineId", request.LineId.ToString("D")),
            ("countedQuantity", InventoryQuantity.ToWire(counted)), ("scanRef", scanRef));
        if (await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        for (var attempt = 1; attempt <= CycleCountContract.MaxHeadAttempts; attempt++)
        {
            var count = await Repository.FindAsync(request.CountId, ct);
            if (count is null) return CycleCountSupport.NotFound<CycleCountLineDto>();
            if (NotCounting(count) is { } notCounting) return notCounting;
            var line = await Repository.FindLineAsync(count.Id, request.LineId, ct);
            if (line is null) return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.LineNotFound, "Cycle count line not found.", 404);
            if (line.Version != request.ExpectedVersion) return LineConflict();
            if (!CycleCountRules.TakesEntry(line))
                return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.InvalidState, $"The line is {line.Status}; it takes no entry.", 409,
                    new Dictionary<string, object?> { ["status"] = line.Status.ToString() });
            if (line.SerialControlled && !CycleCountRules.IsWhole(counted))
                return QuantityInvalid("A serial-controlled line is counted in whole units.");

            var now = Support.Now();
            var entered = new Domain.CycleCounting.Entities.CycleCountLine
            {
                Id = line.Id, TenantId = line.TenantId, LegalEntityId = line.LegalEntityId, CreatedAt = line.CreatedAt, UpdatedAt = now, Version = line.Version + 1,
                CountId = line.CountId, WarehouseId = line.WarehouseId, LocationId = line.LocationId, ItemId = line.ItemId, SkuId = line.SkuId,
                SkuLevel = line.SkuLevel, LotId = line.LotId, StockStatus = line.StockStatus, BaseUomId = line.BaseUomId, Origin = line.Origin,
                RecountOfLineId = line.RecountOfLineId, RecountDepth = line.RecountDepth, ExpectedQuantity = line.ExpectedQuantity,
                SerialControlled = line.SerialControlled, CountedQuantity = counted, CountedBy = Support.UserId, CountedAt = now, ScanRef = scanRef,
                Status = CycleCountLineStatus.Counted
            };
            var (head, lineCounted) = Head(count, 0, CycleCountEventKind.LineCounted, now, line.Id, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["previousCountedQuantity"] = line.CountedQuantity is { } previous ? InventoryQuantity.ToWire(previous) : null,
                ["countedQuantity"] = InventoryQuantity.ToWire(counted), ["scanRef"] = scanRef, ["lineVersion"] = CycleCountSupport.Int(entered.Version)
            }, op, request.IdempotencyKey, fingerprint);

            try
            {
                var outcome = await Repository.InTransactionAsync(async (session, tx) =>
                {
                    if (!await session.ReplaceCountAsync(head, count.Version, tx)) return new CycleCountWriteOutcome(0, line.Id);
                    if (!await session.ReplaceLinesAsync([(entered, line.Version)], tx)) throw new LineLostException();
                    await session.InsertEventsAsync([lineCounted], tx);
                    return new CycleCountWriteOutcome(1, line.Id);
                }, ct);
                if (outcome.Affected == 1)
                    return Response<CycleCountLineDto>.Success(CycleCountReads.Line(entered, CycleCountReads.ShowsExpected(head, callerReadsVariances: false)));
            }
            catch (LineLostException)
            {
                return await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) ?? LineConflict();
            }
            catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Event)
            {
                if (await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
            }

            // the head moved under a concurrent entry on another line: a twin of THIS request answers first, otherwise read again and retry
            if (await ReplayLineAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } committed) return committed;
        }

        return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.Conflict, "The count kept changing while the entry was written; retry.", 409);
    }

    private static Response<CycleCountLineDto> LineConflict()
        => CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.Conflict,
            "The line changed since it was read; reload it and retry with its current ETag.", 409);
}
