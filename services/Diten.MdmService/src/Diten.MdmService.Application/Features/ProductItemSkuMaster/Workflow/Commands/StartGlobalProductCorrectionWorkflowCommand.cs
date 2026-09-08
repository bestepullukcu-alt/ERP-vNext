using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record StartGlobalProductCorrectionWorkflowRequest(
    Guid GlobalProductId,
    int ExpectedVersion,
    Guid OperationId,
    string GlobalProductName);

public sealed record GlobalProductCorrectionWorkflowResult(
    Guid OperationId,
    Guid GlobalProductId,
    string Checkpoint,
    Guid? WorkflowInstanceId,
    int ProductVersion,
    bool IsReplay);

public sealed record StartGlobalProductCorrectionWorkflowCommand(
    StartGlobalProductCorrectionWorkflowRequest Request)
    : IRequest<Response<GlobalProductCorrectionWorkflowResult>>;
