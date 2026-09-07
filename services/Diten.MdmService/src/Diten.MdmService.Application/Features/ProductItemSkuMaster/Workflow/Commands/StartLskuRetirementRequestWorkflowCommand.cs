using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartLskuRetirementRequestWorkflowRequest(Guid LskuId, int ExpectedVersion,
    Guid OperationId, string RequestReason);
public sealed record LskuRetirementRequestWorkflowResult(Guid OperationId, Guid LskuId, string Checkpoint,
    Guid? WorkflowInstanceId, int LskuVersion, bool IsReplay);
public sealed record StartLskuRetirementRequestWorkflowCommand(StartLskuRetirementRequestWorkflowRequest Request)
    : IRequest<Response<LskuRetirementRequestWorkflowResult>>;
