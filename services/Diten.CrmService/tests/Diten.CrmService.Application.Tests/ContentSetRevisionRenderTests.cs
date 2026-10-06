using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.ContentComposition.Rendering;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — render an approved ContentSetRevision to a PDF, store it through MOD-0262-FU01, and
/// bind the pointer. Pins down: only an approved revision renders (else 409); idempotent (a re-render returns the same
/// contentId with no second upload); a successful render binds ContentId + Checksum; a store failure propagates and binds
/// nothing (fail-closed, no swallow); the artifact read resolves the content id from the revision (404 when unrendered).
/// </summary>
public sealed class ContentSetRevisionRenderTests
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

    private sealed class FakeRenderer : IContentSetRevisionRenderer
    {
        public int Calls { get; private set; }
        public RenderedContent Render(ContentSetRevision revision)
        {
            Calls++;
            return new RenderedContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 }, "REV.pdf", "application/pdf");
        }
    }

    private sealed class FakeStore : IContentArtifactStore
    {
        public int StoreCalls { get; private set; }
        public Guid ContentId { get; } = Guid.NewGuid();
        public bool ThrowOnStore { get; init; }

        public Task<ContentArtifactStoreResult> StoreAsync(ContentArtifactStoreRequest request, CancellationToken ct)
        {
            StoreCalls++;
            if (ThrowOnStore)
            {
                throw new ContentArtifactStoreException(503, "The document repository is unavailable.");
            }
            return Task.FromResult(new ContentArtifactStoreResult(ContentId, "abc123", request.Content.Length, "application/pdf"));
        }

        public Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken ct)
            => Task.FromResult<ContentArtifactReadResult?>(
                new ContentArtifactReadResult(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }), "application/pdf", "REV.pdf", 4));
    }

    private sealed class NullAudit : IContentCompositionAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, string et, Guid id, int v, string? d, CancellationToken ct) => Task.CompletedTask;
    }

    private static ContentSetRevision SeedRevision(FakeRevisionRepo repo, Guid tenant, string status)
    {
        var revision = new ContentSetRevision
        {
            TenantId = tenant,
            ContentSetId = Guid.NewGuid(),
            ContentSetVersion = 1,
            RevisionNumber = 1,
            RevisionCode = "SET-1-R1",
            ReviewStatus = status,
            SubmittedBy = "author",
            SubmittedAt = Jan1,
            CreatedAt = Jan1,
            SelectedComponents =
            {
                new ContentSetComponent { KnowledgeContentId = Guid.NewGuid(), ContentVersion = "1.0", LanguageCode = "en" }
            }
        };
        repo.Items.Add(revision);
        return revision;
    }

    private static RenderContentSetRevisionHandler Handler(
        FakeRevisionRepo repo, FakeRenderer renderer, FakeStore store, Guid tenant, string? actor = "renderer")
        => new(Tenant(tenant), new Actor(actor), repo, renderer, store, new NullAudit());

    [Fact]
    public async Task Render_binds_content_id_and_checksum_and_returns_201()
    {
        var repo = new FakeRevisionRepo();
        var renderer = new FakeRenderer();
        var store = new FakeStore();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);

        var r = await Handler(repo, renderer, store, TenantA)
            .Handle(new RenderContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Equal(store.ContentId, r.Data!.ContentId);
        Assert.Equal("abc123", r.Data.Checksum);
        Assert.Equal("application/pdf", r.Data.MediaType);
        Assert.NotNull(revision.RenderedArtifact);
        Assert.Equal(store.ContentId, revision.RenderedArtifact!.ContentId);
        Assert.Equal("renderer", revision.RenderedArtifact.RenderedBy);
        Assert.Equal(1, store.StoreCalls);
    }

    [Fact]
    public async Task Render_of_a_non_approved_revision_is_409_and_stores_nothing()
    {
        var repo = new FakeRevisionRepo();
        var renderer = new FakeRenderer();
        var store = new FakeStore();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Submitted);

        var r = await Handler(repo, renderer, store, TenantA)
            .Handle(new RenderContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Null(revision.RenderedArtifact);
        Assert.Equal(0, renderer.Calls);
        Assert.Equal(0, store.StoreCalls);
    }

    [Fact]
    public async Task Render_is_idempotent_returns_existing_pointer_without_a_second_upload()
    {
        var repo = new FakeRevisionRepo();
        var renderer = new FakeRenderer();
        var store = new FakeStore();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);

        var first = await Handler(repo, renderer, store, TenantA)
            .Handle(new RenderContentSetRevisionCommand(revision.Id), default);
        var second = await Handler(repo, renderer, store, TenantA)
            .Handle(new RenderContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);      // idempotent replay
        Assert.Equal(first.Data!.ContentId, second.Data!.ContentId);
        Assert.Equal(1, renderer.Calls);           // rendered once
        Assert.Equal(1, store.StoreCalls);          // uploaded once (no-dup)
    }

    [Fact]
    public async Task Render_propagates_a_store_failure_and_binds_nothing()
    {
        var repo = new FakeRevisionRepo();
        var renderer = new FakeRenderer();
        var store = new FakeStore { ThrowOnStore = true };
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);

        await Assert.ThrowsAsync<ContentArtifactStoreException>(() =>
            Handler(repo, renderer, store, TenantA).Handle(new RenderContentSetRevisionCommand(revision.Id), default));

        Assert.Null(revision.RenderedArtifact);   // fail-closed: nothing bound
        Assert.Equal(0, repo.UpdateCalls);         // nothing persisted
    }

    [Fact]
    public async Task Render_of_a_missing_or_cross_tenant_revision_is_404()
    {
        var repo = new FakeRevisionRepo();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);

        var cross = await Handler(repo, new FakeRenderer(), new FakeStore(), TenantB)
            .Handle(new RenderContentSetRevisionCommand(revision.Id), default);

        Assert.Equal(404, cross.StatusCode);
    }

    [Fact]
    public async Task Artifact_read_is_404_when_the_revision_has_not_been_rendered()
    {
        var repo = new FakeRevisionRepo();
        var store = new FakeStore();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);

        var handler = new GetContentSetRevisionArtifactHandler(Tenant(TenantA), repo, store);
        var r = await handler.Handle(new GetContentSetRevisionArtifactQuery(revision.Id), default);

        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Artifact_read_streams_the_pdf_after_render()
    {
        var repo = new FakeRevisionRepo();
        var renderer = new FakeRenderer();
        var store = new FakeStore();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);
        await Handler(repo, renderer, store, TenantA).Handle(new RenderContentSetRevisionCommand(revision.Id), default);

        var handler = new GetContentSetRevisionArtifactHandler(Tenant(TenantA), repo, store);
        var r = await handler.Handle(new GetContentSetRevisionArtifactQuery(revision.Id), default);

        Assert.True(r.IsSuccessful);
        Assert.Equal("application/pdf", r.Data!.MediaType);
        Assert.True(r.Data.Content.Length > 0);
    }

    // ── the REAL PDFsharp/MigraDoc renderer produces a valid PDF ────────────────────────────────────────────────────
    [Fact]
    public void PdfSharp_renderer_emits_a_valid_pdf_with_the_magic_bytes()
    {
        var repo = new FakeRevisionRepo();
        var revision = SeedRevision(repo, TenantA, ContentSetReviewStatuses.Approved);
        revision.Scope = new ContentSetScopeRef { ContentScopeId = Guid.NewGuid(), ScopeVersion = "1.0" };
        revision.SelectedClaims.Add(new ContentSetClaim { ClaimId = Guid.NewGuid(), ClaimVersion = "1.0" });

        var content = new PdfSharpContentSetRevisionRenderer().Render(revision);

        Assert.Equal("application/pdf", content.MediaType);
        Assert.Equal("SET-1-R1.pdf", content.FileName);
        Assert.True(content.Bytes.Length > 4);
        // %PDF magic bytes.
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, content.Bytes.Take(4).ToArray());
    }
}
