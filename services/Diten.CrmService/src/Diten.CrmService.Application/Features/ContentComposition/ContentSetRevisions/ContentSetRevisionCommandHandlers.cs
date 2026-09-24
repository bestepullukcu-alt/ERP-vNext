using System.Security.Cryptography;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>
/// SCMM-15 (CAND-CAP-0011, DEC-SCMM-04 C3) — freeze a ContentSet draft into an immutable Revision (submitted). The
/// snapshot is a DEEP copy of the draft's manifest, so later edits to the draft never mutate the frozen revision.
/// Idempotent: an already-open revision at the same draft version is returned instead of a second freeze. A shape guard
/// requires at least one component and rejects a draft whose eligibility snapshot still carries an unresolved item
/// (DEC-SCMM-04 — no freeze over an unresolved reference). A stale ExpectedVersion is a controlled 409.
/// </summary>
public sealed class SubmitContentSetForReviewHandler
    : IRequestHandler<SubmitContentSetForReviewCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentSetRepository _sets;
    private readonly IContentSetRevisionRepository _revisions;

    public SubmitContentSetForReviewHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets, IContentSetRevisionRepository revisions)
    {
        _tenant = tenant;
        _actor = actor;
        _sets = sets;
        _revisions = revisions;
    }

    public async Task<Response<Guid>> Handle(SubmitContentSetForReviewCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var set = await _sets.GetByIdAsync(tenantId, request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<Guid>.Fail("Content set not found.", 404);
        }

        if (set.IsArchived())
        {
            return Response<Guid>.Fail("An archived content set cannot be submitted for review.", 409);
        }

        // Stale-submit guard (DEC-SCMM-04): never silently freeze a draft that has moved on since the caller read it.
        if (request.ExpectedVersion is { } expected && expected != set.Version)
        {
            return Response<Guid>.Fail(
                $"Content set version mismatch (expected {expected}, current {set.Version}); reload before submitting.", 409);
        }

        // Shape guard: a manifest with no components is nothing to review.
        if (set.SelectedComponents.Count == 0)
        {
            return Response<Guid>.Fail("A content set needs at least one component before it can be submitted.", 400);
        }

        // Unresolved-reference guard: an applied eligibility snapshot that still has an unresolved item is not review-ready.
        if (set.EligibilitySnapshot is { } snap
            && snap.Items.Any(i => string.Equals(i.State, "unresolved", StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                "The content set has unresolved eligibility items; resolve them before submitting for review.", 400);
        }

        var lineage = await _revisions.ListByContentSetAsync(tenantId, set.Id, cancellationToken);

        // Idempotent: one open review per (set, version). A re-submit at the same version returns the existing revision.
        var open = lineage.FirstOrDefault(r => r.IsOpen() && r.ContentSetVersion == set.Version);
        if (open is not null)
        {
            return Response<Guid>.Success(open.Id, 200);
        }

        var nextNumber = lineage.Count == 0 ? 1 : lineage.Max(r => r.RevisionNumber) + 1;
        var now = DateTimeOffset.UtcNow;
        var revision = new ContentSetRevision
        {
            TenantId = tenantId,
            ContentSetId = set.Id,
            ContentSetVersion = set.Version,
            RevisionNumber = nextNumber,
            RevisionCode = $"{set.SetCode}-R{nextNumber}",
            // ── frozen manifest (deep copy — the revision must not share references with the mutable draft) ──
            Template = new ContentSetTemplateRef
            {
                ConceptChainTemplateId = set.Template.ConceptChainTemplateId,
                ChainVersion = set.Template.ChainVersion
            },
            Scope = set.Scope is null ? null : new ContentSetScopeRef
            {
                ContentScopeId = set.Scope.ContentScopeId,
                ScopeVersion = set.Scope.ScopeVersion
            },
            SelectedComponents = set.SelectedComponents.Select(CloneComponent).ToList(),
            SelectedClaims = set.SelectedClaims.Select(CloneClaim).ToList(),
            EligibilitySnapshot = CloneSnapshot(set.EligibilitySnapshot),
            ReviewStatus = ContentSetReviewStatuses.Submitted,
            SubmittedBy = _actor.ActorName,
            SubmittedAt = now,
            CorrelationId = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _revisions.InsertAsync(revision, cancellationToken);
        return Response<Guid>.Success(revision.Id, 201);
    }

    // Byte-for-byte VO clones: preserve the SelectionId (unlike a draft clone) so the frozen manifest is identical.
    private static ContentSetComponent CloneComponent(ContentSetComponent c) => new()
    {
        SelectionId = c.SelectionId,
        KnowledgeContentId = c.KnowledgeContentId,
        ContentVersion = c.ContentVersion,
        LanguageCode = c.LanguageCode,
        Role = c.Role,
        Arrangement = CloneArrangement(c.Arrangement)
    };

    private static ContentSetClaim CloneClaim(ContentSetClaim c) => new()
    {
        SelectionId = c.SelectionId,
        ClaimId = c.ClaimId,
        ClaimVersion = c.ClaimVersion,
        Arrangement = CloneArrangement(c.Arrangement)
    };

    private static ContentArrangement CloneArrangement(ContentArrangement a) => new()
    {
        TemplateStepId = a.TemplateStepId,
        BranchId = a.BranchId,
        Position = a.Position
    };

    private static ContentSetEligibilitySnapshot? CloneSnapshot(ContentSetEligibilitySnapshot? snap)
        => snap is null ? null : new ContentSetEligibilitySnapshot
        {
            EvaluatedAtUtc = snap.EvaluatedAtUtc,
            Items = snap.Items.Select(i => new ContentSetEligibilityItem
            {
                ItemKind = i.ItemKind,
                SelectionId = i.SelectionId,
                ItemId = i.ItemId,
                PolicyId = i.PolicyId,
                State = i.State,
                BlockingLevel = i.BlockingLevel,
                Reason = i.Reason,
                PolicyVersion = i.PolicyVersion
            }).ToList()
        };
}

/// <summary>
/// SCMM-15 — record an approve / reject decision on an open revision. SoD: the deciding actor must differ from the
/// submitter (else 403). A duplicate identical decision returns the existing outcome; a conflicting decision on an
/// already-decided revision is a 409. The frozen manifest is never touched — only ReviewStatus + Decision advance.
/// </summary>
public sealed class RecordReviewDecisionHandler : IRequestHandler<RecordReviewDecisionCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentSetRevisionRepository _revisions;

    public RecordReviewDecisionHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRevisionRepository revisions)
    {
        _tenant = tenant;
        _actor = actor;
        _revisions = revisions;
    }

    public async Task<Response<bool>> Handle(RecordReviewDecisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var targetStatus = ContentSetReviewDecisions.ToStatus(request.Decision);
        if (targetStatus is null)
        {
            return Response<bool>.Fail(
                $"Decision must be one of: {ContentSetReviewDecisions.Approve}, {ContentSetReviewDecisions.Reject}.", 400);
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, cancellationToken);
        if (revision is null)
        {
            return Response<bool>.Fail("Content set revision not found.", 404);
        }

        // Separation of duties: an author cannot decide their own revision.
        if (ActorMatchesSubmitter(revision.SubmittedBy))
        {
            return Response<bool>.Fail("The reviewer must differ from the submitter (separation of duties).", 403);
        }

        // Already decided: a duplicate identical decision is idempotent; a different one is a controlled conflict.
        if (!revision.IsOpen())
        {
            return string.Equals(revision.ReviewStatus, targetStatus, StringComparison.Ordinal)
                ? Response<bool>.Success(true)
                : Response<bool>.Fail(
                    $"This revision is already {revision.ReviewStatus} and cannot be changed to {targetStatus}.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        revision.ReviewStatus = targetStatus;
        revision.Decision = new ContentSetReviewDecision
        {
            ReviewerId = _actor.ActorName,
            Decision = request.Decision.Trim().ToLowerInvariant(),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            DecidedAt = now
        };
        revision.UpdatedAt = now;
        revision.UpdatedBy = _actor.ActorName;

        await _revisions.UpdateAsync(revision, cancellationToken);
        return Response<bool>.Success(true);
    }

    // Compare on the server-resolved actor identity. When both are absent they are treated as the same principal, so an
    // unauthenticated actor can never rubber-stamp an unauthenticated submission.
    private bool ActorMatchesSubmitter(string? submittedBy)
        => string.Equals(_actor.ActorName ?? string.Empty, submittedBy ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — render an approved revision to a single PDF, store it through the MOD-0262-FU01
/// document repository, and bind the returned content id + checksum to the revision (manifest-bound, AT05). Only an
/// <c>approved</c> revision may be rendered (else 409). The render is idempotent (AT05 retry no-dup): once an artifact is
/// bound, a re-render returns the existing pointer without a second render or upload. The store is <b>fail-closed</b> — a
/// storage failure throws and the artifact is never bound; audit is fail-soft and never blocks the render.
/// </summary>
public sealed class RenderContentSetRevisionHandler
    : IRequestHandler<RenderContentSetRevisionCommand, Response<RenderedArtifactDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentSetRevisionRepository _revisions;
    private readonly IContentSetRevisionRenderer _renderer;
    private readonly IContentArtifactStore _store;
    private readonly IContentCompositionAuditPublisher _audit;

    public RenderContentSetRevisionHandler(
        ITenantContext tenant,
        IActorContext actor,
        IContentSetRevisionRepository revisions,
        IContentSetRevisionRenderer renderer,
        IContentArtifactStore store,
        IContentCompositionAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _revisions = revisions;
        _renderer = renderer;
        _store = store;
        _audit = audit;
    }

    public async Task<Response<RenderedArtifactDto>> Handle(
        RenderContentSetRevisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<RenderedArtifactDto>.Fail("Tenant context is required.", 400);
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, cancellationToken);
        if (revision is null)
        {
            return Response<RenderedArtifactDto>.Fail("Content set revision not found.", 404);
        }

        // Idempotent (AT05 retry no-dup): an already-bound artifact is returned as-is — no re-render, no second upload.
        if (revision.RenderedArtifact is { } bound)
        {
            return Response<RenderedArtifactDto>.Success(ToDto(bound), 200);
        }

        // Only an approved revision may be rendered (the review gate is the release precondition).
        if (!string.Equals(revision.ReviewStatus, ContentSetReviewStatuses.Approved, StringComparison.Ordinal))
        {
            return Response<RenderedArtifactDto>.Fail(
                $"Only an approved content set revision can be rendered (current status: {revision.ReviewStatus}).", 409);
        }

        // Render → store. The store is fail-closed: a non-2xx throws ContentArtifactStoreException, which propagates and
        // leaves RenderedArtifact unset (nothing is persisted), so a storage outage never marks a revision rendered.
        var content = _renderer.Render(revision);
        var stored = await _store.StoreAsync(
            new ContentArtifactStoreRequest(
                revision.Id,
                DeterministicVersionId(revision.Id, revision.RevisionNumber),
                content.FileName,
                content.MediaType,
                content.Bytes),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        revision.RenderedArtifact = new ContentSetRenderedArtifact
        {
            ContentId = stored.ContentId,
            Checksum = stored.Checksum,
            MediaType = stored.MediaType,
            ByteSize = stored.ByteSize,
            FileName = content.FileName,
            RenderedAtUtc = now,
            RenderedBy = _actor.ActorName
        };
        revision.UpdatedAt = now;
        revision.UpdatedBy = _actor.ActorName;

        await _revisions.UpdateAsync(revision, cancellationToken);

        await SafeAuditAsync(tenantId, revision, stored, cancellationToken);

        return Response<RenderedArtifactDto>.Success(ToDto(revision.RenderedArtifact), 201);
    }

    // Fail-soft: an audit outage never breaks a completed render (the artifact is already stored and bound).
    private async Task SafeAuditAsync(
        Guid tenantId, ContentSetRevision revision, ContentArtifactStoreResult stored, CancellationToken cancellationToken)
    {
        try
        {
            // Counts / ids / correlation only — no manifest payload or PII.
            var detail = $"revision={revision.RevisionCode};contentId={stored.ContentId:D};bytes={stored.ByteSize}";
            await _audit.PublishAsync(
                ContentSetRevisionReasonCodes.Rendered, tenantId, "ContentSetRevision", revision.Id, revision.Version,
                detail, cancellationToken);
        }
        catch
        {
            // Audit is a side effect, not a gate (mirrors IContentCompositionAuditPublisher's fail-soft contract).
        }
    }

    private static RenderedArtifactDto ToDto(ContentSetRenderedArtifact a) => new(
        a.ContentId, a.Checksum, a.MediaType, a.ByteSize, a.FileName, a.RenderedAtUtc, a.RenderedBy);

    // A stable per-(revision, revisionNumber) version id so the deterministic FU01 object key is reproducible. Idempotency
    // itself is enforced above (the bound-artifact short-circuit); this only keeps the storage key stable on a retry.
    private static Guid DeterministicVersionId(Guid revisionId, int revisionNumber)
    {
        var seed = Encoding.UTF8.GetBytes($"content-set-revision-artifact:{revisionId:N}:{revisionNumber}");
        return new Guid(MD5.HashData(seed));
    }
}

/// <summary>
/// SCMM-17 (CAND-CAP-0011, SCMM-17) — release a rendered revision's artifact. The released artifact's ContentId +
/// Checksum are pinned into the release state (manifest-bound, AT05). Preconditions: the revision must be rendered (else
/// 409) and the releaser must differ from the reviewer (separation of duties, else 403 — author → reviewer → releaser are
/// three distinct roles). Idempotent: an already-released revision returns its state (200); a withdrawn revision cannot be
/// re-released (409). Audit is fail-soft.
/// </summary>
public sealed class ReleaseContentSetRevisionHandler
    : IRequestHandler<ReleaseContentSetRevisionCommand, Response<ReleaseStateDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentSetRevisionRepository _revisions;
    private readonly IContentCompositionAuditPublisher _audit;

    public ReleaseContentSetRevisionHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRevisionRepository revisions,
        IContentCompositionAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _revisions = revisions;
        _audit = audit;
    }

    public async Task<Response<ReleaseStateDto>> Handle(
        ReleaseContentSetRevisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ReleaseStateDto>.Fail("Tenant context is required.", 400);
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, cancellationToken);
        if (revision is null)
        {
            return Response<ReleaseStateDto>.Fail("Content set revision not found.", 404);
        }

        // Idempotent replay: already released → return the existing state. Withdrawn is terminal → a re-release is 409.
        if (revision.IsReleased())
        {
            return Response<ReleaseStateDto>.Success(ContentSetRevisionMapper.ToDto(revision.ReleaseState)!, 200);
        }

        if (revision.IsWithdrawn())
        {
            return Response<ReleaseStateDto>.Fail(
                "A withdrawn content set revision cannot be re-released; create a new revision.", 409);
        }

        // Precondition: only a rendered revision has an artifact to release.
        if (revision.RenderedArtifact is not { } artifact)
        {
            return Response<ReleaseStateDto>.Fail(
                "The content set revision has not been rendered; render it before releasing.", 409);
        }

        // Separation of duties: the releaser must differ from the reviewer. An approved revision always carries a review
        // decision with a reviewer; a missing reviewer would make SoD unverifiable, so it is a controlled 409, not a bypass.
        var reviewerId = revision.Decision?.ReviewerId;
        if (string.IsNullOrWhiteSpace(reviewerId))
        {
            return Response<ReleaseStateDto>.Fail(
                "The content set revision has no recorded reviewer; it cannot be released.", 409);
        }

        if (string.Equals(_actor.ActorName ?? string.Empty, reviewerId, StringComparison.OrdinalIgnoreCase))
        {
            return Response<ReleaseStateDto>.Fail(
                "The releaser must differ from the reviewer (separation of duties).", 403);
        }

        var now = DateTimeOffset.UtcNow;
        revision.ReleaseState = new ContentSetReleaseState
        {
            ReleaseStatus = ContentSetReleaseStatuses.Released,
            ReleasedArtifactContentId = artifact.ContentId,   // manifest-bound pin
            ReleasedArtifactChecksum = artifact.Checksum,
            ReleasedAtUtc = now,
            ReleasedBy = _actor.ActorName
        };
        revision.UpdatedAt = now;
        revision.UpdatedBy = _actor.ActorName;

        await _revisions.UpdateAsync(revision, cancellationToken);

        await ContentSetRevisionAudit.SafePublishAsync(
            _audit, tenantId, ContentSetRevisionReasonCodes.Released, revision,
            $"revision={revision.RevisionCode};contentId={artifact.ContentId:D}", cancellationToken);

        return Response<ReleaseStateDto>.Success(ContentSetRevisionMapper.ToDto(revision.ReleaseState)!, 200);
    }
}

/// <summary>
/// SCMM-17 (CAND-CAP-0011, SCMM-17) — managed withdrawal of a released revision. Only a released revision can be withdrawn
/// (else 409) and a reason is required (else 400). Idempotent: an already-withdrawn revision returns its state (200).
/// ⛔ Withdrawal is a <b>state change, not a deletion</b> — it records who/when/why and never removes the stored artifact
/// bytes (AD-6). It is terminal: a withdrawn revision is never re-released. This handler has NO artifact-store dependency,
/// so there is structurally no delete/compensate/purge path it can reach.
/// </summary>
public sealed class WithdrawContentSetRevisionHandler
    : IRequestHandler<WithdrawContentSetRevisionCommand, Response<ReleaseStateDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContentSetRevisionRepository _revisions;
    private readonly IContentCompositionAuditPublisher _audit;

    public WithdrawContentSetRevisionHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRevisionRepository revisions,
        IContentCompositionAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _revisions = revisions;
        _audit = audit;
    }

    public async Task<Response<ReleaseStateDto>> Handle(
        WithdrawContentSetRevisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ReleaseStateDto>.Fail("Tenant context is required.", 400);
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, cancellationToken);
        if (revision is null)
        {
            return Response<ReleaseStateDto>.Fail("Content set revision not found.", 404);
        }

        // Idempotent replay: already withdrawn → return the existing state (a retry needs no fresh reason).
        if (revision.IsWithdrawn())
        {
            return Response<ReleaseStateDto>.Success(ContentSetRevisionMapper.ToDto(revision.ReleaseState)!, 200);
        }

        // Only a released revision can be withdrawn.
        if (!revision.IsReleased())
        {
            return Response<ReleaseStateDto>.Fail(
                "Only a released content set revision can be withdrawn.", 409);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Response<ReleaseStateDto>.Fail("A withdrawal reason is required.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var state = revision.ReleaseState!;                 // released ⇒ present
        state.ReleaseStatus = ContentSetReleaseStatuses.Withdrawn;
        state.WithdrawnAtUtc = now;
        state.WithdrawnBy = _actor.ActorName;
        state.WithdrawalReason = request.Reason.Trim();
        revision.UpdatedAt = now;
        revision.UpdatedBy = _actor.ActorName;

        // NO byte deletion: the stored artifact is retained (AD-6). This handler never calls the artifact store.
        await _revisions.UpdateAsync(revision, cancellationToken);

        await ContentSetRevisionAudit.SafePublishAsync(
            _audit, tenantId, ContentSetRevisionReasonCodes.Withdrawn, revision,
            $"revision={revision.RevisionCode};contentId={state.ReleasedArtifactContentId:D}", cancellationToken);

        return Response<ReleaseStateDto>.Success(ContentSetRevisionMapper.ToDto(state)!, 200);
    }
}

/// <summary>Fail-soft audit helper for the SCMM-17 release/withdraw transitions — an audit outage never breaks a completed
/// state change (mirrors the IContentCompositionAuditPublisher fail-soft contract). Detail is counts/ids only (no PII).</summary>
internal static class ContentSetRevisionAudit
{
    public static async Task SafePublishAsync(
        IContentCompositionAuditPublisher audit, Guid tenantId, string eventName, ContentSetRevision revision,
        string detail, CancellationToken cancellationToken)
    {
        try
        {
            await audit.PublishAsync(
                eventName, tenantId, "ContentSetRevision", revision.Id, revision.Version, detail, cancellationToken);
        }
        catch
        {
            // Audit is a side effect, not a gate.
        }
    }
}
