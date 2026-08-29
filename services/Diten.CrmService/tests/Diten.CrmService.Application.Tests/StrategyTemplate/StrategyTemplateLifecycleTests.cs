using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — lifecycle: activate freezes the bindings, a frozen play is changed only through a new version, and
/// the clone shares nothing but the lineage.
/// </summary>
public sealed class StrategyTemplateLifecycleTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private StrategyTemplateBindingValidator Bindings() => new(_segments, _policies, _paths, _journeys);

    private CreateStrategyTemplateHandler Create() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references);

    private UpdateStrategyTemplateHandler Update() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references);

    private ActivateStrategyTemplateHandler Activate() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings());

    private CreateStrategyTemplateVersionHandler NewVersion() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates);

    private async Task<Guid> DraftBoundTo(Segment segment, string code = "cardio-core-play")
    {
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                segment.Id, code,
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(
                        Guid.NewGuid(), new[] { (Guid.NewGuid(), 100m) })
                }),
            default);
        Assert.True(created.IsSuccessful);
        return created.Data;
    }

    [Fact]
    public async Task Activate_freezes_the_bindings_and_stamps_the_actor()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);

        var response = await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);

        Assert.True(response.IsSuccessful);
        var stored = _templates.Stored(id);
        Assert.Equal(StrategyTemplateStatuses.Active, stored.TemplateStatus);
        Assert.True(stored.AreBindingsFrozen());
        Assert.NotNull(stored.ActivatedAt);
    }

    [Fact]
    public async Task Activate_refuses_while_a_bound_segment_is_not_active_and_writes_no_freeze_stamp()
    {
        var draftSegment = _segments.Add(StrategyTemplateTestDoubles.TenantA, status: SegmentStatuses.Draft);
        var id = await DraftBoundTo(draftSegment);

        var response = await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SegmentNotActive, response.Errors!);
        var stored = _templates.Stored(id);
        Assert.Equal(StrategyTemplateStatuses.Draft, stored.TemplateStatus);
        // A frozen-but-not-live play would be unfixable without a new version, so nothing is stamped on failure.
        Assert.Null(stored.BindingsFrozenAt);
    }

    [Fact]
    public async Task A_frozen_play_refuses_a_binding_change_and_points_at_new_version()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var otherSegment = _segments.Add(StrategyTemplateTestDoubles.TenantA, code: "onco-b");
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var current = _templates.Stored(id);

        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, current.TemplateName, current.EffectiveFrom, null, null, null, null,
                new[] { StrategyTemplateTestBuilders.Segment(otherSegment.Id) },
                null, null, null, current.Version),
            default);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.BindingsFrozen, response.Errors!);
        Assert.Equal(segment.Id, _templates.Stored(id).SegmentBindings.Single().SegmentId);
    }

    [Fact]
    public async Task A_frozen_play_answers_bindings_frozen_even_when_the_payload_is_also_shape_invalid()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var current = _templates.Stored(id);

        // An EMPTY (not null) binding list is both a binding change and an invalid shape. On a frozen play the freeze
        // is the load-bearing answer: telling the caller to "bind at least one segment" would send them fixing a
        // payload that could never be accepted anyway.
        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, current.TemplateName, current.EffectiveFrom, null, null, null, null,
                Array.Empty<StrategyTemplateSegmentBindingInput>(),
                null, null, null, current.Version),
            default);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.BindingsFrozen, response.Errors!);
        Assert.Single(_templates.Stored(id).SegmentBindings);
    }

    [Fact]
    public async Task A_draft_play_still_answers_400_for_an_invalid_shape()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        var current = _templates.Stored(id);

        // Same payload, unfrozen play: the shape validation is still the one that speaks.
        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, current.TemplateName, current.EffectiveFrom, null, null, null, null,
                Array.Empty<StrategyTemplateSegmentBindingInput>(),
                null, null, null, current.Version),
            default);

        Assert.Equal(400, response.StatusCode);
        Assert.DoesNotContain(StrategyTemplateErrorCodes.BindingsFrozen, response.Errors!);
        Assert.Single(_templates.Stored(id).SegmentBindings);
    }

    [Fact]
    public async Task A_frozen_play_can_still_be_renamed()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var current = _templates.Stored(id);

        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, "Cardiology core play (2026)", current.EffectiveFrom, null, null, "New description", null,
                null, null, null, null, current.Version),
            default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Cardiology core play (2026)", _templates.Stored(id).TemplateName);
    }

    [Fact]
    public async Task Resending_identical_bindings_to_a_frozen_play_is_not_a_change()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var current = _templates.Stored(id);

        // A UI round-tripping the whole document sends the same bindings back with the SAME values but new child ids
        // would be generated on mapping; the guard compares what the play binds, so this must pass.
        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, current.TemplateName, current.EffectiveFrom, null, null, null, null,
                current.SegmentBindings
                    .Select(b => new StrategyTemplateSegmentBindingInput(
                        b.SegmentId, b.BindingRole, b.SortOrder, b.Notes))
                    .ToList(),
                new StrategyTemplateFrequencyIntentInput(
                    current.FrequencyIntent.Mode, current.FrequencyIntent.VisitFrequencyPolicyId,
                    current.FrequencyIntent.FrequencyType, current.FrequencyIntent.RequiredVisitCount,
                    current.FrequencyIntent.PeriodType, current.FrequencyIntent.IntentNote),
                current.ProductLines
                    .Select(l => new StrategyTemplateProductLineInput(
                        l.GlobalProductId, l.GlobalProductCodeDisplay, l.LineWeightPercentage, l.SkuAllocationMode,
                        l.SkuAllocations
                            .Select(a => new StrategyTemplateSkuAllocationInput(
                                a.GskuId, a.GskuCanonicalCodeDisplay, a.Percentage, a.SortOrder))
                            .ToList(),
                        l.SortOrder, l.Notes))
                    .ToList(),
                current.ContentBindings
                    .Select(c => new StrategyTemplateContentBindingInput(
                        c.ContentRefType, c.ContentRefId, c.SortOrder, c.Notes))
                    .ToList(),
                current.Version),
            default);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task New_version_clones_with_fresh_child_ids_and_the_next_business_version()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var source = _templates.Stored(id);

        var response = await NewVersion().Handle(new CreateStrategyTemplateVersionCommand(id), default);

        Assert.True(response.IsSuccessful);
        var clone = _templates.Stored(response.Data);
        Assert.NotEqual(source.Id, clone.Id);
        Assert.Equal(source.VersionLineageId, clone.VersionLineageId);
        Assert.Equal(source.TemplateVersion + 1, clone.TemplateVersion);
        Assert.Equal(StrategyTemplateStatuses.Draft, clone.TemplateStatus);
        Assert.Null(clone.BindingsFrozenAt);

        // Fresh child ids: nothing about the old version can be reached through the new one.
        Assert.NotEqual(
            source.SegmentBindings.Single().BindingId, clone.SegmentBindings.Single().BindingId);
        Assert.NotEqual(source.ProductLines.Single().LineId, clone.ProductLines.Single().LineId);
        Assert.NotEqual(
            source.ProductLines.Single().SkuAllocations.Single().AllocationId,
            clone.ProductLines.Single().SkuAllocations.Single().AllocationId);
        // ...but the same references.
        Assert.Equal(source.SegmentBindings.Single().SegmentId, clone.SegmentBindings.Single().SegmentId);
        Assert.Equal(source.ProductLines.Single().GlobalProductId, clone.ProductLines.Single().GlobalProductId);
    }

    [Fact]
    public async Task Activating_the_new_version_supersedes_its_predecessor()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var cloned = await NewVersion().Handle(new CreateStrategyTemplateVersionCommand(id), default);

        await Activate().Handle(new ActivateStrategyTemplateCommand(cloned.Data, null), default);

        var predecessor = _templates.Stored(id);
        Assert.Equal(cloned.Data, predecessor.SupersededByTemplateId);
        // The superseded version stays readable so a past play can still be explained.
        Assert.False(predecessor.IsArchived());
    }

    [Fact]
    public async Task A_lineage_may_not_carry_two_open_drafts()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);
        await NewVersion().Handle(new CreateStrategyTemplateVersionCommand(id), default);

        var second = await NewVersion().Handle(new CreateStrategyTemplateVersionCommand(id), default);

        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task An_already_active_play_cannot_be_activated_again()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var id = await DraftBoundTo(segment);
        await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);

        var second = await Activate().Handle(new ActivateStrategyTemplateCommand(id, null), default);

        Assert.Equal(409, second.StatusCode);
    }
}
