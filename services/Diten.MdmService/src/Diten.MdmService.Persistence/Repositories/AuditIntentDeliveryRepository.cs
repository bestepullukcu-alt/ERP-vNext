using System.Security.Cryptography;
using System.Collections.Concurrent;
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
    private const string ProductAbbreviationCollectionName = "mdm_product_abbreviation_register";
    private readonly IMongoCollection<CodeReservation> _codeReservations;
    private readonly IMongoCollection<GlobalProduct> _globalProducts;
    private readonly IMongoCollection<ProductDefinitionRevision> _productDefinitionRevisions;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<ProductLegalEntityScopePolicy> _productLegalEntityScopePolicies;
    private readonly IMongoCollection<ProductLegalEntityScopeRolloutState> _productLegalEntityScopeRolloutStates;
    private readonly IMongoCollection<ProductAbbreviationRegisterEntry> _productAbbreviations;
    private readonly IAuditIntentTemporalMigrationRepository _temporalMigrationRepository;
    private readonly Guid _tenantId;
    private readonly TimeProvider _timeProvider;
    private readonly IMongoDatabase _database;
    private readonly SelectedAuditIntentDeliveryRequest? _selection;
    private readonly Dictionary<AuditIntentLocator, BsonDocument> _selectedSources = [];
    private readonly Dictionary<AuditIntentLocator, BsonDocument> _selectedOwners = [];
    private readonly ConcurrentDictionary<AuditIntentLocator, AuditIntentClaim> _selectedClaims = new();
    private readonly ConcurrentDictionary<AuditIntentLocator, BsonDocument> _selectedReceipts = new();
    private readonly SemaphoreSlim _selectionPreparation = new(1, 1);
    private bool _selectionPrepared;

    public static AuditIntentDeliveryRepository CreateSelected(IMongoDatabase database,
        SelectedAuditIntentDeliveryRequest request, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(request);
        BsonSerializer.TryRegisterSerializer(new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));
        if (BsonSerializer.LookupSerializer<Guid>() is not MongoDB.Bson.Serialization.Serializers.GuidSerializer serializer
            || serializer.GuidRepresentation != GuidRepresentation.Standard)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_GUID_STORAGE_INVALID");
        return new(database, new SelectedTenantContext(request.TenantId), timeProvider,
            new AuditIntentTemporalMigrationRepository(database, timeProvider), request);
    }

    public static AuditIntentDeliveryRepository CreateSelected(string connectionString, string databaseName,
        SelectedAuditIntentDeliveryRequest request, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        settings.ConnectTimeout = TimeSpan.FromSeconds(2);
        settings.SocketTimeout = TimeSpan.FromSeconds(2);
        return CreateSelected(new MongoClient(settings).GetDatabase(databaseName), request, timeProvider);
    }

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
        : this(database, tenantContext, timeProvider, temporalMigrationRepository, null)
    {
    }

    private AuditIntentDeliveryRepository(IMongoDatabase database, ITenantContext tenantContext,
        TimeProvider timeProvider, IAuditIntentTemporalMigrationRepository temporalMigrationRepository,
        SelectedAuditIntentDeliveryRequest? selection)
    {
        _database = database;
        _selection = selection;
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
        _productAbbreviations = database.GetCollection<ProductAbbreviationRegisterEntry>(
            ProductAbbreviationCollectionName);
        _temporalMigrationRepository = temporalMigrationRepository
            ?? throw new ArgumentNullException(nameof(temporalMigrationRepository));
        _tenantId = tenantContext.TenantId;
        _timeProvider = timeProvider;
        if (selection is null) EnsureIndexes();
    }

    public async Task<IReadOnlyList<AuditIntentWorkItem>> DiscoverEligibleAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (_selection is not null)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_DISCOVERY_FORBIDDEN");
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
                FindEligibleScalarWorkItemsAsync(_productLegalEntityScopeRolloutStates, AuditAggregateType.ProductLegalEntityScopeRolloutState, now, limit, cancellationToken),
                FindEligibleScalarWorkItemsAsync(_productAbbreviations, AuditAggregateType.ProductAbbreviation, now, limit, cancellationToken));
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
        var productAbbreviations = await FindEligibleAggregatesAsync(
            _productAbbreviations,
            AuditAggregateType.ProductAbbreviation,
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
            .Concat(productAbbreviations.SelectMany(aggregate =>
                ToWorkItems(aggregate, AuditAggregateType.ProductAbbreviation, now)))
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
        if (_selection is not null)
        {
            RequireSelectedItem(locator);
            if (!_selectionPrepared || RequireSelectedItem(locator).ExpectedClaimGeneration != expectedClaimGeneration
                || _selectedClaims.ContainsKey(locator)) return null;
            await ValidateSelectedPrerequisitesAsync(cancellationToken);
            if (!await ValidateSelectedSourceAsync(locator, cancellationToken)) return null;
        }
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
        var claim = await (locator.AggregateType switch
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
            AuditAggregateType.ProductAbbreviation => TryClaimInCollectionAsync(
                _productAbbreviations, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now,
                scalarCutover, cancellationToken),
            _ => Task.FromResult<AuditIntentClaim?>(null)
        });
        if (_selection is not null && claim is not null) _selectedClaims.TryAdd(locator, claim);
        return claim;
    }

    public async Task<AuditIntentClaimedPayload?> ReadClaimedPayloadAsync(
        AuditIntentClaim claim,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (!await ValidateSelectedClaimAsync(claim, cancellationToken)) return null;
        if (!IsCurrentTenant(claim.Locator) || !await IsScalarCutoverActiveAsync(cancellationToken)) return null;
        var now = _timeProvider.GetUtcNow();
        return await (claim.Locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => ReadClaimedPayloadFromCollectionAsync(_codeReservations, claim, now, cancellationToken),
            AuditAggregateType.ProductAbbreviation => ReadClaimedPayloadFromCollectionAsync(_productAbbreviations, claim, now, cancellationToken),
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
        ValidateSelectedAcknowledgement(claim, acknowledgement, acknowledgement.CentralAcknowledgement);
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
        ValidateSelectedAcknowledgement(claim, acknowledgement, compactReceiptReference);
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
            AuditAggregateType.ProductAbbreviation => AcknowledgeAndCompactInCollectionAsync(
                _productAbbreviations, claim, acknowledgement, compactReceiptReference, now, cancellationToken),
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
            AuditAggregateType.ProductAbbreviation => CompactInCollectionAsync(
                _productAbbreviations, claim, compactReceiptReference, now, cancellationToken),
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

        filter &= CompleteDocumentFitsAfterIntentUpdateFilter(update, locator.IntentId)
            & SelectedSourceFilter<TEntity>(locator);

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
        var aggregate = await collection.Find(TenantAggregateFilter<TEntity>(claim.Locator)
            & SelectedSourceFilter<TEntity>(claim.Locator)).FirstOrDefaultAsync(cancellationToken);
        var matches = aggregate?.AuditIntents.Where(intent =>
            IsIntentBoundToParent(intent, claim.Locator.AggregateType, claim.Locator.AggregateId, claim.Locator.TenantId)
            && intent.IntentId == claim.Locator.IntentId && intent.DeliveryState == AuditIntentDeliveryState.Processing
            && string.Equals(intent.ClaimToken, claim.ClaimToken, StringComparison.Ordinal)
            && intent.ClaimGeneration == claim.ClaimGeneration && intent.LeaseUntil > now).Take(2).ToArray() ?? [];
        if (matches.Length != 1) return null;
        var intent = matches[0];
        if (_selection is not null)
        {
            ValidateSelectedIntent(RequireSelectedItem(claim.Locator), intent.ToBsonDocument());
            if (!_selectedSources[claim.Locator].Equals(ImmutableSource(intent.ToBsonDocument())))
                throw new InvalidOperationException("SELECTED_AUDIT_INTENT_SOURCE_DRIFT");
        }
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
            AuditAggregateType.ProductAbbreviation => UpdateClaimedInCollectionAsync(
                _productAbbreviations,
                claim,
                now,
                ConvertUpdate<CodeReservation, ProductAbbreviationRegisterEntry>(legacyUpdate),
                ConvertUpdate<CodeReservation, ProductAbbreviationRegisterEntry>(currentUpdate),
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
        if (!await ValidateSelectedClaimAsync(claim, cancellationToken)) return false;
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
        filter &= CompleteDocumentFitsAfterIntentUpdateFilter(update, claim.Locator.IntentId)
            & SelectedSourceFilter<TEntity>(claim.Locator);
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
        if (!await ValidateSelectedClaimAsync(claim, cancellationToken)) return false;
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

        if (_selection is not null)
            ValidateSelectedAcknowledgement(claim, new AuditIntentAcknowledgement(intent.CentralAcknowledgement,
                intent.CentralIdempotencyKey, intent.AcknowledgedContractVersion, intent.AcknowledgedAt.Value), compactReceiptReference);

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
        aggregateFilter &= CompleteDocumentFitsAfterCompactionFilter<TEntity>(claim.Locator.IntentId, receipt)
            & SelectedSourceFilter<TEntity>(claim.Locator);
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
        if (!await ValidateSelectedClaimAsync(claim, cancellationToken)) return false;
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
        aggregateFilter &= CompleteDocumentFitsAfterCompactionFilter<TEntity>(claim.Locator.IntentId, receipt)
            & SelectedSourceFilter<TEntity>(claim.Locator);
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
        var document = await raw.Find(Builders<BsonDocument>.Filter.Eq("TenantId", locator.TenantId)
                & Builders<BsonDocument>.Filter.Eq("_id", locator.AggregateId))
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
        => locator.TenantId != Guid.Empty && locator.TenantId == _tenantId
           && (_selection is null || _selection.Items.Any(item => item.Locator == locator));

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

    public async Task PrepareSelectedAsync(SelectedAuditIntentDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_selection is null || request.ExecutionId != _selection.ExecutionId
            || request.TenantId != _selection.TenantId || !request.Items.SequenceEqual(_selection.Items))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_PERMIT_MISMATCH");
        await _selectionPreparation.WaitAsync(cancellationToken);
        try
        {
            await ValidateSelectedPrerequisitesAsync(cancellationToken);
            if (_selectionPrepared) return;
            foreach (var item in _selection.Items)
            {
                var document = await ReadSelectedDocumentAsync(item.Locator, cancellationToken);
                _selectedOwners[item.Locator] = ValidateSelectedOwner(item.Locator, document);
                await ValidateReservationIdentityEvidenceAsync(item, document, cancellationToken);
                var intents = ReadRawIntents(document).Where(intent =>
                    intent.GetValue("IntentId", BsonNull.Value) == new BsonBinaryData(item.Locator.IntentId, GuidRepresentation.Standard))
                    .Take(2).ToArray();
                var receipt = ValidateSelectedReceipt(item, document);
                if (receipt is not null)
                {
                    _selectedReceipts.TryAdd(item.Locator, receipt.ToBsonDocument());
                    continue;
                }
                if (intents.Length != 1 || intents[0].GetValue("ClaimGeneration", -1).ToInt64() != item.ExpectedClaimGeneration)
                    throw new InvalidOperationException("SELECTED_AUDIT_INTENT_GENERATION_MISMATCH");
                ValidateSelectedIntent(item, intents[0]);
                _selectedSources[item.Locator] = ImmutableSource(intents[0]);
            }
            _selectionPrepared = true;
        }
        finally { _selectionPreparation.Release(); }
    }

    public async Task<LocalAuditIntentReceipt?> ReadSelectedReceiptAsync(AuditIntentLocator locator,
        CancellationToken cancellationToken = default)
    {
        var item = RequireSelectedItem(locator);
        if (!_selectionPrepared) throw new InvalidOperationException("SELECTED_AUDIT_INTENT_PREFLIGHT_REQUIRED");
        var document = await ReadSelectedDocumentAsync(locator, cancellationToken);
        RequireUnchangedOwner(locator, document);
        await ValidateReservationIdentityEvidenceAsync(item, document, cancellationToken);
        var receipt = ValidateSelectedReceipt(item, document);
        if (receipt is null)
        {
            if (_selectedReceipts.ContainsKey(locator))
                throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RECEIPT_DRIFT");
            return null;
        }
        var snapshot = receipt.ToBsonDocument();
        var original = _selectedReceipts.GetOrAdd(locator, snapshot);
        if (!original.Equals(snapshot)) throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RECEIPT_DRIFT");
        return receipt;
    }

    private SelectedAuditIntentDeliveryItem RequireSelectedItem(AuditIntentLocator locator)
        => _selection?.Items.SingleOrDefault(item => item.Locator == locator)
           ?? throw new InvalidOperationException("SELECTED_AUDIT_INTENT_LOCATOR_DENIED");

    private async Task<bool> ValidateSelectedClaimAsync(AuditIntentClaim claim, CancellationToken cancellationToken)
    {
        if (_selection is null) return true;
        RequireSelectedItem(claim.Locator);
        if (!_selectionPrepared || !_selectedClaims.TryGetValue(claim.Locator, out var actual) || actual != claim)
            return false;
        await ValidateSelectedPrerequisitesAsync(cancellationToken);
        return await ValidateSelectedSourceAsync(claim.Locator, cancellationToken);
    }

    private async Task<bool> ValidateSelectedSourceAsync(AuditIntentLocator locator, CancellationToken cancellationToken)
    {
        var item = RequireSelectedItem(locator);
        var document = await ReadSelectedDocumentAsync(locator, cancellationToken);
        RequireUnchangedOwner(locator, document);
        await ValidateReservationIdentityEvidenceAsync(item, document, cancellationToken);
        if (ValidateSelectedReceipt(item, document) is not null)
        {
            _ = await ReadSelectedReceiptAsync(locator, cancellationToken);
            return false; // A receipt is read-back evidence, never a new mutation permit.
        }
        var intents = ReadRawIntents(document).Where(intent =>
            intent.GetValue("IntentId", BsonNull.Value) == new BsonBinaryData(locator.IntentId, GuidRepresentation.Standard))
            .Take(2).ToArray();
        if (intents.Length != 1) return false;
        ValidateSelectedIntent(item, intents[0]);
        if (!_selectedSources.TryGetValue(locator, out var source) || !source.Equals(ImmutableSource(intents[0])))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_SOURCE_DRIFT");
        return true;
    }

    private async Task<BsonDocument> ReadSelectedDocumentAsync(AuditIntentLocator locator, CancellationToken cancellationToken)
    {
        RequireSelectedItem(locator);
        return await _database.GetCollection<BsonDocument>(SelectedCollectionName(locator.AggregateType))
            .Find(new BsonDocument
            {
                { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
                { "_id", new BsonBinaryData(locator.AggregateId, GuidRepresentation.Standard) }
            }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("SELECTED_AUDIT_INTENT_SOURCE_NOT_FOUND");
    }

    private static BsonDocument ValidateSelectedOwner(AuditIntentLocator locator, BsonDocument document)
    {
        if (locator.AggregateType != AuditAggregateType.CodeReservation) return new BsonDocument();
        var reservation = BsonSerializer.Deserialize<CodeReservation>(document);
        var prefix = reservation.EntityType switch
        {
            CodeBearingEntityType.GlobalProduct => "GP-",
            CodeBearingEntityType.Gsku => "GS-",
            CodeBearingEntityType.Lsku => "LS-",
            _ => throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RESERVATION_OWNER_DENIED")
        };
        if (reservation.TenantId != locator.TenantId || reservation.Id != locator.AggregateId
            || reservation.ReservedCode.Length != 15 || !reservation.ReservedCode.StartsWith(prefix, StringComparison.Ordinal)
            || reservation.ReservedCode[3..].Any(character => character is < '0' or > '9')
            || string.IsNullOrWhiteSpace(reservation.ReservationCommandId)
            || string.IsNullOrWhiteSpace(reservation.ReservedByActorId)
            || reservation.ConsumedEntityId == Guid.Empty)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RESERVATION_OWNER_UNPROVEN");
        return new BsonDocument(document.Elements.Where(element => element.Name is
            "EntityType" or "ReservedCode" or "ReservationCommandId" or "ReservedByActorId" or "ConsumedEntityId"));
    }

    private void RequireUnchangedOwner(AuditIntentLocator locator, BsonDocument document)
    {
        if (!_selectedOwners.TryGetValue(locator, out var expected)
            || !expected.Equals(ValidateSelectedOwner(locator, document)))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_OWNER_DRIFT");
    }

    private async Task ValidateReservationIdentityEvidenceAsync(SelectedAuditIntentDeliveryItem item,
        BsonDocument document, CancellationToken cancellationToken)
    {
        if (item.Locator.AggregateType != AuditAggregateType.CodeReservation) return;
        var reservation = BsonSerializer.Deserialize<CodeReservation>(document);
        var reservedEvidence = $"{_tenantId:N}|{reservation.Id:N}|{reservation.ReservedCode}|{reservation.EntityType}|RESERVED";
        var reservedHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reservedEvidence)));
        var intent = reservation.AuditIntents.SingleOrDefault(candidate => candidate.IntentId == item.Locator.IntentId);
        var reservedIdempotencyKey = $"{_tenantId:N}:{AuditAggregateType.CodeReservation}:{reservation.Id:N}:{reservation.ReservationCommandId}";
        if (intent is null && item.EvidenceFingerprint == reservedHash)
        {
            // Compaction removes the operation, not the original reservation's ownership evidence.
            // Require the exact durable receipt and producer command binding; a hash alone is not a replay permit.
            var receipt = ValidateSelectedReceipt(item, document);
            if (receipt is null || receipt.IdempotencyKey != reservedIdempotencyKey)
                throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RESERVATION_EVIDENCE_INVALID");
            return;
        }
        if (intent?.Operation == ProductAuditOperation.CodeReserved || reservation.ConsumedEntityId is null)
        {
            if (item.EvidenceFingerprint != reservedHash
                || intent is not null && (intent.Operation != ProductAuditOperation.CodeReserved
                    || intent.ActorId != reservation.ReservedByActorId || intent.CommandId != reservation.ReservationCommandId
                    || intent.IdempotencyKey != reservedIdempotencyKey))
                throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RESERVATION_EVIDENCE_INVALID");
            return;
        }
        var collectionName = reservation.EntityType switch
        {
            CodeBearingEntityType.GlobalProduct => GlobalProductCollectionName,
            CodeBearingEntityType.Gsku => GskuCollectionName,
            CodeBearingEntityType.Lsku => LskuCollectionName,
            _ => throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RESERVATION_OWNER_DENIED")
        };
        var identity = await _database.GetCollection<BsonDocument>(collectionName).Find(new BsonDocument
        {
            { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
            { "_id", new BsonBinaryData(reservation.ConsumedEntityId.Value, GuidRepresentation.Standard) },
            { "CodeReservationId", new BsonBinaryData(reservation.Id, GuidRepresentation.Standard) },
            { "CanonicalCode", reservation.ReservedCode }
        }).Project(new BsonDocument("_id", 1)).FirstOrDefaultAsync(cancellationToken);
        if (identity is null) throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RESERVATION_OWNER_UNPROVEN");
    }

    private static void ValidateSelectedIntent(SelectedAuditIntentDeliveryItem item, BsonDocument raw)
    {
        var intent = BsonSerializer.Deserialize<LocalAuditIntent>(raw);
        if (!IsIntentBoundToParent(intent, item.Locator.AggregateType, item.Locator.AggregateId, item.Locator.TenantId)
            || intent.IntentId != item.Locator.IntentId || intent.ContractVersion != "mod-0290.audit-intent.v1"
            || intent.EvidenceHash != item.EvidenceFingerprint
            || !IsSelectedOperationAllowed(intent.AggregateType, intent.Operation)
            || AuditIntentTemporalMigrationRepository.ValidateRawIntent(raw) != AuditIntentTemporalStorageKind.Current
            || AuditIntentTemporalStorage.Validate(intent) != AuditIntentTemporalStorageKind.Current)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_SOURCE_INVALID");
    }

    // The current central contract's exact pairs, intersected with this selected first-five permit.
    // Do not infer support from an enum value or a name prefix: unknown/mismatched pairs fail before claim.
    private static bool IsSelectedOperationAllowed(AuditAggregateType aggregateType, ProductAuditOperation operation)
        => aggregateType switch
        {
            AuditAggregateType.CodeReservation => operation is
                ProductAuditOperation.CodeReserved or ProductAuditOperation.CodeConsumed
                or ProductAuditOperation.CodeBindingConfirmed or ProductAuditOperation.CodeBurned,
            AuditAggregateType.GlobalProduct => operation is
                ProductAuditOperation.GlobalProductDraftCreated or ProductAuditOperation.GlobalProductDraftUpdated
                or ProductAuditOperation.GlobalProductIdentitySubmitted or ProductAuditOperation.GlobalProductIdentityApproved
                or ProductAuditOperation.GlobalProductIdentityRejected or ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn
                or ProductAuditOperation.GlobalProductIdentityRetired
                or ProductAuditOperation.GlobalProductCorrectionRequested or ProductAuditOperation.GlobalProductCorrectionApplied
                or ProductAuditOperation.GlobalProductCorrectionRejected or ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired
                or ProductAuditOperation.GlobalProductRetirementRequested or ProductAuditOperation.GlobalProductRetirementRejected
                or ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired,
            AuditAggregateType.ProductDefinitionRevision => operation is
                ProductAuditOperation.ProductDefinitionRevisionDraftCreated
                or ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted
                or ProductAuditOperation.ProductDefinitionRevisionIdentityApproved
                or ProductAuditOperation.ProductDefinitionRevisionIdentityRejected
                or ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn
                or ProductAuditOperation.ProductDefinitionRevisionIdentityRetired,
            AuditAggregateType.Gsku => operation is
                ProductAuditOperation.GskuDraftCreated or ProductAuditOperation.GskuDraftUpdated
                or ProductAuditOperation.GskuIdentitySubmitted or ProductAuditOperation.GskuIdentityApproved
                or ProductAuditOperation.GskuIdentityRejected or ProductAuditOperation.GskuIdentityApprovalWithdrawn
                or ProductAuditOperation.GskuCorrectionRequested or ProductAuditOperation.GskuCorrectionApplied
                or ProductAuditOperation.GskuCorrectionRejected or ProductAuditOperation.GskuCorrectionManualReconciliationRequired
                or ProductAuditOperation.GskuRetirementRequested or ProductAuditOperation.GskuRetirementRejected
                or ProductAuditOperation.GskuRetirementManualReconciliationRequired or ProductAuditOperation.GskuIdentityRetired,
            AuditAggregateType.Lsku => operation is
                ProductAuditOperation.LskuDraftCreated or ProductAuditOperation.LskuIdentitySubmitted
                or ProductAuditOperation.LskuIdentityApproved or ProductAuditOperation.LskuIdentityRejected
                or ProductAuditOperation.LskuIdentityRetired or ProductAuditOperation.LskuIdentityApprovalWithdrawn
                or ProductAuditOperation.LskuRetirementRequested or ProductAuditOperation.LskuRetirementRejected,
            AuditAggregateType.ProductAbbreviation => operation is
                ProductAuditOperation.ProductAbbreviationAllocationRequested
                or ProductAuditOperation.ProductAbbreviationAllocationApproved
                or ProductAuditOperation.ProductAbbreviationAllocationRejected
                or ProductAuditOperation.ProductAbbreviationAllocationCancelled
                or ProductAuditOperation.ProductAbbreviationCorrectionRequested
                or ProductAuditOperation.ProductAbbreviationCorrectionApproved
                or ProductAuditOperation.ProductAbbreviationCorrectionRejected
                or ProductAuditOperation.ProductAbbreviationCorrectionCancelled
                or ProductAuditOperation.ProductAbbreviationRetirementRequested
                or ProductAuditOperation.ProductAbbreviationRetirementApproved
                or ProductAuditOperation.ProductAbbreviationRetirementRejected,
            AuditAggregateType.ProductLegalEntityScopePolicy => operation is
                ProductAuditOperation.ProductLegalEntityScopePolicyCreated
                or ProductAuditOperation.ProductLegalEntityScopePolicyReplaced
                or ProductAuditOperation.ProductLegalEntityScopePolicyEnded,
            _ => false
        };

    private static LocalAuditIntentReceipt? ValidateSelectedReceipt(SelectedAuditIntentDeliveryItem item, BsonDocument document)
    {
        var matches = document.GetValue("AuditIntentReceipts", new BsonArray()).AsBsonArray
            .Select(value => value.AsBsonDocument)
            .Where(value => value.GetValue("IntentId", BsonNull.Value)
                == new BsonBinaryData(item.Locator.IntentId, GuidRepresentation.Standard)).Take(2).ToArray();
        if (matches.Length == 0) return null;
        if (matches.Length != 1 || ReadRawIntents(document).Any(intent => intent.GetValue("IntentId", BsonNull.Value)
                == new BsonBinaryData(item.Locator.IntentId, GuidRepresentation.Standard)))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RECEIPT_DRIFT");
        var receipt = BsonSerializer.Deserialize<LocalAuditIntentReceipt>(matches[0]);
        if (receipt.TenantId != item.Locator.TenantId || receipt.SourceService != AuditIntentContract.SourceService
            || receipt.ContractVersion != "mod-0290.audit-intent.v1" || receipt.EvidenceHash != item.EvidenceFingerprint
            || receipt.CentralIdempotencyKey != AuditIntentContract.BuildCentralIdempotencyKey(
                item.Locator.TenantId, item.Locator.IntentId, receipt.ContractVersion)
            || string.IsNullOrWhiteSpace(receipt.IdempotencyKey)
            || string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement) || receipt.CentralAcknowledgement.Length > 512
            || receipt.CentralAcknowledgement.Trim() != receipt.CentralAcknowledgement
            || receipt.CentralAcknowledgement.Any(char.IsControl)
            || receipt.CompactReceiptReference != receipt.CentralAcknowledgement
            || receipt.AcknowledgedAt == default || receipt.DeliveredAt == default || receipt.CompactedAt == default
            || receipt.AcknowledgedAt.Offset != TimeSpan.Zero || receipt.DeliveredAt.Offset != TimeSpan.Zero
            || receipt.CompactedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RECEIPT_DRIFT");
        return receipt;
    }

    private void ValidateSelectedAcknowledgement(AuditIntentClaim claim,
        AuditIntentAcknowledgement acknowledgement, string compactReceiptReference)
    {
        if (_selection is null) return;
        RequireSelectedItem(claim.Locator);
        if (acknowledgement.ContractVersion != "mod-0290.audit-intent.v1"
            || string.IsNullOrWhiteSpace(acknowledgement.CentralAcknowledgement)
            || acknowledgement.CentralAcknowledgement.Length > 512
            || acknowledgement.CentralAcknowledgement.Trim() != acknowledgement.CentralAcknowledgement
            || acknowledgement.CentralAcknowledgement.Any(char.IsControl)
            || compactReceiptReference != acknowledgement.CentralAcknowledgement
            || acknowledgement.AcceptedAt == default || acknowledgement.AcceptedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_RECEIPT_INVALID");
    }

    private static BsonDocument ImmutableSource(BsonDocument intent)
        => new(intent.Elements.Where(element => element.Name is
            "SourceService" or "SchemaVersion" or "ContractVersion" or "IntentId" or "TenantId" or "AggregateType"
            or "AggregateId" or "PreVersion" or "PostVersion" or "Operation" or "ActorId" or "CorrelationId"
            or "CausationId" or "CommandId" or "Sequence" or "TimestampUtc" or "TimestampUtcTicksV1"
            or "EvidenceHash" or "SnapshotReference" or "IdempotencyKey" or "TemporalStorageVersion")
            .OrderBy(element => element.Name, StringComparer.Ordinal));

    private FilterDefinition<TEntity> SelectedSourceFilter<TEntity>(AuditIntentLocator locator)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        if (_selection is null) return Builders<TEntity>.Filter.Empty;
        RequireSelectedItem(locator);
        if (!_selectionPrepared || !_selectedSources.TryGetValue(locator, out var source))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_PREFLIGHT_REQUIRED");
        var clauses = new BsonArray
        {
            new BsonDocument("AuditIntents", new BsonDocument("$elemMatch", source)),
            _selectedOwners[locator]
        };
        return new BsonDocumentFilterDefinition<TEntity>(new BsonDocument("$and", clauses))
            & ExactlyOneIntentIdentityFilter<TEntity>(locator.IntentId)
            & Builders<TEntity>.Filter.Not(Builders<TEntity>.Filter.ElemMatch(
                aggregate => aggregate.AuditIntentReceipts, receipt => receipt.IntentId == locator.IntentId));
    }

    private async Task ValidateSelectedPrerequisitesAsync(CancellationToken cancellationToken)
    {
        var hello = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);
        if (!hello.GetValue("isWritablePrimary", false).ToBoolean()
            || !hello.TryGetValue("setName", out var setName) || !setName.IsString || string.IsNullOrWhiteSpace(setName.AsString))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_WRITABLE_REPLICA_REQUIRED");
        using var collectionCursor = await _database.ListCollectionsAsync(cancellationToken: cancellationToken);
        var collections = await collectionCursor.ToListAsync(cancellationToken);
        var required = _selection!.Items.Select(item => SelectedCollectionName(item.Locator.AggregateType))
            .Append(AuditIntentTemporalMigrationRepository.StateCollectionName).Distinct(StringComparer.Ordinal);
        foreach (var name in required)
        {
            var specs = collections.Where(spec => spec.GetValue("name", "") == name).ToArray();
            if (specs.Length != 1 || specs[0].GetValue("type", "") != "collection"
                || specs[0].GetValue("options", new BsonDocument()).AsBsonDocument.GetValue("capped", false).ToBoolean())
                throw new InvalidOperationException("SELECTED_AUDIT_INTENT_COLLECTION_INVALID");
            if (name == AuditIntentTemporalMigrationRepository.StateCollectionName) continue;
            var abbreviation = name == ProductAbbreviationCollectionName;
            var expectedKeys = abbreviation
                ? new BsonDocument { { "TenantId", 1 }, { "NormalizedAbbreviation", 1 }, { "LifecycleStatus", 1 } }
                : new BsonDocument
                {
                    { "TenantId", 1 }, { "AuditIntents.TemporalStorageVersion", 1 }, { "AuditIntents.DeliveryState", 1 },
                    { "AuditIntents.NextRetryAtUtcTicksV1", 1 }, { "AuditIntents.LeaseUntilUtcTicksV1", 1 },
                    { "AuditIntents.TimestampUtcTicksV1", 1 }, { "AuditIntents.IntentId", 1 }
                };
            var expectedName = abbreviation ? "ix_mdm_product_abbreviation_register_tenant_resolution"
                : $"ix_{name}_{AuditIntentTemporalMigrationState.ExactIndexSuffix}";
            using var indexCursor = await _database.GetCollection<BsonDocument>(name).Indexes.ListAsync(cancellationToken);
            var indexes = (await indexCursor.ToListAsync(cancellationToken))
                .Where(index => index.GetValue("name", "") == expectedName).ToArray();
            if (indexes.Length != 1 || !indexes[0].GetValue("key", new BsonDocument()).Equals(expectedKeys)
                || indexes[0].GetValue("unique", false).ToBoolean() || indexes[0].GetValue("sparse", false).ToBoolean()
                || indexes[0].GetValue("hidden", false).ToBoolean() || indexes[0].Contains("partialFilterExpression")
                || indexes[0].Contains("expireAfterSeconds") || indexes[0].Contains("collation"))
                throw new InvalidOperationException("SELECTED_AUDIT_INTENT_INDEX_INVALID");
        }
        if (!await IsScalarCutoverActiveAsync(cancellationToken))
            throw new InvalidOperationException("SELECTED_AUDIT_INTENT_CUTOVER_REQUIRED");
    }

    private static string SelectedCollectionName(AuditAggregateType type) => type switch
    {
        AuditAggregateType.CodeReservation => CodeReservationCollectionName,
        AuditAggregateType.GlobalProduct => GlobalProductCollectionName,
        AuditAggregateType.ProductDefinitionRevision => ProductDefinitionRevisionCollectionName,
        AuditAggregateType.Gsku => GskuCollectionName,
        AuditAggregateType.Lsku => LskuCollectionName,
        AuditAggregateType.ProductLegalEntityScopePolicy => ProductLegalEntityScopePolicyCollectionName,
        AuditAggregateType.ProductAbbreviation => ProductAbbreviationCollectionName,
        _ => throw new InvalidOperationException("SELECTED_AUDIT_INTENT_AGGREGATE_DENIED")
    };

    private sealed class SelectedTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid tenantId) => throw new InvalidOperationException("SELECTED_AUDIT_INTENT_TENANT_IMMUTABLE");
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
