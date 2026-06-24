using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class QmsBaselineManualStructureBuilderTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Correlation = "fu04-corr-001";

    [Fact]
    public async Task Manual_create_adds_draft_baseline_without_import()
    {
        var baselineRepo = new FakeBaselineReleaseRepository();
        var handler = new CreateManualQmsBaselineHandler(baselineRepo, Resolved());

        var response = await handler.Handle(
            new CreateManualQmsBaselineCommand(new ManualQmsBaselineRequestModel("2.0", "Manual QMS", "Manual structure", null), Correlation),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Single(baselineRepo.Items);
        Assert.Equal(BaselineReleaseStatus.Draft, baselineRepo.Items[0].Status);
        Assert.StartsWith("BR-MAN-", baselineRepo.Items[0].BaselineReleaseId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_root_and_child_definition_under_draft_baseline()
    {
        var baseline = DraftBaseline();
        var baselineRepo = new FakeBaselineReleaseRepository([baseline]);
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var handler = new CreateQmsBaselineDefinitionHandler(baselineRepo, definitionRepo, new QmsManualStructureService(), Resolved());

        var root = await handler.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Quality", null), Correlation), CancellationToken.None);
        var child = await handler.Handle(
            new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Versioning & Check-in/Check-out", root.Data!.CanonicalId), Correlation),
            CancellationToken.None);

        Assert.True(root.IsSuccessful);
        Assert.True(child.IsSuccessful);
        Assert.Equal("Quality", root.Data!.FullPath);
        Assert.Equal("Quality/Versioning & Check-in/Check-out", child.Data!.FullPath);
        Assert.Equal(root.Data.CanonicalId, child.Data.ParentCanonicalId);
    }

    [Fact]
    public async Task Duplicate_active_sibling_is_conflict()
    {
        var baseline = DraftBaseline();
        var baselineRepo = new FakeBaselineReleaseRepository([baseline]);
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var handler = new CreateQmsBaselineDefinitionHandler(baselineRepo, definitionRepo, new QmsManualStructureService(), Resolved());

        _ = await handler.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Quality", null), Correlation), CancellationToken.None);
        var duplicate = await handler.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("quality", null), Correlation), CancellationToken.None);

        Assert.False(duplicate.IsSuccessful);
        Assert.Equal(409, duplicate.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.Conflict, duplicate.ReasonCode);
    }

    [Fact]
    public async Task Published_baseline_rejects_manual_edit()
    {
        var baseline = DraftBaseline();
        baseline.Status = BaselineReleaseStatus.Published;
        var handler = new CreateQmsBaselineDefinitionHandler(
            new FakeBaselineReleaseRepository([baseline]),
            new FakeCollectionDefinitionRepository(),
            new QmsManualStructureService(),
            Resolved());

        var response = await handler.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Quality", null), Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, response.ReasonCode);
    }

    [Fact]
    public async Task Move_recalculates_full_path_and_preserves_canonical_id()
    {
        var baseline = DraftBaseline();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var create = new CreateQmsBaselineDefinitionHandler(
            new FakeBaselineReleaseRepository([baseline]),
            definitionRepo,
            new QmsManualStructureService(),
            Resolved());
        var root = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Quality", null), Correlation), CancellationToken.None)).Data!;
        var target = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("SOP", null), Correlation), CancellationToken.None)).Data!;
        var move = new MoveQmsBaselineDefinitionHandler(
            new FakeBaselineReleaseRepository([baseline]),
            definitionRepo,
            new QmsManualStructureService(),
            Resolved());

        var response = await move.Handle(
            new MoveQmsBaselineDefinitionCommand(baseline.Id, target.CanonicalId, new QmsCollectionDefinitionMoveModel(root.CanonicalId, 7, target.VersionToken), Correlation),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(target.CanonicalId, response.Data!.CanonicalId);
        Assert.Equal("Quality/SOP", response.Data.FullPath);
        Assert.Equal(7, response.Data.DisplayOrder);
    }

    [Fact]
    public async Task Move_rejects_target_parent_that_disallows_manual_children()
    {
        var baseline = DraftBaseline();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var create = new CreateQmsBaselineDefinitionHandler(
            new FakeBaselineReleaseRepository([baseline]),
            definitionRepo,
            new QmsManualStructureService(),
            Resolved());
        var lockedParent = (await create.Handle(
            new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Locked", null) with { AllowsManualChildren = false }, Correlation),
            CancellationToken.None)).Data!;
        var target = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("SOP", null), Correlation), CancellationToken.None)).Data!;
        var move = new MoveQmsBaselineDefinitionHandler(
            new FakeBaselineReleaseRepository([baseline]),
            definitionRepo,
            new QmsManualStructureService(),
            Resolved());

        var response = await move.Handle(
            new MoveQmsBaselineDefinitionCommand(baseline.Id, target.CanonicalId, new QmsCollectionDefinitionMoveModel(lockedParent.CanonicalId, 0, target.VersionToken), Correlation),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, response.ReasonCode);
        Assert.Contains("manual children", response.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_disabling_manual_children_promotes_direct_children_to_root()
    {
        var baseline = DraftBaseline();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var baselineRepo = new FakeBaselineReleaseRepository([baseline]);
        var service = new QmsManualStructureService();
        var create = new CreateQmsBaselineDefinitionHandler(baselineRepo, definitionRepo, service, Resolved());
        var parent = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Parent", null), Correlation), CancellationToken.None)).Data!;
        var child = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Child", parent.CanonicalId), Correlation), CancellationToken.None)).Data!;
        var grandchild = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Grandchild", child.CanonicalId), Correlation), CancellationToken.None)).Data!;
        var update = new UpdateQmsBaselineDefinitionHandler(baselineRepo, definitionRepo, service, Resolved());

        var response = await update.Handle(
            new UpdateQmsBaselineDefinitionCommand(
                baseline.Id,
                parent.CanonicalId,
                Upsert("Parent", null) with { AllowsManualChildren = false, VersionToken = parent.VersionToken },
                Correlation),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var promotedChild = definitionRepo.Items.Single(x => x.CanonicalId == child.CanonicalId);
        var nestedGrandchild = definitionRepo.Items.Single(x => x.CanonicalId == grandchild.CanonicalId);
        Assert.Null(promotedChild.ParentCanonicalId);
        Assert.Equal("Child", promotedChild.FullPath);
        Assert.Equal(child.CanonicalId, nestedGrandchild.ParentCanonicalId);
        Assert.Equal("Child/Grandchild", nestedGrandchild.FullPath);
        Assert.False(definitionRepo.Items.Single(x => x.CanonicalId == parent.CanonicalId).AllowsManualChildren);
    }

    [Fact]
    public async Task Soft_delete_marks_definition_without_hard_delete()
    {
        var baseline = DraftBaseline();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        var create = new CreateQmsBaselineDefinitionHandler(
            new FakeBaselineReleaseRepository([baseline]),
            definitionRepo,
            new QmsManualStructureService(),
            Resolved());
        var root = (await create.Handle(new CreateQmsBaselineDefinitionCommand(baseline.Id, Upsert("Quality", null), Correlation), CancellationToken.None)).Data!;
        var delete = new DeleteQmsBaselineDefinitionHandler(new FakeBaselineReleaseRepository([baseline]), definitionRepo, Resolved());

        var response = await delete.Handle(new DeleteQmsBaselineDefinitionCommand(baseline.Id, root.CanonicalId, root.VersionToken, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Single(definitionRepo.Items);
        Assert.True(definitionRepo.Items[0].IsDeleted);
        Assert.NotNull(definitionRepo.Items[0].DeletedAt);
    }

    [Fact]
    public async Task Validate_draft_reports_duplicate_sibling_and_orphan_parent()
    {
        var baseline = DraftBaseline();
        var definitionRepo = new FakeCollectionDefinitionRepository();
        definitionRepo.Items.AddRange(
        [
            Definition(baseline.Id, "a", null, "Quality"),
            Definition(baseline.Id, "b", null, "quality"),
            Definition(baseline.Id, "c", "missing", "Child")
        ]);
        var handler = new ValidateQmsBaselineDraftHandler(
            new FakeBaselineReleaseRepository([baseline]),
            definitionRepo,
            new QmsManualStructureService(),
            Resolved());

        var response = await handler.Handle(new ValidateQmsBaselineDraftCommand(baseline.Id, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.Valid);
        Assert.NotEmpty(response.Data.DuplicateSiblingFindings);
        Assert.NotEmpty(response.Data.OrphanParentFindings);
    }

    private static QmsCollectionDefinitionUpsertModel Upsert(string name, string? parentCanonicalId) =>
        new(name, parentCanonicalId, null, null, null, null, null, 0, true, false, false, false, 0);

    private static TenantContext Resolved()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(TenantId);
        return ctx;
    }

    private static BaselineRelease DraftBaseline() => new()
    {
        TenantId = TenantId,
        BaselineReleaseId = "BR-MAN-TEST0001",
        SourceBaselineKey = "manual:test",
        BaselineVersion = "1.0",
        Status = BaselineReleaseStatus.Draft
    };

    private static CollectionDefinition Definition(Guid baselineId, string canonicalId, string? parentCanonicalId, string name) => new()
    {
        TenantId = TenantId,
        BaselineReleaseId = baselineId,
        CanonicalId = canonicalId,
        ParentCanonicalId = parentCanonicalId,
        Name = name,
        PathSegment = name,
        FullPath = name,
        DefinitionHash = "hash"
    };

    private sealed class FakeBaselineReleaseRepository : IBaselineReleaseRepository
    {
        public List<BaselineRelease> Items { get; }

        public FakeBaselineReleaseRepository(IReadOnlyList<BaselineRelease>? items = null)
        {
            Items = items?.ToList() ?? [];
        }

        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default)
        {
            Items.Add(baseline);
            return Task.FromResult(baseline);
        }

        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BaselineRelease>>(Items);

        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default)
        {
            baseline.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Items { get; } = [];

        public Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default)
        {
            Items.Add(definition);
            return Task.FromResult(definition);
        }

        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
        {
            Items.AddRange(definitions);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionDefinition>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId && !x.IsDeleted).ToList());

        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId && !x.IsDeleted));

        public Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default)
        {
            if (definition.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            definition.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }

        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
        {
            foreach (var definition in definitions)
            {
                definition.Version++;
            }

            return Task.CompletedTask;
        }

        public Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default)
        {
            if (definition.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            definition.IsDeleted = true;
            definition.DeletedAt = DateTimeOffset.UtcNow;
            definition.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }
}
