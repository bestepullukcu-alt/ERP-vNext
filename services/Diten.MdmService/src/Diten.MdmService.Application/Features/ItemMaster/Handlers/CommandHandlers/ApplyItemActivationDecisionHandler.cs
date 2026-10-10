using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// FU04 S2 (§21): the decision's item write, made under the processor's claim; the audit record carries the evidence.
public sealed class ApplyItemActivationDecisionHandler(IItemWorkflowOperationRepository operations)
    : IRequestHandler<ApplyItemActivationDecisionCommand, Response<ItemWorkflowWriteResult>>
{
    public async Task<Response<ItemWorkflowWriteResult>> Handle(ApplyItemActivationDecisionCommand command,
        CancellationToken cancellationToken)
    {
        var operation = command.Operation;
        var audit = command.AuditDetails;
        audit.Set(ItemAudit.OperationId, operation.OperationId.ToString("D"));
        audit.Set(ItemAudit.ActivationKind, operation.ActivationKind.ToString());
        var target = ItemActivationWorkflowProcessor.SourceStatus(operation.ActivationKind);
        ItemWorkflowWriteResult written;
        if (command.NotApplicableReason is { } reason)
        {
            audit.Set(ItemAudit.Decision, "NotApplicable");
            audit.Set(ItemAudit.OutcomeCode, reason);
            written = await operations.ApplyDecisionNotApplicableAsync(command.Claim, operation, reason,
                command.UpdatedAtUtcTicks, cancellationToken);
        }
        else if (command.Evidence is { } evidence)
        {
            audit.Set(ItemAudit.Decision, evidence.Decision.ToString());
            audit.Set(ItemAudit.DecisionActorId, evidence.DecisionActorSubjectId.ToString("D"));
            audit.Set(ItemAudit.DecisionAt, evidence.DecisionAtUtc.ToString("O"));
            audit.Set(ItemAudit.ApprovalTaskId, evidence.ApprovalTaskId.ToString("D"));
            audit.Set(ItemAudit.TransitionSequence, evidence.TransitionSequence);
            if (evidence.ReasonCode is { } code) audit.Set(ItemAudit.OutcomeCode, code);
            target = ItemActivationWorkflowProcessor.DecisionTarget(operation.ActivationKind, evidence.Decision);
            written = await operations.ApplyDecisionAsync(command.Claim, operation, evidence, command.UpdatedAtUtcTicks,
                cancellationToken);
        }
        else
        {
            return Response<ItemWorkflowWriteResult>.Fail("ITEM_ACTIVATION_DECISION_CONTRACT_INVALID", 400);
        }
        if (!written.Succeeded)
            return Response<ItemWorkflowWriteResult>.Fail(written.ErrorCode ?? "ITEM_ACTIVATION_STATE_CONFLICT",
                written.ErrorCode == Domain.Repositories.ProductIdentityStore.Unavailable ? 503 : 409);
        if (written.IsReplay) audit.Set(ItemAudit.Replay, true);
        audit.Change(nameof(Item.LifecycleStatus), ItemLifecycleStatus.PendingActivation.ToString(), target.ToString());
        audit.Set(ItemAudit.Lifecycle, target.ToString());
        return Response<ItemWorkflowWriteResult>.Success(written);
    }
}

// The production writer: the decision goes through the pipeline (path b) — see ApplyItemActivationDecisionCommand.
public sealed class MediatorItemActivationDecisionWriter(ISender sender) : IItemActivationDecisionWriter
{
    public Task<ItemWorkflowWriteResult> ApplyDecisionAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        Domain.ValueObjects.ProductIdentityWorkflowDecisionEvidence evidence, long updatedAtUtcTicks,
        CancellationToken cancellationToken) =>
        SendAsync(new(operation.ItemId, operation.TenantId, claim, operation, evidence, null, updatedAtUtcTicks), cancellationToken);

    public Task<ItemWorkflowWriteResult> ApplyNotApplicableAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        string reasonCode, long updatedAtUtcTicks, CancellationToken cancellationToken) =>
        SendAsync(new(operation.ItemId, operation.TenantId, claim, operation, null, reasonCode, updatedAtUtcTicks),
            cancellationToken);

    private async Task<ItemWorkflowWriteResult> SendAsync(ApplyItemActivationDecisionCommand command,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(command, cancellationToken);
        return response.IsSuccessful && response.Data is { } written
            ? written
            : new(false, false, null, response.Errors?.FirstOrDefault() ?? "ITEM_ACTIVATION_STATE_CONFLICT");
    }
}
