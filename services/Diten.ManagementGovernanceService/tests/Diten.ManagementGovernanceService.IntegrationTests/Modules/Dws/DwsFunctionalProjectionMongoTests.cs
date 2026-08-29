using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalProjectionMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task Five_persisted_queries_project_typed_DTOs_and_never_write()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "create");
        var created = await scope.Commands.CreateStructureAsync(new(DwsFunctionalMongoScope.Reference(), "Projection", null), actor, default);
        await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, null, "ROOT", "Root", null, 0, 1),
            actor with { IdempotencyKey = "root-node" },
            default);
        await scope.Commands.CreateStructureBaselineAsync(
            new(created.StructureDefinitionId, 2), actor with { IdempotencyKey = "baseline-1" }, default);
        var next = await scope.Commands.CreateNextStructureRevisionAsync(
            new(created.StructureDefinitionId, null, 1, 2), actor with { IdempotencyKey = "revision-2" }, default);
        await scope.Commands.CreateStructureBaselineAsync(
            new(created.StructureDefinitionId, next.RevisionVersion), actor with { IdempotencyKey = "baseline-2" }, default);
        var query = DwsFunctionalMongoScope.QueryActor(actor);
        var before = await scope.CountsAsync(tenant);

        var summary = await scope.Queries.GetStructureByIdAsync(created.StructureDefinitionId, query, default);
        var tree = await scope.Queries.GetStructureTreeAsync(created.StructureDefinitionId, 1, query, default);
        var validation = await scope.Queries.ValidateStructureAsync(created.StructureDefinitionId, 1, query, default);
        var revisions = await scope.Queries.CompareStructureRevisionsAsync(created.StructureDefinitionId, 1, 2, query, default);
        var baselines = await scope.Queries.CompareStructureBaselinesAsync(created.StructureDefinitionId, 1, 2, query, default);

        Assert.Equal(created.StructureDefinitionId, summary.StructureDefinitionId);
        Assert.Equal(1, tree.RevisionNumber);
        Assert.True(validation.IsValid);
        Assert.Equal((1, 2), (revisions.LeftRevisionNumber, revisions.RightRevisionNumber));
        Assert.Equal((1, 2), (baselines.LeftBaselineNumber, baselines.RightBaselineNumber));
        Assert.Equal(before, await scope.CountsAsync(tenant));
    }

    [Fact]
    public async Task Query_context_with_idempotency_key_is_rejected_without_writes()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "query-guard-create");
        var created = await scope.Commands.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Query guard", null), actor, default);
        var before = await scope.CountsAsync(tenant);

        await Assert.ThrowsAsync<DwsValidationException>(() => scope.Queries.GetStructureByIdAsync(
            created.StructureDefinitionId,
            DwsFunctionalMongoScope.QueryActor(actor) with { IdempotencyKey = "forbidden-query-key" },
            default));

        Assert.Equal(before, await scope.CountsAsync(tenant));
    }

    [Fact]
    public async Task Snapshot_revalidation_rejects_a_changed_version_and_never_writes()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "snapshot-create");
        var created = await scope.Commands.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Snapshot", null), actor, default);
        var snapshot = await scope.QueryStore.LoadRevisionSnapshotAsync(
            tenant, created.StructureDefinitionId, 1, default);
        Assert.NotNull(snapshot);

        await scope.Commands.UpdateStructureMetadataAsync(
            new(created.StructureDefinitionId, "Changed", null, 1),
            actor with { IdempotencyKey = "snapshot-change" },
            default);
        var before = await scope.CountsAsync(tenant);

        var error = await Assert.ThrowsAsync<DwsConflictException>(() =>
            scope.QueryStore.RevalidateSnapshotAsync(snapshot!, default));

        Assert.Equal(DwsErrors.ConcurrencyConflict, error.Code);
        Assert.Equal(before, await scope.CountsAsync(tenant));
    }
}
