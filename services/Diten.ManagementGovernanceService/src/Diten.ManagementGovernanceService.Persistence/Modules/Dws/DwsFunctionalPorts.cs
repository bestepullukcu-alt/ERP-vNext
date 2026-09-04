using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MongoDB.Bson;

namespace Diten.ManagementGovernanceService.Persistence.Modules.Dws;

public sealed class DwsFunctionalCommandPort(
    DwsFunctionalQueryStore queries,
    DwsMongoAtomicWriter writer,
    TimeProvider timeProvider) : IDwsFunctionalCommandPort
{
    public async Task<CreateStructureResult> CreateStructureAsync(CreateStructureRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var metadata = new StructuralMetadata(request.Name, request.Description);
        var hash = Hash(new() { ["externalContextReference"] = Context(request.ExternalContextReference), ["name"] = metadata.Name, ["description"] = metadata.Description });
        var replay = await TryReplayAsync<CreateStructureResult>(actor, DwsCommandFamily.CreateStructure, hash, request.ExternalContextReference, ct);
        if (replay.Found) return replay.Value!;
        var now = Now();
        var definitionId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var result = new CreateStructureResult(definitionId, 1, 1, 1);
        var definition = new DwsDefinitionDocument(definitionId, actor.TenantId, request.ExternalContextReference, 1, 1, 1, now, null, false, null);
        var revision = new DwsRevisionDocument(revisionId, actor.TenantId, definitionId, 1, metadata, false, null, 1, now, null, false, null);
        return await ExecuteAsync(actor, DwsCommandFamily.CreateStructure, hash, result,
        [
            Part("definitions", definitionId, 0, DwsTypedBsonMapper.Values(definition)),
            Part("revisions", revisionId, 0, DwsTypedBsonMapper.Values(revision))
        ], ct);
    }

    public async Task<UpdateStructureMetadataResult> UpdateStructureMetadataAsync(UpdateStructureMetadataRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var metadata = new StructuralMetadata(request.Name, request.Description);
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["name"] = metadata.Name, ["description"] = metadata.Description, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<UpdateStructureMetadataResult>(actor, DwsCommandFamily.UpdateStructureMetadata, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        if (state.Revision.StructuralMetadata == metadata)
            return new(request.StructureDefinitionId, state.Revision.RevisionNumber, state.Revision.Version, DwsOutcomeKind.NoOp);
        var now = Now();
        var updated = state.Revision with { StructuralMetadata = metadata, Version = checked(state.Revision.Version + 1), UpdatedAtUtc = now };
        var result = new UpdateStructureMetadataResult(request.StructureDefinitionId, updated.RevisionNumber, updated.Version, DwsOutcomeKind.Succeeded);
        return await ExecuteAsync(actor, DwsCommandFamily.UpdateStructureMetadata, hash, result,
            [Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated))], ct);
    }

    public async Task<AddStructureNodeResult> AddStructureNodeAsync(AddStructureNodeRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var code = DwsText.Required(request.Code, 100);
        var title = DwsText.Required(request.Title, 300);
        var description = DwsText.Optional(request.Description, 4000);
        if (request.SiblingOrder < 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["parentLogicalNodeId"] = request.ParentLogicalNodeId, ["code"] = code, ["title"] = title, ["description"] = description, ["siblingOrder"] = request.SiblingOrder, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<AddStructureNodeResult>(actor, DwsCommandFamily.AddStructureNode, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        var node = StructureNode.Create(actor.TenantId, state.Revision.Id, request.ParentLogicalNodeId, code, title, description, request.SiblingOrder);
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        var nodeDoc = new DwsNodeDocument(node.Id, actor.TenantId, state.Revision.Id, node.LogicalNodeId, node.ParentLogicalNodeId, node.Code, node.Title, node.Description, node.SiblingOrder, 1, Now(), null, false, null);
        Validate(state.Nodes.Append(nodeDoc), state.Dependencies, actor.TenantId, state.Revision.Id);
        var updated = Bump(state.Revision, Now());
        var result = new AddStructureNodeResult(request.StructureDefinitionId, updated.RevisionNumber, node.LogicalNodeId, updated.Version);
        return await ExecuteAsync(actor, DwsCommandFamily.AddStructureNode, hash, result,
        [
            Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated)),
            Part("nodes", nodeDoc.Id, 0, DwsTypedBsonMapper.Values(nodeDoc))
        ], ct);
    }

    public async Task<MoveStructureNodeResult> MoveStructureNodeAsync(MoveStructureNodeRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["logicalNodeId"] = request.LogicalNodeId, ["newParentLogicalNodeId"] = request.NewParentLogicalNodeId, ["newSiblingOrder"] = request.NewSiblingOrder, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<MoveStructureNodeResult>(actor, DwsCommandFamily.MoveStructureNode, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        var node = state.Nodes.SingleOrDefault(value => value.LogicalNodeId == request.LogicalNodeId) ?? throw new DwsNotFoundException();
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        if (node.ParentLogicalNodeId == request.NewParentLogicalNodeId && node.SiblingOrder == request.NewSiblingOrder)
            return new(request.StructureDefinitionId, state.Revision.RevisionNumber, node.LogicalNodeId, node.ParentLogicalNodeId, node.SiblingOrder, state.Revision.Version, DwsOutcomeKind.NoOp);
        var now = Now();
        var moved = node with { ParentLogicalNodeId = request.NewParentLogicalNodeId, SiblingOrder = request.NewSiblingOrder, Version = checked(node.Version + 1), UpdatedAtUtc = now };
        Validate(state.Nodes.Select(value => value.Id == node.Id ? moved : value), state.Dependencies, actor.TenantId, state.Revision.Id);
        var updated = Bump(state.Revision, now);
        var result = new MoveStructureNodeResult(request.StructureDefinitionId, updated.RevisionNumber, moved.LogicalNodeId, moved.ParentLogicalNodeId, moved.SiblingOrder, updated.Version, DwsOutcomeKind.Succeeded);
        return await ExecuteAsync(actor, DwsCommandFamily.MoveStructureNode, hash, result,
        [
            Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated)),
            Part("nodes", moved.Id, node.Version, DwsTypedBsonMapper.Values(moved))
        ], ct);
    }

    public async Task<ReorderStructureNodeResult> ReorderStructureNodeAsync(ReorderStructureNodeRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["logicalNodeId"] = request.LogicalNodeId, ["siblingOrder"] = request.SiblingOrder, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<ReorderStructureNodeResult>(actor, DwsCommandFamily.ReorderStructureNode, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        var node = state.Nodes.SingleOrDefault(value => value.LogicalNodeId == request.LogicalNodeId) ?? throw new DwsNotFoundException();
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        if (node.SiblingOrder == request.SiblingOrder)
            return new(request.StructureDefinitionId, state.Revision.RevisionNumber, node.LogicalNodeId, node.SiblingOrder, state.Revision.Version, DwsOutcomeKind.NoOp);
        var now = Now();
        var reordered = node with { SiblingOrder = request.SiblingOrder, Version = checked(node.Version + 1), UpdatedAtUtc = now };
        Validate(state.Nodes.Select(value => value.Id == node.Id ? reordered : value), state.Dependencies, actor.TenantId, state.Revision.Id);
        var updated = Bump(state.Revision, now);
        var result = new ReorderStructureNodeResult(request.StructureDefinitionId, updated.RevisionNumber, reordered.LogicalNodeId, reordered.SiblingOrder, updated.Version, DwsOutcomeKind.Succeeded);
        return await ExecuteAsync(actor, DwsCommandFamily.ReorderStructureNode, hash, result,
        [
            Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated)),
            Part("nodes", reordered.Id, node.Version, DwsTypedBsonMapper.Values(reordered))
        ], ct);
    }

    public async Task<RemoveStructureNodeResult> RemoveStructureNodeAsync(RemoveStructureNodeRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["logicalNodeId"] = request.LogicalNodeId, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<RemoveStructureNodeResult>(actor, DwsCommandFamily.RemoveStructureNode, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        var node = state.Nodes.SingleOrDefault(value => value.LogicalNodeId == request.LogicalNodeId) ?? throw new DwsNotFoundException();
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        if (state.Nodes.Any(value => value.ParentLogicalNodeId == node.LogicalNodeId)) throw new DwsConflictException(DwsErrors.NodeHasChildren);
        var now = Now();
        var deleted = node with { IsDeleted = true, DeletedAtUtc = now, UpdatedAtUtc = now, Version = checked(node.Version + 1) };
        var updated = Bump(state.Revision, now);
        var result = new RemoveStructureNodeResult(request.StructureDefinitionId, updated.RevisionNumber, node.LogicalNodeId, true, updated.Version);
        return await ExecuteAsync(actor, DwsCommandFamily.RemoveStructureNode, hash, result,
        [
            Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated)),
            Part("nodes", deleted.Id, node.Version, DwsTypedBsonMapper.Values(deleted)),
            new("dependencies", Guid.NewGuid(), 0, new BsonDocument { ["StructureRevisionId"] = DwsMongoGuid.Canonical(state.Revision.Id), ["LogicalNodeId"] = DwsMongoGuid.Canonical(node.LogicalNodeId), ["DeletedAtUtc"] = new BsonDateTime(now) }, DwsMongoWriteMode.SoftDeleteIncidentDependencies)
        ], ct);
    }

    public async Task<AddStructuralDependencyResult> AddStructuralDependencyAsync(AddStructuralDependencyRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["fromLogicalNodeId"] = request.FromLogicalNodeId, ["toLogicalNodeId"] = request.ToLogicalNodeId, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<AddStructuralDependencyResult>(actor, DwsCommandFamily.AddStructuralDependency, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        var now = Now();
        var dependency = new DwsDependencyDocument(Guid.NewGuid(), actor.TenantId, state.Revision.Id, request.FromLogicalNodeId, request.ToLogicalNodeId, 1, now, null, false, null);
        Validate(state.Nodes, state.Dependencies.Append(dependency), actor.TenantId, state.Revision.Id);
        var updated = Bump(state.Revision, now);
        var result = new AddStructuralDependencyResult(request.StructureDefinitionId, updated.RevisionNumber, request.FromLogicalNodeId, request.ToLogicalNodeId, updated.Version);
        return await ExecuteAsync(actor, DwsCommandFamily.AddStructuralDependency, hash, result,
        [
            Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated)),
            Part("dependencies", dependency.Id, 0, DwsTypedBsonMapper.Values(dependency))
        ], ct);
    }

    public async Task<RemoveStructuralDependencyResult> RemoveStructuralDependencyAsync(RemoveStructuralDependencyRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["fromLogicalNodeId"] = request.FromLogicalNodeId, ["toLogicalNodeId"] = request.ToLogicalNodeId, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<RemoveStructuralDependencyResult>(actor, DwsCommandFamily.RemoveStructuralDependency, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        var dependency = state.Dependencies.SingleOrDefault(value => value.FromLogicalNodeId == request.FromLogicalNodeId && value.ToLogicalNodeId == request.ToLogicalNodeId) ?? throw new DwsNotFoundException();
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        var now = Now();
        var deleted = dependency with { IsDeleted = true, DeletedAtUtc = now, UpdatedAtUtc = now, Version = checked(dependency.Version + 1) };
        var updated = Bump(state.Revision, now);
        var result = new RemoveStructuralDependencyResult(request.StructureDefinitionId, updated.RevisionNumber, request.FromLogicalNodeId, request.ToLogicalNodeId, true, updated.Version);
        return await ExecuteAsync(actor, DwsCommandFamily.RemoveStructuralDependency, hash, result,
        [
            Part("revisions", updated.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(updated)),
            Part("dependencies", deleted.Id, dependency.Version, DwsTypedBsonMapper.Values(deleted))
        ], ct);
    }

    public async Task<CreateStructureBaselineResult> CreateStructureBaselineAsync(CreateStructureBaselineRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["expectedRevisionVersion"] = request.ExpectedRevisionVersion });
        var replay = await TryReplayAsync<CreateStructureBaselineResult>(actor, DwsCommandFamily.CreateStructureBaseline, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var state = await WorkingAsync(actor.TenantId, request.StructureDefinitionId, ct);
        RequireExpected(state.Revision.Version, request.ExpectedRevisionVersion);
        var now = Now();
        var nodes = DomainNodes(state.Nodes);
        var dependencies = DomainDependencies(state.Dependencies);
        var baselineNumber = state.Definition.LatestRevisionNumber;
        var baseline = DwsBaselineBuilder.Build(actor.TenantId, state.Definition.Id, state.Revision.RevisionNumber, baselineNumber, state.Definition.ExternalContextReference, state.Revision.StructuralMetadata, nodes, dependencies, now);
        var baselineDoc = new DwsBaselineDocument(baseline.Id, actor.TenantId, state.Definition.Id, baseline.SourceRevisionNumber, baseline.BaselineNumber, baseline.HashAlgorithm, baseline.CanonicalizationVersion, baseline.ContentHash, baseline.Snapshot, 1, now, false, null);
        var revision = state.Revision with { IsSealed = true, SealedAtUtc = now, UpdatedAtUtc = now, Version = checked(state.Revision.Version + 1) };
        var definition = state.Definition with { CurrentWorkingRevisionNumber = null, UpdatedAtUtc = now, Version = checked(state.Definition.Version + 1) };
        var result = new CreateStructureBaselineResult(definition.Id, revision.RevisionNumber, baselineNumber, baseline.ContentHash, baseline.CanonicalizationVersion, definition.Version);
        return await ExecuteAsync(actor, DwsCommandFamily.CreateStructureBaseline, hash, result,
        [
            Part("definitions", definition.Id, state.Definition.Version, DwsTypedBsonMapper.Values(definition)),
            Part("revisions", revision.Id, request.ExpectedRevisionVersion, DwsTypedBsonMapper.Values(revision)),
            Part("baselines", baselineDoc.Id, 0, DwsTypedBsonMapper.Values(baselineDoc))
        ], ct);
    }

    public async Task<CreateNextStructureRevisionResult> CreateNextStructureRevisionAsync(CreateNextStructureRevisionRequest request, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireCommand();
        var hash = Hash(new() { ["structureDefinitionId"] = request.StructureDefinitionId, ["sourceRevisionNumber"] = request.SourceRevisionNumber, ["sourceBaselineNumber"] = request.SourceBaselineNumber, ["expectedDefinitionVersion"] = request.ExpectedDefinitionVersion });
        var replay = await TryReplayAsync<CreateNextStructureRevisionResult>(actor, DwsCommandFamily.CreateNextStructureRevision, hash, null, ct);
        if (replay.Found) return replay.Value!;
        var definition = await queries.FindDefinitionAsync(actor.TenantId, request.StructureDefinitionId, ct) ?? throw new DwsNotFoundException();
        RequireExpected(definition.Version, request.ExpectedDefinitionVersion);
        if (definition.CurrentWorkingRevisionNumber is not null) throw new DwsConflictException(DwsErrors.WorkingRevisionExists);
        var sourceNumber = request.SourceRevisionNumber;
        if (request.SourceBaselineNumber is int baselineNumber)
            sourceNumber = (await queries.FindBaselineAsync(actor.TenantId, definition.Id, baselineNumber, ct) ?? throw new DwsNotFoundException()).SourceRevisionNumber;
        if (sourceNumber is null) throw new DwsValidationException(DwsErrors.InvalidRequest);
        var source = await queries.LoadRevisionSnapshotAsync(actor.TenantId, definition.Id, sourceNumber.Value, ct) ?? throw new DwsNotFoundException();
        if (!source.Revision.IsSealed) throw new DwsConflictException(DwsErrors.ComparisonRequiresSealedRevision);
        var now = Now();
        var revisionNumber = checked(definition.LatestRevisionNumber + 1);
        var revisionId = Guid.NewGuid();
        var nextRevision = new DwsRevisionDocument(revisionId, actor.TenantId, definition.Id, revisionNumber, source.Revision.StructuralMetadata, false, null, 1, now, null, false, null);
        var nextNodes = source.Nodes.Select(value => value with { Id = Guid.NewGuid(), StructureRevisionId = revisionId, Version = 1, CreatedAtUtc = now, UpdatedAtUtc = null, IsDeleted = false, DeletedAtUtc = null }).ToArray();
        var nextDependencies = source.Dependencies.Select(value => value with { Id = Guid.NewGuid(), StructureRevisionId = revisionId, Version = 1, CreatedAtUtc = now, UpdatedAtUtc = null, IsDeleted = false, DeletedAtUtc = null }).ToArray();
        var nextDefinition = definition with { CurrentWorkingRevisionNumber = revisionNumber, LatestRevisionNumber = revisionNumber, UpdatedAtUtc = now, Version = checked(definition.Version + 1) };
        var result = new CreateNextStructureRevisionResult(definition.Id, revisionNumber, nextDefinition.Version, 1);
        return await ExecuteAsync(actor, DwsCommandFamily.CreateNextStructureRevision, hash, result,
        [
            Part("definitions", nextDefinition.Id, definition.Version, DwsTypedBsonMapper.Values(nextDefinition)),
            Part("revisions", nextRevision.Id, 0, DwsTypedBsonMapper.Values(nextRevision)),
            Many("nodes", nextNodes.Select(Document).ToArray()),
            Many("dependencies", nextDependencies.Select(Document).ToArray())
        ], ct);
    }

    private async Task<T> ExecuteAsync<T>(DwsTrustedActorContext actor, DwsCommandFamily family, string hash, T result, IReadOnlyList<DwsMongoParticipant> business, CancellationToken ct)
    {
        var now = Now();
        var idempotencyKey = actor.IdempotencyKey!;
        var kind = DwsOutcomeKind.Succeeded;
        var outcome = Result(family, result!);
        var domainCode = DwsStableOutcome.DomainCode(family, kind);
        var stable = DwsStableOutcome.Build(family, kind, domainCode, outcome);
        var receipt = new DwsReceiptDocument(Guid.NewGuid(), actor.TenantId, actor.SecuritySubjectId, family.ToString(), idempotencyKey, hash, DwsCanonicalJson.RequestVersion, DwsStableOutcome.Version, DwsClosedValues.Outcome(kind), domainCode, stable, now, 1);
        var auditId = Guid.NewGuid();
        var audit = new DwsAuditIntentDocument(Guid.NewGuid(), actor.TenantId, auditId, actor.EffectiveActorId, actor.DelegatedActorId, "dws-structure", EntityId(result!), family.ToString(), now, 1);
        var outbox = new DwsOutboxDocument(Guid.NewGuid(), actor.TenantId, Guid.NewGuid(), auditId, "NON-DELIVERABLE-LOCAL-TEST", null, stable, 1, now);
        var committedOwnResult = await writer.ExecuteAsync(DwsFunctionalMutationComposer.Compose(actor.TenantId, family.ToString(), idempotencyKey, hash, business, receipt, audit, outbox), cancellationToken: ct);
        if (committedOwnResult) return result;
        // A concurrent identical command may have won. The receipt is the stable
        // authority for both the original completion and race reconciliation.
        var authoritative = await TryReplayAsync<T>(actor, family, hash, null, ct);
        return authoritative.Found
            ? authoritative.Value!
            : throw new DwsValidationException(DwsErrors.CommitIndeterminate);
    }

    private async Task<(bool Found, T? Value)> TryReplayAsync<T>(
        DwsTrustedActorContext actor,
        DwsCommandFamily family,
        string hash,
        ExternalContextReference? expectedContext,
        CancellationToken ct)
    {
        var existing = await queries.FindReceiptAsync(actor.TenantId, family.ToString(), actor.IdempotencyKey!, ct);
        if (existing is null) return (false, default);
        if (existing.SecuritySubjectId != actor.SecuritySubjectId) throw new DwsConflictException(DwsErrors.IdempotencySubjectConflict);
        if (existing.RequestPayloadHash != hash) throw new DwsConflictException(DwsErrors.IdempotencyConflict);
        if (existing.RequestCanonicalizationVersion != DwsCanonicalJson.RequestVersion
            || existing.OutcomeSchemaVersion != DwsStableOutcome.Version
            || existing.OutcomeKind != DwsClosedValues.Outcome(DwsOutcomeKind.Succeeded)
            || existing.DomainCode != DwsStableOutcome.DomainCode(family, DwsOutcomeKind.Succeeded))
            throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
        RequireResultType<T>(family);
        DwsStableOutcome.Validate(family, DwsOutcomeKind.Succeeded, existing.DomainCode, existing.StableOutcomeJson);
        try
        {
            using var document = JsonDocument.Parse(existing.StableOutcomeJson);
            var value = DeserializeResult<T>(document.RootElement.GetProperty("result"));
            var definitionId = StructureDefinitionId(value);
            var definition = await queries.FindDefinitionAsync(actor.TenantId, definitionId, ct) ?? throw new DwsNotFoundException();
            if (expectedContext is not null && definition.ExternalContextReference != expectedContext)
                throw new DwsConflictException(DwsErrors.ExternalContextConflict);
            return (true, value);
        }
        catch (JsonException) { throw new DwsValidationException(DwsErrors.InvalidStableOutcome); }
    }
    private static Guid StructureDefinitionId<T>(T value) => value switch
    {
        CreateStructureResult x => x.StructureDefinitionId,
        UpdateStructureMetadataResult x => x.StructureDefinitionId,
        AddStructureNodeResult x => x.StructureDefinitionId,
        MoveStructureNodeResult x => x.StructureDefinitionId,
        ReorderStructureNodeResult x => x.StructureDefinitionId,
        RemoveStructureNodeResult x => x.StructureDefinitionId,
        AddStructuralDependencyResult x => x.StructureDefinitionId,
        RemoveStructuralDependencyResult x => x.StructureDefinitionId,
        CreateStructureBaselineResult x => x.StructureDefinitionId,
        CreateNextStructureRevisionResult x => x.StructureDefinitionId,
        _ => throw new DwsValidationException(DwsErrors.InvalidStableOutcome)
    };
    private static T DeserializeResult<T>(JsonElement result)
    {
        object value = typeof(T) == typeof(UpdateStructureMetadataResult)
            ? new UpdateStructureMetadataResult(
                result.GetProperty("StructureDefinitionId").GetGuid(),
                result.GetProperty("RevisionNumber").GetInt32(),
                result.GetProperty("RevisionVersion").GetInt32(),
                DwsOutcomeKind.Succeeded)
            : typeof(T) == typeof(MoveStructureNodeResult)
                ? new MoveStructureNodeResult(
                    result.GetProperty("StructureDefinitionId").GetGuid(),
                    result.GetProperty("RevisionNumber").GetInt32(),
                    result.GetProperty("LogicalNodeId").GetGuid(),
                    result.GetProperty("ParentLogicalNodeId").ValueKind == JsonValueKind.Null
                        ? null
                        : result.GetProperty("ParentLogicalNodeId").GetGuid(),
                    result.GetProperty("SiblingOrder").GetInt32(),
                    result.GetProperty("RevisionVersion").GetInt32(),
                    DwsOutcomeKind.Succeeded)
                : typeof(T) == typeof(ReorderStructureNodeResult)
                    ? new ReorderStructureNodeResult(
                        result.GetProperty("StructureDefinitionId").GetGuid(),
                        result.GetProperty("RevisionNumber").GetInt32(),
                        result.GetProperty("LogicalNodeId").GetGuid(),
                        result.GetProperty("SiblingOrder").GetInt32(),
                        result.GetProperty("RevisionVersion").GetInt32(),
                        DwsOutcomeKind.Succeeded)
                    : JsonSerializer.Deserialize<T>(result.GetRawText())
                        ?? throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
        return (T)value;
    }
    private static void RequireResultType<T>(DwsCommandFamily family)
    {
        var expected = family switch
        {
            DwsCommandFamily.CreateStructure => typeof(CreateStructureResult),
            DwsCommandFamily.UpdateStructureMetadata => typeof(UpdateStructureMetadataResult),
            DwsCommandFamily.AddStructureNode => typeof(AddStructureNodeResult),
            DwsCommandFamily.MoveStructureNode => typeof(MoveStructureNodeResult),
            DwsCommandFamily.ReorderStructureNode => typeof(ReorderStructureNodeResult),
            DwsCommandFamily.RemoveStructureNode => typeof(RemoveStructureNodeResult),
            DwsCommandFamily.AddStructuralDependency => typeof(AddStructuralDependencyResult),
            DwsCommandFamily.RemoveStructuralDependency => typeof(RemoveStructuralDependencyResult),
            DwsCommandFamily.CreateStructureBaseline => typeof(CreateStructureBaselineResult),
            DwsCommandFamily.CreateNextStructureRevision => typeof(CreateNextStructureRevisionResult),
            _ => throw new DwsValidationException(DwsErrors.InvalidStableOutcome)
        };
        if (typeof(T) != expected) throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
    }

    private async Task<DwsRevisionSnapshot> WorkingAsync(Guid tenantId, Guid definitionId, CancellationToken ct)
    {
        var state = await queries.LoadRevisionSnapshotAsync(tenantId, definitionId, null, ct) ?? throw new DwsNotFoundException();
        if (state.Definition.CurrentWorkingRevisionNumber is null) throw new DwsConflictException(DwsErrors.SealedRevisionImmutable);
        if (state.Revision.IsSealed) throw new DwsConflictException(DwsErrors.SealedRevisionImmutable);
        return state;
    }

    private DateTime Now() => DwsTenantEntity.RequireUtc(timeProvider.GetUtcNow().UtcDateTime);
    private static void RequireExpected(int actual, int expected) { if (actual != expected) throw new DwsConflictException(DwsErrors.ConcurrencyConflict); }
    private static DwsRevisionDocument Bump(DwsRevisionDocument value, DateTime now) => value with { Version = checked(value.Version + 1), UpdatedAtUtc = DwsTenantEntity.RequireUtc(now) };
    private static DwsMongoParticipant Part(string alias, Guid id, int expected, BsonDocument values) => DwsFunctionalMutationComposer.Participant(alias, id, expected, values);
    private static DwsMongoParticipant Many(string alias, IReadOnlyList<BsonDocument> documents) => new(alias, Guid.NewGuid(), 0, new BsonDocument("Documents", new BsonArray(documents)), DwsMongoWriteMode.InsertMany);
    private static BsonDocument Document(DwsNodeDocument value) => Full(value.Id, value.TenantId, DwsTypedBsonMapper.Values(value));
    private static BsonDocument Document(DwsDependencyDocument value) => Full(value.Id, value.TenantId, DwsTypedBsonMapper.Values(value));
    private static BsonDocument Full(Guid id, Guid tenantId, BsonDocument values) { var document = new BsonDocument { ["_id"] = DwsMongoGuid.Canonical(id), ["TenantId"] = DwsMongoGuid.Canonical(tenantId), ["Version"] = 1, ["IsDeleted"] = false }; document.AddRange(values); return document; }
    private static string Hash(Dictionary<string, object?> value) => DwsCanonicalJson.Build(value).Sha256;
    private static Dictionary<string, object?> Context(ExternalContextReference value) => new() { ["contractName"] = value.ContractName, ["contractVersion"] = value.ContractVersion, ["contextKind"] = value.ContextKind, ["contextId"] = value.ContextId };
    private static IReadOnlyList<StructureNode> DomainNodes(IEnumerable<DwsNodeDocument> values) => values.Select(value => StructureNode.Create(value.TenantId, value.StructureRevisionId, value.ParentLogicalNodeId, value.Code, value.Title, value.Description, value.SiblingOrder, value.LogicalNodeId)).ToArray();
    private static IReadOnlyList<StructuralDependency> DomainDependencies(IEnumerable<DwsDependencyDocument> values) => values.Select(value => StructuralDependency.Create(value.TenantId, value.StructureRevisionId, value.FromLogicalNodeId, value.ToLogicalNodeId, value.CreatedAtUtc)).ToArray();
    private static void Validate(IEnumerable<DwsNodeDocument> nodes, IEnumerable<DwsDependencyDocument> dependencies, Guid tenantId, Guid revisionId) { var ns = DomainNodes(nodes); var ds = DomainDependencies(dependencies); DwsStructuralValidator.ValidateHierarchy(tenantId, revisionId, ns); DwsStructuralValidator.ValidateDependencies(tenantId, revisionId, ns, ds); }
    private static string EntityId<T>(T result) => result switch { CreateStructureResult x => x.StructureDefinitionId.ToString("D"), UpdateStructureMetadataResult x => x.StructureDefinitionId.ToString("D"), AddStructureNodeResult x => x.StructureDefinitionId.ToString("D"), MoveStructureNodeResult x => x.StructureDefinitionId.ToString("D"), ReorderStructureNodeResult x => x.StructureDefinitionId.ToString("D"), RemoveStructureNodeResult x => x.StructureDefinitionId.ToString("D"), AddStructuralDependencyResult x => x.StructureDefinitionId.ToString("D"), RemoveStructuralDependencyResult x => x.StructureDefinitionId.ToString("D"), CreateStructureBaselineResult x => x.StructureDefinitionId.ToString("D"), CreateNextStructureRevisionResult x => x.StructureDefinitionId.ToString("D"), _ => throw new DwsValidationException(DwsErrors.InvalidStableOutcome) };
    private static IReadOnlyDictionary<string, object?> Result<T>(DwsCommandFamily family, T value) => value switch
    {
        CreateStructureResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["DefinitionVersion"] = x.DefinitionVersion, ["RevisionVersion"] = x.RevisionVersion },
        UpdateStructureMetadataResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["RevisionVersion"] = x.RevisionVersion },
        AddStructureNodeResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["LogicalNodeId"] = x.LogicalNodeId, ["RevisionVersion"] = x.RevisionVersion },
        MoveStructureNodeResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["LogicalNodeId"] = x.LogicalNodeId, ["ParentLogicalNodeId"] = x.ParentLogicalNodeId, ["SiblingOrder"] = x.SiblingOrder, ["RevisionVersion"] = x.RevisionVersion },
        ReorderStructureNodeResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["LogicalNodeId"] = x.LogicalNodeId, ["SiblingOrder"] = x.SiblingOrder, ["RevisionVersion"] = x.RevisionVersion },
        RemoveStructureNodeResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["LogicalNodeId"] = x.LogicalNodeId, ["Removed"] = x.Removed, ["RevisionVersion"] = x.RevisionVersion },
        AddStructuralDependencyResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["FromLogicalNodeId"] = x.FromLogicalNodeId, ["ToLogicalNodeId"] = x.ToLogicalNodeId, ["RevisionVersion"] = x.RevisionVersion },
        RemoveStructuralDependencyResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["RevisionNumber"] = x.RevisionNumber, ["FromLogicalNodeId"] = x.FromLogicalNodeId, ["ToLogicalNodeId"] = x.ToLogicalNodeId, ["Removed"] = x.Removed, ["RevisionVersion"] = x.RevisionVersion },
        CreateStructureBaselineResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["SourceRevisionNumber"] = x.SourceRevisionNumber, ["BaselineNumber"] = x.BaselineNumber, ["ContentHash"] = x.ContentHash, ["CanonicalizationVersion"] = x.CanonicalizationVersion, ["DefinitionVersion"] = x.DefinitionVersion },
        CreateNextStructureRevisionResult x => new Dictionary<string, object?> { ["StructureDefinitionId"] = x.StructureDefinitionId, ["NewRevisionNumber"] = x.NewRevisionNumber, ["DefinitionVersion"] = x.DefinitionVersion, ["RevisionVersion"] = x.RevisionVersion },
        _ => throw new DwsValidationException(DwsErrors.InvalidStableOutcome)
    };
}

public sealed class DwsFunctionalQueryPort(DwsFunctionalQueryStore queries) : IDwsFunctionalQueryPort
{
    public Task<StructureSummaryDto> GetStructureByIdAsync(Guid id, DwsTrustedActorContext actor, CancellationToken ct) =>
        RevalidatedAsync(actor, id, [null], snapshots => Summary(snapshots[0].Definition), ct);

    public Task<StructureTreeDto> GetStructureTreeAsync(Guid id, int? revisionNumber, DwsTrustedActorContext actor, CancellationToken ct) =>
        RevalidatedAsync(actor, id, [revisionNumber], snapshots => Tree(snapshots[0]), ct);

    public Task<StructureValidationDto> ValidateStructureAsync(Guid id, int? revisionNumber, DwsTrustedActorContext actor, CancellationToken ct) =>
        RevalidatedAsync(actor, id, [revisionNumber], snapshots => Validation(id, actor.TenantId, snapshots[0]), ct);

    public Task<StructureComparisonDto> CompareStructureRevisionsAsync(Guid id, int left, int right, DwsTrustedActorContext actor, CancellationToken ct) =>
        RevalidatedAsync<StructureComparisonDto>(actor, id, [left, right], snapshots =>
        {
            var l = snapshots[0]; var r = snapshots[1];
            if (!l.Revision.IsSealed || !r.Revision.IsSealed) throw new DwsConflictException(DwsErrors.ComparisonRequiresSealedRevision);
            var comparison = Compare(l, r);
            return new StructureComparisonDto(id, left, right, comparison.Nodes, comparison.Dependencies);
        }, ct);

    public async Task<BaselineComparisonDto> CompareStructureBaselinesAsync(Guid id, int left, int right, DwsTrustedActorContext actor, CancellationToken ct)
    {
        actor.RequireQuery();
        var leftBaseline = await queries.FindBaselineAsync(actor.TenantId, id, left, ct) ?? throw new DwsNotFoundException();
        var rightBaseline = await queries.FindBaselineAsync(actor.TenantId, id, right, ct) ?? throw new DwsNotFoundException();
        return await RevalidatedAsync<BaselineComparisonDto>(actor, id, [leftBaseline.SourceRevisionNumber, rightBaseline.SourceRevisionNumber], snapshots =>
        {
            var comparison = Compare(snapshots[0], snapshots[1]);
            return new BaselineComparisonDto(id, left, leftBaseline.ContentHash, right, rightBaseline.ContentHash, comparison.Nodes, comparison.Dependencies);
        }, ct);
    }

    private async Task<TResult> RevalidatedAsync<TResult>(
        DwsTrustedActorContext actor,
        Guid definitionId,
        IReadOnlyList<int?> revisionNumbers,
        Func<IReadOnlyList<DwsRevisionSnapshot>, TResult> project,
        CancellationToken cancellationToken)
    {
        actor.RequireQuery();
        if (revisionNumbers.Count == 0) throw new DwsValidationException(DwsErrors.InvalidRequest);
        var snapshots = new DwsRevisionSnapshot[revisionNumbers.Count];
        for (var index = 0; index < revisionNumbers.Count; index++)
            snapshots[index] = await queries.LoadRevisionSnapshotAsync(actor.TenantId, definitionId, revisionNumbers[index], cancellationToken)
                ?? throw new DwsNotFoundException();

        var result = project(snapshots);
        foreach (var snapshot in snapshots)
            await queries.RevalidateSnapshotAsync(snapshot, cancellationToken);
        return result;
    }

    private static StructureValidationDto Validation(Guid id, Guid tenantId, DwsRevisionSnapshot state)
    {
        var issues = new List<StructureValidationIssueDto>();
        try { DwsStructuralValidator.ValidateHierarchy(tenantId, state.Revision.Id, DomainNodes(state.Nodes)); } catch (DwsConflictException error) { issues.Add(new(error.Code == DwsErrors.HierarchyCycle ? StructureValidationIssueCode.HierarchyCycle : error.Code == DwsErrors.DuplicateNodeCode ? StructureValidationIssueCode.DuplicateNodeCode : StructureValidationIssueCode.DuplicateSiblingOrder, null, null)); } catch (DwsNotFoundException) { issues.Add(new(StructureValidationIssueCode.MissingParent, null, null)); }
        try { DwsStructuralValidator.ValidateDependencies(tenantId, state.Revision.Id, DomainNodes(state.Nodes), DomainDependencies(state.Dependencies)); } catch (DwsConflictException error) { issues.Add(new(error.Code == DwsErrors.DependencyCycle ? StructureValidationIssueCode.DependencyCycle : StructureValidationIssueCode.DuplicateDependency, null, null)); } catch (DwsNotFoundException) { issues.Add(new(StructureValidationIssueCode.MissingDependencyEndpoint, null, null)); }
        return new(id, state.Revision.RevisionNumber, issues.Count == 0, issues);
    }
    private static StructureSummaryDto Summary(DwsDefinitionDocument value) => new(value.Id, value.ExternalContextReference, value.CurrentWorkingRevisionNumber, value.LatestRevisionNumber, value.Version);
    private static StructureTreeDto Tree(DwsRevisionSnapshot value) => new(Summary(value.Definition), value.Revision.RevisionNumber, new(value.Revision.StructuralMetadata.Name, value.Revision.StructuralMetadata.Description), value.Revision.IsSealed, value.Revision.Version, value.Nodes.Select(x => new StructureNodeDto(x.LogicalNodeId, x.ParentLogicalNodeId, x.Code, x.Title, x.Description, x.SiblingOrder)).ToArray(), value.Dependencies.Select(x => new StructuralDependencyDto(x.FromLogicalNodeId, x.ToLogicalNodeId)).ToArray());
    private static (IReadOnlyList<StructureNodeDifferenceDto> Nodes, IReadOnlyList<StructuralDependencyDifferenceDto> Dependencies) Compare(DwsRevisionSnapshot left, DwsRevisionSnapshot right) { var c = DwsComparison.Compare(DomainNodes(left.Nodes), DomainNodes(right.Nodes), DomainDependencies(left.Dependencies), DomainDependencies(right.Dependencies)); return (c.Nodes.Select(x => new StructureNodeDifferenceDto(x.LogicalNodeId, x.Kind)).ToArray(), c.Dependencies.Select(x => new StructuralDependencyDifferenceDto(x.FromLogicalNodeId, x.ToLogicalNodeId, x.Kind)).ToArray()); }
    private static IReadOnlyList<StructureNode> DomainNodes(IEnumerable<DwsNodeDocument> values) => values.Select(value => StructureNode.Create(value.TenantId, value.StructureRevisionId, value.ParentLogicalNodeId, value.Code, value.Title, value.Description, value.SiblingOrder, value.LogicalNodeId)).ToArray();
    private static IReadOnlyList<StructuralDependency> DomainDependencies(IEnumerable<DwsDependencyDocument> values) => values.Select(value => StructuralDependency.Create(value.TenantId, value.StructureRevisionId, value.FromLogicalNodeId, value.ToLogicalNodeId, value.CreatedAtUtc)).ToArray();
}
