using System.Security.Cryptography;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §4.4 / §4.5 (D1, D3, D4, D10, D16, K-W9): the approver (Platform's line managers of the
/// rep), the MOD-0023 task with the approvers as runtime candidates, the one-time signed-in link, and the ONE place a
/// decision lands (<see cref="ApplyDecisionAsync"/>) — the screen / link decision, the Work Center completion event and
/// the reconcile-on-read all go through it. The first decision wins; a later one finds the order no longer pending.
/// </summary>
public sealed class PreOrderApprovalService
{
    public const string ChangesRequestedReason = PreOrderApprovalDefaults.ReasonPrefix + "_CHANGES_REQUESTED";

    private readonly PreOrderContext _c;
    private readonly IPreOrderApprovalLinkRepository _links;
    private readonly IPreOrderLineManagerReader _managers;
    private readonly IClaimWorkflowClient _workflow;
    private readonly IWorkflowDecisionClient _decisions;
    private readonly IPreOrderNotifier _notifier;
    private readonly PreOrderTransmissionService _transmission;
    private readonly IPreOrderApprovalSettings? _settings;
    private readonly ILogger<PreOrderApprovalService> _logger;

    public PreOrderApprovalService(PreOrderContext context, IPreOrderApprovalLinkRepository links, IPreOrderLineManagerReader managers,
        IClaimWorkflowClient workflow, IWorkflowDecisionClient decisions, IPreOrderNotifier notifier, PreOrderTransmissionService transmission,
        IPreOrderApprovalSettings? settings = null, ILogger<PreOrderApprovalService>? logger = null)
    {
        _c = context;
        _links = links;
        _managers = managers;
        _workflow = workflow;
        _decisions = decisions;
        _notifier = notifier;
        _transmission = transmission;
        _settings = settings;
        _logger = logger ?? NullLogger<PreOrderApprovalService>.Instance;
    }

    public static bool Handles(string? objectType) => objectType == PreOrderApprovalDefaults.ObjectType;

    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();

    public Task<PreOrderLineManagers> ApproversOfAsync(Guid repUserId, CancellationToken ct) => _managers.GetAsync(repUserId, ct);

    public static PreOrderApprovalRequirement Requirement(IReadOnlyList<string> reasons, PreOrderLineManagers managers)
        => new()
        {
            Required = true, Reasons = reasons.ToList(), ApproverPositionId = managers.Managers.FirstOrDefault()?.PositionId,
            ApproverPositionCode = managers.Managers.FirstOrDefault()?.PositionCode,
            ApproverUserIds = managers.Managers.Select(m => m.UserId).Distinct().ToList(),
            ApproverNames = managers.Managers.Where(m => !string.IsNullOrWhiteSpace(m.DisplayName))
                .GroupBy(m => m.UserId).ToDictionary(g => g.Key.ToString("D"), g => g.First().DisplayName!),
            FallbackLevel = managers.Available ? managers.Resolution : "unresolved"
        };

    /// <summary>
    /// The approval round of a submission whose approvers ARE resolved: MOD-0023 first (nothing written when it refuses),
    /// then the link (inert until the order points at its submission), then the order (the link is removed again when the
    /// order write loses — the compensation of the two-root write, §8). The e-mail event goes last (K-N8).
    /// </summary>
    public async Task<Response<PreOrderDto>?> RequestAsync(PreOrder order, int expectedVersion, PreOrderSettings settings,
        PreOrderVisibility visibility, CancellationToken ct)
    {
        var requirement = order.ApprovalRequirement!;
        var start = await _workflow.StartAsync(new ClaimWorkflowStartRequest(
            _settings?.TemplateCode ?? PreOrderApprovalDefaults.TemplateCode, PreOrderApprovalDefaults.ObjectType, order.Id.ToString("D"),
            $"{PreOrderApprovalDefaults.ObjectType}:{order.Id:D}", $"{PreOrderApprovalDefaults.ObjectType}:{order.Id:D}:{order.SubmissionNo}",
            new ClaimWorkflowDisplayContext($"{order.Code} · {order.Pharmacy.Name}", order.Rep.DisplayName, PreOrderAudit.SourceModule,
                PreOrderApprovalDefaults.Link(order.Id), new[] { order.Seller.Prefix, order.CountryCode }.Where(c => !string.IsNullOrEmpty(c)).ToList()),
            PreOrderApprovalDefaults.ReasonPrefix + "_SUBMITTED",
            requirement.ApproverUserIds.Select(id => id.ToString("D")).ToList()), ct);
        if (start.Outcome != ClaimWorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return start.Outcome switch
            {
                ClaimWorkflowCallOutcome.TemplateMissing => PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.WorkflowTemplateMissing,
                    "The approval workflow template is not available (not created or not published).", 409),
                ClaimWorkflowCallOutcome.Forbidden => PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.WorkflowUnavailable,
                    "You are not allowed to start the approval workflow.", 403),
                _ => PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.WorkflowUnavailable, "The approval workflow could not be reached.", 503)
            };
        }

        var token = NewToken();
        var now = _c.Now;
        var link = new PreOrderApprovalLink
        {
            TenantId = order.TenantId, PreOrderId = order.Id, SubmissionNo = order.SubmissionNo, TokenHash = HashToken(token),
            ApproverUserIds = requirement.ApproverUserIds.ToList(), ExpiresAt = now.AddHours(settings.ApprovalLinkTtlHours), CreatedAt = now
        };
        await _links.InsertAsync(link, ct);

        order.Approval = new PreOrderApproval { WorkflowInstanceId = instanceId, SubmissionNo = order.SubmissionNo };
        var payload = new Dictionary<string, string?> { ["approvers"] = requirement.ApproverUserIds.Count.ToString(), ["resolution"] = requirement.FallbackLevel };
        if (order.Status == PreOrderStatuses.PendingApproval)
        {
            // the approvers of a waiting order were resolved late (Platform was down at submission): the round starts now
            PreOrderStateMachine.Append(order, PreOrderEventTypes.ApprovalRequested, _c.CallerText, now, payload);
        }
        else
        {
            PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.RequestApproval, PreOrderEventTypes.ApprovalRequested, _c.CallerText, now, payload);
        }
        if (!await _c.SaveAsync(order, expectedVersion, ct))
        {
            await _links.DeleteAsync(order.TenantId, link.Id, ct);
            _logger.LogWarning("pre_order.submit.lost_race PreOrderId={Id} WorkflowInstanceId={Instance}", order.Id, instanceId);
            return PreOrderResults.Stale<PreOrderDto>();
        }

        await _notifier.ApprovalRequestedAsync(order, token, link.ExpiresAt, ct);
        return null;
    }

    /// <summary>The decision on MOD-0023's task (the caller's token): the task the Work Center shows. Null = done.</summary>
    public async Task<Response<PreOrderDto>?> DecideOnWorkflowAsync(PreOrder order, string decision, string? reason, CancellationToken ct)
    {
        if (order.Approval?.WorkflowInstanceId is not { } instanceId)
        {
            return null; // no workflow round (it never started) — the CRM decision stands alone
        }

        var mine = await _decisions.GetMyTasksAsync(ct);
        if (mine is null)
        {
            return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.WorkflowUnavailable, "The approval workflow could not be reached.", 503);
        }

        var task = mine.FirstOrDefault(t => t.WorkflowInstanceId == instanceId && t.IsOpen);
        if (task is null)
        {
            var all = await _workflow.GetTasksAsync([instanceId], ct);
            if (all is null)
            {
                return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.WorkflowUnavailable, "The approval workflow could not be reached.", 503);
            }

            task = all.FirstOrDefault(t => t.WorkflowInstanceId == instanceId && t.IsOpen);
            if (task is null)
            {
                return PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.AlreadyDecided, "The approval was already decided.", 409);
            }
        }

        var approve = decision == PreOrderDecisions.Approve;
        var reasonCode = decision switch
        {
            PreOrderDecisions.Approve => PreOrderApprovalDefaults.ReasonPrefix + "_APPROVED",
            PreOrderDecisions.Reject => PreOrderApprovalDefaults.ReasonPrefix + "_REJECTED",
            _ => ChangesRequestedReason
        };
        var result = await _decisions.DecideTaskAsync(task.TaskId, approve, _c.CallerText!, reasonCode,
            $"{PreOrderApprovalDefaults.ObjectType}:{order.Id:D}:{order.SubmissionNo}:{task.TaskId:D}:{decision}", reason, ct);
        return result.Outcome switch
        {
            ClaimWorkflowCallOutcome.Ok => null,
            ClaimWorkflowCallOutcome.Forbidden => PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.NotApprover,
                result.Detail ?? "You are not an approver of this step.", 403),
            ClaimWorkflowCallOutcome.NotFound => PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.AlreadyDecided, "The approval was already decided.", 409),
            _ => PreOrderResults.Fail<PreOrderDto>(PreOrderErrors.WorkflowUnavailable, "The approval workflow could not be reached.", 503)
        };
    }

    /// <summary>
    /// The ONE place a decision changes the order (§4.5): approve → approved → the system transmission; reject → rejected
    /// (final, copyable); request-changes → draft with the signature cleared (D2) and the event log kept. Every live link of
    /// the submission dies. False = the order was no longer pending (another decision won) — nothing changed.
    /// </summary>
    public async Task<bool> ApplyDecisionAsync(PreOrder order, string decision, Guid? decidedBy, string? reason, string channel,
        DateTimeOffset at, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (order.Status != PreOrderStatuses.PendingApproval)
            {
                return false;
            }

            var expected = order.Version;
            var by = decidedBy?.ToString("D");
            var payload = new Dictionary<string, string?> { ["channel"] = channel, ["reason"] = reason };
            var moved = decision switch
            {
                PreOrderDecisions.Approve => PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.Approve, PreOrderEventTypes.Approved, by, at, payload),
                PreOrderDecisions.Reject => PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.Reject, PreOrderEventTypes.Rejected, by, at, payload),
                _ => PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.RequestChanges, PreOrderEventTypes.ChangesRequested, by, at, payload)
            };
            if (!moved)
            {
                return false;
            }

            order.Approval ??= new PreOrderApproval { SubmissionNo = order.SubmissionNo };
            order.Approval.Decision = decision;
            order.Approval.DecidedByUserId = decidedBy;
            order.Approval.DecidedAt = at;
            order.Approval.Reason = reason;
            order.Approval.Channel = channel;
            if (decision == PreOrderDecisions.RequestChanges && order.Signature is not null)
            {
                order.Signature = null; // D2: the pharmacist signs the changed order again
                PreOrderStateMachine.Append(order, PreOrderEventTypes.SignatureCleared, by, at);
            }

            if (await _c.SaveAsync(order, expected, ct))
            {
                await _links.SupersedeAsync(order.TenantId, order.Id, at, ct);
                switch (decision)
                {
                    case PreOrderDecisions.Approve:
                        await _transmission.TransmitAsync(order, by, ct);
                        break;
                    case PreOrderDecisions.Reject:
                        await _notifier.RejectedAsync(order, reason ?? string.Empty, ct);
                        break;
                    default:
                        await _notifier.ChangesRequestedAsync(order, reason ?? string.Empty, ct);
                        break;
                }

                return true;
            }

            // lost a race: read again — a decision that already landed ends the loop above
            if (await _c.Orders.GetByIdAsync(order.TenantId, order.Id, ct) is not { } fresh)
            {
                return false;
            }

            order = fresh;
        }

        return false;
    }

    /// <summary>
    /// MOD-0023's completion (event or reconcile): bound to the order's CURRENT submission by the instance id; an approval
    /// is applied only with a known approver who is not the rep (the SoD is never skipped); a rejection whose reason is
    /// the change-request code is a change request; a cancelled / timed-out round sends the order back to draft.
    /// </summary>
    public async Task<ClaimReviewApplyResult> ApplyOutcomeAsync(Guid tenantId, Guid preOrderId, Guid workflowInstanceId, string outcome,
        string? completedBy, string? reasonCode, DateTimeOffset completedAt, CancellationToken ct)
    {
        var order = await _c.Orders.GetByIdAsync(tenantId, preOrderId, ct);
        if (order is null || order.TenantId != tenantId || order.Approval?.WorkflowInstanceId != workflowInstanceId)
        {
            return ClaimReviewApplyResult.NotFound;
        }

        if (order.Status != PreOrderStatuses.PendingApproval)
        {
            return ClaimReviewApplyResult.NoMatchingOpenRound;
        }

        Guid? by = Guid.TryParse(completedBy, out var parsed) ? parsed : null;
        string decision;
        if (outcome == ClaimReviewOutcomes.Approved)
        {
            if (by is null || by == order.Rep.UserId)
            {
                _logger.LogWarning("pre_order.outcome.approval_skipped PreOrderId={Id} reason={Reason}", order.Id, by is null ? "approver_unknown" : "sod");
                return ClaimReviewApplyResult.NoMatchingOpenRound;
            }

            decision = PreOrderDecisions.Approve;
        }
        else if (outcome == ClaimReviewOutcomes.Rejected)
        {
            decision = reasonCode == ChangesRequestedReason ? PreOrderDecisions.RequestChanges : PreOrderDecisions.Reject;
        }
        else
        {
            decision = PreOrderDecisions.RequestChanges; // cancelled / timed out: back to the rep
        }

        return await ApplyDecisionAsync(order, decision, by, reasonCode, PreOrderApprovalChannels.WorkCenter, completedAt, ct)
            ? ClaimReviewApplyResult.Applied
            : ClaimReviewApplyResult.NoMatchingOpenRound;
    }

    /// <summary>
    /// Reconcile-on-read (the event bus may be off): a pending order with an old enough round asks MOD-0023 for its
    /// outcome; a pending order whose approvers Platform could not resolve at submission is resolved again and, once they
    /// are known, its round is started. Fail-open — a read never breaks.
    /// </summary>
    public async Task<PreOrder> ReconcileAsync(PreOrder order, PreOrderVisibility visibility, CancellationToken ct)
    {
        if (order.Status != PreOrderStatuses.PendingApproval)
        {
            return order;
        }

        try
        {
            if (order.ApprovalRequirement is { FallbackLevel: "unresolved" } && visibility.IsOwner(order))
            {
                var managers = await _managers.GetAsync(order.Rep.UserId, ct);
                if (managers.Available && managers.Managers.Count > 0)
                {
                    var settings = await _c.SettingsAsync(order.TenantId, ct);
                    var expected = order.Version;
                    order.ApprovalRequirement = Requirement(order.ApprovalRequirement.Reasons, managers);
                    if (await RequestAsync(order, expected, settings, visibility, ct) is not null)
                    {
                        return await _c.Orders.GetByIdAsync(order.TenantId, order.Id, ct) ?? order;
                    }
                }

                return order;
            }

            if (order.Approval?.WorkflowInstanceId is not { } instanceId
                || order.Events.LastOrDefault(e => e.Type == PreOrderEventTypes.ApprovalRequested)?.At
                    > _c.Now.AddSeconds(-(_settings?.ReconcileAfterSeconds ?? PreOrderApprovalDefaults.ReconcileAfterSeconds)))
            {
                return order;
            }

            var states = await _workflow.GetInstancesByObjectsAsync(PreOrderApprovalDefaults.ObjectType, [order.Id.ToString("D")], ct);
            var state = states is not null && states.TryGetValue(order.Id.ToString("D"), out var instances)
                ? instances.FirstOrDefault(i => i.WorkflowInstanceId == instanceId)
                : null;
            if (state?.Outcome is not { } outcome || !ClaimReviewOutcomes.IsValid(outcome))
            {
                return order;
            }

            string? actionedBy = null;
            string? reasonCode = null;
            var tasks = await _workflow.GetTasksAsync([instanceId], ct);
            var last = tasks?.Where(t => t.WorkflowInstanceId == instanceId && !string.IsNullOrWhiteSpace(t.ActionedBy))
                .OrderByDescending(t => t.CompletedAt ?? DateTimeOffset.MinValue).FirstOrDefault();
            actionedBy = last?.ActionedBy;
            reasonCode = last?.ActionReasonCode;
            await ApplyOutcomeAsync(order.TenantId, order.Id, instanceId, outcome, actionedBy, reasonCode, state.CompletedAt ?? _c.Now, ct);
            return await _c.Orders.GetByIdAsync(order.TenantId, order.Id, ct) ?? order;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "pre_order.reconcile_failed PreOrderId={Id}", order.Id);
            return order;
        }
    }

    /// <summary>The link state for a token: valid | expired | consumed | superseded — or null (unknown token).</summary>
    public async Task<(PreOrderApprovalLink? Link, string State)> LinkStateAsync(Guid tenantId, string? token, PreOrder? order, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, PreOrderApprovalLinkStates.Superseded);
        }

        var link = await _links.GetByHashAsync(tenantId, HashToken(token), ct);
        if (link is null)
        {
            return (null, PreOrderApprovalLinkStates.Superseded);
        }

        order ??= await _c.Orders.GetByIdAsync(tenantId, link.PreOrderId, ct);
        var state = link.ConsumedAt is not null ? PreOrderApprovalLinkStates.Consumed
            : link.SupersededAt is not null || order is null || order.Status != PreOrderStatuses.PendingApproval || order.SubmissionNo != link.SubmissionNo
                ? PreOrderApprovalLinkStates.Superseded
            : link.ExpiresAt <= _c.Now ? PreOrderApprovalLinkStates.Expired
            : PreOrderApprovalLinkStates.Valid;
        return (link, state);
    }

    public Task<bool> ConsumeAsync(PreOrderApprovalLink link, Guid userId, CancellationToken ct)
        => _links.TryConsumeAsync(link.TenantId, link.Id, userId, _c.Now, ct);

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
