using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-ST-DETAIL-1 — the version-history read for the Detay panel: it lists a play's lineage newest-first, flags the
/// requested version as current, keeps another tenant's lineage unreadable, and answers 404 for an unknown id.
/// </summary>
public sealed class StrategyTemplateVersionsTests
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
        new NullActorContext(), _templates, Bindings(), _references, StrategyTemplateTestDoubles.DefaultScope());

    private ActivateStrategyTemplateHandler Activate() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings());

    private CreateStrategyTemplateVersionHandler NewVersion() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates);

    private GetStrategyTemplateVersionsHandler Versions(Guid tenant) => new(
        StrategyTemplateTestDoubles.Tenant(tenant), _templates);

    /// <summary>Build a real two-version lineage: create a draft, activate it (freezes v1), then clone a new version
    /// (v2) — the clone shares the lineage id and carries the next TemplateVersion.</summary>
    private async Task<(Guid V1, Guid V2)> TwoVersionLineageAsync()
    {
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                segment.Id, "cardio-core-play",
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(Guid.NewGuid(), new[] { (Guid.NewGuid(), 100m) })
                }),
            default);
        Assert.True(created.IsSuccessful);
        var v1 = created.Data;

        Assert.True((await Activate().Handle(new ActivateStrategyTemplateCommand(v1, null), default)).IsSuccessful);
        var newVersion = await NewVersion().Handle(new CreateStrategyTemplateVersionCommand(v1), default);
        Assert.True(newVersion.IsSuccessful);
        return (v1, newVersion.Data);
    }

    [Fact]
    public async Task Lists_the_lineage_newest_first_and_flags_the_requested_version_current()
    {
        var (v1, v2) = await TwoVersionLineageAsync();

        var response = await Versions(StrategyTemplateTestDoubles.TenantA).Handle(
            new GetStrategyTemplateVersionsQuery(v2), default);

        Assert.True(response.IsSuccessful);
        var versions = response.Data!.Versions;
        Assert.Equal(2, versions.Count);
        // Newest first (TemplateVersion descending).
        Assert.True(versions[0].TemplateVersion > versions[1].TemplateVersion);
        Assert.Equal(v2, versions[0].TemplateId);
        Assert.True(versions[0].IsCurrent);
        Assert.Equal(v1, versions[1].TemplateId);
        Assert.False(versions[1].IsCurrent);
    }

    [Fact]
    public async Task Another_tenant_cannot_read_the_lineage()
    {
        var (_, v2) = await TwoVersionLineageAsync();

        var response = await Versions(StrategyTemplateTestDoubles.TenantB).Handle(
            new GetStrategyTemplateVersionsQuery(v2), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_id_answers_not_found()
    {
        var response = await Versions(StrategyTemplateTestDoubles.TenantA).Handle(
            new GetStrategyTemplateVersionsQuery(Guid.NewGuid()), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }
}
