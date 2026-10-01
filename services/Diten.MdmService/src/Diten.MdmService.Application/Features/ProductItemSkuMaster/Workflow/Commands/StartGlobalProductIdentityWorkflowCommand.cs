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
    bool IsReplay);

public sealed record StartGlobalProductIdentityWorkflowCommand(
    StartGlobalProductIdentityWorkflowRequest Request)
    : IRequest<Response<GlobalProductIdentityWorkflowResult>>;
