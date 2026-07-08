using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record ConsumeWorkflowDecisionCommand(
    WorkflowApprovalDecisionRecordedMessage Message) : IRequest<Response<WorkflowDecisionConsumptionResponse>>;
