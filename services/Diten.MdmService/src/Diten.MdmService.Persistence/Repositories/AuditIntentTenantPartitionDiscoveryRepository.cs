using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class AuditIntentTenantPartitionDiscoveryRepository : IAuditIntentTenantPartitionDiscovery
{
    private static readonly CollectionSpec[] Collections =
    [
        new("mdm_code_reservations", AuditAggregateType.CodeReservation),
        new("mdm_global_products", AuditAggregateType.GlobalProduct),
        new("mdm_product_definition_revisions", AuditAggregateType.ProductDefinitionRevision),
        new("mdm_gskus", AuditAggregateType.Gsku),
        new("mdm_finished_goods", AuditAggregateType.FinishedGood),
        new("mdm_lskus", AuditAggregateType.Lsku),
        new("mdm_product_legal_entity_scope_policies", AuditAggregateType.ProductLegalEntityScopePolicy),
        new("mdm_product_legal_entity_scope_rollout_states", AuditAggregateType.ProductLegalEntityScopeRolloutState),
        new("mdm_product_abbreviation_register", AuditAggregateType.ProductAbbreviation)
    ];

    private readonly IMongoDatabase _database;
    private readonly IAuditIntentTemporalMigrationRepository _temporalMigrationRepository;
    private readonly TimeProvider _clock;

    public AuditIntentTenantPartitionDiscoveryRepository(
        IMongoDatabase database,
        IAuditIntentTemporalMigrationRepository temporalMigrationRepository,
        TimeProvider clock)
    {
        _database = database;
        _temporalMigrationRepository = temporalMigrationRepository;
        _clock = clock;
    }

    public async Task<AuditIntentTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty || limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var state = await _temporalMigrationRepository.GetValidatedStateAsync(cancellationToken);
        if (state?.Phase != AuditIntentTemporalMigrationState.Phases.CutoverActive)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_CUTOVER_REQUIRED");
        }

        var nowTicks = _clock.GetUtcNow().UtcTicks;
        var pages = await Task.WhenAll(Collections.Select(spec => DiscoverInCollectionAsync(
            spec,
            afterTenantId,
            nowTicks,
            limit,
            cancellationToken)));
        var tenantIds = pages.SelectMany(page => page)
            .Distinct()
            .OrderBy(tenantId => tenantId.ToString("N"), StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
        return new AuditIntentTenantPartitionPage(
            tenantIds,
            tenantIds.Length == limit ? tenantIds[^1] : null);
    }

    private async Task<IReadOnlyList<Guid>> DiscoverInCollectionAsync(
        CollectionSpec spec,
        Guid? afterTenantId,
        long nowTicks,
        int limit,
        CancellationToken cancellationToken)
    {
        var tenantFilter = new BsonDocument();
        if (afterTenantId.HasValue)
        {
            tenantFilter["$gt"] = new BsonBinaryData(afterTenantId.Value, GuidRepresentation.Standard);
        }

        var exactIntent = new BsonDocument
        {
            ["AuditIntents.TenantId"] = new BsonDocument("$type", "binData"),
            ["AuditIntents.AggregateType"] = (int)spec.AggregateType,
            ["AuditIntents.SourceService"] = AuditIntentContract.SourceService,
            ["AuditIntents.TemporalStorageVersion"] = AuditIntentTemporalStorage.CurrentVersion,
            ["$or"] = EligibleBranches(nowTicks)
        };
        var match = new BsonDocument(exactIntent)
        {
            ["TenantId"] = tenantFilter.ElementCount == 0
                ? new BsonDocument("$type", "binData")
                : tenantFilter,
            ["$expr"] = new BsonDocument("$eq", new BsonArray { "$TenantId", "$AuditIntents.TenantId" })
        };
        var pipeline = new[]
        {
            new BsonDocument("$unwind", "$AuditIntents"),
            new BsonDocument("$match", match),
            new BsonDocument("$group", new BsonDocument("_id", "$TenantId")),
            new BsonDocument("$sort", new BsonDocument("_id", 1)),
            new BsonDocument("$limit", limit)
        };
        var rows = await _database.GetCollection<BsonDocument>(spec.Name)
            .Aggregate<BsonDocument>(pipeline, new AggregateOptions { AllowDiskUse = false })
            .ToListAsync(cancellationToken);
        return rows.Select(row => row["_id"].AsGuid).ToArray();
    }

    private static BsonArray EligibleBranches(long nowTicks) =>
    [
        new BsonDocument
        {
            ["AuditIntents.DeliveryState"] = (int)AuditIntentDeliveryState.Pending,
            ["$or"] = new BsonArray
            {
                new BsonDocument("AuditIntents.NextRetryAtUtcTicksV1", BsonNull.Value),
                new BsonDocument("AuditIntents.NextRetryAtUtcTicksV1", new BsonDocument("$lte", nowTicks))
            }
        },
        new BsonDocument
        {
            ["AuditIntents.DeliveryState"] = (int)AuditIntentDeliveryState.Processing,
            ["AuditIntents.LeaseUntilUtcTicksV1"] = new BsonDocument("$lte", nowTicks)
        }
    ];

    private sealed record CollectionSpec(string Name, AuditAggregateType AggregateType);
}
