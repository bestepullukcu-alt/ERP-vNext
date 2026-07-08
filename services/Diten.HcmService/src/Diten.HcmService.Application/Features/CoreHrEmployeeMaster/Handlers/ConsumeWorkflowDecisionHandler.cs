using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;

public sealed class ConsumeWorkflowDecisionHandler
    : IRequestHandler<ConsumeWorkflowDecisionCommand, Response<WorkflowDecisionConsumptionResponse>>
{
    public const string ScopeBlockedReason = "mod0251_workflow_decision_consumption_not_enabled";

    public Task<Response<WorkflowDecisionConsumptionResponse>> Handle(
        ConsumeWorkflowDecisionCommand request,
        CancellationToken cancellationToken)
        => Task.FromResult(Response<WorkflowDecisionConsumptionResponse>.Fail(ScopeBlockedReason, 409));
}
