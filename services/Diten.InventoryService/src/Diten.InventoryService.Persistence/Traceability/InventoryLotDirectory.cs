using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// The ledger's lot / serial source, in-process (MOD-0174 pack §3 / §8, WP-0174-P2) — the counterpart of the FU02
/// <c>InventoryLocationDirectory</c>. Lots and serials are tenant-wide (CT-D3 / CT-D7); stock-side facts (the company a
/// serial is in, its location) are the caller's company. Serial keys come ONLY from <see cref="LotIdentityPolicy"/>.
/// <para>
/// While the ledger repository has bound its posting transaction (<see cref="LedgerTransactionBinding"/>), every read and
/// write here runs in that transaction: the lot read also writes the lot's <c>StockTouchSeq</c>, so a status change of the
/// same lot (which replaces the lot document in its own transaction) and the posting conflict in Mongo and the loser
/// re-runs on the committed state. Serial writes are conditional (status + company + location in the filter); a count that
/// does not match means the state changed after the check → <see cref="LotPostingRefusedException"/>.
/// </para>
/// <para>
/// WP-0174-P4 (CT K4-2): a TYPED lot text read inside the posting transaction writes the tenant's typed-lot gate (the
/// <c>trace_lot_migrations</c> document, <c>GateSeq</c> + 1, upsert); the migration commit writes the same document, so a
/// typed posting and the commit conflict and the loser re-runs on the committed state. Once typing is closed, a posting
/// whose pre-check set <see cref="TypedLotPostingGuard.RefuseTypedWhenClosed"/> (lot-controlled inbound) is refused with
/// <c>UNKNOWN_LOT</c> here; every other typed posting keeps today's behaviour (K4-4). The link reads serve the reversal
/// of a migrated transaction (K4-3) and the read-side resolution of <c>getTransactions</c> (CT-D6).
/// </para>
/// </summary>
public sealed class InventoryLotDirectory : ILotMasterClient
{
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<SerialNumber> _serials;
    private readonly IMongoCollection<ManualLotMigration> _migrations;
    private readonly IMongoCollection<MovementLotLink> _links;
    private readonly LedgerTransactionBinding _binding;
    private readonly TypedLotPostingGuard _typedGuard;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public InventoryLotDirectory(IMongoDatabase database, ITenantContext tenantContext, LedgerTransactionBinding binding, TypedLotPostingGuard typedGuard)
    {
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _serials = database.GetCollection<SerialNumber>(TraceCollections.Serials);
        _migrations = database.GetCollection<ManualLotMigration>(TraceCollections.LotMigrations);
        _links = database.GetCollection<MovementLotLink>(TraceCollections.MovementLotLinks);
        _binding = binding;
        _typedGuard = typedGuard;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    // ── lots ──────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Lot?> ResolveLotAsync(string lotId, CancellationToken cancellationToken = default)
    {
        if (!LotPostingRules.IsRegisteredLotForm(lotId, out var id))
        {
            if (_binding.Session is { } posting)
            {
                await TouchTypedLotGateAsync(posting, lotId, cancellationToken);
            }

            return null;
        }

        var filter = Scope<Lot>() & Builders<Lot>.Filter.Eq(x => x.Id, id);
        if (_binding.Session is not { } session)
        {
            return await _lots.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        return await _lots.FindOneAndUpdateAsync(
            session,
            filter,
            Builders<Lot>.Update.Inc(x => x.StockTouchSeq, 1L),
            new FindOneAndUpdateOptions<Lot> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<IReadOnlySet<string>> HeldLotIdsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
    {
        var ids = lotIds.Select(l => LotPostingRules.IsRegisteredLotForm(l, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new HashSet<string>();
        }

        // CT-Q16: Blocked / Expired / stored retest hold — the same question the SKU position was adjusted for
        var lots = await _lots.Find(Scope<Lot>() & Builders<Lot>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);
        return lots.Where(LotPostingRules.HoldsStock).Select(l => l.Id.ToString("D")).ToHashSet(StringComparer.Ordinal);
    }

    // ── typed-lot migration (WP-0174-P4) ──────────────────────────────────────────────────────────────────

    public async Task<bool> IsManualLotEntryClosedAsync(CancellationToken cancellationToken = default)
        => await _migrations.Find(Builders<ManualLotMigration>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)
                                  & Builders<ManualLotMigration>.Filter.Eq(x => x.ManualLotEntryClosed, true))
            .AnyAsync(cancellationToken);

    public async Task<string?> LinkedLotIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
        => (await LinkedLotIdsAsync([transactionId], cancellationToken)).TryGetValue(transactionId, out var lotId) ? lotId : null;

    public async Task<IReadOnlyDictionary<Guid, string>> LinkedLotIdsAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = transactionIds.Select(t => t.ToString("D")).Distinct(StringComparer.Ordinal).ToList();
        var links = await _links.Find(CompanyLinks() & Builders<MovementLotLink>.Filter.In(x => x.TransactionId, ids)).ToListAsync(cancellationToken);
        return links.ToDictionary(l => Guid.Parse(l.TransactionId), l => l.LotId.ToString("D"));
    }

    public async Task<IReadOnlyList<(Guid TransactionId, string TypedText)>> LinksOfLotAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        var links = await _links.Find(CompanyLinks() & Builders<MovementLotLink>.Filter.Eq(x => x.LotId, lotId)).ToListAsync(cancellationToken);
        return links.Select(l => (Guid.Parse(l.TransactionId), l.TypedText)).ToList();
    }

    /// <summary>
    /// The typed-lot gate (CT K4-2): one write of the tenant's migration document in the posting transaction. A closed
    /// tenant refuses a lot-controlled inbound typed text (the pre-check's rule on the committed state).
    /// </summary>
    private async Task TouchTypedLotGateAsync(IClientSessionHandle session, string typedText, CancellationToken ct)
    {
        var u = Builders<ManualLotMigration>.Update;
        var gate = await _migrations.FindOneAndUpdateAsync(
            session,
            Builders<ManualLotMigration>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            u.Combine(
                u.Inc(x => x.GateSeq, 1L),
                u.SetOnInsert(x => x.Id, Guid.NewGuid()),
                u.SetOnInsert(x => x.LegalEntityId, (Guid?)_legalEntityId),
                u.SetOnInsert(x => x.CreatedAt, DateTimeOffset.UtcNow),
                u.SetOnInsert(x => x.IsDeleted, false),
                u.SetOnInsert(x => x.DeletedAt, null),
                u.SetOnInsert(x => x.ManualLotEntryClosed, false)),
            new FindOneAndUpdateOptions<ManualLotMigration> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);
        if (gate.ManualLotEntryClosed && _typedGuard.RefuseTypedWhenClosed)
        {
            throw new LotPostingRefusedException(InventoryErrorCodes.UnknownLot,
                "lotId must name a registered lot: typed lot entry is closed for this tenant (migration completed).",
                new Dictionary<string, object?> { ["lotId"] = typedText });
        }
    }

    /// <summary>Links of the caller's company (a link carries the company of its ledger transaction).</summary>
    private FilterDefinition<MovementLotLink> CompanyLinks()
        => Scope<MovementLotLink>() & Builders<MovementLotLink>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId);

    // ── serials ───────────────────────────────────────────────────────────────────────────────────────────

    public async Task<SerialCheck> ValidateSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default)
    {
        var numbers = movement.SerialNumbers.Select(LotIdentityPolicy.NormalizeSerial).ToList();
        var repeated = numbers.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repeated.Count > 0)
        {
            // Two entries that are one serial after normalisation (trim + upper-case): one of them cannot be received / moved.
            return movement.IsInbound
                ? Refuse(InventoryErrorCodes.SerialAlreadyExists, "The same serial is named twice in the movement.", repeated)
                : Refuse(InventoryErrorCodes.SerialNotAvailable, "The same serial is named twice in the movement.", repeated);
        }

        var existing = await ExistingAsync(movement, numbers, cancellationToken);

        if (movement.IsInbound)
        {
            var active = numbers.Where(n => existing.TryGetValue(n, out var s) && s.Status == SerialStatus.Active).ToList();
            if (active.Count > 0)
            {
                return Refuse(InventoryErrorCodes.SerialAlreadyExists, "A serial is already Active for this SKU (CT-D7).", active);
            }

            var otherLot = numbers.Where(n => existing.TryGetValue(n, out var s) && s.LotId != movement.LotId).ToList();
            return otherLot.Count > 0
                ? Refuse(InventoryErrorCodes.SerialLotMismatch, "A serial belongs to another lot than the movement's lot.", otherLot)
                : SerialCheck.Valid;
        }

        var notAvailable = numbers.Where(n => existing.TryGetValue(n, out var s)
                ? s.Status != SerialStatus.Active || s.LegalEntityId != _legalEntityId
                  || !string.Equals(s.LocationId, movement.FromLocationId, StringComparison.Ordinal)
                : !movement.AcceptUnregistered)
            .ToList();
        if (notAvailable.Count > 0)
        {
            return Refuse(InventoryErrorCodes.SerialNotAvailable,
                "A serial is unknown, Consumed, or not at the source location of this company.", notAvailable);
        }

        var mismatched = numbers.Where(n => existing.TryGetValue(n, out var s) && s.LotId != movement.LotId).ToList();
        return mismatched.Count > 0
            ? Refuse(InventoryErrorCodes.SerialLotMismatch, "A serial belongs to another lot than the movement's lot.", mismatched)
            : SerialCheck.Valid;
    }

    public async Task RegisterSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default)
    {
        var session = RequireSession();
        var at = movement.At ?? throw new ArgumentException("A serial write needs the posting time.", nameof(movement));
        var transactionId = TransactionIdOf(movement);
        var numbers = movement.SerialNumbers.Select(LotIdentityPolicy.NormalizeSerial).ToList();
        var existing = await ExistingAsync(movement, numbers, cancellationToken);

        var u = Builders<SerialNumber>.Update;
        var f = Builders<SerialNumber>.Filter;
        var inserts = new List<SerialNumber>();
        foreach (var number in numbers)
        {
            if (!existing.TryGetValue(number, out var serial))
            {
                if (movement.RegisterNew)
                {
                    inserts.Add(New(movement, number, transactionId, at));
                }

                continue;
            }

            // A Consumed serial comes back (customer return, reversal of an issue): Active again in the receiving company.
            var restored = await _serials.UpdateOneAsync(
                session,
                Scope<SerialNumber>() & f.Eq(x => x.Id, serial.Id) & f.Eq(x => x.Status, SerialStatus.Consumed),
                u.Combine(
                    u.Set(x => x.Status, SerialStatus.Active),
                    u.Set(x => x.LegalEntityId, (Guid?)_legalEntityId),
                    u.Set(x => x.LotId, movement.LotId),
                    u.Set(x => x.WarehouseId, movement.ToWarehouseId),
                    u.Set(x => x.LocationId, movement.ToLocationId),
                    u.Set(x => x.LastTransactionId, transactionId),
                    u.Set(x => x.LastMovementAt, at),
                    u.Set(x => x.UpdatedAt, at),
                    u.Inc(x => x.Version, 1)),
                cancellationToken: cancellationToken);
            if (restored.ModifiedCount != 1)
            {
                throw Refused(InventoryErrorCodes.SerialAlreadyExists, "A serial is already Active for this SKU (CT-D7).", [number]);
            }
        }

        if (inserts.Count == 0)
        {
            return;
        }

        try
        {
            await _serials.InsertManyAsync(session, inserts, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey
                                                                         && e.Message.Contains(TraceIndexConfiguration.SerialIdentityIndex, StringComparison.Ordinal)))
        {
            throw Refused(InventoryErrorCodes.SerialAlreadyExists, "A serial was registered concurrently for this SKU (CT-D7).",
                ex.WriteErrors.Select(e => inserts[e.Index].Number).ToList());
        }
    }

    public Task ConsumeSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default)
        => MoveActiveAsync(movement, consume: true, cancellationToken);

    public Task MoveSerialsAsync(SerialMovement movement, CancellationToken cancellationToken = default)
        => MoveActiveAsync(movement, consume: false, cancellationToken);

    /// <summary>
    /// Outbound / transfer write: every serial the registry knows must still be Active, in this company, at the source
    /// location (conditional filter); an unknown one passed the check only as a pre-P2 serial and is not written.
    /// </summary>
    private async Task MoveActiveAsync(SerialMovement movement, bool consume, CancellationToken ct)
    {
        var session = RequireSession();
        var at = movement.At ?? throw new ArgumentException("A serial write needs the posting time.", nameof(movement));
        var numbers = movement.SerialNumbers.Select(LotIdentityPolicy.NormalizeSerial).ToList();
        var known = (await ExistingAsync(movement, numbers, ct)).Values.ToList();
        if (known.Count == 0)
        {
            return;
        }

        var f = Builders<SerialNumber>.Filter;
        var u = Builders<SerialNumber>.Update;
        var update = u.Combine(
            u.Set(x => x.LastTransactionId, TransactionIdOf(movement)),
            u.Set(x => x.LastMovementAt, at),
            u.Set(x => x.UpdatedAt, at),
            u.Inc(x => x.Version, 1));
        update = consume
            ? u.Combine(update, u.Set(x => x.Status, SerialStatus.Consumed))
            : u.Combine(update, u.Set(x => x.WarehouseId, movement.ToWarehouseId), u.Set(x => x.LocationId, movement.ToLocationId));

        var result = await _serials.UpdateManyAsync(
            session,
            Scope<SerialNumber>() & f.In(x => x.Id, known.Select(s => s.Id))
            & f.Eq(x => x.Status, SerialStatus.Active)
            & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
            & f.Eq(x => x.LocationId, movement.FromLocationId),
            update,
            cancellationToken: ct);
        if (result.ModifiedCount != known.Count)
        {
            throw Refused(InventoryErrorCodes.SerialNotAvailable,
                "A serial is unknown, Consumed, or not at the source location of this company.", known.Select(s => s.Number).ToList());
        }
    }

    /// <summary>The registry rows of these serial numbers for the movement's SKU, keyed by number — ONE query.</summary>
    private async Task<Dictionary<string, SerialNumber>> ExistingAsync(SerialMovement movement, IReadOnlyList<string> numbers, CancellationToken ct)
    {
        var keys = numbers.Select(n => LotIdentityPolicy.SerialKey(_tenantId, movement.SkuId, movement.SkuLevel, n)).Distinct().ToList();
        var filter = Scope<SerialNumber>() & Builders<SerialNumber>.Filter.In(x => x.IdentityKey, keys);
        var rows = _binding.Session is { } session
            ? await _serials.Find(session, filter).ToListAsync(ct)
            : await _serials.Find(filter).ToListAsync(ct);
        return rows.ToDictionary(s => s.Number, StringComparer.Ordinal);
    }

    private SerialNumber New(SerialMovement movement, string number, string transactionId, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        LegalEntityId = _legalEntityId,
        CreatedAt = at,
        IsDeleted = false,
        ItemId = movement.ItemId,
        SkuId = movement.SkuId,
        SkuLevel = movement.SkuLevel,
        LotId = movement.LotId,
        Number = number,
        IdentityKey = LotIdentityPolicy.SerialKey(_tenantId, movement.SkuId, movement.SkuLevel, number),
        Status = SerialStatus.Active,
        WarehouseId = movement.ToWarehouseId,
        LocationId = movement.ToLocationId,
        ReceivedTransactionId = transactionId,
        LastTransactionId = transactionId,
        LastMovementAt = at
    };

    private IClientSessionHandle RequireSession()
        => _binding.Session ?? throw new InvalidOperationException("Serials are written only inside the ledger posting transaction.");

    private static string TransactionIdOf(SerialMovement movement)
        => (movement.TransactionId ?? throw new ArgumentException("A serial write needs the transaction id.", nameof(movement))).ToString("D");

    private static SerialCheck Refuse(string code, string message, IReadOnlyList<string> serials) => new(code, message, serials);

    private static LotPostingRefusedException Refused(string code, string message, IReadOnlyList<string> serials)
        => new(code, message, LotPostingErrors.SerialDetails(serials));

    /// <summary>Tenant + IsDeleted = false — lot and serial identity is tenant-wide (CT-D3 / CT-D7).</summary>
    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<T>.Filter.Eq(x => x.IsDeleted, false);
}
