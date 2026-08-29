using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — the aggregate: creation defaults, code uniqueness, tenant isolation, concurrency and the absence of
/// any hard-delete path.
/// </summary>
public sealed class StrategyTemplateAggregateTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private StrategyTemplateBindingValidator Bindings()
        => new(_segments, _policies, _paths, _journeys);

    private CreateStrategyTemplateHandler Create(Guid tenant = default) => new(
        StrategyTemplateTestDoubles.Tenant(tenant == default ? StrategyTemplateTestDoubles.TenantA : tenant),
        new NullActorContext(), _templates, Bindings(), _references);

    private UpdateStrategyTemplateHandler Update() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references);

    private ArchiveStrategyTemplateHandler Archive() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates);

    private GetStrategyTemplateByIdHandler GetById(Guid tenant = default) => new(
        StrategyTemplateTestDoubles.Tenant(tenant == default ? StrategyTemplateTestDoubles.TenantA : tenant),
        _templates);

    private ListStrategyTemplatesHandler ListFor(Guid tenant) => new(
        StrategyTemplateTestDoubles.Tenant(tenant), _templates);

    private Guid ActiveSegment(Guid tenant = default, string subjectType = SegmentSubjectTypes.Contact)
        => _segments.Add(
            tenant == default ? StrategyTemplateTestDoubles.TenantA : tenant,
            code: $"seg-{Guid.NewGuid():N}"[..12],
            subjectType: subjectType).Id;

    [Fact]
    public async Task Create_starts_as_draft_version_one_and_its_own_lineage_root()
    {
        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);

        Assert.True(response.IsSuccessful);
        var template = _templates.Rows.Single();
        Assert.Equal(StrategyTemplateStatuses.Draft, template.TemplateStatus);
        Assert.Equal(1, template.TemplateVersion);
        Assert.Equal(template.Id, template.VersionLineageId);
        Assert.Null(template.BindingsFrozenAt);
        Assert.Null(template.ActivatedAt);
    }

    [Fact]
    public async Task Create_stamps_the_segment_provenance_from_the_segment_not_the_payload()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA, code: "cardio-a");

        await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(segment.Id), default);

        var binding = _templates.Rows.Single().SegmentBindings.Single();
        Assert.Equal(segment.VersionLineageId, binding.SegmentLineageId);
        Assert.Equal(segment.SegmentVersion, binding.SegmentVersionAtBinding);
        Assert.Equal("cardio-a", binding.SegmentCodeDisplay);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code_but_the_same_code_is_free_in_another_tenant()
    {
        await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);

        var duplicate = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);
        Assert.Equal(409, duplicate.StatusCode);

        var otherTenantSegment = ActiveSegment(StrategyTemplateTestDoubles.TenantB);
        var otherTenant = await Create(StrategyTemplateTestDoubles.TenantB)
            .Handle(StrategyTemplateTestBuilders.NewTemplate(otherTenantSegment), default);
        Assert.True(otherTenant.IsSuccessful);
    }

    [Fact]
    public async Task Create_refuses_a_template_that_binds_no_segment()
    {
        var command = StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()) with { SegmentBindings = null };

        var response = await Create().Handle(command, default);

        Assert.Equal(400, response.StatusCode);
        Assert.Empty(_templates.Rows);
    }

    [Fact]
    public async Task A_template_of_another_tenant_answers_404_and_is_absent_from_the_list()
    {
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);

        var foreign = await GetById(StrategyTemplateTestDoubles.TenantB).Handle(
            new GetStrategyTemplateByIdQuery(created.Data), default);
        Assert.Equal(404, foreign.StatusCode);

        var foreignList = await ListFor(StrategyTemplateTestDoubles.TenantB).Handle(
            new ListStrategyTemplatesQuery(null, null, null, null, null, null, true), default);
        Assert.Empty(foreignList.Data!.Items);
    }

    [Fact]
    public async Task An_update_with_a_stale_version_is_a_409_and_overwrites_nothing()
    {
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);
        var id = created.Data;

        var stale = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, "Renamed", StrategyTemplateTestDoubles.Past, null, null, null, null,
                null, null, null, null, ExpectedVersion: 99),
            default);

        Assert.Equal(409, stale.StatusCode);
        Assert.Equal("Cardiology core play", _templates.Stored(id).TemplateName);
    }

    [Fact]
    public async Task An_archived_template_accepts_no_update()
    {
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);
        var id = created.Data;

        var archived = await Archive().Handle(new ArchiveStrategyTemplateCommand(id, null), default);
        Assert.True(archived.IsSuccessful);

        var update = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, "Renamed", StrategyTemplateTestDoubles.Past, null, null, null, null,
                null, null, null, null, null),
            default);

        Assert.Equal(409, update.StatusCode);
    }

    [Fact]
    public async Task Archiving_is_soft_and_the_row_keeps_its_bindings()
    {
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(ActiveSegment()), default);

        await Archive().Handle(new ArchiveStrategyTemplateCommand(created.Data, null), default);

        var stored = _templates.Stored(created.Data);
        Assert.True(stored.IsArchived());
        Assert.NotNull(stored.ArchivedAt);
        Assert.False(stored.IsDeleted);
        Assert.Single(stored.SegmentBindings);
    }

    [Fact]
    public void The_repository_contract_exposes_no_delete_at_all()
    {
        var methods = typeof(Domain.Repositories.IStrategyTemplateRepository)
            .GetMethods()
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain(methods, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, name => name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_list_answers_the_reverse_question_without_touching_a_member()
    {
        var boundSegment = ActiveSegment();
        var otherSegment = ActiveSegment();
        await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(boundSegment, "play-a"), default);
        await Create().Handle(StrategyTemplateTestBuilders.NewTemplate(otherSegment, "play-b"), default);

        var response = await ListFor(StrategyTemplateTestDoubles.TenantA).Handle(
            new ListStrategyTemplatesQuery(null, null, null, null, boundSegment, null, true), default);

        Assert.Single(response.Data!.Items);
        Assert.Equal("play-a", response.Data.Items[0].TemplateCode);
    }
}
