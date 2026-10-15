using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GP-BL552 · P-2026-10-07-GP-FLOW3-FIX3 item 1 — the switched-off workers' stuck scans read only what is stuck.
/// On this host's throwaway mongod: 100 FINISHED requests of noise, nothing stuck; the production filter (the stores'
/// own public filters, not a copy) is explained — it must be an index scan on its partial index that examines (almost)
/// nothing, however long the history grows.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private const int GpNoise = 100;

    private async Task<BsonDocument> ExplainStuckScanAsync<T>(string collection, FilterDefinition<T> filter)
    {
        var registry = BsonSerializer.SerializerRegistry;
        var command = new BsonDocument
        {
            ["explain"] = new BsonDocument
            {
                ["find"] = collection,
                ["filter"] = filter.Render(registry.GetSerializer<T>(), registry),
                ["sort"] = new BsonDocument("TenantId", 1),
                ["projection"] = new BsonDocument { ["TenantId"] = 1, ["_id"] = 0 },
                ["limit"] = 25
            },
            ["verbosity"] = "executionStats"
        };
        return await host.Database.RunCommandAsync<BsonDocument>(command);
    }

    private static void AssertReadsOnlyTheStuckIndex(BsonDocument explain, params string[] indexes)
    {
        var plan = explain["executionStats"]["executionStages"].ToJson();
        Assert.Contains("IXSCAN", plan);
        Assert.DoesNotContain("COLLSCAN", plan);
        foreach (var index in indexes) Assert.True(plan.Contains(index, StringComparison.Ordinal), plan);
        // The class shares one database: other tests' stuck requests may be there and are rightly read. What must not
        // be read is the finished history — the scan examines what it returns, plus a small constant, never the noise.
        var stats = explain["executionStats"];
        var returned = stats["nReturned"].ToInt32();
        Assert.True(stats["totalKeysExamined"].ToInt32() <= returned + 2, stats.ToJson());
        Assert.True(stats["totalDocsExamined"].ToInt32() <= returned + 2, stats.ToJson());
        Assert.True(stats["totalKeysExamined"].ToInt32() < GpNoise, stats.ToJson());
    }

    [Fact]
    public async Task GPIF3_The_identity_stuck_scan_reads_only_its_partial_index_beside_finished_history()
    {
        _ = new GlobalProductIdentityWorkflowOperationRepository(host.Database, Tenant()); // the indexes, as installed
        await host.Database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .InsertManyAsync(Enumerable.Range(0, GpNoise).Select(_ => new GlobalProductIdentityWorkflowOperation
            {
                Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), OperationId = Guid.NewGuid(), GlobalProductId = Guid.NewGuid(),
                StartIdempotencyKey = Guid.NewGuid().ToString("N"), Checkpoint = GlobalProductIdentityWorkflowCheckpoint.Completed
            }));

        var explain = await ExplainStuckScanAsync(GlobalProductIdentityWorkflowOperationRepository.CollectionName,
            GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository.StuckFilter(null));

        AssertReadsOnlyTheStuckIndex(explain, GlobalProductIdentityWorkflowOperationRepository.StuckIndexName);
    }

    [Fact]
    public async Task GPIF3_The_correction_stuck_scan_reads_only_its_partial_indexes_beside_finished_history()
    {
        var store = new GlobalProductCorrectionOperationRepository(host.Database, Tenant());
        await host.Database.GetCollection<GlobalProductCorrectionOperation>(GlobalProductCorrectionOperationRepository.CollectionName)
            .InsertManyAsync(Enumerable.Range(0, GpNoise).Select(_ => new GlobalProductCorrectionOperation
            {
                Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), OperationId = Guid.NewGuid(), GlobalProductId = Guid.NewGuid(),
                StartIdempotencyKey = Guid.NewGuid().ToString("N"), Checkpoint = GlobalProductCorrectionCheckpoint.Completed
            }));
        await store.DiscoverStuckTenantPartitionsAsync(null, 25); // makes the indexes, once

        // The scan's two reads, each explained: each stays inside its own partial index.
        AssertReadsOnlyTheStuckIndex(await ExplainStuckScanAsync(GlobalProductCorrectionOperationRepository.CollectionName,
            GlobalProductCorrectionOperationRepository.StuckManualFilter(null)), GlobalProductCorrectionOperationRepository.StuckManualIndexName);
        AssertReadsOnlyTheStuckIndex(await ExplainStuckScanAsync(GlobalProductCorrectionOperationRepository.CollectionName,
            GlobalProductCorrectionOperationRepository.StuckAtLimitFilter(null)), GlobalProductCorrectionOperationRepository.StuckAtLimitIndexName);
    }

    [Fact]
    public async Task GPIF3_The_retirement_stuck_scan_reads_only_its_partial_indexes_beside_finished_history()
    {
        var store = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());
        await host.Database.GetCollection<GlobalProductRetirementRequestOperation>(
                GlobalProductRetirementRequestOperationRepository.CollectionName)
            .InsertManyAsync(Enumerable.Range(0, GpNoise).Select(_ => new GlobalProductRetirementRequestOperation
            {
                Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), OperationId = Guid.NewGuid(), GlobalProductId = Guid.NewGuid(),
                StartIdempotencyKey = Guid.NewGuid().ToString("N"), Checkpoint = GlobalProductRetirementRequestCheckpoint.Completed
            }));
        await store.DiscoverStuckTenantPartitionsAsync(null, 25);

        // The scan's two reads, each explained: each stays inside its own partial index.
        AssertReadsOnlyTheStuckIndex(await ExplainStuckScanAsync(GlobalProductRetirementRequestOperationRepository.CollectionName,
            GlobalProductRetirementRequestOperationRepository.StuckManualFilter(null)), GlobalProductRetirementRequestOperationRepository.StuckManualIndexName);
        AssertReadsOnlyTheStuckIndex(await ExplainStuckScanAsync(GlobalProductRetirementRequestOperationRepository.CollectionName,
            GlobalProductRetirementRequestOperationRepository.StuckAtLimitFilter(null)), GlobalProductRetirementRequestOperationRepository.StuckAtLimitIndexName);
    }

    // The stuck scan still FINDS what is stuck, past the noise.
    [Fact]
    public async Task GPIF3_The_correction_stuck_scan_finds_a_stuck_request()
    {
        var (_, key) = await GpAwaitingDecisionAsync();
        host.Engine.Open(GpCorrectionObject, key.ToString("D")).EvidenceInconsistent = true;
        Assert.Equal(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired, (await GpWorkerRunsAsync(key, 2)).Checkpoint);

        var page = await new GlobalProductCorrectionOperationRepository(host.Database, Tenant())
            .DiscoverStuckTenantPartitionsAsync(null, 100);

        Assert.Contains(_tenant, page.TenantIds);
    }

    // The two reads merged: tenants stuck either way, paged two at a time, each found exactly once.
    [Fact]
    public async Task GPIF3_The_retirement_stuck_scan_pages_both_kinds_of_stuck_request_without_loss_or_repeat()
    {
        var operations = host.Database.GetCollection<GlobalProductRetirementRequestOperation>(
            GlobalProductRetirementRequestOperationRepository.CollectionName);
        var tenants = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToList();
        await operations.InsertManyAsync(tenants.SelectMany((tenant, index) => Enumerable.Range(0, 2).Select(_ =>
        {
            var id = Guid.NewGuid();
            var operation = new GlobalProductRetirementRequestOperation
            {
                Id = id, TenantId = tenant, OperationId = id, GlobalProductId = Guid.NewGuid(),
                StartIdempotencyKey = Guid.NewGuid().ToString("N")
            };
            if (index % 2 == 0)
            {
                operation.Checkpoint = GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired;
                operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired;
            }
            else
            {
                operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.Retryable;
                operation.LastFailureCode = Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow
                    .GlobalProductRetirementRequestWorkflowProcessor.RetryLimitReached;
            }
            return operation;
        })));
        var store = new GlobalProductRetirementRequestOperationRepository(host.Database, Tenant());

        var found = new List<Guid>();
        Guid? cursor = null;
        var pages = 0;
        do
        {
            var page = await store.DiscoverStuckTenantPartitionsAsync(cursor, 2);
            found.AddRange(page.TenantIds);
            cursor = page.NextAfterTenantId;
        } while (cursor.HasValue && ++pages < 100);

        Assert.All(tenants, tenant => Assert.Single(found, tenant));
        Assert.Equal(found.Count, found.Distinct().Count());
    }
}
