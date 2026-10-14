using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>§4.9 refreshDecision — "Durumu yenile".</summary>
public sealed record RefreshDecisionCommand(Guid DecisionId) : IRequest<Response<DecisionDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Execute", "QuarantineDecision");
    public Guid? CentralAuditEntityId(object? response) => DecisionId;
}

/// <summary>§4.10 withdrawDecision — "Kararı geri çek" (decider only, PendingApproval only).</summary>
public sealed record WithdrawDecisionCommand(Guid DecisionId, string? Reason, string IdempotencyKey) : IRequest<Response<DecisionDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("LifecycleTransition", "QuarantineDecision");
    public Guid? CentralAuditEntityId(object? response) => DecisionId;
}

/// <summary>§4.18 prepareWorkflowTemplate — "Onay şablonunu hazırla".</summary>
public sealed record PrepareWorkflowTemplateCommand(string IdempotencyKey) : IRequest<Response<WorkflowTemplateResultDto>>, ICentralAuditOutsideTransaction
{
    // BL-501: writes nothing in Inventory (only MOD-0023 calls over HTTP) — recorded right after it succeeds.
    public CentralAuditMetadata CentralAudit => ForQuarantine("Create", "WorkflowTemplate");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<WorkflowTemplateResultDto>(response)?.TemplateId);
}

/// <summary>Contract §4.18 <c>WorkflowTemplateResult</c>.</summary>
public sealed record WorkflowTemplateResultDto(string Code, string TemplateId, int PublishedVersion, bool Created);

/// <summary>
/// §4.9 — asks MOD-0023 NOW (ignoring <c>ReconcileAfterSeconds</c>) and applies a terminal outcome; idempotent. 503
/// <c>QUARANTINE_WORKFLOW_UNAVAILABLE</c> when MOD-0023 cannot be read (state unchanged).
/// </summary>
public sealed class RefreshDecisionHandler : IRequestHandler<RefreshDecisionCommand, Response<DecisionDto>>
{
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineWorkflowReconciler _reconciler;

    public RefreshDecisionHandler(IQuarantineDecisionRepository decisions, QuarantineWorkflowReconciler reconciler)
    {
        _decisions = decisions;
        _reconciler = reconciler;
    }

    public async Task<Response<DecisionDto>> Handle(RefreshDecisionCommand request, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(request.DecisionId, ct);
        if (decision is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        if (await _reconciler.ReconcileAsync([decision.Id], force: true, ct) == QuarantineWorkflowReconciler.Result.Unavailable)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.WorkflowUnavailable, "The workflow service could not be read; nothing changed.", 503);
        }

        return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await _decisions.EventsAsync([decision.Id], ct)));
    }
}

/// <summary>
/// §4.10 — the decider withdraws a pending decision: the open MOD-0023 task is cancelled WITH THE CALLER'S TOKEN, then the
/// decision is Withdrawn and the record reopened; no movement. 403 <c>QUARANTINE_NOT_DECIDER</c>, 409
/// <c>QUARANTINE_DECISION_NOT_PENDING</c>; MOD-0023 refusals map like a start (forbidden 403, unavailable 503, other 400).
/// </summary>
public sealed class WithdrawDecisionHandler : IRequestHandler<WithdrawDecisionCommand, Response<DecisionDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly IQuarantineWorkflowClient _workflow;
    private readonly QuarantineSupport _support;

    public WithdrawDecisionHandler(
        IQuarantineRepository repository, IQuarantineDecisionRepository decisions, IQuarantineWorkflowClient workflow, QuarantineSupport support)
    {
        _repository = repository;
        _decisions = decisions;
        _workflow = workflow;
        _support = support;
    }

    public async Task<Response<DecisionDto>> Handle(WithdrawDecisionCommand request, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(request.DecisionId, ct);
        if (decision is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        if (decision.DeciderUserId != _support.UserId)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.NotDecider, "Only the decider withdraws a decision.", 403);
        }

        if (QuarantineDecisionRules.Reason(request.Reason) is { } v3)
        {
            return QuarantineSupport.Fail<DecisionDto>(v3);
        }

        var events = await _decisions.EventsAsync([decision.Id], ct);
        var state = QuarantineFacts.StateOf(events);
        if (state == DecisionState.Withdrawn)
        {
            return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, events)); // repeat
        }

        if (state != DecisionState.PendingApproval)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotPending, $"A {state} decision cannot be withdrawn.", 409);
        }

        var instanceId = events.Last(e => e.Kind == DecisionEventKind.WorkflowStarted).WorkflowInstanceId!;
        var tasks = await _workflow.TasksAsync(ct);
        if (tasks is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.WorkflowUnavailable, "The workflow service could not be read; nothing changed.", 503);
        }

        var open = tasks.FirstOrDefault(t => string.Equals(t.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase)
                                             && t.Status is "WaitingApproval" or "WaitingEvidence");
        if (open is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotPending,
                "The approval has no open task any more (it may have just been decided — use refresh).", 409);
        }

        var cancel = await _workflow.CancelTaskAsync(open.TaskId, QuarantineContract.WithdrawReasonCode, $"quarantine:withdraw:{decision.Id:N}", request.Reason!.Trim(), ct);
        if (!cancel.IsOk)
        {
            return cancel.Outcome switch
            {
                WorkflowCallOutcome.Forbidden => QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.WorkflowForbidden, "The caller may not cancel the approval task.", 403),
                WorkflowCallOutcome.Unavailable => QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.WorkflowUnavailable, "The workflow service is unavailable; nothing changed.", 503),
                _ => QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.WorkflowRejected, "The workflow service refused the cancellation: " + (cancel.Message ?? cancel.ReasonCode), 400)
            };
        }

        var now = QuarantineSupport.Now();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var hold = decision.HoldId is { } holdId ? await _repository.FindHoldAsync(holdId, ct) : null;
                var blocked = decision.BlockedStockId is { } blockedId ? await _repository.FindBlockedAsync(blockedId, ct) : null;
                await _repository.InTransactionAsync<bool>(async (session, c) =>
                {
                    var withdrawn = QuarantineSupport.Event(decision.Id, DecisionEventKind.Withdrawn, now, _support.UserId, null, request.Reason!.Trim());
                    withdrawn.WorkflowInstanceId = instanceId;
                    withdrawn.WorkflowOutcome = "cancelled";
                    await session.InsertDecisionEventAsync(withdrawn, c);
                    await QuarantineReopen.ReopenAsync(session, hold, blocked, decision.Id, now, c);
                    if (QuarantineApplyEngine.HasRecord(decision))
                    {
                        await session.InsertHistoryAsync([_support.History(QuarantineApplyEngine.SubjectOf(decision), QuarantineApplyEngine.TargetId(decision),
                            HistoryAction.Withdrawn, now, request.Reason!.Trim(), decision.Id)], c);
                    }
                    return true;
                }, ct);
                break;
            }
            catch (QuarantineRetryException) when (attempt < 3)
            {
            }
            catch (QuarantineDuplicateException)
            {
                break; // a concurrent withdraw recorded it
            }
        }

        return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await _decisions.EventsAsync([decision.Id], ct)));
    }
}

/// <summary>
/// §4.18 — creates <c>INV-QUARANTINE-RELEASE</c> (a duplicate code = it already exists) and publishes the shipped JSON with the
/// CALLER's token (the tenant Admin holds the PlatformAdmin design keys; MOD-0022 CT-D6). Idempotent: a template that already
/// has a published version is left untouched (<c>created = false</c>). Forbidden → 403, unavailable → 503.
/// </summary>
public sealed class PrepareWorkflowTemplateHandler : IRequestHandler<PrepareWorkflowTemplateCommand, Response<WorkflowTemplateResultDto>>
{
    private readonly IQuarantineWorkflowClient _workflow;

    public PrepareWorkflowTemplateHandler(IQuarantineWorkflowClient workflow) => _workflow = workflow;

    public async Task<Response<WorkflowTemplateResultDto>> Handle(PrepareWorkflowTemplateCommand request, CancellationToken ct)
    {
        var created = await _workflow.CreateDefinitionAsync(QuarantineWorkflowTemplate.Code, QuarantineWorkflowTemplate.Name, QuarantineWorkflowTemplate.Description, ct);
        WorkflowDefinitionInfo? definition = created.Value;
        if (!created.IsOk)
        {
            if (created.Outcome != WorkflowCallOutcome.Conflict)
            {
                return Refusal(created.Outcome, created.Message ?? created.ReasonCode);
            }

            var list = await _workflow.DefinitionsAsync(ct);
            if (!list.IsOk)
            {
                return Refusal(list.Outcome, list.Message);
            }

            definition = list.Value!.FirstOrDefault(d => d.TemplateCode == QuarantineWorkflowTemplate.Code);
            if (definition is null)
            {
                return Refusal(WorkflowCallOutcome.Rejected, "The template code is taken but the template cannot be read.");
            }
        }

        if (definition!.ActivePublishedVersionId is { } active)
        {
            var number = await _workflow.VersionNumberAsync(definition.TemplateId, active, ct);
            return number.IsOk
                ? Response<WorkflowTemplateResultDto>.Success(new WorkflowTemplateResultDto(definition.TemplateCode, definition.TemplateId, number.Value, false))
                : Refusal(number.Outcome, number.Message);
        }

        var published = await _workflow.PublishAsync(definition.TemplateId, QuarantineWorkflowTemplate.DefinitionJson, QuarantineWorkflowTemplate.SchemaVersion,
            QuarantineWorkflowTemplate.ExpressionVersion, "MOD-0175 INV-QUARANTINE-RELEASE v1 (Onay şablonunu hazırla)", ct);
        return published.IsOk
            ? Response<WorkflowTemplateResultDto>.Success(new WorkflowTemplateResultDto(definition.TemplateCode, definition.TemplateId, published.Value, true))
            : Refusal(published.Outcome, published.Message ?? published.ReasonCode);
    }

    private static Response<WorkflowTemplateResultDto> Refusal(WorkflowCallOutcome outcome, string? message) => outcome switch
    {
        WorkflowCallOutcome.Forbidden => QuarantineSupport.Fail<WorkflowTemplateResultDto>(QuarantineErrorCodes.WorkflowForbidden,
            "The caller may not design / publish workflow templates (tenant Admin).", 403),
        WorkflowCallOutcome.Unavailable => QuarantineSupport.Fail<WorkflowTemplateResultDto>(QuarantineErrorCodes.WorkflowUnavailable,
            "The workflow service is unavailable.", 503),
        _ => QuarantineSupport.Fail<WorkflowTemplateResultDto>(QuarantineErrorCodes.WorkflowRejected, "The workflow service refused: " + message, 400)
    };
}
