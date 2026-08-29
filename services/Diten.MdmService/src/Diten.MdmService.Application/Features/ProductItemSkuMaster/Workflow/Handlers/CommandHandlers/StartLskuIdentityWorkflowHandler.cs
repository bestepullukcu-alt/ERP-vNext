using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class StartLskuIdentityWorkflowHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    LskuIdentityWorkflowProcessor processor,
    LskuIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<StartLskuIdentityWorkflowCommand, Response<LskuIdentityWorkflowResult>>
{
    public async Task<Response<LskuIdentityWorkflowResult>> Handle(
        StartLskuIdentityWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var maker)
            || !actorContext.HasPermission(LskuIdentityLifecyclePermissions.Submit))
            return Fail("LSKU_IDENTITY_LIFECYCLE_FORBIDDEN", 403);

        string token;
        try { token = delegatedTokenAccessor.GetRequiredToken(); }
        catch (ProductIdentityDelegatedTokenException exception) { return Fail(exception.ErrorCode, 401); }

        var input = command.Request;
        if (input is null) return Fail("LSKU_IDENTITY_WORKFLOW_START_INVALID", 400);
        var result = await processor.StartInteractiveAsync(
            tenantContext.TenantId, input.LskuId, input.ExpectedVersion, input.OperationId,
            maker, token, executionConfiguration.LeaseDuration, executionConfiguration.RetryDelay,
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
            return Fail(result.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_START_FAILED", result.StatusCode);

        var operation = result.Operation;
        return Response<LskuIdentityWorkflowResult>.Success(new(
            operation.OperationId, operation.LskuId, operation.Checkpoint.ToString(),
            operation.WorkflowInstanceId, operation.RecoveryDisposition.ToString(), result.IsReplay),
            operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    private static Response<LskuIdentityWorkflowResult> Fail(string code, int status) =>
        Response<LskuIdentityWorkflowResult>.Fail(code, status);
}
