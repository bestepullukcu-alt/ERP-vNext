using System.Reflection;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-17 (CAND-CAP-0011, SCMM-17) — release authorization + managed withdrawal. Pins down: release requires a rendered
/// revision (else 409); the reviewer cannot release (SoD → 403); release pins the artifact's contentId + checksum
/// (manifest-bound) and is idempotent; a withdrawn revision cannot be re-released (409); withdrawal requires a released
/// revision (409) and a reason (400), is idempotent, and — critically — never deletes the stored bytes (no store path).
/// </summary>
public sealed class ContentSetRevisionReleaseTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Actor : IActorContext
    {
        public Actor(string? name) => ActorName = name;
        public string? ActorName { get; }
    }

    private sealed class FakeRevisionRepo : IContentSetRevisionRepository
    {
        public List<ContentSetRevision> Items { get; } = new();
        public int UpdateCalls { get; private set; }

        public Task<ContentSetRevision?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<ContentSetRevision>> ListByContentSetAsync(Guid t, Guid setId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ContentSetRevision>)Items
                .Where(x => x.TenantId == t && x.ContentSetId == setId && !x.IsDeleted).ToList());
        public Task InsertAsync(ContentSetRevision e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ContentSetRevision e, CancellationToken ct) { UpdateCalls++; return Task.CompletedTask; }
    }

    private sealed class NullAudit : IContentCompositionAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, string et, Guid id, int v, string? d, CancellationToken ct) => Task.CompletedTask;
    }

    private static ContentSetRevision Seed(FakeRevisionRepo repo, Guid tenant, bool rendered = true, string reviewer = "reviewer")
    {
        var revision = new ContentSetRevision
        {
            TenantId = tenant,
            ContentSetId = Guid.NewGuid(),
            ContentSetVersion = 1,
            RevisionNumber = 1,
            RevisionCode = "SET-1-R1",
            ReviewStatus = ContentSetReviewStatuses.Approved,
            SubmittedBy = "author",
            SubmittedAt = Jan1,
            CreatedAt = Jan1,
            Decision = new ContentSetReviewDecision { ReviewerId = reviewer, Decision = "approve", DecidedAt = Jan1 },
            RenderedArtifact = rendered
                ? new ContentSetRenderedArtifact
                {
                    ContentId = Guid.NewGuid(), Checksum = "abc123", MediaType = "application/pdf",
                    ByteSize = 1024, FileName = "SET-1-R1.pdf", RenderedAtUtc = Jan1, RenderedBy = "renderer"
                }
                : null
        };
        repo.Items.Add(revision);
        return revision;
    }

    private static ReleaseContentSetRevisionHandler Release(FakeRevisionRepo repo, Guid tenant, string? actor)
        => new(Tenant(tenant), new Actor(actor), repo, new NullAudit());

    private static WithdrawContentSetRevisionHandler Withdraw(FakeRevisionRepo repo, Guid tenant, string? actor)
        => new(Tenant(tenant), new Actor(actor), repo, new NullAudit());

    // ── release ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Release_of_a_not_rendered_revision_is_409()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA, rendered: false);

        var r = await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Null(revision.ReleaseState);
    }

    [Fact]
    public async Task Release_by_the_reviewer_is_403_separation_of_duties()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA, reviewer: "reviewer");

        var r = await Release(repo, TenantA, "reviewer").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(403, r.StatusCode);
        Assert.Null(revision.ReleaseState);
    }

    [Fact]
    public async Task Release_success_pins_the_artifact_and_marks_released()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);
        var artifactId = revision.RenderedArtifact!.ContentId;

        var r = await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ContentSetReleaseStatuses.Released, r.Data!.ReleaseStatus);
        Assert.Equal(artifactId, r.Data.ReleasedArtifactContentId);  // manifest-bound pin
        Assert.Equal("abc123", r.Data.ReleasedArtifactChecksum);
        Assert.Equal("releaser", r.Data.ReleasedBy);
        Assert.True(revision.IsReleased());
    }

    [Fact]
    public async Task Release_is_idempotent_returns_existing_state()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);

        var first = await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);
        var second = await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(first.Data!.ReleasedAtUtc, second.Data!.ReleasedAtUtc);  // not re-released
    }

    [Fact]
    public async Task Release_with_no_recorded_reviewer_is_409()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);
        revision.Decision = null;  // unexpected for approved, but SoD cannot be verified → controlled 409

        var r = await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Release_of_a_missing_or_cross_tenant_revision_is_404()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);

        var cross = await Release(repo, TenantB, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(404, cross.StatusCode);
    }

    // ── withdrawal ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_of_a_not_released_revision_is_409()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);

        var r = await Withdraw(repo, TenantA, "admin")
            .Handle(new WithdrawContentSetRevisionCommand(revision.Id, "compliance"), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Null(revision.ReleaseState);
    }

    [Fact]
    public async Task Withdraw_with_a_blank_reason_is_400()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);
        await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        var r = await Withdraw(repo, TenantA, "admin")
            .Handle(new WithdrawContentSetRevisionCommand(revision.Id, "   "), default);

        Assert.Equal(400, r.StatusCode);
        Assert.True(revision.IsReleased());  // unchanged
    }

    [Fact]
    public async Task Withdraw_success_is_terminal_and_records_who_when_why()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);
        await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);
        var pinnedId = revision.ReleaseState!.ReleasedArtifactContentId;

        var r = await Withdraw(repo, TenantA, "admin")
            .Handle(new WithdrawContentSetRevisionCommand(revision.Id, "regulatory recall"), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ContentSetReleaseStatuses.Withdrawn, r.Data!.ReleaseStatus);
        Assert.Equal("regulatory recall", r.Data.WithdrawalReason);
        Assert.Equal("admin", r.Data.WithdrawnBy);
        Assert.Equal(pinnedId, r.Data.ReleasedArtifactContentId);  // pinned identity preserved
        Assert.True(revision.IsWithdrawn());

        // Terminal: a withdrawn revision cannot be re-released.
        var reRelease = await Release(repo, TenantA, "releaser")
            .Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);
        Assert.Equal(409, reRelease.StatusCode);
    }

    [Fact]
    public async Task Withdraw_is_idempotent_returns_existing_state()
    {
        var repo = new FakeRevisionRepo();
        var revision = Seed(repo, TenantA);
        await Release(repo, TenantA, "releaser").Handle(new ReleaseContentSetRevisionCommand(revision.Id), default);

        var first = await Withdraw(repo, TenantA, "admin")
            .Handle(new WithdrawContentSetRevisionCommand(revision.Id, "recall"), default);
        var second = await Withdraw(repo, TenantA, "admin")
            .Handle(new WithdrawContentSetRevisionCommand(revision.Id, "another reason"), default);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal("recall", second.Data!.WithdrawalReason);  // idempotent — first reason kept, not overwritten
    }

    // ── AD-6: managed withdrawal never deletes the stored bytes ─────────────────────────────────────────────────────
    [Fact]
    public void Withdraw_handler_has_no_artifact_store_dependency_and_the_store_exposes_no_delete_path()
    {
        // Structural guarantee that withdrawal cannot delete/compensate/purge: the handler never depends on the store...
        var ctorParams = typeof(WithdrawContentSetRevisionHandler)
            .GetConstructors().Single().GetParameters().Select(p => p.ParameterType);
        Assert.DoesNotContain(ctorParams, t => t == typeof(IContentArtifactStore));

        // ...and the artifact store seam itself exposes no removal operation at all.
        var storeMethods = typeof(IContentArtifactStore).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name).ToArray();
        Assert.DoesNotContain(storeMethods, n =>
            n.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Compensate", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Purge", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }
}
