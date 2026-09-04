using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsFunctionalCommandMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task All_ten_command_families_replay_identically_after_later_state_changes_without_new_participants()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var reference = DwsFunctionalMongoScope.Reference();
        DwsTrustedActorContext Actor(string key) => scope.CommandActor(tenant, key, subject);

        var createActor = Actor("replay-create");
        var createRequest = new CreateStructureRequest(reference, "Replay matrix", "v1");
        var created = await scope.Commands.CreateStructureAsync(createRequest, createActor, default);

        var metadataActor = Actor("replay-metadata");
        var metadataRequest = new UpdateStructureMetadataRequest(created.StructureDefinitionId, "Replay matrix v2", "v2", 1);
        var metadata = await scope.Commands.UpdateStructureMetadataAsync(metadataRequest, metadataActor, default);

        var addActor = Actor("replay-add-node");
        var addRequest = new AddStructureNodeRequest(created.StructureDefinitionId, null, "ROOT", "Root", null, 0, 2);
        var root = await scope.Commands.AddStructureNodeAsync(addRequest, addActor, default);
        var leaf = await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, null, "LEAF", "Leaf", null, 1, 3),
            Actor("replay-helper-leaf"), default);

        var moveActor = Actor("replay-move");
        var moveRequest = new MoveStructureNodeRequest(created.StructureDefinitionId, leaf.LogicalNodeId, root.LogicalNodeId, 0, 4);
        var moved = await scope.Commands.MoveStructureNodeAsync(moveRequest, moveActor, default);

        var reorderActor = Actor("replay-reorder");
        var reorderRequest = new ReorderStructureNodeRequest(created.StructureDefinitionId, leaf.LogicalNodeId, 1, 5);
        var reordered = await scope.Commands.ReorderStructureNodeAsync(reorderRequest, reorderActor, default);

        var addDependencyActor = Actor("replay-add-dependency");
        var addDependencyRequest = new AddStructuralDependencyRequest(created.StructureDefinitionId, root.LogicalNodeId, leaf.LogicalNodeId, 6);
        var dependency = await scope.Commands.AddStructuralDependencyAsync(addDependencyRequest, addDependencyActor, default);

        var removeDependencyActor = Actor("replay-remove-dependency");
        var removeDependencyRequest = new RemoveStructuralDependencyRequest(created.StructureDefinitionId, root.LogicalNodeId, leaf.LogicalNodeId, 7);
        var removedDependency = await scope.Commands.RemoveStructuralDependencyAsync(removeDependencyRequest, removeDependencyActor, default);

        var removeNodeActor = Actor("replay-remove-node");
        var removeNodeRequest = new RemoveStructureNodeRequest(created.StructureDefinitionId, leaf.LogicalNodeId, 8);
        var removedNode = await scope.Commands.RemoveStructureNodeAsync(removeNodeRequest, removeNodeActor, default);

        var baselineActor = Actor("replay-baseline");
        var baselineRequest = new CreateStructureBaselineRequest(created.StructureDefinitionId, 9);
        var baseline = await scope.Commands.CreateStructureBaselineAsync(baselineRequest, baselineActor, default);

        var nextRevisionActor = Actor("replay-next-revision");
        var nextRevisionRequest = new CreateNextStructureRevisionRequest(
            created.StructureDefinitionId, null, baseline.BaselineNumber, baseline.DefinitionVersion);
        var nextRevision = await scope.Commands.CreateNextStructureRevisionAsync(nextRevisionRequest, nextRevisionActor, default);

        var beforeReplay = await scope.CountsAsync(tenant);

        Assert.Equal(created, await scope.Commands.CreateStructureAsync(createRequest, createActor, default));
        Assert.Equal(metadata, await scope.Commands.UpdateStructureMetadataAsync(metadataRequest, metadataActor, default));
        Assert.Equal(root, await scope.Commands.AddStructureNodeAsync(addRequest, addActor, default));
        Assert.Equal(moved, await scope.Commands.MoveStructureNodeAsync(moveRequest, moveActor, default));
        Assert.Equal(reordered, await scope.Commands.ReorderStructureNodeAsync(reorderRequest, reorderActor, default));
        Assert.Equal(dependency, await scope.Commands.AddStructuralDependencyAsync(addDependencyRequest, addDependencyActor, default));
        Assert.Equal(removedDependency, await scope.Commands.RemoveStructuralDependencyAsync(removeDependencyRequest, removeDependencyActor, default));
        Assert.Equal(removedNode, await scope.Commands.RemoveStructureNodeAsync(removeNodeRequest, removeNodeActor, default));
        Assert.Equal(baseline, await scope.Commands.CreateStructureBaselineAsync(baselineRequest, baselineActor, default));
        Assert.Equal(nextRevision, await scope.Commands.CreateNextStructureRevisionAsync(nextRevisionRequest, nextRevisionActor, default));

        Assert.Equal(beforeReplay, await scope.CountsAsync(tenant));
        Assert.Equal(11, beforeReplay["receipts"]);
        Assert.Equal(11, beforeReplay["audit-intents"]);
        Assert.Equal(11, beforeReplay["outbox"]);
        var receiptFamilies = (await scope.Context.Collection("receipts").Find(
                Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)))
            .ToListAsync())
            .Select(document => document["CommandFamily"].AsString)
            .ToArray();
        Assert.Equal(10, receiptFamilies.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enum.GetNames<DwsCommandFamily>().Order(StringComparer.Ordinal),
            receiptFamilies.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(2, receiptFamilies.Count(family => family == nameof(DwsCommandFamily.AddStructureNode)));

        var summary = await scope.Queries.GetStructureByIdAsync(
            created.StructureDefinitionId, DwsFunctionalMongoScope.QueryActor(createActor), default);
        Assert.Equal(created.StructureDefinitionId, summary.StructureDefinitionId);
        Assert.Equal(reference, summary.ExternalContextReference);
        Assert.Equal(nextRevision.NewRevisionNumber, summary.CurrentWorkingRevisionNumber);

        await scope.Context.Collection("definitions").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(created.StructureDefinitionId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Set("IsDeleted", true).Set("DeletedAtUtc", DateTime.UtcNow));
        var deletedReplay = await Assert.ThrowsAsync<DwsNotFoundException>(() =>
            scope.Commands.RemoveStructureNodeAsync(removeNodeRequest, removeNodeActor, default));
        Assert.Equal(DwsErrors.ResourceNotFound, deletedReplay.Code);
    }

    [Fact]
    public async Task Ten_typed_commands_complete_a_real_transactional_lifecycle()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var subject = Guid.NewGuid();
        DwsTrustedActorContext Actor(string key) => scope.CommandActor(tenant, key, subject);

        var created = await scope.Commands.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Structure", "initial"), Actor("create"), default);
        var first = await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, null, "ROOT", "Root", null, 0, 1), Actor("node-1"), default);
        var second = await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, null, "LEAF", "Leaf", null, 1, 2), Actor("node-2"), default);
        var moved = await scope.Commands.MoveStructureNodeAsync(
            new(created.StructureDefinitionId, second.LogicalNodeId, first.LogicalNodeId, 0, 3), Actor("move"), default);
        var reordered = await scope.Commands.ReorderStructureNodeAsync(
            new(created.StructureDefinitionId, second.LogicalNodeId, 1, 4), Actor("reorder"), default);
        var dependency = await scope.Commands.AddStructuralDependencyAsync(
            new(created.StructureDefinitionId, first.LogicalNodeId, second.LogicalNodeId, 5), Actor("dep-add"), default);
        var removedDependency = await scope.Commands.RemoveStructuralDependencyAsync(
            new(created.StructureDefinitionId, first.LogicalNodeId, second.LogicalNodeId, 6), Actor("dep-remove"), default);
        var removedNode = await scope.Commands.RemoveStructureNodeAsync(
            new(created.StructureDefinitionId, second.LogicalNodeId, 7), Actor("node-remove"), default);
        var metadata = await scope.Commands.UpdateStructureMetadataAsync(
            new(created.StructureDefinitionId, "Structure v2", "updated", 8), Actor("metadata"), default);
        var baseline = await scope.Commands.CreateStructureBaselineAsync(
            new(created.StructureDefinitionId, 9), Actor("baseline"), default);
        var revision = await scope.Commands.CreateNextStructureRevisionAsync(
            new(created.StructureDefinitionId, null, baseline.BaselineNumber, baseline.DefinitionVersion), Actor("revision"), default);

        Assert.Equal(2, revision.NewRevisionNumber);
        Assert.True(removedNode.Removed);
        Assert.True(removedDependency.Removed);
        Assert.Equal(6, dependency.RevisionVersion);
        Assert.Equal("succeeded", metadata.OutcomeKind);
        Assert.Equal(11, (await scope.CountsAsync(tenant))["receipts"]);
        Assert.Equal(11, (await scope.CountsAsync(tenant))["audit-intents"]);
        Assert.Equal(11, (await scope.CountsAsync(tenant))["outbox"]);
        Assert.Equal("succeeded", moved.OutcomeKind);
        Assert.Equal("succeeded", reordered.OutcomeKind);
        var storedNodes = await scope.Context.Collection("nodes").Find(
            Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard))
            & Builders<BsonDocument>.Filter.Eq("LogicalNodeId", new BsonBinaryData(first.LogicalNodeId, GuidRepresentation.Standard)))
            .ToListAsync();
        Assert.NotEmpty(storedNodes);
        Assert.All(storedNodes, storedNode => Assert.NotEqual(
            storedNode["_id"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard),
            storedNode["LogicalNodeId"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard)));
        var outbox = await scope.Context.Collection("outbox").Find(
            Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)))
            .ToListAsync();
        Assert.All(outbox, message => Assert.Equal("NON-DELIVERABLE-LOCAL-TEST", message["DeliveryState"].AsString));
    }

    [Fact]
    public async Task Parent_with_active_child_cannot_be_removed_and_leaves_zero_new_writes()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "leaf-create");
        var created = await scope.Commands.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Leaf guard", null), actor, default);
        var parent = await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, null, "PARENT", "Parent", null, 0, 1),
            actor with { IdempotencyKey = "leaf-parent" }, default);
        await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, parent.LogicalNodeId, "CHILD", "Child", null, 0, 2),
            actor with { IdempotencyKey = "leaf-child" }, default);
        var before = await scope.CountsAsync(tenant);

        var error = await Assert.ThrowsAsync<DwsConflictException>(() => scope.Commands.RemoveStructureNodeAsync(
            new(created.StructureDefinitionId, parent.LogicalNodeId, 3),
            actor with { IdempotencyKey = "leaf-remove" }, default));

        Assert.Equal(DwsErrors.NodeHasChildren, error.Code);
        Assert.Equal(before, await scope.CountsAsync(tenant));
    }

    [Fact]
    public async Task Same_state_metadata_move_and_reorder_are_200_semantic_noops_with_zero_new_writes()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "create");
        var created = await scope.Commands.CreateStructureAsync(new(DwsFunctionalMongoScope.Reference(), "Same", null), actor, default);
        var node = await scope.Commands.AddStructureNodeAsync(
            new(created.StructureDefinitionId, null, "N", "Node", null, 0, 1), actor with { IdempotencyKey = "node" }, default);
        var before = await scope.CountsAsync(tenant);

        var metadata = await scope.Commands.UpdateStructureMetadataAsync(
            new(created.StructureDefinitionId, "Same", null, 2), actor with { IdempotencyKey = "noop-metadata" }, default);
        var move = await scope.Commands.MoveStructureNodeAsync(
            new(created.StructureDefinitionId, node.LogicalNodeId, null, 0, 2), actor with { IdempotencyKey = "noop-move" }, default);
        var reorder = await scope.Commands.ReorderStructureNodeAsync(
            new(created.StructureDefinitionId, node.LogicalNodeId, 0, 2), actor with { IdempotencyKey = "noop-reorder" }, default);

        Assert.Equal("no-op", metadata.OutcomeKind);
        Assert.Equal("no-op", move.OutcomeKind);
        Assert.Equal("no-op", reorder.OutcomeKind);
        Assert.Equal(before, await scope.CountsAsync(tenant));
    }

    [Fact]
    public async Task Replay_CAS_cross_tenant_and_soft_delete_are_fail_closed()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "same-key", Guid.NewGuid());
        var request = new CreateStructureRequest(DwsFunctionalMongoScope.Reference(), "Replay", null);
        var first = await scope.Commands.CreateStructureAsync(request, actor, default);
        var replay = await scope.Commands.CreateStructureAsync(request, actor, default);
        Assert.Equal(first, replay);

        await Assert.ThrowsAsync<DwsConflictException>(() => scope.Commands.CreateStructureAsync(
            new(request.ExternalContextReference, "Changed", null), actor, default));
        await Assert.ThrowsAsync<DwsNotFoundException>(() => scope.Queries.GetStructureByIdAsync(
            first.StructureDefinitionId, DwsFunctionalMongoScope.QueryActor(actor) with { TenantId = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<DwsConflictException>(() => scope.Commands.UpdateStructureMetadataAsync(
            new(first.StructureDefinitionId, "Stale", null, 99), actor with { IdempotencyKey = "stale" }, default));
    }

    [Fact]
    public async Task Cross_tenant_and_soft_deleted_definitions_are_non_disclosing_not_found()
    {
        await using var scope = await DwsFunctionalMongoScope.CreateAsync(mongo);
        var tenant = Guid.NewGuid();
        var actor = scope.CommandActor(tenant, "visibility-create");
        var created = await scope.Commands.CreateStructureAsync(
            new(DwsFunctionalMongoScope.Reference(), "Visibility", null), actor, default);

        await Assert.ThrowsAsync<DwsNotFoundException>(() => scope.Queries.GetStructureByIdAsync(
            created.StructureDefinitionId,
            DwsFunctionalMongoScope.QueryActor(actor) with { TenantId = Guid.NewGuid() },
            default));

        await scope.Context.Collection("definitions").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(created.StructureDefinitionId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Set("IsDeleted", true).Set("DeletedAtUtc", DateTime.UtcNow));
        await Assert.ThrowsAsync<DwsNotFoundException>(() => scope.Queries.GetStructureByIdAsync(
            created.StructureDefinitionId, DwsFunctionalMongoScope.QueryActor(actor), default));
    }
}
