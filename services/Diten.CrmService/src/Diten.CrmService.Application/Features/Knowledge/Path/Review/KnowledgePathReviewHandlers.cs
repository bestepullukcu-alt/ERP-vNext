using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Review;

/// <summary>Shared loading for the review handlers: tenant, path, its revisions (reconciled when a round is stale).</summary>
internal static class KnowledgePathReviewLoad
{
    public static async Task<(Guid TenantId, KnowledgePath? Path, Response<T>? Error)> PathAsync<T>(
        ITenantContext tenant, IKnowledgePathRepository paths, Guid pathId, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return (Guid.Empty, null, Response<T>.Fail("Tenant context is required.", 400));
        }

        var path = await paths.GetByIdAsync(tenantId, pathId, ct);
        return path is null
            ? (tenantId, null, Response<T>.Fail("Knowledge path not found.", 404))
            : (tenantId, path, null);
    }

    /// <summary>The path's revisions after reconcile-on-read (a MOD-0023 problem never breaks the read).</summary>
    public static async Task<IReadOnlyList<KnowledgePathRevision>> RevisionsAsync(
        IKnowledgePathRevisionRepository revisions, KnowledgePathReviewReconciler? reconciler, Guid tenantId, Guid pathId,
        CancellationToken ct)
    {
        var rows = await revisions.ListByPathAsync(tenantId, pathId, ct);
        if (reconciler is not null && rows.Any(r => r.IsOpen()) && await reconciler.ReconcileAsync(tenantId, rows, ct) > 0)
        {
            rows = await revisions.ListByPathAsync(tenantId, pathId, ct);
        }

        return rows;
    }

    public static Response<T> FromStart<T>(ClaimWorkflowStartResult result) => result.Outcome switch
    {
        ClaimWorkflowCallOutcome.TemplateMissing => KnowledgePathReviewRules.Fail<T>(ClaimErrorCodes.ApprovalTemplateMissing,
            "The MLR approval workflow template of this country is not available (not created or not published).", 409),
        ClaimWorkflowCallOutcome.Forbidden => KnowledgePathReviewRules.Fail<T>(ClaimErrorCodes.ApprovalForbidden,
            "You are not allowed to start the approval workflow.", 403),
        ClaimWorkflowCallOutcome.Rejected => KnowledgePathReviewRules.Fail<T>(ClaimErrorCodes.WorkflowRequestRejected,
            $"The approval workflow refused the request: {result.Detail}", 400),
        _ => KnowledgePathReviewRules.Fail<T>(ClaimErrorCodes.WorkflowUnavailable,
            "The approval workflow service cannot be reached. Nothing was changed.", 503)
    };

    public static Response<T> Unavailable<T>() => KnowledgePathReviewRules.Fail<T>(ClaimErrorCodes.WorkflowUnavailable,
        "The approval workflow service cannot be reached. Nothing was changed.", 503);
}

/// <summary>
/// WP-KP-2 — sends a chain-bound DRAFT path to its country's MLR workflow. The gate runs first (nothing written on a
/// refusal); then MOD-0023 is started with the caller's token and a deterministic idempotency key
/// (<c>crm:knowledge-path:{pathId}:r{n}</c>) — a lost answer re-sent reaches the same instance; only after MOD-0023
/// accepted is the revision written and the path moved to <c>review</c>. The draft stays editable (the revision is the
/// frozen copy); the previous revision's unresolved notes are carried and the change summary is computed against it.
/// </summary>
public sealed class SubmitKnowledgePathReviewHandler
    : IRequestHandler<SubmitKnowledgePathReviewCommand, Response<KnowledgePathRevisionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IClaimRepository _claims;
    private readonly KnowledgePathStudioReader _studio;
    private readonly IClaimWorkflowClient _workflow;
    private readonly KnowledgePathReviewReconciler? _reconciler;
    private readonly IKnowledgePathReviewSettings? _settings;
    private readonly ILogger<SubmitKnowledgePathReviewHandler> _logger;

    public SubmitKnowledgePathReviewHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IKnowledgeContentRepository contents, IClaimRepository claims,
        KnowledgePathStudioReader studio, IClaimWorkflowClient workflow, KnowledgePathReviewReconciler? reconciler = null,
        IKnowledgePathReviewSettings? settings = null, ILogger<SubmitKnowledgePathReviewHandler>? logger = null)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
        _contents = contents;
        _claims = claims;
        _studio = studio;
        _workflow = workflow;
        _reconciler = reconciler;
        _settings = settings;
        _logger = logger ?? NullLogger<SubmitKnowledgePathReviewHandler>.Instance;
    }

    public async Task<Response<KnowledgePathRevisionDto>> Handle(SubmitKnowledgePathReviewCommand request, CancellationToken ct)
    {
        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<KnowledgePathRevisionDto>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        if (path!.IsArchived())
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(ClaimErrorCodes.InvalidStatus,
                "An archived path cannot be sent for review.", 409);
        }

        var revisions = await KnowledgePathReviewLoad.RevisionsAsync(_revisions, _reconciler, tenantId, path.Id, ct);
        if (revisions.Any(r => r.IsOpen()))
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(KnowledgePathReviewErrors.ReviewRoundOpen,
                "A review round of this path is still open; withdraw it or wait for its outcome.", 409);
        }

        // Reconcile may have moved the path; read it again before the status gate.
        path = await _paths.GetByIdAsync(tenantId, path.Id, ct) ?? path;
        if (KnowledgePathReviewSubmission.CheckPath(path) is { } pathGate)
        {
            return pathGate.To<KnowledgePathRevisionDto>();
        }

        var studio = await _studio.ReadAsync(tenantId, path, ct);
        var (snapshot, gate) = await KnowledgePathReviewSubmission.FreezeAsync(tenantId, path, studio, _contents, _claims, ct);
        if (gate is not null)
        {
            return gate.To<KnowledgePathRevisionDto>();
        }

        var previous = revisions.LastOrDefault();
        var number = (previous?.RevisionNumber ?? 0) + 1;
        var revisionId = Guid.NewGuid();
        var templateCode = string.Format(
            _settings?.TemplateCodeFormat ?? KnowledgePathReviewDefaults.TemplateCodeFormat, path.CountryCode);
        var start = await _workflow.StartAsync(new ClaimWorkflowStartRequest(
            templateCode,
            KnowledgePathReviewRules.ObjectType,
            revisionId.ToString("D"),
            KnowledgePathReviewRules.ObjectRef(revisionId),
            KnowledgePathReviewRules.IdempotencyKey(path.Id, number),
            new ClaimWorkflowDisplayContext(
                ClaimReviewRules.Truncate($"Bilgi yolu onayı · {path.PathCode} v{path.PathVersion} · Rev {number}", 200),
                ClaimReviewRules.Truncate(path.PathName, 300),
                "crm",
                KnowledgePathReviewRules.ReviewLink(path.Id, revisionId),
                [
                    ClaimReviewRules.Truncate(path.CountryCode ?? string.Empty, 32),
                    ClaimReviewRules.Truncate(path.LanguageCode ?? string.Empty, 32),
                    ClaimReviewRules.Truncate($"v{path.PathVersion}", 32)
                ]),
            KnowledgePathReviewRules.SubmitReasonCode), ct);
        if (start.Outcome != ClaimWorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return KnowledgePathReviewLoad.FromStart<KnowledgePathRevisionDto>(start);
        }

        var now = DateTimeOffset.UtcNow;
        var revision = new KnowledgePathRevision
        {
            Id = revisionId,
            TenantId = tenantId,
            PathId = path.Id,
            PathCode = path.PathCode,
            PathVersion = path.PathVersion,
            RevisionNumber = number,
            Status = KnowledgePathRevisionStatuses.InReview,
            CreatedAt = now,
            CreatedBy = _actor.ActorName,
            Snapshot = snapshot!,
            ReviewRound = new ClaimReviewRound
            {
                WorkflowInstanceId = instanceId, SubmittedAt = now, SubmittedBy = _actor.ActorName, RoundNo = number,
                TemplateCode = templateCode
            },
            Notes = KnowledgePathReviewSubmission.CarryNotes(previous),
            ChangeSummary = KnowledgePathReviewSubmission.Diff(previous, snapshot!)
        };
        await _revisions.InsertAsync(revision, ct);

        // The path follows the round (review). One retry on an optimistic conflict with a concurrent edit.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            path.PathStatus = KnowledgePathStatuses.Review;
            path.UpdatedAt = now;
            path.UpdatedBy = _actor.ActorName;
            if (await _paths.ReplaceAsync(path, path.Version, ct))
            {
                break;
            }

            _logger.LogWarning("knowledge_path.review.submit_path_conflict PathId={PathId} Attempt={Attempt}", path.Id, attempt);
            path = await _paths.GetByIdAsync(tenantId, path.Id, ct) ?? path;
        }

        return Response<KnowledgePathRevisionDto>.Success(KnowledgePathReviewMapper.ToDto(revision), 201);
    }
}

/// <summary>WP-KP-2 — cancels the open round's waiting MOD-0023 task (Platform cancel, caller's token) and applies
/// <c>cancelled</c> through the applier: the revision is withdrawn, the path back to draft.</summary>
public sealed class WithdrawKnowledgePathReviewHandler
    : IRequestHandler<WithdrawKnowledgePathReviewCommand, Response<KnowledgePathRevisionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IClaimWorkflowClient _workflow;
    private readonly KnowledgePathRevisionOutcomeApplier _applier;

    public WithdrawKnowledgePathReviewHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IClaimWorkflowClient workflow, KnowledgePathRevisionOutcomeApplier applier)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
        _workflow = workflow;
        _applier = applier;
    }

    public async Task<Response<KnowledgePathRevisionDto>> Handle(WithdrawKnowledgePathReviewCommand request, CancellationToken ct)
    {
        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<KnowledgePathRevisionDto>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var revision = (await _revisions.ListByPathAsync(tenantId, path!.Id, ct)).LastOrDefault(r => r.IsOpen());
        if (revision is null)
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(ClaimErrorCodes.NoOpenReview,
                "There is no open review round to withdraw.", 409);
        }

        // WP-E2E-FIX-2 (E3-B4) — withdrawing is the submitter's own act: a manager who did not submit the round cannot
        // pull it back (person-based, the same SamePerson rule as the decision SoD). Manage stays required by the route.
        if (!KnowledgePathReviewRules.SamePerson(_actor.ActorName, revision.ReviewRound.SubmittedBy))
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(KnowledgePathReviewErrors.WithdrawNotSubmitter,
                "Only the person who submitted this review round can withdraw it.", 403);
        }

        var instanceId = revision.ReviewRound.WorkflowInstanceId;
        var tasks = await _workflow.GetTasksAsync([instanceId], ct);
        if (tasks is null)
        {
            return KnowledgePathReviewLoad.Unavailable<KnowledgePathRevisionDto>();
        }

        var open = tasks.Where(t => t.WorkflowInstanceId == instanceId && t.IsOpen)
            .OrderByDescending(t => t.DueAt ?? DateTimeOffset.MinValue).FirstOrDefault();
        if (open is null)
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(ClaimErrorCodes.WithdrawNotPossible,
                "The workflow has no waiting step to cancel (it may have just finished).", 409);
        }

        var actor = string.IsNullOrWhiteSpace(_actor.ActorName) ? "crm-user" : _actor.ActorName!;
        var cancel = await _workflow.CancelTaskAsync(open.TaskId, actor, KnowledgePathReviewRules.WithdrawReasonCode,
            $"{KnowledgePathReviewRules.IdempotencyKey(path.Id, revision.RevisionNumber)}:withdraw", ct);
        switch (cancel)
        {
            case ClaimWorkflowCallOutcome.Ok:
                break;
            case ClaimWorkflowCallOutcome.Forbidden:
                return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(ClaimErrorCodes.ApprovalForbidden,
                    "You are not allowed to cancel this approval.", 403);
            case ClaimWorkflowCallOutcome.Unavailable:
                return KnowledgePathReviewLoad.Unavailable<KnowledgePathRevisionDto>();
            default:
                return KnowledgePathReviewRules.Fail<KnowledgePathRevisionDto>(ClaimErrorCodes.WithdrawNotPossible,
                    "The approval workflow refused to cancel the waiting step.", 409);
        }

        await _applier.ApplyAsync(tenantId, revision.Id, instanceId, ClaimReviewOutcomes.Cancelled, actor,
            KnowledgePathReviewRules.WithdrawReasonCode, DateTimeOffset.UtcNow, ct);
        var reloaded = await _revisions.GetByIdAsync(tenantId, revision.Id, ct) ?? revision;
        return Response<KnowledgePathRevisionDto>.Success(KnowledgePathReviewMapper.ToDto(reloaded));
    }
}

/// <summary>
/// WP-KP-2 (K1, D-KP-8) — a reviewer's decision, written to the SAME MOD-0023 task the Work Center shows, with the
/// reviewer's comment (required on a rejection — 400 <c>comment_required</c>). Person-based SoD first: the revision's
/// submitter can never decide on it (403 <c>sod_submitter_cannot_decide</c>; a person, not a role). The task is the
/// caller's assignable open task of the round's instance (<c>tasks/mine</c>, caller's token); when the caller holds none
/// the instance's open task is still called so MOD-0023's own 403 (not a candidate) is returned as is.
/// </summary>
public sealed class DecideKnowledgePathRevisionHandler
    : IRequestHandler<DecideKnowledgePathRevisionCommand, Response<KnowledgePathDecisionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IClaimWorkflowClient _workflow;
    private readonly IWorkflowDecisionClient _decisions;

    public DecideKnowledgePathRevisionHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IClaimWorkflowClient workflow, IWorkflowDecisionClient decisions)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
        _workflow = workflow;
        _decisions = decisions;
    }

    public async Task<Response<KnowledgePathDecisionDto>> Handle(DecideKnowledgePathRevisionCommand request, CancellationToken ct)
    {
        var decision = request.Decision?.Trim().ToLowerInvariant();
        if (decision is not (KnowledgePathDecisions.Approve or KnowledgePathDecisions.Reject))
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(KnowledgePathReviewErrors.DecisionInvalid,
                "Decision must be 'approve' or 'reject'.", 400);
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (decision == KnowledgePathDecisions.Reject && comment is null)
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(KnowledgePathReviewErrors.CommentRequired,
                "A rejection needs a comment (the reason the author will act on).", 400);
        }

        if (comment is { Length: > KnowledgePathReviewRules.MaxCommentLength })
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(KnowledgePathReviewErrors.CommentRequired,
                $"The comment cannot exceed {KnowledgePathReviewRules.MaxCommentLength} characters.", 400);
        }

        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<KnowledgePathDecisionDto>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, ct);
        if (revision is null || revision.PathId != path!.Id)
        {
            return Response<KnowledgePathDecisionDto>.Fail("Revision not found.", 404);
        }

        if (!revision.IsOpen())
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(ClaimErrorCodes.NoOpenReview,
                "This revision has no open review round.", 409);
        }

        var actor = _actor.ActorName;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(ClaimErrorCodes.ApprovalForbidden,
                "The caller's identity is required to decide.", 403);
        }

        // D-KP-8 — person-based SoD (the submitter, whatever positions they hold, never decides on their own revision).
        if (KnowledgePathReviewRules.SamePerson(actor, revision.CreatedBy)
            || KnowledgePathReviewRules.SamePerson(actor, revision.ReviewRound.SubmittedBy))
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(KnowledgePathReviewErrors.SodSubmitterCannotDecide,
                "The submitter of a revision cannot decide on it.", 403);
        }

        var instanceId = revision.ReviewRound.WorkflowInstanceId;
        var mine = await _decisions.GetMyTasksAsync(ct);
        if (mine is null)
        {
            return KnowledgePathReviewLoad.Unavailable<KnowledgePathDecisionDto>();
        }

        var task = mine.FirstOrDefault(t => t.WorkflowInstanceId == instanceId && t.IsOpen);
        if (task is null)
        {
            var all = await _workflow.GetTasksAsync([instanceId], ct);
            if (all is null)
            {
                return KnowledgePathReviewLoad.Unavailable<KnowledgePathDecisionDto>();
            }

            task = all.FirstOrDefault(t => t.WorkflowInstanceId == instanceId && t.IsOpen);
            if (task is null)
            {
                return KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(ClaimErrorCodes.NoOpenReview,
                    "The review has no waiting step (it may have just finished).", 409);
            }
        }

        var approve = decision == KnowledgePathDecisions.Approve;
        var result = await _decisions.DecideTaskAsync(task.TaskId, approve, actor,
            approve ? KnowledgePathReviewRules.ApproveReasonCode : KnowledgePathReviewRules.RejectReasonCode,
            $"{KnowledgePathReviewRules.IdempotencyKey(path.Id, revision.RevisionNumber)}:{task.TaskId:D}:{decision}",
            comment, ct);
        return result.Outcome switch
        {
            ClaimWorkflowCallOutcome.Ok => Response<KnowledgePathDecisionDto>.Success(
                new KnowledgePathDecisionDto(revision.Id, task.TaskId, decision)),
            ClaimWorkflowCallOutcome.Forbidden => KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(
                ClaimErrorCodes.ApprovalForbidden, result.Detail ?? "You are not a reviewer of this step.", 403),
            ClaimWorkflowCallOutcome.NotFound => KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(
                ClaimErrorCodes.NoOpenReview, "The review step is no longer waiting.", 409),
            ClaimWorkflowCallOutcome.Rejected => KnowledgePathReviewRules.Fail<KnowledgePathDecisionDto>(
                ClaimErrorCodes.WorkflowRequestRejected, $"The approval workflow refused the decision: {result.Detail}", 400),
            _ => KnowledgePathReviewLoad.Unavailable<KnowledgePathDecisionDto>()
        };
    }
}

/// <summary>WP-KP-2 — pins a note on a revision (page / block / step, optional position).</summary>
public sealed class AddKnowledgePathRevisionNoteHandler
    : IRequestHandler<AddKnowledgePathRevisionNoteCommand, Response<KnowledgePathRevisionNoteDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;

    public AddKnowledgePathRevisionNoteHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
    }

    public async Task<Response<KnowledgePathRevisionNoteDto>> Handle(AddKnowledgePathRevisionNoteCommand request, CancellationToken ct)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > KnowledgePathReviewRules.MaxNoteLength)
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionNoteDto>(KnowledgePathReviewErrors.NoteTextRequired,
                $"A note needs a text of 1–{KnowledgePathReviewRules.MaxNoteLength} characters.", 400);
        }

        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<KnowledgePathRevisionNoteDto>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, ct);
        if (revision is null || revision.PathId != path!.Id)
        {
            return Response<KnowledgePathRevisionNoteDto>.Fail("Revision not found.", 404);
        }

        var note = new KnowledgePathRevisionNote
        {
            PageRef = Trim(request.PageRef), BlockRef = Trim(request.BlockRef), StepRef = Trim(request.StepRef),
            X = request.X, Y = request.Y, Text = text, Author = _actor.ActorName, CreatedAt = DateTimeOffset.UtcNow
        };
        revision.Notes.Add(note);
        revision.UpdatedAt = note.CreatedAt;
        revision.UpdatedBy = _actor.ActorName;
        return await _revisions.ReplaceAsync(revision, revision.Version, ct)
            ? Response<KnowledgePathRevisionNoteDto>.Success(KnowledgePathReviewMapper.ToDto(note), 201)
            : Response<KnowledgePathRevisionNoteDto>.Fail("The revision was modified by another writer; reload and retry.", 409);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>WP-KP-2 — resolves a note: its author, or a path manager.</summary>
public sealed class ResolveKnowledgePathRevisionNoteHandler
    : IRequestHandler<ResolveKnowledgePathRevisionNoteCommand, Response<KnowledgePathRevisionNoteDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;

    public ResolveKnowledgePathRevisionNoteHandler(ITenantContext tenant, IActorContext actor,
        IKnowledgePathRepository paths, IKnowledgePathRevisionRepository revisions)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _revisions = revisions;
    }

    public async Task<Response<KnowledgePathRevisionNoteDto>> Handle(ResolveKnowledgePathRevisionNoteCommand request, CancellationToken ct)
    {
        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<KnowledgePathRevisionNoteDto>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, ct);
        var note = revision is null || revision.PathId != path!.Id
            ? null
            : revision.Notes.FirstOrDefault(n => n.NoteId == request.NoteId);
        if (note is null)
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionNoteDto>(KnowledgePathReviewErrors.NoteNotFound,
                "Note not found on this revision.", 404);
        }

        if (!request.CanManage && !KnowledgePathReviewRules.SamePerson(_actor.ActorName, note.Author))
        {
            return KnowledgePathReviewRules.Fail<KnowledgePathRevisionNoteDto>(ClaimErrorCodes.ApprovalForbidden,
                "Only the note's author or a path manager can resolve it.", 403);
        }

        if (note.IsResolved())
        {
            return Response<KnowledgePathRevisionNoteDto>.Success(KnowledgePathReviewMapper.ToDto(note)); // idempotent
        }

        note.ResolvedAt = DateTimeOffset.UtcNow;
        note.ResolvedBy = _actor.ActorName;
        revision!.UpdatedAt = note.ResolvedAt;
        revision.UpdatedBy = _actor.ActorName;
        return await _revisions.ReplaceAsync(revision, revision.Version, ct)
            ? Response<KnowledgePathRevisionNoteDto>.Success(KnowledgePathReviewMapper.ToDto(note))
            : Response<KnowledgePathRevisionNoteDto>.Fail("The revision was modified by another writer; reload and retry.", 409);
    }
}

public sealed class ListKnowledgePathRevisionsHandler
    : IRequestHandler<ListKnowledgePathRevisionsQuery, Response<IReadOnlyList<KnowledgePathRevisionSummaryDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly KnowledgePathReviewReconciler? _reconciler;

    public ListKnowledgePathRevisionsHandler(ITenantContext tenant, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, KnowledgePathReviewReconciler? reconciler = null)
    {
        _tenant = tenant;
        _paths = paths;
        _revisions = revisions;
        _reconciler = reconciler;
    }

    public async Task<Response<IReadOnlyList<KnowledgePathRevisionSummaryDto>>> Handle(
        ListKnowledgePathRevisionsQuery request, CancellationToken ct)
    {
        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<IReadOnlyList<KnowledgePathRevisionSummaryDto>>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var rows = await KnowledgePathReviewLoad.RevisionsAsync(_revisions, _reconciler, tenantId, path!.Id, ct);
        return Response<IReadOnlyList<KnowledgePathRevisionSummaryDto>>.Success(
            rows.OrderByDescending(r => r.RevisionNumber).Select(KnowledgePathReviewMapper.ToSummary).ToList());
    }
}

public sealed class GetKnowledgePathRevisionHandler
    : IRequestHandler<GetKnowledgePathRevisionQuery, Response<KnowledgePathRevisionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly KnowledgePathReviewReconciler? _reconciler;

    public GetKnowledgePathRevisionHandler(ITenantContext tenant, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, KnowledgePathReviewReconciler? reconciler = null)
    {
        _tenant = tenant;
        _paths = paths;
        _revisions = revisions;
        _reconciler = reconciler;
    }

    public async Task<Response<KnowledgePathRevisionDto>> Handle(GetKnowledgePathRevisionQuery request, CancellationToken ct)
    {
        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<KnowledgePathRevisionDto>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var revision = await _revisions.GetByIdAsync(tenantId, request.RevisionId, ct);
        if (revision is null || revision.PathId != path!.Id)
        {
            return Response<KnowledgePathRevisionDto>.Fail("Revision not found.", 404);
        }

        if (revision.IsOpen() && _reconciler is not null && await _reconciler.ReconcileAsync(tenantId, [revision], ct) > 0)
        {
            revision = await _revisions.GetByIdAsync(tenantId, revision.Id, ct) ?? revision;
        }

        return Response<KnowledgePathRevisionDto>.Success(KnowledgePathReviewMapper.ToDto(revision));
    }
}

/// <summary>WP-KP-2 (REQ-WCN-01 W-1/W-2, CRM side) — per revision, MOD-0023's transition history with the step NAME,
/// the comment, the person and the time. A history MOD-0023 cannot give is flagged unavailable, never invented.</summary>
public sealed class GetKnowledgePathReviewHistoryHandler
    : IRequestHandler<GetKnowledgePathReviewHistoryQuery, Response<IReadOnlyList<KnowledgePathReviewHistoryDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IWorkflowDecisionClient _decisions;

    public GetKnowledgePathReviewHistoryHandler(ITenantContext tenant, IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions, IWorkflowDecisionClient decisions)
    {
        _tenant = tenant;
        _paths = paths;
        _revisions = revisions;
        _decisions = decisions;
    }

    public async Task<Response<IReadOnlyList<KnowledgePathReviewHistoryDto>>> Handle(
        GetKnowledgePathReviewHistoryQuery request, CancellationToken ct)
    {
        var (tenantId, path, error) = await KnowledgePathReviewLoad.PathAsync<IReadOnlyList<KnowledgePathReviewHistoryDto>>(
            _tenant, _paths, request.PathId, ct);
        if (error is not null)
        {
            return error;
        }

        var result = new List<KnowledgePathReviewHistoryDto>();
        foreach (var revision in (await _revisions.ListByPathAsync(tenantId, path!.Id, ct)).OrderByDescending(r => r.RevisionNumber))
        {
            var instanceId = revision.ReviewRound.WorkflowInstanceId;
            IReadOnlyList<WorkflowHistoryEntry>? entries;
            try
            {
                entries = await _decisions.GetInstanceHistoryAsync(instanceId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                entries = null;
            }

            result.Add(new KnowledgePathReviewHistoryDto(revision.Id, revision.RevisionNumber, instanceId, entries is not null,
                (entries ?? Array.Empty<WorkflowHistoryEntry>()).OrderBy(e => e.SequenceNo).Select(e =>
                    new KnowledgePathReviewHistoryEntryDto(e.Action, e.StepCode, e.StepName, e.Comment, e.ActorId,
                        e.ActorDisplay, e.ReasonCode, e.OccurredAt)).ToList()));
        }

        return Response<IReadOnlyList<KnowledgePathReviewHistoryDto>>.Success(result);
    }
}
