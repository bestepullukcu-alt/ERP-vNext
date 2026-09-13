using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductLegalEntityScopePolicyRepository : IProductLegalEntityScopePolicyRepository
{
    internal const string CollectionName = "mdm_product_legal_entity_scope_policies";
    private const int AuditLifecycleHeadroomBytes = 64 * 1024;
    private const int MaximumPreDeliveryBsonBytes =
        ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - AuditLifecycleHeadroomBytes;

    private readonly IMongoCollection<ProductLegalEntityScopePolicy> _policies;
    private readonly IProductLegalEntityScopeGuardedWriteSession _guardedWriteSession;
    private readonly Guid _tenantId;
    private readonly Func<Func<Task>, Task> _insertExecutor;
    private readonly Func<
        Func<Task<ProductLegalEntityScopePolicy?>>,
        Task<ProductLegalEntityScopePolicy?>> _updateExecutor;

    public ProductLegalEntityScopePolicyRepository(IMongoDatabase database, ITenantContext tenantContext)
        : this(
            database,
            tenantContext,
            new ProductLegalEntityScopeGuardedWriteSession(database, tenantContext),
            static operation => operation(),
            static operation => operation())
    {
    }

    public ProductLegalEntityScopePolicyRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        IProductLegalEntityScopeGuardedWriteSession guardedWriteSession)
        : this(
            database,
            tenantContext,
            guardedWriteSession,
            static operation => operation(),
            static operation => operation())
    {
    }

    internal ProductLegalEntityScopePolicyRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        IProductLegalEntityScopeGuardedWriteSession guardedWriteSession,
        Func<Func<Task>, Task> insertExecutor,
        Func<Func<Task<ProductLegalEntityScopePolicy?>>, Task<ProductLegalEntityScopePolicy?>> updateExecutor)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(guardedWriteSession);
        ArgumentNullException.ThrowIfNull(insertExecutor);
        ArgumentNullException.ThrowIfNull(updateExecutor);
        if (tenantContext.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A trusted tenant is required for product scope persistence.");
        }

        _policies = database.GetCollection<ProductLegalEntityScopePolicy>(CollectionName);
        _guardedWriteSession = guardedWriteSession;
        _tenantId = tenantContext.TenantId;
        _insertExecutor = insertExecutor;
        _updateExecutor = updateExecutor;
        EnsureIndexes();
    }

    public async Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(globalProductId, nameof(globalProductId));
        return await _policies.Find(
                ActiveTenantFilter
                & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(
                    policy => policy.GlobalProductId,
                    globalProductId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(
        Guid creationCommandId,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(creationCommandId, nameof(creationCommandId));
        return await _policies.Find(
                ActiveTenantFilter
                & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(
                    policy => policy.CreationCommandId,
                    creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(
        ProductLegalEntityScopePolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        PrepareCreate(policy);
        EnsureCompleteDocumentWithinBudget(policy.ToBsonDocument(), reserveAuditLifecycleHeadroom: true);

        try
        {
            await _insertExecutor(
                () => _policies.InsertOneAsync(policy, cancellationToken: cancellationToken));
            return new(true, policy);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await RecoverCreateAsync(policy, false, cancellationToken);
        }
        catch (MongoConnectionException)
        {
            return await RecoverCreateAsync(policy, true, cancellationToken);
        }
        catch (MongoExecutionTimeoutException)
        {
            return await RecoverCreateAsync(policy, true, cancellationToken);
        }
        catch (MongoWriteConcernException)
        {
            return await RecoverCreateAsync(policy, true, cancellationToken);
        }
    }

    public async Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(
        ProductLegalEntityScopePolicy policy,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUpdate(policy, expectedVersion);

        var persisted = await FindByIdIncludingDeletedAsync(policy.Id, cancellationToken);
        if (persisted is null
            || persisted.IsDeleted
            || persisted.GlobalProductId != policy.GlobalProductId
            || persisted.CreationCommandId != policy.CreationCommandId)
        {
            return new(false, null, VersionConflict: true);
        }

        var newAuditIntents = GetNewAuditIntents(persisted, policy, _tenantId);
        if (newAuditIntents.Single().Operation
            == ProductAuditOperation.ProductLegalEntityScopePolicyReplaced)
        {
            return new(
                false,
                persisted,
                VersionConflict: true,
                VerifiedZeroMutation: true);
        }
        EnsureProjectedDocumentWithinBudget(persisted, policy, newAuditIntents);

        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(item => item.Id, policy.Id)
                     & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(
                         item => item.GlobalProductId,
                         policy.GlobalProductId)
                     & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(
                         item => item.CreationCommandId,
                         policy.CreationCommandId)
                     & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(item => item.Version, expectedVersion)
                     & BuildServerSideBudgetFilter(policy, newAuditIntents);
        var update = Builders<ProductLegalEntityScopePolicy>.Update
            .Set(item => item.ScopePeriods, policy.ScopePeriods)
            .Set(item => item.UpdatedAt, policy.UpdatedAt)
            .Set(item => item.Version, policy.Version);
        if (newAuditIntents.Count > 0)
        {
            update = update.PushEach(item => item.AuditIntents, newAuditIntents);
        }

        try
        {
            var updated = await _updateExecutor(
                () => _policies.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<ProductLegalEntityScopePolicy>
                    {
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken)!);
            if (updated is not null)
            {
                EnsureCompleteDocumentWithinBudget(updated.ToBsonDocument(), reserveAuditLifecycleHeadroom: true);
                return ExactBusinessState(updated, policy)
                    ? new(true, updated)
                    : new(false, updated, VersionConflict: true);
            }

            return await RecoverUpdateAsync(policy, false, cancellationToken);
        }
        catch (MongoConnectionException)
        {
            return await RecoverUpdateAsync(policy, true, cancellationToken);
        }
        catch (MongoExecutionTimeoutException)
        {
            return await RecoverUpdateAsync(policy, true, cancellationToken);
        }
        catch (MongoWriteConcernException)
        {
            return await RecoverUpdateAsync(policy, true, cancellationToken);
        }
    }

    public Task<ProductLegalEntityScopePolicyWriteResult> ReplaceAsync(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ProductLegalEntityScopeWriterLease lease,
        ProductLegalEntityScopePolicy requestedPolicy,
        int expectedVersion,
        CancellationToken cancellationToken = default)
        => _guardedWriteSession.ReplaceAsync(
            authority,
            lease,
            requestedPolicy,
            expectedVersion,
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(globalProductIds);
        cancellationToken.ThrowIfCancellationRequested();
        if (globalProductIds.Count > ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot
            || globalProductIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentOutOfRangeException(nameof(globalProductIds));
        }
        if (globalProductIds.Count == 0)
        {
            return [];
        }

        return await _policies.Find(
                ActiveTenantFilter
                & Builders<ProductLegalEntityScopePolicy>.Filter.In(
                    policy => policy.GlobalProductId,
                    globalProductIds))
            .SortBy(policy => policy.GlobalProductId)
            .Project(policy => policy.GlobalProductId)
            .ToListAsync(cancellationToken);
    }

    private void PrepareCreate(ProductLegalEntityScopePolicy policy)
    {
        if (policy.GlobalProductId == Guid.Empty
            || policy.CreationCommandId == Guid.Empty
            || policy.Id == Guid.Empty)
        {
            throw new ArgumentException("Product scope identities must be non-empty.", nameof(policy));
        }

        policy.TenantId = _tenantId;
        policy.IsDeleted = false;
        policy.DeletedAt = null;
        policy.Version = 0;
        policy.EnsureValid(DateTimeOffset.UtcNow);
        if (policy.AuditIntents.Count != 1 || policy.AuditIntentReceipts.Count != 0)
        {
            throw new ArgumentException("Policy create requires one new audit intent and no receipts.", nameof(policy));
        }

        var period = policy.ScopePeriods.Single();
        ValidateNewAuditIntent(
            policy.AuditIntents[0],
            _tenantId,
            policy.Id,
            AuditAggregateType.ProductLegalEntityScopePolicy,
            ProductAuditOperation.ProductLegalEntityScopePolicyCreated,
            -1,
            0,
            period.CommandId,
            period.ActorId,
            period.CreatedAtUtc);
    }

    private void ValidateUpdate(ProductLegalEntityScopePolicy policy, int expectedVersion)
    {
        if (policy.Id == Guid.Empty
            || policy.GlobalProductId == Guid.Empty
            || policy.CreationCommandId == Guid.Empty
            || policy.TenantId != _tenantId
            || policy.IsDeleted
            || expectedVersion < 0
            || policy.Version != expectedVersion + 1)
        {
            throw new ArgumentException("Product scope update contract is invalid.", nameof(policy));
        }

        policy.EnsureValid(DateTimeOffset.UtcNow);
    }

    private async Task<ProductLegalEntityScopePolicyWriteResult> RecoverCreateAsync(
        ProductLegalEntityScopePolicy requested,
        bool ambiguous,
        CancellationToken cancellationToken)
    {
        var existing = await _policies.Find(
                TenantIncludingDeletedFilter
                & Builders<ProductLegalEntityScopePolicy>.Filter.Or(
                    Builders<ProductLegalEntityScopePolicy>.Filter.Eq(
                        item => item.GlobalProductId,
                        requested.GlobalProductId),
                    Builders<ProductLegalEntityScopePolicy>.Filter.Eq(
                        item => item.CreationCommandId,
                        requested.CreationCommandId)))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null
            && !existing.IsDeleted
            && ExactCreatePayload(existing, requested)
            && (!ambiguous || ContainsCompleteImmutableAuditProof(existing.AuditIntents, requested.AuditIntents)))
        {
            EnsureCompleteDocumentWithinBudget(existing.ToBsonDocument(), reserveAuditLifecycleHeadroom: false);
            return new(true, existing);
        }

        return new(
            false,
            existing,
            VersionConflict: !ambiguous,
            WriteOutcomeAmbiguous: ambiguous);
    }

    private async Task<ProductLegalEntityScopePolicyWriteResult> RecoverUpdateAsync(
        ProductLegalEntityScopePolicy requested,
        bool ambiguous,
        CancellationToken cancellationToken)
    {
        var existing = await FindByIdIncludingDeletedAsync(requested.Id, cancellationToken);
        if (existing is not null && !existing.IsDeleted && ExactBusinessState(existing, requested))
        {
            EnsureCompleteDocumentWithinBudget(existing.ToBsonDocument(), reserveAuditLifecycleHeadroom: false);
            return new(true, existing);
        }

        return new(
            false,
            existing,
            VersionConflict: !ambiguous,
            WriteOutcomeAmbiguous: ambiguous);
    }

    private async Task<ProductLegalEntityScopePolicy?> FindByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await _policies.Find(
                TenantIncludingDeletedFilter
                & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(item => item.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    internal static IReadOnlyList<LocalAuditIntent> GetNewAuditIntents(
        ProductLegalEntityScopePolicy persisted,
        ProductLegalEntityScopePolicy requested,
        Guid tenantId)
    {
        if (requested.AuditIntents.Select(intent => intent.IntentId).Distinct().Count()
            != requested.AuditIntents.Count)
        {
            throw new ArgumentException("Audit intent identities must be unique.", nameof(requested));
        }

        var persistedById = persisted.AuditIntents.ToDictionary(intent => intent.IntentId);
        foreach (var requestedIntent in requested.AuditIntents)
        {
            if (persistedById.TryGetValue(requestedIntent.IntentId, out var persistedIntent)
                && !ImmutableIntentPayloadEquals(persistedIntent, requestedIntent))
            {
                throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_AUDIT_INTENT_IMMUTABLE_DRIFT");
            }
        }

        var newIntents = requested.AuditIntents
            .Where(intent => !persistedById.ContainsKey(intent.IntentId))
            .ToArray();
        if (newIntents.Length != 1)
        {
            throw new ArgumentException("Policy update requires exactly one new audit intent.", nameof(requested));
        }

        var transition = ResolvePolicyTransition(requested, newIntents[0]);
        ValidateNewAuditIntent(
            newIntents[0],
            tenantId,
            requested.Id,
            AuditAggregateType.ProductLegalEntityScopePolicy,
            transition.Operation,
            requested.Version - 1,
            requested.Version,
            transition.CommandId,
            transition.ActorId,
            transition.TimestampUtc);
        return newIntents;
    }

    internal static (ProductAuditOperation Operation, Guid CommandId, Guid ActorId, DateTimeOffset TimestampUtc)
        ResolvePolicyTransition(
            ProductLegalEntityScopePolicy requested,
            LocalAuditIntent intent)
    {
        if (!Guid.TryParse(intent.CommandId, out var commandId) || commandId == Guid.Empty)
        {
            throw new ArgumentException("Policy audit command identity is invalid.", nameof(intent));
        }

        if (intent.Operation == ProductAuditOperation.ProductLegalEntityScopePolicyReplaced)
        {
            var added = requested.ScopePeriods.Where(period => period.CommandId == commandId).ToArray();
            var ended = requested.ScopePeriods.Where(period => period.EndCommandId == commandId).ToArray();
            if (added.Length == 1
                && ended.Length == 1
                && ended[0].EffectiveToUtc == added[0].EffectiveFromUtc)
            {
                return (
                    ProductAuditOperation.ProductLegalEntityScopePolicyReplaced,
                    commandId,
                    added[0].ActorId,
                    added[0].CreatedAtUtc);
            }
        }
        else if (intent.Operation == ProductAuditOperation.ProductLegalEntityScopePolicyEnded)
        {
            var ended = requested.ScopePeriods.Where(period => period.EndCommandId == commandId).ToArray();
            if (ended.Length == 1)
            {
                return (
                    ProductAuditOperation.ProductLegalEntityScopePolicyEnded,
                    commandId,
                    ended[0].EndedByActorId!.Value,
                    ended[0].EndedAtUtc!.Value);
            }
        }

        throw new ArgumentException("Policy update does not describe one approved transition.", nameof(requested));
    }

    private static void ValidateNewAuditIntent(
        LocalAuditIntent intent,
        Guid tenantId,
        Guid aggregateId,
        AuditAggregateType aggregateType,
        ProductAuditOperation operation,
        int preVersion,
        int postVersion,
        Guid commandId,
        Guid actorId,
        DateTimeOffset timestampUtc)
    {
        if (intent.IntentId == Guid.Empty
            || intent.TenantId != tenantId
            || intent.AggregateId != aggregateId
            || intent.AggregateType != aggregateType
            || !string.Equals(intent.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
            || intent.SchemaVersion != 1
            || intent.Operation != operation
            || intent.PreVersion != preVersion
            || intent.PostVersion != postVersion
            || !Guid.TryParse(intent.CommandId, out var parsedCommandId)
            || parsedCommandId != commandId
            || !Guid.TryParse(intent.ActorId, out var parsedActorId)
            || parsedActorId != actorId
            || intent.TimestampUtc != timestampUtc
            || intent.TimestampUtc.Offset != TimeSpan.Zero
            || intent.Sequence != postVersion + 1L
            || string.IsNullOrWhiteSpace(intent.CorrelationId)
            || string.IsNullOrWhiteSpace(intent.CausationId)
            || string.IsNullOrWhiteSpace(intent.EvidenceHash)
            || string.IsNullOrWhiteSpace(intent.SnapshotReference)
            || string.IsNullOrWhiteSpace(intent.IdempotencyKey)
            || !IsInitialDeliveryState(intent))
        {
            throw new ArgumentException("Audit intent immutable binding is invalid.", nameof(intent));
        }
    }

    private static bool ImmutableIntentPayloadEquals(LocalAuditIntent left, LocalAuditIntent right)
        => string.Equals(left.SourceService, right.SourceService, StringComparison.Ordinal)
           && left.SchemaVersion == right.SchemaVersion
           && string.Equals(left.ContractVersion, right.ContractVersion, StringComparison.Ordinal)
           && left.IntentId == right.IntentId
           && left.TenantId == right.TenantId
           && left.AggregateType == right.AggregateType
           && left.AggregateId == right.AggregateId
           && left.PreVersion == right.PreVersion
           && left.PostVersion == right.PostVersion
           && left.Operation == right.Operation
           && string.Equals(left.ActorId, right.ActorId, StringComparison.Ordinal)
           && string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal)
           && string.Equals(left.CausationId, right.CausationId, StringComparison.Ordinal)
           && string.Equals(left.CommandId, right.CommandId, StringComparison.Ordinal)
           && left.Sequence == right.Sequence
           && left.TimestampUtc == right.TimestampUtc
           && string.Equals(left.EvidenceHash, right.EvidenceHash, StringComparison.Ordinal)
           && string.Equals(left.SnapshotReference, right.SnapshotReference, StringComparison.Ordinal)
           && string.Equals(left.IdempotencyKey, right.IdempotencyKey, StringComparison.Ordinal);

    private static bool IsInitialDeliveryState(LocalAuditIntent intent)
        => intent.DeliveryState == AuditIntentDeliveryState.Pending
           && intent.AttemptCount == 0
           && intent.LastAttemptAt is null
           && intent.NextRetryAt is null
           && intent.CentralAcknowledgement is null
           && intent.CentralIdempotencyKey is null
           && intent.AcknowledgedContractVersion is null
           && intent.AcknowledgedAt is null
           && intent.LastError is null
           && intent.LeaseOwner is null
           && intent.ClaimToken is null
           && intent.ClaimGeneration == 0
           && intent.ClaimedAt is null
           && intent.LeaseUntil is null
           && intent.DeliveredAt is null
           && intent.DeadLetteredAt is null
           && intent.CompactedAt is null
           && intent.CompactReceiptReference is null
           && intent.FailureClass == AuditIntentFailureClass.None
           && intent.FailureReason is null;

    private static bool ExactCreatePayload(
        ProductLegalEntityScopePolicy persisted,
        ProductLegalEntityScopePolicy requested)
    {
        if (persisted.TenantId != requested.TenantId
            || persisted.GlobalProductId != requested.GlobalProductId
            || persisted.CreationCommandId != requested.CreationCommandId
            || persisted.ScopePeriods.Count != 1
            || requested.ScopePeriods.Count != 1)
        {
            return false;
        }

        var left = persisted.ScopePeriods[0];
        var right = requested.ScopePeriods[0];
        return left.Mode == right.Mode
               && left.ActorId == right.ActorId
               && left.CommandId == right.CommandId
               && left.LegalEntityIds.SequenceEqual(right.LegalEntityIds);
    }

    internal static bool ExactBusinessState(
        ProductLegalEntityScopePolicy persisted,
        ProductLegalEntityScopePolicy requested)
        => persisted.TenantId == requested.TenantId
           && persisted.Id == requested.Id
           && persisted.GlobalProductId == requested.GlobalProductId
           && persisted.CreationCommandId == requested.CreationCommandId
           && persisted.Version == requested.Version
           && persisted.IsDeleted == requested.IsDeleted
           && ExactPeriods(persisted.ScopePeriods, requested.ScopePeriods)
           && ContainsCompleteImmutableAuditProof(persisted.AuditIntents, requested.AuditIntents);

    private static bool ExactPeriods(
        IReadOnlyList<ProductLegalEntityScopePeriod> left,
        IReadOnlyList<ProductLegalEntityScopePeriod> right)
        => left.Count == right.Count
           && left.Zip(right).All(pair =>
               pair.First.ToBsonDocument().Equals(pair.Second.ToBsonDocument()));

    internal static FilterDefinition<ProductLegalEntityScopePolicy> BuildServerSideBudgetFilter(
        ProductLegalEntityScopePolicy requested,
        IReadOnlyCollection<LocalAuditIntent> newAuditIntents)
    {
        var requestedDocument = requested.ToBsonDocument();
        var existingIntents = new BsonDocument(
            "$ifNull",
            new BsonArray { "$AuditIntents", new BsonArray() });
        BsonValue projectedIntents = existingIntents;
        if (newAuditIntents.Count > 0)
        {
            projectedIntents = new BsonDocument(
                "$concatArrays",
                new BsonArray
                {
                    existingIntents,
                    new BsonArray(newAuditIntents.Select(intent => intent.ToBsonDocument()))
                });
        }

        var projectedFields = new BsonDocument
        {
            { nameof(ProductLegalEntityScopePolicy.ScopePeriods), requestedDocument[nameof(ProductLegalEntityScopePolicy.ScopePeriods)] },
            { nameof(ProductLegalEntityScopePolicy.UpdatedAt), requestedDocument[nameof(ProductLegalEntityScopePolicy.UpdatedAt)] },
            { nameof(ProductLegalEntityScopePolicy.Version), requested.Version },
            { nameof(ProductLegalEntityScopePolicy.AuditIntents), projectedIntents }
        };
        var projectedDocument = new BsonDocument(
            "$mergeObjects",
            new BsonArray { "$$ROOT", projectedFields });
        var expression = new BsonDocument(
            "$expr",
            new BsonDocument(
                "$lte",
                new BsonArray
                {
                    new BsonDocument("$bsonSize", projectedDocument),
                    MaximumPreDeliveryBsonBytes
                }));
        return new BsonDocumentFilterDefinition<ProductLegalEntityScopePolicy>(expression);
    }

    internal static void EnsureProjectedDocumentWithinBudget(
        ProductLegalEntityScopePolicy persisted,
        ProductLegalEntityScopePolicy requested,
        IReadOnlyCollection<LocalAuditIntent> newAuditIntents)
    {
        var projected = persisted.ToBsonDocument();
        var requestedDocument = requested.ToBsonDocument();
        projected[nameof(ProductLegalEntityScopePolicy.ScopePeriods)] =
            requestedDocument[nameof(ProductLegalEntityScopePolicy.ScopePeriods)];
        projected[nameof(ProductLegalEntityScopePolicy.UpdatedAt)] =
            requestedDocument[nameof(ProductLegalEntityScopePolicy.UpdatedAt)];
        projected[nameof(ProductLegalEntityScopePolicy.Version)] = requested.Version;
        var intents = projected[nameof(ProductLegalEntityScopePolicy.AuditIntents)].AsBsonArray;
        foreach (var intent in newAuditIntents)
        {
            intents.Add(intent.ToBsonDocument());
        }

        EnsureCompleteDocumentWithinBudget(projected, reserveAuditLifecycleHeadroom: true);
    }

    internal static void EnsureCompleteDocumentWithinBudget(
        BsonDocument document,
        bool reserveAuditLifecycleHeadroom)
    {
        var size = document.ToBson().Length;
        ProductLegalEntityScopePolicy.EnsureSerializedBsonSizeWithinLimit(size);
        if (reserveAuditLifecycleHeadroom && size > MaximumPreDeliveryBsonBytes)
        {
            throw new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_AUDIT_HEADROOM_EXCEEDED");
        }
    }

    private static bool ContainsCompleteImmutableAuditProof(
        IReadOnlyCollection<LocalAuditIntent> persisted,
        IReadOnlyCollection<LocalAuditIntent> requested)
        => requested.All(requestedIntent => persisted.Any(persistedIntent =>
            persistedIntent.IntentId == requestedIntent.IntentId
            && ImmutableIntentPayloadEquals(persistedIntent, requestedIntent)));

    private void EnsureIndexes()
    {
        _policies.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductLegalEntityScopePolicy>(
                Builders<ProductLegalEntityScopePolicy>.IndexKeys
                    .Ascending(policy => policy.TenantId)
                    .Ascending(policy => policy.GlobalProductId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_legal_entity_scope_policies_tenant_product"
                }),
            new CreateIndexModel<ProductLegalEntityScopePolicy>(
                Builders<ProductLegalEntityScopePolicy>.IndexKeys
                    .Ascending(policy => policy.TenantId)
                    .Ascending(policy => policy.CreationCommandId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_legal_entity_scope_policies_tenant_command"
                })
        ]);
    }

    private FilterDefinition<ProductLegalEntityScopePolicy> TenantIncludingDeletedFilter =>
        Builders<ProductLegalEntityScopePolicy>.Filter.Eq(policy => policy.TenantId, _tenantId);

    private FilterDefinition<ProductLegalEntityScopePolicy> ActiveTenantFilter =>
        TenantIncludingDeletedFilter
        & Builders<ProductLegalEntityScopePolicy>.Filter.Eq(policy => policy.IsDeleted, false);

    private static void EnsureIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must be non-empty.", parameterName);
        }
    }
}
