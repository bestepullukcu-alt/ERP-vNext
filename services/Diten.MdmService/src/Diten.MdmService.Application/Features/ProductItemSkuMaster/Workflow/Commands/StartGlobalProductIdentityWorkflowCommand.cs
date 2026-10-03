using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartGlobalProductIdentityWorkflowRequest(
    Guid GlobalProductId,
    int ExpectedVersion,
    Guid OperationId);

public sealed record GlobalProductIdentityWorkflowResult(
    Guid OperationId,
    Guid GlobalProductId,
    string Checkpoint,
    Guid? WorkflowInstanceId,
    string RecoveryDisposition,
    bool IsReplay,
    // Only on 409 PRODUCT_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT, to the requester: the withdrawal to repeat.
    Guid? PendingWithdrawalCommandId = null);

public sealed record StartGlobalProductIdentityWorkflowCommand(
    StartGlobalProductIdentityWorkflowRequest Request)
    : IRequest<Response<GlobalProductIdentityWorkflowResult>>;
