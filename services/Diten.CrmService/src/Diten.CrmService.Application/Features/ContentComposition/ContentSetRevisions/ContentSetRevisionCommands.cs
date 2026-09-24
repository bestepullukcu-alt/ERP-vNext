using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

// SCMM-15 (CAND-CAP-0011, DEC-SCMM-04 C3) ContentSetRevision write surface. TenantId is server-resolved and never in the
// payload. A revision freezes a ContentSet draft's manifest, then advances only through the in-domain review states.

/// <summary>Submit a ContentSet draft for review: freeze its manifest into a new immutable Revision (status
/// <c>submitted</c>). Idempotent — if an open revision (submitted/in-review) already exists for the same set at the same
/// version, that revision is returned rather than a second one created. <see cref="ExpectedVersion"/> (optional) guards a
/// stale submit: a mismatch with the draft's current concurrency Version is a controlled 409, never a silent freeze of a
/// changed draft.</summary>
public sealed record SubmitContentSetForReviewCommand(
    Guid ContentSetId,
    int? ExpectedVersion = null) : IRequest<Response<Guid>>;

/// <summary>Record a review decision on a revision. <see cref="Decision"/> is approve | reject
/// (<see cref="Diten.CrmService.Domain.Entities.ContentSetReviewDecisions"/>). Only an open revision can be decided;
/// separation-of-duties requires the deciding actor to differ from the submitter (else 403); a duplicate decision returns
/// the existing outcome.</summary>
public sealed record RecordReviewDecisionCommand(
    Guid RevisionId,
    string Decision,
    string? Reason = null) : IRequest<Response<bool>>;

/// <summary>SCMM-16B (CAND-CAP-0011, SCMM-16) — render an approved revision's frozen manifest to a single PDF, store it
/// through the MOD-0262-FU01 document repository, and bind the returned content id + checksum to the revision. Only an
/// <c>approved</c> revision can be rendered (else 409). Idempotent (AT05 retry no-dup): once an artifact is bound, a
/// re-render returns the existing pointer with no second upload. TenantId is server-resolved; the revision id is the
/// route, never a client-supplied content id.</summary>
public sealed record RenderContentSetRevisionCommand(Guid RevisionId)
    : IRequest<Response<RenderedArtifactDto>>;

/// <summary>SCMM-17 (CAND-CAP-0011, SCMM-17) — release a rendered revision's artifact (manifest-bound: the
/// RenderedArtifact ContentId + Checksum are pinned into the release state). Preconditions: the revision must be rendered
/// (else 409) and the releaser must differ from the reviewer (separation of duties, else 403). Idempotent — an
/// already-released revision returns its release state; a withdrawn revision cannot be re-released (409).</summary>
public sealed record ReleaseContentSetRevisionCommand(Guid RevisionId)
    : IRequest<Response<ReleaseStateDto>>;

/// <summary>SCMM-17 — managed withdrawal of a released revision. Only a released revision can be withdrawn (else 409); a
/// reason is required (else 400). Idempotent — an already-withdrawn revision returns its state. Withdrawal is a managed
/// state change: the stored artifact bytes are never deleted (AD-6), and it is terminal (no re-release).</summary>
public sealed record WithdrawContentSetRevisionCommand(Guid RevisionId, string Reason)
    : IRequest<Response<ReleaseStateDto>>;
