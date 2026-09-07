using System.Security.Cryptography;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class AuditIntentDeliveryRepository : IAuditIntentDeliveryRepository
{
    private const string CodeReservationCollectionName = "mdm_code_reservations";
    private const string GlobalProductCollectionName = "mdm_global_products";
    private const string ProductDefinitionRevisionCollectionName = "mdm_product_definition_revisions";
    private const string GskuCollectionName = "mdm_gskus";
    private const string FinishedGoodCollectionName = "mdm_finished_goods";
    private const string LskuCollectionName = "mdm_lskus";
    private const string ProductLegalEntityScopePolicyCollectionName = "mdm_product_legal_entity_scope_policies";
    private const string ProductLegalEntityScopeRolloutStateCollectionName = "mdm_product_legal_entity_scope_rollout_states";
    private readonly IMongoCollection<CodeReservation> _codeReservations;
    private readonly IMongoCollection<GlobalProduct> _globalProducts;
    private readonly IMongoCollection<ProductDefinitionRevision> _productDefinitionRevisions;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<ProductLegalEntityScopePolicy> _productLegalEntityScopePolicies;
    private readonly IMongoCollection<ProductLegalEntityScopeRolloutState> _productLegalEntityScopeRolloutStates;
    private readonly IAuditIntentTemporalMigrationRepository _temporalMigrationRepository;
    private readonly Guid _tenantId;
    private readonly TimeProvider _timeProvider;

    public AuditIntentDeliveryRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
        : this(
            database,
            tenantContext,
            timeProvider,
            new AuditIntentTemporalMigrationRepository(database, timeProvider))
    {
    }

    public AuditIntentDeliveryRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        IAuditIntentTemporalMigrationRepository temporalMigrationRepository)
    {
        _codeReservations = database.GetCollection<CodeReservation>(CodeReservationCollectionName);
        _globalProducts = database.GetCollection<GlobalProduct>(GlobalProductCollectionName);
        _productDefinitionRevisions = database.GetCollection<ProductDefinitionRevision>(ProductDefinitionRevisionCollectionName);
        _gskus = database.GetCollection<Gsku>(GskuCollectionName);
        _finishedGoods = database.GetCollection<FinishedGood>(FinishedGoodCollectionName);
        _lskus = database.GetCollection<Lsku>(LskuCollectionName);
        _productLegalEntityScopePolicies = database.GetCollection<ProductLegalEntityScopePolicy>(
            ProductLegalEntityScopePolicyCollectionName);
        _productLegalEntityScopeRolloutStates = database.GetCollection<ProductLegalEntityScopeRolloutState>(
            ProductLegalEntityScopeRolloutStateCollectionName);
        _temporalMigrationRepository = temporalMigrationRepository
            ?? throw new ArgumentNullException(nameof(temporalMigrationRepository));
        _tenantId = tenantContext.TenantId;
        _timeProvider = timeProvider;
        EnsureIndexes();
    }

    public async Task<IReadOnlyList<AuditIntentWorkItem>> DiscoverEligibleAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var now = _timeProvider.GetUtcNow();
        var scalarCutover = await IsScalarCutoverActiveAsync(cancellationToken);
        if (scalarCutover)
        {
            var scalarItems = await Task.WhenAll(
                FindEligibleScalarWorkItemsAsync(_codeReservations, AuditAggregateType.CodeReservation, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_globalProducts, AuditAggregateType.GlobalProduct, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_productDefinitionRevisions, AuditAggregateType.ProductDefinitionRevision, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_gskus, AuditAggregateType.Gsku, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_finishedGoods, AuditAggregateType.FinishedGood, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_lskus, AuditAggregateType.Lsku, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_productLegalEntityScopePolicies, AuditAggregateType.ProductLegalEntityScopePolicy, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_productLegalEntityScopeRolloutStates, AuditAggregateType.ProductLegalEntityScopeRolloutState, now, limit, cancellationToken));
            return scalarItems.SelectMany(items => items)
                .OrderBy(item => item.TimestampUtc.UtcTicks)
                .ThenBy(item => item.Locator.IntentId.ToString("N"), StringComparer.Ordinal)
                .Take(limit)
                .ToArray();
        }

        var codeReservations = await FindEligibleAggregatesAsync(
            _codeReservations,
            AuditAggregateType.CodeReservation,
            now,
            limit,
            false,
            cancellationToken);
        var globalProducts = await FindEligibleAggregatesAsync(
            _globalProducts,
            AuditAggregateType.GlobalProduct,
            now,
            limit,
            false,
            cancellationToken);
        var productDefinitionRevisions = await FindEligibleAggregatesAsync(
            _productDefinitionRevisions,
            AuditAggregateType.ProductDefinitionRevision,
            now,
            limit,
            false,
            cancellationToken);
        var gskus = await FindEligibleAggregatesAsync(
            _gskus,
            AuditAggregateType.Gsku,
            now,
            limit,
            false,
            cancellationToken);
        var finishedGoods = await FindEligibleAggregatesAsync(
            _finishedGoods,
            AuditAggregateType.FinishedGood,
            now,
            limit,
            false,
            cancellationToken);
        var lskus = await FindEligibleAggregatesAsync(
            _lskus,
            AuditAggregateType.Lsku,
            now,
            limit,
            false,
            cancellationToken);
        var productLegalEntityScopePolicies = await FindEligibleAggregatesAsync(
            _productLegalEntityScopePolicies,
            AuditAggregateType.ProductLegalEntityScopePolicy,
            now,
            limit,
            false,
            cancellationToken);
        var productLegalEntityScopeRolloutStates = await FindEligibleAggregatesAsync(
            _productLegalEntityScopeRolloutStates,
            AuditAggregateType.ProductLegalEntityScopeRolloutState,
            now,
            limit,
            false,
            cancellationToken);

        return codeReservations
            .SelectMany(aggregate => ToWorkItems(aggregate, AuditAggregateType.CodeReservation, now))
            .Concat(globalProducts.SelectMany(aggregate => ToWorkItems(aggregate, AuditAggregateType.GlobalProduct, now)))
            .Concat(productDefinitionRevisions.SelectMany(aggregate =>
                ToWorkItems(aggregate, AuditAggregateType.ProductDefinitionRevision, now)))
            .Concat(gskus.SelectMany(aggregate => ToWorkItems(aggregate, AuditAggregateType.Gsku, now)))
            .Concat(finishedGoods.SelectMany(aggregate =>
                ToWorkItems(aggregate, AuditAggregateType.FinishedGood, now)))
            .Concat(lskus.SelectMany(aggregate =>
                ToWorkItems(aggregate, AuditAggregateType.Lsku, now)))
            .Concat(productLegalEntityScopePolicies.SelectMany(aggregate =>
                ToWorkItems(aggregate, AuditAggregateType.ProductLegalEntityScopePolicy, now)))
            .Concat(productLegalEntityScopeRolloutStates.SelectMany(aggregate =>
                ToWorkItems(aggregate, AuditAggregateType.ProductLegalEntityScopeRolloutState, now)))
            .OrderBy(item => item.TimestampUtc.UtcTicks)
            .ThenBy(item => item.Locator.IntentId.ToString("N"), StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    public async Task<AuditIntentClaim?> TryClaimAsync(
        AuditIntentLocator locator,
        long expectedClaimGeneration,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentTenant(locator) || expectedClaimGeneration < 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(leaseOwner))
        {
            throw new ArgumentException("Lease owner is required.", nameof(leaseOwner));
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        var now = _timeProvider.GetUtcNow();
        var scalarCutover = await IsScalarCutoverActiveAsync(cancellationToken);
        return await (locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => TryClaimInCollectionAsync(
                _codeReservations,
                locator,
                expectedClaimGeneration,
                leaseOwner,
                leaseDuration,
                now,
                scalarCutover,
                cancellationToken),
            AuditAggregateType.GlobalProduct => TryClaimInCollectionAsync(
                _globalProducts,
                locator,
                expectedClaimGeneration,
                leaseOwner,
                leaseDuration,
                now,
                scalarCutover,
                cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => TryClaimInCollectionAsync(
                _productDefinitionRevisions, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, scalarCutover, cancellationToken),
            AuditAggregateType.Gsku => TryClaimInCollectionAsync(
                _gskus, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, scalarCutover, cancellationToken),
            AuditAggregateType.FinishedGood => TryClaimInCollectionAsync(
                _finishedGoods, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, scalarCutover, cancellationToken),
            AuditAggregateType.Lsku => TryClaimInCollectionAsync(
                _lskus, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, scalarCutover, cancellationToken),
            AuditAggregateType.ProductLegalEntityScopePolicy => TryClaimInCollectionAsync(
                _productLegalEntityScopePolicies,
                locator,
                expectedClaimGeneration,
                leaseOwner,
                leaseDuration,
                now,
                scalarCutover,
                cancellationToken),
            AuditAggregateType.ProductLegalEntityScopeRolloutState => TryClaimInCollectionAsync(
                _productLegalEntityScopeRolloutStates,
                locator,
                expectedClaimGeneration,
                leaseOwner,
                leaseDuration,
                now,
                scalarCutover,
                cancellationToken),
            _ => Task.FromResult<AuditIntentClaim?>(null)
        });
    }

    public async Task<AuditIntentClaimedPayload?> ReadClaimedPayloadAsync(
        AuditIntentClaim claim,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (!IsCurrentTenant(claim.Locator) || !await IsScalarCutoverActiveAsync(cancellationToken)) return null;
        var now = _timeProvider.GetUtcNow();
        return await (claim.Locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => ReadClaimedPayloadFromCollectionAsync(_codeReservations, claim, now, cancellationToken),
            AuditAggregateType.GlobalProduct => ReadClaimedPayloadFromCollectionAsync(_globalProducts, claim, now, cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => ReadClaimedPayloadFromCollectionAsync(_productDefinitionRevisions, claim, now, cancellationToken),
            AuditAggregateType.Gsku => ReadClaimedPayloadFromCollectionAsync(_gskus, claim, now, cancellationToken),
            AuditAggregateType.FinishedGood => ReadClaimedPayloadFromCollectionAsync(_finishedGoods, claim, now, cancellationToken),
            AuditAggregateType.Lsku => ReadClaimedPayloadFromCollectionAsync(_lskus, claim, now, cancellationToken),
            AuditAggregateType.ProductLegalEntityScopePolicy => ReadClaimedPayloadFromCollectionAsync(_productLegalEntityScopePolicies, claim, now, cancellationToken),
            AuditAggregateType.ProductLegalEntityScopeRolloutState => ReadClaimedPayloadFromCollectionAsync(_productLegalEntityScopeRolloutStates, claim, now, cancellationToken),
            _ => Task.FromResult<AuditIntentClaimedPayload?>(null)
        });
    }

    public Task<bool> MarkRetryableFailureAsync(
        AuditIntentClaim claim,
        TimeSpan retryDelay,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (retryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        var now = _timeProvider.GetUtcNow();
        var nextRetryAt = now.Add(retryDelay);
        ValidateReason(reason);
        var update = Builders<CodeReservation>.Update
            .Set("AuditIntents.$.DeliveryState", AuditIntentDeliveryState.Pending)
            .Set("AuditIntents.$.NextRetryAt", nextRetryAt)
            .Set("AuditIntents.$.FailureClass", AuditIntentFailureClass.Retryable)
            .Set("AuditIntents.$.FailureReason", reason.Trim())
            .Set("AuditIntents.$.LastError", reason.Trim())
            .Set("AuditIntents.$.LeaseOwner", (string?)null)
            .Set("AuditIntents.$.ClaimToken", (string?)null)
            .Set("AuditIntents.$.LeaseUntil", (DateTimeOffset?)null);
        var currentUpdate = update
            .Set("AuditIntents.$.NextRetryAtUtcTicksV1", nextRetryAt.UtcTicks)
            .Set("AuditIntents.$.LeaseUntilUtcTicksV1", (long?)null)
            .Set("AuditIntents.$.TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion);
        return UpdateClaimedIntentAsync(claim, now, update, currentUpdate, cancellationToken);
    }

    public Task<bool> MarkDeadLetterAsync(
        AuditIntentClaim claim,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        ValidateReason(reason);
        var update = Builders<CodeReservation>.Update
            .Set("AuditIntents.$.DeliveryState", AuditIntentDeliveryState.DeadLetter)
            .Set("AuditIntents.$.DeadLetteredAt", now)
            .Set("AuditIntents.$.FailureClass", AuditIntentFailureClass.Terminal)
            .Set("AuditIntents.$.FailureReason", reason.Trim())
            .Set("AuditIntents.$.LastError", reason.Trim())
            .Set("AuditIntents.$.NextRetryAt", (DateTimeOffset?)null)
            .Set("AuditIntents.$.LeaseOwner", (string?)null)
            .Set("AuditIntents.$.ClaimToken", (string?)null)
            .Set("AuditIntents.$.LeaseUntil", (DateTimeOffset?)null);
        var currentUpdate = update
            .Set("AuditIntents.$.NextRetryAtUtcTicksV1", (long?)null)
            .Set("AuditIntents.$.LeaseUntilUtcTicksV1", (long?)null)
            .Set("AuditIntents.$.TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion);
        return UpdateClaimedIntentAsync(claim, now, update, currentUpdate, cancellationToken);
    }

    public Task<bool> MarkDeliveredAsync(
        AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        ValidateAcknowledgement(claim, acknowledgement);
        var update = Builders<CodeReservation>.Update
            .Set("AuditIntents.$.DeliveryState", AuditIntentDeliveryState.Delivered)
            .Set("AuditIntents.$.DeliveredAt", now)
            .Set("AuditIntents.$.CentralAcknowledgement", acknowledgement.CentralAcknowledgement.Trim())
            .Set("AuditIntents.$.CentralIdempotencyKey", acknowledgement.CentralIdempotencyKey.Trim())
            .Set("AuditIntents.$.AcknowledgedContractVersion", acknowledgement.ContractVersion.Trim())
            .Set("AuditIntents.$.AcknowledgedAt", acknowledgement.AcceptedAt)
            .Set("AuditIntents.$.FailureClass", AuditIntentFailureClass.None)
            .Set("AuditIntents.$.FailureReason", (string?)null)
            .Set("AuditIntents.$.LastError", (string?)null)
            .Set("AuditIntents.$.NextRetryAt", (DateTimeOffset?)null)
            .Set("AuditIntents.$.LeaseOwner", (string?)null)
            .Set("AuditIntents.$.LeaseUntil", (DateTimeOffset?)null);
        var currentUpdate = update
            .Set("AuditIntents.$.NextRetryAtUtcTicksV1", (long?)null)
            .Set("AuditIntents.$.LeaseUntilUtcTicksV1", (long?)null)
            .Set("AuditIntents.$.TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion);
        return UpdateClaimedIntentAsync(claim, now, update, currentUpdate, cancellationToken);
    }

    public Task<bool> AcknowledgeAndCompactAsync(
        AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement,
        string compactReceiptReference,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentTenant(claim.Locator) || string.IsNullOrWhiteSpace(compactReceiptReference))
        {
            return Task.FromResult(false);
        }

        ValidateAcknowledgement(claim, acknowledgement);
        var now = _timeProvider.GetUtcNow();
        return claim.Locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => AcknowledgeAndCompactInCollectionAsync(
                _codeReservations, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.GlobalProduct => AcknowledgeAndCompactInCollectionAsync(
                _globalProducts, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => AcknowledgeAndCompactInCollectionAsync(
                _productDefinitionRevisions, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.Gsku => AcknowledgeAndCompactInCollectionAsync(
                _gskus, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.FinishedGood => AcknowledgeAndCompactInCollectionAsync(
                _finishedGoods, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.Lsku => AcknowledgeAndCompactInCollectionAsync(
                _lskus, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.ProductLegalEntityScopePolicy => AcknowledgeAndCompactInCollectionAsync(
                _productLegalEntityScopePolicies,
                claim,
                acknowledgement,
                compactReceiptReference,
                now,
                cancellationToken),
            AuditAggregateType.ProductLegalEntityScopeRolloutState => AcknowledgeAndCompactInCollectionAsync(
                _productLegalEntityScopeRolloutStates,
                claim,
                acknowledgement,
                compactReceiptReference,
                now,
                cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    public Task<bool> CompactDeliveredAsync(
        AuditIntentClaim claim,
        string compactReceiptReference,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentTenant(claim.Locator) || string.IsNullOrWhiteSpace(compactReceiptReference))
        {
            return Task.FromResult(false);
        }

        var now = _timeProvider.GetUtcNow();
        return claim.Locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => CompactInCollectionAsync(
                _codeReservations,
                claim,
                compactReceiptReference,
                now,
                cancellationToken),
            AuditAggregateType.GlobalProduct => CompactInCollectionAsync(
                _globalProducts,
                claim,
                compactReceiptReference,
                now,
                cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => CompactInCollectionAsync(
                _productDefinitionRevisions, claim, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.Gsku => CompactInCollectionAsync(
                _gskus, claim, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.FinishedGood => CompactInCollectionAsync(
                _finishedGoods, claim, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.Lsku => CompactInCollectionAsync(
                _lskus, claim, compactReceiptReference, now, cancellationToken),
            AuditAggregateType.ProductLegalEntityScopePolicy => CompactInCollectionAsync(
                _productLegalEntityScopePolicies,
                claim,
                compactReceiptReference,
                now,
                cancellationToken),
            AuditAggregateType.ProductLegalEntityScopeRolloutState => CompactInCollectionAsync(
                _productLegalEntityScopeRolloutStates,
                claim,
                compactReceiptReference,
                now,
                cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    private async Task<AuditIntentClaim?> TryClaimInCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentLocator locator,
        long expectedClaimGeneration,
        string leaseOwner,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        bool scalarCutover,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(locator)).FirstOrDefaultAsync(cancellationToken);
        var matches = aggregate?.AuditIntents.Where(intent =>
            IsIntentBoundToParent(intent, locator.AggregateType, locator.AggregateId, locator.TenantId)
            && intent.IntentId == locator.IntentId
            && intent.ClaimGeneration == expectedClaimGeneration).Take(2).ToArray() ?? [];
        if (matches.Length != 1)
        {
            return null;
        }
        var storageKind = AuditIntentTemporalStorage.Validate(matches[0]);
        var rawStorageKind = await ReadRawStorageKindAsync(collection, locator, cancellationToken);
        if (!rawStorageKind.HasValue)
        {
            return null;
        }
        if (rawStorageKind.Value != storageKind)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_TYPED_RAW_MISMATCH");
        }
        if (scalarCutover && storageKind != AuditIntentTemporalStorageKind.Current)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_LEGACY_AFTER_CUTOVER");
        }
        var useScalar = storageKind == AuditIntentTemporalStorageKind.Current;
        var eligible = BoundIntentFilter(locator)
            & EligibleIntentFilter(now, useScalar)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimGeneration, expectedClaimGeneration);
        var filter = TenantAggregateFilter<TEntity>(locator)
            & Builders<TEntity>.Filter.ElemMatch(aggregate => aggregate.AuditIntents, eligible);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var leaseUntil = now.Add(leaseDuration);
        var update = Builders<TEntity>.Update
            .Set("AuditIntents.$.DeliveryState", AuditIntentDeliveryState.Processing)
            .Set("AuditIntents.$.LeaseOwner", leaseOwner.Trim())
            .Set("AuditIntents.$.ClaimToken", token)
            .Inc("AuditIntents.$.ClaimGeneration", 1L)
            .Set("AuditIntents.$.ClaimedAt", now)
            .Set("AuditIntents.$.LeaseUntil", leaseUntil)
            .Set("AuditIntents.$.LastAttemptAt", now)
            .Set("AuditIntents.$.NextRetryAt", (DateTimeOffset?)null)
            .Inc("AuditIntents.$.AttemptCount", 1);
        if (useScalar)
        {
            update = update
                .Set("AuditIntents.$.LeaseUntilUtcTicksV1", leaseUntil.UtcTicks)
                .Set("AuditIntents.$.NextRetryAtUtcTicksV1", (long?)null)
                .Set("AuditIntents.$.TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion);
            eligible &= Builders<LocalAuditIntent>.Filter.Eq(
                intent => intent.TimestampUtcTicksV1,
                matches[0].TimestampUtcTicksV1)
                & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.NextRetryAtUtcTicksV1, matches[0].NextRetryAtUtcTicksV1)
                & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.LeaseUntilUtcTicksV1, matches[0].LeaseUntilUtcTicksV1);
        }
        else
        {
            eligible &= Builders<LocalAuditIntent>.Filter.Exists(intent => intent.TemporalStorageVersion, false);
        }

        filter &= CompleteDocumentFitsAfterIntentUpdateFilter(update, locator.IntentId);

        var updated = await collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<TEntity> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        var intent = updated?.AuditIntents.SingleOrDefault(candidate =>
            IsIntentBoundToParent(candidate, locator.AggregateType, locator.AggregateId, locator.TenantId)
            && candidate.IntentId == locator.IntentId);
        return intent is null
            ? null
            : new AuditIntentClaim(
                locator,
                token,
                leaseOwner.Trim(),
                intent.ClaimGeneration,
                now,
                leaseUntil,
                intent.AttemptCount);
    }

    private async Task<AuditIntentClaimedPayload?> ReadClaimedPayloadFromCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection, AuditIntentClaim claim, DateTimeOffset now, CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(claim.Locator)).FirstOrDefaultAsync(cancellationToken);
        var matches = aggregate?.AuditIntents.Where(intent =>
            IsIntentBoundToParent(intent, claim.Locator.AggregateType, claim.Locator.AggregateId, claim.Locator.TenantId)
            && intent.IntentId == claim.Locator.IntentId && intent.DeliveryState == AuditIntentDeliveryState.Processing
            && string.Equals(intent.ClaimToken, claim.ClaimToken, StringComparison.Ordinal)
            && intent.ClaimGeneration == claim.ClaimGeneration && intent.LeaseUntil > now).Take(2).ToArray() ?? [];
        if (matches.Length != 1) return null;
        var intent = matches[0];
        if (AuditIntentTemporalStorage.Validate(intent) != AuditIntentTemporalStorageKind.Current
            || await ReadRawStorageKindAsync(collection, claim.Locator, cancellationToken) != AuditIntentTemporalStorageKind.Current)
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CUTOVER_PAYLOAD_INVALID");
        if (!Guid.TryParseExact(intent.CorrelationId, "D", out var correlationId) || correlationId == Guid.Empty
            || string.IsNullOrWhiteSpace(intent.ContractVersion))
            throw new InvalidOperationException("AUDIT_INTENT_CLAIMED_PAYLOAD_INVALID");
        return new AuditIntentClaimedPayload(intent.SourceService, intent.ContractVersion, intent.IntentId, intent.TenantId,
            intent.AggregateType, intent.AggregateId, intent.PreVersion, intent.PostVersion, intent.Operation, intent.ActorId,
            correlationId, intent.CausationId, intent.CommandId, intent.Sequence, intent.TimestampUtc, intent.EvidenceHash,
            intent.SnapshotReference, intent.IdempotencyKey);
    }

    private Task<bool> UpdateClaimedIntentAsync(
        AuditIntentClaim claim,
        DateTimeOffset now,
        UpdateDefinition<CodeReservation> legacyUpdate,
        UpdateDefinition<CodeReservation> currentUpdate,
        CancellationToken cancellationToken)
    {
        if (!IsCurrentTenant(claim.Locator))
        {
            return Task.FromResult(false);
        }

        return claim.Locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => UpdateClaimedInCollectionAsync(
                _codeReservations,
                claim,
                now,
                legacyUpdate,
                currentUpdate,
                cancellationToken),
            AuditAggregateType.GlobalProduct => UpdateClaimedInCollectionAsync(
                _globalProducts,
                claim,
                now,
                ConvertUpdate<CodeReservation, GlobalProduct>(legacyUpdate),
                ConvertUpdate<CodeReservation, GlobalProduct>(currentUpdate),
                cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => UpdateClaimedInCollectionAsync(
                _productDefinitionRevisions,
                claim,
                now,
                ConvertUpdate<CodeReservation, ProductDefinitionRevision>(legacyUpdate),
                ConvertUpdate<CodeReservation, ProductDefinitionRevision>(currentUpdate),
                cancellationToken),
            AuditAggregateType.Gsku => UpdateClaimedInCollectionAsync(
                _gskus,
                claim,
                now,
                ConvertUpdate<CodeReservation, Gsku>(legacyUpdate),
                ConvertUpdate<CodeReservation, Gsku>(currentUpdate),
                cancellationToken),
            AuditAggregateType.FinishedGood => UpdateClaimedInCollectionAsync(
                _finishedGoods,
                claim,
                now,
                ConvertUpdate<CodeReservation, FinishedGood>(legacyUpdate),
                ConvertUpdate<CodeReservation, FinishedGood>(currentUpdate),
                cancellationToken),
            AuditAggregateType.Lsku => UpdateClaimedInCollectionAsync(
                _lskus,
                claim,
                now,
                ConvertUpdate<CodeReservation, Lsku>(legacyUpdate),
                ConvertUpdate<CodeReservation, Lsku>(currentUpdate),
                cancellationToken),
            AuditAggregateType.ProductLegalEntityScopePolicy => UpdateClaimedInCollectionAsync(
                _productLegalEntityScopePolicies,
                claim,
                now,
                ConvertUpdate<CodeReservation, ProductLegalEntityScopePolicy>(legacyUpdate),
                ConvertUpdate<CodeReservation, ProductLegalEntityScopePolicy>(currentUpdate),
                cancellationToken),
            AuditAggregateType.ProductLegalEntityScopeRolloutState => UpdateClaimedInCollectionAsync(
                _productLegalEntityScopeRolloutStates,
                claim,
                now,
                ConvertUpdate<CodeReservation, ProductLegalEntityScopeRolloutState>(legacyUpdate),
                ConvertUpdate<CodeReservation, ProductLegalEntityScopeRolloutState>(currentUpdate),
                cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    private async Task<bool> UpdateClaimedInCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentClaim claim,
        DateTimeOffset now,
        UpdateDefinition<TEntity> legacyUpdate,
        UpdateDefinition<TEntity> currentUpdate,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(claim.Locator))
            .FirstOrDefaultAsync(cancellationToken);
        var matches = aggregate?.AuditIntents.Where(intent =>
                IsIntentBoundToParent(intent, claim.Locator.AggregateType, claim.Locator.AggregateId, claim.Locator.TenantId)
                && intent.IntentId == claim.Locator.IntentId
                && intent.ClaimToken == claim.ClaimToken
                && intent.ClaimGeneration == claim.ClaimGeneration)
            .Take(2)
            .ToArray() ?? [];
        if (matches.Length != 1)
        {
            return false;
        }

        var storageKind = AuditIntentTemporalStorage.Validate(matches[0]);
        var rawStorageKind = await ReadRawStorageKindAsync(collection, claim.Locator, cancellationToken);
        if (!rawStorageKind.HasValue)
        {
            return false;
        }
        if (rawStorageKind.Value != storageKind)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_TYPED_RAW_MISMATCH");
        }
        var update = storageKind == AuditIntentTemporalStorageKind.Current ? currentUpdate : legacyUpdate;
        var intentFilter = ClaimedIntentFilter(claim, now, storageKind, matches[0]);
        var filter = TenantAggregateFilter<TEntity>(claim.Locator)
            & Builders<TEntity>.Filter.ElemMatch(aggregate => aggregate.AuditIntents, intentFilter);
        filter &= CompleteDocumentFitsAfterIntentUpdateFilter(update, claim.Locator.IntentId);
        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private async Task<bool> CompactInCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentClaim claim,
        string compactReceiptReference,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var existingReplay = await GetCompactionReplayResultAsync(
            collection,
            claim,
            compactReceiptReference,
            cancellationToken);
        if (existingReplay.HasValue)
        {
            return existingReplay.Value;
        }

        var deliveredIntentFilter = DeliveredIntentFilter(claim);
        var aggregateFilter = TenantAggregateFilter<TEntity>(claim.Locator)
            & Builders<TEntity>.Filter.ElemMatch(aggregate => aggregate.AuditIntents, deliveredIntentFilter);
        var aggregate = await collection.Find(aggregateFilter).FirstOrDefaultAsync(cancellationToken);
        var intent = aggregate?.AuditIntents.SingleOrDefault(candidate =>
            IsIntentBoundToParent(
                candidate,
                claim.Locator.AggregateType,
                claim.Locator.AggregateId,
                claim.Locator.TenantId)
            && candidate.IntentId == claim.Locator.IntentId);
        if (intent?.CentralAcknowledgement is null
            || intent.CentralIdempotencyKey is null
            || intent.AcknowledgedContractVersion is null
            || intent.AcknowledgedAt is null
            || intent.DeliveredAt is null)
        {
            return false;
        }

        var receipt = new LocalAuditIntentReceipt
        {
            SourceService = intent.SourceService,
            IntentId = intent.IntentId,
            TenantId = intent.TenantId,
            IdempotencyKey = intent.IdempotencyKey,
            CentralAcknowledgement = intent.CentralAcknowledgement,
            CentralIdempotencyKey = intent.CentralIdempotencyKey,
            ContractVersion = intent.AcknowledgedContractVersion,
            AcknowledgedAt = intent.AcknowledgedAt.Value,
            DeliveredAt = intent.DeliveredAt.Value,
            CompactedAt = now,
            CompactReceiptReference = compactReceiptReference.Trim(),
            EvidenceHash = intent.EvidenceHash
        };
        var update = Builders<TEntity>.Update
            .PullFilter(aggregateItem => aggregateItem.AuditIntents, deliveredIntentFilter)
            .Push(aggregateItem => aggregateItem.AuditIntentReceipts, receipt);
        aggregateFilter &= CompleteDocumentFitsAfterCompactionFilter<TEntity>(claim.Locator.IntentId, receipt);
        var result = await collection.UpdateOneAsync(aggregateFilter, update, cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return true;
        }

        return await GetCompactionReplayResultAsync(
            collection,
            claim,
            compactReceiptReference,
            cancellationToken) == true;
    }

    private async Task<bool> AcknowledgeAndCompactInCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement,
        string compactReceiptReference,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var normalizedReceiptReference = compactReceiptReference.Trim();
        var replay = await GetAcknowledgementCompactionReplayResultAsync(
            collection,
            claim,
            acknowledgement,
            normalizedReceiptReference,
            cancellationToken);
        if (replay.HasValue)
        {
            return replay.Value;
        }

        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(claim.Locator))
            .FirstOrDefaultAsync(cancellationToken);
        var matchingIntents = aggregate?.AuditIntents.Where(candidate =>
            IsIntentBoundToParent(
                candidate,
                claim.Locator.AggregateType,
                claim.Locator.AggregateId,
                claim.Locator.TenantId)
            && candidate.IntentId == claim.Locator.IntentId
            && candidate.DeliveryState == AuditIntentDeliveryState.Processing
            && string.Equals(candidate.ClaimToken, claim.ClaimToken, StringComparison.Ordinal)
            && candidate.ClaimGeneration == claim.ClaimGeneration
            && candidate.LeaseUntil > now).Take(2).ToArray() ?? [];
        if (matchingIntents.Length != 1)
        {
            return await GetAcknowledgementCompactionReplayResultAsync(
                collection,
                claim,
                acknowledgement,
                normalizedReceiptReference,
                cancellationToken) == true;
        }

        var storageKind = AuditIntentTemporalStorage.Validate(matchingIntents[0]);
        var rawStorageKind = await ReadRawStorageKindAsync(collection, claim.Locator, cancellationToken);
        if (!rawStorageKind.HasValue)
        {
            return await GetAcknowledgementCompactionReplayResultAsync(
                collection,
                claim,
                acknowledgement,
                normalizedReceiptReference,
                cancellationToken) == true;
        }
        if (rawStorageKind.Value != storageKind)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_TYPED_RAW_MISMATCH");
        }
        var claimedIntentFilter = ClaimedIntentFilter(claim, now, storageKind, matchingIntents[0]);
        var aggregateFilter = TenantAggregateFilter<TEntity>(claim.Locator)
            & Builders<TEntity>.Filter.ElemMatch(aggregateItem => aggregateItem.AuditIntents, claimedIntentFilter)
            & ExactlyOneIntentIdentityFilter<TEntity>(claim.Locator.IntentId)
            & Builders<TEntity>.Filter.Not(Builders<TEntity>.Filter.ElemMatch(
                aggregateItem => aggregateItem.AuditIntentReceipts,
                receipt => receipt.IntentId == claim.Locator.IntentId));

        var intent = matchingIntents[0];

        var receipt = new LocalAuditIntentReceipt
        {
            SourceService = intent.SourceService,
            IntentId = intent.IntentId,
            TenantId = intent.TenantId,
            IdempotencyKey = intent.IdempotencyKey,
            CentralAcknowledgement = acknowledgement.CentralAcknowledgement.Trim(),
            CentralIdempotencyKey = acknowledgement.CentralIdempotencyKey.Trim(),
            ContractVersion = acknowledgement.ContractVersion.Trim(),
            AcknowledgedAt = acknowledgement.AcceptedAt,
            DeliveredAt = now,
            CompactedAt = now,
            CompactReceiptReference = normalizedReceiptReference,
            EvidenceHash = intent.EvidenceHash
        };
        aggregateFilter &= CompleteDocumentFitsAfterCompactionFilter<TEntity>(claim.Locator.IntentId, receipt);
        var update = Builders<TEntity>.Update
            .PullFilter(aggregateItem => aggregateItem.AuditIntents, claimedIntentFilter)
            .Push(aggregateItem => aggregateItem.AuditIntentReceipts, receipt);
        var result = await collection.UpdateOneAsync(aggregateFilter, update, cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return true;
        }

        return await GetAcknowledgementCompactionReplayResultAsync(
            collection,
            claim,
            acknowledgement,
            normalizedReceiptReference,
            cancellationToken) == true;
    }

    private async Task<bool?> GetAcknowledgementCompactionReplayResultAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement,
        string compactReceiptReference,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(claim.Locator))
            .FirstOrDefaultAsync(cancellationToken);
        if (aggregate is null)
        {
            return false;
        }

        var receipts = aggregate.AuditIntentReceipts
            .Where(receipt => receipt.IntentId == claim.Locator.IntentId)
            .ToArray();
        if (receipts.Length == 0)
        {
            return null;
        }

        if (receipts.Length != 1
            || aggregate.AuditIntents.Any(intent => intent.IntentId == claim.Locator.IntentId))
        {
            return false;
        }

        var receipt = receipts[0];
        return receipt.TenantId == claim.Locator.TenantId
               && string.Equals(receipt.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
               && string.Equals(receipt.CentralAcknowledgement, acknowledgement.CentralAcknowledgement.Trim(), StringComparison.Ordinal)
               && string.Equals(receipt.CentralIdempotencyKey, acknowledgement.CentralIdempotencyKey.Trim(), StringComparison.Ordinal)
               && string.Equals(receipt.ContractVersion, acknowledgement.ContractVersion.Trim(), StringComparison.Ordinal)
               && receipt.AcknowledgedAt == acknowledgement.AcceptedAt
               && string.Equals(receipt.CompactReceiptReference, compactReceiptReference, StringComparison.Ordinal);
    }

    private async Task<bool?> GetCompactionReplayResultAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentClaim claim,
        string compactReceiptReference,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(claim.Locator))
            .FirstOrDefaultAsync(cancellationToken);
        if (aggregate is null)
        {
            return false;
        }

        var receipts = aggregate.AuditIntentReceipts
            .Where(receipt => receipt.IntentId == claim.Locator.IntentId)
            .ToArray();
        if (receipts.Length == 0)
        {
            return null;
        }

        if (receipts.Length != 1
            || aggregate.AuditIntents.Any(intent => intent.IntentId == claim.Locator.IntentId))
        {
            return false;
        }

        var receipt = receipts[0];
        if (receipt.TenantId != claim.Locator.TenantId
            || !string.Equals(receipt.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(receipt.ContractVersion)
            || string.IsNullOrWhiteSpace(receipt.CentralIdempotencyKey))
        {
            return false;
        }

        var expectedCentralIdempotencyKey = AuditIntentContract.BuildCentralIdempotencyKey(
            claim.Locator.TenantId,
            claim.Locator.IntentId,
            receipt.ContractVersion);
        return string.Equals(
                   receipt.CentralIdempotencyKey,
                   expectedCentralIdempotencyKey,
                   StringComparison.Ordinal)
               && string.Equals(
                   receipt.CompactReceiptReference,
                   compactReceiptReference.Trim(),
                   StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<AuditIntentWorkItem>> FindEligibleScalarWorkItemsAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditAggregateType aggregateType,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var eligible = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument
            {
                { "AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.Pending },
                { "$or", new BsonArray
                    {
                        new BsonDocument("AuditIntents.NextRetryAtUtcTicksV1", BsonNull.Value),
                        new BsonDocument("AuditIntents.NextRetryAtUtcTicksV1", new BsonDocument("$lte", now.UtcTicks))
                    }
                }
            },
            new BsonDocument
            {
                { "AuditIntents.DeliveryState", (int)AuditIntentDeliveryState.Processing },
                { "AuditIntents.LeaseUntilUtcTicksV1", new BsonDocument("$lte", now.UtcTicks) }
            }
        });
        var exactMatch = new BsonDocument
        {
            { "TenantId", tenant },
            { "AuditIntents.TenantId", tenant },
            { "AuditIntents.AggregateType", (int)aggregateType },
            { "AuditIntents.SourceService", AuditIntentContract.SourceService },
            { "AuditIntents.TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion },
            { "$and", new BsonArray { eligible } }
        };
        var preEligible = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument
            {
                { "DeliveryState", (int)AuditIntentDeliveryState.Pending },
                { "$or", new BsonArray
                    {
                        new BsonDocument("NextRetryAtUtcTicksV1", BsonNull.Value),
                        new BsonDocument("NextRetryAtUtcTicksV1", new BsonDocument("$lte", now.UtcTicks))
                    }
                }
            },
            new BsonDocument
            {
                { "DeliveryState", (int)AuditIntentDeliveryState.Processing },
                { "LeaseUntilUtcTicksV1", new BsonDocument("$lte", now.UtcTicks) }
            }
        });
        var preMatch = new BsonDocument
        {
            { "TenantId", tenant },
            { "AuditIntents", new BsonDocument("$elemMatch", new BsonDocument
                {
                    { "TenantId", tenant },
                    { "AggregateType", (int)aggregateType },
                    { "SourceService", AuditIntentContract.SourceService },
                    { "TemporalStorageVersion", AuditIntentTemporalStorage.CurrentVersion },
                    { "$and", new BsonArray { preEligible } }
                })
            }
        };
        var pipeline = new BsonDocument[]
        {
            new("$match", preMatch),
            new("$unwind", "$AuditIntents"),
            new("$match", exactMatch),
            new("$sort", new BsonDocument
            {
                { "AuditIntents.TimestampUtcTicksV1", 1 },
                { "AuditIntents.IntentId", 1 }
            }),
            new("$limit", limit),
            new("$project", new BsonDocument
            {
                { "AggregateId", "$_id" },
                { "IsDeleted", 1 },
                { "Intent", "$AuditIntents" },
                { "_id", 0 }
            })
        };
        var rawCollection = collection.Database.GetCollection<BsonDocument>(collection.CollectionNamespace.CollectionName);
        var documents = await rawCollection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        var workItems = new List<AuditIntentWorkItem>(documents.Count);
        foreach (var document in documents)
        {
            var aggregateId = document["AggregateId"].AsGuid;
            var rawIntent = document["Intent"].AsBsonDocument;
            if (AuditIntentTemporalMigrationRepository.ValidateRawIntent(rawIntent)
                    != AuditIntentTemporalStorageKind.Current)
            {
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_SCALAR_DISCOVERY_INVALID");
            }
            var intent = BsonSerializer.Deserialize<LocalAuditIntent>(rawIntent);
            if (AuditIntentTemporalStorage.Validate(intent) != AuditIntentTemporalStorageKind.Current
                || !IsIntentBoundToParent(intent, aggregateType, aggregateId, _tenantId)
                || !IsEligible(intent, now))
            {
                throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_SCALAR_DISCOVERY_INVALID");
            }

            workItems.Add(new AuditIntentWorkItem(
                new AuditIntentLocator(_tenantId, aggregateType, aggregateId, intent.IntentId),
                intent.DeliveryState,
                intent.AttemptCount,
                intent.ClaimGeneration,
                intent.TimestampUtc,
                intent.NextRetryAt,
                intent.LeaseUntil,
                document.TryGetValue("IsDeleted", out var deleted) && deleted.IsBoolean && deleted.AsBoolean));
        }

        return workItems;
    }

    private async Task<IReadOnlyList<TEntity>> FindEligibleAggregatesAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditAggregateType aggregateType,
        DateTimeOffset now,
        int limit,
        bool scalarCutover,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var boundIntent = Builders<LocalAuditIntent>.Filter.Eq(intent => intent.TenantId, _tenantId)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.AggregateType, aggregateType)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.SourceService, AuditIntentContract.SourceService)
            & EligibleIntentFilter(now, scalarCutover);
        var filter = Builders<TEntity>.Filter.Eq(aggregate => aggregate.TenantId, _tenantId)
            & Builders<TEntity>.Filter.ElemMatch(aggregate => aggregate.AuditIntents, boundIntent);
        var query = collection.Find(filter);
        if (scalarCutover)
        {
            query = query.Sort(Builders<TEntity>.Sort
                .Ascending("AuditIntents.TimestampUtcTicksV1")
                .Ascending("AuditIntents.IntentId"));
        }
        var aggregates = await query.Limit(limit).ToListAsync(cancellationToken);
        await ValidateRawAggregatesAsync(collection, aggregates.Select(item => item.Id).ToArray(), cancellationToken);
        return aggregates;
    }

    private static async Task ValidateRawAggregatesAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        IReadOnlyCollection<Guid> aggregateIds,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        if (aggregateIds.Count == 0)
        {
            return;
        }
        var raw = collection.Database.GetCollection<BsonDocument>(collection.CollectionNamespace.CollectionName);
        var documents = await raw.Find(Builders<BsonDocument>.Filter.In("_id", aggregateIds))
            .ToListAsync(cancellationToken);
        foreach (var intent in documents.SelectMany(ReadRawIntents))
        {
            _ = AuditIntentTemporalMigrationRepository.ValidateRawIntent(intent);
        }
    }

    private static async Task<AuditIntentTemporalStorageKind?> ReadRawStorageKindAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentLocator locator,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var raw = collection.Database.GetCollection<BsonDocument>(collection.CollectionNamespace.CollectionName);
        var document = await raw.Find(Builders<BsonDocument>.Filter.Eq("_id", locator.AggregateId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_AGGREGATE_NOT_FOUND");
        var matches = ReadRawIntents(document)
            .Where(intent => intent.TryGetValue("IntentId", out var id) && id.IsGuid && id.AsGuid == locator.IntentId)
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            return null;
        }
        return AuditIntentTemporalMigrationRepository.ValidateRawIntent(matches[0]);
    }

    private static IEnumerable<BsonDocument> ReadRawIntents(BsonDocument document)
        => document.TryGetValue("AuditIntents", out var intents) && intents.IsBsonArray
            ? intents.AsBsonArray.Select(value => value.AsBsonDocument)
            : [];

    private IEnumerable<AuditIntentWorkItem> ToWorkItems<TEntity>(
        TEntity aggregate,
        AuditAggregateType aggregateType,
        DateTimeOffset now)
        where TEntity : EntityBase, IAuditIntentAggregate
        => aggregate.AuditIntents
            .Where(intent => IsEligibleAndTemporallyValid(intent, now)
                             && IsIntentBoundToParent(intent, aggregateType, aggregate.Id, aggregate.TenantId))
            .Select(intent => new AuditIntentWorkItem(
                new AuditIntentLocator(aggregate.TenantId, aggregateType, aggregate.Id, intent.IntentId),
                intent.DeliveryState,
                intent.AttemptCount,
                intent.ClaimGeneration,
                intent.TimestampUtc,
                intent.NextRetryAt,
                intent.LeaseUntil,
                aggregate.IsDeleted));

    private static FilterDefinition<LocalAuditIntent> EligibleIntentFilter(DateTimeOffset now, bool scalarCutover = false)
    {
        if (scalarCutover)
        {
            var current = Builders<LocalAuditIntent>.Filter.Eq(
                intent => intent.TemporalStorageVersion,
                AuditIntentTemporalStorage.CurrentVersion);
            var pendingCurrent = Builders<LocalAuditIntent>.Filter.Eq(
                    intent => intent.DeliveryState,
                    AuditIntentDeliveryState.Pending)
                & (Builders<LocalAuditIntent>.Filter.Eq(intent => intent.NextRetryAtUtcTicksV1, null)
                   | Builders<LocalAuditIntent>.Filter.Lte(intent => intent.NextRetryAtUtcTicksV1, now.UtcTicks));
            var staleCurrent = Builders<LocalAuditIntent>.Filter.Eq(
                    intent => intent.DeliveryState,
                    AuditIntentDeliveryState.Processing)
                & Builders<LocalAuditIntent>.Filter.Lte(intent => intent.LeaseUntilUtcTicksV1, now.UtcTicks);
            return current & (pendingCurrent | staleCurrent);
        }

        var pending = Builders<LocalAuditIntent>.Filter.Eq(
                intent => intent.DeliveryState,
                AuditIntentDeliveryState.Pending)
            & (Builders<LocalAuditIntent>.Filter.Eq(intent => intent.NextRetryAt, null)
               | Builders<LocalAuditIntent>.Filter.Lte(intent => intent.NextRetryAt, now));
        var staleProcessing = Builders<LocalAuditIntent>.Filter.Eq(
                intent => intent.DeliveryState,
                AuditIntentDeliveryState.Processing)
            & Builders<LocalAuditIntent>.Filter.Lte(intent => intent.LeaseUntil, now);
        return pending | staleProcessing;
    }

    private static bool IsEligible(LocalAuditIntent intent, DateTimeOffset now)
        => intent.DeliveryState == AuditIntentDeliveryState.Pending
               && (intent.NextRetryAt is null || intent.NextRetryAt <= now)
           || intent.DeliveryState == AuditIntentDeliveryState.Processing
               && intent.LeaseUntil is not null
               && intent.LeaseUntil <= now;

    private static bool IsEligibleAndTemporallyValid(LocalAuditIntent intent, DateTimeOffset now)
    {
        _ = AuditIntentTemporalStorage.Validate(intent);
        return IsEligible(intent, now);
    }

    private async Task<bool> IsScalarCutoverActiveAsync(CancellationToken cancellationToken)
    {
        var state = await _temporalMigrationRepository.GetValidatedStateAsync(cancellationToken);
        if (state is null)
        {
            return false;
        }
        if (state.Phase != AuditIntentTemporalMigrationState.Phases.CutoverActive)
        {
            return false;
        }
        if (!string.Equals(
                state.IndexEvidenceFingerprint,
                AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CUTOVER_EVIDENCE_INVALID");
        }
        return true;
    }

    private FilterDefinition<TEntity> TenantAggregateFilter<TEntity>(AuditIntentLocator locator)
        where TEntity : EntityBase, IAuditIntentAggregate
        => Builders<TEntity>.Filter.Eq(aggregate => aggregate.TenantId, _tenantId)
           & Builders<TEntity>.Filter.Eq(aggregate => aggregate.Id, locator.AggregateId);

    private static FilterDefinition<LocalAuditIntent> DeliveredIntentFilter(AuditIntentClaim claim)
        => BoundIntentFilter(claim.Locator)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.DeliveryState, AuditIntentDeliveryState.Delivered)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimToken, claim.ClaimToken)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimGeneration, claim.ClaimGeneration)
           & Builders<LocalAuditIntent>.Filter.Ne(intent => intent.CentralAcknowledgement, null)
           & Builders<LocalAuditIntent>.Filter.Ne(intent => intent.CentralIdempotencyKey, null)
           & Builders<LocalAuditIntent>.Filter.Ne(intent => intent.AcknowledgedContractVersion, null);

    private static FilterDefinition<LocalAuditIntent> ClaimedIntentFilter(
        AuditIntentClaim claim,
        DateTimeOffset now,
        AuditIntentTemporalStorageKind storageKind,
        LocalAuditIntent source)
    {
        var filter = BoundIntentFilter(claim.Locator)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.DeliveryState, AuditIntentDeliveryState.Processing)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimToken, claim.ClaimToken)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimGeneration, claim.ClaimGeneration);
        if (storageKind == AuditIntentTemporalStorageKind.Current)
        {
            return filter
                & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.TemporalStorageVersion, AuditIntentTemporalStorage.CurrentVersion)
                & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.TimestampUtcTicksV1, source.TimestampUtcTicksV1)
                & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.NextRetryAtUtcTicksV1, source.NextRetryAtUtcTicksV1)
                & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.LeaseUntilUtcTicksV1, source.LeaseUntilUtcTicksV1)
                & Builders<LocalAuditIntent>.Filter.Gt(intent => intent.LeaseUntilUtcTicksV1, now.UtcTicks);
        }

        return filter
            & Builders<LocalAuditIntent>.Filter.Exists(intent => intent.TemporalStorageVersion, false)
            & Builders<LocalAuditIntent>.Filter.Gt(intent => intent.LeaseUntil, now);
    }

    private static FilterDefinition<LocalAuditIntent> BoundIntentFilter(AuditIntentLocator locator)
        => Builders<LocalAuditIntent>.Filter.Eq(intent => intent.IntentId, locator.IntentId)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.TenantId, locator.TenantId)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.AggregateId, locator.AggregateId)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.AggregateType, locator.AggregateType)
           & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.SourceService, AuditIntentContract.SourceService);

    private static bool IsIntentBoundToParent(
        LocalAuditIntent intent,
        AuditAggregateType aggregateType,
        Guid aggregateId,
        Guid tenantId)
        => intent.TenantId == tenantId
           && intent.AggregateId == aggregateId
           && intent.AggregateType == aggregateType
           && string.Equals(intent.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal);

    private bool IsCurrentTenant(AuditIntentLocator locator)
        => locator.TenantId != Guid.Empty && locator.TenantId == _tenantId;

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }
    }

    private static void ValidateAcknowledgement(
        AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement)
    {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (string.IsNullOrWhiteSpace(acknowledgement.CentralAcknowledgement)
            || string.IsNullOrWhiteSpace(acknowledgement.CentralIdempotencyKey)
            || string.IsNullOrWhiteSpace(acknowledgement.ContractVersion)
            || acknowledgement.AcceptedAt == default)
        {
            throw new ArgumentException("A durable central-outbox acknowledgement contract is required.", nameof(acknowledgement));
        }

        var expectedIdempotencyKey = AuditIntentContract.BuildCentralIdempotencyKey(
            claim.Locator.TenantId,
            claim.Locator.IntentId,
            acknowledgement.ContractVersion);
        if (!string.Equals(
                acknowledgement.CentralIdempotencyKey.Trim(),
                expectedIdempotencyKey,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Central idempotency must use SourceService + TenantId + IntentId + ContractVersion.",
                nameof(acknowledgement));
        }
    }

    private void EnsureIndexes()
    {
        EnsureIndexes(_codeReservations, "code_reservations");
        EnsureIndexes(_globalProducts, "global_products");
        EnsureIndexes(_productDefinitionRevisions, "product_definition_revisions");
        EnsureIndexes(_gskus, "gskus");
        EnsureIndexes(_finishedGoods, "finished_goods");
        EnsureIndexes(_lskus, "lskus");
        EnsureIndexes(_productLegalEntityScopePolicies, "product_legal_entity_scope_policies");
        EnsureIndexes(_productLegalEntityScopeRolloutStates, "product_legal_entity_scope_rollout_states");
    }

    private static void EnsureIndexes<TEntity>(IMongoCollection<TEntity> collection, string suffix)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var keys = Builders<TEntity>.IndexKeys
            .Ascending(aggregate => aggregate.TenantId)
            .Ascending("AuditIntents.DeliveryState")
            .Ascending("AuditIntents.NextRetryAt")
            .Ascending("AuditIntents.LeaseUntil");
        collection.Indexes.CreateOne(new CreateIndexModel<TEntity>(
            keys,
            new CreateIndexOptions { Name = $"ix_mdm_{suffix}_tenant_audit_delivery" }));
    }

    private static UpdateDefinition<TTarget> ConvertUpdate<TSource, TTarget>(UpdateDefinition<TSource> update)
        => new BsonDocumentUpdateDefinition<TTarget>(update.Render(
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<TSource>(),
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry).AsBsonDocument);

    private static FilterDefinition<TEntity> CompleteDocumentFitsAfterIntentUpdateFilter<TEntity>(
        UpdateDefinition<TEntity> update,
        Guid intentId)
    {
        if (!RequiresCompleteDocumentBudget<TEntity>())
        {
            return Builders<TEntity>.Filter.Empty;
        }

        var rendered = update.Render(
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<TEntity>(),
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry).AsBsonDocument;
        var intentPatch = new BsonDocument();
        if (rendered.TryGetValue("$set", out var setValue))
        {
            foreach (var element in setValue.AsBsonDocument)
            {
                intentPatch[GetAuditIntentMemberName(element.Name)] = new BsonDocument("$literal", element.Value);
            }
        }
        if (rendered.TryGetValue("$inc", out var incrementValue))
        {
            foreach (var element in incrementValue.AsBsonDocument)
            {
                var memberName = GetAuditIntentMemberName(element.Name);
                intentPatch[memberName] = new BsonDocument(
                    "$add",
                    new BsonArray { $"$$intent.{memberName}", element.Value });
            }
        }

        var candidateIntents = new BsonDocument(
            "$map",
            new BsonDocument
            {
                { "input", "$AuditIntents" },
                { "as", "intent" },
                {
                    "in",
                    new BsonDocument(
                        "$cond",
                        new BsonArray
                        {
                            new BsonDocument(
                                "$eq",
                                new BsonArray
                                {
                                    "$$intent.IntentId",
                                    new BsonBinaryData(intentId, GuidRepresentation.Standard)
                                }),
                            new BsonDocument("$mergeObjects", new BsonArray { "$$intent", intentPatch }),
                            "$$intent"
                        })
                }
            });

        return CompleteDocumentFitsFilter<TEntity>(
            new BsonDocument("AuditIntents", candidateIntents));
    }

    private static FilterDefinition<TEntity> CompleteDocumentFitsAfterCompactionFilter<TEntity>(
        Guid intentId,
        LocalAuditIntentReceipt receipt)
    {
        if (!RequiresCompleteDocumentBudget<TEntity>())
        {
            return Builders<TEntity>.Filter.Empty;
        }

        var candidateIntents = new BsonDocument(
            "$filter",
            new BsonDocument
            {
                { "input", "$AuditIntents" },
                { "as", "intent" },
                {
                    "cond",
                    new BsonDocument(
                        "$ne",
                        new BsonArray
                        {
                            "$$intent.IntentId",
                            new BsonBinaryData(intentId, GuidRepresentation.Standard)
                        })
                }
            });
        var serializedReceipt = receipt.ToBsonDocument();
        var candidateReceipts = new BsonDocument(
            "$concatArrays",
            new BsonArray
            {
                new BsonDocument("$ifNull", new BsonArray { "$AuditIntentReceipts", new BsonArray() }),
                new BsonDocument("$literal", new BsonArray { serializedReceipt })
            });
        return CompleteDocumentFitsFilter<TEntity>(
            new BsonDocument
            {
                { "AuditIntents", candidateIntents },
                { "AuditIntentReceipts", candidateReceipts }
            });
    }

    private static FilterDefinition<TEntity> ExactlyOneIntentIdentityFilter<TEntity>(Guid intentId)
        where TEntity : EntityBase, IAuditIntentAggregate
        => new BsonDocumentFilterDefinition<TEntity>(
            new BsonDocument(
                "$expr",
                new BsonDocument(
                    "$eq",
                    new BsonArray
                    {
                        new BsonDocument(
                            "$size",
                            new BsonDocument(
                                "$filter",
                                new BsonDocument
                                {
                                    { "input", "$AuditIntents" },
                                    { "as", "intent" },
                                    {
                                        "cond",
                                        new BsonDocument(
                                            "$eq",
                                            new BsonArray
                                            {
                                                "$$intent.IntentId",
                                                new BsonBinaryData(intentId, GuidRepresentation.Standard)
                                            })
                                    }
                                })),
                        1
                    })));

    private static FilterDefinition<TEntity> CompleteDocumentFitsFilter<TEntity>(BsonDocument changedFields)
    {
        var candidateDocument = new BsonDocument(
            "$mergeObjects",
            new BsonArray { "$$ROOT", changedFields });
        return new BsonDocumentFilterDefinition<TEntity>(
            new BsonDocument(
                "$expr",
                new BsonDocument(
                    "$lte",
                    new BsonArray
                    {
                        new BsonDocument("$bsonSize", candidateDocument),
                        ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes
                    })));
    }

    private static bool RequiresCompleteDocumentBudget<TEntity>()
        => typeof(TEntity) == typeof(ProductLegalEntityScopePolicy)
           || typeof(TEntity) == typeof(ProductLegalEntityScopeRolloutState);

    private static string GetAuditIntentMemberName(string updatePath)
    {
        const string prefix = "AuditIntents.$.";
        if (!updatePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Audit bookkeeping update escaped the embedded intent boundary.");
        }

        return updatePath[prefix.Length..];
    }
}
