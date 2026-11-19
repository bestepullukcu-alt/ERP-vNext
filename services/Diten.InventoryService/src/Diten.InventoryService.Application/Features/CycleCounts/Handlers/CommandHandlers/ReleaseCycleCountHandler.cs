using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>releaseCycleCount</c> (pack §8.2; AC-2, AC-4, AC-13, AC-52 (a)). Draft only. <c>SnapshotAt</c> = the moment the release begins on
/// MOD-0181's own clock. ONE snapshot transaction (pack §8.2 rule 4): the scope bins, their AVAILABLE balance rows with stock (Q-8 = A) and the
/// window's movement ids are read from one snapshot; then the count (compare-and-set), a claim for EVERY scope bin (an empty bin too — F-6;
/// the partial unique index refuses a bin another open count holds → 409 <c>COUNT_SCOPE_OVERLAP</c>, the loser keeps nothing), the
/// <c>Snapshot</c> lines and the <c>Released</c> event (counts only — no expected quantity, F-13). PRODUCT-MASTER's serial flags are read
/// OUTSIDE the transaction, from a plain pre-read of the scope's SKUs: a SKU that arrives between that pre-read and the snapshot ends the attempt
/// without a write and its flags are read before the next attempt (at most three). No ledger movement is ever blocked (Q-6 = B).
/// </summary>
public sealed class ReleaseCycleCountHandler : CycleCountCommandHandler, IRequestHandler<ReleaseCycleCountCommand, Response<CycleCountDto>>
{
    private const int MaxAttempts = 3;

    private readonly CycleCountProductFlags _products;

    public ReleaseCycleCountHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountProductFlags products)
        : base(repository, support) => _products = products;

    public async Task<Response<CycleCountDto>> Handle(ReleaseCycleCountCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpReleaseCycleCount;
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("countId", request.CountId.ToString("D")));
        if (await ReplayAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var count = await Repository.FindAsync(request.CountId, ct);
        if (count is null) return CycleCountSupport.NotFound<CycleCountDto>();
        if (count.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountDto>();
        if (!CycleCountRules.IsDraft(count)) return CycleCountSupport.InvalidState<CycleCountDto>(count.Status);

        var snapshotAt = Support.Now();
        var scope = new CycleCountScopeRequest(count.WarehouseId, count.CountType == CycleCountType.Full, count.ScopeLocationIds, count.ScopeSkuIds,
            CycleCountRules.WindowStart(snapshotAt));
        var flags = new Dictionary<(Guid ItemId, Guid SkuId, SkuLevel Level), bool>();
        // DL-3 (Q-24 = A): the line limit is the company setting (default 2000)
        var limit = CycleCountPolicy.Of(await Repository.FindSettingsAsync(ct)).MaxLinesPerCount;
        try
        {
            // the flags of the scope's SKUs BEFORE the snapshot transaction (no HTTP call while it is open): normally ONE snapshot attempt
            var (known, refusal) = await _products.SerialFlagsAsync<CycleCountDto>(await Repository.ScopeSkusAsync(scope, ct), ct);
            if (refusal is not null) return refusal;
            foreach (var (key, value) in known!) flags[key] = value;
        }
        catch (CycleCountInventoryUnavailableException)
        {
            return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.InventoryUnavailable, "The stock of the scope could not be read; nothing was written.", 503);
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            Attempt result;
            try
            {
                result = await TryReleaseAsync(count, scope, snapshotAt, flags, limit, request.IdempotencyKey, fingerprint, ct);
            }
            catch (CycleCountDuplicateException ex)
            {
                if (await ReplayAsync(count.Id, op, request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
                return ex.Index == CycleCountUniqueIndex.BinClaim
                    ? CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.ScopeOverlap,
                        "A bin of this scope is already being counted by another open count.", 409)
                    : CycleCountSupport.Conflict<CycleCountDto>();
            }
            catch (CycleCountInventoryUnavailableException)
            {
                return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.InventoryUnavailable, "The stock snapshot could not be read; nothing was written.", 503);
            }

            switch (result.Kind)
            {
                case AttemptKind.Released:
                    return Response<CycleCountDto>.Success(CycleCountReads.Dto(result.Count!));
                case AttemptKind.Refused:
                    return result.Refusal!;
                case AttemptKind.Lost:
                    return await ReplayAsync(count.Id, op, request.IdempotencyKey, fingerprint, ct) ?? CycleCountSupport.Conflict<CycleCountDto>();
                case AttemptKind.NeedsFlags:
                    var (read, refusal) = await _products.SerialFlagsAsync<CycleCountDto>(result.MissingFlags!, ct);
                    if (refusal is not null) return refusal;
                    foreach (var (key, value) in read!) flags[key] = value;
                    break;
            }
        }

        return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.Conflict, "The scope kept changing while it was read; retry the release.", 409);
    }

    private enum AttemptKind
    {
        Released,
        Refused,
        Lost,
        NeedsFlags
    }

    private sealed record Attempt(AttemptKind Kind, CycleCount? Count = null, Response<CycleCountDto>? Refusal = null,
        IReadOnlyList<(Guid ItemId, Guid SkuId, SkuLevel Level)>? MissingFlags = null);

    private async Task<Attempt> TryReleaseAsync(CycleCount count, CycleCountScopeRequest scope, DateTimeOffset snapshotAt,
        IReadOnlyDictionary<(Guid ItemId, Guid SkuId, SkuLevel Level), bool> flags, int limit, string key, string fingerprint, CancellationToken ct)
    {
        Attempt result = new(AttemptKind.Lost);
        await Repository.InTransactionAsync(async (session, tx) =>
        {
            var snapshot = await session.ReadScopeSnapshotAsync(scope, tx);
            if (snapshot.BinIds.Count == 0)
            {
                result = new Attempt(AttemptKind.Refused, Refusal: CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.ScopeEmpty,
                    "The scope has no active stockable location to count.", 422));
                return new CycleCountWriteOutcome(0, count.Id);
            }

            if (snapshot.BinIds.Count > limit || snapshot.Rows.Count > limit)
            {
                result = new Attempt(AttemptKind.Refused, Refusal: CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.ScopeTooLarge,
                    "The scope is larger than one count may hold; split it.", 422, new Dictionary<string, object?>
                    {
                        ["binCount"] = snapshot.BinIds.Count, ["lineCount"] = snapshot.Rows.Count, ["maxLinesPerCount"] = limit
                    }));
                return new CycleCountWriteOutcome(0, count.Id);
            }

            var missing = snapshot.Rows.Select(r => (r.ItemId, r.SkuId, r.SkuLevel)).Distinct().Where(k => !flags.ContainsKey(k)).ToList();
            if (missing.Count > 0)
            {
                result = new Attempt(AttemptKind.NeedsFlags, MissingFlags: missing);
                return new CycleCountWriteOutcome(0, count.Id);
            }

            var now = Support.Now();
            var copy = Copy(count);
            copy.Status = CycleCountStatus.Released;
            copy.SnapshotAt = snapshotAt;
            copy.ScopeBinIds = [.. snapshot.BinIds];
            copy.SnapshotTransactionIds = [.. snapshot.TransactionIds];
            copy.LineCount = snapshot.Rows.Count;
            copy.Version = count.Version + 1;
            copy.UpdatedAt = now;

            var lines = snapshot.Rows
                .OrderBy(r => r.LocationId).ThenBy(r => r.SkuId).ThenBy(r => r.LotId, StringComparer.Ordinal)
                .Select(r => new CycleCountLine
                {
                    Id = Guid.NewGuid(), CreatedAt = now, Version = 1, CountId = count.Id, WarehouseId = count.WarehouseId, LocationId = r.LocationId,
                    ItemId = r.ItemId, SkuId = r.SkuId, SkuLevel = r.SkuLevel, LotId = r.LotId, StockStatus = r.StockStatus, BaseUomId = r.BaseUomId,
                    Origin = CycleCountLineOrigin.Snapshot, ExpectedQuantity = r.OnHand, SerialControlled = flags[(r.ItemId, r.SkuId, r.SkuLevel)],
                    Status = CycleCountLineStatus.Open
                }).ToList();
            var claims = snapshot.BinIds.Select(bin => new CycleCountBinClaim
            {
                Id = Guid.NewGuid(), CreatedAt = now, Version = 1, BinId = bin, CountId = count.Id, Open = true, ClaimedAt = now
            }).ToList();
            // F-13: the Released event carries counts only — never a per-line expected quantity
            var released = Support.Event(copy, CycleCountEventKind.Released, now, new Dictionary<string, string?>
            {
                ["snapshotAt"] = CycleCountSupport.Wire(snapshotAt), ["binCount"] = CycleCountSupport.Int(snapshot.BinIds.Count),
                ["lineCount"] = CycleCountSupport.Int(lines.Count), ["claimedBinCount"] = CycleCountSupport.Int(claims.Count)
            }, key, CycleCountContract.OpReleaseCycleCount, fingerprint);

            if (!await session.ReplaceCountAsync(copy, count.Version, tx))
            {
                result = new Attempt(AttemptKind.Lost);
                return new CycleCountWriteOutcome(0, count.Id);
            }

            await session.InsertBinClaimsAsync(claims, tx);
            await session.InsertLinesAsync(lines, tx);
            await session.InsertEventsAsync([released], tx);
            result = new Attempt(AttemptKind.Released, copy);
            return new CycleCountWriteOutcome(1, count.Id);
        }, ct);
        return result;
    }
}
