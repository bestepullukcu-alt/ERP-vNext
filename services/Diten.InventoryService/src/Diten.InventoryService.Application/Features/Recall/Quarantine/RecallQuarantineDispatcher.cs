using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Recall.Quarantine;

/// <summary>
/// Pack §8.4 KR-B table — which request a Stock item of an approved revision gets (pure, written in the approval transaction):
/// <list type="bullet">
/// <item>excluded (own mark or ancestor, CT-R6) → <c>NotNeeded / Excluded</c>;</item>
/// <item>lot Blocked / Expired (0174) → <c>NotNeeded / LotBlocked | LotExpired</c> — the stock is already out of use, 0175 would refuse;</item>
/// <item>stock QUARANTINE / QUALITY_INSPECTION → <c>NotNeeded / AlreadyQuarantined</c>; BLOCKED / DAMAGED / EXPIRED → <c>AlreadyBlocked</c>;</item>
/// <item>AVAILABLE (a retest-due lot included — AVAILABLE→QUARANTINE is its allowed move) → <c>Pending</c>, sent after the commit.</item>
/// </list>
/// </summary>
public static class RecallQuarantinePlanner
{
    public static RecallQuarantineRequest Plan(RecallCase c, int revision, RecallScopeItem stock, bool effectivelyIncluded, Lot? lot, Guid requestedBy,
        DateTimeOffset now)
    {
        var (status, reason) = !effectivelyIncluded ? (RecallQuarantineRequestStatus.NotNeeded, RecallNotNeededReason.Excluded)
            : lot is { Status: LotStatus.Blocked } ? (RecallQuarantineRequestStatus.NotNeeded, RecallNotNeededReason.LotBlocked)
            : lot is not null && LotPostingRules.IsExpiredOn(lot, now) ? (RecallQuarantineRequestStatus.NotNeeded, RecallNotNeededReason.LotExpired)
            : stock.StockStatus is StockStatus.Quarantine or StockStatus.QualityInspection ? (RecallQuarantineRequestStatus.NotNeeded, RecallNotNeededReason.AlreadyQuarantined)
            : stock.StockStatus is StockStatus.Blocked or StockStatus.Damaged or StockStatus.Expired ? (RecallQuarantineRequestStatus.NotNeeded, RecallNotNeededReason.AlreadyBlocked)
            : (RecallQuarantineRequestStatus.Pending, (RecallNotNeededReason?)null);
        return New(c, revision, stock.NodeKey, stock.LotId, stock.LotNumber, stock.ItemId, stock.SkuId, stock.SkuLevel, stock.WarehouseId, stock.LocationId,
            stock.Quantity ?? 0m, stock.BaseUomId, status, reason, requestedBy, now);
    }

    public static RecallQuarantineRequest New(RecallCase c, int revision, string nodeKey, string lotId, string lotNumber, Guid itemId, Guid skuId, SkuLevel level,
        string? warehouseId, string? locationId, decimal quantity, string? baseUomId, RecallQuarantineRequestStatus status, RecallNotNeededReason? reason,
        Guid requestedBy, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = now,
        CaseId = c.Id,
        CaseNumber = c.Number,
        Revision = revision,
        NodeKey = nodeKey,
        LotId = lotId,
        LotNumber = lotNumber,
        ItemId = itemId,
        SkuId = skuId,
        SkuLevel = level,
        WarehouseId = warehouseId,
        LocationId = locationId,
        SnapshotQuantity = quantity,
        BaseUomId = baseUomId,
        Status = status,
        NotNeededReason = reason,
        QuarantineKey = RecallContract.QuarantineKey(c.Id, revision, nodeKey, 0),
        RequestedBy = requestedBy,
        RequestedAtUtc = now
    };

    /// <summary>The statuses "Yeniden dene" takes (pack §8.4: reservation, moved stock, other errors) — never Pending (the sweeper's) or done ones.</summary>
    public static bool IsRetryable(RecallQuarantineRequestStatus s)
        => s is RecallQuarantineRequestStatus.BlockedByReservation or RecallQuarantineRequestStatus.NothingAvailable or RecallQuarantineRequestStatus.Failed;
}

/// <summary>
/// Pack §8.4 — sends the <c>Pending</c> requests to MOD-0175 AFTER the approval commit (in the request; the hosted sweeper retries a Pending one
/// older than 2 minutes — a crash between commit and dispatch is recovered). ONE bucket per call (partial success: free buckets are held,
/// reserved ones reported). The bucket quantity is the current AVAILABLE of the bucket, at most the snapshot quantity, decided once per key and
/// stored before the call — a retried call replays at 0175 (same key, same body). The answer maps onto the request; the recall NEVER cancels a
/// reservation and never decides for 0175 (CT-R2). Each outcome writes the request, a case event and an audit row in one transaction.
/// </summary>
public sealed class RecallQuarantineDispatcher
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private readonly IRecallRepository _repository;
    private readonly IRecallQuarantineRequester _requester;
    private readonly IRecallLedgerReader _ledger;
    private readonly RecallSupport _support;
    private readonly ILogger<RecallQuarantineDispatcher> _logger;

    public RecallQuarantineDispatcher(IRecallRepository repository, IRecallQuarantineRequester requester, IRecallLedgerReader ledger, RecallSupport support,
        ILogger<RecallQuarantineDispatcher> logger)
    {
        _repository = repository;
        _requester = requester;
        _ledger = ledger;
        _support = support;
        _logger = logger;
    }

    /// <summary>Sends every Pending request of these ids (re-read first); returns the requests as they stand afterwards.</summary>
    public async Task<IReadOnlyList<RecallQuarantineRequest>> DispatchAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken ct)
    {
        var result = new List<RecallQuarantineRequest>();
        foreach (var id in requestIds)
        {
            var request = await _repository.FindQuarantineRequestAsync(id, ct);
            if (request is null) continue;
            if (request.Status == RecallQuarantineRequestStatus.Pending)
            {
                try
                {
                    request = await SendAsync(request, ct) ?? request;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // stays Pending: the sweeper retries it (never a Failed by an exception we did not understand); FU-0177-SWEEP-01: the sweeper
                    // counts it (DeferAsync) and stops at the limit. FU-0177-SWEEP-02 (OBS-001): the warning names the tenant and company.
                    _deferrals[request.Id] = Reason(ex);
                    _logger.LogWarning(ex, "recall.quarantine.dispatch_deferred {RequestId} tenant {TenantId} company {LegalEntityId}", request.Id, request.TenantId,
                        request.LegalEntityId);
                }
            }

            result.Add(request);
        }

        return result;
    }

    /// <summary>The error code of a request the sweeper stopped retrying (FU-0177-SWEEP-01).</summary>
    public const string RetryLimitReached = "RECALL_QUARANTINE_RETRY_LIMIT";

    private readonly Dictionary<Guid, string> _deferrals = [];

    /// <summary>Why the last <see cref="DispatchAsync"/> of this request ended without an answer (null = it did not).</summary>
    public string? DeferralOf(Guid requestId) => _deferrals.GetValueOrDefault(requestId);

    /// <summary>The reason text of a deferral: the exception TYPE and its message (no request body, no user data), at most 300 characters.</summary>
    public static string Reason(Exception ex)
    {
        var text = $"{ex.GetType().Name}: {ex.Message}";
        return text.Length > 300 ? text[..300] : text;
    }

    /// <summary>
    /// WP-MVP1-FU-CLEAN1 (FU-0177-SWEEP-01) — one sweep ended without an answer for this Pending request: the deferral is counted
    /// (<see cref="RecallQuarantineRequest.DeferredAttempts"/>, the next sweep waits the usual age again). At <paramref name="maxDeferredAttempts"/>
    /// the request turns <c>Failed</c> with <see cref="RetryLimitReached"/> and the reason, written with its case event and audit row like every
    /// other outcome — the sweeper takes Pending only, so it is never taken again ("Yeniden dene" still can, CT-R2).
    /// </summary>
    public async Task<RecallQuarantineRequest?> DeferAsync(Guid requestId, string reason, int maxDeferredAttempts, CancellationToken ct)
    {
        var request = await _repository.FindQuarantineRequestAsync(requestId, ct);
        if (request is null || request.Status != RecallQuarantineRequestStatus.Pending) return request;
        var now = _support.Now();
        var count = request.DeferredAttempts + 1;
        if (count < Math.Max(1, maxDeferredAttempts))
        {
            return await SaveAsync(request, r =>
            {
                r.DeferredAttempts = count;
                r.LastAttemptAtUtc = now;
            }, now, ct, silent: true);
        }

        _logger.LogWarning("recall.quarantine.retry_limit {RequestId} tenant {TenantId} company {LegalEntityId} after {Count} sweeps", request.Id, request.TenantId,
            request.LegalEntityId, count);
        return await SaveAsync(request, r =>
        {
            r.DeferredAttempts = count;
            r.LastAttemptAtUtc = now;
            r.Status = RecallQuarantineRequestStatus.Failed;
            r.ErrorCode = RetryLimitReached;
            r.ErrorMessage = $"No answer after {count} sweeps (limit {maxDeferredAttempts}); last: {reason}";
        }, now, ct);
    }

    private async Task<RecallQuarantineRequest?> SendAsync(RecallQuarantineRequest request, CancellationToken ct)
    {
        var now = _support.Now();
        if (request.WarehouseId is null || request.LocationId is null)
        {
            return await SaveAsync(request, r =>
            {
                r.Status = RecallQuarantineRequestStatus.Failed;
                r.ErrorCode = Common.InventoryErrorCodes.InvalidRequest;
                r.ErrorMessage = "The stock line names no warehouse / location: a quarantine bucket cannot be built.";
            }, now, ct);
        }

        // the bucket quantity: decided ONCE per key and stored before the call, so a replay sends the same body
        if (request.PlannedQuantity is null)
        {
            var available = (await _ledger.StockOfLotsAsync([request.LotId], ct))
                .Where(b => b.Status == StockStatus.Available && b.ItemId == request.ItemId && b.SkuId == request.SkuId && b.SkuLevel == request.SkuLevel
                            && b.WarehouseId == request.WarehouseId && b.LocationId == request.LocationId)
                .Sum(b => b.OnHand);
            var planned = Math.Min(available, request.SnapshotQuantity);
            if (planned <= 0m)
            {
                return await SaveAsync(request, r =>
                {
                    r.Status = RecallQuarantineRequestStatus.NothingAvailable;
                    r.PlannedQuantity = 0m;
                    r.ErrorCode = null;
                    r.ErrorMessage = "The bucket holds no AVAILABLE stock any more (moved, issued before the block, or already held).";
                }, now, ct);
            }

            if (await SaveAsync(request, r => r.PlannedQuantity = planned, now, ct, silent: true) is not { } planned1) return null; // a concurrent dispatcher took it
            request = planned1;
        }

        request.Attempts++;
        request.LastAttemptAtUtc = now;
        var bucket = new RecallQuarantineBucket(request.ItemId, request.SkuId, request.SkuLevel, request.WarehouseId!, request.LocationId!,
            request.PlannedQuantity!.Value, request.LotId);
        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        var answer = await _requester.QuarantineAsync(new RecallQuarantineCall([bucket], RecallContract.QuarantineReason(request.CaseNumber, c?.Reason ?? string.Empty),
            request.QuarantineKey, request.CaseId, request.CaseNumber), ct);

        return await SaveAsync(request, r =>
        {
            r.ErrorCode = null;
            r.ErrorMessage = null;
            r.Reservations = [];
            if (answer.Held)
            {
                r.HoldIds = answer.Holds.Select(h => h.HoldId).ToList();
                r.HeldQuantity = answer.Holds.Sum(h => h.Quantity);
                r.Status = r.HeldQuantity < r.SnapshotQuantity ? RecallQuarantineRequestStatus.PartiallyHeld : RecallQuarantineRequestStatus.Held;
            }
            else if (answer.ErrorCode == QuarantineStockReserved)
            {
                r.Status = RecallQuarantineRequestStatus.BlockedByReservation;
                r.ErrorCode = answer.ErrorCode;
                r.ErrorMessage = answer.Message;
                r.Reservations = ReservationLines(answer.Details);
            }
            else if (answer.ErrorCode == QuarantineNothingAvailable)
            {
                r.Status = RecallQuarantineRequestStatus.NothingAvailable;
                r.ErrorCode = answer.ErrorCode;
                r.ErrorMessage = answer.Message;
            }
            else
            {
                r.Status = RecallQuarantineRequestStatus.Failed;
                r.ErrorCode = answer.ErrorCode;
                r.ErrorMessage = answer.Message;
            }
        }, now, ct);
    }

    /// <summary>MOD-0175 refusal codes (QUARANTINE 1.x — read as data, never decided here).</summary>
    public const string QuarantineStockReserved = "QUARANTINE_STOCK_RESERVED";

    public const string QuarantineNothingAvailable = "QUARANTINE_NOTHING_AVAILABLE";

    private static List<RecallReservationLine> ReservationLines(IReadOnlyDictionary<string, object?>? details)
    {
        if (details is null || !details.TryGetValue("reservations", out var raw) || raw is null) return [];
        var json = JsonSerializer.SerializeToElement(raw, raw.GetType(), Wire);
        if (json.ValueKind != JsonValueKind.Array) return [];
        return json.EnumerateArray().Select(e => new RecallReservationLine
        {
            ReservationId = Text(e, "reservationId") ?? string.Empty,
            HolderModule = Text(e, "sourceModule"),
            SourceDocumentId = Text(e, "sourceDocumentId"),
            RemainingQuantity = e.TryGetProperty("remainingQuantity", out var q) ? q.ToString() : null
        }).ToList();

        static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>Writes the request (version-guarded) with its event + audit; null = another writer won (the caller re-reads).</summary>
    private async Task<RecallQuarantineRequest?> SaveAsync(RecallQuarantineRequest request, Action<RecallQuarantineRequest> change, DateTimeOffset now, CancellationToken ct,
        bool silent = false)
    {
        var expected = request.Version;
        change(request);
        request.Version = expected + 1;
        request.UpdatedAt = now;
        var held = request.Status is RecallQuarantineRequestStatus.Held or RecallQuarantineRequestStatus.PartiallyHeld;
        var refs = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["requestId"] = request.Id.ToString("D"),
            ["nodeKey"] = request.NodeKey,
            ["status"] = request.Status.ToString(),
            ["quarantineKey"] = request.QuarantineKey,
            ["quantity"] = request.PlannedQuantity?.ToString(CultureInfo.InvariantCulture),
            ["holdIds"] = string.Join(",", request.HoldIds),
            ["errorCode"] = request.ErrorCode
        };
        var saved = await _repository.InTransactionAsync(async (session, tx) =>
        {
            if (!await session.ReplaceQuarantineRequestAsync(request, expected, tx)) return false;
            if (silent) return true;
            await session.InsertEventAsync(_support.Event(request.CaseId, held ? RecallCaseEventKind.QuarantineRequested : RecallCaseEventKind.QuarantineFailed, now,
                $"{request.Status}: {request.LotNumber} {request.WarehouseId}/{request.LocationId}", refs), tx);
            await session.InsertAuditAsync(_support.Audit(held ? RecallContract.AuditQuarantineRequested : RecallContract.AuditQuarantineFailed,
                RecallContract.SubjectCase, request.CaseId, request.CaseNumber, now, request.CommandKey, refs), tx);
            return true;
        }, ct);
        return saved ? request : null;
    }
}
