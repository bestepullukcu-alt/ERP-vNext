using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Approvals;

// WP-VW-W3 (W3-BE-c, §0) — the objection answer and the time-boxed evidence exception are approved through ONE MOD-0023
// round at a time, in the Work Center, exactly as the KP-5a regulatory texts are (RegulatoryTextLifecycle /
// RegulatoryTextCore): the SAME Gateway clients (caller's token — never a service token), the SAME round / decision
// shapes, the SAME outcome event inbox routed by ObjectType, reconcile-on-read. What is NEW: the separation of duties is
// enforced in the outcome applier too (Platform only stops the submitter): an approval by the author / last editor /
// submitter sends the record back to draft with a reason code instead of approving it.

/// <summary>WP-VW-W3 (W3-BE-c) — workflow configuration (<c>Crm:ObjectionResponses:Workflow:TemplateCode</c>,
/// <c>Crm:VisitEvidenceExceptions:Workflow:TemplateCode</c>). Template codes are configuration; W3-CFG creates them.</summary>
public interface IAuthoringApprovalSettings
{
    string ObjectionResponseTemplateCode { get; }
    string VisitEvidenceExceptionTemplateCode { get; }

    /// <summary>An open round older than this is re-checked against MOD-0023 on read (default 120 s).</summary>
    int ReconcileAfterSeconds { get; }
}

public static class AuthoringApprovalDefaults
{
    public const string ObjectionResponseTemplateCode = "CRM-OBJ-RESP";
    public const string VisitEvidenceExceptionTemplateCode = "CRM-VISIT-EVIDENCE-EXC";
    public const int ReconcileAfterSeconds = 120;
}

/// <summary>What differs between the approvable kinds (object type, reason codes, the Work Center link).</summary>
public sealed record WorkflowApprovalKind(string ObjectType, string ReasonPrefix, Func<Guid, string> Link)
{
    public static readonly WorkflowApprovalKind ObjectionResponse = new(
        "crm.objection-response", "CRM_OBJECTION_RESPONSE", id => $"/CRM/ObjectionResponses/{id:D}");

    public static readonly WorkflowApprovalKind VisitEvidenceException = new(
        "crm.visit-evidence-exception", "CRM_VISIT_EVIDENCE_EXCEPTION",
        id => $"/CRM/VisitEvidenceSettings?tab=exceptions&id={id:D}");

    public string ObjectRef(Guid id) => $"{ObjectType.Replace("crm.", "crm/", StringComparison.Ordinal)}/{id:D}";

    public string IdempotencyKey(Guid id, int roundNo) => $"{ObjectType}:{id:D}:r{roundNo}";

    public string SubmitReason => ReasonPrefix + "_SUBMITTED";
    public string WithdrawReason => ReasonPrefix + "_WITHDRAWN";
    public string ApproveReason => ReasonPrefix + "_APPROVED";
    public string ReturnReason => ReasonPrefix + "_RETURNED";
}

public static class WorkflowApprovalErrors
{
    /// <summary>409 — the kind's MOD-0023 template does not exist (or is not published).</summary>
    public const string TemplateMissing = "workflow_template_missing";
}

/// <summary>Load / optimistic replace of one approvable kind (the applier works without an HTTP tenant: explicit tenant).</summary>
public interface IWorkflowApprovalStore<T> where T : EntityBase, IWorkflowApprovable
{
    Task<T?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<bool> ReplaceAsync(T record, int expectedVersion, CancellationToken ct);
}

public static class WorkflowApprovalRules
{
    public const string WorkflowActor = "workflow:mod-0023";

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The author, the last editor or the submitter of the open (else latest) round — never its decider.</summary>
    public static bool IsOwn(IWorkflowApprovable record, string? actor)
        => KnowledgePathReviewRules.SamePerson(actor, record.CreatedBy)
           || KnowledgePathReviewRules.SamePerson(actor, record.UpdatedBy)
           || KnowledgePathReviewRules.SamePerson(actor, (record.OpenRound() ?? record.CurrentRound())?.SubmittedBy);

    public static Response<T> FromStart<T>(ClaimWorkflowStartResult result) => result.Outcome switch
    {
        ClaimWorkflowCallOutcome.TemplateMissing => Fail<T>(WorkflowApprovalErrors.TemplateMissing,
            "The approval workflow template is not available (not created or not published).", 409),
        ClaimWorkflowCallOutcome.Forbidden => Fail<T>(ClaimErrorCodes.ApprovalForbidden,
            "You are not allowed to start the approval workflow.", 403),
        ClaimWorkflowCallOutcome.Rejected => Fail<T>(ClaimErrorCodes.WorkflowRequestRejected,
            $"The approval workflow refused the request: {result.Detail}", 400),
        _ => Unavailable<T>()
    };

    public static Response<T> Unavailable<T>() => Fail<T>(ClaimErrorCodes.WorkflowUnavailable,
        "The approval workflow service cannot be reached. Nothing was changed.", 503);

    public static Response<T> Conflict<T>() => Response<T>.Fail("The record was modified by another writer; reload and retry.", 409);
}

/// <summary>
/// WP-VW-W3-FIX (F3) — the Work Center records its own action as a machine reason code (<c>WORKCENTER_APPROVE</c>,
/// <c>WORKCENTER_REJECT</c>, … — Platform <c>WorkflowApprovalWorkItemActionDispatcher</c>). Such a code is not a person's
/// note: it is neither stored as a round's reason / a decision's comment nor shown. A real comment (a return note) and the
/// CRM's own refusal codes (<c>…_sod</c>, <c>…_overlap</c>, <c>objection_claim_not_approved</c>) are kept.
/// </summary>
public static class WorkflowApprovalNotes
{
    private static readonly System.Text.RegularExpressions.Regex MachineCode =
        new("^WORKCENTER_[A-Z_]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool IsMachineCode(string? value) => value is not null && MachineCode.IsMatch(value.Trim());

    /// <summary>The value, or null when it is a Work Center machine code.</summary>
    public static string? Clean(string? value) => IsMachineCode(value) ? null : value;
}

/// <summary>A review round / a decision on the wire.</summary>
public sealed record ApprovalRoundDto(
    int RoundNo, string? SubmittedBy, DateTimeOffset SubmittedAt, string? Outcome, DateTimeOffset? ClosedAt,
    string? CompletedBy, string? ReasonCode, string? TemplateCode)
{
    // A machine code stored before W3-FIX is filtered on the read too.
    public static IReadOnlyList<ApprovalRoundDto> From(IWorkflowApprovable record)
        => record.ReviewRounds.OrderBy(r => r.RoundNo)
            .Select(r => new ApprovalRoundDto(r.RoundNo, r.SubmittedBy, r.SubmittedAt, r.Outcome, r.ClosedAt, r.CompletedBy,
                WorkflowApprovalNotes.Clean(r.ReasonCode), r.TemplateCode))
            .ToList();
}

public sealed record ApprovalDecisionDto(int RoundNo, string? By, string Outcome, string? Comment, DateTimeOffset At)
{
    public static IReadOnlyList<ApprovalDecisionDto> From(IWorkflowApprovable record)
        => record.Decisions.OrderBy(d => d.RoundNo).ThenBy(d => d.At)
            .Select(d => new ApprovalDecisionDto(d.RoundNo, d.By, d.Outcome, WorkflowApprovalNotes.Clean(d.Comment), d.At))
            .ToList();
}

/// <summary>
/// WP-VW-W3 (W3-BE-c) — submit / withdraw / decide on the SAME MOD-0023 task the Work Center shows (KP-5a
/// <c>RegulatoryTextLifecycle</c> 180–430, generic over the kind). The state change of a decision arrives as the round's
/// outcome (event / reconcile) through <see cref="WorkflowApprovalOutcomeApplier"/>.
/// </summary>
public sealed class WorkflowApprovalFlow
{
    private readonly IClaimWorkflowClient _workflow;
    private readonly IWorkflowDecisionClient _decisions;
    private readonly IActorContext _actor;
    private readonly WorkflowApprovalOutcomeApplier _applier;

    public WorkflowApprovalFlow(IClaimWorkflowClient workflow, IWorkflowDecisionClient decisions, IActorContext actor,
        WorkflowApprovalOutcomeApplier applier)
    {
        _workflow = workflow;
        _decisions = decisions;
        _actor = actor;
        _applier = applier;
    }

    public string? Actor => _actor.ActorName;

    /// <summary>Starts the round (caller's token) and moves the record to <c>in_review</c>. MOD-0023 down ⇒ 503, nothing changed.</summary>
    public async Task<Response<TOut>?> SubmitAsync<T, TOut>(IWorkflowApprovalStore<T> store, T record, WorkflowApprovalKind kind,
        string templateCode, ClaimWorkflowDisplayContext display, string inReviewStatus, CancellationToken ct)
        where T : EntityBase, IWorkflowApprovable
    {
        var roundNo = record.NextRoundNo();
        var start = await _workflow.StartAsync(new ClaimWorkflowStartRequest(
            templateCode, kind.ObjectType, record.Id.ToString("D"), kind.ObjectRef(record.Id),
            kind.IdempotencyKey(record.Id, roundNo), display, kind.SubmitReason), ct);
        if (start.Outcome != ClaimWorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return WorkflowApprovalRules.FromStart<TOut>(start);
        }

        var now = DateTimeOffset.UtcNow;
        var current = record;
        // One retry on an optimistic conflict with a concurrent edit (MOD-0023 already holds the round).
        for (var attempt = 0; attempt < 2 && current is not null; attempt++)
        {
            current.ReviewRounds.Add(new ClaimReviewRound
            {
                WorkflowInstanceId = instanceId, SubmittedAt = now, SubmittedBy = _actor.ActorName, RoundNo = roundNo,
                TemplateCode = templateCode
            });
            current.Status = inReviewStatus;
            current.UpdatedAt = now;
            if (await store.ReplaceAsync(current, current.Version, ct))
            {
                return null;
            }

            current = await store.GetAsync(record.TenantId, record.Id, ct);
        }

        return WorkflowApprovalRules.Conflict<TOut>();
    }

    /// <summary>Cancels the open round (only its submitter); the record returns to draft through the applier.</summary>
    public async Task<Response<TOut>?> WithdrawAsync<T, TOut>(IWorkflowApprovalStore<T> store, T record, WorkflowApprovalKind kind,
        CancellationToken ct) where T : EntityBase, IWorkflowApprovable
    {
        if (record.OpenRound() is not { } round)
        {
            return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "There is no open approval to withdraw.", 409);
        }

        var actor = _actor.ActorName;
        if (!KnowledgePathReviewRules.SamePerson(actor, round.SubmittedBy))
        {
            return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden,
                "Only the person who sent the record for approval can withdraw it.", 403);
        }

        var tasks = await _workflow.GetTasksAsync([round.WorkflowInstanceId], ct);
        if (tasks is null)
        {
            return WorkflowApprovalRules.Unavailable<TOut>();
        }

        var open = tasks.Where(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen)
            .OrderByDescending(t => t.DueAt ?? DateTimeOffset.MinValue).FirstOrDefault();
        if (open is null)
        {
            return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.WithdrawNotPossible,
                "The workflow has no waiting step to cancel (it may have just finished).", 409);
        }

        var cancel = await _workflow.CancelTaskAsync(open.TaskId, actor!, kind.WithdrawReason,
            $"{kind.IdempotencyKey(record.Id, round.RoundNo)}:withdraw", ct);
        switch (cancel)
        {
            case ClaimWorkflowCallOutcome.Ok:
                break;
            case ClaimWorkflowCallOutcome.Forbidden:
                return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden, "You are not allowed to cancel this approval.", 403);
            case ClaimWorkflowCallOutcome.Unavailable:
                return WorkflowApprovalRules.Unavailable<TOut>();
            default:
                return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.WithdrawNotPossible,
                    "The approval workflow refused to cancel the waiting step.", 409);
        }

        await _applier.ApplyAsync(record.TenantId, kind.ObjectType, record.Id, round.WorkflowInstanceId,
            ClaimReviewOutcomes.Cancelled, actor, kind.WithdrawReason, DateTimeOffset.UtcNow, ct);
        return null;
    }

    /// <summary>
    /// A decision on the SAME MOD-0023 task the Work Center shows: <c>approve</c> | <c>return</c> (a return needs a
    /// comment, 400 <c>rejection_comment_required</c>); the author / last editor / submitter never decides (403
    /// <paramref name="sodCode"/>). The decision (with its comment) is recorded; the state change arrives as the outcome.
    /// </summary>
    public async Task<Response<TOut>?> DecideAsync<T, TOut>(IWorkflowApprovalStore<T> store, T record, WorkflowApprovalKind kind,
        string? decision, string? comment, string sodCode, CancellationToken ct) where T : EntityBase, IWorkflowApprovable
    {
        var normalized = decision?.Trim().ToLowerInvariant();
        if (normalized is not (WorkflowApprovalDecisions.Approve or WorkflowApprovalDecisions.Return))
        {
            return WorkflowApprovalRules.Fail<TOut>(RegulatoryTextErrors.DecisionInvalid, "Decision must be 'approve' or 'return'.", 400);
        }

        var text = WorkflowApprovalRules.Clean(comment);
        if (normalized == WorkflowApprovalDecisions.Return && text is null)
        {
            return WorkflowApprovalRules.Fail<TOut>(RegulatoryTextErrors.RejectionCommentRequired,
                "Sending back to draft needs a comment (the reason the author will act on).", 400);
        }

        if (text is { Length: > RegulatoryTextLimits.Comment })
        {
            return WorkflowApprovalRules.Fail<TOut>(RegulatoryTextErrors.TextTooLong,
                $"The comment cannot exceed {RegulatoryTextLimits.Comment} characters.", 400);
        }

        if (record.OpenRound() is not { } round)
        {
            return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "This record has no open approval.", 409);
        }

        var actor = _actor.ActorName;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden, "The caller's identity is required to decide.", 403);
        }

        if (WorkflowApprovalRules.IsOwn(record, actor))
        {
            return WorkflowApprovalRules.Fail<TOut>(sodCode,
                "The author, the last editor or the submitter cannot decide on it (separation of duties).", 403);
        }

        var mine = await _decisions.GetMyTasksAsync(ct);
        if (mine is null)
        {
            return WorkflowApprovalRules.Unavailable<TOut>();
        }

        var task = mine.FirstOrDefault(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen);
        if (task is null)
        {
            // Not a candidate: call the instance's open task anyway so MOD-0023's own 403 is returned as is (KP-2).
            var all = await _workflow.GetTasksAsync([round.WorkflowInstanceId], ct);
            if (all is null)
            {
                return WorkflowApprovalRules.Unavailable<TOut>();
            }

            task = all.FirstOrDefault(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen);
            if (task is null)
            {
                return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "The approval has no waiting step (it may have just finished).", 409);
            }
        }

        var approve = normalized == WorkflowApprovalDecisions.Approve;
        var result = await _decisions.DecideTaskAsync(task.TaskId, approve, actor,
            approve ? kind.ApproveReason : kind.ReturnReason,
            $"{kind.IdempotencyKey(record.Id, round.RoundNo)}:{task.TaskId:D}:{normalized}", text, ct);
        switch (result.Outcome)
        {
            case ClaimWorkflowCallOutcome.Ok:
                break;
            case ClaimWorkflowCallOutcome.Forbidden:
                return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.ApprovalForbidden, result.Detail ?? "You are not a reviewer of this step.", 403);
            case ClaimWorkflowCallOutcome.NotFound:
                return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.NoOpenReview, "The approval step is no longer waiting.", 409);
            case ClaimWorkflowCallOutcome.Rejected:
                return WorkflowApprovalRules.Fail<TOut>(ClaimErrorCodes.WorkflowRequestRejected,
                    $"The approval workflow refused the decision: {result.Detail}", 400);
            default:
                return WorkflowApprovalRules.Unavailable<TOut>();
        }

        // Record the decision with its comment (the outcome event may already have recorded it without one).
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var current = await store.GetAsync(record.TenantId, record.Id, ct);
            if (current is null)
            {
                break;
            }

            var existing = current.Decisions.FirstOrDefault(d => d.RoundNo == round.RoundNo);
            if (existing is null)
            {
                current.Decisions.Add(new RegulatoryTextDecision
                {
                    RoundNo = round.RoundNo, By = actor, Outcome = normalized, Comment = text, At = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.By ??= actor;
                existing.Comment ??= text;
            }

            if (await store.ReplaceAsync(current, current.Version, ct))
            {
                break;
            }
        }

        return null;
    }

    /// <summary>The caller's open task instance ids (one MOD-0023 read, only when a record is in review).</summary>
    public async Task<HashSet<Guid>> MyOpenInstancesAsync(IEnumerable<IWorkflowApprovable> records, CancellationToken ct)
        => records.Any(r => r.OpenRound() is not null) && await _decisions.GetMyTasksAsync(ct) is { } tasks
            ? tasks.Where(t => t.IsOpen).Select(t => t.WorkflowInstanceId).ToHashSet()
            : new HashSet<Guid>();

    /// <summary>
    /// A return decided in the Work Center arrives without its comment (the event carries none): the comment is read
    /// from the instance's MOD-0023 history for the read only (fail-open — a history problem never breaks a read).
    /// </summary>
    public async Task FillReturnCommentsAsync(IWorkflowApprovable record, CancellationToken ct)
    {
        foreach (var decision in record.Decisions.Where(d => d.Outcome == WorkflowApprovalDecisions.Return && d.Comment is null))
        {
            var round = record.ReviewRounds.FirstOrDefault(r => r.RoundNo == decision.RoundNo);
            if (round is null)
            {
                continue;
            }

            try
            {
                var history = await _decisions.GetInstanceHistoryAsync(round.WorkflowInstanceId, ct);
                var entry = history?.LastOrDefault(h => !string.IsNullOrWhiteSpace(h.Comment)
                                                        && !WorkflowApprovalNotes.IsMachineCode(h.Comment)
                                                        && h.Action.Contains("reject", StringComparison.OrdinalIgnoreCase));
                if (entry is not null)
                {
                    decision.Comment = entry.Comment;
                    decision.By ??= entry.ActorId;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // fail-open: the comment simply stays empty on this read
            }
        }
    }
}

/// <summary>
/// WP-VW-W3 (W3-BE-c) — the ONE place a round's outcome changes an objection answer or an evidence exception (event
/// consumer routed by ObjectType, reconcile-on-read, withdraw). Safe to repeat: an outcome lands only on the record's OPEN
/// round whose workflow instance id matches.
/// <list type="bullet">
/// <item><c>approved</c> → the CRM side checks first: an approval by the author / last editor / submitter → <c>draft</c>
/// + <c>…_sod</c>; an answer whose claims are no longer approved → <c>draft</c> + <c>objection_claim_not_approved</c>; an
/// exception that now overlaps another → <c>draft</c> + <c>visit_evidence_exception_overlap</c>; else approved;</item>
/// <item><c>rejected</c> / <c>cancelled</c> / <c>timed-out</c> → <c>draft</c>, the decision kept.</item>
/// </list>
/// An approval whose approver is unknown (a reconcile that could not read the actioning task) is NOT applied — the
/// completion event (which names the approver) applies it; the SoD is never skipped.
/// </summary>
public sealed class WorkflowApprovalOutcomeApplier
{
    private readonly Domain.Repositories.IObjectionResponseRepository _objections;
    private readonly Domain.Repositories.IVisitEvidenceExceptionRepository _exceptions;
    private readonly Domain.Repositories.IClaimRepository _claims;
    private readonly ILogger<WorkflowApprovalOutcomeApplier> _logger;

    public WorkflowApprovalOutcomeApplier(
        Domain.Repositories.IObjectionResponseRepository objections,
        Domain.Repositories.IVisitEvidenceExceptionRepository exceptions,
        Domain.Repositories.IClaimRepository claims,
        ILogger<WorkflowApprovalOutcomeApplier>? logger = null)
    {
        _objections = objections;
        _exceptions = exceptions;
        _claims = claims;
        _logger = logger ?? NullLogger<WorkflowApprovalOutcomeApplier>.Instance;
    }

    public static bool Handles(string? objectType)
        => objectType == WorkflowApprovalKind.ObjectionResponse.ObjectType
           || objectType == WorkflowApprovalKind.VisitEvidenceException.ObjectType;

    public IWorkflowApprovalStore<ObjectionResponse> Objections => new ObjectionResponseStore(_objections);

    public IWorkflowApprovalStore<VisitEvidenceException> Exceptions => new VisitEvidenceExceptionStore(_exceptions);

    public Task<ClaimReviewApplyResult> ApplyAsync(Guid tenantId, string objectType, Guid id, Guid workflowInstanceId,
        string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
        => objectType == WorkflowApprovalKind.ObjectionResponse.ObjectType
            ? ApplyAsync(Objections, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt,
                ObjectionResponseStatuses.Draft, ObjectionRefusalAsync, (r, by, at) =>
                {
                    r.Status = ObjectionResponseStatuses.Approved;
                    r.ApprovedAt = at;
                    r.ApprovedBy = by;
                }, ct)
            : objectType == WorkflowApprovalKind.VisitEvidenceException.ObjectType
                ? ApplyAsync(Exceptions, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt,
                    VisitEvidenceExceptionStatuses.Draft, ExceptionRefusalAsync, (r, by, at) =>
                    {
                        r.Status = VisitEvidenceExceptionStatuses.Approved;
                        r.ApprovedAt = at;
                        r.ApprovedBy = by;
                    }, ct)
                : Task.FromResult(ClaimReviewApplyResult.NotFound);

    private async Task<string?> ObjectionRefusalAsync(ObjectionResponse record, string approver, CancellationToken ct)
    {
        if (WorkflowApprovalRules.IsOwn(record, approver))
        {
            return ObjectionResponseErrors.SeparationOfDuties;
        }

        foreach (var claimId in record.ClaimIds)
        {
            var claim = await _claims.GetByIdAsync(record.TenantId, claimId, ct);
            if (claim is null || claim.TenantId != record.TenantId || !claim.IsApproved() || claim.IsArchived())
            {
                return ObjectionResponseErrors.ClaimNotApproved;
            }
        }

        return null;
    }

    private async Task<string?> ExceptionRefusalAsync(VisitEvidenceException record, string approver, CancellationToken ct)
    {
        // The creator / submitter never approves (the last editor of a draft is the creator or a co-editor: same rule).
        if (WorkflowApprovalRules.IsOwn(record, approver))
        {
            return VisitEvidenceExceptionErrors.SeparationOfDuties;
        }

        var others = await _exceptions.ListByResourceAsync(record.TenantId, record.ResourceId, ct);
        return VisitEvidence.VisitEvidenceExceptionChecks.Overlapping(record, others) is not null ? VisitEvidenceExceptionErrors.Overlap : null;
    }

    private async Task<ClaimReviewApplyResult> ApplyAsync<T>(IWorkflowApprovalStore<T> store, Guid tenantId, Guid id,
        Guid workflowInstanceId, string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt,
        string draftStatus, Func<T, string, CancellationToken, Task<string?>> refusal, Action<T, string, DateTimeOffset> approve,
        CancellationToken ct) where T : EntityBase, IWorkflowApprovable
    {
        if (!ClaimReviewOutcomes.IsValid(outcome))
        {
            return ClaimReviewApplyResult.UnknownOutcome;
        }

        if (outcome == ClaimReviewOutcomes.Approved && string.IsNullOrWhiteSpace(completedBy))
        {
            _logger.LogInformation("approval.outcome_deferred Id={Id} Instance={Instance} reason=approver_unknown", id, workflowInstanceId);
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        // One retry on an optimistic conflict (the decision endpoint may be recording its comment at the same moment).
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var record = await store.GetAsync(tenantId, id, ct);
            if (record is null)
            {
                return ClaimReviewApplyResult.NotFound;
            }

            var round = record.OpenRound();
            if (round is null || round.WorkflowInstanceId != workflowInstanceId)
            {
                _logger.LogInformation("approval.outcome_ignored Id={Id} Instance={Instance}", id, workflowInstanceId);
                return ClaimReviewApplyResult.NoMatchingOpenRound;
            }

            round.Outcome = outcome;
            round.ClosedAt = completedAt;
            round.CompletedBy = completedBy;
            round.ReasonCode = WorkflowApprovalNotes.Clean(reasonCode);
            var decision = outcome switch
            {
                ClaimReviewOutcomes.Approved => WorkflowApprovalDecisions.Approve,
                ClaimReviewOutcomes.Rejected => WorkflowApprovalDecisions.Return,
                _ => null
            };
            if (decision is not null && record.Decisions.All(d => d.RoundNo != round.RoundNo))
            {
                record.Decisions.Add(new RegulatoryTextDecision { RoundNo = round.RoundNo, By = completedBy, Outcome = decision, At = completedAt });
            }

            record.UpdatedAt = DateTimeOffset.UtcNow;
            if (outcome == ClaimReviewOutcomes.Approved
                && await refusal(record, completedBy!, ct) is { } refused)
            {
                // The CRM side refuses the approval: back to draft, the reason on the round.
                round.ReasonCode = refused;
                record.Status = draftStatus;
                _logger.LogWarning("approval.outcome_refused Id={Id} Instance={Instance} reason={Reason}", id, workflowInstanceId, refused);
            }
            else if (outcome == ClaimReviewOutcomes.Approved)
            {
                approve(record, completedBy!, completedAt);
            }
            else
            {
                record.Status = draftStatus;
            }

            if (await store.ReplaceAsync(record, record.Version, ct))
            {
                return ClaimReviewApplyResult.Applied;
            }
        }

        _logger.LogWarning("approval.apply_conflict Id={Id}", id);
        return ClaimReviewApplyResult.NoMatchingOpenRound;
    }

    private sealed class ObjectionResponseStore(Domain.Repositories.IObjectionResponseRepository repository)
        : IWorkflowApprovalStore<ObjectionResponse>
    {
        public Task<ObjectionResponse?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) => repository.GetByIdAsync(tenantId, id, ct);

        public Task<bool> ReplaceAsync(ObjectionResponse record, int expectedVersion, CancellationToken ct)
            => repository.ReplaceAsync(record, expectedVersion, ct);
    }

    private sealed class VisitEvidenceExceptionStore(Domain.Repositories.IVisitEvidenceExceptionRepository repository)
        : IWorkflowApprovalStore<VisitEvidenceException>
    {
        public Task<VisitEvidenceException?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) => repository.GetByIdAsync(tenantId, id, ct);

        public Task<bool> ReplaceAsync(VisitEvidenceException record, int expectedVersion, CancellationToken ct)
            => repository.ReplaceAsync(record, expectedVersion, ct);
    }
}

/// <summary>
/// WP-VW-W3 (W3-BE-c) — reconcile-on-read: an open round older than the window is checked against MOD-0023's batch status
/// read (caller's token); a finished one is applied through the applier. The approver of an approved round is read from
/// the actioning task (the SoD needs it); a MOD-0023 problem never breaks a read.
/// </summary>
public sealed class WorkflowApprovalReconciler
{
    private readonly IClaimWorkflowClient _client;
    private readonly WorkflowApprovalOutcomeApplier _applier;
    private readonly IAuthoringApprovalSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<WorkflowApprovalReconciler> _logger;

    public WorkflowApprovalReconciler(IClaimWorkflowClient client, WorkflowApprovalOutcomeApplier applier,
        IAuthoringApprovalSettings? settings = null, TimeProvider? clock = null, ILogger<WorkflowApprovalReconciler>? logger = null)
    {
        _client = client;
        _applier = applier;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<WorkflowApprovalReconciler>.Instance;
    }

    /// <summary>Returns how many records changed (the caller reloads when &gt; 0).</summary>
    public async Task<int> ReconcileAsync(Guid tenantId, WorkflowApprovalKind kind, IEnumerable<IWorkflowApprovable> records,
        CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().AddSeconds(-(_settings?.ReconcileAfterSeconds ?? AuthoringApprovalDefaults.ReconcileAfterSeconds));
        var due = records.Where(r => r.OpenRound() is { } round && round.SubmittedAt <= cutoff).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        try
        {
            var applied = 0;
            foreach (var chunk in due.Chunk(100))
            {
                var states = await _client.GetInstancesByObjectsAsync(kind.ObjectType, chunk.Select(r => r.Id.ToString("D")).ToList(), ct);
                if (states is null)
                {
                    return applied;
                }

                foreach (var record in chunk)
                {
                    var round = record.OpenRound()!;
                    var state = states.TryGetValue(record.Id.ToString("D"), out var instances)
                        ? instances.FirstOrDefault(i => i.WorkflowInstanceId == round.WorkflowInstanceId)
                        : null;
                    if (state?.Outcome is not { } outcome || !ClaimReviewOutcomes.IsValid(outcome))
                    {
                        continue;
                    }

                    string? approver = null;
                    if (outcome == ClaimReviewOutcomes.Approved)
                    {
                        var tasks = await _client.GetTasksAsync([round.WorkflowInstanceId], ct);
                        approver = tasks?.Where(t => t.WorkflowInstanceId == round.WorkflowInstanceId && !string.IsNullOrWhiteSpace(t.ActionedBy))
                            .OrderByDescending(t => t.CompletedAt ?? DateTimeOffset.MinValue)
                            .Select(t => t.ActionedBy)
                            .FirstOrDefault();
                    }

                    if (await _applier.ApplyAsync(tenantId, kind.ObjectType, record.Id, round.WorkflowInstanceId, outcome,
                            approver, reasonCode: null, state.CompletedAt ?? _clock.GetUtcNow(), ct) == ClaimReviewApplyResult.Applied)
                    {
                        applied++;
                    }
                }
            }

            return applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "approval.reconcile_failed Kind={Kind}", kind.ObjectType);
            return 0;
        }
    }
}
