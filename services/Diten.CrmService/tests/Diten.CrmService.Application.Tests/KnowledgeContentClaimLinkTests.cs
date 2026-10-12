using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge;
using Diten.CrmService.Application.Features.Knowledge.Concept;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Content.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Content.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-CL-BE-6 — KnowledgeContent ↔ claim link. Pins down: save-time validation (foreign / mismatched claim, other
/// country's version, product mismatch, ≤20, status free), the publish gate (claim_not_approved,
/// claim_language_mismatch, review-required passes and is flagged, empty refs = old behaviour), the detail enrichment,
/// the usage read (content / knowledge path / journey groups — WP-KP-4: the retired content set is no longer a source —,
/// archived excluded, countryCode filter, tenant isolation) and persistence (legacy document reads, Guids stored as strings so the by-claim filter matches).
/// </summary>
public sealed class KnowledgeContentClaimLinkTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProductX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ProductY = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeContents Contents { get; } = new();
        public FakeSubjects Subjects { get; } = new();
        public FakeClaims Claims { get; } = new();
        public FakeVersions Versions { get; } = new();
        public FakePaths Paths { get; } = new();
        public FakeJourneys Journeys { get; } = new();
        public CapturingAudit Audit { get; } = new();
        public Guid SubjectId { get; }

        public Fixture()
        {
            SubjectId = Subjects.Seed(TenantA);
            Subjects.Seed(TenantB, SubjectId);
        }

        public CreateKnowledgeContentHandler Create(Guid? tenant = null, bool withClaims = true)
            => new(Tenant(tenant ?? TenantA), new NullActorContext(), Contents, Subjects, new NoTopics(),
                new NoProfiles(), new NoNodes(), withClaims ? Claims : null, withClaims ? Versions : null, Audit);

        public UpdateKnowledgeContentHandler Update()
            => new(Tenant(TenantA), new NullActorContext(), Contents, Subjects, new NoTopics(), new NoProfiles(),
                new NoNodes(), Audit, Claims, Versions);

        public GetKnowledgeContentHandler Get() => new(Tenant(TenantA), Contents, Claims, Versions);

        public GetClaimUsageHandler Usage(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantA), Claims, Versions, Contents, Paths, Journeys);

        public Claim SeedClaim(string code, string status, Guid? product = null, Guid? tenant = null)
        {
            var claim = new Claim
            {
                TenantId = tenant ?? TenantA, ClaimCode = code, ClaimName = code, ClaimText = "t", ClaimVersion = "1.0",
                Status = status, ProductId = product, EffectiveFrom = Jan1
            };
            Claims.Items.Add(claim);
            return claim;
        }

        public ClaimCountryVersion SeedVersion(Claim claim, string country, string status, params string[] languages)
        {
            var version = new ClaimCountryVersion
            {
                TenantId = claim.TenantId, ClaimCode = claim.ClaimCode, ClaimId = claim.Id, CountryCode = country,
                Status = status, AdaptationTypeCode = "verbatim", ValidFrom = Jan1
            };
            foreach (var language in languages.Length == 0 ? new[] { "tr" } : languages)
            {
                version.Texts.Add(new ClaimLocalizedText { LanguageCode = language, Text = "x" });
            }

            Versions.Items.Add(version);
            return version;
        }

        public CreateKnowledgeContentCommand ContentCmd(
            string code = "KC-1",
            string status = KnowledgeContentStatuses.Draft,
            string language = "tr",
            Guid? product = null,
            params KnowledgeContentClaimRefInput[] refs)
            => new(code, "Content " + code, KnowledgeContentTypes.Presentation, SubjectId, language, "1.0", Jan1,
                ContentStatus: status, ProductId: product, Url: "https://example.test/" + code,
                ClaimRefs: refs.Length == 0 ? null : refs);

        public UpdateKnowledgeContentCommand UpdateCmd(
            Guid id,
            string status,
            string language = "tr",
            Guid? product = null,
            IReadOnlyList<KnowledgeContentClaimRefInput>? refs = null)
            => new(id, "Content", KnowledgeContentTypes.Presentation, SubjectId, language, "1.0", Jan1,
                ContentStatus: status, ProductId: product, Url: "https://example.test/u", ClaimRefs: refs);
    }

    private static KnowledgeContentClaimRefInput Core(Claim c) => new(c.ClaimCode, c.Id);

    private static KnowledgeContentClaimRefInput Country(Claim c, ClaimCountryVersion v, string? country = null)
        => new(c.ClaimCode, c.Id, v.Id, country ?? v.CountryCode);

    private static string Code<T>(Diten.CrmService.Application.Common.Models.Response<T> r) => r.Errors![0];

    // ============================================================ save-time validation

    [Fact]
    public async Task Save_rejects_a_claim_of_another_tenant_or_another_code()
    {
        var fx = new Fixture();
        var foreign = fx.SeedClaim("CL-B", ClaimStatuses.Approved, tenant: TenantB);
        var r1 = await fx.Create().Handle(fx.ContentCmd(refs: Core(foreign)), default);
        Assert.Equal(400, r1.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimNotFound, Code(r1));

        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var r2 = await fx.Create().Handle(fx.ContentCmd(refs: new KnowledgeContentClaimRefInput("CL-OTHER", a.Id)), default);
        Assert.Equal(400, r2.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimRefMismatch, Code(r2));
        Assert.Empty(fx.Contents.Items);
    }

    [Fact]
    public async Task Save_rejects_a_country_version_of_another_country_or_claim()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var b = fx.SeedClaim("CL-B", ClaimStatuses.Approved);
        var de = fx.SeedVersion(a, "DE", ClaimStatuses.Approved, "de");
        var bTr = fx.SeedVersion(b, "TR", ClaimStatuses.Approved);

        var r1 = await fx.Create().Handle(fx.ContentCmd(refs: Country(a, de, "TR")), default);
        Assert.Equal(400, r1.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimRefMismatch, Code(r1));

        var r2 = await fx.Create().Handle(fx.ContentCmd(refs: new KnowledgeContentClaimRefInput("CL-A", a.Id, bTr.Id)), default);
        Assert.Equal(400, r2.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimRefMismatch, Code(r2));

        var r3 = await fx.Create().Handle(
            fx.ContentCmd(refs: new KnowledgeContentClaimRefInput("CL-A", a.Id, Guid.NewGuid(), "TR")), default);
        Assert.Equal(KnowledgeContentClaimErrors.CountryVersionNotFound, Code(r3));
    }

    [Fact]
    public async Task Save_rejects_product_mismatch_but_allows_an_unset_side()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved, ProductX);

        var mismatch = await fx.Create().Handle(fx.ContentCmd(product: ProductY, refs: Core(a)), default);
        Assert.Equal(400, mismatch.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimProductMismatch, Code(mismatch));

        Assert.Equal(201, (await fx.Create().Handle(fx.ContentCmd("KC-2", product: ProductX, refs: Core(a)), default)).StatusCode);
        Assert.Equal(201, (await fx.Create().Handle(fx.ContentCmd("KC-3", product: null, refs: Core(a)), default)).StatusCode);
    }

    [Fact]
    public async Task Save_is_status_free_draft_content_binds_a_draft_claim_and_stores_normalised_refs()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Draft);
        var tr = fx.SeedVersion(a, "TR", ClaimStatuses.Draft);

        var r = await fx.Create().Handle(
            fx.ContentCmd(refs: new KnowledgeContentClaimRefInput(" cl-a ", a.Id, tr.Id)), default);
        Assert.Equal(201, r.StatusCode);
        var stored = Assert.Single(Assert.Single(fx.Contents.Items).ClaimRefs);
        Assert.Equal("CL-A", stored.ClaimCode);     // canonical code from the claim record
        Assert.Equal("TR", stored.CountryCode);     // taken from the version
        var evt = Assert.Single(fx.Audit.Events);
        Assert.Equal(KnowledgeReasonCodes.ContentClaimRefsChanged, evt.Event);
        Assert.Equal("KC-1;claimRefs=1;added=1;removed=0", evt.Detail);
    }

    [Fact]
    public async Task Save_rejects_more_than_twenty_refs_and_duplicates()
    {
        var fx = new Fixture();
        var refs = Enumerable.Range(0, 21).Select(i => Core(fx.SeedClaim("CL-" + i, ClaimStatuses.Approved))).ToArray();
        var tooMany = await fx.Create().Handle(fx.ContentCmd(refs: refs), default);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimRefsTooMany, Code(tooMany));

        var dup = await fx.Create().Handle(fx.ContentCmd(refs: new[] { refs[0], refs[0] }), default);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimRefDuplicate, Code(dup));
    }

    [Fact]
    public async Task Update_without_claim_refs_keeps_them_and_an_empty_list_clears_them()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var id = (await fx.Create().Handle(fx.ContentCmd(refs: Core(a)), default)).Data;

        Assert.True((await fx.Update().Handle(fx.UpdateCmd(id, KnowledgeContentStatuses.Draft), default)).IsSuccessful);
        Assert.Single(fx.Contents.Items[0].ClaimRefs);
        Assert.Single(fx.Audit.Events); // untouched refs → no change event

        Assert.True((await fx.Update().Handle(
            fx.UpdateCmd(id, KnowledgeContentStatuses.Draft, refs: Array.Empty<KnowledgeContentClaimRefInput>()),
            default)).IsSuccessful);
        Assert.Empty(fx.Contents.Items[0].ClaimRefs);
        Assert.Equal("KC-1;claimRefs=0;added=0;removed=1", fx.Audit.Events[^1].Detail);
    }

    [Fact]
    public async Task Update_changing_product_revalidates_existing_refs()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved, ProductX);
        var id = (await fx.Create().Handle(fx.ContentCmd(product: ProductX, refs: Core(a)), default)).Data;

        var r = await fx.Update().Handle(fx.UpdateCmd(id, KnowledgeContentStatuses.Draft, product: ProductY), default);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimProductMismatch, Code(r));
        Assert.Equal(ProductX, fx.Contents.Items[0].ProductId);
    }

    // ============================================================ publish gate

    [Fact]
    public async Task Publish_is_blocked_when_the_country_version_is_not_approved()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var tr = fx.SeedVersion(a, "TR", ClaimStatuses.InReview);

        var create = await fx.Create().Handle(
            fx.ContentCmd(status: KnowledgeContentStatuses.Published, refs: Country(a, tr)), default);
        Assert.Equal(409, create.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimNotApproved, Code(create));

        var id = (await fx.Create().Handle(fx.ContentCmd(refs: Country(a, tr)), default)).Data;
        var publish = await fx.Update().Handle(fx.UpdateCmd(id, KnowledgeContentStatuses.Published), default);
        Assert.Equal(409, publish.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimNotApproved, Code(publish));
        Assert.Equal(KnowledgeContentStatuses.Draft, fx.Contents.Items[0].ContentStatus);

        tr.Status = ClaimStatuses.Approved;
        Assert.True((await fx.Update().Handle(fx.UpdateCmd(id, KnowledgeContentStatuses.Published), default)).IsSuccessful);
        Assert.Equal(KnowledgeContentStatuses.Published, fx.Contents.Items[0].ContentStatus);
    }

    [Fact]
    public async Task Publish_is_blocked_for_a_core_ref_whose_claim_is_not_approved()
    {
        var fx = new Fixture();
        var draft = fx.SeedClaim("CL-D", ClaimStatuses.Draft);
        var approved = fx.SeedClaim("CL-OK", ClaimStatuses.Approved);

        var blocked = await fx.Create().Handle(
            fx.ContentCmd(status: KnowledgeContentStatuses.Published, language: "en", refs: Core(draft)), default);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimNotApproved, Code(blocked));

        var ok = await fx.Create().Handle(
            fx.ContentCmd("KC-2", KnowledgeContentStatuses.Published, "en", refs: Core(approved)), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Publish_is_blocked_when_the_content_language_is_not_a_version_text_language()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var de = fx.SeedVersion(a, "DE", ClaimStatuses.Approved, "de");

        var r = await fx.Create().Handle(
            fx.ContentCmd(status: KnowledgeContentStatuses.Published, language: "en", refs: Country(a, de)), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimLanguageMismatch, Code(r));

        var ok = await fx.Create().Handle(
            fx.ContentCmd("KC-2", KnowledgeContentStatuses.Published, "DE", refs: Country(a, de)), default);
        Assert.Equal(201, ok.StatusCode);
    }

    [Fact]
    public async Task Review_required_does_not_block_and_the_detail_read_flags_it()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var tr = fx.SeedVersion(a, "TR", ClaimStatuses.ReviewRequired);

        var r = await fx.Create().Handle(
            fx.ContentCmd(status: KnowledgeContentStatuses.Published, refs: Country(a, tr)), default);
        Assert.Equal(201, r.StatusCode);

        var dto = (await fx.Get().Handle(new GetKnowledgeContentQuery(r.Data), default)).Data!;
        var claimRef = Assert.Single(dto.ClaimRefs!);
        Assert.True(claimRef.ClaimNeedsReview);
        Assert.Equal(ClaimStatuses.Approved, claimRef.ClaimStatus);
        Assert.Equal(ClaimStatuses.ReviewRequired, claimRef.CountryVersionStatus);
        Assert.Equal("TR", claimRef.CountryCode);
    }

    [Fact]
    public async Task Empty_claim_refs_keep_the_old_behaviour_even_without_a_claim_store()
    {
        var fx = new Fixture();
        var r = await fx.Create(withClaims: false).Handle(
            fx.ContentCmd(status: KnowledgeContentStatuses.Published, language: "en"), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Empty(fx.Contents.Items[0].ClaimRefs);
        Assert.Empty(fx.Audit.Events);

        var dto = (await fx.Get().Handle(new GetKnowledgeContentQuery(r.Data), default)).Data!;
        Assert.Empty(dto.ClaimRefs!);
    }

    [Fact]
    public async Task An_already_published_content_is_not_regated_by_an_unrelated_edit()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var tr = fx.SeedVersion(a, "TR", ClaimStatuses.Approved);
        var id = (await fx.Create().Handle(
            fx.ContentCmd(status: KnowledgeContentStatuses.Published, refs: Country(a, tr)), default)).Data;

        tr.Status = ClaimStatuses.Inactive; // superseded later
        Assert.True((await fx.Update().Handle(fx.UpdateCmd(id, KnowledgeContentStatuses.Published), default)).IsSuccessful);

        // …but switching the language of a published content re-runs the gate.
        var lang = await fx.Update().Handle(fx.UpdateCmd(id, KnowledgeContentStatuses.Published, "en"), default);
        Assert.Equal(KnowledgeContentClaimErrors.ClaimNotApproved, Code(lang));
    }

    // ============================================================ usage read

    private static async Task<(Fixture Fx, Claim Claim)> SeedUsageAsync()
    {
        var fx = new Fixture();
        var a = fx.SeedClaim("CL-A", ClaimStatuses.Approved);
        var a2 = fx.SeedClaim("CL-A", ClaimStatuses.Draft); // a second record (newer version) of the same code
        var tr = fx.SeedVersion(a, "TR", ClaimStatuses.ReviewRequired);
        var de = fx.SeedVersion(a, "DE", ClaimStatuses.Approved, "de");

        var trContent = (await fx.Create().Handle(fx.ContentCmd("KC-TR", refs: Country(a, tr)), default)).Data;
        await fx.Create().Handle(fx.ContentCmd("KC-DE", language: "de", refs: Country(a, de)), default);
        await fx.Create().Handle(fx.ContentCmd("KC-GL", language: "en", refs: Core(a)), default);
        var archived = (await fx.Create().Handle(fx.ContentCmd("KC-ARCH", refs: Country(a, tr)), default)).Data;
        fx.Contents.Items.Single(c => c.Id == archived).ArchivedAt = Jan1;
        await fx.Create().Handle(fx.ContentCmd("KC-NONE"), default);

        // WP-KP-4 — knowledge paths that PLACED a record of the code (KP-1 KnowledgePath.Claims); the archived one is
        // not a use.
        fx.Paths.Items.Add(new KnowledgePath
        {
            TenantId = TenantA, PathCode = "KP-TR", PathName = "Almiba TR", PathVersion = "2.0", CountryCode = "TR",
            LanguageCode = "tr", PathStatus = KnowledgePathStatuses.Published,
            Claims = { new KnowledgePathClaim { ClaimId = a.Id, ClaimCode = "CL-A" } }
        });
        fx.Paths.Items.Add(new KnowledgePath
        {
            TenantId = TenantA, PathCode = "KP-ARCH", PathName = "Archived", CountryCode = "TR", ArchivedAt = Jan1,
            Claims = { new KnowledgePathClaim { ClaimId = a2.Id, ClaimCode = "CL-A" } }
        });

        var path = new KnowledgePath
        {
            TenantId = TenantA, PathCode = "P-1", PathName = "Path",
            Steps = { new KnowledgePathStep { StepCode = "S1", ContentId = trContent, ContentCode = "KC-TR" } }
        };
        fx.Paths.Items.Add(path);
        fx.Journeys.Items.Add(new ContentEngagementJourney
        {
            TenantId = TenantA, JourneyCode = "J-1", JourneyName = "Journey", JourneyVersion = "1.0", LanguageCode = "tr",
            Stages = { new ContentEngagementJourneyStage { StageCode = "ST1", RecommendedKnowledgePathId = path.Id, PathCode = "P-1" } }
        });
        fx.Journeys.Items.Add(new ContentEngagementJourney
        {
            TenantId = TenantA, JourneyCode = "J-ARCH", JourneyName = "Old", ArchivedAt = Jan1,
            Stages = { new ContentEngagementJourneyStage { StageCode = "ST1", RecommendedKnowledgePathId = path.Id } }
        });
        return (fx, a);
    }

    [Fact]
    public async Task Usage_groups_contents_knowledge_paths_and_journeys_by_country_and_excludes_archived()
    {
        var (fx, _) = await SeedUsageAsync();
        var r = await fx.Usage().Handle(new GetClaimUsageQuery("CL-A"), default);
        Assert.True(r.IsSuccessful);
        var groups = r.Data!.Groups.ToDictionary(g => g.CountryCode);
        Assert.Equal(new[] { "GLOBAL", "DE", "TR" }, r.Data.Groups.Select(g => g.CountryCode));

        var tr = groups["TR"].Items;
        Assert.Collection(tr,
            i => { Assert.Equal(("content", "KC-TR"), (i.Type, i.Code)); Assert.True(i.ClaimNeedsReview); },
            i =>
            {
                Assert.Equal(("knowledge-path", "KP-TR", "Almiba TR"), (i.Type, i.Code, i.Name));
                Assert.Equal(("2.0", KnowledgePathStatuses.Published, "TR"), (i.Version, i.Status, i.CountryCode));
                Assert.True(i.ClaimNeedsReview);              // the TR version of the claim is review-required
            },
            i => { Assert.Equal(("journey", "J-1"), (i.Type, i.Code)); Assert.Equal("KC-TR", i.Via); Assert.True(i.ClaimNeedsReview); });

        Assert.Equal(new[] { "KC-DE" }, groups["DE"].Items.Select(i => i.Code));
        Assert.False(groups["DE"].Items[0].ClaimNeedsReview);
        // GLOBAL = the core-bound content only; WP-KP-4: the path groups under its own country (TR), nowhere else.
        Assert.Equal(new[] { ("content", "KC-GL") }, groups["GLOBAL"].Items.Select(i => (i.Type, i.Code)));

        var all = r.Data.Groups.SelectMany(g => g.Items).Select(i => i.Code).ToList();
        Assert.DoesNotContain("KC-ARCH", all);
        Assert.DoesNotContain("KP-ARCH", all);
        Assert.DoesNotContain(r.Data.Groups.SelectMany(g => g.Items), i => i.Type == "content-set");   // never produced
        Assert.DoesNotContain("J-ARCH", all);
        Assert.DoesNotContain("KC-NONE", all);
    }

    [Fact]
    public async Task Usage_country_filter_returns_only_that_group()
    {
        var (fx, _) = await SeedUsageAsync();
        var r = await fx.Usage().Handle(new GetClaimUsageQuery("CL-A", "de"), default);
        var group = Assert.Single(r.Data!.Groups);
        Assert.Equal("DE", group.CountryCode);
        Assert.Equal(new[] { "KC-DE" }, group.Items.Select(i => i.Code));
    }

    [Fact]
    public async Task Usage_is_tenant_isolated_and_requires_a_claim_code()
    {
        var (fx, _) = await SeedUsageAsync();
        var other = await fx.Usage(TenantB).Handle(new GetClaimUsageQuery("CL-A"), default);
        Assert.True(other.IsSuccessful);
        Assert.Empty(other.Data!.Groups);

        var missing = await fx.Usage().Handle(new GetClaimUsageQuery(" "), default);
        Assert.Equal(400, missing.StatusCode);
    }

    // ============================================================ WP-CL-FE-1 — list counters (includeCounts)

    private static ListClaimsHandler ListWithCounts(Fixture fx, FakeClaimEvidenceClient evidence) =>
        new(Tenant(TenantA), fx.Claims, fx.Versions, null, new ClaimEvidenceReviewer(evidence, fx.Claims, fx.Versions),
            null, fx.Contents, fx.Paths, fx.Journeys);

    [Fact]
    public async Task List_counts_read_each_source_once_per_page()
    {
        var (fx, a) = await SeedUsageAsync();
        var a2 = fx.Claims.Items.Single(c => c.ClaimCode == "CL-A" && c.Id != a.Id);
        var b = fx.SeedClaim("CL-B", ClaimStatuses.Draft);
        var evidence = new FakeClaimEvidenceClient();
        evidence.Seed(ClaimEvidenceRules.For(a));
        evidence.Seed(ClaimEvidenceRules.For(a));
        evidence.Seed(ClaimEvidenceRules.For(a), status: "removed");
        var de = fx.Versions.Items.Single(v => v.CountryCode == "DE");
        de.ValidTo = DateTimeOffset.UtcNow.AddDays(10);
        var before = (fx.Contents.ListCalls, fx.Paths.ListCalls, fx.Journeys.ListCalls);

        var r = await ListWithCounts(fx, evidence).Handle(new ListClaimsQuery(IncludeCounts: true), default);

        Assert.Equal(200, r.StatusCode);
        var rows = r.Data!.Items.ToDictionary(i => i.ClaimId);
        Assert.Equal(2, rows[a.Id].EvidenceCount);
        Assert.Equal(0, rows[a2.Id].EvidenceCount);
        Assert.Equal(5, rows[a.Id].UsageCount); // KC-TR, KC-DE, KC-GL + KP-TR + J-1 (archived ones excluded)
        Assert.Equal(5, rows[a2.Id].UsageCount); // usage is per claim code
        Assert.Equal(0, rows[b.Id].UsageCount);
        Assert.Equal(2, rows[a.Id].ApprovedCountryCount); // TR review-required + DE approved
        Assert.Equal(0, rows[b.Id].ApprovedCountryCount);
        Assert.Equal(new[] { "DE" }, rows[a.Id].ExpiringCountryCodes);
        Assert.Equal(1, evidence.QueryCalls); // ONE bulk evidence read for the whole page
        Assert.Equal((before.Item1 + 1, before.Item2 + 1, before.Item3 + 1),
            (fx.Contents.ListCalls, fx.Paths.ListCalls, fx.Journeys.ListCalls));
    }

    [Fact]
    public async Task List_without_include_counts_leaves_counters_null_and_reads_no_usage_source()
    {
        var (fx, _) = await SeedUsageAsync();
        var before = fx.Contents.ListCalls;

        var r = await ListWithCounts(fx, new FakeClaimEvidenceClient()).Handle(new ListClaimsQuery(), default);

        Assert.All(r.Data!.Items, i =>
        {
            Assert.Null(i.EvidenceCount);
            Assert.Null(i.UsageCount);
            Assert.Null(i.ApprovedCountryCount);
        });
        Assert.Equal(before, fx.Contents.ListCalls);
    }

    [Fact]
    public async Task List_counts_evidence_is_null_when_the_evidence_service_is_down_and_the_list_still_works()
    {
        var (fx, a) = await SeedUsageAsync();
        var evidence = new FakeClaimEvidenceClient { Unavailable = true };

        var r = await ListWithCounts(fx, evidence).Handle(new ListClaimsQuery(IncludeCounts: true), default);

        Assert.Equal(200, r.StatusCode);
        var row = r.Data!.Items.Single(i => i.ClaimId == a.Id);
        Assert.Null(row.EvidenceCount);
        Assert.Equal(5, row.UsageCount);
    }

    [Fact]
    public async Task List_counts_usage_is_null_when_a_usage_source_is_not_available()
    {
        var (fx, a) = await SeedUsageAsync();
        var handler = new ListClaimsHandler(Tenant(TenantA), fx.Claims, fx.Versions, null, null, null, fx.Contents,
            null, fx.Journeys);

        var row = (await handler.Handle(new ListClaimsQuery(IncludeCounts: true), default)).Data!.Items
            .Single(i => i.ClaimId == a.Id);

        Assert.Null(row.UsageCount);
        Assert.Null(row.EvidenceCount);
        Assert.Equal(2, row.ApprovedCountryCount);
    }

    // WP-KP-4 — a knowledge path groups under its own country (its identity since KP-1); a path without a country (a
    // legacy path) groups under GLOBAL.
    [Theory]
    [InlineData("tr", "TR")]
    [InlineData(" DE ", "DE")]
    [InlineData(null, "GLOBAL")]
    [InlineData("", "GLOBAL")]
    public void A_knowledge_path_groups_under_its_own_country(string? country, string expected)
        => Assert.Equal(expected, GetClaimUsageHandler.PathGroup(new KnowledgePath { CountryCode = country }));

    [Fact]
    public async Task Usage_reads_the_path_country_from_the_path_and_matches_by_claim_record()
    {
        var (fx, _) = await SeedUsageAsync();
        fx.Paths.Items.Single(p => p.PathCode == "KP-TR").CountryCode = "FR"; // no FR version of the claim exists

        var group = Assert.Single((await fx.Usage().Handle(new GetClaimUsageQuery("CL-A", "fr"), default)).Data!.Groups);
        Assert.Equal("FR", group.CountryCode);
        var item = Assert.Single(group.Items);
        Assert.Equal(("knowledge-path", "KP-TR"), (item.Type, item.Code));
        Assert.False(item.ClaimNeedsReview);                         // no FR version of the record is in review

        // Another claim's record on the path is not a use of CL-A.
        fx.Paths.Items.Single(p => p.PathCode == "KP-TR").Claims[0].ClaimId = Guid.NewGuid();
        Assert.Empty((await fx.Usage().Handle(new GetClaimUsageQuery("CL-A", "fr"), default)).Data!.Groups);
    }

    [Fact]
    public void Usage_endpoint_sits_under_the_claims_prefix_with_the_read_permission()
    {
        var method = typeof(ClaimUsageController).GetMethod(nameof(ClaimUsageController.Get))!;
        var attribute = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Contains("GET", attribute.HttpMethods);
        Assert.Equal("api/crm/content-composition/claims/usage", attribute.Template);
        Assert.Equal(ClaimPermissions.Read, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    // ============================================================ persistence

    [Fact]
    public void Legacy_content_document_without_claim_refs_reads_with_an_empty_list()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var legacy = new KnowledgeContent
        {
            TenantId = TenantA, ContentCode = "KC-OLD", ContentTitle = "Old", ContentType = "presentation",
            LanguageCode = "en", ContentVersion = "1.0", EffectiveFrom = Jan1
        }.ToBsonDocument();
        legacy.Remove("ClaimRefs");

        var read = BsonSerializer.Deserialize<KnowledgeContent>(legacy);
        Assert.Equal("KC-OLD", read.ContentCode);
        Assert.Empty(read.ClaimRefs);
        Assert.Empty(KnowledgeMapper.ToDto(read).ClaimRefs!);
    }

    [Fact]
    public void Claim_ref_guids_are_stored_as_strings_so_the_by_claim_filter_matches()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(KnowledgeContentClaimRef)));
        var claimId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var content = new KnowledgeContent
        {
            TenantId = TenantA,
            ClaimRefs = { new KnowledgeContentClaimRef { ClaimCode = "CL-A", ClaimId = claimId, CountryVersionId = versionId, CountryCode = "TR" } }
        };
        var doc = content.ToBsonDocument();
        var stored = doc["ClaimRefs"].AsBsonArray[0].AsBsonDocument;
        Assert.Equal(BsonType.String, stored["ClaimId"].BsonType);
        Assert.Equal(claimId.ToString(), stored["ClaimId"].AsString);
        Assert.Equal(BsonType.String, stored["CountryVersionId"].BsonType);
        Assert.Equal(claimId, BsonSerializer.Deserialize<KnowledgeContent>(doc).ClaimRefs[0].ClaimId);

        // A by-id filter renders the SAME string representation as the stored value (no binary-vs-string miss).
        var filter = Builders<KnowledgeContent>.Filter.ElemMatch(c => c.ClaimRefs, r => r.ClaimId == claimId);
        var rendered = filter.Render(
            BsonSerializer.SerializerRegistry.GetSerializer<KnowledgeContent>(), BsonSerializer.SerializerRegistry);
        Assert.Equal(BsonType.String, rendered["ClaimRefs"]["$elemMatch"]["ClaimId"].BsonType);
    }

    // ============================================================ fakes

    private sealed class FakeCatalog : IReferenceDataCatalogReader
    {
        private readonly string[] _codes;

        public FakeCatalog(params string[] codes) => _codes = codes;

        public string? RequestedSet { get; private set; }

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
        {
            RequestedSet = setCode;
            return Task.FromResult(new ReferenceSetSnapshot(setCode, true, _codes
                .Select(c => new ReferenceValueSnapshot(c, c, null, true, false, null)).ToList()));
        }
    }

    private sealed class CapturingAudit : IKnowledgeConceptAuditPublisher
    {
        public List<(string Event, Guid EntityId, string? Detail)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityId, detail));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeContents : IKnowledgeContentRepository
    {
        public List<KnowledgeContent> Items { get; } = new();

        public Task<KnowledgeContent?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));

        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid t, CancellationToken ct)
            { ListCalls++; return Task.FromResult((IReadOnlyList<KnowledgeContent>)Items.Where(c => c.TenantId == t).ToList()); }

        public Task<KnowledgeContent?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ContentCode == code && !c.IsArchived()));

        public Task InsertAsync(KnowledgeContent content, CancellationToken ct)
        {
            Items.Add(content);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(KnowledgeContent content, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSubjects : ISubjectRepository
    {
        private readonly List<Subject> _items = new();

        public Guid Seed(Guid tenant, Guid? id = null)
        {
            var subject = new Subject { TenantId = tenant, SubjectCode = "SUB", SubjectName = "Subject", Status = TaxonomyStatuses.Active };
            if (id is { } fixedId)
            {
                subject.Id = fixedId;
            }

            _items.Add(subject);
            return subject.Id;
        }

        public Task<Subject?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(_items.FirstOrDefault(s => s.TenantId == t && s.Id == id));

        public Task<IReadOnlyList<Subject>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Subject>)_items.Where(s => s.TenantId == t).ToList());

        public Task<Subject?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult<Subject?>(null);

        public Task InsertAsync(Subject subject, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Subject subject, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoTopics : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)new List<Topic>());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Topic>)new List<Topic>());
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid subjectId, string code, CancellationToken ct)
            => Task.FromResult<Topic?>(null);
        public Task InsertAsync(Topic topic, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Topic topic, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoProfiles : IAudienceProfileRepository
    {
        public Task<AudienceProfile?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult<AudienceProfile?>(null);
        public Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<AudienceProfile>)new List<AudienceProfile>());
        public Task<AudienceProfile?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult<AudienceProfile?>(null);
        public Task InsertAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(AudienceProfile profile, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoNodes : IConceptNodeRepository
    {
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<ConceptNode?>(null);
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)new List<ConceptNode>());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConceptNode>)new List<ConceptNode>());
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid subjectId, Guid typeId, string code, CancellationToken ct)
            => Task.FromResult<ConceptNode?>(null);
        public Task InsertAsync(ConceptNode n, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ConceptNode n, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClaims : IClaimRepository
    {
        public List<Claim> Items { get; } = new();

        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t).ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(c => c.TenantId == t && c.ClaimCode == code).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.ClaimCode == code && !c.IsArchived()));
        public Task InsertAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Claim entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeVersions : IClaimCountryVersionRepository
    {
        public List<ClaimCountryVersion> Items { get; } = new();

        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(v => v.TenantId == t && v.Id == id));
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(v => v.TenantId == t && v.ClaimCode == code).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(v => v.TenantId == t && v.ClaimId == claimId).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(v => v.TenantId == t).ToList());
        public Task InsertAsync(ClaimCountryVersion entity, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ClaimCountryVersion entity, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePaths : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();

        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct)
            { ListCalls++; return Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(p => p.TenantId == t).ToList()); }
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(p => p.TenantId == t && p.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath entity, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeJourneys : IContentEngagementJourneyRepository
    {
        public List<ContentEngagementJourney> Items { get; } = new();

        public Task<ContentEngagementJourney?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(j => j.TenantId == t && j.Id == id));
        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<ContentEngagementJourney>> ListAsync(Guid t, CancellationToken ct)
            { ListCalls++; return Task.FromResult((IReadOnlyList<ContentEngagementJourney>)Items.Where(j => j.TenantId == t).ToList()); }
        public Task<IReadOnlyList<ContentEngagementJourney>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentEngagementJourney>)Items.Where(j => j.TenantId == t && j.JourneyCode == code).ToList());
        public Task InsertAsync(ContentEngagementJourney entity, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ReplaceAsync(ContentEngagementJourney entity, int expectedVersion, CancellationToken ct)
            => Task.FromResult(true);
    }
}
