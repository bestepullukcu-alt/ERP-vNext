using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Application.Tests.VisitContentSequence;
using Diten.CrmService.Domain.Entities;
using Xunit;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-E2E-FIX-3 (E5-B2) — ONE play version in force per segment, on the PRODUCTION reader
/// (<see cref="StrategyTemplateReader"/>) and the PRODUCTION content resolver (<see cref="VisitContentSequenceResolver"/>);
/// only the template store is in-memory. A version replaced by an ACTIVE successor is out of force (the old ascending
/// sort let v1 beat v2); a successor that is not live (draft / archived) takes nothing over; the highest version wins
/// otherwise. The status set is untouched — "superseded" stays a mark. The list / detail carry the successor's version
/// for the "Yerini v{n} aldı" badge.
/// </summary>
public sealed class StrategyTemplateEffectiveVersionTests
{
    private static readonly Guid Tenant = StrategyTemplateTestDoubles.TenantA;
    private static readonly DateTimeOffset At = StrategyTemplateTestDoubles.Now;

    private readonly FakeStrategyTemplateRepository _store = new();

    private StrategyTemplateReader Reader() => new(StrategyTemplateTestDoubles.Tenant(Tenant), _store);

    private TemplateEntity Play(Guid segmentId, int version, string status = StrategyTemplateStatuses.Active,
        Guid? lineage = null, string code = "PLAY-TUTUKON")
    {
        var t = new TemplateEntity
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            TemplateCode = code,
            TemplateName = $"{code} v{version}",
            TemplateStatus = status,
            TemplateVersion = version,
            VersionLineageId = lineage ?? Guid.NewGuid(),
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            SegmentBindings = new List<StrategyTemplateSegmentBinding> { new() { SegmentId = segmentId, SegmentLineageId = segmentId } },
            ArchivedAt = status == StrategyTemplateStatuses.Archived ? StrategyTemplateTestDoubles.Past : null
        };
        _store.Rows.Add(t);
        return t;
    }

    private static void Supersede(TemplateEntity predecessor, TemplateEntity successor)
        => predecessor.SupersededByTemplateId = successor.Id;

    // ── Acceptance 1 ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Same_segment_v1_superseded_by_an_active_v2_the_reader_picks_v2_only()
    {
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, lineage: lineage);
        Supersede(v1, v2);

        var plays = await Reader().ListBySegmentAsync(segment, At, default);

        Assert.Equal(new[] { v2.Id }, plays.Select(p => p.TemplateId));
    }

    [Fact]
    public async Task V1_alone_is_in_force()
    {
        var segment = Guid.NewGuid();
        var v1 = Play(segment, 1);

        Assert.Equal(v1.Id, Assert.Single(await Reader().ListBySegmentAsync(segment, At, default)).TemplateId);
    }

    [Fact]
    public async Task A_superseded_version_is_out_of_force_even_on_a_segment_its_successor_no_longer_binds()
    {
        // v2 re-bound the play to another segment: the old segment must not keep reaching v1's products / journey.
        var oldSegment = Guid.NewGuid();
        var newSegment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(oldSegment, 1, lineage: lineage);
        var v2 = Play(newSegment, 2, lineage: lineage);
        Supersede(v1, v2);

        Assert.Empty(await Reader().ListBySegmentAsync(oldSegment, At, default));
        Assert.Equal(v2.Id, Assert.Single(await Reader().ListBySegmentAsync(newSegment, At, default)).TemplateId);
    }

    [Fact]
    public async Task Two_live_versions_without_the_mark_the_highest_version_wins()
    {
        // The supersede stamp is a separate write; if it failed, the higher version must still win.
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, lineage: lineage);

        var plays = await Reader().ListBySegmentAsync(segment, At, default);

        Assert.Equal(v2.Id, plays[0].TemplateId);
        Assert.Equal(v2.Id, StrategyTemplateReader.InPreferenceOrder(plays.Reverse()).First().TemplateId);
    }

    [Fact]
    public async Task The_content_resolver_picks_v2_from_the_segment_through_the_reader()
    {
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, lineage: lineage);
        Supersede(v1, v2);
        var resolver = Resolver();

        var result = await resolver.ResolveAsync(
            new VisitContentSequenceRequest("contact", Guid.Empty, segment, null, null, null, At), default);

        Assert.Equal(v2.Id, result.StrategyTemplateId);
    }

    [Fact]
    public async Task An_explicit_template_id_is_used_as_today()
    {
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, lineage: lineage);
        Supersede(v1, v2);

        var result = await Resolver().ResolveAsync(
            new VisitContentSequenceRequest("contact", Guid.Empty, null, v1.Id, null, null, At), default);

        Assert.Equal(v1.Id, result.StrategyTemplateId); // still an active status: the caller asked for it explicitly
    }

    // ── Acceptance 2 ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(StrategyTemplateStatuses.Draft)]
    [InlineData(StrategyTemplateStatuses.Archived)]
    public async Task A_successor_that_is_not_live_takes_nothing_over_v1_stays_in_force(string successorStatus)
    {
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, successorStatus, lineage: lineage);
        Supersede(v1, v2);

        Assert.Equal(v1.Id, Assert.Single(await Reader().ListBySegmentAsync(segment, At, default)).TemplateId);
        var result = await Resolver().ResolveAsync(
            new VisitContentSequenceRequest("contact", Guid.Empty, segment, null, null, null, At), default);
        Assert.Equal(v1.Id, result.StrategyTemplateId);
    }

    [Fact]
    public async Task A_successor_not_yet_effective_takes_nothing_over_until_its_window_opens()
    {
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, lineage: lineage);
        v2.EffectiveFrom = At.AddDays(10);
        Supersede(v1, v2);

        Assert.Equal(v1.Id, Assert.Single(await Reader().ListBySegmentAsync(segment, At, default)).TemplateId);
        Assert.Equal(v2.Id, Assert.Single(await Reader().ListBySegmentAsync(segment, At.AddDays(11), default)).TemplateId);
    }

    // ── "Yerini v{n} aldı" badge data ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_and_detail_carry_the_successors_version()
    {
        var segment = Guid.NewGuid();
        var lineage = Guid.NewGuid();
        var v1 = Play(segment, 1, lineage: lineage);
        var v2 = Play(segment, 2, lineage: lineage);
        Supersede(v1, v2);
        var tenant = StrategyTemplateTestDoubles.Tenant(Tenant);

        var list = (await new ListStrategyTemplatesHandler(tenant, _store)
            .Handle(new ListStrategyTemplatesQuery(null, null, null, null, null, null, true), default)).Data!.Items;
        Assert.Equal(2, list.Single(i => i.TemplateId == v1.Id).SupersededByTemplateVersion);
        Assert.Null(list.Single(i => i.TemplateId == v2.Id).SupersededByTemplateVersion);

        var detail = (await new GetStrategyTemplateByIdHandler(tenant, _store)
            .Handle(new GetStrategyTemplateByIdQuery(v1.Id), default)).Data!;
        Assert.Equal(2, detail.SupersededByTemplateVersion);
        Assert.Equal(StrategyTemplateStatuses.Active, detail.TemplateStatus); // status set unchanged — no new status
    }

    private VisitContentSequenceResolver Resolver()
    {
        var kit = new VisitContentTestKit();
        return new VisitContentSequenceResolver(
            StrategyTemplateTestDoubles.Tenant(Tenant), Reader(), kit.Segments, kit.Journeys, kit.Capacities, kit.Sources);
    }
}
