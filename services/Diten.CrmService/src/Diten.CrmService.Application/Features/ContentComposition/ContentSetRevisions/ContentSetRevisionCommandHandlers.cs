using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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
