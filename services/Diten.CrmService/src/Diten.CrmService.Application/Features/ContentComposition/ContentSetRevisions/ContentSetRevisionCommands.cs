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
