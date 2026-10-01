using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartGlobalProductRetirementRequestWorkflowRequest(Guid GlobalProductId,
    int ExpectedVersion, Guid OperationId, string Reason);
public sealed record GlobalProductRetirementRequestWorkflowResult(Guid OperationId, Guid GlobalProductId,
    string Checkpoint, Guid? WorkflowInstanceId, int ProductVersion, bool IsReplay);
public sealed record StartGlobalProductRetirementRequestWorkflowCommand(
    StartGlobalProductRetirementRequestWorkflowRequest Request)
    : IRequest<Response<GlobalProductRetirementRequestWorkflowResult>>;
