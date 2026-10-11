using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Knowledge.Regulatory;

// WP-KP-5a — the safety text and the country legal profile are approved ONLY by Regulatory, through ONE MOD-0023 round
// (template KP-REG-{country}), the KP-2 pattern reused, not copied: the same Gateway clients (caller's token), the same
// claim review round shape, the same outcome event inbox routed by ObjectType, reconcile-on-read, person-based SoD.

/// <summary>WP-KP-5a — permission keys (PKS-001). Registered in the Auth catalog; granted to roles by KP-5a-CFG.</summary>
public static class SafetyTextPermissions
{
    public const string Read = "crm.safety-text.read";
    public const string Manage = "crm.safety-text.manage";
    public const string Submit = "crm.safety-text.submit";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Submit };
}

public static class CountryLegalProfilePermissions
{
    public const string Read = "crm.country-legal-profile.read";
    public const string Manage = "crm.country-legal-profile.manage";
    public const string Submit = "crm.country-legal-profile.submit";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Submit };
}

/// <summary>WP-KP-5a — workflow configuration (<c>Crm:RegulatoryTexts:Workflow</c>).</summary>
public interface IRegulatoryTextReviewSettings
{
    /// <summary>The Regulatory template; <c>{0}</c> = the country (default <c>KP-REG-{0}</c>).</summary>
    string TemplateCodeFormat { get; }

    /// <summary>An open round older than this is re-checked against MOD-0023 on read (default 120 s).</summary>
    int ReconcileAfterSeconds { get; }
}

public static class RegulatoryTextReviewDefaults
{
    public const string TemplateCodeFormat = "KP-REG-{0}";
    public const int ReconcileAfterSeconds = 120;
}

/// <summary>WP-KP-5a — what differs between the two kinds; everything else is the shared lifecycle.</summary>
public sealed record RegulatoryTextKind(
    string ObjectType,
    string CodePrefix,
    string LinkPrefix,
    string OpenDraftError,
    string MissingError,
    string TitleNoun)
{
    public static readonly RegulatoryTextKind SafetyText = new(
        "crm.safety-text", "SAF", "/CRM/SafetyTexts/", RegulatoryTextErrors.SafetyTextOpenDraftExists,
        RegulatoryTextErrors.SafetyTextMissing, "Güvenlilik metni onayı");

    public static readonly RegulatoryTextKind LegalProfile = new(
        "crm.country-legal-profile", "LGL", "/CRM/LegalProfiles/", RegulatoryTextErrors.LegalProfileOpenDraftExists,
        RegulatoryTextErrors.LegalProfileMissing, "Ülke yasal profili onayı");

    public string ReviewLink(Guid id) => $"{LinkPrefix}{id:D}";

    public string ObjectRef(Guid id) => $"{ObjectType.Replace("crm.", "crm/", StringComparison.Ordinal)}/{id:D}";

    public string IdempotencyKey(Guid id, int roundNo) => $"{ObjectType}:{id:D}:r{roundNo}";

    public string SubmitReason => ReasonPrefix + "_SUBMITTED";
    public string WithdrawReason => ReasonPrefix + "_WITHDRAWN";
    public string ApproveReason => ReasonPrefix + "_APPROVED";
    public string RejectReason => ReasonPrefix + "_REJECTED";

    private string ReasonPrefix => "CRM_" + ObjectType["crm.".Length..].Replace('-', '_').ToUpperInvariant();
}

public static class RegulatoryTextRules
{
    public const string WorkflowActor = "workflow:mod-0023";

    public static Response<T> Fail<T>(string code, string message, int status) => Response<T>.Fail(new[] { code, message }, status);

    public static string NormalizeCountry(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeLanguage(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Required / max-length text check (null = valid).</summary>
    public static (string Code, string Message)? CheckText(string field, string? value, int max, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? (RegulatoryTextErrors.TextRequired, $"{field} is required.") : null;
        }

        return value.Length > max ? (RegulatoryTextErrors.TextTooLong, $"{field} cannot exceed {max} characters.") : null;
    }

    /// <summary>The submitter or the author of the version — never its decider (person-based SoD).</summary>
    public static bool IsOwnVersion(RegulatoryText text, string? actor)
        => KnowledgePathReviewRules.SamePerson(actor, text.CreatedBy)
           || KnowledgePathReviewRules.SamePerson(actor, text.CurrentRound()?.SubmittedBy);

    public static Response<T> FromStart<T>(ClaimWorkflowStartResult result) => result.Outcome switch
    {
        ClaimWorkflowCallOutcome.TemplateMissing => Fail<T>(RegulatoryTextErrors.ReviewTemplateMissing,
            "The Regulatory approval workflow template of this country is not available (not created or not published).", 409),
        ClaimWorkflowCallOutcome.Forbidden => Fail<T>(ClaimErrorCodes.ApprovalForbidden,
            "You are not allowed to start the approval workflow.", 403),
        ClaimWorkflowCallOutcome.Rejected => Fail<T>(ClaimErrorCodes.WorkflowRequestRejected,
            $"The approval workflow refused the request: {result.Detail}", 400),
        _ => Unavailable<T>()
    };

    public static Response<T> Unavailable<T>() => Fail<T>(ClaimErrorCodes.WorkflowUnavailable,
        "The approval workflow service cannot be reached. Nothing was changed.", 503);
}

/// <summary>
/// WP-KP-5a — the ONE place a Regulatory round's outcome changes a text. Reached by the completion-event consumer
/// (routed by ObjectType), reconcile-on-read and withdraw; safe to repeat: an outcome lands only on the text's OPEN round
/// whose workflow instance id matches.
/// <list type="bullet">
/// <item><c>approved</c> → the key's current active → <c>superseded</c> FIRST (the unique active index), then this text
/// → <c>active</c>; a failed activation restores the previous active;</item>
/// <item><c>rejected</c> / <c>cancelled</c> / <c>timed-out</c> → back to <c>draft</c>, the decision kept.</item>
/// </list>
/// A decision taken in the Work Center reaches CRM only as this outcome: it is recorded without a comment (the CRM
/// decision endpoint records the comment itself).
/// </summary>
public sealed class RegulatoryTextOutcomeApplier
{
    private readonly ISafetyTextRepository _safetyTexts;
    private readonly ICountryLegalProfileRepository _profiles;
    private readonly ILogger<RegulatoryTextOutcomeApplier> _logger;

    public RegulatoryTextOutcomeApplier(ISafetyTextRepository safetyTexts, ICountryLegalProfileRepository profiles,
        ILogger<RegulatoryTextOutcomeApplier>? logger = null)
    {
        _safetyTexts = safetyTexts;
        _profiles = profiles;
        _logger = logger ?? NullLogger<RegulatoryTextOutcomeApplier>.Instance;
    }

    public static bool Handles(string? objectType)
        => objectType == RegulatoryTextKind.SafetyText.ObjectType || objectType == RegulatoryTextKind.LegalProfile.ObjectType;

    public Task<ClaimReviewApplyResult> ApplyAsync(Guid tenantId, string objectType, Guid id, Guid workflowInstanceId,
        string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
        => objectType == RegulatoryTextKind.SafetyText.ObjectType
            ? ApplyAsync<SafetyText>(_safetyTexts, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt, ct)
            : objectType == RegulatoryTextKind.LegalProfile.ObjectType
                ? ApplyAsync<CountryLegalProfile>(_profiles, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode,
                    completedAt, ct)
                : Task.FromResult(ClaimReviewApplyResult.NotFound);

    private async Task<ClaimReviewApplyResult> ApplyAsync<T>(IRegulatoryTextRepository<T> repository, Guid tenantId, Guid id,
        Guid workflowInstanceId, string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt,
        CancellationToken ct) where T : RegulatoryText
    {
        if (!ClaimReviewOutcomes.IsValid(outcome))
        {
            return ClaimReviewApplyResult.UnknownOutcome;
        }

        // One retry on an optimistic conflict (the decision endpoint may be recording its comment at the same moment).
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var text = await repository.GetByIdAsync(tenantId, id, ct);
            if (text is null)
            {
                return ClaimReviewApplyResult.NotFound;
            }

            var round = text.OpenRound();
            if (round is null || round.WorkflowInstanceId != workflowInstanceId || !text.IsInReview())
            {
                _logger.LogInformation("regulatory_text.review.outcome_ignored Id={Id} Instance={Instance}", id, workflowInstanceId);
                return ClaimReviewApplyResult.NoMatchingOpenRound;
            }

            var now = DateTimeOffset.UtcNow;
            round.Outcome = outcome;
            round.ClosedAt = completedAt;
            round.CompletedBy = completedBy;
            round.ReasonCode = reasonCode;
            RecordOutcomeDecision(text, round, outcome, completedBy, completedAt);
            text.UpdatedAt = now;
            text.UpdatedBy = RegulatoryTextRules.WorkflowActor;

            if (outcome != ClaimReviewOutcomes.Approved)
            {
                text.Status = RegulatoryTextStatuses.Draft;
                if (await repository.ReplaceAsync(text, text.Version, ct))
                {
                    return ClaimReviewApplyResult.Applied;
                }

                continue;
            }

            // Approved: supersede the key's current active FIRST, then activate (the unique active index).
            var previous = (await repository.ListAsync(tenantId, ct))
                .FirstOrDefault(r => r.Id != text.Id && r.IsActive() && r.Key() == text.Key());
            if (previous is not null)
            {
                previous.Status = RegulatoryTextStatuses.Superseded;
                previous.SupersededAt = now;
                previous.UpdatedAt = now;
                previous.UpdatedBy = RegulatoryTextRules.WorkflowActor;
                if (!await repository.ReplaceAsync(previous, previous.Version, ct))
                {
                    continue;
                }
            }

            text.Status = RegulatoryTextStatuses.Active;
            text.ActivatedAt = now;
            if (await repository.ReplaceAsync(text, text.Version, ct))
            {
                return ClaimReviewApplyResult.Applied;
            }

            // The activation lost a race: give the key its previous active back.
            if (previous is not null)
            {
                previous.Status = RegulatoryTextStatuses.Active;
                previous.SupersededAt = null;
                await repository.ReplaceAsync(previous, previous.Version, ct);
            }
        }

        _logger.LogWarning("regulatory_text.review.apply_conflict Id={Id}", id);
        return ClaimReviewApplyResult.NoMatchingOpenRound;
    }

    private static void RecordOutcomeDecision(
        RegulatoryText text, ClaimReviewRound round, string outcome, string? by, DateTimeOffset at)
    {
        var decision = outcome switch
        {
            ClaimReviewOutcomes.Approved => RegulatoryTextDecisionOutcomes.Approve,
            ClaimReviewOutcomes.Rejected => RegulatoryTextDecisionOutcomes.Reject,
            _ => null
        };
        if (decision is not null && text.Decisions.All(d => d.RoundNo != round.RoundNo))
        {
            text.Decisions.Add(new RegulatoryTextDecision { RoundNo = round.RoundNo, By = by, Outcome = decision, At = at });
        }
    }
}

/// <summary>WP-KP-5a — reconcile-on-read (KP-2): an open round older than the window is checked against MOD-0023's batch
/// status read (caller's token); a finished one is applied through the applier. A MOD-0023 problem never breaks a read.</summary>
public sealed class RegulatoryTextReviewReconciler
{
    private readonly IClaimWorkflowClient _client;
    private readonly RegulatoryTextOutcomeApplier _applier;
    private readonly IRegulatoryTextReviewSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<RegulatoryTextReviewReconciler> _logger;

    public RegulatoryTextReviewReconciler(IClaimWorkflowClient client, RegulatoryTextOutcomeApplier applier,
        IRegulatoryTextReviewSettings? settings = null, TimeProvider? clock = null,
        ILogger<RegulatoryTextReviewReconciler>? logger = null)
    {
        _client = client;
        _applier = applier;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<RegulatoryTextReviewReconciler>.Instance;
    }

    /// <summary>Returns how many texts changed (the caller reloads when &gt; 0).</summary>
    public async Task<int> ReconcileAsync(
        Guid tenantId, RegulatoryTextKind kind, IEnumerable<RegulatoryText> texts, CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().AddSeconds(
            -(_settings?.ReconcileAfterSeconds ?? RegulatoryTextReviewDefaults.ReconcileAfterSeconds));
        var due = texts.Where(t => t.OpenRound() is { } r && r.SubmittedAt <= cutoff).ToList();
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
                    kind.ObjectType, chunk.Select(t => t.Id.ToString("D")).ToList(), ct);
                if (states is null)
                {
                    return applied;
                }

                foreach (var text in chunk)
                {
                    var round = text.OpenRound()!;
                    var state = states.TryGetValue(text.Id.ToString("D"), out var instances)
                        ? instances.FirstOrDefault(i => i.WorkflowInstanceId == round.WorkflowInstanceId)
                        : null;
                    if (state?.Outcome is not { } outcome || !ClaimReviewOutcomes.IsValid(outcome))
                    {
                        continue;
                    }

                    if (await _applier.ApplyAsync(tenantId, kind.ObjectType, text.Id, round.WorkflowInstanceId, outcome,
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
            _logger.LogWarning(ex, "regulatory_text.review.reconcile_failed");
            return 0;
        }
    }
}
