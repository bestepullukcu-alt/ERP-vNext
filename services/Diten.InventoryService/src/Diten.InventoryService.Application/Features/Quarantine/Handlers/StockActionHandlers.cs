using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>
/// The two no-workflow SAFETY actions (pack §8 KQ-F: they only take stock out of use): "Karantinaya Al" (AVAILABLE →
/// QUARANTINE) and "Stoğu bloke et" (AVAILABLE → BLOCKED). Per bucket: one STATUS_TRANSFER through the ledger's internal
/// posting service, then the record (hold / blocked stock) carrying that transaction.
/// <list type="bullet">
/// <item>CT-Q6 / V18: when reserved stock would be uncovered nothing is posted — 409 <c>QUARANTINE_STOCK_RESERVED</c> with the
/// SKU's active reservations (checked before the first posting; a race the check misses is refused by the ledger's own
/// coverage guard and mapped the same way).</item>
/// <item>Idempotency: bucket i posts under a ledger key derived from (user, request key, i); its record carries
/// <c>{key}#{i}</c>. A repeated request answers from the records (same body → 200; another body → 400
/// <c>IDEMPOTENCY_KEY_REUSED</c>); a request that stopped half-way continues where it stopped, never posting a bucket twice.</item>
/// <item>Q4 (O-Q2-6, ASSUMPTION-0175-Q4-02): a serial-tracked bucket moves serials the SYSTEM chooses — the Active registered serials
/// of the SKU / lot at the location, minus those named on an open 0175 record there, in intake order: the whole bucket takes them
/// all, a part takes the first ones. They ride the movement (the ledger validates them) and are stored on the hold / blocked record,
/// so a later decision splits them (FU-0175-Q1-SER).</item>
/// <item>Q4 (KQ-H): the reservation pre-check counts only buckets whose lot does not already hold its stock
/// (<see cref="LotPostingRules.HoldsStock(Domain.Traceability.Entities.Lot)"/>): a retest-held lot's AVAILABLE stock is out of the SKU
/// position already, moving it to QUARANTINE uncovers nothing (existing reservations stay, CT-Q6).</item>
/// </list>
/// </summary>
public sealed class QuarantineStockActions
{
    public const int MaxBuckets = 50;

    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineLedgerReader _ledgerReader;
    private readonly QuarantineMovementPoster _poster;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    public QuarantineStockActions(
        IQuarantineRepository repository,
        IQuarantineLedgerReader ledgerReader,
        QuarantineMovementPoster poster,
        QuarantineFacts facts,
        QuarantineSupport support)
    {
        _repository = repository;
        _ledgerReader = ledgerReader;
        _poster = poster;
        _facts = facts;
        _support = support;
    }

    public sealed record PostedBucket(int Index, BucketInput Bucket, string TransactionId, string Number, string BaseUomId, List<string>? SerialIds);

    /// <summary>Shape + reason checks shared by both actions (V3; bucket count / quantity / ids).</summary>
    public static Response<T>? ShapeRefusal<T>(IReadOnlyList<BucketInput>? buckets, string? reason, string? signatureId)
    {
        if (signatureId is not null)
        {
            // KQ-B decider signatures are verified from slice Q3 (MOD-0022); an unverifiable id is not accepted silently.
            return QuarantineSupport.Invalid<T>("signatureId is verified from MOD-0175 slice Q3; send it without a signature (policy row not required).");
        }

        if (QuarantineDecisionRules.Reason(reason) is { } r)
        {
            return QuarantineSupport.Fail<T>(r);
        }

        if (buckets is null || buckets.Count is < 1 or > MaxBuckets)
        {
            return QuarantineSupport.Invalid<T>($"buckets is required: 1-{MaxBuckets} stock rows.");
        }

        if (buckets.Any(b => b.Quantity <= 0m || !QuarantineSupport.TryParseId(b.WarehouseId, out _) || !QuarantineSupport.TryParseId(b.LocationId, out _)))
        {
            return QuarantineSupport.Invalid<T>("Every bucket needs a quantity greater than 0 and canonical warehouseId / locationId UUIDs.");
        }

        return null;
    }

    public static string Fingerprint(string kind, IReadOnlyList<BucketInput> buckets, string? reason, string? extra)
        => QuarantineSupport.Fingerprint(
            [("kind", kind), ("reason", reason?.Trim()), ("extra", extra),
             .. buckets.Select((b, i) => ($"b{i}", $"{b.ItemId:D}|{b.SkuId:D}|{b.SkuLevel}|{b.WarehouseId}|{b.LocationId}|{b.LotId}|{QuarantineSupport.D(b.Quantity)}"))]);

    /// <summary>
    /// Posts every bucket not posted yet (STATUS_TRANSFER AVAILABLE → <paramref name="target"/>) and hands each posted bucket
    /// to <paramref name="record"/> (which writes the 0175 record). Returns the refusal of the first bucket that failed.
    /// </summary>
    public async Task<Response<bool>> PostBucketsAsync(
        string kind,
        string sourceType,
        string numberPrefix,
        StockStatus target,
        IReadOnlyList<BucketInput> buckets,
        IReadOnlySet<int> alreadyRecorded,
        string idempotencyKey,
        string? movementReason,
        Func<PostedBucket, Task> record,
        CancellationToken ct)
    {
        var pending = Enumerable.Range(0, buckets.Count).Where(i => !alreadyRecorded.Contains(i)).ToList();
        var uoms = new Dictionary<int, string>();
        foreach (var i in pending)
        {
            var b = buckets[i];
            var (onHand, uom) = await _facts.BucketAsync(b.ItemId, b.SkuId, b.SkuLevel, b.WarehouseId, b.LocationId, b.LotId, StockStatus.Available, ct);
            if (onHand <= 0m || uom is null)
            {
                return QuarantineSupport.Fail<bool>(QuarantineErrorCodes.NothingAvailable,
                    $"Bucket {i} holds no AVAILABLE stock; nothing to take out of use.", 409);
            }

            uoms[i] = uom;
        }

        // CT-Q6: refuse before the first posting when the SKU's reservations would be uncovered (a lot holding its stock uncovers nothing).
        var holding = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lotId in pending.Select(i => buckets[i].LotId).OfType<string>().Distinct())
        {
            if (await _facts.LotAsync(lotId, ct) is { } lot && LotPostingRules.HoldsStock(lot)) holding.Add(lotId);
        }

        foreach (var sku in pending.Where(i => buckets[i].LotId is null || !holding.Contains(buckets[i].LotId!))
                     .GroupBy(i => (buckets[i].ItemId, buckets[i].SkuId, buckets[i].SkuLevel)))
        {
            var required = sku.Sum(i => buckets[i].Quantity);
            var unreserved = await _ledgerReader.SkuUnreservedAsync(sku.Key.ItemId, sku.Key.SkuId, sku.Key.SkuLevel, ct);
            if (unreserved is not null && required > unreserved.Value)
            {
                return await ReservedAsync(sku.Key.ItemId, sku.Key.SkuId, sku.Key.SkuLevel, required, unreserved.Value, ct);
            }
        }

        foreach (var i in pending)
        {
            var b = buckets[i];
            var ledgerKey = QuarantineMovementPoster.RequestKey(kind, _support.UserId, idempotencyKey, i);
            var number = await NumberForAsync(ledgerKey, numberPrefix, ct);
            var serials = await SerialsForAsync(b, ct);
            var movement = new PlannedMovement(1, MovementType.StatusTransfer, StockStatus.Available, target, b.WarehouseId, b.LocationId,
                b.WarehouseId, b.LocationId, b.Quantity) { SerialIds = serials };
            var envelope = new MovementEnvelope(b.ItemId, b.SkuId, b.SkuLevel, b.LotId, uoms[i], sourceType, number, null, movementReason);
            var posted = await _poster.PostAsync(movement, envelope, ledgerKey, _support.UserId, ct);
            if (!posted.Posted)
            {
                if (posted.ErrorCode == InventoryErrorCodes.InsufficientStock)
                {
                    var unreserved = await _ledgerReader.SkuUnreservedAsync(b.ItemId, b.SkuId, b.SkuLevel, ct);
                    var reservations = await _ledgerReader.ActiveReservationsAsync(b.ItemId, b.SkuId, b.SkuLevel, ct);
                    if (reservations.Count > 0)
                    {
                        return await ReservedAsync(b.ItemId, b.SkuId, b.SkuLevel, b.Quantity, unreserved ?? 0m, ct);
                    }
                }

                return QuarantineSupport.Fail<bool>(posted.ErrorCode!, posted.Message ?? "The ledger refused the movement.", posted.Status, posted.Details);
            }

            await record(new PostedBucket(i, b, posted.TransactionId!, number, uoms[i], serials));
        }

        return Response<bool>.Success(true);
    }

    /// <summary>
    /// ASSUMPTION-0175-Q4-02: the serials a bucket moves — null for a bucket with no registered serial there (not serial-tracked).
    /// The registry knows the location, not the stock status, so serials an open 0175 hold / blocked record of the same bucket names
    /// (QUARANTINE / BLOCKED there) are left out; the rest go in intake order, the first <c>quantity</c> of them.
    /// </summary>
    private async Task<List<string>?> SerialsForAsync(BucketInput b, CancellationToken ct)
    {
        var registered = await _ledgerReader.SerialsAtAsync(b.ItemId, b.SkuId, b.SkuLevel, b.LotId, b.WarehouseId, b.LocationId, ct);
        if (registered.Count == 0 || b.Quantity != decimal.Truncate(b.Quantity))
        {
            return null;
        }

        var taken = new HashSet<string>(StringComparer.Ordinal);
        var holds = await _repository.ListHoldsAsync(new HoldQuery(null, b.WarehouseId, null, null, null, null, b.LotId, null, 1, 500), ct);
        foreach (var hold in holds.Items.Where(h => h.Status != HoldStatus.Decided && SameBucket(h.ItemId, h.SkuId, h.SkuLevel, h.LotId, h.LocationId, b)))
        {
            taken.UnionWith(hold.SerialIds ?? []);
        }

        var blocked = await _repository.ListBlockedAsync(new BlockedQuery(null, null, false, null, BlockedStatus.Open, b.WarehouseId, 1, 500), ct);
        foreach (var record in blocked.Items.Where(r => SameBucket(r.ItemId, r.SkuId, r.SkuLevel, r.LotId, r.LocationId, b)))
        {
            taken.UnionWith(record.SerialIds ?? []);
        }

        return registered.Where(s => !taken.Contains(s)).Take((int)b.Quantity).ToList();
    }

    private static bool SameBucket(Guid itemId, Guid skuId, SkuLevel level, string? lotId, string locationId, BucketInput b)
        => itemId == b.ItemId && skuId == b.SkuId && level == b.SkuLevel && string.Equals(lotId, b.LotId, StringComparison.Ordinal)
           && string.Equals(locationId, b.LocationId, StringComparison.Ordinal);

    /// <summary>The record number a bucket's movement carries: re-used from the committed ledger transaction on a retry.</summary>
    private async Task<string> NumberForAsync(string ledgerKey, string prefix, CancellationToken ct)
        => await _poster.CommittedDocumentAsync(ledgerKey, ct)
           ?? await _repository.InTransactionAsync((session, c) => session.NextNumberAsync(prefix, QuarantineSupport.Now().UtcDateTime.Year, c), ct);

    private async Task<Response<bool>> ReservedAsync(Guid itemId, Guid skuId, SkuLevel level, decimal requested, decimal unreserved, CancellationToken ct)
    {
        var reservations = await _ledgerReader.ActiveReservationsAsync(itemId, skuId, level, ct);
        return QuarantineSupport.Fail<bool>(QuarantineErrorCodes.StockReserved,
            "Reserved stock would be uncovered; release or change the reservations first (MOD-0175 does not touch other modules' reservations).", 409,
            new Dictionary<string, object?>
            {
                ["requested"] = InventoryQuantity.ToWire(requested),
                ["unreserved"] = InventoryQuantity.ToWire(Math.Max(0m, unreserved)),
                ["reservations"] = reservations
                    .Select(r => new ReservationLineDto(r.ReservationId.ToString("D"), r.SourceModule, r.SourceDocumentId, r.RemainingQuantity))
                    .ToList()
            });
    }
}

public sealed class QuarantineStockHandler : IRequestHandler<QuarantineStockCommand, Response<IReadOnlyList<HoldRefDto>>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineStockActions _actions;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    private readonly IQuarantineLedgerReader _ledgerReader;

    public QuarantineStockHandler(
        IQuarantineRepository repository, QuarantineStockActions actions, QuarantineFacts facts, QuarantineSupport support, IQuarantineLedgerReader ledgerReader)
    {
        _repository = repository;
        _actions = actions;
        _facts = facts;
        _support = support;
        _ledgerReader = ledgerReader;
    }

    public async Task<Response<IReadOnlyList<HoldRefDto>>> Handle(QuarantineStockCommand request, CancellationToken ct)
    {
        if (request.Mode is not ("Manual" or "Retest"))
        {
            return QuarantineSupport.Invalid<IReadOnlyList<HoldRefDto>>("mode must be Manual or Retest.");
        }

        var retest = request.Mode == "Retest";
        List<BucketInput>? buckets;
        Domain.Traceability.Entities.Lot? retestLot = null;
        string fingerprint;
        List<string> keys;
        if (retest)
        {
            // KQ-H: every AVAILABLE bucket of the lot, one hold per bucket (Source Retest); the buckets are the system's, not the body's.
            if (request.Buckets is { Count: > 0 })
            {
                return QuarantineSupport.Invalid<IReadOnlyList<HoldRefDto>>("mode Retest takes lotId only; the AVAILABLE buckets of the lot are found by the service.");
            }

            retestLot = await _facts.LotAsync(request.LotId, ct);
            if (retestLot?.RetestDate is null)
            {
                return QuarantineSupport.Invalid<IReadOnlyList<HoldRefDto>>("mode Retest needs the lotId of a registered lot with a retest date.");
            }

            if (request.SignatureId is not null || QuarantineDecisionRules.Reason(request.Reason) is not null)
            {
                return QuarantineStockActions.ShapeRefusal<IReadOnlyList<HoldRefDto>>(null, request.Reason, request.SignatureId)!; // signature / reason refusal
            }

            fingerprint = QuarantineStockActions.Fingerprint("hold-retest", [], request.Reason, request.LotId);
            keys = Enumerable.Range(0, QuarantineStockActions.MaxBuckets).Select(i => $"{request.IdempotencyKey}#{i}").ToList();
            var created = await _repository.FindHoldsByCreateKeysAsync(_support.UserId, keys, ct);
            if (created.Any(h => h.CreateRequestFingerprint != fingerprint))
            {
                return QuarantineSupport.KeyReused<IReadOnlyList<HoldRefDto>>();
            }

            if (created.Count > 0)
            {
                return await AnswerAsync(created, keys, 200, ct); // ASSUMPTION-0175-Q4-03: a Retest key answers the holds it created
            }

            buckets = (await _ledgerReader.LotBalancesAsync([request.LotId!], StockStatus.Available, ct))
                .GroupBy(r => (r.ItemId, r.SkuId, r.SkuLevel, r.WarehouseId, r.LocationId))
                .Select(g => new BucketInput(g.Key.ItemId, g.Key.SkuId, g.Key.SkuLevel, g.Key.WarehouseId!, g.Key.LocationId!, g.Sum(r => r.OnHand), request.LotId))
                .Take(QuarantineStockActions.MaxBuckets)
                .ToList();
            if (buckets.Count == 0)
            {
                return QuarantineSupport.Fail<IReadOnlyList<HoldRefDto>>(QuarantineErrorCodes.NothingAvailable, "The lot holds no AVAILABLE stock in this company.", 409);
            }
        }
        else
        {
            buckets = request.Buckets?.Select(b => b with { LotId = b.LotId ?? request.LotId }).ToList();
            if (QuarantineStockActions.ShapeRefusal<IReadOnlyList<HoldRefDto>>(buckets, request.Reason, request.SignatureId) is { } refusal)
            {
                return refusal;
            }

            fingerprint = QuarantineStockActions.Fingerprint("hold", buckets!, request.Reason, request.LotId);
            keys = Enumerable.Range(0, buckets!.Count).Select(i => $"{request.IdempotencyKey}#{i}").ToList();
        }

        var existing = await _repository.FindHoldsByCreateKeysAsync(_support.UserId, keys, ct);
        if (existing.Any(h => h.CreateRequestFingerprint != fingerprint))
        {
            return QuarantineSupport.KeyReused<IReadOnlyList<HoldRefDto>>();
        }

        var done = existing.Select(h => keys.IndexOf(h.CreateIdempotencyKey!)).ToHashSet();
        var reason = request.Reason!.Trim();
        var result = await _actions.PostBucketsAsync("hold", QuarantineContract.SourceTypeHold, QuarantineContract.HoldPrefix, StockStatus.Quarantine,
            buckets, done, request.IdempotencyKey, null, async posted =>
            {
                var now = QuarantineSupport.Now();
                var hold = new QuarantineHold
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    HoldNumber = posted.Number,
                    ItemId = posted.Bucket.ItemId,
                    SkuId = posted.Bucket.SkuId,
                    SkuLevel = posted.Bucket.SkuLevel,
                    LotId = posted.Bucket.LotId,
                    WarehouseId = posted.Bucket.WarehouseId,
                    LocationId = posted.Bucket.LocationId,
                    StockStatus = StockStatus.Quarantine,
                    Quantity = posted.Bucket.Quantity,
                    BaseUomId = posted.BaseUomId,
                    SerialIds = posted.SerialIds,
                    Source = retest ? HoldSource.Retest : HoldSource.Manual,
                    RetestDateAtIntake = retestLot?.RetestDate,
                    SourceMovementId = posted.TransactionId,
                    SourceDocument = new HoldSourceDocument { Module = QuarantineContract.ModuleId, Type = QuarantineContract.SourceTypeHold, Number = posted.Number, Text = reason },
                    EnteredAtUtc = now,
                    CreatedBy = _support.UserId,
                    CreateIdempotencyKey = keys[posted.Index],
                    CreateRequestFingerprint = fingerprint
                };
                try
                {
                    await _repository.InTransactionAsync<bool>(async (session, c) =>
                    {
                        await session.InsertHoldAsync(hold, c);
                        await session.InsertHistoryAsync([_support.History(HistorySubject.Hold, hold.Id, HistoryAction.Quarantined, now, reason)], c);
                        return true;
                    }, ct);
                }
                catch (QuarantineDuplicateException)
                {
                    // the record of this bucket was written by a concurrent run of the same request
                }
            }, ct);
        if (!result.IsSuccessful)
        {
            return Response<IReadOnlyList<HoldRefDto>>.Fail(result.ErrorCode!, result.Errors[0], result.StatusCode, result.ErrorDetails);
        }

        var holds = await _repository.FindHoldsByCreateKeysAsync(_support.UserId, keys, ct);
        return await AnswerAsync(holds, keys, done.Count == buckets.Count ? 200 : 201, ct);
    }

    private async Task<Response<IReadOnlyList<HoldRefDto>>> AnswerAsync(IReadOnlyList<QuarantineHold> holds, List<string> keys, int status, CancellationToken ct)
    {
        var lots = await _facts.LotsAsync(holds.Select(h => h.LotId), ct);
        var target = (await _facts.SettingsAsync(ct)).WaitTargetDays;
        var now = QuarantineSupport.Now();
        IReadOnlyList<HoldRefDto> body = holds.OrderBy(h => keys.IndexOf(h.CreateIdempotencyKey!))
            .Select(h => QuarantineMapping.HoldRef(h, lots, target, null, now)).ToList();
        return Response<IReadOnlyList<HoldRefDto>>.Success(body, status);
    }
}

public sealed class BlockStockHandler : IRequestHandler<BlockStockCommand, Response<IReadOnlyList<BlockedStockRefDto>>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineStockActions _actions;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    public BlockStockHandler(IQuarantineRepository repository, QuarantineStockActions actions, QuarantineFacts facts, QuarantineSupport support)
    {
        _repository = repository;
        _actions = actions;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<IReadOnlyList<BlockedStockRefDto>>> Handle(BlockStockCommand request, CancellationToken ct)
    {
        if (QuarantineStockActions.ShapeRefusal<IReadOnlyList<BlockedStockRefDto>>(request.Buckets, request.Reason, request.SignatureId) is { } refusal)
        {
            return refusal;
        }

        if (request.Note is { Length: > QuarantineContract.MaxNoteLength })
        {
            return QuarantineSupport.Invalid<IReadOnlyList<BlockedStockRefDto>>($"note must be at most {QuarantineContract.MaxNoteLength} characters.");
        }

        var active = await _facts.ActiveBlockReasonsAsync(ct);
        if (QuarantineDecisionRules.BlockReason(true, request.BlockReasonCode, active) is { } v5)
        {
            return QuarantineSupport.Fail<IReadOnlyList<BlockedStockRefDto>>(v5);
        }

        var fingerprint = QuarantineStockActions.Fingerprint("block", request.Buckets, request.Reason, $"{request.BlockReasonCode}|{request.Note}");
        var keys = Enumerable.Range(0, request.Buckets.Count).Select(i => $"{request.IdempotencyKey}#{i}").ToList();
        var existing = await _repository.FindBlockedByCreateKeysAsync(_support.UserId, keys, ct);
        if (existing.Any(b => b.CreateRequestFingerprint != fingerprint))
        {
            return QuarantineSupport.KeyReused<IReadOnlyList<BlockedStockRefDto>>();
        }

        var done = existing.Select(b => keys.IndexOf(b.CreateIdempotencyKey!)).ToHashSet();
        var reason = request.Reason!.Trim();
        var result = await _actions.PostBucketsAsync("block", QuarantineContract.SourceTypeBlock, QuarantineContract.BlockedPrefix, StockStatus.Blocked,
            request.Buckets, done, request.IdempotencyKey, request.BlockReasonCode, async posted =>
            {
                var now = QuarantineSupport.Now();
                var blocked = new BlockedStock
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    BlockedNumber = posted.Number,
                    ItemId = posted.Bucket.ItemId,
                    SkuId = posted.Bucket.SkuId,
                    SkuLevel = posted.Bucket.SkuLevel,
                    LotId = posted.Bucket.LotId,
                    WarehouseId = posted.Bucket.WarehouseId,
                    LocationId = posted.Bucket.LocationId,
                    Quantity = posted.Bucket.Quantity,
                    BaseUomId = posted.BaseUomId,
                    SerialIds = posted.SerialIds,
                    BlockReasonCode = request.BlockReasonCode,
                    Note = request.Note,
                    BlockedAtUtc = now,
                    Origin = BlockedOrigin.ManualBlock,
                    SourceMovementId = posted.TransactionId,
                    CreateIdempotencyKey = keys[posted.Index],
                    CreateRequestFingerprint = fingerprint,
                    CreatedBy = _support.UserId,
                    PendingDisposition = false,
                    Reviewed = false
                };
                try
                {
                    await _repository.InTransactionAsync<bool>(async (session, c) =>
                    {
                        await session.InsertBlockedAsync(blocked, c);
                        await session.MarkBlockReasonUsedAsync(request.BlockReasonCode!, now, c);
                        await session.InsertHistoryAsync([_support.History(HistorySubject.Blocked, blocked.Id, HistoryAction.Blocked, now, reason)], c);
                        return true;
                    }, ct);
                }
                catch (QuarantineDuplicateException)
                {
                }
            }, ct);
        if (!result.IsSuccessful)
        {
            return Response<IReadOnlyList<BlockedStockRefDto>>.Fail(result.ErrorCode!, result.Errors[0], result.StatusCode, result.ErrorDetails);
        }

        var records = await _repository.FindBlockedByCreateKeysAsync(_support.UserId, keys, ct);
        var lots = await _facts.LotsAsync(records.Select(r => r.LotId), ct);
        var reasons = (await _repository.BlockReasonsAsync(ct)).ToDictionary(r => r.Code, StringComparer.Ordinal);
        IReadOnlyList<BlockedStockRefDto> body = records.OrderBy(r => keys.IndexOf(r.CreateIdempotencyKey!))
            .Select(r => QuarantineMapping.BlockedRef(r, lots, reasons, "en", null, null)).ToList();
        return Response<IReadOnlyList<BlockedStockRefDto>>.Success(body, done.Count == request.Buckets.Count ? 200 : 201);
    }
}
