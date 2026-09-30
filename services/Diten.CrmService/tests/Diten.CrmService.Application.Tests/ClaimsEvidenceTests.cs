using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Infrastructure.Evidence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-CL-BE-5 — claim evidence via MOD-0031: link (ObjectRef built by CRM) / lock / remove ownership, inheritance
/// (a country version sees its bound claim record's links), "≥1 evidence to submit", the copy onto a new version, the
/// read-time supersede / suspend → review-required rule, the expiring flag and "MOD-0031 down never breaks a read".
/// MOD-0031 is faked at the <see cref="IClaimEvidenceClient"/> seam; the Gateway client is tested over a stub handler.
/// </summary>
public sealed class ClaimsEvidenceTests
{
    private static readonly Guid TenantA = Guid.Parse("a5a5a5a5-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("b5b5b5b5-0000-0000-0000-00000000000b");
    private static readonly Guid Product = Guid.Parse("0000bbbb-0000-0000-0000-0000000000b1");

    // ============================================================ fixture

    private sealed class Store
    {
        public List<Claim> Claims { get; } = new();
        public List<ClaimCountryVersion> Versions { get; } = new();
    }

    private sealed class Fx
    {
        public Store Store { get; }
        public Guid TenantId { get; }
        public ClaimRepo Claims { get; }
        public VersionRepo Versions { get; }
        public FakeClaimEvidenceClient Evidence { get; }
        public FakeClaimWorkflowClient Workflow { get; } = new();
        public Audit Audit { get; } = new();
        public Clock Clock { get; } = new();

        public Fx(Guid tenant, Store? shared = null, FakeClaimEvidenceClient? evidence = null)
        {
            TenantId = tenant;
            Store = shared ?? new Store();
            Claims = new ClaimRepo(Store);
            Versions = new VersionRepo(Store);
            Evidence = evidence ?? new FakeClaimEvidenceClient();
        }

        public TenantContext Ctx()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantId);
            return ctx;
        }

        public ClaimEvidenceReviewer Reviewer() => new(Evidence, Claims, Versions, Audit, null, Clock);

        public GetClaimHandler Get() => new(Ctx(), Claims, Versions, null, Reviewer());
        public ListClaimsHandler List() => new(Ctx(), Claims, Versions, null, Reviewer());
        public GetClaimCountryVersionHandler GetVersion() => new(Ctx(), Versions, null, null, Reviewer());

        public GetClaimCoverageHandler Coverage() =>
            new(Ctx(), Claims, Versions, new Catalog("TR", "BY"), null, null, Reviewer());

        public GetClaimEvidenceHandler ClaimEvidence() => new(Ctx(), Claims, Evidence, null, Clock);
        public GetClaimCountryVersionEvidenceHandler VersionEvidence() => new(Ctx(), Versions, Evidence, null, Clock);
        public LinkClaimEvidenceHandler Link() => new(Ctx(), Claims, Evidence, Audit);
        public LinkClaimCountryVersionEvidenceHandler LinkVersion() => new(Ctx(), Versions, Evidence, Audit);
        public RemoveClaimEvidenceHandler Remove() => new(Ctx(), Claims, Versions, Evidence, Audit);

        public SubmitClaimReviewHandler Submit(IClaimEvidenceClient? evidence) =>
            new(Ctx(), new Actor("alice"), Claims, Workflow, null, Audit, evidence);

        public SubmitClaimCountryVersionReviewHandler SubmitVersion(IClaimEvidenceClient? evidence) =>
            new(Ctx(), new Actor("alice"), Claims, Versions, Workflow, new Languages(), null, Audit, evidence);

        public CreateClaimNewVersionHandler NewVersion() => new(Ctx(), new Actor("alice"), Claims, Audit, Evidence);

        public CreateClaimCountryNewVersionHandler NewCountryVersion() =>
            new(Ctx(), new Actor("alice"), Claims, Versions, Audit, Evidence);

        public Claim SeedClaim(string code = "EV-1", string status = ClaimStatuses.Draft, string version = "1.0",
            string kind = ClaimKinds.Core, string? localCountry = null)
        {
            var claim = new Claim
            {
                TenantId = TenantId, ClaimCode = code, ClaimName = "Name " + code, ClaimText = "Reduces symptoms",
                ClaimVersion = version, Status = status, Kind = kind, LocalCountryCode = localCountry, ProductId = Product,
                EffectiveFrom = DateTimeOffset.UtcNow
            };
            if (status is ClaimStatuses.Approved or ClaimStatuses.ReviewRequired)
            {
                claim.ApprovedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
                claim.ApprovedBy = "approver-1";
            }

            Store.Claims.Add(claim);
            return claim;
        }

        public ClaimCountryVersion SeedVersion(Claim claim, string country = "TR", string status = ClaimStatuses.Draft)
        {
            var version = new ClaimCountryVersion
            {
                TenantId = TenantId, ClaimCode = claim.ClaimCode, ClaimId = claim.Id, BoundCoreVersion = claim.ClaimVersion,
                CountryCode = country, CountryVersion = "1.0", Status = status, AdaptationTypeCode = "verbatim",
                ValidFrom = DateTimeOffset.UtcNow.Date,
                Texts = [new ClaimLocalizedText { LanguageCode = "tr", Text = "metin" }]
            };
            if (status == ClaimStatuses.Approved)
            {
                version.ApprovedAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
            }

            Store.Versions.Add(version);
            return version;
        }

        public ClaimEvidenceLink LinkOf(Claim c, Guid? document = null, string status = "active") =>
            Evidence.Seed(ClaimEvidenceRules.For(c), document, status);

        public ClaimEvidenceLink LinkOf(ClaimCountryVersion v, Guid? document = null) =>
            Evidence.Seed(ClaimEvidenceRules.For(v), document);
    }

    private static ClaimEvidenceLinkInput Input(Guid? document = null) => new("controlled", document ?? Guid.NewGuid(),
        Guid.NewGuid(), "smpc-pil", new ClaimEvidenceLocator("4.1", "4", null, "Reduced symptoms by 30%"), []);

    private static string Code<T>(Diten.CrmService.Application.Common.Models.Response<T> r) => r.Errors![0];

    // ============================================================ link / lock / remove

    [Fact]
    public async Task Link_stamps_the_object_ref_from_the_claim_record()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim(version: "3.0");

        var r = await fx.Link().Handle(new LinkClaimEvidenceCommand(claim.Id, Input()), default);

        Assert.Equal(201, r.StatusCode);
        var stamped = Assert.Single(fx.Evidence.LinkedRefs);
        Assert.Equal(new ClaimEvidenceObjectRef("crm", "claim", claim.Id.ToString("D"), "3.0"), stamped);
        Assert.Equal(ClaimEvidenceRules.OriginCore, r.Data!.Origin);
        var audit = Assert.Single(fx.Audit.Events, e => e.Event == ClaimReasonCodes.EvidenceLinked);
        Assert.DoesNotContain("Reduced symptoms", audit.Detail); // ids only, never the quote
    }

    [Fact]
    public async Task Link_on_a_country_version_stamps_its_own_object_ref()
    {
        var fx = new Fx(TenantA);
        var version = fx.SeedVersion(fx.SeedClaim(status: ClaimStatuses.Approved));

        var r = await fx.LinkVersion().Handle(new LinkClaimCountryVersionEvidenceCommand(version.Id, Input()), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Equal(new ClaimEvidenceObjectRef("crm", "claim-country-version", version.Id.ToString("D"), "1.0"),
            Assert.Single(fx.Evidence.LinkedRefs));
        Assert.Equal(ClaimEvidenceRules.OriginLocal, r.Data!.Origin);
    }

    [Fact]
    public void Link_request_body_cannot_carry_an_object_ref()
    {
        Assert.DoesNotContain(typeof(LinkClaimEvidenceRequest).GetProperties(),
            p => p.Name.Contains("Object", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ClaimStatuses.InReview)]
    [InlineData(ClaimStatuses.Approved)]
    [InlineData(ClaimStatuses.ReviewRequired)]
    [InlineData(ClaimStatuses.Inactive)]
    [InlineData(ClaimStatuses.Archived)]
    public async Task Evidence_is_locked_outside_draft(string status)
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim(status: status);
        var version = fx.SeedVersion(fx.SeedClaim("EV-2", ClaimStatuses.Approved), status: status);
        var own = fx.LinkOf(claim);

        Assert.Equal(ClaimErrorCodes.EvidenceLocked,
            Code(await fx.Link().Handle(new LinkClaimEvidenceCommand(claim.Id, Input()), default)));
        Assert.Equal(ClaimErrorCodes.EvidenceLocked,
            Code(await fx.LinkVersion().Handle(new LinkClaimCountryVersionEvidenceCommand(version.Id, Input()), default)));
        var remove = await fx.Remove().Handle(new RemoveClaimEvidenceCommand(own.LinkId, "wrong"), default);
        Assert.Equal(409, remove.StatusCode);
        Assert.Equal(ClaimErrorCodes.EvidenceLocked, Code(remove));
        Assert.Empty(fx.Evidence.LinkedRefs);
        Assert.True(fx.Evidence.Links.Single(l => l.LinkId == own.LinkId).IsActive);
    }

    [Fact]
    public async Task Remove_of_a_link_that_is_not_a_crm_claim_link_is_404()
    {
        var fx = new Fx(TenantA);
        var draft = fx.SeedClaim();
        // Same id as a real draft claim of this tenant, but another object type / module: still not a claim link.
        var foreign = fx.Evidence.Seed(new ClaimEvidenceObjectRef("crm", "knowledge-content", draft.Id.ToString("D"), "1"));
        var otherModule = fx.Evidence.Seed(new ClaimEvidenceObjectRef("pv", "claim", draft.Id.ToString("D"), "1"));
        var unknownRecord = fx.Evidence.Seed(new ClaimEvidenceObjectRef("crm", "claim", Guid.NewGuid().ToString("D"), "1"));

        foreach (var link in new[] { foreign, otherModule, unknownRecord })
        {
            Assert.Equal(404, (await fx.Remove().Handle(new RemoveClaimEvidenceCommand(link.LinkId, "x"), default)).StatusCode);
            Assert.True(fx.Evidence.Links.Single(l => l.LinkId == link.LinkId).IsActive);
        }

        Assert.Equal(404, (await fx.Remove().Handle(new RemoveClaimEvidenceCommand(Guid.NewGuid(), "x"), default)).StatusCode);
    }

    [Fact]
    public async Task Remove_of_another_tenants_claim_link_is_404()
    {
        var shared = new Store();
        var evidence = new FakeClaimEvidenceClient();
        var a = new Fx(TenantA, shared, evidence);
        var claimOfA = a.SeedClaim();
        var link = a.LinkOf(claimOfA);

        var b = new Fx(TenantB, shared, evidence);
        Assert.Equal(404, (await b.Remove().Handle(new RemoveClaimEvidenceCommand(link.LinkId, "x"), default)).StatusCode);
        Assert.True(evidence.Links.Single(l => l.LinkId == link.LinkId).IsActive);
    }

    [Fact]
    public async Task Remove_of_an_own_draft_link_removes_and_audits()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        var link = fx.LinkOf(claim);

        var r = await fx.Remove().Handle(new RemoveClaimEvidenceCommand(link.LinkId, "wrong page"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.False(fx.Evidence.Links.Single(l => l.LinkId == link.LinkId).IsActive);
        var audit = Assert.Single(fx.Audit.Events, e => e.Event == ClaimReasonCodes.EvidenceRemoved);
        Assert.DoesNotContain("wrong page", audit.Detail);
        Assert.Equal("removal_reason_required",
            Code(await fx.Remove().Handle(new RemoveClaimEvidenceCommand(fx.LinkOf(claim).LinkId, " "), default)));
    }

    // ============================================================ inheritance

    [Fact]
    public async Task Country_version_inherits_the_bound_claim_records_active_links_as_core()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim(status: ClaimStatuses.Approved);
        var version = fx.SeedVersion(core);
        var inherited = fx.LinkOf(core);
        fx.LinkOf(core, status: "removed");
        var own = fx.LinkOf(version);
        var otherCore = fx.SeedClaim("EV-OTHER", ClaimStatuses.Approved);
        fx.LinkOf(otherCore);

        var r = (await fx.VersionEvidence().Handle(new GetClaimCountryVersionEvidenceQuery(version.Id), default)).Data!;

        Assert.Equal(2, r.EffectiveCount);
        Assert.Equal([(inherited.LinkId, "core"), (own.LinkId, "local")],
            r.Items.Select(i => (i.LinkId, i.Origin)).ToList());
        Assert.False(r.Locked);

        var coreView = (await fx.ClaimEvidence().Handle(new GetClaimEvidenceQuery(core.Id), default)).Data!;
        Assert.Equal(inherited.LinkId, Assert.Single(coreView.Items).LinkId); // a claim inherits nothing
        Assert.True(coreView.Locked);
    }

    [Fact]
    public async Task Evidence_list_flags_needs_review_and_expiring()
    {
        var fx = new Fx(TenantA);
        fx.Clock.Now = new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
        var claim = fx.SeedClaim();
        var doc = Guid.NewGuid();
        fx.LinkOf(claim, doc);
        fx.Evidence.Documents[doc] = (true, "effective", fx.Clock.Now.Value.AddDays(30));

        var r = (await fx.ClaimEvidence().Handle(new GetClaimEvidenceQuery(claim.Id), default)).Data!;
        Assert.True(r.AnyNeedsReview);
        Assert.True(r.AnyExpiring);
        Assert.True(r.Items[0].IsSuperseded);
    }

    // ============================================================ submit rule

    [Fact]
    public async Task Submit_without_evidence_is_evidence_required_and_never_starts_a_workflow()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        fx.LinkOf(claim, status: "removed");

        var r = await fx.Submit(fx.Evidence).Handle(new SubmitClaimReviewCommand(claim.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(ClaimErrorCodes.EvidenceRequired, Code(r));
        Assert.Empty(fx.Workflow.Starts);
        Assert.Equal(ClaimStatuses.Draft, claim.Status);

        fx.LinkOf(claim);
        Assert.Equal(201, (await fx.Submit(fx.Evidence).Handle(new SubmitClaimReviewCommand(claim.Id), default)).StatusCode);
    }

    [Fact]
    public async Task Submit_keeps_the_be4_rule_order_evidence_comes_after_them()
    {
        var fx = new Fx(TenantA);
        var noProduct = fx.SeedClaim();
        noProduct.ProductId = null;
        Assert.Equal(ClaimErrorCodes.ProductRequired,
            Code(await fx.Submit(fx.Evidence).Handle(new SubmitClaimReviewCommand(noProduct.Id), default)));

        var approved = fx.SeedClaim("EV-3", ClaimStatuses.Approved);
        Assert.Equal(ClaimErrorCodes.InvalidStatus,
            Code(await fx.Submit(fx.Evidence).Handle(new SubmitClaimReviewCommand(approved.Id), default)));
        Assert.Equal(0, fx.Evidence.QueryCalls); // the evidence read happens only after the BE-4 rules passed
    }

    [Fact]
    public async Task Submit_is_fail_closed_when_evidence_cannot_be_read()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        fx.LinkOf(claim);
        fx.Evidence.Unavailable = true;

        Assert.Equal(ClaimErrorCodes.EvidenceUnavailable,
            Code(await fx.Submit(fx.Evidence).Handle(new SubmitClaimReviewCommand(claim.Id), default)));
        Assert.Equal(ClaimErrorCodes.EvidenceUnavailable,
            Code(await fx.Submit(null).Handle(new SubmitClaimReviewCommand(claim.Id), default)));
        Assert.Empty(fx.Workflow.Starts);
    }

    [Fact]
    public async Task Country_version_submit_counts_inherited_core_evidence()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim(status: ClaimStatuses.Approved);
        var version = fx.SeedVersion(core);

        Assert.Equal(ClaimErrorCodes.EvidenceRequired,
            Code(await fx.SubmitVersion(fx.Evidence).Handle(new SubmitClaimCountryVersionReviewCommand(version.Id), default)));

        fx.LinkOf(core); // only inherited evidence
        var r = await fx.SubmitVersion(fx.Evidence).Handle(new SubmitClaimCountryVersionReviewCommand(version.Id), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Single(fx.Workflow.Starts);
    }

    // ============================================================ copy on a new version

    [Fact]
    public async Task Core_new_version_copies_the_own_active_links_under_the_new_object_ref()
    {
        var fx = new Fx(TenantA);
        var source = fx.SeedClaim(status: ClaimStatuses.Approved);
        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();
        fx.LinkOf(source, d1);
        fx.LinkOf(source, d2);
        fx.LinkOf(source, status: "removed");

        var r = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(source.Id), default);

        Assert.Equal(201, r.StatusCode);
        var target = fx.Store.Claims.Single(c => c.Id == r.Data);
        var copies = fx.Evidence.Links.Where(l => l.ObjectRef.ObjectId == target.Id.ToString("D")).ToList();
        Assert.Equal(2, copies.Count);
        Assert.All(copies, l => Assert.Equal(new ClaimEvidenceObjectRef("crm", "claim", target.Id.ToString("D"), "2.0"), l.ObjectRef));
        Assert.Equal(new[] { d1, d2 }.OrderBy(x => x), copies.Select(l => l.DocumentId).OrderBy(x => x));
        var audit = Assert.Single(fx.Audit.Events, e => e.Event == ClaimReasonCodes.EvidenceCopied);
        Assert.Contains("copied=2|failed=0", audit.Detail);
    }

    [Fact]
    public async Task A_failed_copy_still_creates_the_version_and_submit_then_asks_for_evidence()
    {
        var fx = new Fx(TenantA);
        var source = fx.SeedClaim(status: ClaimStatuses.Approved);
        fx.LinkOf(source);
        fx.Evidence.RejectLinks = (403, "document_not_readable");

        var r = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(source.Id), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Contains("copied=0|failed=1",
            Assert.Single(fx.Audit.Events, e => e.Event == ClaimReasonCodes.EvidenceCopied).Detail);
        fx.Evidence.RejectLinks = null;
        Assert.Equal(ClaimErrorCodes.EvidenceRequired,
            Code(await fx.Submit(fx.Evidence).Handle(new SubmitClaimReviewCommand(r.Data), default)));
    }

    [Fact]
    public async Task Country_new_version_copies_only_its_own_links_not_the_inherited_ones()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim(status: ClaimStatuses.Approved);
        var source = fx.SeedVersion(core, status: ClaimStatuses.Approved);
        fx.LinkOf(core);
        var own = fx.LinkOf(source);

        var r = await fx.NewCountryVersion().Handle(new CreateClaimCountryNewVersionCommand(source.Id), default);

        Assert.Equal(201, r.StatusCode);
        var copy = Assert.Single(fx.Evidence.Links, l => l.ObjectRef.ObjectId == r.Data.ToString("D"));
        Assert.Equal("claim-country-version", copy.ObjectRef.ObjectType);
        Assert.Equal("1.1", copy.ObjectRef.ObjectVersion);
        Assert.Equal(own.DocumentId, copy.DocumentId);
        Assert.Equal(own.DocumentVersionId, copy.DocumentVersionId); // the pinned version travels
    }

    [Fact]
    public async Task A_review_required_core_can_open_a_new_version_and_its_approval_retires_it()
    {
        var fx = new Fx(TenantA);
        var flagged = fx.SeedClaim(status: ClaimStatuses.ReviewRequired);

        var r = await fx.NewVersion().Handle(new CreateClaimNewVersionCommand(flagged.Id), default);
        Assert.Equal(201, r.StatusCode);

        var next = fx.Store.Claims.Single(c => c.Id == r.Data);
        var instance = Guid.NewGuid();
        next.ReviewRounds.Add(new ClaimReviewRound { WorkflowInstanceId = instance, RoundNo = 1, SubmittedAt = DateTimeOffset.UtcNow });
        next.Status = ClaimStatuses.InReview;
        await new ClaimReviewOutcomeApplier(fx.Claims, fx.Versions, fx.Audit).ApplyAsync(TenantA,
            ClaimReviewRules.ClaimObjectType, next.Id, instance, ClaimReviewOutcomes.Approved, "approver", "OK",
            DateTimeOffset.UtcNow, default);

        Assert.Equal(ClaimStatuses.Approved, next.Status);
        Assert.Equal(ClaimStatuses.Inactive, flagged.Status);
    }

    // ============================================================ read-time: changed document → review-required

    [Theory]
    [InlineData(true, "effective")]
    [InlineData(false, "suspended")]
    [InlineData(false, "retired")]
    [InlineData(false, "withdrawn")]
    public async Task Approved_claim_with_a_changed_document_turns_review_required_on_read(bool superseded, string state)
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim(status: ClaimStatuses.Approved);
        var approvedAt = claim.ApprovedAt;
        var doc = Guid.NewGuid();
        var link = fx.LinkOf(claim, doc);
        fx.Evidence.Documents[doc] = (superseded, state, null);

        var dto = (await fx.Get().Handle(new GetClaimQuery(claim.Id), default)).Data!;

        Assert.Equal(ClaimStatuses.ReviewRequired, dto.Status);
        Assert.Equal(approvedAt, dto.ApprovedAt);
        var audit = Assert.Single(fx.Audit.Events, e => e.Event == ClaimReasonCodes.ReviewRequiredEvidenceChanged);
        Assert.Contains($"link={link.LinkId:D}", audit.Detail);
        Assert.Contains($"document={doc:D}", audit.Detail);
    }

    [Fact]
    public async Task Read_time_flag_is_idempotent_and_does_not_run_the_core_propagation()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim(status: ClaimStatuses.Approved);
        var version = fx.SeedVersion(core, status: ClaimStatuses.Approved);
        var coreDoc = Guid.NewGuid();
        fx.LinkOf(core, coreDoc);
        fx.Evidence.Documents[coreDoc] = (true, "effective", null);

        await fx.Get().Handle(new GetClaimQuery(core.Id), default);
        await fx.Get().Handle(new GetClaimQuery(core.Id), default);
        await fx.List().Handle(new ListClaimsQuery(null, null, null, true), default);

        Assert.Equal(ClaimStatuses.ReviewRequired, core.Status);
        Assert.Equal(2, fx.Audit.Events.Count(e => e.Event == ClaimReasonCodes.ReviewRequiredEvidenceChanged)); // core + version, once each
        Assert.DoesNotContain(fx.Audit.Events, e => e.Event == ClaimReasonCodes.CountryVersionsReviewRequired);
        Assert.Equal(ClaimStatuses.ReviewRequired, version.Status); // via its OWN effective (inherited) evidence
    }

    [Fact]
    public async Task A_country_versions_own_suspended_document_flags_only_that_version()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim(status: ClaimStatuses.Approved);
        var version = fx.SeedVersion(core, status: ClaimStatuses.Approved);
        fx.LinkOf(core);
        var doc = Guid.NewGuid();
        fx.LinkOf(version, doc);
        fx.Evidence.Documents[doc] = (false, "suspended", null);

        var dto = (await fx.GetVersion().Handle(new GetClaimCountryVersionQuery(version.Id), default)).Data!;

        Assert.Equal(ClaimStatuses.ReviewRequired, dto.Status);
        Assert.NotNull(dto.ApprovedAt);
        Assert.Equal(ClaimStatuses.Approved, core.Status);
    }

    [Fact]
    public async Task Draft_and_unchanged_records_are_not_touched()
    {
        var fx = new Fx(TenantA);
        var draft = fx.SeedClaim();
        var doc = Guid.NewGuid();
        fx.LinkOf(draft, doc);
        fx.Evidence.Documents[doc] = (true, "effective", null);
        var fine = fx.SeedClaim("EV-OK", ClaimStatuses.Approved);
        fx.LinkOf(fine);
        var unknown = fx.SeedClaim("EV-UNK", ClaimStatuses.Approved);
        var unknownDoc = Guid.NewGuid();
        fx.LinkOf(unknown, unknownDoc);
        fx.Evidence.Documents[unknownDoc] = (false, "unknown", null);

        await fx.List().Handle(new ListClaimsQuery(null, null, null, true), default);

        Assert.Equal(ClaimStatuses.Draft, draft.Status);
        Assert.Equal(ClaimStatuses.Approved, fine.Status);
        Assert.Equal(ClaimStatuses.Approved, unknown.Status); // unknown is never a guess
        Assert.Empty(fx.Audit.Events);
    }

    [Fact]
    public async Task Expiring_evidence_is_flagged_on_detail_list_and_coverage_without_changing_status()
    {
        var fx = new Fx(TenantA);
        fx.Clock.Now = new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
        var soon = fx.SeedClaim(status: ClaimStatuses.Approved);
        var soonDoc = Guid.NewGuid();
        fx.LinkOf(soon, soonDoc);
        fx.Evidence.Documents[soonDoc] = (false, "effective", fx.Clock.Now.Value.AddDays(59));
        var version = fx.SeedVersion(soon, status: ClaimStatuses.Approved);
        var later = fx.SeedClaim("EV-LATER", ClaimStatuses.Approved);
        var laterDoc = Guid.NewGuid();
        fx.LinkOf(later, laterDoc);
        fx.Evidence.Documents[laterDoc] = (false, "effective", fx.Clock.Now.Value.AddDays(61));

        Assert.True((await fx.Get().Handle(new GetClaimQuery(soon.Id), default)).Data!.EvidenceExpiring);
        Assert.False((await fx.Get().Handle(new GetClaimQuery(later.Id), default)).Data!.EvidenceExpiring);
        var list = (await fx.List().Handle(new ListClaimsQuery(null, null, null, true), default)).Data!.Items;
        Assert.True(list.Single(c => c.ClaimId == soon.Id).EvidenceExpiring);
        Assert.False(list.Single(c => c.ClaimId == later.Id).EvidenceExpiring);
        Assert.True((await fx.GetVersion().Handle(new GetClaimCountryVersionQuery(version.Id), default)).Data!.EvidenceExpiring);

        var coverage = (await fx.Coverage().Handle(new GetClaimCoverageQuery(null, null, null, null), default)).Data!;
        var row = coverage.Rows.Single(r => r.ClaimId == soon.Id);
        Assert.True(row.EvidenceExpiring);
        Assert.True(row.Cells.Single(c => c.CountryCode == "TR").EvidenceExpiring);
        Assert.False(coverage.Rows.Single(r => r.ClaimId == later.Id).EvidenceExpiring);

        Assert.Equal(ClaimStatuses.Approved, soon.Status);
        Assert.Empty(fx.Audit.Events);
    }

    [Fact]
    public async Task Reads_do_not_break_when_the_evidence_service_is_down()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim(status: ClaimStatuses.Approved);
        var version = fx.SeedVersion(claim, status: ClaimStatuses.Approved);
        var doc = Guid.NewGuid();
        fx.LinkOf(claim, doc);
        fx.Evidence.Documents[doc] = (true, "effective", DateTimeOffset.UtcNow);
        fx.Evidence.Unavailable = true;

        var get = await fx.Get().Handle(new GetClaimQuery(claim.Id), default);
        var list = await fx.List().Handle(new ListClaimsQuery(null, null, null, true), default);
        var cv = await fx.GetVersion().Handle(new GetClaimCountryVersionQuery(version.Id), default);
        var coverage = await fx.Coverage().Handle(new GetClaimCoverageQuery(null, null, null, null), default);

        Assert.Equal(200, get.StatusCode);
        Assert.Equal(200, list.StatusCode);
        Assert.Equal(200, cv.StatusCode);
        Assert.Equal(200, coverage.StatusCode);
        Assert.Equal(ClaimStatuses.Approved, get.Data!.Status);
        Assert.False(get.Data.EvidenceExpiring);
        Assert.Equal(ClaimEvidenceErrors(), Code(await fx.ClaimEvidence().Handle(new GetClaimEvidenceQuery(claim.Id), default)));
    }

    private static string ClaimEvidenceErrors() => ClaimErrorCodes.EvidenceUnavailable;

    [Fact]
    public async Task One_bulk_evidence_read_per_page()
    {
        var fx = new Fx(TenantA);
        for (var i = 0; i < 5; i++)
        {
            var c = fx.SeedClaim($"EV-P{i}", ClaimStatuses.Approved);
            fx.SeedVersion(c, status: ClaimStatuses.Approved);
            fx.LinkOf(c);
        }

        await fx.List().Handle(new ListClaimsQuery(null, null, null, true), default);
        Assert.Equal(1, fx.Evidence.QueryCalls);
    }

    // ============================================================ Gateway client

    [Fact]
    public async Task Gateway_client_queries_in_bulk_forwards_the_caller_and_parses_the_computed_state()
    {
        var linkId = Guid.NewGuid();
        var currentId = Guid.NewGuid();
        var body = "{\"data\":[{\"objectRef\":{\"module\":\"crm\",\"objectType\":\"claim\",\"objectId\":\"c1\"},\"links\":[{"
                   + $"\"linkId\":\"{linkId}\",\"objectRef\":{{\"module\":\"crm\",\"objectType\":\"claim\",\"objectId\":\"c1\",\"objectVersion\":\"1.0\"}},"
                   + $"\"documentKind\":\"controlled\",\"documentId\":\"{Guid.NewGuid()}\",\"documentVersionId\":\"{Guid.NewGuid()}\","
                   + "\"documentVersionLabel\":\"v1\",\"documentTitle\":\"SmPC\",\"evidenceTypeCode\":\"smpc-pil\","
                   + "\"locator\":{\"section\":\"4.1\",\"page\":\"4\",\"table\":null,\"quote\":\"q\"},\"supportedSpans\":[],"
                   + "\"status\":\"active\",\"linkedBy\":\"u\",\"linkedAt\":\"2026-09-01T00:00:00+00:00\","
                   + $"\"currentVersionId\":\"{currentId}\",\"currentVersionLabel\":\"v2\",\"isSuperseded\":true,"
                   + "\"documentState\":\"effective\",\"reviewDueAt\":\"2026-10-01T00:00:00+00:00\"}]}]}";
        var handler = new StubHandler(HttpStatusCode.OK, body);
        var client = GatewayClient(handler);

        var links = await client.QueryAsync(
            Enumerable.Range(0, 150).Select(i => new ClaimEvidenceObjectRef("crm", "claim", $"c{i}", null)).ToList(), false,
            default);

        Assert.Equal(2, handler.Requests.Count); // 150 objects → two ≤100 calls
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal("/api/v1/evidence/links/query", r.Path);
            Assert.Equal("Bearer user-token", r.Authorization);
            Assert.Equal(TenantA.ToString(), r.Tenant);
        });
        Assert.Equal(100, JsonDocument.Parse(handler.Requests[0].Body!).RootElement.GetProperty("objects").GetArrayLength());
        var link = Assert.Single(links!); // the same link answered twice is kept once
        Assert.True(link.IsSuperseded);
        Assert.Equal(currentId, link.CurrentVersionId);
        Assert.Equal("v2", link.CurrentVersionLabel);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), link.ReviewDueAt);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, ClaimEvidenceCallOutcome.Unavailable)]
    [InlineData(HttpStatusCode.Forbidden, ClaimEvidenceCallOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ClaimEvidenceCallOutcome.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ClaimEvidenceCallOutcome.Rejected)]
    public async Task Gateway_client_maps_link_refusals(HttpStatusCode status, ClaimEvidenceCallOutcome expected)
    {
        var client = GatewayClient(new StubHandler(status,
            "{\"data\":null,\"errors\":[\"An identical active evidence link already exists.\"],\"reason_code\":\"duplicate_link\"}"));

        var r = await client.LinkAsync(new ClaimEvidenceObjectRef("crm", "claim", "c1", "1.0"), Input(), default);

        Assert.Equal(expected, r.Outcome);
        Assert.Equal("duplicate_link", r.ReasonCode);
    }

    [Fact]
    public async Task Gateway_client_down_is_unavailable_and_query_null()
    {
        var client = GatewayClient(new StubHandler(throws: true));
        Assert.Null(await client.QueryAsync([new ClaimEvidenceObjectRef("crm", "claim", "c1", null)], false, default));
        Assert.Equal(ClaimEvidenceCallOutcome.Unavailable, (await client.GetAsync(Guid.NewGuid(), default)).Outcome);
    }

    [Fact]
    public async Task Gateway_client_sends_the_crm_object_ref_on_link()
    {
        var handler = new StubHandler(HttpStatusCode.Created, "{\"data\":null}");
        var client = GatewayClient(handler);
        await client.LinkAsync(new ClaimEvidenceObjectRef("crm", "claim-country-version", "v1", "1.1"), Input(), default);

        var sent = JsonDocument.Parse(handler.Requests.Single().Body!).RootElement.GetProperty("objectRef");
        Assert.Equal("claim-country-version", sent.GetProperty("objectType").GetString());
        Assert.Equal("1.1", sent.GetProperty("objectVersion").GetString());
    }

    // ============================================================ WP-CL-FIX-1 — document code / state

    [Fact]
    public async Task Evidence_items_carry_the_document_code()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        var link = fx.LinkOf(claim);
        var index = fx.Evidence.Links.FindIndex(l => l.LinkId == link.LinkId);
        fx.Evidence.Links[index] = link with { DocumentCode = "GMG-ALM-SMPC-0001" };

        var item = Assert.Single((await fx.ClaimEvidence().Handle(new GetClaimEvidenceQuery(claim.Id), default)).Data!.Items);

        Assert.Equal("GMG-ALM-SMPC-0001", item.DocumentCode);
    }

    [Fact]
    public async Task Gateway_client_reads_document_code_and_option_state()
    {
        var linkBody = "{\"data\":{\"linkId\":\"" + Guid.NewGuid() + "\",\"objectRef\":{\"module\":\"crm\",\"objectType\":\"claim\",\"objectId\":\"c1\"},"
                       + "\"documentKind\":\"controlled\",\"documentId\":\"" + Guid.NewGuid() + "\",\"documentTitle\":\"SmPC\",\"evidenceTypeCode\":\"smpc-pil\","
                       + "\"locator\":{\"quote\":\"q\"},\"status\":\"active\",\"linkedAt\":\"2026-09-29T00:00:00+00:00\",\"documentCode\":\"GMG-ALM-SMPC-0001\"}}";
        var link = await GatewayClient(new StubHandler(HttpStatusCode.OK, linkBody)).GetAsync(Guid.NewGuid(), default);
        Assert.Equal("GMG-ALM-SMPC-0001", link.Data!.DocumentCode);

        var optionsBody = "{\"data\":[{\"kind\":\"controlled\",\"documentId\":\"" + Guid.NewGuid() + "\",\"title\":\"SmPC\",\"code\":\"GMG-ALM-SMPC-0001\","
                          + "\"status\":\"Active\",\"documentState\":\"effective\"}]}";
        var options = await GatewayClient(new StubHandler(HttpStatusCode.OK, optionsBody)).GetDocumentOptionsAsync(null, null, default);
        var option = Assert.Single(options.Data!);
        Assert.Equal("effective", option.DocumentState);
        Assert.Equal("GMG-ALM-SMPC-0001", option.Code);
    }

    // ============================================================ surface

    [Theory]
    [InlineData(nameof(ClaimsController.Evidence), "GET", "claims/{claimId:guid}/evidence", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.CountryVersionEvidence), "GET", "claims/country-versions/{countryVersionId:guid}/evidence", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.LinkEvidence), "POST", "claims/{claimId:guid}/evidence", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.LinkCountryVersionEvidence), "POST", "claims/country-versions/{countryVersionId:guid}/evidence", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.RemoveEvidence), "POST", "claims/evidence/{linkId:guid}/remove", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.EvidenceDocumentOptions), "GET", "claims/evidence/document-options", ClaimPermissions.Read)]
    public void Evidence_endpoints_use_existing_claim_permissions(string action, string verb, string route, string permission)
    {
        var method = typeof(ClaimsController).GetMethod(action)!;
        var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Contains(verb, http.HttpMethods);
        Assert.Equal("api/crm/content-composition/" + route, http.Template);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    [Fact]
    public async Task Document_options_pass_through()
    {
        var fx = new Fx(TenantA);
        var r = await new GetClaimEvidenceDocumentOptionsHandler(fx.Ctx(), fx.Evidence)
            .Handle(new GetClaimEvidenceDocumentOptionsQuery("smpc", "controlled"), default);
        Assert.Equal("SmPC smpc", Assert.Single(r.Data!).Title);
        fx.Evidence.Unavailable = true;
        Assert.Equal(503, (await new GetClaimEvidenceDocumentOptionsHandler(fx.Ctx(), fx.Evidence)
            .Handle(new GetClaimEvidenceDocumentOptionsQuery(null, null), default)).StatusCode);
    }

    // ============================================================ fakes

    private static GatewayClaimEvidenceClient GatewayClient(StubHandler handler)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer user-token";
        context.Request.Headers["X-Tenant-Id"] = TenantA.ToString();
        return new GatewayClaimEvidenceClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test") },
            new ConfigurationBuilder().Build(),
            new HttpContextAccessor { HttpContext = context },
            NullLogger<GatewayClaimEvidenceClient>.Instance);
    }

    private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "", bool throws = false)
        : HttpMessageHandler
    {
        public List<(string Path, string? Authorization, string? Tenant, string? Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("Authorization", out var a) ? a.Single() : null,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            if (throws)
            {
                throw new HttpRequestException("connection refused");
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Actor(string name) : IActorContext
    {
        public string? ActorName => name;
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset? Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now ?? DateTimeOffset.UtcNow;
    }

    private sealed class Languages : IReferenceMetadataReader
    {
        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string> { ["Languages"] = "tr" });
    }

    private sealed class Catalog(params string[] codes) : IReferenceDataCatalogReader
    {
        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
            => Task.FromResult(new ReferenceSetSnapshot(setCode, true,
                codes.Select(c => new ReferenceValueSnapshot(c, c, null, true, false, null)).ToList()));
    }

    private sealed class Audit : IContentCompositionAuditPublisher
    {
        public List<(string Event, string EntityType, Guid EntityId, string? Detail)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityType, entityId, detail));
            return Task.CompletedTask;
        }
    }

    private sealed class ClaimRepo(Store s) : IClaimRepository
    {
        private IEnumerable<Claim> Of(Guid t) => s.Claims.Where(x => x.TenantId == t && !x.IsDeleted);
        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of(t).FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult<IReadOnlyList<Claim>>(Of(t).ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Claim>>(Of(t).Where(x => x.ClaimCode == code).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) =>
            Task.FromResult(Of(t).FirstOrDefault(x => x.ClaimCode == code && !x.IsArchived()));
        public Task InsertAsync(Claim e, CancellationToken ct) { s.Claims.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Claim e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class VersionRepo(Store s) : IClaimCountryVersionRepository
    {
        private IEnumerable<ClaimCountryVersion> Of(Guid t) => s.Versions.Where(x => x.TenantId == t && !x.IsDeleted);
        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of(t).FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Of(t).Where(x => x.ClaimCode == code).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Of(t).Where(x => x.ClaimId == claimId).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Of(t).ToList());
        public Task InsertAsync(ClaimCountryVersion e, CancellationToken ct) { s.Versions.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;
    }
}
