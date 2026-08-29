using System.Reflection;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Contract;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — the load-bearing negative: <b>a template binds and never produces</b>.
/// <para>Two kinds of proof are used. Behavioural: every foreign repository double counts its writes, and a full
/// lifecycle run must leave all of those counters at zero. Structural: reflection over the feature's own types, because
/// "nobody injected the membership reader" is a fact about the code that no runtime test can establish.</para>
/// </summary>
public sealed class StrategyTemplateNoWriteGuardTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private StrategyTemplateBindingValidator Bindings() => new(_segments, _policies, _paths, _journeys);

    private static readonly Assembly ApplicationAssembly = typeof(StrategyTemplatePermissions).Assembly;

    private static IEnumerable<Type> FeatureTypes => ApplicationAssembly
        .GetTypes()
        .Where(t => t.Namespace is not null
                    && t.Namespace.StartsWith(
                        "Diten.CrmService.Application.Features.StrategyTemplate", StringComparison.Ordinal));

    [Fact]
    public async Task A_full_lifecycle_writes_to_no_foreign_aggregate()
    {
        var tenant = StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA);
        var actor = new NullActorContext();
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var path = _paths.Add(StrategyTemplateTestDoubles.TenantA);
        var policy = _policies.Add(StrategyTemplateTestDoubles.TenantA);

        var create = new CreateStrategyTemplateHandler(tenant, actor, _templates, Bindings(), _references);
        var update = new UpdateStrategyTemplateHandler(tenant, actor, _templates, Bindings(), _references);
        var activate = new ActivateStrategyTemplateHandler(tenant, actor, _templates, Bindings());
        var newVersion = new CreateStrategyTemplateVersionHandler(tenant, actor, _templates);
        var archive = new ArchiveStrategyTemplateHandler(tenant, actor, _templates);

        var created = await create.Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                segment.Id,
                frequency: StrategyTemplateTestBuilders.PolicyReference(policy.Id),
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(Guid.NewGuid(), new[] { (Guid.NewGuid(), 100m) })
                },
                contentBindings: new[] { StrategyTemplateTestBuilders.KnowledgePath(path.Id) }),
            default);
        Assert.True(created.IsSuccessful);
        var id = created.Data;

        var stored = _templates.Stored(id);
        await update.Handle(
            new UpdateStrategyTemplateCommand(
                id, "Renamed play", stored.EffectiveFrom, null, null, null, null,
                null, null, null, null, stored.Version),
            default);
        await activate.Handle(new ActivateStrategyTemplateCommand(id, null), default);
        var clone = await newVersion.Handle(new CreateStrategyTemplateVersionCommand(id), default);
        await archive.Handle(new ArchiveStrategyTemplateCommand(clone.Data, null), default);

        // The whole point of this FU, asserted: not one write reached a segment, a frequency policy or a content row.
        Assert.Equal(0, _segments.WriteCalls);
        Assert.Equal(0, _policies.WriteCalls);
        Assert.Equal(0, _paths.WriteCalls);
        Assert.Equal(0, _journeys.WriteCalls);
    }

    [Fact]
    public async Task No_frequency_intent_mode_writes_a_policy()
    {
        var tenant = StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA);
        var create = new CreateStrategyTemplateHandler(
            tenant, new NullActorContext(), _templates, Bindings(), _references);
        var policy = _policies.Add(StrategyTemplateTestDoubles.TenantA);

        var intents = new[]
        {
            StrategyTemplateTestBuilders.NoFrequency(),
            StrategyTemplateTestBuilders.DeclaredIntent(),
            StrategyTemplateTestBuilders.PolicyReference(policy.Id)
        };

        for (var i = 0; i < intents.Length; i++)
        {
            var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA, code: $"seg-{i}");
            var response = await create.Handle(
                StrategyTemplateTestBuilders.NewTemplate(segment.Id, $"play-{i}", frequency: intents[i]), default);
            Assert.True(response.IsSuccessful);
        }

        Assert.Equal(0, _policies.WriteCalls);
    }

    [Fact]
    public void The_feature_never_injects_the_segment_membership_reader()
    {
        // Reading a play must never imply the right to see the people inside its segments. The only way to be sure is
        // that the seam is not reachable from this feature at all.
        var offenders = FeatureTypes
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType == typeof(ISegmentMembershipReader))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_feature_never_injects_a_producing_repository()
    {
        // ITargetCustomerRepository and ICampaignRepository are the two ways a "playbook" could quietly start producing
        // rows. Neither is reachable from this feature.
        var forbidden = new[] { typeof(ITargetCustomerRepository), typeof(ICampaignRepository) };

        var offenders = FeatureTypes
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Where(p => forbidden.Contains(p.ParameterType))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void No_type_in_the_feature_mentions_a_brand()
    {
        // D-BRAND: the product does not use brands, so there is no BrandId field and no brand reference kind. A
        // nullable FK nobody fills is a permanent lie in the data model.
        var members = FeatureTypes
            .SelectMany(t => t.GetProperties().Select(p => p.Name).Concat(t.GetFields().Select(f => f.Name)))
            .Where(name => name.Contains("Brand", StringComparison.OrdinalIgnoreCase))
            // The single allowed mention is the contract flag that DENIES brand support out loud.
            .Where(name => !string.Equals(name, "SupportsBrandBinding", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(members);
        Assert.False(StrategyTemplateFeatureFlags.Current.SupportsBrandBinding);

        var entityMembers = typeof(Domain.Entities.StrategyTemplate)
            .GetProperties()
            .Select(p => p.Name)
            .Where(name => name.Contains("Brand", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(entityMembers);
    }

    [Fact]
    public void The_produce_flags_are_all_false_in_the_published_contract()
    {
        var flags = StrategyTemplateFeatureFlags.Current;

        Assert.False(flags.SupportsStrategyApply);
        Assert.False(flags.SupportsMicroTargetGeneration);
        Assert.False(flags.SupportsCyclePeriod);
        Assert.False(flags.SupportsFrequencyPolicyWrite);
        Assert.False(flags.SupportsCampaignTargetGeneration);
        Assert.False(flags.SupportsSegmentMembershipResolution);
        Assert.False(flags.SupportsUcln);
        Assert.False(flags.SupportsLoyaltyPlanning);
        Assert.False(flags.SupportsPromoWeekPlanning);
        Assert.False(flags.SupportsPatientNumberPlanning);
        Assert.False(flags.SupportsSubjectListAggregate);
        Assert.False(flags.SupportsAudienceAggregate);
        Assert.False(flags.SupportsBrandBinding);
        Assert.False(flags.SupportsLskuBinding);
        Assert.False(flags.SupportsProductSkuContainmentValidation);
        Assert.False(flags.SupportsStrategyEngine);
    }

    [Fact]
    public async Task The_contract_publishes_the_MOD_0165_frequency_vocabulary_rather_than_a_copy()
    {
        var handler = new GetStrategyTemplateContractHandler(
            StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA));

        var response = await handler.Handle(new GetStrategyTemplateContractQuery(), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(FrequencyType.All, response.Data!.Vocabularies.FrequencyTypes);
        Assert.Equal(FrequencyPeriodType.All, response.Data.Vocabularies.FrequencyPeriodTypes);
        Assert.Equal("MOD-0167-FU04", response.Data.ModuleId);
    }

    [Fact]
    public async Task The_binding_view_reports_no_member_and_never_claims_containment()
    {
        var tenant = StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA);
        var segment = _segments.Add(StrategyTemplateTestDoubles.TenantA);
        var create = new CreateStrategyTemplateHandler(
            tenant, new NullActorContext(), _templates, Bindings(), _references);
        var created = await create.Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                segment.Id,
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(Guid.NewGuid(), new[] { (Guid.NewGuid(), 100m) })
                }),
            default);

        var bindings = new GetStrategyTemplateBindingsHandler(
            tenant, _templates, _segments, _policies, _paths, _journeys);
        var response = await bindings.Handle(
            new GetStrategyTemplateBindingsQuery(created.Data, StrategyTemplateTestDoubles.Now), default);

        Assert.True(response.IsSuccessful);
        Assert.All(response.Data!.ProductLines, line => Assert.False(line.ContainmentVerified));

        // No member, member count or subject id may appear anywhere in the binding view's shape.
        var forbidden = new[] { "MemberCount", "Members", "SubjectId", "MemberIds" };
        var names = typeof(StrategyTemplateBindingsDto).GetProperties().Select(p => p.Name)
            .Concat(typeof(StrategyTemplateSegmentBindingViewDto).GetProperties().Select(p => p.Name))
            .ToList();
        Assert.DoesNotContain(names, name => forbidden.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_declared_intent_is_never_reported_as_binding_to_a_consumer()
    {
        var template = new Domain.Entities.StrategyTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = StrategyTemplateTestDoubles.TenantA,
            TemplateCode = "play",
            TemplateName = "Play",
            SubjectType = StrategyTemplateSubjectTypes.Contact,
            TemplateStatus = StrategyTemplateStatuses.Active,
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            FrequencyIntent = new StrategyTemplateFrequencyIntent
            {
                Mode = StrategyFrequencyIntentModes.DeclaredIntent,
                FrequencyType = FrequencyType.Weekly,
                RequiredVisitCount = 2,
                PeriodType = FrequencyPeriodType.Week
            }
        };

        var set = StrategyTemplateReader.ToBindingSet(template);

        // MOD-0165 neither reads nor honours a declared rhythm; saying otherwise here would be the SoR breach this FU
        // exists to avoid.
        Assert.False(set.FrequencyIntent.Binding);
    }
}
