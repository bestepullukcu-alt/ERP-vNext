using System.Security.Cryptography;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
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
    private readonly IMongoCollection<CodeReservation> _codeReservations;
    private readonly IMongoCollection<GlobalProduct> _globalProducts;
    private readonly IMongoCollection<ProductDefinitionRevision> _productDefinitionRevisions;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly Guid _tenantId;
    private readonly TimeProvider _timeProvider;

    public AuditIntentDeliveryRepository(
        IMongoDatabase database,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _codeReservations = database.GetCollection<CodeReservation>(CodeReservationCollectionName);
        _globalProducts = database.GetCollection<GlobalProduct>(GlobalProductCollectionName);
        _productDefinitionRevisions = database.GetCollection<ProductDefinitionRevision>(ProductDefinitionRevisionCollectionName);
        _gskus = database.GetCollection<Gsku>(GskuCollectionName);
        _finishedGoods = database.GetCollection<FinishedGood>(FinishedGoodCollectionName);
        _lskus = database.GetCollection<Lsku>(LskuCollectionName);
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
        var codeReservations = await FindEligibleAggregatesAsync(
            _codeReservations,
            AuditAggregateType.CodeReservation,
            now,
            cancellationToken);
        var globalProducts = await FindEligibleAggregatesAsync(
            _globalProducts,
            AuditAggregateType.GlobalProduct,
            now,
            cancellationToken);
        var productDefinitionRevisions = await FindEligibleAggregatesAsync(
            _productDefinitionRevisions,
            AuditAggregateType.ProductDefinitionRevision,
            now,
            cancellationToken);
        var gskus = await FindEligibleAggregatesAsync(
            _gskus,
            AuditAggregateType.Gsku,
            now,
            cancellationToken);
        var finishedGoods = await FindEligibleAggregatesAsync(
            _finishedGoods,
            AuditAggregateType.FinishedGood,
            now,
            cancellationToken);
        var lskus = await FindEligibleAggregatesAsync(
            _lskus,
            AuditAggregateType.Lsku,
            now,
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
            .OrderBy(item => item.TimestampUtc)
            .ThenBy(item => item.Locator.IntentId)
            .Take(limit)
            .ToArray();
    }

    public Task<AuditIntentClaim?> TryClaimAsync(
        AuditIntentLocator locator,
        long expectedClaimGeneration,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentTenant(locator) || expectedClaimGeneration < 0)
        {
            return Task.FromResult<AuditIntentClaim?>(null);
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
        return locator.AggregateType switch
        {
            AuditAggregateType.CodeReservation => TryClaimInCollectionAsync(
                _codeReservations,
                locator,
                expectedClaimGeneration,
                leaseOwner,
                leaseDuration,
                now,
                cancellationToken),
            AuditAggregateType.GlobalProduct => TryClaimInCollectionAsync(
                _globalProducts,
                locator,
                expectedClaimGeneration,
                leaseOwner,
                leaseDuration,
                now,
                cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => TryClaimInCollectionAsync(
                _productDefinitionRevisions, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, cancellationToken),
            AuditAggregateType.Gsku => TryClaimInCollectionAsync(
                _gskus, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, cancellationToken),
            AuditAggregateType.FinishedGood => TryClaimInCollectionAsync(
                _finishedGoods, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, cancellationToken),
            AuditAggregateType.Lsku => TryClaimInCollectionAsync(
                _lskus, locator, expectedClaimGeneration, leaseOwner, leaseDuration, now, cancellationToken),
            _ => Task.FromResult<AuditIntentClaim?>(null)
        };
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
        return UpdateClaimedIntentAsync(claim, now, update, cancellationToken);
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
        return UpdateClaimedIntentAsync(claim, now, update, cancellationToken);
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
        return UpdateClaimedIntentAsync(claim, now, update, cancellationToken);
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
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var eligible = BoundIntentFilter(locator)
            & EligibleIntentFilter(now)
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

    private Task<bool> UpdateClaimedIntentAsync(
        AuditIntentClaim claim,
        DateTimeOffset now,
        UpdateDefinition<CodeReservation> codeReservationUpdate,
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
                codeReservationUpdate,
                cancellationToken),
            AuditAggregateType.GlobalProduct => UpdateClaimedInCollectionAsync(
                _globalProducts,
                claim,
                now,
                ConvertUpdate<CodeReservation, GlobalProduct>(codeReservationUpdate),
                cancellationToken),
            AuditAggregateType.ProductDefinitionRevision => UpdateClaimedInCollectionAsync(
                _productDefinitionRevisions,
                claim,
                now,
                ConvertUpdate<CodeReservation, ProductDefinitionRevision>(codeReservationUpdate),
                cancellationToken),
            AuditAggregateType.Gsku => UpdateClaimedInCollectionAsync(
                _gskus,
                claim,
                now,
                ConvertUpdate<CodeReservation, Gsku>(codeReservationUpdate),
                cancellationToken),
            AuditAggregateType.FinishedGood => UpdateClaimedInCollectionAsync(
                _finishedGoods,
                claim,
                now,
                ConvertUpdate<CodeReservation, FinishedGood>(codeReservationUpdate),
                cancellationToken),
            AuditAggregateType.Lsku => UpdateClaimedInCollectionAsync(
                _lskus,
                claim,
                now,
                ConvertUpdate<CodeReservation, Lsku>(codeReservationUpdate),
                cancellationToken),
            _ => Task.FromResult(false)
        };
    }

    private async Task<bool> UpdateClaimedInCollectionAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditIntentClaim claim,
        DateTimeOffset now,
        UpdateDefinition<TEntity> update,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var intentFilter = BoundIntentFilter(claim.Locator)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.DeliveryState, AuditIntentDeliveryState.Processing)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimToken, claim.ClaimToken)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.ClaimGeneration, claim.ClaimGeneration)
            & Builders<LocalAuditIntent>.Filter.Gt(intent => intent.LeaseUntil, now);
        var filter = TenantAggregateFilter<TEntity>(claim.Locator)
            & Builders<TEntity>.Filter.ElemMatch(aggregate => aggregate.AuditIntents, intentFilter);
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

    private async Task<IReadOnlyList<TEntity>> FindEligibleAggregatesAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        AuditAggregateType aggregateType,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TEntity : EntityBase, IAuditIntentAggregate
    {
        var boundIntent = Builders<LocalAuditIntent>.Filter.Eq(intent => intent.TenantId, _tenantId)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.AggregateType, aggregateType)
            & Builders<LocalAuditIntent>.Filter.Eq(intent => intent.SourceService, AuditIntentContract.SourceService)
            & EligibleIntentFilter(now);
        var filter = Builders<TEntity>.Filter.Eq(aggregate => aggregate.TenantId, _tenantId)
            & Builders<TEntity>.Filter.ElemMatch(aggregate => aggregate.AuditIntents, boundIntent);
        return await collection.Find(filter).ToListAsync(cancellationToken);
    }

    private IEnumerable<AuditIntentWorkItem> ToWorkItems<TEntity>(
        TEntity aggregate,
        AuditAggregateType aggregateType,
        DateTimeOffset now)
        where TEntity : EntityBase, IAuditIntentAggregate
        => aggregate.AuditIntents
            .Where(intent => IsEligible(intent, now)
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

    private static FilterDefinition<LocalAuditIntent> EligibleIntentFilter(DateTimeOffset now)
    {
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
}
