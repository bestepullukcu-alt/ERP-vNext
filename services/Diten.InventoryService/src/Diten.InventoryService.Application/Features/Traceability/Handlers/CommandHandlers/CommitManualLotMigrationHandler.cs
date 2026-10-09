using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;

/// <summary>
/// commitManualLotMigration (pack §11.5, §8 "Migration re-key", CT-D6, CT K4-2…K4-5). Everything that can refuse is decided
/// BEFORE the transaction; a refusal writes nothing. Order:
/// <list type="number">
/// <item>Shape (400): idempotency key, reason ≥ 10 (<c>REASON_REQUIRED</c>), <c>expectedGroupCount</c>, decisions.</item>
/// <item>Completed tenant: same key + same body → 200 replay; same key, other body → 400 <c>IDEMPOTENCY_KEY_REUSED</c>;
/// another key → 409 <c>MIGRATION_ALREADY_COMPLETED</c>.</item>
/// <item>The groups now (PRODUCT-MASTER 503): another count than <c>expectedGroupCount</c>, or a decision for a group that
/// no longer exists → 409 <c>MIGRATION_STALE</c>.</item>
/// <item><see cref="ManualLotMigrationPlan"/>: a group without a complete decision → 422 <c>MIGRATION_DECISIONS_INCOMPLETE</c>;
/// an unresolved typo chain → 422 <c>MIGRATION_TYPO_UNRESOLVED</c>; a "link existing" lot that is not a lot of the
/// group's SKU → 422 <c>MIGRATION_DECISIONS_INCOMPLETE</c>.</item>
/// <item>New lots: the createLot field rules (400 <c>LOT_NUMBER_INVALID</c> / <c>LOT_DATES_INVALID</c> /
/// <c>INVALID_REQUEST</c>); the K-G key taken (by an existing lot or by two decisions) → 409 <c>LOT_ALREADY_EXISTS</c>.</item>
/// <item>ONE transaction (<see cref="IManualLotMigrationRepository.CommitAsync"/>): re-reads the groups (stale → 409),
/// writes lots (Draft, source migration), history (<c>Created</c> + <c>Migrated</c> / <c>Linked</c>), links, the ledger
/// re-key, serial lots and the completed record. No ledger transaction document changes.</item>
/// </list>
/// </summary>
public sealed class CommitManualLotMigrationHandler : IRequestHandler<CommitManualLotMigrationCommand, Response<MigrationResultDto>>
{
    private readonly IManualLotMigrationRepository _store;
    private readonly ILotRepository _lots;
    private readonly ManualLotGroupReader _groups;
    private readonly TraceWriteSupport _support;
    private readonly Common.ITenantContext _tenant;

    public CommitManualLotMigrationHandler(
        IManualLotMigrationRepository store,
        ILotRepository lots,
        ManualLotGroupReader groups,
        TraceWriteSupport support,
        Common.ITenantContext tenant)
    {
        _store = store;
        _lots = lots;
        _groups = groups;
        _support = support;
        _tenant = tenant;
    }

    public async Task<Response<MigrationResultDto>> Handle(CommitManualLotMigrationCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TraceMapping.Today(now);

        // 1. shape
        var keyError = TraceWriteSupport.KeyError(request.HeaderIdempotencyKey, request.IdempotencyKey, out var key);
        if (keyError is not null) return TraceWriteSupport.Invalid<MigrationResultDto>(keyError);
        var reason = TraceWriteSupport.Trimmed(request.Reason);
        if (!LotStatusRules.IsReasonValid(reason))
            return TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.ReasonRequired,
                $"The migration needs a reason of at least {LotStatusRules.MinReasonLength} characters.", 400);
        if (request.ExpectedGroupCount is not { } expectedCount || expectedCount < 0)
            return TraceWriteSupport.Invalid<MigrationResultDto>("expectedGroupCount is required (the number of groups the decisions were made on).");

        var decisions = new List<MigrationDecisionInput>();
        foreach (var raw in request.Decisions)
        {
            if (string.IsNullOrWhiteSpace(raw.GroupId) || !Enum.TryParse<MigrationDecisionKind>(raw.Kind, ignoreCase: false, out var kind)
                || !Enum.IsDefined(kind) || int.TryParse(raw.Kind, out _))
                return TraceWriteSupport.Invalid<MigrationResultDto>("Every decision needs a groupId and a kind: LinkExisting, Typo or NewLot.");
            Guid? lotId = null;
            if (raw.LotId is not null)
            {
                if (!TraceWriteSupport.TryParseId(raw.LotId, out var parsed))
                    return TraceWriteSupport.Invalid<MigrationResultDto>($"Decision {raw.GroupId}: lotId must be a UUID.");
                lotId = parsed;
            }

            if (kind == MigrationDecisionKind.NewLot && raw.NewLot is null)
                return TraceWriteSupport.Invalid<MigrationResultDto>($"Decision {raw.GroupId}: a NewLot decision carries newLot.");
            decisions.Add(new MigrationDecisionInput(raw.GroupId, kind, lotId, raw.TargetGroupId));
        }

        var fingerprint = Fingerprint(reason!, expectedCount, request.Decisions);

        // 2. completed tenant → replay / reuse / 409
        var existing = await _store.GetAsync(cancellationToken);
        if (existing is { ManualLotEntryClosed: true })
        {
            if (!string.Equals(existing.IdempotencyKey, key, StringComparison.Ordinal))
                return Completed();
            return string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? Response<MigrationResultDto>.Success(TraceMigrationMapping.ToResult(existing), 200)
                : TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.IdempotencyKeyReused,
                    "This idempotency key was already used for a different migration request.", 400);
        }

        // 3. the groups now
        ManualLotGroupSnapshot snapshot;
        try
        {
            snapshot = await _groups.ReadAsync(cancellationToken);
        }
        catch (ProductMasterUnavailableException)
        {
            return TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.ProductMasterUnavailable,
                "PRODUCT-MASTER could not say which items are lot-controlled; nothing was migrated.", 503);
        }

        var groups = snapshot.Groups.Select(g => g.Group).ToList();
        if (groups.Count != expectedCount)
            return Stale($"{expectedCount} groups were listed, {groups.Count} exist now; reload the list.");

        // 4. decisions → one target per group
        var plan = ManualLotMigrationPlan.Resolve(groups, decisions);
        if (!plan.IsValid)
        {
            var named = string.Join(", ", plan.Groups.Take(10));
            return plan.Error switch
            {
                MigrationPlanError.Stale => Stale($"Decisions name groups that no longer exist: {named}."),
                MigrationPlanError.Duplicate => TraceWriteSupport.Invalid<MigrationResultDto>($"A group has more than one decision: {named}."),
                MigrationPlanError.Incomplete => Incomplete($"Groups without a complete decision: {named}."),
                _ => TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.MigrationTypoUnresolved,
                    $"Typo decisions that do not reach a lot of the same SKU within {ManualLotMatcher.MaxTypoChain} steps: {named}.", 422)
            };
        }

        var byGroup = groups.ToDictionary(g => g.GroupId, StringComparer.Ordinal);
        var decided = decisions.ToDictionary(d => d.GroupId, StringComparer.Ordinal);
        var rawByGroup = request.Decisions.ToDictionary(d => d.GroupId!, StringComparer.Ordinal);

        var existingIds = plan.Targets.Where(t => t.ExistingLotId.HasValue).Select(t => t.ExistingLotId!.Value).Distinct().ToList();
        var existingLots = (await _lots.FindManyAsync(existingIds, cancellationToken)).ToDictionary(l => l.Id);
        var wrongLot = plan.Targets.Where(t => t.ExistingLotId is { } id
                                               && (!existingLots.TryGetValue(id, out var lot)
                                                   || lot.SkuId != byGroup[t.GroupId].SkuId || lot.SkuLevel != byGroup[t.GroupId].SkuLevel))
            .Select(t => t.GroupId).ToList();
        if (wrongLot.Count > 0)
            return Incomplete($"\"Link existing\" names a lot that is not a lot of the group's SKU: {string.Join(", ", wrongLot.Take(10))}.");

        // 5. new lots (createLot field rules + the K-G key)
        var newLots = new Dictionary<string, Lot>(StringComparer.Ordinal);
        foreach (var owner in plan.Targets.Where(t => t.NewLotOwnerGroupId is not null).Select(t => t.NewLotOwnerGroupId!).Distinct(StringComparer.Ordinal))
        {
            var group = byGroup[owner];
            var fields = rawByGroup[owner].NewLot!;
            var lotNumber = fields.LotNumber is null ? null : LotIdentityPolicy.NormalizeLotNumber(fields.LotNumber);
            if (!LotIdentityPolicy.IsValidLotNumber(lotNumber))
                return TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.LotNumberInvalid,
                    $"Decision {owner}: newLot.lotNumber must be 1-{LotIdentityPolicy.MaxLotNumberLength} characters of A-Z 0-9 . / - (trimmed, upper-cased).", 400);
            var attributes = LotAttributes.Read(fields.ManufacturerLot, fields.SupplierLot, fields.CountryOfOrigin, fields.CoaReference);
            if (attributes.Error is not null) return TraceWriteSupport.Invalid<MigrationResultDto>($"Decision {owner}: {attributes.Error}");
            var dates = LotAttributes.ReadDates(fields.ManufacturingDate, fields.ExpiryDate, fields.RetestDate, today);
            if (dates.Error is not null) return TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.LotDatesInvalid, $"Decision {owner}: {dates.Error}", 400);

            newLots[owner] = new Lot
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                ItemId = group.ItemId,
                SkuId = group.SkuId,
                SkuLevel = group.SkuLevel,
                LotNumber = lotNumber!,
                IdentityKey = LotIdentityPolicy.LotKey(_tenant.TenantId, group.SkuId, group.SkuLevel, lotNumber!),
                ManufacturerLot = attributes.ManufacturerLot,
                SupplierLot = attributes.SupplierLot,
                ManufacturingDate = dates.Manufacturing,
                ExpiryDate = dates.Expiry,
                RetestDate = dates.Retest,
                CountryOfOrigin = attributes.Country,
                CoaReference = attributes.Coa,
                Status = LotStatus.Draft,
                TrackingMode = snapshot.NeedsSerials[(group.SkuId, group.SkuLevel)] ? LotTrackingMode.LotAndSerial : LotTrackingMode.Lot,
                CreationSource = "Migration",
                CreatedBy = _support.UserId,
                Version = 1
            };
        }

        var clash = newLots.Values.GroupBy(l => l.IdentityKey, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1)?.First();
        if (clash is not null) return AlreadyExists(clash.LotNumber);
        var skuLots = await _store.LotsOfSkusAsync(newLots.Values.Select(l => (l.SkuId, l.SkuLevel)).Distinct().ToList(), cancellationToken);
        var taken = newLots.Values.FirstOrDefault(l => skuLots.Any(r => r.SkuId == l.SkuId && r.SkuLevel == l.SkuLevel && r.LotNumber == l.LotNumber));
        if (taken is not null) return AlreadyExists(taken.LotNumber);

        // 6. one transaction
        var links = plan.Targets.Select(t => new ManualLotMigrationLink(byGroup[t.GroupId],
            t.ExistingLotId ?? newLots[t.NewLotOwnerGroupId!].Id)).ToList();
        var history = new List<LotChangeEntry>();
        foreach (var lot in newLots.Values)
        {
            history.Add(Migration(_support.History(lot.Id, LotChangeAction.Created, null, null, lot.LotNumber, reason, now)));
        }

        foreach (var link in links)
        {
            var action = newLots.Values.Any(l => l.Id == link.LotId) ? LotChangeAction.Migrated : LotChangeAction.Linked;
            history.Add(Migration(_support.History(link.LotId, action, "typedText", null, link.Group.TypedText, reason, now)));
        }

        try
        {
            var done = await _store.CommitAsync(new ManualLotMigrationCommit(
                key, fingerprint, reason!, _support.UserId, now,
                groups.Select(g => g.GroupId).ToHashSet(StringComparer.Ordinal),
                snapshot.ExcludedItemIds,
                newLots.Values.ToList(),
                links,
                history), cancellationToken);
            return Response<MigrationResultDto>.Success(TraceMigrationMapping.ToResult(done), 200);
        }
        catch (MigrationAlreadyCompletedException)
        {
            return Completed();
        }
        catch (MigrationStaleException stale)
        {
            return Stale(stale.Message);
        }
        catch (LotUniqueViolationException)
        {
            return AlreadyExists(newLots.Values.FirstOrDefault()?.LotNumber ?? string.Empty);
        }
    }

    private static LotChangeEntry Migration(LotChangeEntry entry)
    {
        entry.Source = TraceWriteSupport.SourceMigration;
        return entry;
    }

    /// <summary>The request as the server understood it: reason, count and every decision (order-independent).</summary>
    private static string Fingerprint(string reason, int expectedCount, IReadOnlyList<MigrationDecisionRaw> decisions)
    {
        var fields = new List<(string Name, string? Value)> { ("reason", reason), ("expectedGroupCount", expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        foreach (var d in decisions.OrderBy(d => d.GroupId, StringComparer.Ordinal))
        {
            var n = d.NewLot;
            fields.Add(("decision", string.Join("|", d.GroupId, d.Kind, d.LotId, d.TargetGroupId,
                n?.LotNumber, n?.ManufacturerLot, n?.SupplierLot, n?.ManufacturingDate, n?.ExpiryDate, n?.RetestDate, n?.CountryOfOrigin, n?.CoaReference)));
        }

        return TraceWriteSupport.Fingerprint(fields.ToArray());
    }

    private static Response<MigrationResultDto> Completed()
        => TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.MigrationAlreadyCompleted,
            "The typed-lot migration of this tenant is already completed; typing is closed.", 409);

    private static Response<MigrationResultDto> Stale(string message)
        => TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.MigrationStale, message, 409);

    private static Response<MigrationResultDto> Incomplete(string message)
        => TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.MigrationDecisionsIncomplete, message, 422);

    private static Response<MigrationResultDto> AlreadyExists(string lotNumber)
        => TraceWriteSupport.Fail<MigrationResultDto>(TraceErrorCodes.LotAlreadyExists, $"Lot '{lotNumber}' already exists for this SKU.", 409);
}
