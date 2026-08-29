using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;

public sealed class StartGlobalProductIdentityWorkflowHandler(
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    IProductIdentityDelegatedTokenAccessor delegatedTokenAccessor,
    GlobalProductIdentityWorkflowProcessor processor,
    ProductIdentityWorkflowExecutionConfiguration executionConfiguration)
    : IRequestHandler<StartGlobalProductIdentityWorkflowCommand,
        Response<GlobalProductIdentityWorkflowResult>>
{
    public async Task<Response<GlobalProductIdentityWorkflowResult>> Handle(
        StartGlobalProductIdentityWorkflowCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!actorContext.TryResolveCanonicalHumanSubject(out var makerSubjectId)
            || !actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductSubmit))
        {
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }

        string delegatedToken;
        try
        {
            delegatedToken = delegatedTokenAccessor.GetRequiredToken();
        }
        catch (ProductIdentityDelegatedTokenException exception)
        {
            return Fail(exception.ErrorCode, 401);
        }

        var input = request.Request;
        if (input is null)
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_START_INVALID", 400);
        }
        var result = await processor.StartInteractiveAsync(
            tenantContext.TenantId,
            input.GlobalProductId,
            input.ExpectedVersion,
            input.OperationId,
            makerSubjectId,
            delegatedToken,
            executionConfiguration.LeaseDuration,
            executionConfiguration.RetryDelay,
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
        {
            return Fail(result.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_START_FAILED", result.StatusCode);
        }

        var operation = result.Operation;
        return Response<GlobalProductIdentityWorkflowResult>.Success(
            new(
                operation.OperationId,
                operation.GlobalProductId,
                operation.Checkpoint.ToString(),
                operation.WorkflowInstanceId,
                operation.RecoveryDisposition.ToString(),
                result.IsReplay),
            operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed ? 200 : 202);
    }

    private static Response<GlobalProductIdentityWorkflowResult> Fail(string code, int statusCode) =>
        Response<GlobalProductIdentityWorkflowResult>.Fail(code, statusCode);
}
