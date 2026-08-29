using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — TargetCustomer is MANUAL membership and nothing else. These tests pin the rules that keep "did a
/// rule or a person put this here?" answerable from the model instead of from a row-by-row reading.
/// </summary>
public sealed class TargetCustomerRulesTests
{
    private static readonly Guid Tenant = SegmentTestDoubles.TenantA;

    private readonly FakeSegmentRepository _segments = new();
    private readonly FakeTargetCustomerRepository _targets = new();

    private AddTargetCustomerHandler Add() => new(
        SegmentTestDoubles.Tenant(Tenant), new NullActorContext(), _segments, _targets);

    private UpdateTargetCustomerHandler Update() => new(
        SegmentTestDoubles.Tenant(Tenant), new NullActorContext(), _segments, _targets);

    private ArchiveTargetCustomerHandler ArchiveRow() => new(
        SegmentTestDoubles.Tenant(Tenant), new NullActorContext(), _targets);

    private Segment Seed(string type = SegmentTypes.Hybrid, string subjectType = SegmentSubjectTypes.Contact)
    {
        var segment = SegmentTestBuilders.Segment(
            Tenant, type: type, subjectType: subjectType,
            criteria: type == SegmentTypes.Static
                ? new List<SegmentCriteriaNode>()
                : SegmentTestBuilders.Criteria(SegmentTestBuilders.SpecialtyIs("cardiology").ToArray()));
        _segments.Rows.Add(segment);
        return segment;
    }

    private static AddTargetCustomerCommand Row(
        Guid segmentId, Guid subjectId, string mode = SegmentMembershipModes.ManualInclude,
        string subjectType = SegmentSubjectTypes.Contact, string reason = "board decision",
        IReadOnlyList<string>? reasonCodes = null)
        => new(segmentId, subjectType, subjectId, mode, reason,
            reasonCodes ?? new[] { SegmentReasonCodes.ManualInclude },
            SegmentTestDoubles.Past, null, "Dr Who", null);

    [Fact]
    public async Task A_dynamic_segment_refuses_a_manual_row_and_says_which_code_it_refused_with()
    {
        var segment = Seed(SegmentTypes.Dynamic);

        var response = await Add().Handle(Row(segment.Id, Guid.NewGuid()), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(SegmentErrorCodes.TypeForbidsManualMembership, response.Errors!);
        Assert.Empty(_targets.Rows);
    }

    [Fact]
    public async Task The_subject_type_must_match_the_segment()
    {
        var segment = Seed(subjectType: SegmentSubjectTypes.Contact);

        var response = await Add().Handle(
            Row(segment.Id, Guid.NewGuid(), subjectType: SegmentSubjectTypes.Account), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(SegmentErrorCodes.SubjectTypeMismatch, response.Errors!);
    }

    [Fact]
    public async Task A_second_live_row_for_the_same_subject_is_a_conflict_rather_than_a_contradiction()
    {
        var segment = Seed();
        var subject = Guid.NewGuid();

        Assert.True((await Add().Handle(Row(segment.Id, subject), default)).IsSuccessful);

        var second = await Add().Handle(
            Row(segment.Id, subject, SegmentMembershipModes.ManualExclude,
                reasonCodes: new[] { SegmentReasonCodes.ManualExclude }), default);

        Assert.Equal(409, second.StatusCode);
        Assert.Single(_targets.Rows);
    }

    [Fact]
    public async Task Switching_include_to_exclude_is_an_update_of_the_one_row()
    {
        var segment = Seed();
        var subject = Guid.NewGuid();
        var created = await Add().Handle(Row(segment.Id, subject), default);

        var response = await Update().Handle(
            new UpdateTargetCustomerCommand(
                segment.Id, created.Data, SegmentMembershipModes.ManualExclude, "escalated",
                new[] { SegmentReasonCodes.ManualExclude }, SegmentTestDoubles.Past, null, null, null, null),
            default);

        Assert.True(response.IsSuccessful);
        var row = Assert.Single(_targets.Rows);
        Assert.True(row.IsExclude());
    }

    [Fact]
    public async Task A_membership_without_a_reason_is_not_authorable()
    {
        var segment = Seed();

        var blank = await Add().Handle(Row(segment.Id, Guid.NewGuid(), reason: "   "), default);
        Assert.Equal(400, blank.StatusCode);

        var noCodes = await Add().Handle(
            Row(segment.Id, Guid.NewGuid(), reasonCodes: Array.Empty<string>()), default);
        Assert.Equal(400, noCodes.StatusCode);

        var unknownCode = await Add().Handle(
            Row(segment.Id, Guid.NewGuid(), reasonCodes: new[] { "because-i-said-so" }), default);
        Assert.Equal(400, unknownCode.StatusCode);
    }

    [Fact]
    public async Task An_archived_row_accepts_no_update_and_cannot_be_archived_twice()
    {
        var segment = Seed();
        var created = await Add().Handle(Row(segment.Id, Guid.NewGuid()), default);

        Assert.True((await ArchiveRow()
            .Handle(new ArchiveTargetCustomerCommand(segment.Id, created.Data, null), default)).IsSuccessful);

        var update = await Update().Handle(
            new UpdateTargetCustomerCommand(
                segment.Id, created.Data, SegmentMembershipModes.ManualInclude, "again",
                new[] { SegmentReasonCodes.ManualInclude }, SegmentTestDoubles.Past, null, null, null, null),
            default);
        Assert.Equal(409, update.StatusCode);

        var again = await ArchiveRow()
            .Handle(new ArchiveTargetCustomerCommand(segment.Id, created.Data, null), default);
        Assert.Equal(409, again.StatusCode);

        // Archived, never deleted: the decision and its reason stay readable.
        Assert.Single(_targets.Rows);
    }

    [Fact]
    public async Task An_archived_row_frees_the_subject_for_a_new_decision()
    {
        var segment = Seed();
        var subject = Guid.NewGuid();
        var created = await Add().Handle(Row(segment.Id, subject), default);
        await ArchiveRow().Handle(new ArchiveTargetCustomerCommand(segment.Id, created.Data, null), default);

        var again = await Add().Handle(Row(segment.Id, subject), default);

        Assert.True(again.IsSuccessful);
        Assert.Equal(2, _targets.Rows.Count);
    }

    [Fact]
    public async Task An_archived_segment_accepts_no_membership_row()
    {
        var segment = Seed();
        segment.SegmentStatus = SegmentStatuses.Archived;
        segment.ArchivedAt = SegmentTestDoubles.Past;

        var response = await Add().Handle(Row(segment.Id, Guid.NewGuid()), default);

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void The_membership_mode_vocabulary_has_exactly_two_values_and_no_derived_third()
    {
        Assert.Equal(2, SegmentMembershipModes.All.Count);
        Assert.Contains(SegmentMembershipModes.ManualInclude, SegmentMembershipModes.All);
        Assert.Contains(SegmentMembershipModes.ManualExclude, SegmentMembershipModes.All);
        Assert.DoesNotContain(SegmentMembershipModes.All, m => m.Contains("derived", StringComparison.Ordinal));
    }
}
