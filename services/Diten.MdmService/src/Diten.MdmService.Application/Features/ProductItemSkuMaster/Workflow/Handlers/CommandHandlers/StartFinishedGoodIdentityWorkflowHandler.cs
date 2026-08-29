using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class StartFinishedGoodIdentityWorkflowHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    FinishedGoodIdentityWorkflowProcessor processor,
    FinishedGoodIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<StartFinishedGoodIdentityWorkflowCommand, Response<FinishedGoodIdentityWorkflowResult>>
{
    public async Task<Response<FinishedGoodIdentityWorkflowResult>> Handle(
        StartFinishedGoodIdentityWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var maker)
            || !actorContext.HasPermission(FinishedGoodIdentityLifecyclePermissions.Submit))
            return Fail("FINISHED_GOOD_IDENTITY_LIFECYCLE_FORBIDDEN", 403);

        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }

        var input = command.Request;
        if (input is null) return Fail("FINISHED_GOOD_IDENTITY_WORKFLOW_START_INVALID", 400);
        var result = await processor.StartInteractiveAsync(
            tenantContext.TenantId, input.FinishedGoodId, input.ExpectedVersion, input.OperationId,
            maker, token, executionConfiguration.LeaseDuration, executionConfiguration.RetryDelay,
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "FINISHED_GOOD_IDENTITY_WORKFLOW_START_FAILED", result.StatusCode);

        var operation = result.Operation;
        return Response<FinishedGoodIdentityWorkflowResult>.Success(new(
            operation.OperationId, operation.FinishedGoodId, operation.Checkpoint.ToString(),
            operation.WorkflowInstanceId, operation.RecoveryDisposition.ToString(), result.IsReplay),
            operation.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    private static Response<FinishedGoodIdentityWorkflowResult> Fail(string code, int status) =>
        Response<FinishedGoodIdentityWorkflowResult>.Fail(code, status);
}
