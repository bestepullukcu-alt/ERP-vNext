using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Commercial.Pricing;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.4 (D13, "pricing approvals"): publishing a price list and changing a rule (create / revise /
// retire) go through ONE MOD-0023 round at a time, on the SAME machinery as the W3 authoring approvals (WorkflowApprovalFlow:
// the caller's token, the round / decision shapes, the outcome event inbox routed by ObjectType, reconcile-on-read). What is
// MOD-0156's own: the template code (Commercial:Pricing:Workflow:TemplateCode = COM-PRICING-PUBLISH, created live by W6-CFG),
// the deep links, and what an approval DOES — checked again when it lands (separation of duties, one active list per
// (legal entity, country, first day), no overlapping active rule), else the record goes back to draft with the reason.

/// <summary>Workflow configuration of MOD-0156 (template code, reconcile window).</summary>
public interface IPricingApprovalSettings
{
    string TemplateCode { get; }
    int ReconcileAfterSeconds { get; }
}

public static class PricingApprovalDefaults
{
    public const string TemplateCode = "COM-PRICING-PUBLISH";
    public const int ReconcileAfterSeconds = 120;
}

public static class PricingApprovalKinds
{
    public static readonly WorkflowApprovalKind PriceList = new(
        "commercial.price-list", "COMMERCIAL_PRICE_LIST", id => $"/Commercial/Pricing/PriceLists/Details/{id:D}");

    public static readonly WorkflowApprovalKind PricingRule = new(
        "commercial.pricing-rule", "COMMERCIAL_PRICING_RULE", id => $"/Commercial/Pricing/PricingRules/Details/{id:D}");
}

/// <summary>
/// The ONE place a round's outcome changes a price list or a rule (event consumer, reconcile-on-read, and the decision
/// endpoint itself — which applies at once, so a decision taken here never waits for the event bus). Safe to repeat: an
/// outcome lands only on the record's OPEN round whose workflow instance id matches.
/// </summary>
public sealed class PricingApprovalOutcomeApplier
{
    private readonly IPriceListRepository _lists;
    private readonly IPriceListItemRepository _items;
    private readonly IPricingRuleRepository _rules;
    private readonly PricingChangeLogWriter _log;
    private readonly ILogger<PricingApprovalOutcomeApplier> _logger;

    public PricingApprovalOutcomeApplier(IPriceListRepository lists, IPriceListItemRepository items, IPricingRuleRepository rules,
        PricingChangeLogWriter log, ILogger<PricingApprovalOutcomeApplier>? logger = null)
    {
        _lists = lists;
        _items = items;
        _rules = rules;
        _log = log;
        _logger = logger ?? NullLogger<PricingApprovalOutcomeApplier>.Instance;
    }

    public static bool Handles(string? objectType)
        => objectType == PricingApprovalKinds.PriceList.ObjectType || objectType == PricingApprovalKinds.PricingRule.ObjectType;

    public IWorkflowApprovalStore<PriceList> Lists => new PriceListStore(_lists);
    public IWorkflowApprovalStore<PricingRule> Rules => new PricingRuleStore(_rules);

    public Task<ClaimReviewApplyResult> ApplyAsync(Guid tenantId, string objectType, Guid id, Guid workflowInstanceId, string outcome,
        string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
        => objectType == PricingApprovalKinds.PriceList.ObjectType
            ? ApplyAsync(Lists, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt,
                PriceListStatuses.Draft, ListRefusalAsync, PublishListAsync, PricingChangeEntityTypes.PriceList, ct)
            : objectType == PricingApprovalKinds.PricingRule.ObjectType
                ? ApplyAsync(Rules, tenantId, id, workflowInstanceId, outcome, completedBy, reasonCode, completedAt,
                    PricingRuleStatuses.Draft, RuleRefusalAsync, ActivateRuleAsync, PricingChangeEntityTypes.PricingRule, ct)
                : Task.FromResult(ClaimReviewApplyResult.NotFound);

    // ── what is checked again when an approval lands ─────────────────────────────────────────────────────────────

    /// <summary>The refusal of a list's approval (SoD, overlap, empty), else null.</summary>
    public async Task<string?> ListRefusalAsync(PriceList list, string approver, CancellationToken ct)
    {
        if (WorkflowApprovalRules.IsOwn(list, approver))
        {
            return PricingErrors.PriceListSod;
        }

        if (await ListOverlapAsync(list, ct))
        {
            return PricingErrors.PriceListOverlap;
        }

        return await _items.CountAsync(list.TenantId, list.Id, ct) == 0 ? PricingErrors.PriceListEmpty : null;
    }

    /// <summary>Another published list of the same (legal entity, country) begins the same day (§4.1.2, 409 price_list_overlap).</summary>
    public async Task<bool> ListOverlapAsync(PriceList list, CancellationToken ct)
        => (await _lists.ListPublishedAsync(list.TenantId, list.LegalEntityId, list.CountryCode, ct))
            .Any(other => other.Id != list.Id && other.TenantId == list.TenantId && other.IsPublished() && other.ValidFrom == list.ValidFrom);

    public async Task<string?> RuleRefusalAsync(PricingRule rule, string approver, CancellationToken ct)
    {
        if (WorkflowApprovalRules.IsOwn(rule, approver))
        {
            return PricingErrors.RuleSod;
        }

        return await RuleOverlapAsync(rule, ct) ? PricingErrors.RuleOverlap : null;
    }

    /// <summary>An ACTIVE rule of another key with the same (legal entity, SKU, segment) and specificity intersects this
    /// one's period (§4.3, 409 pricing_rule_overlap). A retirement never overlaps.</summary>
    public async Task<bool> RuleOverlapAsync(PricingRule rule, CancellationToken ct)
    {
        if (rule.ChangeKind == PricingRuleChangeKinds.Retire)
        {
            return false;
        }

        var active = await _rules.ListActiveAsync(rule.TenantId, rule.LegalEntityId, new[] { rule.SkuId }, ct);
        return active.Any(other => other.RuleKey != rule.RuleKey && other.TenantId == rule.TenantId && RuleSpecificity.Overlaps(rule, other));
    }

    // ── what an approval does ────────────────────────────────────────────────────────────────────────────────────

    private Task PublishListAsync(PriceList list, string by, DateTimeOffset at, CancellationToken ct)
    {
        list.Status = PriceListStatuses.Published;
        list.PublishedAt = at;
        list.PublishedBy = by;
        return Task.CompletedTask;
    }

    private async Task ActivateRuleAsync(PricingRule rule, string by, DateTimeOffset at, CancellationToken ct)
    {
        rule.ActivatedAt = at;
        rule.ActivatedBy = by;
        rule.Status = rule.ChangeKind == PricingRuleChangeKinds.Retire ? PricingRuleStatuses.Retired : PricingRuleStatuses.Active;
        await Task.CompletedTask;
    }

    /// <summary>After an approved rule version is stored: its previous active versions become superseded (a revision) or
    /// retired (a retirement). Each is its own write; one that does not land never doubles a rule — the evaluation counts a
    /// rule key once, at its highest active version.</summary>
    private async Task CloseEarlierVersionsAsync(PricingRule approved, string by, CancellationToken ct)
    {
        var versions = await _rules.ListByRuleKeyAsync(approved.TenantId, approved.RuleKey, ct);
        foreach (var earlier in versions.Where(v => v.Id != approved.Id && v.TenantId == approved.TenantId && v.IsActive()))
        {
            var target = approved.ChangeKind == PricingRuleChangeKinds.Retire ? PricingRuleStatuses.Retired : PricingRuleStatuses.Superseded;
            var before = earlier.Status;
            earlier.Status = target;
            earlier.UpdatedAt = DateTimeOffset.UtcNow;
            if (await _rules.ReplaceAsync(earlier, earlier.Version, ct))
            {
                await _log.WriteAsync(earlier.TenantId, PricingChangeEntityTypes.PricingRule, earlier.Id, earlier.Version, earlier.RuleKey,
                    target == PricingRuleStatuses.Retired ? PricingChangeActions.Retired : PricingChangeActions.Superseded, by,
                    PricingChangeLogWriter.Diff(("status", before, target)), ct);
            }
            else
            {
                _logger.LogWarning("pricing.rule.close_earlier_conflict RuleKey={RuleKey} Id={Id}", approved.RuleKey, earlier.Id);
            }
        }
    }

    private async Task<ClaimReviewApplyResult> ApplyAsync<T>(IWorkflowApprovalStore<T> store, Guid tenantId, Guid id, Guid workflowInstanceId,
        string outcome, string? completedBy, string? reasonCode, DateTimeOffset completedAt, string draftStatus,
        Func<T, string, CancellationToken, Task<string?>> refusal, Func<T, string, DateTimeOffset, CancellationToken, Task> approve,
        string entityType, CancellationToken ct) where T : EntityBase, IWorkflowApprovable
    {
        if (!ClaimReviewOutcomes.IsValid(outcome))
        {
            return ClaimReviewApplyResult.UnknownOutcome;
        }

        if (outcome == ClaimReviewOutcomes.Approved && string.IsNullOrWhiteSpace(completedBy))
        {
            // the SoD needs the approver: the completion event (which names it) applies the approval
            _logger.LogInformation("pricing.approval.outcome_deferred Id={Id} Instance={Instance} reason=approver_unknown", id, workflowInstanceId);
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var record = await store.GetAsync(tenantId, id, ct);
            if (record is null || record.TenantId != tenantId)
            {
                return ClaimReviewApplyResult.NotFound;
            }

            var round = record.OpenRound();
            if (round is null || round.WorkflowInstanceId != workflowInstanceId)
            {
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
            string action;
            if (outcome == ClaimReviewOutcomes.Approved && await refusal(record, completedBy!, ct) is { } refused)
            {
                round.ReasonCode = refused;
                record.Status = draftStatus;
                action = PricingChangeActions.Refused;
                _logger.LogWarning("pricing.approval.outcome_refused Id={Id} Instance={Instance} reason={Reason}", id, workflowInstanceId, refused);
            }
            else if (outcome == ClaimReviewOutcomes.Approved)
            {
                await approve(record, completedBy!, completedAt, ct);
                action = record.Status == PricingRuleStatuses.Retired ? PricingChangeActions.Retired : PricingChangeActions.Published;
            }
            else
            {
                record.Status = draftStatus;
                action = outcome == ClaimReviewOutcomes.Rejected ? PricingChangeActions.Returned : PricingChangeActions.Withdrawn;
            }

            if (!await store.ReplaceAsync(record, record.Version, ct))
            {
                continue;
            }

            await _log.WriteAsync(record.TenantId, entityType, record.Id, record.Version,
                record is PricingRule r ? r.RuleKey : (record as PriceList)?.FamilyId, action, completedBy,
                PricingChangeLogWriter.Diff(("status", PriceListStatuses.InReview, record.Status), ("reason", null, round.ReasonCode)), ct);
            if (record is PricingRule activated && activated.Status is PricingRuleStatuses.Active or PricingRuleStatuses.Retired)
            {
                await CloseEarlierVersionsAsync(activated, completedBy!, ct);
            }

            return ClaimReviewApplyResult.Applied;
        }

        _logger.LogWarning("pricing.approval.apply_conflict Id={Id}", id);
        return ClaimReviewApplyResult.NoMatchingOpenRound;
    }

    private sealed class PriceListStore(IPriceListRepository repository) : IWorkflowApprovalStore<PriceList>
    {
        public Task<PriceList?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) => repository.GetByIdAsync(tenantId, id, ct);
        public Task<bool> ReplaceAsync(PriceList record, int expectedVersion, CancellationToken ct) => repository.ReplaceAsync(record, expectedVersion, ct);
    }

    private sealed class PricingRuleStore(IPricingRuleRepository repository) : IWorkflowApprovalStore<PricingRule>
    {
        public Task<PricingRule?> GetAsync(Guid tenantId, Guid id, CancellationToken ct) => repository.GetByIdAsync(tenantId, id, ct);
        public Task<bool> ReplaceAsync(PricingRule record, int expectedVersion, CancellationToken ct) => repository.ReplaceAsync(record, expectedVersion, ct);
    }
}

/// <summary>Reconcile-on-read (the W3 pattern): an open round older than the window is checked against MOD-0023 (caller's
/// token); a finished one is applied through <see cref="PricingApprovalOutcomeApplier"/>. A MOD-0023 problem never breaks a read.</summary>
public sealed class PricingApprovalReconciler
{
    private readonly IClaimWorkflowClient _client;
    private readonly PricingApprovalOutcomeApplier _applier;
    private readonly IPricingApprovalSettings? _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<PricingApprovalReconciler> _logger;

    public PricingApprovalReconciler(IClaimWorkflowClient client, PricingApprovalOutcomeApplier applier, IPricingApprovalSettings? settings = null,
        TimeProvider? clock = null, ILogger<PricingApprovalReconciler>? logger = null)
    {
        _client = client;
        _applier = applier;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<PricingApprovalReconciler>.Instance;
    }

    public async Task<int> ReconcileAsync(Guid tenantId, WorkflowApprovalKind kind, IEnumerable<IWorkflowApprovable> records, CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().AddSeconds(-(_settings?.ReconcileAfterSeconds ?? PricingApprovalDefaults.ReconcileAfterSeconds));
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
                            .OrderByDescending(t => t.CompletedAt ?? DateTimeOffset.MinValue).Select(t => t.ActionedBy).FirstOrDefault();
                    }

                    if (await _applier.ApplyAsync(tenantId, kind.ObjectType, record.Id, round.WorkflowInstanceId, outcome, approver, null,
                            state.CompletedAt ?? _clock.GetUtcNow(), ct) == ClaimReviewApplyResult.Applied)
                    {
                        applied++;
                    }
                }
            }

            return applied;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "pricing.approval.reconcile_failed Kind={Kind}", kind.ObjectType);
            return 0;
        }
    }
}
