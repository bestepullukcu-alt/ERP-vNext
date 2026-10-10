using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

/// <summary>
/// MOD-0290-FU04 S2: whether the item approval runs in this host (ItemActivationWorkflow:Enabled with a valid
/// configuration). Off: a submit / reactivation / withdrawal writes nothing and answers 503
/// ITEM_ACTIVATION_APPROVAL_UNAVAILABLE — never a PendingActivation no engine holds (§8.4 B; S1's skeleton answer).
/// </summary>
public sealed record ItemActivationAvailability(bool Enabled);

/// <summary>
/// MOD-0290-FU04 S2 — the submit and the reactivation request (S1's skeleton replaced by the approval): the item is read,
/// its state and version checked, the activation gate run, then the processor starts the approval. A repeat of the same
/// request (same item, version and kind) is the same operation — answered from where it stands.
/// </summary>
public sealed class ItemActivationRequests(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    IItemRepository items,
    IItemWorkflowOperationRepository operations,
    ItemActivationAvailability availability,
    IServiceProvider services)
{
    public const string SubmitAbandoned = "ITEM_ACTIVATION_SUBMIT_ABANDONED";
    public const string DecisionNotApplicable = "ITEM_ACTIVATION_DECISION_NOT_APPLICABLE";

    public async Task<Response<ItemModels.ItemDto>> StartAsync(Guid itemId, ItemModels.ItemLifecycleRequest request,
        ItemActivationKind kind, AuditDetails audit, CancellationToken cancellationToken)
    {
        var expectedVersion = request.ExpectedVersion!.Value;
        var transition = kind == ItemActivationKind.Reactivation ? ItemTransition.RequestReactivation : ItemTransition.Submit;
        audit.Set(ItemAudit.ActivationKind, kind.ToString());
        var item = await items.GetByIdAsync(itemId, cancellationToken);
        if (item is null) return Fail(ItemErrors.NotFound, 404);
        var operationId = ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenantContext.TenantId, itemId,
            expectedVersion, kind);
        audit.Set(ItemAudit.OperationId, operationId.ToString("D"));
        // A repeat of a request already taken is answered from its operation (the item has moved on since).
        var known = await operations.GetByOperationIdAsync(operationId, cancellationToken) is not null;
        if (!known)
        {
            if (!ItemLifecycle.Allows(item.LifecycleStatus, transition)) return Fail(ItemErrors.TransitionInvalid, 409);
            if (item.Version != expectedVersion) return Fail(ItemErrors.ConcurrencyConflict, 409);
        }
        if (!availability.Enabled) return Fail(ItemErrors.ActivationApprovalUnavailable, 503);
        if (!known && ItemActivationGate.MissingFields(item) is { Count: > 0 } missing)
        {
            // R-04: an item whose stock fields are unknown is not sent for approval — the fields are named.
            audit.Set(ItemAudit.MissingFields, missing.ToArray());
            return Response<ItemModels.ItemDto>.Fail([ItemActivationGate.StockFieldsRequired, .. missing], 422);
        }
        if (!actorContext.TryResolveCanonicalHumanSubject(out var maker)) return Fail("ITEM_ACTIVATION_FORBIDDEN", 403);
        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }

        var processor = Processor();
        var execution = Execution();
        var result = await processor.StartInteractiveAsync(tenantContext.TenantId, itemId, kind, expectedVersion,
            operationId, maker, token, execution.LeaseDuration, execution.RetryDelay, cancellationToken);
        if (result.IsReplay) audit.Set(ItemAudit.Replay, true);
        if (result.Operation is { } ended && ended.Checkpoint is ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or ItemWorkflowCheckpoint.DecisionNotApplicable)
        {
            audit.Set(ItemAudit.OutcomeCode, ended.Checkpoint.ToString());
            return Response<ItemModels.ItemDto>.Fail(
                [ended.Checkpoint == ItemWorkflowCheckpoint.DecisionNotApplicable ? DecisionNotApplicable
                    : ended.LastFailureCode == ItemActivationWorkflowProcessor.SubmitTargetChanged
                        ? ItemActivationWorkflowProcessor.SubmitTargetChanged : SubmitAbandoned,
                 ended.LastFailureCode ?? result.ErrorCode ?? SubmitAbandoned], 409);
        }
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "ITEM_ACTIVATION_WORKFLOW_START_FAILED", result.StatusCode);
        audit.Set(ItemAudit.OutcomeCode, result.Operation.Checkpoint.ToString());
        var after = await items.GetByIdAsync(itemId, cancellationToken);
        if (after is null) return Fail(ItemErrors.NotFound, 404);
        if (after.LifecycleStatus != item.LifecycleStatus)
            audit.Change(nameof(Item.LifecycleStatus), item.LifecycleStatus.ToString(), after.LifecycleStatus.ToString());
        return Response<ItemModels.ItemDto>.Success(ItemRules.ToDto(after),
            result.Operation.Checkpoint == ItemWorkflowCheckpoint.Completed ? 200 : 202);
    }

    public async Task<Response<ItemModels.ItemDto>> WithdrawAsync(Guid itemId, ItemModels.ItemLifecycleRequest request,
        AuditDetails audit, CancellationToken cancellationToken)
    {
        var expectedVersion = request.ExpectedVersion!.Value;
        audit.Set(ItemAudit.ReasonProvided, !string.IsNullOrWhiteSpace(request.Reason));
        var item = await items.GetByIdAsync(itemId, cancellationToken);
        if (item is null) return Fail(ItemErrors.NotFound, 404);
        if (!availability.Enabled) return Fail(ItemErrors.ActivationApprovalUnavailable, 503);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var requester)) return Fail("ITEM_ACTIVATION_FORBIDDEN", 403);
        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }
        var commandId = ItemActivationWorkflowStartRequestFactory.WithdrawalCommandIdFor(tenantContext.TenantId, itemId,
            expectedVersion, requester);
        var execution = Execution();
        // The free text never enters the audit record (FIX1-EK); it travels to the engine as the cancel's comment.
        var result = await Processor().WithdrawInteractiveAsync(tenantContext.TenantId, itemId, expectedVersion, commandId,
            requester, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(), token,
            execution.LeaseDuration, execution.RetryDelay, cancellationToken);
        if (result.IsReplay) audit.Set(ItemAudit.Replay, true);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "ITEM_ACTIVATION_WITHDRAWAL_FAILED", result.StatusCode);
        audit.Set(ItemAudit.OperationId, result.Operation.OperationId.ToString("D"));
        audit.Set(ItemAudit.ActivationKind, result.Operation.ActivationKind.ToString());
        audit.Set(ItemAudit.OutcomeCode, result.Operation.Checkpoint.ToString());
        var after = await items.GetByIdAsync(itemId, cancellationToken);
        if (after is null) return Fail(ItemErrors.NotFound, 404);
        if (after.LifecycleStatus != item.LifecycleStatus)
            audit.Change(nameof(Item.LifecycleStatus), item.LifecycleStatus.ToString(), after.LifecycleStatus.ToString());
        return Response<ItemModels.ItemDto>.Success(ItemRules.ToDto(after),
            result.Operation.Checkpoint == ItemWorkflowCheckpoint.Completed ? 200 : 202);
    }

    // Resolved only when the approval is on (its configuration is validated at start-up then).
    private ItemActivationWorkflowProcessor Processor() =>
        (ItemActivationWorkflowProcessor)(services.GetService(typeof(ItemActivationWorkflowProcessor))
            ?? throw new InvalidOperationException(ItemErrors.ActivationApprovalUnavailable));

    private ItemActivationWorkflowExecutionConfiguration Execution() =>
        (ItemActivationWorkflowExecutionConfiguration)(services.GetService(typeof(ItemActivationWorkflowExecutionConfiguration))
            ?? throw new InvalidOperationException(ItemErrors.ActivationApprovalUnavailable));

    private static Response<ItemModels.ItemDto> Fail(string code, int status) => Response<ItemModels.ItemDto>.Fail(code, status);
}
