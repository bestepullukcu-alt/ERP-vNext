using System.Globalization;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// Slice Q2 approval port (pack §8 KQ-F): a submitted decision starts ONE MOD-0023 instance of <c>INV-QUARANTINE-RELEASE</c>
/// with the decider's own token. Object <c>quarantine.decision</c> / decision id / decision number; candidates = the company's
/// approver candidates (V16: none → 409, nothing started); idempotency key <c>quarantine:quarantine.decision:{id}:r1</c> (a
/// repeated submit gets the SAME instance from MOD-0023); due = entered + wait target (KQ-D); DisplayContext for WorkCenter
/// (title, subtitle, chips, app-relative deep link to the quarantine record). Error map: template missing → 409, forbidden →
/// 403, other 4xx → 400, 5xx / transport → 503 — the decision ends StartFailed and NOTHING moves.
/// </summary>
public sealed class WorkflowApprovalPort : IQuarantineApprovalPort
{
    private readonly IQuarantineWorkflowClient _workflow;
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;

    public WorkflowApprovalPort(IQuarantineWorkflowClient workflow, IQuarantineRepository repository, QuarantineFacts facts)
    {
        _workflow = workflow;
        _repository = repository;
        _facts = facts;
    }

    public async Task<ApprovalStart> StartAsync(QuarantineDecision decision, CancellationToken cancellationToken = default)
    {
        var settings = await _facts.SettingsAsync(cancellationToken);
        // Q3 (O-Q2-3): MOD-0023 assigns ONE candidate (lexicographic first after resolution) and refuses the starter (SoD),
        // so the decider is never sent as a candidate; nobody left → 409, no un-approvable task is created. Position
        // candidates are kept (not resolvable here — ASSUMPTION-0175-Q3-04).
        var decider = "user:" + decision.DeciderUserId.ToString("D");
        var candidates = settings.ApproverCandidates
            .Where(c => !string.Equals(c.Trim(), decider, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0)
        {
            return ApprovalStart.Failed(QuarantineErrorCodes.ApproversNotConfigured, 409,
                "No approver candidate other than the decider is configured for this company (Quarantine settings); nothing was sent.");
        }

        var hold = decision.HoldId is { } holdId ? await _repository.FindHoldAsync(holdId, cancellationToken) : null;
        var entered = hold?.EnteredAtUtc ?? decision.DecidedAtUtc;
        var request = new WorkflowStartRequest(
            QuarantineWorkflowTemplate.Code,
            QuarantineWorkflowTemplate.ObjectType,
            decision.Id.ToString("D"),
            decision.DecisionNumber,
            candidates,
            ReasonCode(decision.Kind),
            QuarantineWorkflowTemplate.IdempotencyKey(decision.Id),
            entered.AddDays(settings.WaitTargetDays),
            Display(decision, hold));

        var result = await _workflow.StartAsync(request, cancellationToken);
        return result.Outcome switch
        {
            WorkflowCallOutcome.Ok when result.InstanceId is not null => ApprovalStart.Ok(result.InstanceId),
            WorkflowCallOutcome.TemplateMissing => ApprovalStart.Failed(QuarantineErrorCodes.WorkflowTemplateMissing, 409,
                "The approval template INV-QUARANTINE-RELEASE is not published for this tenant (Quarantine settings → prepare the template)."),
            WorkflowCallOutcome.Forbidden => ApprovalStart.Failed(QuarantineErrorCodes.WorkflowForbidden, 403,
                "The caller may not start an approval (platform.workflow.instances.start)."),
            WorkflowCallOutcome.Rejected => ApprovalStart.Failed(QuarantineErrorCodes.WorkflowRejected, 400,
                "The workflow service refused the start: " + (result.Message ?? result.ReasonCode ?? "no detail")),
            _ => ApprovalStart.Failed(QuarantineErrorCodes.WorkflowUnavailable, 503, "The workflow service is unavailable; nothing was sent.")
        };
    }

    /// <summary>Pack §8: <c>reasonCode = QUARANTINE_{KIND}</c> (HoldDecision → QUARANTINE_HOLD_DECISION).</summary>
    public static string ReasonCode(DecisionKind kind)
        => "QUARANTINE_" + string.Concat(kind.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();

    /// <summary>
    /// MOD-0023 DisplayContext (title ≤ 200, subtitle ≤ 300, chips ≤ 5 × 32, app-relative deep link): "Karantina kararı {KRN} ·
    /// {kind}", "{SKU} · Lot {lot} · {qty} {uom}", chips [kind, lot, warehouse], link to the quarantine record.
    /// </summary>
    public static WorkflowDisplay Display(QuarantineDecision decision, QuarantineHold? hold)
    {
        var subject = hold?.HoldNumber ?? decision.DecisionNumber;
        var kind = decision.Kind switch
        {
            DecisionKind.HoldDecision => decision.Lines.Count > 1 ? "Kısmi karar" : decision.Lines.Single().Decision switch
            {
                LineDecision.Release => "Serbest bırak",
                LineDecision.Reject => "Reddet",
                _ => "Bloke et"
            },
            DecisionKind.Disposition => "Disposition · " + decision.Disposition!.Kind,
            DecisionKind.Unblock => "Blokeyi kaldır",
            _ => "Lot serbest bırakma"
        };
        var quantity = decision.Kind == DecisionKind.Disposition ? decision.Disposition!.Quantity : decision.AvailableQuantityAtDecision;
        var subtitle = $"SKU {decision.SkuId.ToString("D")[..8]} · Lot {decision.LotId ?? "—"} · {quantity.ToString(CultureInfo.InvariantCulture)} {decision.BaseUomId} · {WireNames.ToWire(decision.FromStockStatus)}";
        var link = decision.HoldId is { } holdId ? $"/Quarantine/Details/{holdId:D}" : $"/Quarantine/Blocked/Details/{decision.BlockedStockId:D}";
        return new WorkflowDisplay(
            Cut($"Karantina kararı {subject} · {kind}", 200),
            Cut(subtitle, 300),
            QuarantineContract.ModuleId,
            link,
            new[] { kind, decision.LotId is null ? null : "Lot " + decision.LotId, "Depo " + decision.WarehouseId[..8] }
                .OfType<string>().Select(c => Cut(c, 32)).Take(5).ToList());
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>
/// Read-time reconciliation (pack §8 — the GUARANTEED path; RabbitMQ is off here and dead-lettered completion events are never
/// re-sent, CT-Q11): for the decisions a read touches that wait for approval (or were approved but not applied), ask MOD-0023
/// with the READER's token (<c>instances/by-objects</c>, ≤ 100 ids), and hand every terminal outcome of the decision's OWN
/// instance to <see cref="QuarantineOutcomeApplier"/>; the approver is the actor of the approving history row (the real
/// person — WP-0023-FX binds the actor). Applying is exactly-once by construction (once-only events + ledger keys), so two
/// parallel reads apply once. Best effort: an unreadable MOD-0023 never fails the read (refresh reports it).
/// </summary>
public sealed class QuarantineWorkflowReconciler
{
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly IQuarantineWorkflowClient _workflow;
    private readonly QuarantineOutcomeApplier _applier;
    private readonly QuarantineFacts _facts;
    private readonly ILogger<QuarantineWorkflowReconciler> _logger;

    public QuarantineWorkflowReconciler(
        IQuarantineDecisionRepository decisions, IQuarantineWorkflowClient workflow, QuarantineOutcomeApplier applier, QuarantineFacts facts,
        ILogger<QuarantineWorkflowReconciler> logger)
    {
        _decisions = decisions;
        _workflow = workflow;
        _applier = applier;
        _facts = facts;
        _logger = logger;
    }

    public enum Result
    {
        Nothing,
        Reconciled,
        Unavailable
    }

    /// <summary>Reconciles the given decisions (the ids a read touched). <paramref name="force"/> ignores <c>ReconcileAfterSeconds</c> ("Durumu yenile").</summary>
    public async Task<Result> ReconcileAsync(IEnumerable<Guid?> decisionIds, bool force, CancellationToken ct)
    {
        var ids = decisionIds.Where(i => i is not null).Select(i => i!.Value).Distinct().Take(100).ToList();
        if (ids.Count == 0)
        {
            return Result.Nothing;
        }

        var events = (await _decisions.EventsAsync(ids, ct)).ToLookup(e => e.DecisionId);
        var settings = await _facts.SettingsAsync(ct);
        var now = QuarantineSupport.Now();
        var waiting = ids
            .Select(id => (Id: id, Events: events[id].OrderBy(e => e.Sequence).ToList()))
            .Where(d => QuarantineFacts.StateOf(d.Events) is DecisionState.PendingApproval or DecisionState.Approved)
            .Select(d => (d.Id, Started: d.Events.LastOrDefault(e => e.Kind == DecisionEventKind.WorkflowStarted)))
            .Where(d => d.Started?.WorkflowInstanceId is not null
                        && (force || (now - d.Started.AtUtc).TotalSeconds >= settings.ReconcileAfterSeconds))
            .ToList();
        if (waiting.Count == 0)
        {
            return Result.Nothing;
        }

        var instances = await _workflow.InstancesByObjectsAsync(QuarantineWorkflowTemplate.ObjectType, waiting.Select(w => w.Id.ToString("D")).ToList(), ct);
        if (instances is null)
        {
            _logger.LogInformation("quarantine.reconcile.unavailable Decisions={Count}", waiting.Count);
            return Result.Unavailable;
        }

        var reconciled = false;
        foreach (var (id, started) in waiting)
        {
            var instanceId = started!.WorkflowInstanceId!;
            var state = instances.GetValueOrDefault(id.ToString("D"))?
                .FirstOrDefault(i => string.Equals(i.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            if (state?.Outcome is not { } outcome || Map(outcome) is not { } mapped)
            {
                continue; // still running (or another round's instance — never applied: trust rule)
            }

            var history = await _workflow.HistoryAsync(instanceId, ct);
            var actorRow = history?.Where(h => h.Action is "approve" or "reject" or "cancel" or "timeout").OrderBy(h => h.SequenceNo).LastOrDefault();
            if (!Guid.TryParse(actorRow?.ActorId, out var actor))
            {
                if (mapped == ApprovalOutcome.Approved)
                {
                    // the approver must be known for the four-eyes check (V17) — never apply on an unknown approver
                    _logger.LogWarning("quarantine.reconcile.approver_unknown Decision={Decision} Instance={Instance}", id, instanceId);
                    continue;
                }

                actor = Guid.Empty;
            }

            var applied = await _applier.ApplyOutcomeAsync(id, mapped, actor, instanceId, ct,
                mapped == ApprovalOutcome.Approved ? actorRow?.SignatureId : null);
            reconciled |= applied.IsSuccessful;
        }

        return reconciled ? Result.Reconciled : Result.Nothing;
    }

    public static ApprovalOutcome? Map(string outcome) => outcome switch
    {
        "approved" => ApprovalOutcome.Approved,
        "rejected" => ApprovalOutcome.Rejected,
        "cancelled" => ApprovalOutcome.Cancelled,
        "timed-out" => ApprovalOutcome.TimedOut,
        _ => null
    };
}
