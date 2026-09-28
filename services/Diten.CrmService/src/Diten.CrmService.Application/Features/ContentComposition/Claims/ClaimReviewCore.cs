using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

// WP-CL-BE-4 — claims are approved ONLY through a MOD-0023 workflow round. CRM starts the round with the CALLER's
// token (Platform records the submitter; its SoD rule "the submitter cannot approve" then works), keeps the rounds on
// the record, and applies the round's outcome in ONE place (ClaimReviewOutcomeApplier) — reached from the completion
// event consumer and from reconcile-on-read alike.

/// <summary>WP-CL-BE-4 — shared constants and fixed answers of the approval flow.</summary>
public static class ClaimReviewRules
{
    public const string ClaimObjectType = "crm.claim";
    public const string CountryVersionObjectType = "crm.claim-country-version";
    public const string SubmitReasonCode = "CRM_CLAIM_SUBMITTED";
    public const string WithdrawReasonCode = "CRM_CLAIM_WITHDRAWN";

    public static string ObjectRef(string objectType, Guid id) => objectType == ClaimObjectType
        ? $"crm/claim/{id:D}"
        : $"crm/claim-country-version/{id:D}";

    public static string IdempotencyKey(string objectType, Guid id, int roundNo) => $"crm:{objectType}:{id:D}:r{roundNo}";

    public static Response<T> InReviewLocked<T>() => Response<T>.Fail(
        new[] { ClaimErrorCodes.InReviewLocked, "The record is under review; withdraw the review to change it." }, 409);

    public static Response<T> ApprovalViaWorkflowOnly<T>() => Response<T>.Fail(
        new[]
        {
            ClaimErrorCodes.ApprovalViaWorkflowOnly,
            "Claims are approved only through their MOD-0023 approval workflow; use submit-review."
        }, 409);

    public static ClaimReviewRound? OpenRound(IEnumerable<ClaimReviewRound> rounds) =>
        rounds.LastOrDefault(r => r.IsOpen());

    public static int NextRoundNo(IEnumerable<ClaimReviewRound> rounds) =>
        rounds.Select(r => r.RoundNo).DefaultIfEmpty(0).Max() + 1;

    public static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>WP-CL-BE-4 — workflow configuration (<c>Crm:Claims:Workflow</c>). Template codes are configuration, never
/// compiled in; the templates themselves are created by CL-CFG-1.</summary>
public interface IClaimWorkflowSettings
{
    /// <summary>Template of a core claim round (default <c>CLAIM-CORE-MLR</c>).</summary>
    string CoreTemplateCode { get; }

    /// <summary>Template of a country (or local) round; <c>{0}</c> = country code (default <c>CLAIM-LOCAL-MLR-{0}</c>).</summary>
    string LocalTemplateCodeFormat { get; }

    /// <summary>An open round older than this is re-checked against MOD-0023 on read (default 120 s).</summary>
    int ReconcileAfterSeconds { get; }
}

public static class ClaimWorkflowDefaults
{
    public const string CoreTemplateCode = "CLAIM-CORE-MLR";
    public const string LocalTemplateCodeFormat = "CLAIM-LOCAL-MLR-{0}";
    public const int ReconcileAfterSeconds = 120;
}

public enum ClaimWorkflowCallOutcome
{
    Ok,
    TemplateMissing,
    Forbidden,
    Unavailable,
    Rejected,
    NotFound
}

public sealed record ClaimWorkflowDisplayContext(
    string Title, string? Subtitle, string SourceModule, string DeepLinkUrl, IReadOnlyList<string> Chips);

public sealed record ClaimWorkflowStartRequest(
    string TemplateCode,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string IdempotencyKey,
    ClaimWorkflowDisplayContext DisplayContext);

public sealed record ClaimWorkflowStartResult(ClaimWorkflowCallOutcome Outcome, Guid? WorkflowInstanceId, string? Detail);

/// <summary>One MOD-0023 instance as the batch status read reports it.</summary>
public sealed record ClaimWorkflowInstanceState(
    Guid WorkflowInstanceId, string Status, string? Outcome, DateTimeOffset? CompletedAt);

/// <summary>One MOD-0023 approval task (a step row). Platform exposes no comment on this read.</summary>
public sealed record ClaimWorkflowTaskState(
    Guid TaskId,
    Guid WorkflowInstanceId,
    string StageCode,
    string StepCode,
    string Status,
    string? AssigneeRef,
    string? ActionedBy,
    string? ActionReasonCode,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt)
{
    /// <summary>A task MOD-0023 still waits on.</summary>
    public bool IsOpen => Status is "WaitingApproval" or "WaitingEvidence" or "Escalated";
}

/// <summary>
/// WP-CL-BE-4 — CRM's view of the MOD-0023 cross-service contract (WP-CL-BE-3 consumer guide), reached through the
/// Gateway with the CALLER's <c>Authorization</c> + <c>X-Tenant-Id</c>. Never a service token: a service identity would
/// become the submitter and silently switch SoD off.
/// </summary>
public interface IClaimWorkflowClient
{
    Task<ClaimWorkflowStartResult> StartAsync(ClaimWorkflowStartRequest request, CancellationToken ct);

    /// <summary>Instances per object id (≤100 ids per call). Null when MOD-0023 cannot be reached.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>?> GetInstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken ct);

    /// <summary>The approval tasks of these instances. MOD-0023's instance detail carries no tasks, so this reads the
    /// tenant task list and filters it. Null when MOD-0023 cannot be reached.</summary>
    Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetTasksAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct);

    Task<ClaimWorkflowCallOutcome> CancelTaskAsync(
        Guid taskId, string actorId, string reasonCode, string idempotencyKey, CancellationToken ct);
}

public enum ClaimReviewApplyResult
{
    Applied,
    NotFound,
    NoMatchingOpenRound,
    UnknownOutcome
}

/// <summary>
/// WP-CL-BE-4 — the ONE place a review round's outcome changes a claim or a country version. Reached by the completion
/// event consumer, by reconcile-on-read and by withdraw; all three are safe to repeat: an outcome only lands on the
/// record's OPEN round whose workflow instance id matches, so a replayed, late or forged event for any other instance
/// is ignored.
/// <list type="bullet">
/// <item><c>approved</c> → approved. A core/local claim: every other approved record of the code → inactive and, when
/// one was superseded, that code's approved country versions → review-required (moved here from the old direct
/// approve). A country version: the previous approved / review-required version of the same country → inactive.</item>
/// <item><c>rejected</c> / <c>cancelled</c> / <c>timed-out</c> → draft, the round keeps the outcome and reason.</item>
/// </list>
/// Repositories are addressed with the tenant id explicitly, so the applier works with no HTTP tenant context (the
/// consumer).
/// </summary>
public sealed class ClaimReviewOutcomeApplier
{
    private const string WorkflowActor = "workflow:mod-0023";

    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly ILogger<ClaimReviewOutcomeApplier> _logger;

    public ClaimReviewOutcomeApplier(
        IClaimRepository claims,
        IClaimCountryVersionRepository countryVersions,
        IContentCompositionAuditPublisher? audit = null,
        ILogger<ClaimReviewOutcomeApplier>? logger = null)
    {
        _claims = claims;
        _countryVersions = countryVersions;
        _audit = audit;
        _logger = logger ?? NullLogger<ClaimReviewOutcomeApplier>.Instance;
    }

    public async Task<ClaimReviewApplyResult> ApplyAsync(
        Guid tenantId,
        string objectType,
        Guid objectId,
        Guid workflowInstanceId,
        string outcome,
        string? completedBy,
        string? reasonCode,
        DateTimeOffset completedAt,
        CancellationToken ct)
    {
        if (!ClaimReviewOutcomes.IsValid(outcome))
        {
            return ClaimReviewApplyResult.UnknownOutcome;
        }

        return objectType switch
        {
            ClaimReviewRules.ClaimObjectType => await ApplyToClaimAsync(
                tenantId, objectId, workflowInstanceId, outcome, completedBy, reasonCode, completedAt, ct),
            ClaimReviewRules.CountryVersionObjectType => await ApplyToCountryVersionAsync(
                tenantId, objectId, workflowInstanceId, outcome, completedBy, reasonCode, completedAt, ct),
            _ => ClaimReviewApplyResult.NotFound
        };
    }

    private async Task<ClaimReviewApplyResult> ApplyToClaimAsync(Guid tenantId, Guid claimId, Guid instanceId,
        string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
    {
        var claim = await _claims.GetByIdAsync(tenantId, claimId, ct);
        if (claim is null)
        {
            return ClaimReviewApplyResult.NotFound;
        }

        var round = ClaimReviewRules.OpenRound(claim.ReviewRounds);
        if (round is null || round.WorkflowInstanceId != instanceId)
        {
            LogIgnored(ClaimReviewRules.ClaimObjectType, claimId, instanceId, round?.WorkflowInstanceId);
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        var now = DateTimeOffset.UtcNow;
        Close(round, outcome, completedBy, reasonCode, completedAt);
        claim.UpdatedAt = now;
        claim.UpdatedBy = WorkflowActor;
        if (outcome == ClaimReviewOutcomes.Approved)
        {
            claim.Status = ClaimStatuses.Approved;
            claim.ApprovedAt = completedAt;
            claim.ApprovedBy = completedBy ?? WorkflowActor;
            await _claims.UpdateAsync(claim, ct);
            await PublishAsync(ClaimReasonCodes.Approved, tenantId, ContentCompositionAuditEntities.Claim, claim.Id,
                claim.Version, claim.ClaimCode, ct);
            await PropagateCoreApprovalAsync(tenantId, claim, now, ct);
        }
        else
        {
            claim.Status = ClaimStatuses.Draft;
            await _claims.UpdateAsync(claim, ct);
        }

        await PublishAsync(ClaimReasonCodes.ReviewOutcomeApplied, tenantId, ContentCompositionAuditEntities.Claim,
            claim.Id, claim.Version, $"{claim.ClaimCode}|r{round.RoundNo}|{outcome}", ct);
        return ClaimReviewApplyResult.Applied;
    }

    /// <summary>WP-CL-BE-1 rule, moved here: the other approved records of the code → inactive; when one was
    /// superseded, the code's approved country versions bound to an older core → review-required (ApprovedAt kept).</summary>
    private async Task PropagateCoreApprovalAsync(Guid tenantId, Claim approved, DateTimeOffset now, CancellationToken ct)
    {
        var superseded = 0;
        foreach (var other in await _claims.ListByCodeAsync(tenantId, approved.ClaimCode, ct))
        {
            // WP-CL-BE-5 — a record turned review-required by its evidence is replaced the same way as an approved one.
            if (other.Id == approved.Id || other.IsArchived()
                || !(other.IsApproved() || other.Status == ClaimStatuses.ReviewRequired))
            {
                continue;
            }

            other.Status = ClaimStatuses.Inactive;
            other.UpdatedAt = now;
            other.UpdatedBy = WorkflowActor;
            await _claims.UpdateAsync(other, ct);
            superseded++;
        }

        if (superseded == 0 && approved.SupersedesClaimId is null)
        {
            return;
        }

        var flagged = 0;
        foreach (var version in await _countryVersions.ListByClaimCodeAsync(tenantId, approved.ClaimCode, ct))
        {
            if (version.Status != ClaimStatuses.Approved || version.IsArchived()
                || string.Equals(version.BoundCoreVersion, approved.ClaimVersion, StringComparison.Ordinal))
            {
                continue;
            }

            version.Status = ClaimStatuses.ReviewRequired;
            version.UpdatedAt = now;
            version.UpdatedBy = WorkflowActor;
            await _countryVersions.UpdateAsync(version, ct);
            flagged++;
        }

        if (flagged > 0)
        {
            await PublishAsync(ClaimReasonCodes.CountryVersionsReviewRequired, tenantId,
                ContentCompositionAuditEntities.Claim, approved.Id, approved.Version,
                $"{approved.ClaimCode}|count={flagged}", ct);
        }
    }

    private async Task<ClaimReviewApplyResult> ApplyToCountryVersionAsync(Guid tenantId, Guid versionId,
        Guid instanceId, string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt,
        CancellationToken ct)
    {
        var version = await _countryVersions.GetByIdAsync(tenantId, versionId, ct);
        if (version is null)
        {
            return ClaimReviewApplyResult.NotFound;
        }

        var round = ClaimReviewRules.OpenRound(version.ReviewRounds);
        if (round is null || round.WorkflowInstanceId != instanceId)
        {
            LogIgnored(ClaimReviewRules.CountryVersionObjectType, versionId, instanceId, round?.WorkflowInstanceId);
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        var now = DateTimeOffset.UtcNow;
        Close(round, outcome, completedBy, reasonCode, completedAt);
        version.UpdatedAt = now;
        version.UpdatedBy = WorkflowActor;
        if (outcome == ClaimReviewOutcomes.Approved)
        {
            foreach (var previous in await _countryVersions.ListByClaimCodeAsync(tenantId, version.ClaimCode, ct))
            {
                if (previous.Id == version.Id || previous.IsArchived()
                    || !string.Equals(previous.CountryCode, version.CountryCode, StringComparison.OrdinalIgnoreCase)
                    || previous.Status is not (ClaimStatuses.Approved or ClaimStatuses.ReviewRequired))
                {
                    continue;
                }

                previous.Status = ClaimStatuses.Inactive;
                previous.UpdatedAt = now;
                previous.UpdatedBy = WorkflowActor;
                await _countryVersions.UpdateAsync(previous, ct);
            }

            version.Status = ClaimStatuses.Approved;
            version.ApprovedAt = completedAt;
            version.ApprovedBy = completedBy ?? WorkflowActor;
            await _countryVersions.UpdateAsync(version, ct);
            await ClaimCountryVersionAudit.PublishAsync(_audit, ClaimReasonCodes.CountryVersionApproved, tenantId,
                version, ct);
        }
        else
        {
            version.Status = ClaimStatuses.Draft;
            await _countryVersions.UpdateAsync(version, ct);
        }

        await PublishAsync(ClaimReasonCodes.ReviewOutcomeApplied, tenantId,
            ContentCompositionAuditEntities.ClaimCountryVersion, version.Id, version.Version,
            $"{version.ClaimCode}|{version.CountryCode}|r{round.RoundNo}|{outcome}", ct);
        return ClaimReviewApplyResult.Applied;
    }

    private static void Close(ClaimReviewRound round, string outcome, string? completedBy, string? reasonCode,
        DateTimeOffset completedAt)
    {
        round.Outcome = outcome;
        round.ClosedAt = completedAt;
        round.CompletedBy = string.IsNullOrWhiteSpace(completedBy) ? null : completedBy;
        round.ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode;
    }

    private Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid id, int version, string detail,
        CancellationToken ct)
        => _audit is null ? Task.CompletedTask : _audit.PublishAsync(eventName, tenantId, entityType, id, version, detail, ct);

    private void LogIgnored(string objectType, Guid objectId, Guid instanceId, Guid? openInstanceId)
        => _logger.LogInformation(
            "claims.review.outcome_ignored ObjectType={ObjectType} ObjectId={ObjectId} WorkflowInstanceId={InstanceId} "
            + "OpenRoundInstanceId={OpenInstanceId}", objectType, objectId, instanceId, openInstanceId);
}

/// <summary>
/// WP-CL-BE-4 — reconcile-on-read: CRM has no background sweeper, so a list / detail / coverage read re-checks every
/// open round older than <see cref="IClaimWorkflowSettings.ReconcileAfterSeconds"/> against MOD-0023's batch status
/// read (the caller's token, ≤100 ids per call) and applies terminal outcomes through the applier. MOD-0023 being
/// unreachable never breaks the read: the round simply stays open until the next read or the event.
/// </summary>
public sealed class ClaimReviewReconciler
{
    private const int BatchSize = 100;

    private readonly IClaimWorkflowClient _client;
    private readonly ClaimReviewOutcomeApplier _applier;
    private readonly IClaimWorkflowSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<ClaimReviewReconciler> _logger;

    public ClaimReviewReconciler(
        IClaimWorkflowClient client,
        ClaimReviewOutcomeApplier applier,
        IClaimWorkflowSettings? settings = null,
        TimeProvider? clock = null,
        ILogger<ClaimReviewReconciler>? logger = null)
    {
        _client = client;
        _applier = applier;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<ClaimReviewReconciler>.Instance;
    }

    /// <summary>Returns how many records changed (the caller reloads when &gt; 0).</summary>
    public async Task<int> ReconcileAsync(
        Guid tenantId, IEnumerable<Claim> claims, IEnumerable<ClaimCountryVersion> versions, CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().AddSeconds(
            -(_settings?.ReconcileAfterSeconds ?? ClaimWorkflowDefaults.ReconcileAfterSeconds));

        var pendingClaims = Due(claims.Where(c => c.Status == ClaimStatuses.InReview)
            .Select(c => (c.Id, ClaimReviewRules.OpenRound(c.ReviewRounds))), cutoff);
        var pendingVersions = Due(versions.Where(v => v.Status == ClaimStatuses.InReview)
            .Select(v => (v.Id, ClaimReviewRules.OpenRound(v.ReviewRounds))), cutoff);
        if (pendingClaims.Count == 0 && pendingVersions.Count == 0)
        {
            return 0;
        }

        var applied = 0;
        applied += await ReconcileTypeAsync(tenantId, ClaimReviewRules.ClaimObjectType, pendingClaims, ct);
        applied += await ReconcileTypeAsync(tenantId, ClaimReviewRules.CountryVersionObjectType, pendingVersions, ct);
        return applied;
    }

    private static List<(Guid Id, ClaimReviewRound Round)> Due(
        IEnumerable<(Guid Id, ClaimReviewRound? Round)> rows, DateTimeOffset cutoff)
    {
        var due = new List<(Guid Id, ClaimReviewRound Round)>();
        foreach (var (id, round) in rows)
        {
            if (round is not null && round.SubmittedAt <= cutoff)
            {
                due.Add((id, round));
            }
        }

        return due;
    }

    private async Task<int> ReconcileTypeAsync(Guid tenantId, string objectType,
        IReadOnlyList<(Guid Id, ClaimReviewRound Round)> pending, CancellationToken ct)
    {
        var applied = 0;
        foreach (var chunk in pending.Chunk(BatchSize))
        {
            IReadOnlyDictionary<string, IReadOnlyList<ClaimWorkflowInstanceState>>? states;
            try
            {
                states = await _client.GetInstancesByObjectsAsync(
                    objectType, chunk.Select(x => x.Id.ToString("D")).ToList(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "claims.review.reconcile_failed ObjectType={ObjectType}", objectType);
                return applied;
            }

            if (states is null)
            {
                _logger.LogInformation("claims.review.reconcile_unavailable ObjectType={ObjectType}", objectType);
                return applied;
            }

            foreach (var (id, round) in chunk)
            {
                if (!states.TryGetValue(id.ToString("D"), out var instances))
                {
                    continue;
                }

                var state = instances.FirstOrDefault(i => i.WorkflowInstanceId == round.WorkflowInstanceId);
                if (state?.Outcome is not { } outcome || !ClaimReviewOutcomes.IsValid(outcome))
                {
                    continue;
                }

                var result = await _applier.ApplyAsync(tenantId, objectType, id, round.WorkflowInstanceId, outcome,
                    completedBy: null, reasonCode: null, state.CompletedAt ?? _clock.GetUtcNow(), ct);
                if (result == ClaimReviewApplyResult.Applied)
                {
                    applied++;
                }
            }
        }

        return applied;
    }
}

/// <summary>WP-CL-BE-4 — the read handlers' guard around reconcile-on-read: never lets a MOD-0023 problem break a read.
/// Returns true when anything changed (the caller reloads).</summary>
internal static class ClaimReadReconcile
{
    public static async Task<bool> RunAsync(ClaimReviewReconciler? reconciler, Guid tenantId,
        IEnumerable<Claim> claims, IEnumerable<ClaimCountryVersion> versions, CancellationToken ct)
    {
        if (reconciler is null)
        {
            return false;
        }

        try
        {
            return await reconciler.ReconcileAsync(tenantId, claims, versions, ct) > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}
