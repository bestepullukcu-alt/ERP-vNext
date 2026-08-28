using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — the IN-SERVICE binding proofs: a play may only point at a segment, a policy and content it is
/// allowed to point at, and every refusal names a machine-readable code.
/// </summary>
public sealed class StrategyTemplateBindingValidationTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private CreateStrategyTemplateHandler Create() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates,
        new StrategyTemplateBindingValidator(_segments, _policies, _paths, _journeys),
        _references);

    private static void AssertCode(string expected, IReadOnlyList<string>? errors)
    {
        Assert.NotNull(errors);
        Assert.Contains(expected, errors!);
    }

    // ---------------- segment ----------------

    [Fact]
    public async Task An_unknown_segment_is_a_400_and_nothing_is_written()
    {
        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(Guid.NewGuid()), default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.SegmentReferenceNotFound, response.Errors);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task A_segment_of_another_tenant_is_not_found_rather_than_leaked()
    {
        var foreign = _segments.Add(StrategyTemplateTestDoubles.TenantB);

        var response = await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(foreign.Id), default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.SegmentReferenceNotFound, response.Errors);
    }

    [Fact]
    public async Task An_archived_segment_cannot_be_bound()
    {
        var archived = _segments.Add(StrategyTemplateTestDoubles.TenantA, archived: true);

        var response = await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(archived.Id), default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.SegmentArchived, response.Errors);
    }

    [Fact]
    public async Task A_segment_whose_subject_type_differs_from_the_template_is_refused()
    {
        var accountSegment = _segments.Add(
            StrategyTemplateTestDoubles.TenantA, subjectType: SegmentSubjectTypes.Account);

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                accountSegment.Id, subjectType: StrategyTemplateSubjectTypes.Contact),
            default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.SegmentSubjectTypeMismatch, response.Errors);
    }

    [Fact]
    public async Task A_draft_segment_may_be_bound_by_a_draft_play()
    {
        var draftSegment = _segments.Add(StrategyTemplateTestDoubles.TenantA, status: SegmentStatuses.Draft);

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(draftSegment.Id), default);

        // Preparing a play alongside its population is normal; the stricter rule applies at activate, not here.
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task The_same_segment_cannot_be_bound_twice()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            SegmentBindings = new[]
            {
                StrategyTemplateTestBuilders.Segment(segment.Id, 10),
                StrategyTemplateTestBuilders.Segment(segment.Id, 20)
            }
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.SegmentBindingDuplicate, response.Errors);
    }

    // ---------------- frequency intent ----------------

    [Fact]
    public async Task A_policy_reference_to_an_unknown_policy_is_refused()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = StrategyTemplateTestBuilders.PolicyReference(Guid.NewGuid())
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyPolicyNotFound, response.Errors);
    }

    [Fact]
    public async Task A_policy_reference_to_an_inactive_policy_is_refused()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var policy = _policies.Add(StrategyTemplateTestDoubles.TenantA, status: FrequencyPolicyStatus.Draft);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = StrategyTemplateTestBuilders.PolicyReference(policy.Id)
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyPolicyNotActive, response.Errors);
    }

    [Fact]
    public async Task A_segment_targeted_policy_must_target_a_bound_segment()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var policy = _policies.Add(
            StrategyTemplateTestDoubles.TenantA,
            targetType: FrequencyTargetType.Segment,
            targetId: Guid.NewGuid());
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = StrategyTemplateTestBuilders.PolicyReference(policy.Id)
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyPolicyTargetMismatch, response.Errors);
    }

    [Fact]
    public async Task A_policy_targeting_something_other_than_a_segment_is_accepted()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var policy = _policies.Add(StrategyTemplateTestDoubles.TenantA, targetType: FrequencyTargetType.Account);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = StrategyTemplateTestBuilders.PolicyReference(policy.Id)
        };

        var response = await Create().Handle(command, default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("weekly-core", _templates.Rows.Single().FrequencyIntent.PolicyCodeDisplay);
    }

    [Fact]
    public async Task A_mixed_frequency_shape_is_refused_rather_than_resolved()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var policy = _policies.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = new StrategyTemplateFrequencyIntentInput(
                StrategyFrequencyIntentModes.PolicyReference, policy.Id,
                FrequencyType.Weekly, 2, FrequencyPeriodType.Week, null)
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid, response.Errors);
    }

    [Fact]
    public async Task A_declared_intent_is_validated_against_the_MOD_0165_vocabulary()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var invalid = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = StrategyTemplateTestBuilders.DeclaredIntent(frequencyType: "fortnightly")
        };

        var response = await Create().Handle(invalid, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid, response.Errors);
    }

    [Fact]
    public async Task A_declared_intent_needs_a_positive_visit_count()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = StrategyTemplateTestBuilders.DeclaredIntent(requiredVisitCount: 0)
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid, response.Errors);
    }

    [Fact]
    public async Task A_none_intent_may_not_smuggle_a_rhythm()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            FrequencyIntent = new StrategyTemplateFrequencyIntentInput(
                StrategyFrequencyIntentModes.None, null, FrequencyType.Weekly, 3, FrequencyPeriodType.Week, null)
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid, response.Errors);
    }

    // ---------------- content ----------------

    [Fact]
    public async Task An_unpublished_knowledge_path_cannot_be_bound()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var draftPath = _paths.Add(StrategyTemplateTestDoubles.TenantA, status: KnowledgePathStatuses.Draft);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            ContentBindings = new[] { StrategyTemplateTestBuilders.KnowledgePath(draftPath.Id) }
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.ContentNotPublished, response.Errors);
    }

    [Fact]
    public async Task An_archived_journey_cannot_be_bound()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var journey = _journeys.Add(StrategyTemplateTestDoubles.TenantA, archived: true);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            ContentBindings = new[] { StrategyTemplateTestBuilders.Journey(journey.Id) }
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.ContentArchived, response.Errors);
    }

    [Fact]
    public async Task An_unknown_content_reference_is_refused()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            ContentBindings = new[] { StrategyTemplateTestBuilders.Journey(Guid.NewGuid()) }
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.ContentReferenceNotFound, response.Errors);
    }

    [Fact]
    public async Task Both_content_kinds_bind_and_stamp_their_business_version()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var path = _paths.Add(StrategyTemplateTestDoubles.TenantA);
        var journey = _journeys.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            ContentBindings = new[]
            {
                StrategyTemplateTestBuilders.KnowledgePath(path.Id),
                StrategyTemplateTestBuilders.Journey(journey.Id)
            }
        };

        var response = await Create().Handle(command, default);

        Assert.True(response.IsSuccessful);
        var bindings = _templates.Rows.Single().ContentBindings;
        Assert.Equal(2, bindings.Count);
        Assert.All(bindings, b => Assert.Equal("1.0", b.ContentVersionAtBinding));
        Assert.Contains(bindings, b => b.ContentCodeDisplay == "onboarding");
        Assert.Contains(bindings, b => b.ContentCodeDisplay == "adoption");
    }

    [Fact]
    public async Task The_same_content_row_cannot_be_bound_twice()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var path = _paths.Add(StrategyTemplateTestDoubles.TenantA);
        var command = StrategyTemplateTestBuilders.NewTemplate(segment.Id) with
        {
            ContentBindings = new[]
            {
                StrategyTemplateTestBuilders.KnowledgePath(path.Id, 10),
                StrategyTemplateTestBuilders.KnowledgePath(path.Id, 20)
            }
        };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        AssertCode(StrategyTemplateErrorCodes.ContentBindingDuplicate, response.Errors);
    }
}
