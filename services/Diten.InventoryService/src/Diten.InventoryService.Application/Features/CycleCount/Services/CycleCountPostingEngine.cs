using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

public enum CycleCountPostingKind
{
    Posted,
    Refused,

    /// <summary>The key is held by a movement of another request (<c>KeyReusedDifferentPayload</c>) — an integrity error, never a refusal.</summary>
    KeyReused,

    /// <summary>The movement holding the key was reversed — an integrity error.</summary>
    HolderReversed,

    /// <summary>The commit outcome is not known (5xx, time limit, cancellation, an unexpected exception) — the correction stays Pending.</summary>
    Unknown
}

/// <summary>One posting attempt's (or one key lookup's) result.</summary>
public sealed record CycleCountPostingResult(CycleCountPostingKind Kind, Guid? TransactionId = null, string? TransactionNumber = null, string? Code = null,
    IReadOnlyDictionary<string, object?>? Details = null, int Status = 0, Guid? HolderTransactionId = null);

/// <summary>
/// The correction's core (pack §8.6, §8.7; MOD-0178 <c>PutawayPostingEngine</c> pattern): build the line's STORED ledger request (the same bytes on
/// every attempt — <c>transactionDate</c> / <c>postingDate</c> are read from the stored correction, never recomputed), post it through the ONE
/// stock door — the <c>_poster</c> field, ONE call — ask the ledger key through <see cref="IPostingKeyLookup"/>, and write a result through
/// <see cref="CycleCountLineWriter"/>. Answer mapping: 2xx → Posted; 400 <c>IDEMPOTENCY_KEY_REUSED</c> → NOT a refusal: the key is looked up and a
/// different payload is an integrity error (r5, SB1/F-4); any other definite 4xx → Refused with code and details as the ledger sent them (AC-27,
/// AC-28; no partial quantity, no retry with another amount); every outcome whose commit is not known → Unknown (Pending, 503).
/// </summary>
public sealed class CycleCountPostingEngine
{
    private const string WireDate = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>The posting time limit — beyond it the outcome is unknown and the correction stays Pending.</summary>
    public static readonly TimeSpan PostingTimeout = TimeSpan.FromSeconds(30);

    private readonly ICycleCountMovementPoster _poster;
    private readonly IPostingKeyLookup _lookup;
    private readonly CycleCountSupport _support;
    private readonly CycleCountLineWriter _writer;

    public CycleCountPostingEngine(ICycleCountMovementPoster poster, IPostingKeyLookup lookup, CycleCountSupport support, CycleCountLineWriter writer)
    {
        _poster = poster;
        _lookup = lookup;
        _support = support;
        _writer = writer;
    }

    /// <summary>
    /// Pack §8.6: the line's <c>COUNT_GAIN</c> (to = the line's bin, its status, its lot) or <c>COUNT_LOSS</c> (from = the bin), |variance| in the base
    /// unit, source MOD-0181 / CYCLE_COUNT / the count / the line, reason <c>CYCLE_COUNT_VARIANCE</c>, key <c>cycle-count:{lineId:D}</c>, the stored
    /// dates. Deterministic: a retry, the sweeper and a lookup build the identical request. Any other movement type is refused here (AC-29).
    /// </summary>
    public static PostMovementCommand Request(CycleCount count, CycleCountLine line)
    {
        var a = line.Adjustment ?? throw new InvalidOperationException("The line has no correction to post.");
        if (!CycleCountDecisionRules.IsCountType(a.MovementType))
            throw new InvalidOperationException($"MOD-0181 posts COUNT_GAIN / COUNT_LOSS only, not {a.MovementType}.");
        var gain = a.MovementType == MovementType.CountGain;
        var warehouse = line.WarehouseId.ToString("D");
        var bin = line.LocationId.ToString("D");
        var status = WireNames.ToWire(line.StockStatus);
        return new PostMovementCommand
        {
            HeaderIdempotencyKey = a.IdempotencyKey,
            IdempotencyKey = a.IdempotencyKey,
            DocumentGroupId = count.Number,
            MovementType = WireNames.ToWire(a.MovementType),
            MovementReason = CycleCountDecisionRules.MovementReason,
            ItemId = line.ItemId.ToString("D"),
            SkuId = line.SkuId.ToString("D"),
            SkuLevel = WireNames.ToWire(line.SkuLevel),
            FromWarehouseId = gain ? null : warehouse,
            FromLocationId = gain ? null : bin,
            ToWarehouseId = gain ? warehouse : null,
            ToLocationId = gain ? bin : null,
            Quantity = InventoryQuantity.ToWire(a.Quantity),
            UomId = line.BaseUomId,
            LotId = line.LotId,
            FromStockStatus = gain ? null : status,
            ToStockStatus = gain ? status : null,
            SourceModule = CycleCountDecisionRules.SourceModule,
            SourceType = CycleCountDecisionRules.SourceType,
            SourceDocumentId = count.Id.ToString("D"),
            SourceLineId = line.Id.ToString("D"),
            TransactionDate = a.TransactionDate.UtcDateTime.ToString(WireDate, CultureInfo.InvariantCulture),
            PostingDate = a.PostingDate.UtcDateTime.ToString(WireDate, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>One posting attempt with the answer mapping above (the ONE call of the stock door).</summary>
    public async Task<CycleCountPostingResult> PostAsync(CycleCount count, CycleCountLine line, CancellationToken ct)
    {
        var request = Request(count, line);
        using var timeout = new CancellationTokenSource(PostingTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        Common.Models.Response<Inventory.MovementResponseDto> response;
        try
        {
            response = await _poster.PostAsync(request, _support.LegalEntityId, _support.UserId, linked.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // a cancellation, the time limit or an unexpected exception: the commit outcome is unknown — Refused is never written for it
            return new CycleCountPostingResult(CycleCountPostingKind.Unknown, Code: CycleCountErrorCodes.InventoryUnavailable, Status: 503);
        }

        if (response.IsSuccessful && response.Data is { } posted)
            return new CycleCountPostingResult(CycleCountPostingKind.Posted, Guid.Parse(posted.TransactionId), posted.TransactionNumber);
        if (string.Equals(response.ErrorCode, InventoryErrorCodes.IdempotencyKeyReused, StringComparison.Ordinal))
            return await LookupAsync(count, line, ct) ?? Unknown();
        if (response.StatusCode >= 500) return Unknown();
        return new CycleCountPostingResult(CycleCountPostingKind.Refused, Code: response.ErrorCode ?? InventoryErrorCodes.InvalidRequest, Details: response.ErrorDetails,
            Status: response.StatusCode);
    }

    private static CycleCountPostingResult Unknown()
        => new(CycleCountPostingKind.Unknown, Code: CycleCountErrorCodes.InventoryUnavailable, Status: 503);

    /// <summary>
    /// The ledger key (pack §8.6 retry / sweeper step 1): Committed and not reversed → Posted (the holder's movement — no second one); Committed but
    /// reversed → HolderReversed; another request under the key → KeyReused; nothing under the key → null.
    /// </summary>
    public async Task<CycleCountPostingResult?> LookupAsync(CycleCount count, CycleCountLine line, CancellationToken ct)
    {
        var found = await _lookup.LookupAsync(Request(count, line), ct);
        if (found.State == PostingKeyState.NotCommitted || found.Holder is not { } h) return null;
        return found.State switch
        {
            PostingKeyState.Committed when h.ReversedByTransactionId is null => new CycleCountPostingResult(CycleCountPostingKind.Posted, h.TransactionId, h.TransactionNumber),
            PostingKeyState.Committed => new CycleCountPostingResult(CycleCountPostingKind.HolderReversed, Code: CycleCountErrorCodes.AdjustmentIntegrity, Status: 409,
                HolderTransactionId: h.TransactionId),
            _ => new CycleCountPostingResult(CycleCountPostingKind.KeyReused, Code: CycleCountErrorCodes.AdjustmentIntegrity, Status: 409, HolderTransactionId: h.TransactionId)
        };
    }

    /// <summary>The raw lookup with its holder (the resolve decides on the holder's content).</summary>
    public Task<PostingKeyLookupResult> RawLookupAsync(CycleCount count, CycleCountLine line, CancellationToken ct) => _lookup.LookupAsync(Request(count, line), ct);

    /// <summary>Retry / sweeper (pack §8.6): the key first; nothing under it → the same request is posted again.</summary>
    public async Task<CycleCountPostingResult> RecoverAsync(CycleCount count, CycleCountLine line, CancellationToken ct)
        => await LookupAsync(count, line, ct) ?? await PostAsync(count, line, ct);

    /// <summary>
    /// Writes one attempt's result on the line — only while its correction is still Pending (a concurrent attempt that already wrote wins; this one
    /// changes nothing). Unknown writes nothing (the correction stays Pending).
    /// </summary>
    public Task<CycleCountLineWrite<T>> WriteResultAsync<T>(Guid countId, Guid lineId, CycleCountPostingResult result, CancellationToken ct)
    {
        if (result.Kind == CycleCountPostingKind.Unknown) return Task.FromResult(new CycleCountLineWrite<T>(CycleCountLineWriteKind.Unchanged, null, null));
        return _writer.WriteAsync<T>(countId, lineId, (_, line) =>
        {
            if (line.Status != CycleCountLineStatus.Approved || line.Adjustment?.Status != CycleCountAdjustmentStatus.Pending)
                return CycleCountLineStep<T>.Unchanged;
            var (changed, row) = Apply(line, result, _support.Now());
            return CycleCountLineStep<T>.Change(changed, row);
        }, null, null, null, ct);
    }

    /// <summary>The line after a definite result, and its timeline row.</summary>
    public static (CycleCountLine Line, CycleCountLineEvent Event) Apply(CycleCountLine line, CycleCountPostingResult result, DateTimeOffset now)
    {
        var changed = line.Copy();
        var a = changed.Adjustment!;
        a.LastAttemptAt = now;
        var data = new Dictionary<string, string?>(StringComparer.Ordinal) { ["attempts"] = a.Attempts.ToString(CultureInfo.InvariantCulture) };
        switch (result.Kind)
        {
            case CycleCountPostingKind.Posted:
                a.Status = CycleCountAdjustmentStatus.Posted;
                a.TransactionId = result.TransactionId;
                a.TransactionNumber = result.TransactionNumber;
                data["transactionId"] = result.TransactionId?.ToString("D");
                data["transactionNumber"] = result.TransactionNumber;
                return (changed, new CycleCountLineEvent(CycleCountEventKind.AdjustmentPosted, data));
            case CycleCountPostingKind.Refused:
                a.Status = CycleCountAdjustmentStatus.Refused;
                a.RefusalCode = result.Code;
                a.RefusalDetailsJson = result.Details is { Count: > 0 } details ? JsonSerializer.Serialize(details) : null;
                a.RefusalStatus = result.Status;
                data["refusalCode"] = result.Code;
                data["refusalStatus"] = result.Status.ToString(CultureInfo.InvariantCulture);
                return (changed, new CycleCountLineEvent(CycleCountEventKind.AdjustmentRefused, data));
            default:
                a.Status = CycleCountAdjustmentStatus.IntegrityError;
                a.IntegrityReason = result.Kind == CycleCountPostingKind.HolderReversed ? "HolderReversed" : "KeyReused";
                a.HolderTransactionId = result.HolderTransactionId;
                data["reason"] = a.IntegrityReason;
                data["holderTransactionId"] = result.HolderTransactionId?.ToString("D");
                return (changed, new CycleCountLineEvent(CycleCountEventKind.AdjustmentIntegrityError, data));
        }
    }
}
