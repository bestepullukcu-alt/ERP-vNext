using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-CH-1 — the chain template "Outputs" read: only the paths whose KP-1 ChainRef is this chain version (another
/// chain, a ChainRef-less legacy path, an archived path and another tenant never), the other versions on request, the
/// current release flag, journeys by KP-3's usage rule (pinned id + latest-published code), the upcoming planned visit
/// COUNT (cancelled / archived / past / another path never), and a count-only restricted answer.
/// </summary>
public sealed class ConceptChainTemplateOutputsTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Subject = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 1);

    private readonly Repo<ConceptChainTemplate> _templates = new();
    private readonly Repo<KnowledgePath> _paths = new();
    private readonly Repo<KnowledgePathRevision> _revisions = new();
    private readonly Repo<ContentEngagementJourney> _journeys = new();
    private readonly Repo<PlannedVisitEntity> _visits = new();

    private readonly ConceptChainTemplate _v1;
    private readonly ConceptChainTemplate _v2;
    private readonly ConceptChainTemplate _otherChain;

    public ConceptChainTemplateOutputsTests()
    {
        _v1 = Chain("TPL-ALMIBA-01", "1.0");
        _v2 = Chain("TPL-ALMIBA-01", "2.0");
        _otherChain = Chain("TPL-OTHER", "1.0");
    }

    // ============================================================ paths

    [Fact]
    public async Task Only_paths_bound_to_this_chain_version_are_outputs()
    {
        var mine = Path(_v1, "KP-TR", "tr", "TR");
        Path(_v2, "KP-V2", "tr", "TR");                      // another version of the same chain
        Path(_otherChain, "KP-OTHER", "tr", "TR");           // another chain
        Path(null, "KP-LEGACY", "tr", "TR");                 // no ChainRef (legacy)
        Path(_v1, "KP-ARCH", "tr", "TR").ArchivedAt = Now;   // archived
        var foreign = Path(_v1, "KP-FOREIGN", "tr", "TR");
        foreign.TenantId = OtherTenant;                      // another tenant

        var dto = await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id));

        Assert.Equal([mine.Id], dto.Paths.Select(p => p.PathId).ToArray());
        Assert.Equal(1, dto.PathCount);
        Assert.False(dto.IncludesOtherVersions);
    }

    [Fact]
    public async Task Other_versions_of_the_same_chain_code_come_with_their_chain_version_on_request()
    {
        Path(_v1, "KP-A", "tr", "TR");
        Path(_v2, "KP-B", "en", "GB");
        Path(_otherChain, "KP-OTHER", "tr", "TR");

        var dto = await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id, IncludeOtherVersions: true));

        Assert.True(dto.IncludesOtherVersions);
        // Ordered by country, language, version: GB before TR.
        Assert.Equal(["KP-B", "KP-A"], dto.Paths.Select(p => p.PathCode).ToArray());
        Assert.Equal(["2.0", "1.0"], dto.Paths.Select(p => p.ChainVersion).ToArray());
    }

    [Fact]
    public async Task A_chain_of_another_tenant_is_not_found()
    {
        _v1.TenantId = OtherTenant;

        var response = await Handler().Handle(new GetConceptChainTemplateOutputsQuery(_v1.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task The_current_release_flag_and_the_latest_revision_are_reported()
    {
        var published = Path(_v1, "KP-PUB", "tr", "TR");
        published.PathStatus = KnowledgePathStatuses.Published;
        var draft = Path(_v1, "KP-DRAFT", "tr", "TR");
        _revisions.Items.Add(new KnowledgePathRevision { TenantId = Tenant, PathId = draft.Id, RevisionNumber = 1, Status = KnowledgePathRevisionStatuses.Rejected });
        _revisions.Items.Add(new KnowledgePathRevision { TenantId = Tenant, PathId = draft.Id, RevisionNumber = 2, Status = KnowledgePathRevisionStatuses.InReview });

        var dto = await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id));

        Assert.True(dto.Paths.Single(p => p.PathId == published.Id).IsCurrentRelease);
        var d = dto.Paths.Single(p => p.PathId == draft.Id);
        Assert.False(d.IsCurrentRelease);
        Assert.Equal(KnowledgePathRevisionStatuses.InReview, d.LatestRevisionStatus);
        Assert.Equal(2, d.LatestRevisionNumber);
        Assert.Equal(1, dto.CurrentReleaseCount);
    }

    // ============================================================ journeys (KP-3 UsesPath)

    [Fact]
    public async Task Journeys_using_the_paths_pinned_or_by_latest_published_code_are_listed_with_their_stage_count()
    {
        var path = Path(_v1, "KP-TR", "tr", "TR");
        var otherPath = Path(_otherChain, "KP-OTHER", "tr", "TR");
        var pinned = Journey("J-PIN", (path, ContentEngagementJourneyPathPin.Pinned), (path, ContentEngagementJourneyPathPin.Pinned));
        var following = Journey("J-LATEST", (new KnowledgePath { Id = Guid.NewGuid(), PathCode = "KP-TR" }, ContentEngagementJourneyPathPin.LatestPublished));
        Journey("J-OTHER", (otherPath, ContentEngagementJourneyPathPin.Pinned));
        Journey("J-ARCH", (path, ContentEngagementJourneyPathPin.Pinned)).ArchivedAt = Now;

        var dto = await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id));

        Assert.Equal(["J-LATEST", "J-PIN"], dto.Journeys.Select(j => j.JourneyCode).ToArray());
        Assert.Equal(2, dto.Journeys.Single(j => j.JourneyId == pinned.Id).StageCount);
        Assert.Equal(1, dto.Journeys.Single(j => j.JourneyId == following.Id).StageCount);
        Assert.Equal(2, dto.JourneyCount);
    }

    // ============================================================ planned visits (count only)

    [Fact]
    public async Task Upcoming_planned_visits_telling_the_paths_are_counted_and_nothing_else()
    {
        var path = Path(_v1, "KP-TR", "tr", "TR");
        var other = Path(_otherChain, "KP-OTHER", "tr", "TR");
        Visit(path, Today, PlannedVisitStatus.Planned);                 // counts
        Visit(path, Today.AddDays(7), PlannedVisitStatus.Confirmed);    // counts
        Visit(path, Today.AddDays(3), PlannedVisitStatus.Draft);        // counts (not cancelled / archived)
        Visit(path, Today.AddDays(2), PlannedVisitStatus.Cancelled);    // cancelled
        Visit(path, Today.AddDays(2), PlannedVisitStatus.Archived);     // archived
        Visit(path, Today.AddDays(-1), PlannedVisitStatus.Planned);     // past
        Visit(other, Today.AddDays(1), PlannedVisitStatus.Planned);     // another path
        Visit(path, Today.AddDays(1), PlannedVisitStatus.Planned).TenantId = OtherTenant;

        var dto = await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id));

        Assert.Equal(3, dto.PlannedVisitCount);
    }

    [Fact]
    public void A_cancelled_plan_never_counts()
    {
        var pathId = Guid.NewGuid();
        var visit = new PlannedVisitEntity
        {
            TenantId = Tenant, PlannedDate = Today, PlanStatus = PlannedVisitStatus.Cancelled,
            ContentItems = [new PlannedVisitContentItem { PathId = pathId }]
        };

        Assert.False(ConceptChainOutputRules.CountsAsPlannedVisit(visit, new HashSet<Guid> { pathId }, Today));
    }

    // ============================================================ permissions

    [Fact]
    public async Task Without_detail_permissions_the_sections_are_counts_only()
    {
        var path = Path(_v1, "KP-TR", "tr", "TR");
        Journey("J-PIN", (path, ContentEngagementJourneyPathPin.Pinned));
        Visit(path, Today, PlannedVisitStatus.Planned);

        var dto = await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id, CanReadPaths: false, CanReadJourneys: false));

        Assert.True(dto.PathsRestricted);
        Assert.True(dto.JourneysRestricted);
        Assert.Empty(dto.Paths);
        Assert.Empty(dto.Journeys);
        Assert.Equal(1, dto.PathCount);
        Assert.Equal(1, dto.JourneyCount);
        Assert.Equal(1, dto.PlannedVisitCount);
    }

    [Fact]
    public async Task Nothing_is_written()
    {
        var path = Path(_v1, "KP-TR", "tr", "TR");
        Journey("J-PIN", (path, ContentEngagementJourneyPathPin.Pinned));
        Visit(path, Today, PlannedVisitStatus.Planned);

        await Run(new GetConceptChainTemplateOutputsQuery(_v1.Id, IncludeOtherVersions: true));

        Assert.Equal(0, _templates.Writes + _paths.Writes + _revisions.Writes + _journeys.Writes + _visits.Writes);
    }

    // ============================================================ fixtures

    private async Task<ConceptChainTemplateOutputsDto> Run(GetConceptChainTemplateOutputsQuery query)
    {
        var response = await Handler().Handle(query, default);
        Assert.True(response.IsSuccessful, string.Join(" | ", response.Errors ?? []));
        return response.Data!;
    }

    private GetConceptChainTemplateOutputsHandler Handler()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Tenant);
        return new GetConceptChainTemplateOutputsHandler(tenant, _templates, _paths, _revisions, _journeys, _visits,
            new FixedClock(Now));
    }

    private ConceptChainTemplate Chain(string code, string version)
    {
        var chain = new ConceptChainTemplate { TenantId = Tenant, SubjectId = Subject, ChainCode = code, ChainVersion = version };
        _templates.Items.Add(chain);
        return chain;
    }

    private KnowledgePath Path(ConceptChainTemplate? chain, string code, string language, string country)
    {
        var path = new KnowledgePath
        {
            TenantId = Tenant, PathCode = code, PathName = code + " name", LanguageCode = language, CountryCode = country,
            PathVersion = "1.0", PathStatus = KnowledgePathStatuses.Draft,
            ChainTemplate = chain is null ? null : new KnowledgePathChainRef { ConceptChainTemplateId = chain.Id, ChainVersion = chain.ChainVersion }
        };
        _paths.Items.Add(path);
        return path;
    }

    private ContentEngagementJourney Journey(string code, params (KnowledgePath Path, string Pin)[] stages)
    {
        var journey = new ContentEngagementJourney
        {
            TenantId = Tenant, JourneyCode = code, JourneyName = code + " name", LanguageCode = "tr",
            JourneyStatus = ContentEngagementJourneyStatuses.Published,
            Stages = stages.Select((s, i) => new ContentEngagementJourneyStage
            {
                StageOrder = i, StageCode = $"S{i}", StageName = $"Stage {i}",
                RecommendedKnowledgePathId = s.Path.Id, PathCode = s.Path.PathCode, PathVersionPinPolicy = s.Pin
            }).ToList()
        };
        _journeys.Items.Add(journey);
        return journey;
    }

    private PlannedVisitEntity Visit(KnowledgePath path, DateOnly date, string status)
    {
        var visit = new PlannedVisitEntity
        {
            TenantId = Tenant, PlannedDate = date, PlanStatus = status,
            ContentItems = [new PlannedVisitContentItem { PathId = path.Id, PathCode = path.PathCode }]
        };
        _visits.Items.Add(visit);
        return visit;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>One in-memory store serving every repository the read touches; any write is counted.</summary>
    private sealed class Repo<T> : IConceptChainTemplateRepository, IKnowledgePathRepository, IKnowledgePathRevisionRepository,
        IContentEngagementJourneyRepository, IPlannedVisitRepository
        where T : EntityBase
    {
        public List<T> Items { get; } = new();
        public int Writes { get; private set; }

        private IEnumerable<TE> Of<TE>(Guid t) where TE : EntityBase => Items.OfType<TE>().Where(x => x.TenantId == t);

        Task<ConceptChainTemplate?> IConceptChainTemplateRepository.GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of<ConceptChainTemplate>(t).FirstOrDefault(x => x.Id == id));
        Task<IReadOnlyList<ConceptChainTemplate>> IConceptChainTemplateRepository.ListAsync(Guid t, CancellationToken ct) => Task.FromResult<IReadOnlyList<ConceptChainTemplate>>(Of<ConceptChainTemplate>(t).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct) => Task.FromResult<IReadOnlyList<ConceptChainTemplate>>(Of<ConceptChainTemplate>(t).Where(x => x.SubjectId == s).ToList());
        public Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid t, Guid s, string code, CancellationToken ct) => Task.FromResult<IReadOnlyList<ConceptChainTemplate>>(Of<ConceptChainTemplate>(t).Where(x => x.SubjectId == s && x.ChainCode == code).ToList());
        public Task InsertAsync(ConceptChainTemplate e, CancellationToken ct) { Writes++; return Task.CompletedTask; }
        public Task UpdateAsync(ConceptChainTemplate e, CancellationToken ct) { Writes++; return Task.CompletedTask; }

        Task<KnowledgePath?> IKnowledgePathRepository.GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of<KnowledgePath>(t).FirstOrDefault(x => x.Id == id));
        Task<IReadOnlyList<KnowledgePath>> IKnowledgePathRepository.ListAsync(Guid t, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgePath>>(Of<KnowledgePath>(t).ToList());
        Task<IReadOnlyList<KnowledgePath>> IKnowledgePathRepository.ListByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgePath>>(Of<KnowledgePath>(t).Where(x => x.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath e, CancellationToken ct) { Writes++; return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath e, int v, CancellationToken ct) { Writes++; return Task.FromResult(true); }

        Task<KnowledgePathRevision?> IKnowledgePathRevisionRepository.GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of<KnowledgePathRevision>(t).FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<KnowledgePathRevision>> ListByPathAsync(Guid t, Guid pathId, CancellationToken ct) => Task.FromResult<IReadOnlyList<KnowledgePathRevision>>(Of<KnowledgePathRevision>(t).Where(x => x.PathId == pathId).ToList());
        public Task InsertAsync(KnowledgePathRevision e, CancellationToken ct) { Writes++; return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePathRevision e, int v, CancellationToken ct) { Writes++; return Task.FromResult(true); }

        Task<ContentEngagementJourney?> IContentEngagementJourneyRepository.GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of<ContentEngagementJourney>(t).FirstOrDefault(x => x.Id == id));
        Task<IReadOnlyList<ContentEngagementJourney>> IContentEngagementJourneyRepository.ListAsync(Guid t, CancellationToken ct) => Task.FromResult<IReadOnlyList<ContentEngagementJourney>>(Of<ContentEngagementJourney>(t).ToList());
        Task<IReadOnlyList<ContentEngagementJourney>> IContentEngagementJourneyRepository.ListByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<IReadOnlyList<ContentEngagementJourney>>(Of<ContentEngagementJourney>(t).Where(x => x.JourneyCode == code).ToList());
        public Task InsertAsync(ContentEngagementJourney e, CancellationToken ct) { Writes++; return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(ContentEngagementJourney e, int v, CancellationToken ct) { Writes++; return Task.FromResult(true); }

        Task<PlannedVisitEntity?> IPlannedVisitRepository.GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of<PlannedVisitEntity>(t).FirstOrDefault(x => x.Id == id));
        Task<IReadOnlyList<PlannedVisitEntity>> IPlannedVisitRepository.ListAsync(Guid t, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Of<PlannedVisitEntity>(t).ToList());
        Task<IReadOnlyList<PlannedVisitEntity>> IPlannedVisitRepository.ListByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>([]);
        public Task<IReadOnlyList<PlannedVisitEntity>> ListByResourceAndDateAsync(Guid t, string r, DateOnly d, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>([]);
        public Task<IReadOnlyList<PlannedVisitEntity>> ListByTargetAndDateAsync(Guid t, Guid target, DateOnly d, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>([]);
        // Mirrors the Mongo narrowing (tenant + date + path) ONLY; the status rule is the handler's.
        public Task<IReadOnlyList<PlannedVisitEntity>> ListFromDateByContentPathsAsync(Guid t, IReadOnlyCollection<Guid> pathIds, DateOnly from, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Of<PlannedVisitEntity>(t)
                .Where(x => x.PlannedDate >= from && x.ContentItems.Any(i => pathIds.Contains(i.PathId))).ToList());
        public Task InsertAsync(PlannedVisitEntity e, CancellationToken ct) { Writes++; return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(PlannedVisitEntity e, int v, CancellationToken ct) { Writes++; return Task.FromResult(true); }
    }
}
