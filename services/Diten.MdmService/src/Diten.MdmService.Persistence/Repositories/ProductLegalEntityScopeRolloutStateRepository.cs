using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductLegalEntityScopeRolloutStateRepository
    : IProductLegalEntityScopeRolloutStateRepository
{
    internal const string CollectionName = "mdm_product_legal_entity_scope_rollout_states";
    private const int AuditLifecycleHeadroomBytes = 64 * 1024;
    private const int MaximumPreDeliveryBsonBytes =
        ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - AuditLifecycleHeadroomBytes;

    private readonly IMongoCollection<ProductLegalEntityScopeRolloutState> _states;
    private readonly Guid _tenantId;
    private readonly Func<Func<Task>, Task> _insertExecutor;
    private readonly Func<
        Func<Task<ProductLegalEntityScopeRolloutState?>>,
        Task<ProductLegalEntityScopeRolloutState?>> _updateExecutor;

    public ProductLegalEntityScopeRolloutStateRepository(IMongoDatabase database, ITenantContext tenantContext)
        : this(
            database,
            tenantContext,
            static operation => operation(),
            static operation => operation())
    {
    }

    internal ProductLegalEntityScopeRolloutStateRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        Func<Func<Task>, Task> insertExecutor,
        Func<Func<Task<ProductLegalEntityScopeRolloutState?>>, Task<ProductLegalEntityScopeRolloutState?>> updateExecutor)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(insertExecutor);
        ArgumentNullException.ThrowIfNull(updateExecutor);
        if (tenantContext.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A trusted tenant is required for product scope rollout persistence.");
        }

        _states = database.GetCollection<ProductLegalEntityScopeRolloutState>(CollectionName);
        _tenantId = tenantContext.TenantId;
        _insertExecutor = insertExecutor;
        _updateExecutor = updateExecutor;
        EnsureIndexes();
    }

    public async Task<ProductLegalEntityScopeRolloutState?> GetAsync(
        CancellationToken cancellationToken = default)
        => EnsureValidPersistedState(
            await _states.Find(ActiveTenantFilter).FirstOrDefaultAsync(cancellationToken));

    public async Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(
        Guid creationCommandId,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(creationCommandId, nameof(creationCommandId));
        return EnsureValidPersistedState(await _states.Find(
                ActiveTenantFilter
                & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
                    state => state.CreationCommandId,
                    creationCommandId))
            .FirstOrDefaultAsync(cancellationToken));
    }

    private static ProductLegalEntityScopeRolloutState? EnsureValidPersistedState(
        ProductLegalEntityScopeRolloutState? state)
    {
        state?.EnsureValid();
        return state;
    }

    public async Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
        ProductLegalEntityScopeRolloutState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        PrepareCreate(state);
        EnsureCompleteDocumentWithinBudget(state.ToBsonDocument(), reserveAuditLifecycleHeadroom: true);
        try
        {
            await _insertExecutor(
                () => _states.InsertOneAsync(state, cancellationToken: cancellationToken));
            return new(true, state);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await RecoverCreateAsync(state, false, cancellationToken);
        }
        catch (MongoConnectionException)
        {
            return await RecoverCreateAsync(state, true, cancellationToken);
        }
        catch (MongoExecutionTimeoutException)
        {
            return await RecoverCreateAsync(state, true, cancellationToken);
        }
        catch (MongoWriteConcernException)
        {
            return await RecoverCreateAsync(state, true, cancellationToken);
        }
    }

    public async Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
        ProductLegalEntityScopeRolloutState state,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateUpdate(state, expectedVersion);

        var persisted = await FindByIdIncludingDeletedAsync(state.Id, cancellationToken);
        if (persisted is null
            || persisted.IsDeleted
            || persisted.CreationCommandId != state.CreationCommandId
            || persisted.CreatedByActorId != state.CreatedByActorId)
        {
            return new(false, null, VersionConflict: true);
        }

        var newAuditIntents = GetNewAuditIntents(persisted, state);
        EnsureProjectedDocumentWithinBudget(persisted, state, newAuditIntents);

        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Id, state.Id)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
                         item => item.CreationCommandId,
                         state.CreationCommandId)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
                         item => item.CreatedByActorId,
                         state.CreatedByActorId)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Version, expectedVersion)
                     & BuildServerSideBudgetFilter(state, newAuditIntents);
        var update = Builders<ProductLegalEntityScopeRolloutState>.Update
            .Set(item => item.Mode, state.Mode)
            .Set(item => item.UpdatedAt, state.UpdatedAt)
            .Set(item => item.Version, state.Version);
        if (newAuditIntents.Count > 0)
        {
            update = update.PushEach(item => item.AuditIntents, newAuditIntents);
        }

        try
        {
            var updated = await _updateExecutor(
                () => _states.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<ProductLegalEntityScopeRolloutState>
                    {
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken)!);
            if (updated is not null)
            {
                EnsureCompleteDocumentWithinBudget(updated.ToBsonDocument(), reserveAuditLifecycleHeadroom: true);
                return ExactBusinessState(updated, state)
                    ? new(true, updated)
                    : new(false, updated, VersionConflict: true);
            }

            return await RecoverUpdateAsync(state, false, cancellationToken);
        }
        catch (MongoConnectionException)
        {
            return await RecoverUpdateAsync(state, true, cancellationToken);
        }
        catch (MongoExecutionTimeoutException)
        {
            return await RecoverUpdateAsync(state, true, cancellationToken);
        }
        catch (MongoWriteConcernException)
        {
            return await RecoverUpdateAsync(state, true, cancellationToken);
        }
    }

    public async Task<ProductLegalEntityScopeWriterLeaseResult> AcquireWriterLeaseAsync(
        ProductLegalEntityScopeWriterLease requested,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        requested.EnsureValid();
        var current = await GetAsync(cancellationToken);
        if (current is null) return new(false, null, null, LegacyBypass: true);
        if (current.ActiveFence is not null)
            return new(false, null, current, FailureCode: "PRODUCT_SCOPE_WRITE_FENCE_ACTIVE");
        if (current.Mode == ProductLegalEntityScopeRolloutMode.FailClosedSuspended)
            return new(false, null, current, FailureCode: "PRODUCT_SCOPE_FAIL_CLOSED_SUSPENDED");
        if (current.ActiveWriterLease is not null)
        {
            var lease = current.ActiveWriterLease;
            return lease.CommandId == requested.CommandId
                   && lease.ActorId == requested.ActorId
                   && string.Equals(lease.MutationKind, requested.MutationKind, StringComparison.Ordinal)
                   && string.Equals(lease.PayloadFingerprint, requested.PayloadFingerprint, StringComparison.Ordinal)
                ? new(true, lease, current)
                : new(false, null, current, FailureCode: "PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE");
        }

        requested.Generation = checked(current.WriterLeaseGeneration + 1);
        requested.EnsureValid();
        var generationFilter = current.WriterLeaseGeneration == 0
            ? Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.WriterLeaseGeneration, 0)
              | Builders<ProductLegalEntityScopeRolloutState>.Filter.Exists(
                  nameof(ProductLegalEntityScopeRolloutState.WriterLeaseGeneration), false)
            : Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
                item => item.WriterLeaseGeneration, current.WriterLeaseGeneration);
        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Id, current.Id)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Version, current.Version)
                     & generationFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveWriterLease, null)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveFence, null);
        var update = Builders<ProductLegalEntityScopeRolloutState>.Update
            .Set(item => item.ActiveWriterLease, requested)
            .Set(item => item.WriterLeaseGeneration, requested.Generation);
        var updated = await _states.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<ProductLegalEntityScopeRolloutState> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is null
            ? new(false, null, await GetAsync(cancellationToken), FailureCode: "PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE")
            : new(true, updated.ActiveWriterLease, updated);
    }

    public async Task<bool> BindWriterLeaseBaselineAsync(
        Guid token,
        long generation,
        string preWriteStateHash,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(token, nameof(token));
        if (generation <= 0 || !ExactHash(preWriteStateHash)) return false;
        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveWriterLease.Token", token)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveWriterLease.Generation", generation)
                     & new BsonDocumentFilterDefinition<ProductLegalEntityScopeRolloutState>(
                         new BsonDocument("ActiveWriterLease.PreWriteStateHash", BsonNull.Value));
        var result = await _states.UpdateOneAsync(
            filter,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(
                "ActiveWriterLease.PreWriteStateHash",
                preWriteStateHash),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> ReleaseWriterLeaseAsync(
        Guid token,
        long generation,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity(token, nameof(token));
        if (generation <= 0) return false;
        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveWriterLease.Token", token)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveWriterLease.Generation", generation)
                     & new BsonDocumentFilterDefinition<ProductLegalEntityScopeRolloutState>(
                         new BsonDocument("ActiveWriterLease.PreWriteStateHash", new BsonDocument("$ne", BsonNull.Value)));
        var result = await _states.UpdateOneAsync(
            filter,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.ActiveWriterLease, null),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<ProductLegalEntityScopeFenceResult> AcquireFenceAsync(
        ProductLegalEntityScopeActivationFence requested,
        Guid expectedRolloutStateId,
        int expectedVersion,
        ProductLegalEntityScopeRolloutMode expectedMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        requested.EnsureValid();
        var current = await GetAsync(cancellationToken);
        if (current is null || current.Id != expectedRolloutStateId || current.Version != expectedVersion
            || current.Mode != expectedMode)
            return new(false, null, current, "PRODUCT_SCOPE_ROLLOUT_VERSION_CONFLICT");
        if (current.ActiveWriterLease is not null)
            return new(false, null, current, "PRODUCT_SCOPE_WRITER_LEASE_ACTIVE");
        if (current.ActiveFence is not null)
        {
            return ExactFence(current.ActiveFence, requested)
                ? new(true, current.ActiveFence, current)
                : new(false, null, current, "PRODUCT_SCOPE_ACTIVATION_FENCE_CONFLICT");
        }
        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Id, expectedRolloutStateId)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Version, expectedVersion)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Mode, expectedMode)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveWriterLease, null)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveFence, null);
        var updated = await _states.FindOneAndUpdateAsync(
            filter,
            Builders<ProductLegalEntityScopeRolloutState>.Update.Set(item => item.ActiveFence, requested),
            new FindOneAndUpdateOptions<ProductLegalEntityScopeRolloutState> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is null
            ? new(false, null, await GetAsync(cancellationToken), "PRODUCT_SCOPE_ACTIVATION_FENCE_CONFLICT")
            : new(true, updated.ActiveFence, updated);
    }

    public async Task<bool> BindFenceSnapshotsAsync(
        string fenceToken,
        ProductLegalEntityScopeInventorySnapshot first,
        ProductLegalEntityScopeInventorySnapshot second,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        first.EnsureValid();
        second.EnsureValid();
        if (!ExactHash(fenceToken) || !string.Equals(first.StableFactsHash, second.StableFactsHash, StringComparison.Ordinal))
            return false;
        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveFence.Token", fenceToken)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveFence.State", ProductLegalEntityScopeAdmissionState.Closing)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveWriterLease, null);
        var update = Builders<ProductLegalEntityScopeRolloutState>.Update
            .Set("ActiveFence.State", ProductLegalEntityScopeAdmissionState.Quiesced)
            .Set("ActiveFence.FirstStableFactsHash", first.StableFactsHash)
            .Set("ActiveFence.SecondStableFactsHash", second.StableFactsHash)
            .Set(item => item.LastInventorySnapshot, second);
        var result = await _states.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<ProductLegalEntityScopeRolloutStateWriteResult> CommitTransitionAsync(
        string fenceToken,
        ProductLegalEntityScopeRolloutState requested,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        if (!ExactHash(fenceToken)) throw new ArgumentException("Fence token is invalid.", nameof(fenceToken));
        ValidateUpdate(requested, expectedVersion);
        var persisted = await GetAsync(cancellationToken);
        if (persisted is null || persisted.Id != requested.Id || persisted.ActiveFence is null
            || !string.Equals(persisted.ActiveFence.Token, fenceToken, StringComparison.Ordinal)
            || persisted.ActiveFence.State != ProductLegalEntityScopeAdmissionState.Quiesced)
            return new(false, persisted, VersionConflict: true);
        var newIntents = GetNewAuditIntents(persisted, requested);
        EnsureProjectedDocumentWithinBudget(persisted, requested, newIntents);
        var filter = ActiveTenantFilter
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Id, requested.Id)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Version, expectedVersion)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveFence.Token", fenceToken)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq("ActiveFence.State", ProductLegalEntityScopeAdmissionState.Quiesced)
                     & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveWriterLease, null)
                     & BuildServerSideBudgetFilter(requested, newIntents);
        var update = Builders<ProductLegalEntityScopeRolloutState>.Update
            .Set(item => item.Mode, requested.Mode)
            .Set(item => item.Version, requested.Version)
            .Set(item => item.UpdatedAt, requested.UpdatedAt)
            .Set(item => item.LastInventorySnapshot, requested.LastInventorySnapshot)
            .Set(item => item.LastTransitionCommandId, requested.LastTransitionCommandId)
            .Set(item => item.LastTransitionActorId, requested.LastTransitionActorId)
            .Set(item => item.LastTransitionAction, requested.LastTransitionAction)
            .Set(item => item.LastTransitionReasonCode, requested.LastTransitionReasonCode)
            .Set(item => item.LastTransitionEvidenceHash, requested.LastTransitionEvidenceHash)
            .Set(item => item.LastTransitionIntentId, requested.LastTransitionIntentId)
            .Set(item => item.ActiveFence, null)
            .PushEach(item => item.AuditIntents, newIntents);
        try
        {
            var updated = await _updateExecutor(
                () => _states.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<ProductLegalEntityScopeRolloutState>
                    {
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken)!);
            return updated is null
                ? await RecoverUpdateAsync(requested, false, cancellationToken)
                : new(true, updated);
        }
        catch (MongoConnectionException)
        {
            return await RecoverUpdateAsync(requested, true, cancellationToken);
        }
        catch (MongoExecutionTimeoutException)
        {
            return await RecoverUpdateAsync(requested, true, cancellationToken);
        }
        catch (MongoWriteConcernException)
        {
            return await RecoverUpdateAsync(requested, true, cancellationToken);
        }
    }

    private static bool ExactFence(ProductLegalEntityScopeActivationFence left, ProductLegalEntityScopeActivationFence right)
        => string.Equals(left.Token, right.Token, StringComparison.Ordinal)
           && string.Equals(left.Action, right.Action, StringComparison.Ordinal)
           && left.CommandId == right.CommandId && left.ActorId == right.ActorId
           && string.Equals(left.ReasonCode, right.ReasonCode, StringComparison.Ordinal);

    private static bool ExactHash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit)
        && string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal);

    private void PrepareCreate(ProductLegalEntityScopeRolloutState state)
    {
        if (state.Id == Guid.Empty || state.CreationCommandId == Guid.Empty)
        {
            throw new ArgumentException("Rollout state identities must be non-empty.", nameof(state));
        }

        state.TenantId = _tenantId;
        state.IsDeleted = false;
        state.DeletedAt = null;
        state.Version = 0;
        state.EnsureValid();
        if (state.AuditIntents.Count != 0 || state.AuditIntentReceipts.Count != 0)
        {
            throw new ArgumentException(
                "Preparation creation has no approved audit operation or receipt.",
                nameof(state));
        }
    }

    private void ValidateUpdate(ProductLegalEntityScopeRolloutState state, int expectedVersion)
    {
        if (state.Id == Guid.Empty
            || state.CreationCommandId == Guid.Empty
            || state.TenantId != _tenantId
            || state.IsDeleted
            || expectedVersion < 0
            || state.Version != expectedVersion + 1)
        {
            throw new ArgumentException("Rollout state update contract is invalid.", nameof(state));
        }

        state.EnsureValid();
    }

    private async Task<ProductLegalEntityScopeRolloutStateWriteResult> RecoverCreateAsync(
        ProductLegalEntityScopeRolloutState requested,
        bool ambiguous,
        CancellationToken cancellationToken)
    {
        var existing = await _states.Find(
                TenantIncludingDeletedFilter
                & Builders<ProductLegalEntityScopeRolloutState>.Filter.Or(
                    Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Id, requested.Id),
                    Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
                        item => item.CreationCommandId,
                        requested.CreationCommandId)))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null && !existing.IsDeleted && ExactCreatePayload(existing, requested))
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

    private async Task<ProductLegalEntityScopeRolloutStateWriteResult> RecoverUpdateAsync(
        ProductLegalEntityScopeRolloutState requested,
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

    private async Task<ProductLegalEntityScopeRolloutState?> FindByIdIncludingDeletedAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await _states.Find(
                TenantIncludingDeletedFilter
                & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    private IReadOnlyList<LocalAuditIntent> GetNewAuditIntents(
        ProductLegalEntityScopeRolloutState persisted,
        ProductLegalEntityScopeRolloutState requested)
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
            throw new ArgumentException("Rollout update requires exactly one new audit intent.", nameof(requested));
        }

        var expectedOperation = requested.Mode switch
        {
            ProductLegalEntityScopeRolloutMode.Enforced =>
                ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            ProductLegalEntityScopeRolloutMode.FailClosedSuspended =>
                ProductAuditOperation.ProductLegalEntityScopeEnforcementSuspended,
            _ => throw new ArgumentException(
                "Rollout update does not describe one approved transition.",
                nameof(requested))
        };
        ValidateNewAuditIntent(
            newIntents[0],
            requested.Id,
            expectedOperation,
            requested.Version - 1,
            requested.Version);
        return newIntents;
    }

    private void ValidateNewAuditIntent(
        LocalAuditIntent intent,
        Guid aggregateId,
        ProductAuditOperation operation,
        int preVersion,
        int postVersion)
    {
        if (intent.IntentId == Guid.Empty
            || intent.TenantId != _tenantId
            || intent.AggregateId != aggregateId
            || intent.AggregateType != AuditAggregateType.ProductLegalEntityScopeRolloutState
            || !string.Equals(intent.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
            || intent.SchemaVersion != 1
            || intent.Operation != operation
            || intent.PreVersion != preVersion
            || intent.PostVersion != postVersion
            || !Guid.TryParse(intent.CommandId, out var parsedCommandId)
            || parsedCommandId == Guid.Empty
            || !Guid.TryParse(intent.ActorId, out var parsedActorId)
            || parsedActorId == Guid.Empty
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
        ProductLegalEntityScopeRolloutState persisted,
        ProductLegalEntityScopeRolloutState requested)
        => persisted.TenantId == requested.TenantId
           && persisted.CreationCommandId == requested.CreationCommandId
           && persisted.CreatedByActorId == requested.CreatedByActorId
           && persisted.Mode == requested.Mode;

    private static bool ExactBusinessState(
        ProductLegalEntityScopeRolloutState persisted,
        ProductLegalEntityScopeRolloutState requested)
        => persisted.TenantId == requested.TenantId
           && persisted.Id == requested.Id
           && persisted.CreationCommandId == requested.CreationCommandId
           && persisted.CreatedByActorId == requested.CreatedByActorId
           && persisted.Mode == requested.Mode
           && persisted.Version == requested.Version
           && persisted.IsDeleted == requested.IsDeleted
           && persisted.LastTransitionCommandId == requested.LastTransitionCommandId
           && persisted.LastTransitionActorId == requested.LastTransitionActorId
           && string.Equals(persisted.LastTransitionAction, requested.LastTransitionAction, StringComparison.Ordinal)
           && string.Equals(persisted.LastTransitionReasonCode, requested.LastTransitionReasonCode, StringComparison.Ordinal)
           && string.Equals(persisted.LastTransitionEvidenceHash, requested.LastTransitionEvidenceHash, StringComparison.Ordinal)
           && persisted.LastTransitionIntentId == requested.LastTransitionIntentId
           && ExactSnapshot(persisted.LastInventorySnapshot, requested.LastInventorySnapshot)
           && ContainsCompleteImmutableAuditProof(persisted.AuditIntents, requested.AuditIntents);

    private static bool ExactSnapshot(
        ProductLegalEntityScopeInventorySnapshot? left,
        ProductLegalEntityScopeInventorySnapshot? right) =>
        left is null && right is null || left is not null && right is not null
        && left.SchemaVersion == right.SchemaVersion && left.ObservedAtUtc == right.ObservedAtUtc
        && string.Equals(left.CanonicalPayload, right.CanonicalPayload, StringComparison.Ordinal)
        && string.Equals(left.StableFactsHash, right.StableFactsHash, StringComparison.Ordinal)
        && string.Equals(left.SnapshotHash, right.SnapshotHash, StringComparison.Ordinal);

    private static FilterDefinition<ProductLegalEntityScopeRolloutState> BuildServerSideBudgetFilter(
        ProductLegalEntityScopeRolloutState requested,
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
            { nameof(ProductLegalEntityScopeRolloutState.Mode), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.Mode)] },
            { nameof(ProductLegalEntityScopeRolloutState.UpdatedAt), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.UpdatedAt)] },
            { nameof(ProductLegalEntityScopeRolloutState.Version), requested.Version },
            { nameof(ProductLegalEntityScopeRolloutState.LastInventorySnapshot), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastInventorySnapshot)] },
            { nameof(ProductLegalEntityScopeRolloutState.LastTransitionCommandId), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastTransitionCommandId)] },
            { nameof(ProductLegalEntityScopeRolloutState.LastTransitionActorId), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastTransitionActorId)] },
            { nameof(ProductLegalEntityScopeRolloutState.LastTransitionAction), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastTransitionAction)] },
            { nameof(ProductLegalEntityScopeRolloutState.LastTransitionReasonCode), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastTransitionReasonCode)] },
            { nameof(ProductLegalEntityScopeRolloutState.LastTransitionEvidenceHash), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastTransitionEvidenceHash)] },
            { nameof(ProductLegalEntityScopeRolloutState.LastTransitionIntentId), requestedDocument[nameof(ProductLegalEntityScopeRolloutState.LastTransitionIntentId)] },
            { nameof(ProductLegalEntityScopeRolloutState.ActiveFence), BsonNull.Value },
            { nameof(ProductLegalEntityScopeRolloutState.AuditIntents), projectedIntents }
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
        return new BsonDocumentFilterDefinition<ProductLegalEntityScopeRolloutState>(expression);
    }

    private static void EnsureProjectedDocumentWithinBudget(
        ProductLegalEntityScopeRolloutState persisted,
        ProductLegalEntityScopeRolloutState requested,
        IReadOnlyCollection<LocalAuditIntent> newAuditIntents)
    {
        var projected = persisted.ToBsonDocument();
        var requestedDocument = requested.ToBsonDocument();
        projected[nameof(ProductLegalEntityScopeRolloutState.Mode)] =
            requestedDocument[nameof(ProductLegalEntityScopeRolloutState.Mode)];
        projected[nameof(ProductLegalEntityScopeRolloutState.UpdatedAt)] =
            requestedDocument[nameof(ProductLegalEntityScopeRolloutState.UpdatedAt)];
        projected[nameof(ProductLegalEntityScopeRolloutState.Version)] = requested.Version;
        foreach (var field in new[]
        {
            nameof(ProductLegalEntityScopeRolloutState.LastInventorySnapshot),
            nameof(ProductLegalEntityScopeRolloutState.LastTransitionCommandId),
            nameof(ProductLegalEntityScopeRolloutState.LastTransitionActorId),
            nameof(ProductLegalEntityScopeRolloutState.LastTransitionAction),
            nameof(ProductLegalEntityScopeRolloutState.LastTransitionReasonCode),
            nameof(ProductLegalEntityScopeRolloutState.LastTransitionEvidenceHash)
            , nameof(ProductLegalEntityScopeRolloutState.LastTransitionIntentId)
        }) projected[field] = requestedDocument[field];
        projected[nameof(ProductLegalEntityScopeRolloutState.ActiveFence)] = BsonNull.Value;
        var intents = projected[nameof(ProductLegalEntityScopeRolloutState.AuditIntents)].AsBsonArray;
        foreach (var intent in newAuditIntents)
        {
            intents.Add(intent.ToBsonDocument());
        }

        EnsureCompleteDocumentWithinBudget(projected, reserveAuditLifecycleHeadroom: true);
    }

    private static void EnsureCompleteDocumentWithinBudget(
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
        _states.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductLegalEntityScopeRolloutState>(
                Builders<ProductLegalEntityScopeRolloutState>.IndexKeys.Ascending(state => state.TenantId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_legal_entity_scope_rollout_states_tenant"
                }),
            new CreateIndexModel<ProductLegalEntityScopeRolloutState>(
                Builders<ProductLegalEntityScopeRolloutState>.IndexKeys
                    .Ascending(state => state.TenantId)
                    .Ascending(state => state.CreationCommandId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_legal_entity_scope_rollout_states_tenant_command"
                })
        ]);
    }

    private FilterDefinition<ProductLegalEntityScopeRolloutState> TenantIncludingDeletedFilter =>
        Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(state => state.TenantId, _tenantId);

    private FilterDefinition<ProductLegalEntityScopeRolloutState> ActiveTenantFilter =>
        TenantIncludingDeletedFilter
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(state => state.IsDeleted, false);

    private static void EnsureIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identity must be non-empty.", parameterName);
        }
    }
}
