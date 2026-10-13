using Diten.MdmService.Domain.Entities;
using Diten.Shared.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Features.Approvals;

// WP-VW-W4 (W4-BE-b1) — the MDM copy of the CrmService approval core (Features/Approvals/WorkflowApprovalCore.cs, W3-BE-c),
// NOT a shared library. A record is approved through ONE MOD-0023 round at a time, in the Work Center: the Gateway client
// with the caller's token (never a service token), the round / decision shapes, the outcome event inbox routed by
// ObjectType ("mdm.*"), reconcile-on-read. The separation of duties is enforced in the outcome applier too (Platform only
// stops the submitter): an approval by the author / last editor / submitter sends the record back to draft with a reason
// code instead of applying it.
// What differs from the CRM copy: the applier is not wired to concrete repositories — each approvable kind registers a
// WorkflowApprovalTarget<T> (its store, its draft status, its "approve" action, an optional extra refusal); W4-BE-b2
// registers the competitor proposal decision. In b1 only a test-only approvable proves the core.

/// <summary>Workflow configuration (<c>Mdm:CompetitorProposals:Workflow:TemplateCode</c>,
/// <c>Mdm:Workflow:ReconcileAfterSeconds</c>). Template codes are configuration; W4-CFG creates the template.</summary>
public interface IMdmApprovalSettings
{
    string CompetitorProposalTemplateCode { get; }

    /// <summary>An open round older than this is re-checked against MOD-0023 on read (default 120 s).</summary>
    int ReconcileAfterSeconds { get; }
}

public static class MdmApprovalDefaults
{
    public const string CompetitorProposalTemplateCode = "MDM-COMP-PROPOSAL";
    public const int ReconcileAfterSeconds = 120;
    public const string SourceModule = "MOD-0290";
}

/// <summary>What differs between the approvable kinds (object type, reason codes, the Work Center link).</summary>
public sealed record WorkflowApprovalKind(string ObjectType, string ReasonPrefix, Func<Guid, string> Link)
{
    /// <summary>W4-BE-b2 — the competitor proposal decision (§2.6, <c>requestedObjectType = mdm.competitor-proposal</c>).</summary>
    public static readonly WorkflowApprovalKind CompetitorProposal = new(
        "mdm.competitor-proposal", "MDM_COMPETITOR_PROPOSAL", id => $"/MDM/CompetitorProducts/Proposals/{id:D}");

    public string ObjectRef(Guid id) => $"{ObjectType.Replace("mdm.", "mdm/", StringComparison.Ordinal)}/{id:D}";

    public string IdempotencyKey(Guid id, int roundNo) => $"{ObjectType}:{id:D}:r{roundNo}";

    public string SubmitReason => ReasonPrefix + "_SUBMITTED";
    public string WithdrawReason => ReasonPrefix + "_WITHDRAWN";
    public string ApproveReason => ReasonPrefix + "_APPROVED";
    public string ReturnReason => ReasonPrefix + "_RETURNED";
}

/// <summary>The coded failures of the approval core (<c>[code, message]</c>; the CRM codes, same words).</summary>
public static class WorkflowApprovalErrors
{
    /// <summary>409 — the kind's MOD-0023 template does not exist (or is not published).</summary>
    public const string TemplateMissing = "workflow_template_missing";
    public const string ApprovalForbidden = "approval_forbidden";
    public const string WorkflowUnavailable = "workflow_unavailable";
    public const string WorkflowRequestRejected = "workflow_request_rejected";
    public const string NoOpenReview = "no_open_review";
    public const string WithdrawNotPossible = "withdraw_not_possible";
    public const string DecisionInvalid = "decision_invalid";
    public const string RejectionCommentRequired = "rejection_comment_required";
    public const string TextTooLong = "text_too_long";

    /// <summary>409 — the optimistic version check (CAS) failed: someone else wrote the record.</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";

    public const int CommentMaxLength = 2_000;
}

/// <summary>Load / optimistic replace of one approvable kind. The applier runs without an HTTP tenant (the event
/// consumer): the tenant is EXPLICIT on every call, and the replace succeeds only on the expected version (CAS).</summary>
public interface IWorkflowApprovalStore<T> where T : EntityBase, IWorkflowApprovable
{
    Task<T?> GetAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Replaces when the stored version still equals <paramref name="expectedVersion"/>; the record's version
    /// becomes <c>expectedVersion + 1</c>. False (and the version unchanged) on a lost race.</summary>
    Task<bool> ReplaceAsync(T record, int expectedVersion, CancellationToken ct);
}

public static class WorkflowApprovalRules
{
    public const string WorkflowActor = "workflow:mod-0023";

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool SamePerson(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
           && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The author, the last editor or the submitter of the open (else latest) round — never its decider.</summary>
    public static bool IsOwn(IWorkflowApprovable record, string? actor)
        => SamePerson(actor, record.CreatedBy)
           || SamePerson(actor, record.UpdatedBy)
           || SamePerson(actor, (record.OpenRound() ?? record.CurrentRound())?.SubmittedBy);

    public static Response<T> FromStart<T>(WorkflowStartResult result) => result.Outcome switch
    {
        WorkflowCallOutcome.TemplateMissing => Fail<T>(WorkflowApprovalErrors.TemplateMissing,
            "The approval workflow template is not available (not created or not published).", 409),
        WorkflowCallOutcome.Forbidden => Fail<T>(WorkflowApprovalErrors.ApprovalForbidden,
            "You are not allowed to start the approval workflow.", 403),
        WorkflowCallOutcome.Rejected => Fail<T>(WorkflowApprovalErrors.WorkflowRequestRejected,
            $"The approval workflow refused the request: {result.Detail}", 400),
        _ => Unavailable<T>()
    };

    public static Response<T> Unavailable<T>() => Fail<T>(WorkflowApprovalErrors.WorkflowUnavailable,
        "The approval workflow service cannot be reached. Nothing was changed.", 503);

    public static Response<T> Conflict<T>() => Fail<T>(WorkflowApprovalErrors.ConcurrencyConflict,
        "The record was modified by another writer; reload and retry.", 409);
}

/// <summary>
/// The Work Center records its own action as a machine reason code (<c>WORKCENTER_APPROVE</c>, …). Such a code is not a
/// person's note: it is neither stored as a round's reason / a decision's comment nor shown.
/// </summary>
public static class WorkflowApprovalNotes
{
    private static readonly System.Text.RegularExpressions.Regex MachineCode =
        new("^WORKCENTER_[A-Z_]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool IsMachineCode(string? value) => value is not null && MachineCode.IsMatch(value.Trim());

    public static string? Clean(string? value) => IsMachineCode(value) ? null : value;
}

/// <summary>A review round / a decision on the wire.</summary>
public sealed record ApprovalRoundDto(
    int RoundNo, string? SubmittedBy, DateTimeOffset SubmittedAt, string? Outcome, DateTimeOffset? ClosedAt,
    string? CompletedBy, string? ReasonCode, string? TemplateCode)
{
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
/// One approvable kind as the applier sees it: its store, its draft status, what "approved" does, and an optional extra
/// refusal checked at approval time (beside the SoD, which is always checked). W4-BE-b2 registers the competitor proposal.
/// </summary>
public abstract class WorkflowApprovalTarget
{
    public abstract WorkflowApprovalKind Kind { get; }

    internal abstract Task<WorkflowApplyResult> ApplyAsync(WorkflowApprovalOutcomeApplier applier, Guid tenantId, Guid id,
        Guid workflowInstanceId, string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt,
        CancellationToken ct);
}

public sealed class WorkflowApprovalTarget<T> : WorkflowApprovalTarget where T : EntityBase, IWorkflowApprovable
{
    public WorkflowApprovalTarget(WorkflowApprovalKind kind, IWorkflowApprovalStore<T> store, string draftStatus, string sodCode,
        Action<T, string, DateTimeOffset> approve, Func<T, string, CancellationToken, Task<string?>>? extraRefusal = null)
    {
        Kind = kind;
        Store = store;
        DraftStatus = draftStatus;
        SodCode = sodCode;
        Approve = approve;
        ExtraRefusal = extraRefusal;
    }

    public override WorkflowApprovalKind Kind { get; }
    public IWorkflowApprovalStore<T> Store { get; }
    public string DraftStatus { get; }
    public string SodCode { get; }
    public Action<T, string, DateTimeOffset> Approve { get; }
    public Func<T, string, CancellationToken, Task<string?>>? ExtraRefusal { get; }

    /// <summary>The SoD first (author / last editor / submitter), then the kind's own check.</summary>
    public async Task<string?> RefusalAsync(T record, string approver, CancellationToken ct)
        => WorkflowApprovalRules.IsOwn(record, approver)
            ? SodCode
            : ExtraRefusal is null ? null : await ExtraRefusal(record, approver, ct);

    internal override Task<WorkflowApplyResult> ApplyAsync(WorkflowApprovalOutcomeApplier applier, Guid tenantId, Guid id,
        Guid workflowInstanceId, string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt,
        CancellationToken ct)
        => applier.ApplyCoreAsync(this, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt, ct);
}

/// <summary>
/// Submit / withdraw / decide on the SAME MOD-0023 task the Work Center shows. The state change of a decision arrives as
/// the round's outcome (event / reconcile) through <see cref="WorkflowApprovalOutcomeApplier"/>.
/// </summary>
public sealed class WorkflowApprovalFlow
{
    private readonly IMdmWorkflowClient _workflow;
    private readonly IMdmActorContext _actor;
    private readonly WorkflowApprovalOutcomeApplier _applier;

    public WorkflowApprovalFlow(IMdmWorkflowClient workflow, IMdmActorContext actor, WorkflowApprovalOutcomeApplier applier)
    {
        _workflow = workflow;
        _actor = actor;
        _applier = applier;
    }

    public string? Actor => _actor.ActorName;

    /// <summary>Starts the round (caller's token, idempotency <c>{type}:{id}:r{round}</c>) and moves the record to
    /// <paramref name="inReviewStatus"/>. MOD-0023 down ⇒ 503, nothing changed. Null = success.</summary>
    public async Task<Response<TOut>?> SubmitAsync<T, TOut>(IWorkflowApprovalStore<T> store, T record, WorkflowApprovalKind kind,
        string templateCode, WorkflowDisplayContext display, string inReviewStatus, CancellationToken ct)
        where T : EntityBase, IWorkflowApprovable
    {
        if (record.OpenRound() is not null)
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.WorkflowRequestRejected, "An approval round is already open.", 409);
        }

        var roundNo = record.NextRoundNo();
        var start = await _workflow.StartAsync(new WorkflowStartRequest(
            templateCode, kind.ObjectType, record.Id.ToString("D"), kind.ObjectRef(record.Id),
            kind.IdempotencyKey(record.Id, roundNo), display, kind.SubmitReason), ct);
        if (start.Outcome != WorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return WorkflowApprovalRules.FromStart<TOut>(start);
        }

        var now = DateTimeOffset.UtcNow;
        var current = record;
        // One retry on an optimistic conflict with a concurrent edit (MOD-0023 already holds the round).
        for (var attempt = 0; attempt < 2 && current is not null; attempt++)
        {
            current.ReviewRounds.Add(new WorkflowApprovalRound
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
    public async Task<Response<TOut>?> WithdrawAsync<T, TOut>(T record, WorkflowApprovalKind kind, CancellationToken ct)
        where T : EntityBase, IWorkflowApprovable
    {
        if (record.OpenRound() is not { } round)
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.NoOpenReview, "There is no open approval to withdraw.", 409);
        }

        var actor = _actor.ActorName;
        if (!WorkflowApprovalRules.SamePerson(actor, round.SubmittedBy))
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.ApprovalForbidden,
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
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.WithdrawNotPossible,
                "The workflow has no waiting step to cancel (it may have just finished).", 409);
        }

        var cancel = await _workflow.CancelTaskAsync(open.TaskId, actor!, kind.WithdrawReason,
            $"{kind.IdempotencyKey(record.Id, round.RoundNo)}:withdraw", ct);
        switch (cancel)
        {
            case WorkflowCallOutcome.Ok:
                break;
            case WorkflowCallOutcome.Forbidden:
                return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.ApprovalForbidden, "You are not allowed to cancel this approval.", 403);
            case WorkflowCallOutcome.Unavailable:
                return WorkflowApprovalRules.Unavailable<TOut>();
            default:
                return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.WithdrawNotPossible,
                    "The approval workflow refused to cancel the waiting step.", 409);
        }

        await _applier.ApplyAsync(record.TenantId, kind.ObjectType, record.Id, round.WorkflowInstanceId,
            WorkflowApprovalOutcomes.Cancelled, actor, kind.WithdrawReason, DateTimeOffset.UtcNow, ct);
        return null;
    }

    /// <summary>
    /// A decision on the SAME MOD-0023 task the Work Center shows: <c>approve</c> | <c>return</c> (a return needs a comment);
    /// the author / last editor / submitter never decides (403 <paramref name="sodCode"/>). The decision (with its comment)
    /// is recorded; the state change arrives as the outcome.
    /// </summary>
    public async Task<Response<TOut>?> DecideAsync<T, TOut>(IWorkflowApprovalStore<T> store, T record, WorkflowApprovalKind kind,
        string? decision, string? comment, string sodCode, CancellationToken ct) where T : EntityBase, IWorkflowApprovable
    {
        var normalized = decision?.Trim().ToLowerInvariant();
        if (normalized is not (WorkflowApprovalDecisions.Approve or WorkflowApprovalDecisions.Return))
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.DecisionInvalid, "Decision must be 'approve' or 'return'.", 400);
        }

        var text = WorkflowApprovalRules.Clean(comment);
        if (normalized == WorkflowApprovalDecisions.Return && text is null)
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.RejectionCommentRequired,
                "Sending back to draft needs a comment (the reason the author will act on).", 400);
        }

        if (text is { Length: > WorkflowApprovalErrors.CommentMaxLength })
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.TextTooLong,
                $"The comment cannot exceed {WorkflowApprovalErrors.CommentMaxLength} characters.", 400);
        }

        if (record.OpenRound() is not { } round)
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.NoOpenReview, "This record has no open approval.", 409);
        }

        var actor = _actor.ActorName;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.ApprovalForbidden, "The caller's identity is required to decide.", 403);
        }

        if (WorkflowApprovalRules.IsOwn(record, actor))
        {
            return WorkflowApprovalRules.Fail<TOut>(sodCode,
                "The author, the last editor or the submitter cannot decide on it (separation of duties).", 403);
        }

        var mine = await _workflow.GetMyTasksAsync(ct);
        if (mine is null)
        {
            return WorkflowApprovalRules.Unavailable<TOut>();
        }

        var task = mine.FirstOrDefault(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen);
        if (task is null)
        {
            // Not a candidate: call the instance's open task anyway so MOD-0023's own 403 is returned as is.
            var all = await _workflow.GetTasksAsync([round.WorkflowInstanceId], ct);
            if (all is null)
            {
                return WorkflowApprovalRules.Unavailable<TOut>();
            }

            task = all.FirstOrDefault(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen);
            if (task is null)
            {
                return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.NoOpenReview, "The approval has no waiting step (it may have just finished).", 409);
            }
        }

        var approve = normalized == WorkflowApprovalDecisions.Approve;
        var result = await _workflow.DecideTaskAsync(task.TaskId, approve, actor,
            approve ? kind.ApproveReason : kind.ReturnReason,
            $"{kind.IdempotencyKey(record.Id, round.RoundNo)}:{task.TaskId:D}:{normalized}", text, ct);
        switch (result.Outcome)
        {
            case WorkflowCallOutcome.Ok:
                break;
            case WorkflowCallOutcome.Forbidden:
                return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.ApprovalForbidden, result.Detail ?? "You are not a reviewer of this step.", 403);
            case WorkflowCallOutcome.NotFound:
                return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.NoOpenReview, "The approval step is no longer waiting.", 409);
            case WorkflowCallOutcome.Rejected:
                return WorkflowApprovalRules.Fail<TOut>(WorkflowApprovalErrors.WorkflowRequestRejected,
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
                current.Decisions.Add(new WorkflowApprovalDecision
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

    /// <summary>A return decided in the Work Center arrives without its comment: it is read from the instance's history
    /// for the read only (fail-open — a history problem never breaks a read).</summary>
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
                var history = await _workflow.GetInstanceHistoryAsync(round.WorkflowInstanceId, ct);
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
/// The ONE place a round's outcome changes an approvable MDM record (event consumer routed by ObjectType, reconcile-on-read,
/// withdraw). Safe to repeat: an outcome lands only on the record's OPEN round whose workflow instance id matches.
/// <list type="bullet">
/// <item><c>approved</c> → the MDM side checks first: an approval by the author / last editor / submitter → draft + the
/// kind's <c>…_sod</c> code; the kind's own refusal → draft + its code; else the kind's approve action;</item>
/// <item><c>rejected</c> / <c>cancelled</c> / <c>timed-out</c> → draft, the decision kept.</item>
/// </list>
/// An approval whose approver is unknown (a reconcile that could not read the actioning task) is NOT applied — the
/// completion event (which names the approver) applies it; the SoD is never skipped.
/// </summary>
public sealed class WorkflowApprovalOutcomeApplier
{
    private readonly IReadOnlyDictionary<string, WorkflowApprovalTarget> _targets;
    private readonly ILogger<WorkflowApprovalOutcomeApplier> _logger;

    public WorkflowApprovalOutcomeApplier(IEnumerable<WorkflowApprovalTarget> targets, ILogger<WorkflowApprovalOutcomeApplier>? logger = null)
    {
        _targets = targets.ToDictionary(t => t.Kind.ObjectType, StringComparer.Ordinal);
        _logger = logger ?? NullLogger<WorkflowApprovalOutcomeApplier>.Instance;
    }

    /// <summary>An MDM workflow object type (<c>mdm.*</c>) — the consumer's routing filter.</summary>
    public static bool Handles(string? objectType) => objectType is not null && objectType.StartsWith("mdm.", StringComparison.Ordinal);

    /// <summary>A registered approvable kind for this object type.</summary>
    public bool CanApply(string? objectType) => objectType is not null && _targets.ContainsKey(objectType);

    public Task<WorkflowApplyResult> ApplyAsync(Guid tenantId, string objectType, Guid id, Guid workflowInstanceId,
        string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
        => _targets.TryGetValue(objectType, out var target)
            ? target.ApplyAsync(this, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt, ct)
            : Task.FromResult(WorkflowApplyResult.NotFound);

    internal async Task<WorkflowApplyResult> ApplyCoreAsync<T>(WorkflowApprovalTarget<T> target, Guid tenantId, Guid id,
        Guid workflowInstanceId, string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt,
        CancellationToken ct) where T : EntityBase, IWorkflowApprovable
    {
        if (!WorkflowApprovalOutcomes.IsValid(outcome))
        {
            return WorkflowApplyResult.UnknownOutcome;
        }

        if (outcome == WorkflowApprovalOutcomes.Approved && string.IsNullOrWhiteSpace(completedBy))
        {
            _logger.LogInformation("mdm.approval.outcome_deferred Id={Id} Instance={Instance} reason=approver_unknown", id, workflowInstanceId);
            return WorkflowApplyResult.NoMatchingOpenRound;
        }

        // One retry on an optimistic conflict (the decision endpoint may be recording its comment at the same moment).
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var record = await target.Store.GetAsync(tenantId, id, ct);
            if (record is null)
            {
                return WorkflowApplyResult.NotFound;
            }

            var round = record.OpenRound();
            if (round is null || round.WorkflowInstanceId != workflowInstanceId)
            {
                _logger.LogInformation("mdm.approval.outcome_ignored Id={Id} Instance={Instance}", id, workflowInstanceId);
                return WorkflowApplyResult.NoMatchingOpenRound;
            }

            round.Outcome = outcome;
            round.ClosedAt = completedAt;
            round.CompletedBy = completedBy;
            round.ReasonCode = WorkflowApprovalNotes.Clean(reasonCode);
            var decision = outcome switch
            {
                WorkflowApprovalOutcomes.Approved => WorkflowApprovalDecisions.Approve,
                WorkflowApprovalOutcomes.Rejected => WorkflowApprovalDecisions.Return,
                _ => null
            };
            if (decision is not null && record.Decisions.All(d => d.RoundNo != round.RoundNo))
            {
                record.Decisions.Add(new WorkflowApprovalDecision { RoundNo = round.RoundNo, By = completedBy, Outcome = decision, At = completedAt });
            }

            record.UpdatedAt = DateTimeOffset.UtcNow;
            if (outcome == WorkflowApprovalOutcomes.Approved && await target.RefusalAsync(record, completedBy!, ct) is { } refused)
            {
                // The MDM side refuses the approval: back to draft, the reason on the round.
                round.ReasonCode = refused;
                record.Status = target.DraftStatus;
                _logger.LogWarning("mdm.approval.outcome_refused Id={Id} Instance={Instance} reason={Reason}", id, workflowInstanceId, refused);
            }
            else if (outcome == WorkflowApprovalOutcomes.Approved)
            {
                target.Approve(record, completedBy!, completedAt);
            }
            else
            {
                record.Status = target.DraftStatus;
            }

            if (await target.Store.ReplaceAsync(record, record.Version, ct))
            {
                return WorkflowApplyResult.Applied;
            }
        }

        _logger.LogWarning("mdm.approval.apply_conflict Id={Id}", id);
        return WorkflowApplyResult.NoMatchingOpenRound;
    }
}

/// <summary>
/// Reconcile-on-read: an open round older than the window is checked against MOD-0023's batch status read (caller's token);
/// a finished one is applied through the applier. The approver of an approved round is read from the actioning task (the
/// SoD needs it); a MOD-0023 problem never breaks a read.
/// </summary>
public sealed class WorkflowApprovalReconciler
{
    private readonly IMdmWorkflowClient _client;
    private readonly WorkflowApprovalOutcomeApplier _applier;
    private readonly IMdmApprovalSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<WorkflowApprovalReconciler> _logger;

    public WorkflowApprovalReconciler(IMdmWorkflowClient client, WorkflowApprovalOutcomeApplier applier,
        IMdmApprovalSettings? settings = null, TimeProvider? clock = null, ILogger<WorkflowApprovalReconciler>? logger = null)
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
        var cutoff = _clock.GetUtcNow().AddSeconds(-(_settings?.ReconcileAfterSeconds ?? MdmApprovalDefaults.ReconcileAfterSeconds));
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
                    if (state?.Outcome is not { } outcome || !WorkflowApprovalOutcomes.IsValid(outcome))
                    {
                        continue;
                    }

                    string? approver = null;
                    if (outcome == WorkflowApprovalOutcomes.Approved)
                    {
                        var tasks = await _client.GetTasksAsync([round.WorkflowInstanceId], ct);
                        approver = tasks?.Where(t => t.WorkflowInstanceId == round.WorkflowInstanceId && !string.IsNullOrWhiteSpace(t.ActionedBy))
                            .OrderByDescending(t => t.CompletedAt ?? DateTimeOffset.MinValue)
                            .Select(t => t.ActionedBy)
                            .FirstOrDefault();
                    }

                    if (await _applier.ApplyAsync(tenantId, kind.ObjectType, record.Id, round.WorkflowInstanceId, outcome,
                            approver, reasonCode: null, state.CompletedAt ?? _clock.GetUtcNow(), ct) == WorkflowApplyResult.Applied)
                    {
                        applied++;
                    }
                }
            }

            return applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "mdm.approval.reconcile_failed Kind={Kind}", kind.ObjectType);
            return 0;
        }
    }
}
