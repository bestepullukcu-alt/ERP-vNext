using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// Owns the only atomic persistence path for pre-start product-identity workflow recovery.
/// It deliberately uses the existing collections and indexes; recovery is a state transition, not a new schema.
/// </summary>
public sealed class ProductIdentityWorkflowOperationRecoveryRepository
    : IProductIdentityWorkflowOperationRecoveryRepository
{
    private const string GlobalProducts = "mdm_global_products";
    private const string Revisions = "mdm_product_definition_revisions";
    private const string Gskus = "mdm_gskus";
    private const string Lskus = "mdm_lskus";
    private const string FinishedGoods = "mdm_finished_goods";

    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly Guid _tenantId;

    public ProductIdentityWorkflowOperationRecoveryRepository(
        IMongoClient client,
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _client = client;
        _database = database;
        _tenantId = tenantContext.TenantId;
    }

    public async Task<ProductIdentityWorkflowOperationRecoveryCandidate?> GetCandidateAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (_tenantId == Guid.Empty || operationId == Guid.Empty) return null;

        var candidates = new List<ProductIdentityWorkflowOperationRecoveryCandidate>(4);
        await AddCandidateAsync<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName, operationId,
            operation => new GlobalProductWorkflowRecoveryScope(operation.GlobalProductId, operation.ExpectedProductVersion),
            operation => operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Prepared,
            candidates, cancellationToken);
        await AddCandidateAsync<FirstGskuIdentityWorkflowOperation>(
            FirstGskuIdentityWorkflowOperationRepository.CollectionName, operationId,
            operation => new FirstGskuWorkflowRecoveryScope(operation.GlobalProductId,
                operation.ProductDefinitionRevisionId, operation.GskuId, operation.ExpectedGskuVersion,
                operation.ExpectedRevisionVersion),
            operation => operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Prepared,
            candidates, cancellationToken);
        await AddCandidateAsync<LskuIdentityWorkflowOperation>(
            LskuIdentityWorkflowOperationRepository.CollectionName, operationId,
            operation => new LskuWorkflowRecoveryScope(operation.LskuId, operation.GskuId,
                operation.ProductDefinitionRevisionId, operation.MarketCode, operation.ExpectedLskuVersion),
            operation => operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Prepared,
            candidates, cancellationToken);
        await AddCandidateAsync<FinishedGoodIdentityWorkflowOperation>(
            FinishedGoodIdentityWorkflowOperationRepository.CollectionName, operationId,
            operation => new FinishedGoodWorkflowRecoveryScope(operation.FinishedGoodId, operation.GskuId,
                operation.ProductDefinitionRevisionId, operation.ExpectedFinishedGoodVersion),
            operation => operation.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.Prepared,
            candidates, cancellationToken);

        // A shared operation identity across families is ambiguous and therefore never recoverable.
        return candidates.Count == 1 ? candidates[0] : null;
    }

    public async Task<ProductIdentityWorkflowOperationRecoveryWriteResult> RecoverAsync(
        ProductIdentityWorkflowOperationRecoveryMutation mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (_tenantId == Guid.Empty || !ValidMutation(mutation))
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible, mutation, "RECOVERY_MUTATION_INVALID");

        var existing = await FindRawOperationAsync(mutation.OperationId, cancellationToken);
        if (existing is null)
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.NotFound, mutation, "OPERATION_NOT_FOUND");
        if (existing.Value.Ambiguous)
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation, "OPERATION_ID_AMBIGUOUS");
        if (IsExactReplayHeader(existing.Value.Document, mutation))
            return await ReplayOrConflictAsync(existing.Value.Family, existing.Value.CollectionName,
                existing.Value.Document, mutation, cancellationToken);

        var preflight = ValidatePreflight(existing.Value.Family, existing.Value.Document, mutation);
        if (preflight is not null) return preflight;

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            session.StartTransaction();
            var operationCollection = _database.GetCollection<BsonDocument>(existing.Value.CollectionName);
            var operationFilter = ExactOperationCas(existing.Value.Family, mutation);
            var operationUpdate = RecoveryOperationUpdate(existing.Value.Family, mutation);
            var operationWrite = await operationCollection.UpdateOneAsync(
                session, operationFilter, operationUpdate, cancellationToken: cancellationToken);
            if (operationWrite.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return await ClassifyAfterLostCasAsync(mutation, cancellationToken);
            }

            if (!await UpdateTargetsAsync(session, mutation, cancellationToken))
            {
                await session.AbortTransactionAsync(cancellationToken);
                return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation,
                    "TARGET_VERSION_OR_STATE_CONFLICT");
            }

            if (mutation.Successor is not null)
            {
                var successor = BuildSuccessor(existing.Value.Family, existing.Value.Document, mutation);
                await operationCollection.InsertOneAsync(session, successor, cancellationToken: cancellationToken);
            }

            await session.CommitTransactionAsync(cancellationToken);
            return new(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied, mutation.OperationId,
                UpdatedScope(mutation.ExpectedScope), mutation.Evidence.Disposition,
                mutation.ExpectedOperationVersion + 1, mutation.Successor, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            return await ClassifyAfterLostCasAsync(mutation, cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 112
            || exception.HasErrorLabel("TransientTransactionError"))
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            return await ClassifyAfterLostCasAsync(mutation, cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task AddCandidateAsync<TOperation>(
        string collectionName,
        Guid operationId,
        Func<TOperation, ProductIdentityWorkflowOperationRecoveryScope> scope,
        Func<TOperation, bool> prepared,
        ICollection<ProductIdentityWorkflowOperationRecoveryCandidate> candidates,
        CancellationToken cancellationToken)
        where TOperation : EntityBase
    {
        var operation = await _database.GetCollection<TOperation>(collectionName)
            .Find(ActiveOperationFilter<TOperation>(operationId)).FirstOrDefaultAsync(cancellationToken);
        if (operation is null) return;

        dynamic item = operation;
        candidates.Add(new(operationId, operation.Version, scope(operation), item.MakerSubjectId,
            item.StartIdempotencyKey, item.OperationFingerprint, prepared(operation), HasWorkflowEvidence(item),
            item.RecoveryDisposition, item.LeaseOwner, item.LeaseUntilUtcTicksV1, item.LeaseGeneration));
    }

    private static bool HasWorkflowEvidence(dynamic operation) =>
        operation.WorkflowInstanceId is not null || operation.WorkflowTemplateVersionId is not null
        || operation.ApprovalTaskId is not null || operation.AssignmentSnapshotId is not null
        || operation.StartTransitionLogId is not null || operation.DecisionTransitionLogId is not null;

    private async Task<(ProductIdentityWorkflowOperationFamily Family, string CollectionName,
        BsonDocument Document, bool Ambiguous)?> FindRawOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var hits = new List<(ProductIdentityWorkflowOperationFamily, string, BsonDocument)>();
        foreach (var descriptor in Families)
        {
            var document = await _database.GetCollection<BsonDocument>(descriptor.Collection)
                .Find(ActiveRawOperationFilter(operationId)).FirstOrDefaultAsync(cancellationToken);
            if (document is not null) hits.Add((descriptor.Family, descriptor.Collection, document));
        }
        return hits.Count == 0 ? null : (hits[0].Item1, hits[0].Item2, hits[0].Item3, hits.Count != 1);
    }

    private ProductIdentityWorkflowOperationRecoveryWriteResult? ValidatePreflight(
        ProductIdentityWorkflowOperationFamily family,
        BsonDocument operation,
        ProductIdentityWorkflowOperationRecoveryMutation mutation)
    {
        if (family != mutation.ExpectedScope.Family)
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation, "OPERATION_FAMILY_CONFLICT");
        if (operation.GetValue("RecoveryDisposition", 0).ToInt32() != 0)
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation, "RECOVERY_ALREADY_TERMINAL");
        if (operation.GetValue("Checkpoint", 0).ToInt32() != 1 || HasAnyWorkflowEvidence(operation))
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible, mutation, "WORKFLOW_ALREADY_OBSERVED");
        var leaseOwner = operation.GetValue("LeaseOwner", BsonNull.Value);
        var leaseUntil = operation.GetValue("LeaseUntilUtcTicksV1", BsonNull.Value);
        if (leaseOwner.IsBsonNull != leaseUntil.IsBsonNull
            || !leaseOwner.IsBsonNull && (!leaseOwner.IsString || string.IsNullOrWhiteSpace(leaseOwner.AsString)
                || !leaseUntil.IsInt64 || leaseUntil.AsInt64 <= 0))
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible, mutation,
                "OPERATION_LEASE_MALFORMED");
        if (leaseOwner.IsString && leaseUntil.AsInt64 > mutation.Evidence.RecoveredAtUtcTicksV1)
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Ineligible, mutation, "OPERATION_LEASE_ACTIVE");
        return null;
    }

    private FilterDefinition<BsonDocument> ExactOperationCas(
        ProductIdentityWorkflowOperationFamily family,
        ProductIdentityWorkflowOperationRecoveryMutation mutation)
    {
        var f = Builders<BsonDocument>.Filter;
        var filter = ActiveRawOperationFilter(mutation.OperationId)
            & f.Eq("Version", mutation.ExpectedOperationVersion)
            & f.Eq("Checkpoint", 1)
            & f.Eq("RecoveryDisposition", 0)
            & f.Eq("MakerSubjectId", mutation.ExpectedOriginalMakerSubjectId)
            & f.Eq("StartIdempotencyKey", mutation.ExpectedStartIdempotencyKey)
            & f.Eq("OperationFingerprint", mutation.ExpectedOperationFingerprint)
            & f.Eq("LeaseGeneration", mutation.ExpectedLeaseGeneration)
            & NullableEquals("LeaseOwner", mutation.ExpectedLeaseOwner)
            & NullableEquals("LeaseUntilUtcTicksV1", mutation.ExpectedLeaseUntilUtcTicksV1)
            & NoWorkflowEvidenceFilter();
        return filter & ScopeFilter(family, mutation.ExpectedScope);
    }

    private static UpdateDefinition<BsonDocument> RecoveryOperationUpdate(
        ProductIdentityWorkflowOperationFamily family,
        ProductIdentityWorkflowOperationRecoveryMutation mutation)
    {
        var checkpoint = (family, mutation.Evidence.Disposition) switch
        {
            (ProductIdentityWorkflowOperationFamily.GlobalProduct,
                ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart) => 11,
            (ProductIdentityWorkflowOperationFamily.GlobalProduct,
                ProductIdentityWorkflowRecoveryDisposition.Superseded) => 12,
            (ProductIdentityWorkflowOperationFamily.FirstGsku,
                ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart) => 16,
            (ProductIdentityWorkflowOperationFamily.FirstGsku,
                ProductIdentityWorkflowRecoveryDisposition.Superseded) => 17,
            (ProductIdentityWorkflowOperationFamily.Lsku or ProductIdentityWorkflowOperationFamily.FinishedGood,
                ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart) => 12,
            (ProductIdentityWorkflowOperationFamily.Lsku or ProductIdentityWorkflowOperationFamily.FinishedGood,
                ProductIdentityWorkflowRecoveryDisposition.Superseded) => 13,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var u = Builders<BsonDocument>.Update
            .Set("Checkpoint", checkpoint)
            .Set("RecoveryDisposition", (int)mutation.Evidence.Disposition)
            .Set("RecoveryCommandId", mutation.Evidence.CommandId)
            .Set("RecoveryOperatorSubjectId", mutation.Evidence.OperatorSubjectId)
            .Set("RecoveryReasonCode", mutation.Evidence.ReasonCode)
            .Set("RecoveryComment", (BsonValue)(mutation.Evidence.Comment is null
                ? BsonNull.Value
                : new BsonString(mutation.Evidence.Comment)))
            .Set("RecoveryWorkflowNotFoundEvidenceId", mutation.Evidence.WorkflowNotFoundEvidenceId)
            .Set("RecoveryWorkflowNotFoundEvidenceFingerprint", mutation.Evidence.WorkflowNotFoundEvidenceFingerprint)
            .Set("RecoveryWorkflowNotFoundObservedAtUtcTicksV1", mutation.Evidence.WorkflowNotFoundObservedAtUtcTicksV1)
            .Set("RecoveredAtUtcTicksV1", mutation.Evidence.RecoveredAtUtcTicksV1)
            .Set("SuccessorOperationId", mutation.Successor is null
                ? (BsonValue)BsonNull.Value
                : new BsonBinaryData(mutation.Successor.OperationId, GuidRepresentation.Standard))
            .Set("SuccessorStartIdempotencyKey", mutation.Successor is null
                ? (BsonValue)BsonNull.Value
                : new BsonString(mutation.Successor.StartIdempotencyKey))
            .Set("SuccessorOperationFingerprint", mutation.Successor is null
                ? (BsonValue)BsonNull.Value
                : new BsonString(mutation.Successor.OperationFingerprint))
            .Set("NextAttemptAtUtcTicksV1", BsonNull.Value)
            .Set("LastFailureCode", BsonNull.Value)
            .Set("LeaseOwner", BsonNull.Value)
            .Set("LeaseUntilUtcTicksV1", BsonNull.Value)
            .Set("UpdatedAtUtcTicksV1", mutation.Evidence.RecoveredAtUtcTicksV1)
            .Set("UpdatedAt", DateTimeOffsetValue(mutation.Evidence.RecoveredAtUtcTicksV1))
            .Inc("Version", 1);
        return u;
    }

    private async Task<bool> UpdateTargetsAsync(
        IClientSessionHandle session,
        ProductIdentityWorkflowOperationRecoveryMutation mutation,
        CancellationToken cancellationToken)
    {
        var intents = mutation.AuditIntents;
        var auditOperation = mutation.Evidence.Disposition == ProductIdentityWorkflowRecoveryDisposition.Superseded
            ? ProductAuditOperation.ProductIdentityWorkflowOperationSuperseded
            : ProductAuditOperation.ProductIdentityWorkflowOperationAbandonedBeforeWorkflowStart;
        return mutation.ExpectedScope switch
        {
            GlobalProductWorkflowRecoveryScope scope when intents.Count == 1 =>
                await UpdateTargetAsync<GlobalProduct>(session, GlobalProducts, scope.GlobalProductId,
                    scope.GlobalProductVersion, intents[0], AuditAggregateType.GlobalProduct, auditOperation, mutation.Evidence,
                    cancellationToken),
            FirstGskuWorkflowRecoveryScope scope when intents.Count == 2 =>
                await UpdateTargetAsync<ProductDefinitionRevision>(session, Revisions,
                    scope.ProductDefinitionRevisionId, scope.ProductDefinitionRevisionVersion,
                    intents.Single(item => item.AggregateId == scope.ProductDefinitionRevisionId),
                    AuditAggregateType.ProductDefinitionRevision, auditOperation, mutation.Evidence,
                    cancellationToken)
                && await UpdateTargetAsync<Gsku>(session, Gskus, scope.GskuId, scope.GskuVersion,
                    intents.Single(item => item.AggregateId == scope.GskuId), AuditAggregateType.Gsku,
                    auditOperation, mutation.Evidence, cancellationToken),
            LskuWorkflowRecoveryScope scope when intents.Count == 1 =>
                await UpdateTargetAsync<Lsku>(session, Lskus, scope.LskuId, scope.LskuVersion,
                    intents[0], AuditAggregateType.Lsku, auditOperation, mutation.Evidence, cancellationToken),
            FinishedGoodWorkflowRecoveryScope scope when intents.Count == 1 =>
                await UpdateTargetAsync<FinishedGood>(session, FinishedGoods, scope.FinishedGoodId,
                    scope.FinishedGoodVersion, intents[0], AuditAggregateType.FinishedGood, auditOperation, mutation.Evidence,
                    cancellationToken),
            _ => false
        };
    }

    private async Task<bool> UpdateTargetAsync<T>(IClientSessionHandle session, string collectionName,
        Guid id, int expectedVersion, LocalAuditIntent intent, AuditAggregateType aggregateType,
        ProductAuditOperation auditOperation, ProductIdentityWorkflowOperationRecoveryEvidence evidence,
        CancellationToken cancellationToken)
        where T : EntityBase
    {
        if (!ValidIntent(intent, id, expectedVersion, aggregateType, auditOperation, evidence)) return false;
        var f = Builders<T>.Filter;
        var filter = f.Eq(item => item.TenantId, _tenantId) & f.Eq(item => item.Id, id)
            & f.Eq(item => item.IsDeleted, false) & f.Eq(item => item.Version, expectedVersion)
            & f.Eq("LifecycleStatus", (int)ProductIdentityLifecycleStatus.Draft)
            & f.Or(f.Eq("WorkflowBinding", BsonNull.Value), f.Exists("WorkflowBinding", false))
            & f.Or(f.Eq("IdentityWorkflowBinding", BsonNull.Value), f.Exists("IdentityWorkflowBinding", false))
            & new BsonDocument("AuditIntents", new BsonDocument("$not",
                new BsonDocument("$elemMatch", new BsonDocument("IntentId",
                    new BsonBinaryData(intent.IntentId, GuidRepresentation.Standard)))))
            & new BsonDocument("$expr", new BsonDocument("$lt", new BsonArray
            {
                new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray
                    { "$AuditIntents", new BsonArray() })),
                AuditIntentLimits.MaxPerAggregate
            }));
        var update = Builders<T>.Update.Push("AuditIntents", intent).Inc(item => item.Version, 1)
            .Set(item => item.UpdatedAt, intent.TimestampUtc);
        var result = await _database.GetCollection<T>(collectionName)
            .UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private bool ValidIntent(LocalAuditIntent intent, Guid aggregateId, int expectedVersion,
        AuditAggregateType aggregateType, ProductAuditOperation auditOperation,
        ProductIdentityWorkflowOperationRecoveryEvidence evidence) =>
        intent.TenantId == _tenantId && intent.AggregateId == aggregateId && intent.IntentId != Guid.Empty
        && intent.AggregateType == aggregateType
        && intent.PreVersion == expectedVersion && intent.PostVersion == expectedVersion + 1
        && intent.Sequence == expectedVersion + 1
        && intent.Operation == auditOperation
        && intent.SourceService == AuditIntentContract.SourceService
        && intent.SchemaVersion == 1 && intent.ContractVersion is null
        && intent.TimestampUtc != default && intent.TimestampUtc.Offset == TimeSpan.Zero
        && intent.TimestampUtcTicksV1 is > 0 && intent.TimestampUtc.UtcTicks == intent.TimestampUtcTicksV1
        && intent.TemporalStorageVersion == AuditIntentTemporalStorage.CurrentVersion
        && IsExactBounded(intent.ActorId, 128) && IsExactBounded(intent.CorrelationId, 256)
        && IsExactBounded(intent.CausationId, 256) && IsExactBounded(intent.CommandId, 256)
        && intent.ActorId == evidence.OperatorSubjectId.ToString("D")
        && intent.CommandId == evidence.CommandId.ToString("D")
        && intent.CausationId == evidence.CommandId.ToString("D")
        && IsUpperHex(intent.EvidenceHash, 64) && IsExactBounded(intent.SnapshotReference, 256)
        && IsExactBounded(intent.IdempotencyKey, 256)
        && intent.DeliveryState == AuditIntentDeliveryState.Pending && intent.AttemptCount == 0
        && intent.LastAttemptAt is null && intent.NextRetryAt is null && intent.NextRetryAtUtcTicksV1 is null
        && intent.CentralAcknowledgement is null && intent.CentralIdempotencyKey is null
        && intent.AcknowledgedContractVersion is null && intent.AcknowledgedAt is null && intent.LastError is null
        && intent.LeaseOwner is null && intent.ClaimToken is null && intent.ClaimGeneration == 0
        && intent.ClaimedAt is null && intent.LeaseUntil is null && intent.LeaseUntilUtcTicksV1 is null
        && intent.DeliveredAt is null && intent.DeadLetteredAt is null && intent.CompactedAt is null
        && intent.CompactReceiptReference is null && intent.FailureClass == AuditIntentFailureClass.None
        && intent.FailureReason is null;

    private BsonDocument BuildSuccessor(ProductIdentityWorkflowOperationFamily family, BsonDocument original,
        ProductIdentityWorkflowOperationRecoveryMutation mutation)
    {
        var successor = original.DeepClone().AsBsonDocument;
        successor["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        successor["OperationId"] = GuidValue(mutation.Successor!.OperationId);
        successor["MakerSubjectId"] = GuidValue(mutation.Successor.MakerSubjectId);
        successor["StartIdempotencyKey"] = mutation.Successor.StartIdempotencyKey;
        successor["OperationFingerprint"] = mutation.Successor.OperationFingerprint;
        successor["Checkpoint"] = 1;
        successor["RecoveryDisposition"] = 0;
        successor["Version"] = 0;
        var primaryVersionField = family switch
        {
            ProductIdentityWorkflowOperationFamily.GlobalProduct => "ExpectedProductVersion",
            ProductIdentityWorkflowOperationFamily.FirstGsku => "ExpectedGskuVersion",
            ProductIdentityWorkflowOperationFamily.Lsku => "ExpectedLskuVersion",
            ProductIdentityWorkflowOperationFamily.FinishedGood => "ExpectedFinishedGoodVersion",
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        successor[primaryVersionField] = GetPrimaryVersion(mutation.Successor.Scope);
        if (mutation.Successor.Scope is FirstGskuWorkflowRecoveryScope first)
            successor["ExpectedRevisionVersion"] = first.ProductDefinitionRevisionVersion;
        foreach (var field in MutableRecoveryFields) successor[field] = BsonNull.Value;
        successor["LeaseGeneration"] = 0;
        successor["TemporalStorageVersion"] = 1;
        successor["CreatedAtUtcTicksV1"] = mutation.Evidence.RecoveredAtUtcTicksV1;
        successor["UpdatedAtUtcTicksV1"] = mutation.Evidence.RecoveredAtUtcTicksV1;
        successor["CreatedAt"] = DateTimeOffsetValue(mutation.Evidence.RecoveredAtUtcTicksV1);
        successor["UpdatedAt"] = DateTimeOffsetValue(mutation.Evidence.RecoveredAtUtcTicksV1);
        successor["IsDeleted"] = false;
        successor["DeletedAt"] = BsonNull.Value;
        return successor;
    }

    private async Task<ProductIdentityWorkflowOperationRecoveryWriteResult> ClassifyAfterLostCasAsync(
        ProductIdentityWorkflowOperationRecoveryMutation mutation, CancellationToken cancellationToken)
    {
        var current = await FindRawOperationAsync(mutation.OperationId, cancellationToken);
        if (current is null) return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.NotFound, mutation, "OPERATION_NOT_FOUND");
        if (!current.Value.Ambiguous && IsExactReplayHeader(current.Value.Document, mutation))
            return await ReplayOrConflictAsync(current.Value.Family, current.Value.CollectionName,
                current.Value.Document, mutation, cancellationToken);
        return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation, "RECOVERY_CONCURRENCY_CONFLICT");
    }

    private async Task<ProductIdentityWorkflowOperationRecoveryWriteResult> ReplayOrConflictAsync(
        ProductIdentityWorkflowOperationFamily family,
        string operationCollection,
        BsonDocument oldOperation,
        ProductIdentityWorkflowOperationRecoveryMutation mutation,
        CancellationToken cancellationToken)
    {
        if (!await PersistedAuditsMatchAsync(mutation, cancellationToken))
            return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation,
                "RECOVERY_AUDIT_REPLAY_CONFLICT");

        ProductIdentityWorkflowOperationRecoverySuccessor? persistedSuccessor = null;
        if (mutation.Successor is not null)
        {
            var document = await _database.GetCollection<BsonDocument>(operationCollection)
                .Find(ActiveRawOperationFilter(mutation.Successor.OperationId))
                .FirstOrDefaultAsync(cancellationToken);
            if (document is null || !SuccessorMatches(family, document, mutation.Successor))
                return Result(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation,
                    "RECOVERY_SUCCESSOR_REPLAY_CONFLICT");
            persistedSuccessor = new(
                document["OperationId"].AsGuid,
                document["StartIdempotencyKey"].AsString,
                document["OperationFingerprint"].AsString,
                document["MakerSubjectId"].AsGuid,
                ScopeFromDocument(family, document));
        }

        return new(ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay, mutation.OperationId,
            UpdatedScope(mutation.ExpectedScope), mutation.Evidence.Disposition,
            oldOperation.GetValue("Version", mutation.ExpectedOperationVersion + 1).ToInt32(),
            persistedSuccessor, null);
    }

    private async Task<bool> PersistedAuditsMatchAsync(
        ProductIdentityWorkflowOperationRecoveryMutation mutation,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var descriptor in TargetDescriptors(mutation.ExpectedScope))
            {
                var target = await _database.GetCollection<BsonDocument>(descriptor.Collection)
                    .Find(ActiveTargetFilter(descriptor.Id)).FirstOrDefaultAsync(cancellationToken);
                if (target is null || !target.TryGetValue("AuditIntents", out var arrayValue)
                    || !arrayValue.IsBsonArray)
                    return false;
                var expected = mutation.AuditIntents.SingleOrDefault(item => item.AggregateId == descriptor.Id);
                if (expected is null) return false;
                var matches = arrayValue.AsBsonArray
                    .Where(value => value.IsBsonDocument
                        && value.AsBsonDocument.GetValue("IntentId", BsonNull.Value) == GuidValue(expected.IntentId))
                    .Select(value => BsonSerializer.Deserialize<LocalAuditIntent>(value.AsBsonDocument))
                    .ToArray();
                if (matches.Length != 1 || !SameImmutableAudit(matches[0], expected)) return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is FormatException or BsonSerializationException
            or InvalidOperationException)
        {
            return false;
        }
    }

    private FilterDefinition<BsonDocument> ActiveTargetFilter(Guid id)
    {
        var f = Builders<BsonDocument>.Filter;
        return f.Eq("TenantId", _tenantId) & f.Eq("_id", id) & f.Eq("IsDeleted", false);
    }

    private static IReadOnlyList<(string Collection, Guid Id, int ExpectedVersion)> TargetDescriptors(
        ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => [(GlobalProducts, item.GlobalProductId, item.GlobalProductVersion)],
        FirstGskuWorkflowRecoveryScope item =>
        [
            (Revisions, item.ProductDefinitionRevisionId, item.ProductDefinitionRevisionVersion),
            (Gskus, item.GskuId, item.GskuVersion)
        ],
        LskuWorkflowRecoveryScope item => [(Lskus, item.LskuId, item.LskuVersion)],
        FinishedGoodWorkflowRecoveryScope item => [(FinishedGoods, item.FinishedGoodId, item.FinishedGoodVersion)],
        _ => []
    };

    private static bool SameImmutableAudit(LocalAuditIntent left, LocalAuditIntent right) =>
        left.SourceService == right.SourceService && left.SchemaVersion == right.SchemaVersion
        && left.ContractVersion == right.ContractVersion && left.IntentId == right.IntentId
        && left.TenantId == right.TenantId && left.AggregateType == right.AggregateType
        && left.AggregateId == right.AggregateId && left.PreVersion == right.PreVersion
        && left.PostVersion == right.PostVersion && left.Operation == right.Operation
        && left.ActorId == right.ActorId && left.CorrelationId == right.CorrelationId
        && left.CausationId == right.CausationId && left.CommandId == right.CommandId
        && left.Sequence == right.Sequence && left.TimestampUtc == right.TimestampUtc
        && left.TimestampUtcTicksV1 == right.TimestampUtcTicksV1
        && left.TemporalStorageVersion == right.TemporalStorageVersion
        && left.EvidenceHash == right.EvidenceHash && left.SnapshotReference == right.SnapshotReference
        && left.IdempotencyKey == right.IdempotencyKey;

    private bool SuccessorMatches(ProductIdentityWorkflowOperationFamily family, BsonDocument document,
        ProductIdentityWorkflowOperationRecoverySuccessor expected) =>
        document.GetValue("TenantId", BsonNull.Value) == GuidValue(_tenantId)
        && document.GetValue("IsDeleted", true) == false
        && document.GetValue("OperationId", BsonNull.Value) == GuidValue(expected.OperationId)
        && document.GetValue("MakerSubjectId", BsonNull.Value) == GuidValue(expected.MakerSubjectId)
        && document.GetValue("StartIdempotencyKey", BsonNull.Value) == expected.StartIdempotencyKey
        && document.GetValue("OperationFingerprint", BsonNull.Value) == expected.OperationFingerprint
        && ScopeFromDocument(family, document) == expected.Scope;

    private static ProductIdentityWorkflowOperationRecoveryScope ScopeFromDocument(
        ProductIdentityWorkflowOperationFamily family, BsonDocument document) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => new GlobalProductWorkflowRecoveryScope(
            document["GlobalProductId"].AsGuid, document["ExpectedProductVersion"].ToInt32()),
        ProductIdentityWorkflowOperationFamily.FirstGsku => new FirstGskuWorkflowRecoveryScope(
            document["GlobalProductId"].AsGuid, document["ProductDefinitionRevisionId"].AsGuid,
            document["GskuId"].AsGuid, document["ExpectedGskuVersion"].ToInt32(),
            document["ExpectedRevisionVersion"].ToInt32()),
        ProductIdentityWorkflowOperationFamily.Lsku => new LskuWorkflowRecoveryScope(
            document["LskuId"].AsGuid, document["GskuId"].AsGuid,
            document["ProductDefinitionRevisionId"].AsGuid, document["MarketCode"].AsString,
            document["ExpectedLskuVersion"].ToInt32()),
        ProductIdentityWorkflowOperationFamily.FinishedGood => new FinishedGoodWorkflowRecoveryScope(
            document["FinishedGoodId"].AsGuid, document["GskuId"].AsGuid,
            document["ProductDefinitionRevisionId"].AsGuid,
            document["ExpectedFinishedGoodVersion"].ToInt32()),
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static bool IsExactReplayHeader(BsonDocument operation, ProductIdentityWorkflowOperationRecoveryMutation mutation) =>
        operation.GetValue("Version", -1).ToInt32() == mutation.ExpectedOperationVersion + 1
        && operation.GetValue("MakerSubjectId", BsonNull.Value) == GuidValue(mutation.ExpectedOriginalMakerSubjectId)
        && operation.GetValue("StartIdempotencyKey", BsonNull.Value) == mutation.ExpectedStartIdempotencyKey
        && operation.GetValue("OperationFingerprint", BsonNull.Value) == mutation.ExpectedOperationFingerprint
        && operation.GetValue("RecoveryCommandId", BsonNull.Value) == GuidValue(mutation.Evidence.CommandId)
        && operation.GetValue("RecoveryDisposition", 0).ToInt32() == (int)mutation.Evidence.Disposition
        && operation.GetValue("RecoveryOperatorSubjectId", BsonNull.Value) == GuidValue(mutation.Evidence.OperatorSubjectId)
        && operation.GetValue("RecoveryReasonCode", BsonNull.Value) == BsonValue.Create(mutation.Evidence.ReasonCode)
        && operation.GetValue("RecoveryComment", BsonNull.Value) == (mutation.Evidence.Comment is null ? BsonNull.Value : BsonValue.Create(mutation.Evidence.Comment))
        && operation.GetValue("RecoveryWorkflowNotFoundEvidenceId", BsonNull.Value) == GuidValue(mutation.Evidence.WorkflowNotFoundEvidenceId)
        && operation.GetValue("RecoveryWorkflowNotFoundEvidenceFingerprint", BsonNull.Value) == BsonValue.Create(mutation.Evidence.WorkflowNotFoundEvidenceFingerprint)
        && operation.GetValue("RecoveryWorkflowNotFoundObservedAtUtcTicksV1", BsonNull.Value) == BsonValue.Create(mutation.Evidence.WorkflowNotFoundObservedAtUtcTicksV1)
        && operation.GetValue("RecoveredAtUtcTicksV1", BsonNull.Value) == BsonValue.Create(mutation.Evidence.RecoveredAtUtcTicksV1)
        && ScopeMatches(operation, mutation.ExpectedScope)
        && operation.GetValue("SuccessorOperationId", BsonNull.Value) == (mutation.Successor is null ? BsonNull.Value : GuidValue(mutation.Successor.OperationId))
        && operation.GetValue("SuccessorStartIdempotencyKey", BsonNull.Value) == (mutation.Successor is null ? BsonNull.Value : BsonValue.Create(mutation.Successor.StartIdempotencyKey))
        && operation.GetValue("SuccessorOperationFingerprint", BsonNull.Value) == (mutation.Successor is null ? BsonNull.Value : BsonValue.Create(mutation.Successor.OperationFingerprint));

    private bool ValidMutation(ProductIdentityWorkflowOperationRecoveryMutation mutation) =>
        mutation.OperationId != Guid.Empty && mutation.ExpectedOperationVersion >= 0
        && mutation.ExpectedOriginalMakerSubjectId != Guid.Empty
        && IsExactBounded(mutation.ExpectedStartIdempotencyKey, 256)
        && IsLowerHex(mutation.ExpectedOperationFingerprint, 64)
        && mutation.Evidence.CommandId != Guid.Empty && mutation.Evidence.OperatorSubjectId != Guid.Empty
        && mutation.Evidence.OperatorSubjectId != mutation.ExpectedOriginalMakerSubjectId
        && IsExactBounded(mutation.Evidence.ReasonCode, 128)
        && IsOptionalExactBounded(mutation.Evidence.Comment, 512)
        && mutation.Evidence.WorkflowNotFoundEvidenceId != Guid.Empty
        && IsLowerHex(mutation.Evidence.WorkflowNotFoundEvidenceFingerprint, 64)
        && mutation.Evidence.Disposition is ProductIdentityWorkflowRecoveryDisposition.AbandonedBeforeWorkflowStart
            or ProductIdentityWorkflowRecoveryDisposition.Superseded
        && mutation.Evidence.RecoveredAtUtcTicksV1 > 0
        && mutation.Evidence.WorkflowNotFoundObservedAtUtcTicksV1 > 0
        && mutation.Evidence.WorkflowNotFoundObservedAtUtcTicksV1 <= mutation.Evidence.RecoveredAtUtcTicksV1
        && (mutation.Evidence.Disposition == ProductIdentityWorkflowRecoveryDisposition.Superseded) == (mutation.Successor is not null)
        && (mutation.Successor is null || mutation.Successor.MakerSubjectId == mutation.Evidence.OperatorSubjectId
            && mutation.Successor.OperationId != Guid.Empty && mutation.Successor.OperationId != mutation.OperationId
            && IsExactBounded(mutation.Successor.StartIdempotencyKey, 256)
            && IsLowerHex(mutation.Successor.OperationFingerprint, 64)
            && mutation.Successor.Scope == UpdatedScope(mutation.ExpectedScope))
        && ValidAuditShape(mutation);

    private bool ValidAuditShape(ProductIdentityWorkflowOperationRecoveryMutation mutation)
    {
        var expectedIds = mutation.ExpectedScope switch
        {
            GlobalProductWorkflowRecoveryScope item => new[] { item.GlobalProductId },
            FirstGskuWorkflowRecoveryScope item => new[] { item.ProductDefinitionRevisionId, item.GskuId },
            LskuWorkflowRecoveryScope item => new[] { item.LskuId },
            FinishedGoodWorkflowRecoveryScope item => new[] { item.FinishedGoodId },
            _ => []
        };
        return mutation.AuditIntents.Count == expectedIds.Length
            && mutation.AuditIntents.Select(item => item.AggregateId).Order().SequenceEqual(expectedIds.Order())
            && mutation.AuditIntents.All(item => item.TenantId == _tenantId);
    }

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 } && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);

    private static bool IsOptionalExactBounded(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);

    private static bool IsLowerHex(string? value, int exactLength) =>
        value is not null && value.Length == exactLength
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsUpperHex(string? value, int exactLength) =>
        value is not null && value.Length == exactLength
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static ProductIdentityWorkflowOperationRecoveryScope UpdatedScope(ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => new GlobalProductWorkflowRecoveryScope(item.GlobalProductId, item.GlobalProductVersion + 1),
        FirstGskuWorkflowRecoveryScope item => new FirstGskuWorkflowRecoveryScope(item.GlobalProductId,
            item.ProductDefinitionRevisionId, item.GskuId, item.GskuVersion + 1,
            item.ProductDefinitionRevisionVersion + 1),
        LskuWorkflowRecoveryScope item => new LskuWorkflowRecoveryScope(item.LskuId, item.GskuId,
            item.ProductDefinitionRevisionId, item.MarketCode, item.LskuVersion + 1),
        FinishedGoodWorkflowRecoveryScope item => new FinishedGoodWorkflowRecoveryScope(item.FinishedGoodId,
            item.GskuId, item.ProductDefinitionRevisionId, item.FinishedGoodVersion + 1),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static int GetPrimaryVersion(ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => item.GlobalProductVersion,
        FirstGskuWorkflowRecoveryScope item => item.GskuVersion,
        LskuWorkflowRecoveryScope item => item.LskuVersion,
        FinishedGoodWorkflowRecoveryScope item => item.FinishedGoodVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private FilterDefinition<T> ActiveOperationFilter<T>(Guid operationId) where T : EntityBase
    {
        var f = Builders<T>.Filter;
        return f.Eq(item => item.TenantId, _tenantId) & f.Eq(item => item.IsDeleted, false)
            & f.Eq("OperationId", operationId);
    }

    private FilterDefinition<BsonDocument> ActiveRawOperationFilter(Guid operationId)
    {
        var f = Builders<BsonDocument>.Filter;
        return f.Eq("TenantId", _tenantId) & f.Eq("IsDeleted", false) & f.Eq("OperationId", operationId);
    }

    private FilterDefinition<BsonDocument> NoWorkflowEvidenceFilter()
    {
        var f = Builders<BsonDocument>.Filter;
        return f.Eq("WorkflowInstanceId", BsonNull.Value) & f.Eq("WorkflowTemplateVersionId", BsonNull.Value)
            & f.Eq("ApprovalTaskId", BsonNull.Value) & f.Eq("AssignmentSnapshotId", BsonNull.Value)
            & f.Eq("StartTransitionLogId", BsonNull.Value) & f.Eq("DecisionTransitionLogId", BsonNull.Value);
    }

    private static bool HasAnyWorkflowEvidence(BsonDocument operation) =>
        new[] { "WorkflowInstanceId", "WorkflowTemplateVersionId", "ApprovalTaskId", "AssignmentSnapshotId",
            "StartTransitionLogId", "DecisionTransitionLogId" }
        .Any(field => operation.TryGetValue(field, out var value) && !value.IsBsonNull);

    private static FilterDefinition<BsonDocument> ScopeFilter(ProductIdentityWorkflowOperationFamily family,
        ProductIdentityWorkflowOperationRecoveryScope scope)
    {
        var f = Builders<BsonDocument>.Filter;
        return scope switch
        {
            GlobalProductWorkflowRecoveryScope item => f.Eq("GlobalProductId", item.GlobalProductId)
                & f.Eq("ExpectedProductVersion", item.GlobalProductVersion),
            FirstGskuWorkflowRecoveryScope item => f.Eq("GlobalProductId", item.GlobalProductId)
                & f.Eq("ProductDefinitionRevisionId", item.ProductDefinitionRevisionId) & f.Eq("GskuId", item.GskuId)
                & f.Eq("ExpectedGskuVersion", item.GskuVersion)
                & f.Eq("ExpectedRevisionVersion", item.ProductDefinitionRevisionVersion),
            LskuWorkflowRecoveryScope item => f.Eq("LskuId", item.LskuId) & f.Eq("GskuId", item.GskuId)
                & f.Eq("ProductDefinitionRevisionId", item.ProductDefinitionRevisionId)
                & f.Eq("MarketCode", item.MarketCode) & f.Eq("ExpectedLskuVersion", item.LskuVersion),
            FinishedGoodWorkflowRecoveryScope item => f.Eq("FinishedGoodId", item.FinishedGoodId)
                & f.Eq("GskuId", item.GskuId) & f.Eq("ProductDefinitionRevisionId", item.ProductDefinitionRevisionId)
                & f.Eq("ExpectedFinishedGoodVersion", item.FinishedGoodVersion),
            _ => f.Empty
        };
    }

    private static bool ScopeMatches(BsonDocument operation, ProductIdentityWorkflowOperationRecoveryScope scope)
        => scope switch
        {
            GlobalProductWorkflowRecoveryScope item => operation["GlobalProductId"] == GuidValue(item.GlobalProductId)
                && operation["ExpectedProductVersion"].ToInt32() == item.GlobalProductVersion,
            FirstGskuWorkflowRecoveryScope item => operation["GlobalProductId"] == GuidValue(item.GlobalProductId)
                && operation["ProductDefinitionRevisionId"] == GuidValue(item.ProductDefinitionRevisionId)
                && operation["GskuId"] == GuidValue(item.GskuId)
                && operation["ExpectedGskuVersion"].ToInt32() == item.GskuVersion
                && operation["ExpectedRevisionVersion"].ToInt32() == item.ProductDefinitionRevisionVersion,
            LskuWorkflowRecoveryScope item => operation["LskuId"] == GuidValue(item.LskuId)
                && operation["GskuId"] == GuidValue(item.GskuId)
                && operation["ProductDefinitionRevisionId"] == GuidValue(item.ProductDefinitionRevisionId)
                && operation["MarketCode"] == item.MarketCode
                && operation["ExpectedLskuVersion"].ToInt32() == item.LskuVersion,
            FinishedGoodWorkflowRecoveryScope item => operation["FinishedGoodId"] == GuidValue(item.FinishedGoodId)
                && operation["GskuId"] == GuidValue(item.GskuId)
                && operation["ProductDefinitionRevisionId"] == GuidValue(item.ProductDefinitionRevisionId)
                && operation["ExpectedFinishedGoodVersion"].ToInt32() == item.FinishedGoodVersion,
            _ => false
        };

    private static BsonBinaryData GuidValue(Guid value) => new(value, GuidRepresentation.Standard);

    private static BsonArray DateTimeOffsetValue(long utcTicks) => [utcTicks, 0];

    private static FilterDefinition<BsonDocument> NullableEquals(string field, string? value)
    {
        var f = Builders<BsonDocument>.Filter;
        return value is null ? f.Or(f.Eq(field, BsonNull.Value), f.Exists(field, false)) : f.Eq(field, value);
    }

    private static FilterDefinition<BsonDocument> NullableEquals(string field, long? value)
    {
        var f = Builders<BsonDocument>.Filter;
        return value.HasValue ? f.Eq(field, value.Value) : f.Or(f.Eq(field, BsonNull.Value), f.Exists(field, false));
    }

    private static ProductIdentityWorkflowOperationRecoveryWriteResult Result(
        ProductIdentityWorkflowOperationRecoveryWriteStatus status,
        ProductIdentityWorkflowOperationRecoveryMutation mutation,
        string error) => new(status, mutation.OperationId, mutation.ExpectedScope, null, null, null, error);

    private static readonly (ProductIdentityWorkflowOperationFamily Family, string Collection)[] Families =
    [
        (ProductIdentityWorkflowOperationFamily.GlobalProduct, GlobalProductIdentityWorkflowOperationRepository.CollectionName),
        (ProductIdentityWorkflowOperationFamily.FirstGsku, FirstGskuIdentityWorkflowOperationRepository.CollectionName),
        (ProductIdentityWorkflowOperationFamily.Lsku, LskuIdentityWorkflowOperationRepository.CollectionName),
        (ProductIdentityWorkflowOperationFamily.FinishedGood, FinishedGoodIdentityWorkflowOperationRepository.CollectionName)
    ];

    private static readonly string[] MutableRecoveryFields =
    [
        "WorkflowInstanceId", "WorkflowTemplateVersionId", "ApprovalTaskId", "AssignmentSnapshotId",
        "StartTransitionLogId", "WorkflowStartedAtUtcTicksV1", "DecisionTransitionLogId", "DecisionKind",
        "DecisionObservedAtUtcTicksV1", "DecisionActorSubjectId", "DecisionReasonCode", "DecisionObjectType",
        "DecisionObjectId", "DecisionObjectRef", "DecisionWorkflowTemplateId", "DecisionWorkflowTemplateVersionId",
        "DecisionTaskStatus", "DecisionInstanceStatus", "DecisionTransitionSequence", "DecisionAtUtcTicksV1",
        "NextAttemptAtUtcTicksV1", "LastFailureCode", "LeaseOwner", "LeaseUntilUtcTicksV1", "RecoveryCommandId",
        "RecoveryOperatorSubjectId", "RecoveryReasonCode", "RecoveryComment", "RecoveryWorkflowNotFoundEvidenceId",
        "RecoveryWorkflowNotFoundEvidenceFingerprint", "RecoveryWorkflowNotFoundObservedAtUtcTicksV1",
        "RecoveredAtUtcTicksV1", "SuccessorOperationId", "SuccessorStartIdempotencyKey", "SuccessorOperationFingerprint"
    ];
}
