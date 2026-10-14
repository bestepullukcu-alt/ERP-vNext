using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartGskuCorrectionWorkflowRequest(Guid GskuId, int ExpectedGskuVersion,
    Guid OperationId, decimal PackQuantity, string PackUomCode);
public sealed record GskuCorrectionWorkflowResult(Guid OperationId, Guid GskuId, string Checkpoint,
    Guid? WorkflowInstanceId, int GskuVersion, bool IsReplay);
public sealed record StartGskuCorrectionWorkflowCommand(StartGskuCorrectionWorkflowRequest Request)
    : IRequest<Response<GskuCorrectionWorkflowResult>>;
