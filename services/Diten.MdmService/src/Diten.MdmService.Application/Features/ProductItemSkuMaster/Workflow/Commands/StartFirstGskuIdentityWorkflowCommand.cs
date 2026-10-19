using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartFirstGskuIdentityWorkflowCommand(StartFirstGskuIdentityWorkflowRequest Request)
    : IRequest<Response<FirstGskuIdentityWorkflowResult>>;

public sealed record FirstGskuIdentityWorkflowResult(
    Guid OperationId,
    Guid ProductDefinitionRevisionId,
    Guid GskuId,
    string Checkpoint,
    Guid? WorkflowInstanceId,
    string RecoveryDisposition,
    bool IsReplay);
