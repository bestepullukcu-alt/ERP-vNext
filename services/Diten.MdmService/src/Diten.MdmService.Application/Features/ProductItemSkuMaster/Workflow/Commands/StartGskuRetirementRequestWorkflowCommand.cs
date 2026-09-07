using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartGskuRetirementRequestWorkflowRequest(Guid GskuId, int ExpectedGskuVersion,
    Guid OperationId, string RequestReason);
public sealed record GskuRetirementRequestWorkflowResult(Guid OperationId, Guid GskuId, string Checkpoint,
    Guid? WorkflowInstanceId, int GskuVersion, bool IsReplay);
public sealed record StartGskuRetirementRequestWorkflowCommand(StartGskuRetirementRequestWorkflowRequest Request)
    : IRequest<Response<GskuRetirementRequestWorkflowResult>>;
