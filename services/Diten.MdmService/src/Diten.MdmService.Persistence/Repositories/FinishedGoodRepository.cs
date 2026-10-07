using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FinishedGoodRepository : IFinishedGoodRepository
{
    private const string CollectionName = "mdm_finished_goods";
    private const string CreationAttemptCollectionName = "mdm_finished_good_creation_attempts";
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<FinishedGoodCreationAttempt> _creationAttempts;
    private readonly IMongoCollection<BsonDocument> _documents;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public FinishedGoodRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _finishedGoods = database.GetCollection<FinishedGood>(CollectionName);
        _creationAttempts = database.GetCollection<FinishedGoodCreationAttempt>(CreationAttemptCollectionName);
        _documents = database.GetCollection<BsonDocument>(CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _revisions = database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FinishedGoodCreationAttemptResult> BindCreationAttemptAsync(
        Guid gskuId,
        string normalizedCreationCommandId,
        string requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (gskuId == Guid.Empty
            || !Exact(normalizedCreationCommandId, 128)
            || !string.Equals(
                normalizedCreationCommandId,
                normalizedCreationCommandId.Trim().ToUpperInvariant(),
                StringComparison.Ordinal)
            || !Fingerprint(requestFingerprint)
            || !string.Equals(
                requestFingerprint,
                GskuChildCreationAdmission.ComputeRequestFingerprint(
                    gskuId,
                    GskuChildIdentityKind.FinishedGood,
                    normalizedCreationCommandId),
                StringComparison.Ordinal))
        {
            return FinishedGoodCreationAttemptResult.Invalid();
        }

        try
        {
            var existing = await FindCreationAttemptAsync(normalizedCreationCommandId, cancellationToken);
            if (existing is not null)
            {
                return MatchCreationAttempt(existing, gskuId, requestFingerprint);
            }

            if (await HasLegacyPartialAsync(normalizedCreationCommandId, cancellationToken))
            {
                return FinishedGoodCreationAttemptResult.ReconciliationRequired();
            }

            var timestamp = DateTimeOffset.UtcNow;
            var attempt = new FinishedGoodCreationAttempt
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                GskuId = gskuId,
                CreationCommandId = normalizedCreationCommandId,
                RequestFingerprint = requestFingerprint,
                IsDeleted = false,
                DeletedAt = null,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 0
            };

            await _creationAttempts.InsertOneAsync(attempt, cancellationToken: cancellationToken);
            return FinishedGoodCreationAttemptResult.Bound(attempt);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await ReadBackCreationAttemptAsync(normalizedCreationCommandId, gskuId, requestFingerprint, cancellationToken);
        }
        catch (MongoConnectionException)
        {
            return await ReadBackCreationAttemptAsync(normalizedCreationCommandId, gskuId, requestFingerprint, cancellationToken);
        }
        catch (MongoExecutionTimeoutException)
        {
            return await ReadBackCreationAttemptAsync(normalizedCreationCommandId, gskuId, requestFingerprint, cancellationToken);
        }
        catch (MongoWriteConcernException)
        {
            return await ReadBackCreationAttemptAsync(normalizedCreationCommandId, gskuId, requestFingerprint, cancellationToken);
        }
        catch (MongoException)
        {
            return FinishedGoodCreationAttemptResult.Unavailable();
        }
    }

    public async Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(ActiveTenantFilter & Builders<FinishedGood>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGood?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(
                TenantIncludingDeletedFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGood?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(
                TenantIncludingDeletedFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGoodPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveTenantFilter;
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            var codeFilter = Builders<FinishedGood>.Filter.Regex(
                x => x.CanonicalCode,
                new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
            filter &= matchingGskuIds is { Count: > 0 }
                ? Builders<FinishedGood>.Filter.Or(
                    codeFilter,
                    Builders<FinishedGood>.Filter.In(x => x.GskuId, matchingGskuIds))
                : codeFilter;
        }

        var totalCount = await _finishedGoods.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _finishedGoods.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<FinishedGoodPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
    {
        var match = new BsonDocument
        {
            { nameof(FinishedGood.TenantId), ProductLegalEntityScopeAggregation.GuidBson(_tenantId) },
            // FG FIX1 part B item 8: $ne true, not false — a document written before the field existed is live.
            { nameof(FinishedGood.IsDeleted), new BsonDocument("$ne", true) }
        };
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            var code = new BsonDocument(
                nameof(FinishedGood.CanonicalCode),
                new BsonRegularExpression(
                    "^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
            match["$or"] = matchingGskuIds is { Count: > 0 }
                ? new BsonArray
                {
                    code,
                    new BsonDocument(nameof(FinishedGood.GskuId), new BsonDocument("$in",
                        new BsonArray(matchingGskuIds.Select(ProductLegalEntityScopeAggregation.GuidBson))))
                }
                : new BsonArray { code };
        }

        var pipeline = new List<BsonDocument> { new("$match", match) };
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateGskuRevisionResolutionStages(
            _tenantId,
            "$" + nameof(FinishedGood.GskuId)));
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateAccessStages(
            _tenantId,
            effectiveCandidateLegalEntityIds,
            serverNowUtc));
        pipeline.Add(ProductLegalEntityScopeAggregation.CleanupStage("ScopeGskus", "ScopeRevisions"));
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { nameof(FinishedGood.CanonicalCode), 1 },
            { "_id", 1 }
        }));
        pipeline.Add(new BsonDocument("$facet", new BsonDocument
        {
            { "items", new BsonArray
                {
                    new BsonDocument("$skip", (pageNumber - 1) * pageSize),
                    new BsonDocument("$limit", pageSize)
                }
            },
            { "summary", new BsonArray { new BsonDocument("$count", "total") } }
        }));

        var result = await _documents.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<FinishedGood>(item.AsBsonDocument))
            .ToArray() ?? [];
        var total = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument["total"].ToInt64() ?? 0;
        return new(items, total);
    }

    public async Task<FinishedGoodCreateResult> CreateDraftAsync(
        FinishedGood finishedGood,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(finishedGood.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                return new(false, existing, "CREATION_COMMAND_TOMBSTONED");
            }

            // FG FIX1 review m7: the create key of a cancelled Draft is a terminal conflict here too, never a replay.
            if (existing.DraftCancellation is not null)
            {
                return new(false, existing, "FINISHED_GOOD_DRAFT_CANCELLED");
            }

            return SameFacts(existing, finishedGood)
                ? new(true, existing)
                : new(false, existing, "IDEMPOTENCY_KEY_CONFLICT");
        }

        if (finishedGood.Id == Guid.Empty
            || finishedGood.GskuId == Guid.Empty
            || finishedGood.CodeReservationId == Guid.Empty
            || string.IsNullOrWhiteSpace(finishedGood.CanonicalCode)
            || string.IsNullOrWhiteSpace(finishedGood.CreationCommandId)
            || finishedGood.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
        {
            return new(false, null, "FINISHED_GOOD_CONTRACT_INVALID");
        }

        var requiredAdmissionFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            finishedGood.GskuId, GskuChildIdentityKind.FinishedGood, finishedGood.CreationCommandId);
        var referenceableGskuFilter = Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Gsku>.Filter.Eq(x => x.Id, finishedGood.GskuId)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions,
                x => x.ChildKind == GskuChildIdentityKind.FinishedGood
                     && x.CreationCommandId == finishedGood.CreationCommandId
                     && x.RequestFingerprint == requiredAdmissionFingerprint);
        var parent = await _gskus.Find(referenceableGskuFilter).FirstOrDefaultAsync(cancellationToken);
        if (parent is null || !await _revisions.Find(
                Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, parent.ProductDefinitionRevisionId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(
                    x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved))
            .AnyAsync(cancellationToken))
        {
            return new(false, null, "GSKU_NOT_REFERENCEABLE");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, finishedGood.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, finishedGood.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, finishedGood.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(
                x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (finishedGood.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || finishedGood.AuditIntents.Any(intent =>
                intent.TenantId != _tenantId
                || intent.AggregateType != AuditAggregateType.FinishedGood
                || intent.AggregateId != finishedGood.Id))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        finishedGood.TenantId = _tenantId;
        finishedGood.CreatedAt = DateTimeOffset.UtcNow;
        finishedGood.UpdatedAt = finishedGood.CreatedAt;
        finishedGood.IsDeleted = false;
        finishedGood.DeletedAt = null;
        finishedGood.Version = 0;

        try
        {
            // FG FIX1 part B item 1 (the LSKU InsertBoundToLiveCodeAsync pattern): the row goes in only with its code
            // still live — the reservation write and the insert are ONE transaction, so a burn (which reads "no live row
            // bound" in its own transaction) and this insert fail each other: never a live row with a burned code. A
            // transient transaction error is retried with backoff; running out is ambiguous (202), never a refusal.
            for (var attempt = 1; ; attempt++)
            {
                var inserted = await InsertBoundToLiveCodeAsync(finishedGood, reservationFilter, cancellationToken);
                if (inserted.ErrorCode != InsertConflictRetry) return inserted;
                if (attempt >= InsertAttempts) return AmbiguousWrite();
                await Task.Delay(TimeSpan.FromMilliseconds(50 << attempt), cancellationToken);
            }
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(finishedGood.CreationCommandId, cancellationToken)
                ?? await GetByReservationIdAsync(finishedGood.CodeReservationId, cancellationToken);
            return existing?.DraftCancellation is not null
                ? new(false, existing, "FINISHED_GOOD_DRAFT_CANCELLED")
                : existing is not null && !existing.IsDeleted && SameFacts(existing, finishedGood)
                    ? new(true, existing)
                    : new(false, existing, "FINISHED_GOOD_DUPLICATE_CONFLICT");
        }
        catch (MongoConnectionException)
        {
            return AmbiguousWrite();
        }
        catch (MongoExecutionTimeoutException)
        {
            return AmbiguousWrite();
        }
        catch (MongoWriteConcernException)
        {
            return AmbiguousWrite();
        }
        // The LSKU FIX5 K6 rule: a commit whose result is unknown and a server-selection timeout are not refusals.
        catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            return AmbiguousWrite();
        }
        catch (TimeoutException)
        {
            return AmbiguousWrite();
        }
    }

    internal const int InsertAttempts = 4;

    // FG FIX2 item 2 (CT-approved, the LSKU BeforeReservationWriteForTests twin): tests only — a barrier inside the
    // insert's transaction, before its reservation write. Internal, null by default, no configuration path; only
    // Diten.MdmService.Api.Tests sees it (InternalsVisibleTo).
    internal Func<Task>? BeforeReservationWriteForTests { get; set; }
    private const string InsertConflictRetry = "FINISHED_GOOD_INSERT_CONFLICT_RETRY";

    private async Task<FinishedGoodCreateResult> InsertBoundToLiveCodeAsync(FinishedGood finishedGood,
        FilterDefinition<CodeReservation> reservationFilter, CancellationToken cancellationToken)
    {
        using var session = await _finishedGoods.Database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
            if (BeforeReservationWriteForTests is { } barrier) await barrier();
            // UpdatedAt only: metadata outside the reservation's Version and its audit chain; it exists to make an
            // overlapping burn and this insert conflict.
            var touched = await _reservations.UpdateOneAsync(session,
                reservationFilter & Builders<CodeReservation>.Filter.Ne(x => x.BindingState, CodeReservationBindingState.Burned),
                Builders<CodeReservation>.Update.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken: cancellationToken);
            if (touched.MatchedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, null, "CODE_RESERVATION_BURNED");
            }
            await _finishedGoods.InsertOneAsync(session, finishedGood, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return new(true, finishedGood);
        }
        catch (Exception exception) when (exception is NotSupportedException || exception is MongoCommandException { Code: 20 })
        {
            // Without transactions (a standalone mongod) no burn can happen at all (it is refused): the plain insert.
            await _finishedGoods.InsertOneAsync(finishedGood, cancellationToken: cancellationToken);
            return new(true, finishedGood);
        }
        catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError")
                                               && exception is not MongoWriteException)
        {
            // Nothing of ours committed. A burn that won leaves the code burned; anything else is tried again.
            var code = await _reservations.Find(Builders<CodeReservation>.Filter.Eq(x => x.Id, finishedGood.CodeReservationId))
                .FirstOrDefaultAsync(cancellationToken);
            return code?.BindingState == CodeReservationBindingState.Burned
                ? new(false, null, "CODE_RESERVATION_BURNED")
                : new(false, null, InsertConflictRetry);
        }
    }

    private static FinishedGoodCreateResult AmbiguousWrite()
        => new(false, null, "FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);

    private static bool SameFacts(FinishedGood left, FinishedGood right)
        => left.Id == right.Id
           && left.GskuId == right.GskuId
           && left.CodeReservationId == right.CodeReservationId
           && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
           && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal);

    public async Task<FinishedGoodCreateResult> CreateDraftWithAdmissionAsync(
        FinishedGood finishedGood, string admissionFingerprint, CancellationToken cancellationToken = default)
    {
        var expected = GskuChildCreationAdmission.ComputeRequestFingerprint(
            finishedGood.GskuId, GskuChildIdentityKind.FinishedGood, finishedGood.CreationCommandId);
        if (!string.Equals(admissionFingerprint, expected, StringComparison.Ordinal))
            return new(false, null, "GSKU_CHILD_ADMISSION_REQUIRED");
        return await CreateDraftAsync(finishedGood, cancellationToken);
    }

    private void EnsureIndexes()
    {
        _finishedGoods.Indexes.CreateMany([
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_code" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_reservation" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_command" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.GskuId),
                new CreateIndexOptions { Name = "ix_mdm_finished_goods_tenant_gsku" }),
            // FG FIX1 review m3: the tenant-free pending-cancellation census reads only cancelled FGs.
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending("DraftCancellation.RequestedAtUtc"),
                new CreateIndexOptions<FinishedGood>
                {
                    Name = "ix_mdm_finished_goods_draft_cancellation_requested",
                    PartialFilterExpression = Builders<FinishedGood>.Filter.Exists("DraftCancellation.RequestedAtUtc")
                })
        ]);

        _creationAttempts.Indexes.CreateOne(new CreateIndexModel<FinishedGoodCreationAttempt>(
            Builders<FinishedGoodCreationAttempt>.IndexKeys
                .Ascending(x => x.TenantId)
                .Ascending(x => x.CreationCommandId),
            new CreateIndexOptions
            {
                Unique = true,
                Name = "ux_mdm_finished_good_creation_attempts_tenant_command"
            }));
    }

    private async Task<FinishedGoodCreationAttemptResult> ReadBackCreationAttemptAsync(
        string creationCommandId,
        Guid gskuId,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await FindCreationAttemptAsync(creationCommandId, cancellationToken);
            return existing is null
                ? FinishedGoodCreationAttemptResult.Unavailable()
                : MatchCreationAttempt(existing, gskuId, requestFingerprint);
        }
        catch (MongoException)
        {
            return FinishedGoodCreationAttemptResult.Unavailable();
        }
    }

    private async Task<FinishedGoodCreationAttempt?> FindCreationAttemptAsync(
        string creationCommandId,
        CancellationToken cancellationToken)
        => await _creationAttempts.Find(
                Builders<FinishedGoodCreationAttempt>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<FinishedGoodCreationAttempt>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<bool> HasLegacyPartialAsync(string creationCommandId, CancellationToken cancellationToken)
    {
        var admission = await _gskus.Find(
                Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions,
                    x => x.ChildKind == GskuChildIdentityKind.FinishedGood
                         && x.CreationCommandId == creationCommandId))
            .Limit(1)
            .AnyAsync(cancellationToken);
        if (admission)
        {
            return true;
        }

        return await _reservations.Find(
                Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)
                & Builders<CodeReservation>.Filter.Or(
                    Builders<CodeReservation>.Filter.Eq(x => x.ReservationCommandId, creationCommandId),
                    Builders<CodeReservation>.Filter.Eq(x => x.ConsumeCommandId, creationCommandId)))
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    private static FinishedGoodCreationAttemptResult MatchCreationAttempt(
        FinishedGoodCreationAttempt attempt,
        Guid gskuId,
        string requestFingerprint)
        => !attempt.IsDeleted
           && attempt.GskuId == gskuId
           && string.Equals(attempt.RequestFingerprint, requestFingerprint, StringComparison.Ordinal)
            ? FinishedGoodCreationAttemptResult.Bound(attempt)
            : FinishedGoodCreationAttemptResult.Conflict(attempt);

    // FG FIX1 item 1: the audited Draft cancellation. One CAS: Draft, the expected version, no cancellation yet, room
    // for one more intent — then the decision and its intent go in together and the version moves; the FG is retained.
    // The same operation again is a replay (the same decision) or a conflict (another one); nothing is ever deleted.
    public async Task<FinishedGoodDraftCancelResult> CancelDraftAsync(
        Guid id, int expectedVersion, FinishedGoodDraftCancellation cancellation, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentNullException.ThrowIfNull(auditIntent);
        if (id == Guid.Empty || expectedVersion < 0 || cancellation.OperationId == Guid.Empty
            || !Fingerprint(cancellation.Fingerprint) || cancellation.RequesterSubjectId == Guid.Empty
            || !Exact(cancellation.ReasonCode, 128) || cancellation.BaseVersion != expectedVersion
            || cancellation.IntentId != auditIntent.IntentId || cancellation.IdempotencyKey != auditIntent.IdempotencyKey
            || cancellation.EvidenceHash != auditIntent.EvidenceHash
            || auditIntent.Operation != ProductAuditOperation.FinishedGoodDraftCancelled
            || auditIntent.AggregateType != AuditAggregateType.FinishedGood || auditIntent.AggregateId != id
            || auditIntent.TenantId != _tenantId || auditIntent.PreVersion != expectedVersion
            || auditIntent.PostVersion != expectedVersion + 1)
        {
            return new(false, null, "FINISHED_GOOD_DRAFT_CANCEL_CONTRACT_INVALID");
        }

        // Not Eq(IsDeleted, false) / Eq(DraftCancellation, null) on purpose: a document written before a field existed
        // has no such field — Ne(true) and Eq(null) both match a missing field (FG FIX1 legacy rule).
        var target = TenantIncludingDeletedFilter
            & Builders<FinishedGood>.Filter.Ne(x => x.IsDeleted, true)
            & Builders<FinishedGood>.Filter.Eq(x => x.Id, id);
        var filter = target
            & Builders<FinishedGood>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<FinishedGood>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
            & Builders<FinishedGood>.Filter.Eq(x => x.DraftCancellation, null)
            & Builders<FinishedGood>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var update = Builders<FinishedGood>.Update
            .Set(x => x.DraftCancellation, cancellation)
            .Push(x => x.AuditIntents, auditIntent)
            .Set(x => x.UpdatedAt, auditIntent.TimestampUtc)
            .Inc(x => x.Version, 1)
            // WP-MDM-ITEM-MASTER S3 FIX1 R2 (CT decision — the ONE narrow exception to "identity writes unchanged"): a
            // cancelled draft gives its sales-box GTIN back in the same write. It stays Draft (retained), so it stayed
            // inside the live GTIN index and held the GTIN for good — a new FG for the same box could never take it.
            // Filter, version and the cancel's audit intent are unchanged; a cancelled FG takes no new GTIN (S3 refuses).
            .Unset(x => x.Gtin);
        // S3 FIX2 (CT decision, same narrow exception): the GTIN is a regulated identifier — the cancelled draft keeps the
        // value it gave back as ReleasedGtin, so it stays traceable to the record that held it. Read AT the expected
        // version: the version check pins it (a GTIN change moves the version), so this is exactly the value the write
        // below unsets; the cancel's filter is not touched.
        var atExpectedVersion = await _finishedGoods.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (atExpectedVersion?.Gtin is { } releasedGtin)
            update = update.Set(x => x.ReleasedGtin, releasedGtin);
        var written = await _finishedGoods.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<FinishedGood> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        if (written is not null) return new(true, written);

        var current = await _finishedGoods.Find(target).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return new(false, null, "FINISHED_GOOD_NOT_FOUND");
        if (current.DraftCancellation is { } existing)
            return existing.OperationId == cancellation.OperationId
                ? existing.Fingerprint == cancellation.Fingerprint
                    ? new(true, current, IsReplay: true)
                    : new(false, current, "IDEMPOTENCY_KEY_CONFLICT")
                : new(false, current, "FINISHED_GOOD_DRAFT_ALREADY_CANCELLED");
        return new(false, current,
            current.LifecycleStatus != ProductIdentityLifecycleStatus.Draft ? "FINISHED_GOOD_NOT_A_DRAFT"
            : current.Version != expectedVersion ? "CONCURRENCY_CONFLICT"
            : current.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate ? "AUDIT_INTENT_CAPACITY_REACHED"
            : "FINISHED_GOOD_DRAFT_CANCEL_CONFLICT");
    }

    private static bool Exact(string? value, int maximum) =>
        value is { Length: > 0 } && value.Length <= maximum
        && value == value.Trim() && !value.Any(char.IsControl);

    private static bool Fingerprint(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private FilterDefinition<FinishedGood> TenantIncludingDeletedFilter =>
        Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId);

    // FG FIX1 review m5: Ne(true), not Eq(false) — a document written without the field is live, as the GSKU blocker
    // already reads it; otherwise it blocks the GSKU yet can never be read or cancelled.
    private FilterDefinition<FinishedGood> ActiveTenantFilter =>
        TenantIncludingDeletedFilter & Builders<FinishedGood>.Filter.Ne(x => x.IsDeleted, true);
}
