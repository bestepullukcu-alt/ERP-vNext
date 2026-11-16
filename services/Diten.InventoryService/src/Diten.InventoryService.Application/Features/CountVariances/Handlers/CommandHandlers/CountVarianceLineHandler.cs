using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// The shared skeleton of the DL-3 LINE decisions (approve, reject, retry, resolve-integrity — draft §1 rules 5–6): the line by its id (404
/// <c>COUNT_LINE_NOT_FOUND</c>, another company's or tenant's included), then the caller's key event of this count and operation (same fingerprint →
/// 200 + the line as it is now, other → 400) BEFORE If-Match and every business rule; every write goes through <see cref="CycleCountLineWriter"/>.
/// </summary>
public abstract class CountVarianceLineHandler : CycleCountCommandHandler
{
    protected CountVarianceLineHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountLineWriter writer) : base(repository, support)
        => Writer = writer;

    protected CycleCountLineWriter Writer { get; }

    /// <summary>The line and its (live) count, or null.</summary>
    protected async Task<(CycleCountLine Line, CycleCount Count)?> LoadAsync(Guid lineId, CancellationToken ct)
    {
        var line = await Repository.FindLineByIdAsync(lineId, ct);
        if (line is null) return null;
        var count = await Repository.FindAsync(line.CountId, ct);
        return count is null ? null : (line, count);
    }

    protected static Response<CycleCountLineDto> LineNotFound()
        => CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.LineNotFound, "Cycle count line not found.", 404);

    /// <summary>null = not a replay; otherwise 200 + the line as it is now, or 400 for another body (draft §1 rule 5).</summary>
    protected async Task<Response<CycleCountLineDto>?> ReplayAsync(CycleCount count, Guid lineId, string op, string key, string fingerprint, bool readsVariances,
        CancellationToken ct)
    {
        var earlier = await Repository.FindKeyEventAsync(count.Id, Support.UserId, op, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return CycleCountSupport.KeyReused<CycleCountLineDto>();
        var line = await Repository.FindLineByIdAsync(lineId, ct);
        var now = await Repository.FindIncludingDeletedAsync(count.Id, ct) ?? count;
        return line is null ? LineNotFound() : Response<CycleCountLineDto>.Success(CycleCountReads.Line(line, CycleCountReads.ShowsExpected(now, readsVariances)));
    }

    /// <summary>A write's non-written outcomes: 404 / the refusal / the twin's replay / 409.</summary>
    protected async Task<Response<CycleCountLineDto>> NotWrittenAsync(CycleCountLineWrite<CycleCountLineDto> write, CycleCount count, Guid lineId, string op, string key,
        string fingerprint, bool readsVariances, CancellationToken ct) => write.Kind switch
    {
        CycleCountLineWriteKind.NotFound => LineNotFound(),
        CycleCountLineWriteKind.Refused => write.Refusal!,
        CycleCountLineWriteKind.Twin => await ReplayAsync(count, lineId, op, key, fingerprint, readsVariances, ct) ?? CycleCountSupport.Conflict<CycleCountLineDto>(),
        _ => write.Refusal ?? CycleCountSupport.Conflict<CycleCountLineDto>()
    };

    /// <summary>
    /// The answer of a decision that posted (pack §8.6): Posted / Refused (the ledger's answer is on the line) → 200; IntegrityError → 409
    /// <c>COUNT_ADJUSTMENT_INTEGRITY</c>; still Pending (the outcome is unknown) → 503 <c>COUNT_INVENTORY_UNAVAILABLE</c> — the retry or the sweeper
    /// recovers it with the same key.
    /// </summary>
    protected static Response<CycleCountLineDto> Answer(CycleCount count, CycleCountLine line, bool readsVariances)
    {
        var dto = CycleCountReads.Line(line, CycleCountReads.ShowsExpected(count, readsVariances));
        return line.Adjustment?.Status switch
        {
            CycleCountAdjustmentStatus.Pending => CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.InventoryUnavailable,
                "The stock answer was not received; the correction stays pending and is retried with the same key.", 503,
                new Dictionary<string, object?> { ["lineId"] = line.Id.ToString("D"), ["adjustmentStatus"] = "Pending" }),
            CycleCountAdjustmentStatus.IntegrityError => CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.AdjustmentIntegrity,
                "The ledger holds another movement under this line's key; resolve it.", 409, new Dictionary<string, object?>
                {
                    ["lineId"] = line.Id.ToString("D"), ["reason"] = line.Adjustment.IntegrityReason,
                    ["holderTransactionId"] = line.Adjustment.HolderTransactionId?.ToString("D")
                }),
            _ => Response<CycleCountLineDto>.Success(dto)
        };
    }

    /// <summary>The refusal of a line that does not take this decision in its state (pack §8.3).</summary>
    protected static CycleCountLineStep<CycleCountLineDto> WrongLineState(CycleCountLine line)
        => CycleCountLineStep<CycleCountLineDto>.Refuse(CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.InvalidState,
            $"The line is {line.Status}; this decision is not allowed in that state.", 409, new Dictionary<string, object?>
            {
                ["lineStatus"] = line.Status.ToString(), ["adjustmentStatus"] = line.Adjustment?.Status.ToString()
            }));

    protected static CycleCountLineStep<CycleCountLineDto> Refuse(Response<CycleCountLineDto> refusal) => CycleCountLineStep<CycleCountLineDto>.Refuse(refusal);
}
