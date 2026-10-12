using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Review;

// WP-KP-2 — a chain-bound knowledge path is approved ONLY through a MOD-0023 MLR round (KP-MLR-{country}), the claims
// v2 pattern (WP-CL-BE-3/3a/4): CRM starts the round with the CALLER's token, keeps the round on a frozen revision and
// applies the outcome in ONE place (KnowledgePathRevisionOutcomeApplier) — reached from the completion-event consumer,
// from reconcile-on-read and from withdraw. A decision is taken on the SAME MOD-0023 task from either channel (the
// path's reviewer view through CRM, or the Work Center) — one channel, with the reviewer's comment (K1).

/// <summary>WP-KP-2 — shared constants of the path review flow.</summary>
public static class KnowledgePathReviewRules
{
    public const string ObjectType = "crm.knowledge-path-revision";
    public const string SubmitReasonCode = "CRM_KNOWLEDGE_PATH_SUBMITTED";
    public const string WithdrawReasonCode = "CRM_KNOWLEDGE_PATH_WITHDRAWN";
    public const string ApproveReasonCode = "CRM_KNOWLEDGE_PATH_APPROVED";
    public const string RejectReasonCode = "CRM_KNOWLEDGE_PATH_REJECTED";
    public const int MaxCommentLength = 2000;
    public const int MaxNoteLength = 2000;

    public static string ObjectRef(Guid revisionId) => $"crm/knowledge-path-revision/{revisionId:D}";

    public static string IdempotencyKey(Guid pathId, int revisionNumber) => $"crm:knowledge-path:{pathId:D}:r{revisionNumber}";

    /// <summary>The reviewer view of a revision (KP-UI-2). One place: the deep link of the MOD-0023 work item.</summary>
    public static string ReviewLink(Guid pathId, Guid revisionId) => $"/CRM/KnowledgePaths/{pathId:D}/Review/{revisionId:D}";

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static Response<T> ApprovalViaWorkflowOnly<T>() => Fail<T>(ClaimErrorCodes.ApprovalViaWorkflowOnly,
        "A chain-bound knowledge path is approved only through its MLR review workflow; use submit-review.", 409);

    public static bool SamePerson(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
           && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>WP-KP-2 — workflow configuration (<c>Crm:KnowledgePaths:Workflow</c>). Template codes are configuration; the
/// templates themselves are created by KP-2-CFG.</summary>
public interface IKnowledgePathReviewSettings
{
    /// <summary>The MLR template of a path; <c>{0}</c> = the path country (default <c>KP-MLR-{0}</c>).</summary>
    string TemplateCodeFormat { get; }

    /// <summary>An open round older than this is re-checked against MOD-0023 on read (default 120 s).</summary>
    int ReconcileAfterSeconds { get; }
}

public static class KnowledgePathReviewDefaults
{
    public const string TemplateCodeFormat = "KP-MLR-{0}";
    public const int ReconcileAfterSeconds = 120;
}

/// <summary>One transition row of a MOD-0023 instance (Platform <c>instances/{id}/history</c>): step NAME + comment.</summary>
public sealed record WorkflowHistoryEntry(
    long SequenceNo,
    string Action,
    string? ActorId,
    string? ActorDisplay,
    string? StepCode,
    string? StepName,
    string? Comment,
    string? ReasonCode,
    DateTimeOffset OccurredAt);

public sealed record WorkflowDecisionResult(ClaimWorkflowCallOutcome Outcome, string? Detail);

/// <summary>
/// WP-KP-2 — the MOD-0023 calls a reviewer's decision needs, beside <see cref="IClaimWorkflowClient"/> (start, batch
/// status, task list, cancel). Same Gateway, same CALLER's token: <c>tasks/mine</c> is the caller's assignable open
/// tasks, approve / reject carry the caller's comment, history returns the step names and comments.
/// </summary>
public interface IWorkflowDecisionClient
{
    /// <summary>The caller's actionable tasks (candidate or assignee). Null when MOD-0023 cannot be reached.</summary>
    Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct);

    Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
        string idempotencyKey, string? comment, CancellationToken ct);

    /// <summary>The instance's transition history. Null when MOD-0023 cannot be reached.</summary>
    Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct);
}

/// <summary>
/// WP-KP-2 — the ONE place a review round's outcome changes a revision and its path. Reached by the completion event
/// consumer, reconcile-on-read and withdraw; safe to repeat: an outcome lands only on the revision's OPEN round whose
/// workflow instance id matches (a replayed, late or forged event for any other instance is ignored).
/// <list type="bullet">
/// <item><c>approved</c> → revision approved, path <c>approved</c> (release is KP-3);</item>
/// <item><c>rejected</c> / <c>cancelled</c> / <c>timed-out</c> → revision closed, path back to <c>draft</c>.</item>
/// </list>
/// The path moves only while it is <c>review</c> (a later lifecycle change — inactive / archived — is never undone). The
/// path is written FIRST: if the revision write then fails the round is still open and the next read / event re-applies
/// (the path move is idempotent). Repositories take the tenant explicitly (the consumer has no HTTP tenant).
/// </summary>
public sealed class KnowledgePathRevisionOutcomeApplier
{
    private const string WorkflowActor = "workflow:mod-0023";

    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IKnowledgePathRepository _paths;
    private readonly ILogger<KnowledgePathRevisionOutcomeApplier> _logger;

    public KnowledgePathRevisionOutcomeApplier(
        IKnowledgePathRevisionRepository revisions, IKnowledgePathRepository paths,
        ILogger<KnowledgePathRevisionOutcomeApplier>? logger = null)
    {
        _revisions = revisions;
        _paths = paths;
        _logger = logger ?? NullLogger<KnowledgePathRevisionOutcomeApplier>.Instance;
    }

    public async Task<ClaimReviewApplyResult> ApplyAsync(Guid tenantId, Guid revisionId, Guid workflowInstanceId,
        string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
    {
        if (!ClaimReviewOutcomes.IsValid(outcome))
        {
            return ClaimReviewApplyResult.UnknownOutcome;
        }

        var revision = await _revisions.GetByIdAsync(tenantId, revisionId, ct);
        if (revision is null)
        {
            return ClaimReviewApplyResult.NotFound;
        }

        if (!revision.IsOpen() || revision.ReviewRound.WorkflowInstanceId != workflowInstanceId)
        {
            _logger.LogInformation(
                "knowledge_path.review.outcome_ignored RevisionId={RevisionId} Instance={Instance} Open={Open}",
                revisionId, workflowInstanceId, revision.ReviewRound.WorkflowInstanceId);
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        await MovePathAsync(tenantId, revision.PathId, outcome == ClaimReviewOutcomes.Approved, ct);

        var round = revision.ReviewRound;
        round.Outcome = outcome;
        round.ClosedAt = completedAt;
        round.CompletedBy = completedBy;
        round.ReasonCode = reasonCode;
        revision.Status = KnowledgePathRevisionStatuses.FromOutcome(outcome);
        revision.UpdatedAt = DateTimeOffset.UtcNow;
        revision.UpdatedBy = WorkflowActor;
        if (!await _revisions.ReplaceAsync(revision, revision.Version, ct))
        {
            _logger.LogWarning("knowledge_path.review.revision_conflict RevisionId={RevisionId}", revisionId);
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        return ClaimReviewApplyResult.Applied;
    }

    private async Task MovePathAsync(Guid tenantId, Guid pathId, bool approved, CancellationToken ct)
    {
        // One retry on an optimistic conflict (an author may be editing the draft at the same moment).
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var path = await _paths.GetByIdAsync(tenantId, pathId, ct);
            if (path is null || path.IsArchived()
                || !string.Equals(path.PathStatus, KnowledgePathStatuses.Review, StringComparison.Ordinal))
            {
                return;
            }

            path.PathStatus = approved ? KnowledgePathStatuses.Approved : KnowledgePathStatuses.Draft;
            path.UpdatedAt = DateTimeOffset.UtcNow;
            path.UpdatedBy = WorkflowActor;
            if (await _paths.ReplaceAsync(path, path.Version, ct))
            {
                return;
            }
        }

        _logger.LogWarning("knowledge_path.review.path_conflict PathId={PathId}", pathId);
    }
}

/// <summary>
/// WP-KP-2 — reconcile-on-read: an open round older than the window is checked against MOD-0023's batch status read
/// (caller's token) and a finished one is applied through the applier. A MOD-0023 problem never breaks the read.
/// </summary>
public sealed class KnowledgePathReviewReconciler
{
    private readonly IClaimWorkflowClient _client;
    private readonly KnowledgePathRevisionOutcomeApplier _applier;
    private readonly IKnowledgePathReviewSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<KnowledgePathReviewReconciler> _logger;

    public KnowledgePathReviewReconciler(IClaimWorkflowClient client, KnowledgePathRevisionOutcomeApplier applier,
        IKnowledgePathReviewSettings? settings = null, TimeProvider? clock = null,
        ILogger<KnowledgePathReviewReconciler>? logger = null)
    {
        _client = client;
        _applier = applier;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<KnowledgePathReviewReconciler>.Instance;
    }

    /// <summary>Returns how many revisions changed (the caller reloads when &gt; 0).</summary>
    public async Task<int> ReconcileAsync(Guid tenantId, IEnumerable<KnowledgePathRevision> revisions, CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().AddSeconds(
            -(_settings?.ReconcileAfterSeconds ?? KnowledgePathReviewDefaults.ReconcileAfterSeconds));
        var due = revisions.Where(r => r.IsOpen() && r.ReviewRound.SubmittedAt <= cutoff).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        try
        {
            var applied = 0;
            foreach (var chunk in due.Chunk(100))
            {
                var states = await _client.GetInstancesByObjectsAsync(
                    KnowledgePathReviewRules.ObjectType, chunk.Select(r => r.Id.ToString("D")).ToList(), ct);
                if (states is null)
                {
                    return applied;
                }

                foreach (var revision in chunk)
                {
                    var state = states.TryGetValue(revision.Id.ToString("D"), out var instances)
                        ? instances.FirstOrDefault(i => i.WorkflowInstanceId == revision.ReviewRound.WorkflowInstanceId)
                        : null;
                    if (state?.Outcome is not { } outcome || !ClaimReviewOutcomes.IsValid(outcome))
                    {
                        continue;
                    }

                    if (await _applier.ApplyAsync(tenantId, revision.Id, revision.ReviewRound.WorkflowInstanceId, outcome,
                            completedBy: null, reasonCode: null, state.CompletedAt ?? _clock.GetUtcNow(), ct)
                        == ClaimReviewApplyResult.Applied)
                    {
                        applied++;
                    }
                }
            }

            return applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "knowledge_path.review.reconcile_failed");
            return 0;
        }
    }
}
