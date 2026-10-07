using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/*
 * WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the stock-behaviour writes (see IProductStockBehaviourRepository).
 *
 * Why the write cannot slip past an identity or lifecycle operation (CT decision A):
 *  - The record is read FIRST, then its identity operations. Every write is conditioned on the version read, on the
 *    status read (Draft or IdentityApproved only), on no lifecycle operation holding the record (ActiveLifecycleOperation
 *    and, for a GSKU, RetirementOperationId null — Eq(null) also matches a document without the field).
 *  - GSKU: a submit reserves in ONE transaction with a touch of the GSKU (IdentitySubmitReservations + 1) conditioned on
 *    the GSKU version. A reservation committed before the operation read is seen there (unfinished → refused); one
 *    committed after it changes the count, and this write is conditioned on the count read → it matches nothing. A
 *    reservation after this write finds the version moved and is refused by its own filter (DRAFT_CHANGED).
 *  - LSKU: a submit fences the LSKU (ActiveLifecycleOperation, conditioned on the version) before it starts; this write
 *    requires no fence, the fence requires this write's version — exactly one of the two lands.
 *  - Finished Good: its identity operation does not write the Finished Good. An unfinished one is refused here; one
 *    reserved after the operation read keeps the version it read, and its own version check later finds the record
 *    moved — the existing "no state without a way out" answer, never an approval of facts the record no longer has.
 *    (Same window as the Finished Good draft cancel today; named in the S3 report.)
 *  - A first fill on a non-Draft record is also conditioned on the field still being empty: Exists(false) | Eq(null)
 *    (R2 — never Eq(false) / Size(0)).
 */
public sealed class ProductStockBehaviourRepository : IProductStockBehaviourRepository
{
    public const string GtinIndexName = "ux_finished_goods_tenant_gtin_live_v1";

    private static readonly int[] LiveFinishedGoodStatuses =
    [
        (int)ProductIdentityLifecycleStatus.Draft,
        (int)ProductIdentityLifecycleStatus.PendingIdentityApproval,
        (int)ProductIdentityLifecycleStatus.IdentityApproved
    ];

    // The identity operations whose version pin is spent. AwaitingMakerReplay and ManualReconciliationRequired are NOT
    // here: the same submit may still be repeated or reconciled at the version it pinned.
    private static readonly LskuIdentityWorkflowCheckpoint[] LskuFinished =
    [
        LskuIdentityWorkflowCheckpoint.Completed,
        LskuIdentityWorkflowCheckpoint.DecisionNotApplicable,
        LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
        LskuIdentityWorkflowCheckpoint.Superseded
    ];

    private static readonly FinishedGoodIdentityWorkflowCheckpoint[] FinishedGoodFinished =
    [
        FinishedGoodIdentityWorkflowCheckpoint.Completed,
        FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
        FinishedGoodIdentityWorkflowCheckpoint.Superseded
    ];

    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<FirstGskuIdentityWorkflowOperation> _gskuIdentity;
    private readonly IMongoCollection<LskuIdentityWorkflowOperation> _lskuIdentity;
    private readonly IMongoCollection<FinishedGoodIdentityWorkflowOperation> _finishedGoodIdentity;
    private readonly Guid _tenantId;

    public ProductStockBehaviourRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _lskus = database.GetCollection<Lsku>("mdm_lskus");
        _finishedGoods = database.GetCollection<FinishedGood>("mdm_finished_goods");
        _gskuIdentity = database.GetCollection<FirstGskuIdentityWorkflowOperation>(
            FirstGskuIdentityWorkflowOperationRepository.CollectionName);
        _lskuIdentity = database.GetCollection<LskuIdentityWorkflowOperation>(
            LskuIdentityWorkflowOperationRepository.CollectionName);
        _finishedGoodIdentity = database.GetCollection<FinishedGoodIdentityWorkflowOperation>(
            FinishedGoodIdentityWorkflowOperationRepository.CollectionName);
        _tenantId = tenantContext.TenantId;
    }

    /// <summary>
    /// R4: the sales-box GTIN is unique among the tenant's non-retired Finished Goods. Partial filter with
    /// <c>$exists</c> + <c>$type</c> + <c>$in</c> only (no <c>$ne</c>): a document without a GTIN, or with a null one,
    /// is outside the index and never collides. Built at start-up by <c>ProductStockBehaviourIndexInitializer</c>.
    /// </summary>
    public static Task EnsureGtinIndexAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var finishedGoods = database.GetCollection<BsonDocument>("mdm_finished_goods");
        var partial = new BsonDocument
        {
            { nameof(FinishedGood.Gtin), new BsonDocument { { "$exists", true }, { "$type", "string" } } },
            { nameof(FinishedGood.LifecycleStatus), new BsonDocument("$in", new BsonArray(LiveFinishedGoodStatuses)) }
        };
        return finishedGoods.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending(nameof(FinishedGood.TenantId)).Ascending(nameof(FinishedGood.Gtin)),
            new CreateIndexOptions<BsonDocument>
            {
                Name = GtinIndexName,
                Unique = true,
                PartialFilterExpression = partial
            }), cancellationToken: cancellationToken);
    }

    public async Task<StockBehaviourWriteResult> WriteGskuAsync(
        StockBehaviourWrite write, CancellationToken cancellationToken = default)
    {
        if (!Valid(write) || write.MarksGtin) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.ContractInvalid);
        var target = Target<Gsku>(write.RecordId);
        var current = await _gskus.Find(target).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.NotFound);
        if (Replay(current.StockBehaviourCommand, write) is { } replay) return replay;
        if (current.Version == write.ExpectedVersion)
        {
            var latest = await FirstGskuIdentityWorkflowOperationRepository.LatestForGsku(
                    _gskuIdentity, OperationTenant<FirstGskuIdentityWorkflowOperation>(), current.Id)
                .FirstOrDefaultAsync(cancellationToken);
            // The draft edit's own rule (FirstGskuIdentitySubmitState): only no submit, or a finished / ended one, frees
            // the record.
            if (FirstGskuIdentitySubmitState.Of(latest, current.Version).Kind is not
                (FirstGskuIdentitySubmitStateKind.None or FirstGskuIdentitySubmitStateKind.Ended))
                return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.BlockedByOperation);
        }

        var reservations = current.IdentitySubmitReservations == 0
            ? Builders<Gsku>.Filter.Exists(x => x.IdentitySubmitReservations, false)
              | Builders<Gsku>.Filter.Eq(x => x.IdentitySubmitReservations, 0)
            : Builders<Gsku>.Filter.Eq(x => x.IdentitySubmitReservations, current.IdentitySubmitReservations);
        var filter = Conditions(target, write)
            & Builders<Gsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & reservations;
        var written = await _gskus.FindOneAndUpdateAsync(filter,
            Update<Gsku>(write, nameof(Gsku.StockBehaviourCommand)),
            new FindOneAndUpdateOptions<Gsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (written is not null) return new(true, written.Version, null);

        var now = await _gskus.Find(target).FirstOrDefaultAsync(cancellationToken);
        return Classify(now, now?.StockBehaviourCommand, now?.Version, now?.LifecycleStatus, write,
            now is not null && (now.ActiveLifecycleOperation is not null || now.RetirementOperationId is not null
                                || now.IdentitySubmitReservations != current.IdentitySubmitReservations));
    }

    public async Task<StockBehaviourWriteResult> WriteLskuAsync(
        StockBehaviourWrite write, CancellationToken cancellationToken = default)
    {
        if (!Valid(write) || write.MarksGtin) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.ContractInvalid);
        var target = Target<Lsku>(write.RecordId);
        var current = await _lskus.Find(target).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.NotFound);
        if (Replay(current.StockBehaviourCommand, write) is { } replay) return replay;
        if (current.Version == write.ExpectedVersion
            && await _lskuIdentity.Find(OperationTenant<LskuIdentityWorkflowOperation>()
                    & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(x => x.LskuId, current.Id)
                    & Builders<LskuIdentityWorkflowOperation>.Filter.Nin(x => x.Checkpoint, LskuFinished))
                .AnyAsync(cancellationToken))
            return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.BlockedByOperation);

        var filter = Conditions(target, write) & Builders<Lsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null);
        var written = await _lskus.FindOneAndUpdateAsync(filter,
            Update<Lsku>(write, nameof(Lsku.StockBehaviourCommand)),
            new FindOneAndUpdateOptions<Lsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (written is not null) return new(true, written.Version, null);

        var now = await _lskus.Find(target).FirstOrDefaultAsync(cancellationToken);
        return Classify(now, now?.StockBehaviourCommand, now?.Version, now?.LifecycleStatus, write,
            now?.ActiveLifecycleOperation is not null);
    }

    public async Task<StockBehaviourWriteResult> WriteFinishedGoodAsync(
        StockBehaviourWrite write, CancellationToken cancellationToken = default)
    {
        if (!Valid(write)) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.ContractInvalid);
        var target = Target<FinishedGood>(write.RecordId);
        var current = await _finishedGoods.Find(target).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.NotFound);
        if (Replay(write.MarksGtin ? current.GtinCommand : current.StockBehaviourCommand, write) is { } replay)
            return replay;
        if (current.Version == write.ExpectedVersion
            && (current.DraftCancellation is not null
                || await _finishedGoodIdentity.Find(OperationTenant<FinishedGoodIdentityWorkflowOperation>()
                        & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(x => x.FinishedGoodId, current.Id)
                        & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Nin(x => x.Checkpoint, FinishedGoodFinished))
                    .AnyAsync(cancellationToken)))
            return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.BlockedByOperation);

        var filter = Conditions(target, write) & Builders<FinishedGood>.Filter.Eq(x => x.DraftCancellation, null);
        var update = Update<FinishedGood>(write,
            write.MarksGtin ? nameof(FinishedGood.GtinCommand) : nameof(FinishedGood.StockBehaviourCommand));
        try
        {
            var written = await _finishedGoods.FindOneAndUpdateAsync(filter, update,
                new FindOneAndUpdateOptions<FinishedGood> { ReturnDocument = ReturnDocument.After }, cancellationToken);
            if (written is not null) return new(true, written.Version, null);
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.GtinConflict);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.GtinConflict);
        }

        var now = await _finishedGoods.Find(target).FirstOrDefaultAsync(cancellationToken);
        return Classify(now, write.MarksGtin ? now?.GtinCommand : now?.StockBehaviourCommand, now?.Version,
            now?.LifecycleStatus, write, now?.DraftCancellation is not null);
    }

    public Task<bool> GtinHeldByAnotherAsync(string gtin, Guid finishedGoodId, CancellationToken cancellationToken = default) =>
        _finishedGoods.Find(Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<FinishedGood>.Filter.Eq(x => x.Gtin, gtin)
                & Builders<FinishedGood>.Filter.In(x => x.LifecycleStatus, LiveFinishedGoodStatuses
                    .Select(status => (ProductIdentityLifecycleStatus)status))
                & Builders<FinishedGood>.Filter.Ne(x => x.Id, finishedGoodId))
            .AnyAsync(cancellationToken);

    private static bool Valid(StockBehaviourWrite write) =>
        write.RecordId != Guid.Empty && write.ExpectedVersion >= 0 && write.ExpectedVersion < int.MaxValue
        && write.ExpectedStatus is ProductIdentityLifecycleStatus.Draft or ProductIdentityLifecycleStatus.IdentityApproved
        && write.Sets.Count > 0 && write.FirstFills.All(write.Sets.ContainsKey)
        && write.Mark.OperationId != Guid.Empty && write.Mark.ResultVersion == write.ExpectedVersion + 1
        && write.MarksGtin == (write.Sets.Count == 1 && write.Sets.ContainsKey(StockBehaviourFields.Gtin))
        && (write.MarksGtin || !write.Sets.ContainsKey(StockBehaviourFields.Gtin));

    // Ne(true), not Eq(false): a document written without IsDeleted is live (the FG FIX1 legacy rule).
    private FilterDefinition<T> Target<T>(Guid id) where T : EntityBase =>
        Builders<T>.Filter.Eq(x => x.TenantId, _tenantId)
        & Builders<T>.Filter.Ne(x => x.IsDeleted, true)
        & Builders<T>.Filter.Eq(x => x.Id, id);

    private FilterDefinition<T> OperationTenant<T>() where T : EntityBase =>
        Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Ne(x => x.IsDeleted, true);

    private static FilterDefinition<T> Conditions<T>(FilterDefinition<T> target, StockBehaviourWrite write)
        where T : EntityBase
    {
        var filter = target
            & Builders<T>.Filter.Eq(x => x.Version, write.ExpectedVersion)
            & new BsonDocument("LifecycleStatus", ToBson<T>("LifecycleStatus", write.ExpectedStatus));
        // R2 / R3: a first fill lands only while the field is still empty — missing (a document written before S3) or
        // null. Two first fills at once: the second finds the field filled and matches nothing.
        foreach (var field in write.FirstFills)
            filter &= new BsonDocument("$or", new BsonArray
            {
                new BsonDocument(field, new BsonDocument("$exists", false)),
                new BsonDocument(field, BsonNull.Value)
            });
        return filter;
    }

    // One $set / $inc, every value serialized by the entity's own member serializer (enum as stored, decimal text as
    // text, nullable as null).
    private static UpdateDefinition<T> Update<T>(StockBehaviourWrite write, string markField) where T : EntityBase
    {
        var set = new BsonDocument
        {
            { nameof(EntityBase.UpdatedAt), ToBson<T>(nameof(EntityBase.UpdatedAt), (DateTimeOffset?)write.AtUtc) },
            { markField, ToBson<T>(markField, write.Mark) }
        };
        foreach (var (field, value) in write.Sets)
            set[field] = ToBson<T>(field, value);
        return new BsonDocumentUpdateDefinition<T>(new BsonDocument
        {
            { "$set", set },
            { "$inc", new BsonDocument(nameof(EntityBase.Version), 1) }
        });
    }

    private static BsonValue ToBson<T>(string field, object? value)
    {
        if (value is null) return BsonNull.Value;
        // AllMemberMaps: Version / UpdatedAt / LifecycleStatus may be declared on a base class.
        var member = BsonClassMap.LookupClassMap(typeof(T)).AllMemberMaps.FirstOrDefault(map => map.MemberName == field)
                     ?? throw new InvalidOperationException($"{typeof(T).Name} has no stored field {field}.");
        var holder = new BsonDocument();
        using (var writer = new BsonDocumentWriter(holder))
        {
            writer.WriteStartDocument();
            writer.WriteName("v");
            member.GetSerializer().Serialize(BsonSerializationContext.CreateRoot(writer), value);
            writer.WriteEndDocument();
        }
        return holder["v"];
    }

    private static StockBehaviourWriteResult? Replay(StockBehaviourCommandMark? mark, StockBehaviourWrite write) =>
        mark is null || mark.OperationId != write.Mark.OperationId
            ? null
            : string.Equals(mark.Fingerprint, write.Mark.Fingerprint, StringComparison.Ordinal)
                ? new(true, mark.ResultVersion, null, IsReplay: true)
                : StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.IdempotencyConflict);

    // Says why the conditional write matched nothing, from the record as it is now.
    private static StockBehaviourWriteResult Classify<T>(
        T? now, StockBehaviourCommandMark? mark, int? version, ProductIdentityLifecycleStatus? status,
        StockBehaviourWrite write, bool heldByOperation) where T : EntityBase
    {
        if (now is null) return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.NotFound);
        if (Replay(mark, write) is { } replay) return replay;
        if (status != write.ExpectedStatus || heldByOperation)
            return StockBehaviourWriteResult.Fail(version == write.ExpectedVersion
                ? StockBehaviourStoreCodes.BlockedByOperation
                : StockBehaviourStoreCodes.ConcurrencyConflict);
        // The race of two first fills (R3): the loser finds the field filled by the winner.
        if (write.FirstFills.Any(field => typeof(T).GetProperty(field)?.GetValue(now) is not null))
            return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.FieldLocked);
        return StockBehaviourWriteResult.Fail(StockBehaviourStoreCodes.ConcurrencyConflict);
    }
}
