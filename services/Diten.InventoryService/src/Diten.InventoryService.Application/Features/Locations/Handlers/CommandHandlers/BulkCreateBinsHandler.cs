using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Commands;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.CommandHandlers;

/// <summary>
/// bulkCreateBins (MOD-0173-FU02 K-12). Shape → count 1…1000 (400 <c>LOCATION_BULK_LIMIT</c>, before a code is built)
/// → target in the caller's company (404), not Archived (409), a Zone or StorageLocation (422 level), Active (422) →
/// codes <c>{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}</c>, existing codes (any status) skipped.
/// <list type="bullet">
/// <item><c>dryRun: true</c> → 200 <c>{ toCreate, skipped, count }</c>; nothing is written, the key is not consumed.</item>
/// <item>Real run → ONE transaction: every bin + one <c>BulkCreated</c> history row per bin + the target's structure
/// touch → 201 <c>{ created, skipped, count }</c>.</item>
/// <item>Replay: the bins carry the batch key + fingerprint; same body → 200 with the same <c>created</c>, the skipped
/// list recomputed as the requested codes the batch did not create; different body → 400. A batch that created
/// nothing leaves no key behind (nothing to replay, nothing written — ASSUMPTION-LOC1-06).</item>
/// </list>
/// </summary>
public sealed class BulkCreateBinsHandler : IRequestHandler<BulkCreateBinsCommand, Response<BulkBinsResultDto>>
{
    private readonly ILocationRepository _locations;
    private readonly LocationWriteSupport _support;

    public BulkCreateBinsHandler(ILocationRepository locations, LocationWriteSupport support)
    {
        _locations = locations;
        _support = support;
    }

    public async Task<Response<BulkBinsResultDto>> Handle(BulkCreateBinsCommand request, CancellationToken cancellationToken)
    {
        var keyError = LocationWriteSupport.KeyError(request.HeaderIdempotencyKey, request.IdempotencyKey, out var key);
        if (keyError is not null) return LocationWriteSupport.Invalid<BulkBinsResultDto>(keyError);

        if (request.AisleFrom is null || request.AisleTo is null || request.RackFrom is null || request.RackTo is null
            || request.LevelFrom is null || request.LevelTo is null || request.PositionsPerLevel is null)
        {
            return LocationWriteSupport.Invalid<BulkBinsResultDto>(
                "aisleFrom, aisleTo, rackFrom, rackTo, levelFrom, levelTo and positionsPerLevel are required.");
        }

        var aisleFrom = request.AisleFrom.Trim().ToUpperInvariant();
        var aisleTo = request.AisleTo.Trim().ToUpperInvariant();
        var plan = LocationHierarchyRules.PlanBulkBins(aisleFrom, aisleTo, request.RackFrom.Value, request.RackTo.Value,
            request.LevelFrom.Value, request.LevelTo.Value, request.PositionsPerLevel.Value, out var shapeError);
        if (plan is null) return LocationWriteSupport.Invalid<BulkBinsResultDto>(shapeError!);
        if (plan.Count is < 1 or > LocationHierarchyRules.MaxBulkBins)
        {
            return LocationWriteSupport.Fail<BulkBinsResultDto>(LocationErrorCodes.BulkLimit,
                $"The range describes {plan.Count} bins; one batch may create 1-{LocationHierarchyRules.MaxBulkBins}.", 400);
        }

        if (!LocationWriteSupport.TryParseId(request.LocationId, out var targetId)) return LocationWriteSupport.NotFound<BulkBinsResultDto>();

        var stockable = request.IsStockable ?? true;
        var dryRun = request.DryRun ?? false;
        var codes = plan.Codes();
        var fingerprint = LocationWriteSupport.Fingerprint(
            ("target", targetId.ToString("D")), ("aisleFrom", aisleFrom), ("aisleTo", aisleTo),
            ("rackFrom", LocationWriteSupport.Wire(request.RackFrom)), ("rackTo", LocationWriteSupport.Wire(request.RackTo)),
            ("levelFrom", LocationWriteSupport.Wire(request.LevelFrom)), ("levelTo", LocationWriteSupport.Wire(request.LevelTo)),
            ("positionsPerLevel", LocationWriteSupport.Wire(request.PositionsPerLevel)), ("isStockable", LocationWriteSupport.Wire(stockable)));

        if (!dryRun)
        {
            var committed = await _locations.FindByBulkKeyAsync(key, cancellationToken);
            if (committed.Count > 0) return Replay(committed, fingerprint, codes);
        }

        if (dryRun)
        {
            var target = await _locations.FindAsync(targetId, cancellationToken);
            if (TargetRefusal(target) is { } refusal) return refusal;

            var taken = await _locations.TakenCodesAsync(codes.Select(LocationHierarchyRules.NormalizeCode).ToList(), cancellationToken);
            var toCreate = codes.Where(c => !taken.Contains(LocationHierarchyRules.NormalizeCode(c))).ToList();
            return Response<BulkBinsResultDto>.Success(new BulkBinsResultDto
            {
                ToCreate = toCreate,
                Skipped = codes.Except(toCreate, StringComparer.Ordinal).ToList(),
                Count = toCreate.Count
            }, 200);
        }

        Response<BulkBinsResultDto> outcome;
        try
        {
            outcome = await _locations.InTransactionAsync(async (tx, ct) =>
            {
                var target = await tx.FindAsync(targetId, ct);
                if (TargetRefusal(target) is { } refusal) return refusal;

                var taken = await tx.TakenCodesAsync(codes.Select(LocationHierarchyRules.NormalizeCode).ToList(), ct);
                var now = DateTimeOffset.UtcNow;
                var bins = codes
                    .Where(c => !taken.Contains(LocationHierarchyRules.NormalizeCode(c)))
                    .Select(c => new Location
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = now,
                        Code = c,
                        CodeNormalized = LocationHierarchyRules.NormalizeCode(c),
                        Name = c,
                        Type = LocationType.Bin,
                        ParentId = target!.Id,
                        AncestorIds = [.. target.AncestorIds, target.Id],
                        Status = LocationStatus.Active,
                        IsStockable = stockable,
                        Version = 1,
                        CreatedBy = _support.UserId,
                        BulkIdempotencyKey = key,
                        BulkRequestFingerprint = fingerprint
                    })
                    .ToList();

                if (bins.Count > 0)
                {
                    await tx.InsertAsync(bins, ct);
                    await tx.TouchStructureAsync(target!.Id, ct);
                    await tx.AppendHistoryAsync(
                        bins.Select(b => _support.History(b.Id, LocationChangeAction.BulkCreated, null, null, b.Code, now)).ToList(), ct);
                }

                return Response<BulkBinsResultDto>.Success(Result(bins, codes), 201);
            }, cancellationToken);
        }
        catch (LocationUniqueViolationException violation) when (violation.Index == LocationUniqueIndex.Code)
        {
            // A concurrent batch with the same key committed first → replay it; otherwise a code was taken meanwhile.
            var winner = await _locations.FindByBulkKeyAsync(key, cancellationToken);
            return winner.Count > 0
                ? Replay(winner, fingerprint, codes)
                : LocationWriteSupport.Fail<BulkBinsResultDto>(LocationErrorCodes.CodeTaken,
                    "A code of this range was taken by a concurrent change; preview again.", 409);
        }

        // Nothing created because a concurrent batch with the SAME key committed these codes first: that is a replay.
        if (outcome.IsSuccessful && outcome.Data!.Count == 0
            && await _locations.FindByBulkKeyAsync(key, cancellationToken) is { Count: > 0 } sameKey)
        {
            return Replay(sameKey, fingerprint, codes);
        }

        return outcome;
    }

    private static Response<BulkBinsResultDto>? TargetRefusal(Location? target)
    {
        if (target is null) return LocationWriteSupport.NotFound<BulkBinsResultDto>();
        if (target.Status == LocationStatus.Archived)
            return LocationWriteSupport.Fail<BulkBinsResultDto>(LocationErrorCodes.Archived, "The target location is archived.", 409);
        if (!LocationHierarchyRules.MayHoldBulkBins(target.Type))
            return LocationWriteSupport.Fail<BulkBinsResultDto>(LocationErrorCodes.LevelInvalid,
                $"Bins are created under a Zone or a StorageLocation, not under a {target.Type}.", 422);
        if (target.Status != LocationStatus.Active)
            return LocationWriteSupport.Fail<BulkBinsResultDto>(LocationErrorCodes.ParentInactive, "The target location is not active.", 422);
        return null;
    }

    private static Response<BulkBinsResultDto> Replay(IReadOnlyList<Location> committed, string fingerprint, IReadOnlyList<string> codes)
        => committed.All(b => string.Equals(b.BulkRequestFingerprint, fingerprint, StringComparison.Ordinal))
            ? Response<BulkBinsResultDto>.Success(Result(committed, codes), 200)
            : LocationWriteSupport.Fail<BulkBinsResultDto>(LocationErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different bin range.", 400);

    private static BulkBinsResultDto Result(IReadOnlyList<Location> created, IReadOnlyList<string> codes)
    {
        var order = codes.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i, StringComparer.Ordinal);
        var createdCodes = created.Select(b => b.Code).ToHashSet(StringComparer.Ordinal);
        return new BulkBinsResultDto
        {
            Created = created.OrderBy(b => order.GetValueOrDefault(b.Code, int.MaxValue)).Select(b => LocationMapping.ToRef(b)).ToList(),
            Skipped = codes.Where(c => !createdCodes.Contains(c)).ToList(),
            Count = created.Count
        };
    }
}
