using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — the segment aggregate: lifecycle, versioning, the criteria freeze, tenant isolation, concurrency and
/// the absence of any hard-delete path.
/// </summary>
public sealed class SegmentAggregateTests
{
    private readonly FakeSegmentRepository _segments = new();
    private readonly FakeProductReferenceValidator _references = new();

    private CreateSegmentHandler Create(Guid tenant = default) => new(
        SegmentTestDoubles.Tenant(tenant == default ? SegmentTestDoubles.TenantA : tenant),
        new NullActorContext(), _segments, _references);

    private UpdateSegmentHandler Update() => new(
        SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantA), new NullActorContext(), _segments, _references);

    private ActivateSegmentHandler Activate() => new(
        SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantA), new NullActorContext(), _segments);

    private ArchiveSegmentHandler Archive() => new(
        SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantA), new NullActorContext(), _segments);

    private CreateSegmentVersionHandler NewVersion() => new(
        SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantA), new NullActorContext(), _segments);

    private static CreateSegmentCommand NewSegment(
        string code = "onc-cardio",
        string type = SegmentTypes.Dynamic,
        string subjectType = SegmentSubjectTypes.Contact,
        List<SegmentCriteriaNodeInput>? criteria = null)
        => new(code, "Cardiology", type, subjectType, SegmentMatchModes.All,
            SegmentTestDoubles.Past, null, null, null, null,
            criteria ?? SegmentTestBuilders.SpecialtyIs("cardiology"));

    [Fact]
    public async Task Create_starts_as_draft_version_one_and_its_own_lineage_root()
    {
        var response = await Create().Handle(NewSegment(), default);

        Assert.True(response.IsSuccessful);
        var segment = _segments.Rows.Single();
        Assert.Equal(SegmentStatuses.Draft, segment.SegmentStatus);
        Assert.Equal(1, segment.SegmentVersion);
        Assert.Equal(segment.Id, segment.VersionLineageId);
        Assert.Null(segment.CriteriaFrozenAt);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code_but_the_same_code_is_free_in_another_tenant()
    {
        await Create().Handle(NewSegment(), default);

        var duplicate = await Create().Handle(NewSegment(), default);
        Assert.Equal(409, duplicate.StatusCode);

        var otherTenant = await Create(SegmentTestDoubles.TenantB).Handle(NewSegment(), default);
        Assert.True(otherTenant.IsSuccessful);
    }

    [Fact]
    public async Task Create_forbids_criteria_on_a_static_segment_and_requires_them_on_a_dynamic_one()
    {
        var staticWithCriteria = await Create().Handle(
            NewSegment("static-a", SegmentTypes.Static), default);
        Assert.Equal(400, staticWithCriteria.StatusCode);

        var dynamicWithoutCriteria = await Create().Handle(
            NewSegment("dyn-a", SegmentTypes.Dynamic, criteria: new List<SegmentCriteriaNodeInput>()), default);
        Assert.Equal(400, dynamicWithoutCriteria.StatusCode);

        var staticWithoutCriteria = await Create().Handle(
            NewSegment("static-b", SegmentTypes.Static, criteria: new List<SegmentCriteriaNodeInput>()), default);
        Assert.True(staticWithoutCriteria.IsSuccessful);
    }

    [Fact]
    public async Task Update_cannot_move_the_lifecycle_and_cannot_touch_frozen_criteria()
    {
        var created = await Create().Handle(NewSegment(), default);
        var id = created.Data;

        var toActive = await Update().Handle(
            new UpdateSegmentCommand(id, "Cardiology", SegmentTypes.Dynamic, SegmentStatuses.Active,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, null, null,
                CriteriaProvided: false, null, null), default);
        Assert.Equal(400, toActive.StatusCode);

        await Activate().Handle(new ActivateSegmentCommand(id, null), default);

        var frozen = await Update().Handle(
            new UpdateSegmentCommand(id, "Cardiology", SegmentTypes.Dynamic, SegmentStatuses.Active,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, null, null,
                CriteriaProvided: true, SegmentTestBuilders.SpecialtyIs("oncology"), null), default);

        Assert.Equal(409, frozen.StatusCode);
        Assert.Contains(SegmentErrorCodes.CriteriaFrozen, frozen.Errors!);
    }

    [Fact]
    public async Task Update_of_metadata_is_allowed_on_an_active_segment_and_resending_the_same_rule_is_not_a_change()
    {
        var created = await Create().Handle(NewSegment(), default);
        var id = created.Data;
        await Activate().Handle(new ActivateSegmentCommand(id, null), default);

        var stored = _segments.Rows.Single();
        var sameTree = stored.Criteria
            .Select(n => new SegmentCriteriaNodeInput(
                n.NodeId, n.ParentNodeId, n.NodeKind, n.GroupOperator, n.AttributeCode, n.Operator, n.Values,
                n.ValueType, n.Parameters, n.Negate, n.SortOrder, n.Label))
            .ToList();

        var response = await Update().Handle(
            new UpdateSegmentCommand(id, "Renamed", SegmentTypes.Dynamic, SegmentStatuses.Active,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, "note", null,
                CriteriaProvided: true, sameTree, null), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Renamed", _segments.Rows.Single().SegmentName);
    }

    [Fact]
    public async Task Activate_freezes_the_criteria_and_new_version_clones_with_remapped_node_ids()
    {
        var criteria = new List<SegmentCriteriaNodeInput>();
        var groupId = Guid.NewGuid();
        criteria.Add(SegmentTestBuilders.Group(SegmentGroupOperators.And, groupId));
        criteria.Add(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
            new[] { "cardiology" }, parentNodeId: groupId));

        var created = await Create().Handle(NewSegment(criteria: criteria), default);
        var id = created.Data;

        await Activate().Handle(new ActivateSegmentCommand(id, null), default);
        var original = _segments.Rows.Single(s => s.Id == id);
        Assert.NotNull(original.CriteriaFrozenAt);
        Assert.Equal(SegmentStatuses.Active, original.SegmentStatus);

        var versioned = await NewVersion().Handle(new CreateSegmentVersionCommand(id), default);
        var clone = _segments.Rows.Single(s => s.Id == versioned.Data);

        Assert.Equal(2, clone.SegmentVersion);
        Assert.Equal(original.VersionLineageId, clone.VersionLineageId);
        Assert.Equal(SegmentStatuses.Draft, clone.SegmentStatus);
        Assert.Null(clone.CriteriaFrozenAt);

        // Not one node id survives, and every parent reference points INSIDE the clone: no leak into the old tree.
        var originalIds = original.Criteria.Select(n => n.NodeId).ToHashSet();
        var cloneIds = clone.Criteria.Select(n => n.NodeId).ToHashSet();
        Assert.Empty(originalIds.Intersect(cloneIds));
        Assert.All(
            clone.Criteria.Where(n => n.ParentNodeId is not null),
            n => Assert.Contains(n.ParentNodeId!.Value, cloneIds));
    }

    [Fact]
    public async Task Activating_a_new_version_supersedes_its_predecessor_but_leaves_it_resolvable()
    {
        var created = await Create().Handle(NewSegment(), default);
        var firstId = created.Data;
        await Activate().Handle(new ActivateSegmentCommand(firstId, null), default);

        var versioned = await NewVersion().Handle(new CreateSegmentVersionCommand(firstId), default);
        await Activate().Handle(new ActivateSegmentCommand(versioned.Data, null), default);

        var predecessor = _segments.Rows.Single(s => s.Id == firstId);
        Assert.Equal(versioned.Data, predecessor.SupersededBySegmentId);

        // Superseded, NOT archived: the old rule still resolves, which is what makes a past selection explainable.
        Assert.False(predecessor.IsArchived());
        Assert.True(predecessor.IsActive());
    }

    [Fact]
    public async Task Archived_segments_refuse_updates_and_cannot_be_reactivated()
    {
        var created = await Create().Handle(NewSegment(), default);
        var id = created.Data;

        Assert.True((await Archive().Handle(new ArchiveSegmentCommand(id, null), default)).IsSuccessful);

        var update = await Update().Handle(
            new UpdateSegmentCommand(id, "x", SegmentTypes.Dynamic, SegmentStatuses.Archived,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, null, null,
                CriteriaProvided: false, null, null), default);
        Assert.Equal(409, update.StatusCode);

        var reactivate = await Activate().Handle(new ActivateSegmentCommand(id, null), default);
        Assert.Equal(409, reactivate.StatusCode);
    }

    [Fact]
    public async Task A_stale_expected_version_is_a_conflict_and_never_a_silent_overwrite()
    {
        var created = await Create().Handle(NewSegment(), default);
        var id = created.Data;

        var response = await Update().Handle(
            new UpdateSegmentCommand(id, "x", SegmentTypes.Dynamic, SegmentStatuses.Draft,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, null, null,
                CriteriaProvided: false, null, ExpectedVersion: 99), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal("Cardiology", _segments.Rows.Single().SegmentName);
    }

    [Fact]
    public async Task Another_tenant_sees_a_404_on_get_and_nothing_in_the_list()
    {
        var created = await Create().Handle(NewSegment(), default);

        var get = await new GetSegmentByIdHandler(
                SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantB), _segments)
            .Handle(new GetSegmentByIdQuery(created.Data), default);
        Assert.Equal(404, get.StatusCode);

        var list = await new ListSegmentsHandler(
                SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantB), _segments)
            .Handle(new ListSegmentsQuery(null, null, null, null, null, null, true), default);
        Assert.Empty(list.Data!.Items);
    }

    [Fact]
    public void The_repository_contract_exposes_no_delete_path_at_all()
    {
        var methods = typeof(Domain.Repositories.ISegmentRepository).GetMethods()
            .Concat(typeof(Domain.Repositories.ITargetCustomerRepository).GetMethods())
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain(methods, m => m.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }
}
