using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartLskuIdentityWorkflowRequest(
    Guid LskuId,
    int ExpectedVersion,
    Guid OperationId);

public sealed record LskuIdentityWorkflowResult(
    Guid OperationId,
    Guid LskuId,
    string Checkpoint,
    Guid? WorkflowInstanceId,
    string RecoveryDisposition,
    bool IsReplay);

public sealed record StartLskuIdentityWorkflowCommand(
    StartLskuIdentityWorkflowRequest Request)
    : IRequest<Response<LskuIdentityWorkflowResult>>;
