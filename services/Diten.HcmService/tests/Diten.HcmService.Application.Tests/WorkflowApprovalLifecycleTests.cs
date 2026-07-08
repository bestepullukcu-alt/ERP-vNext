using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class WorkflowApprovalLifecycleTests
{
    [Fact]
    public async Task WorkflowDecisionHandler_BlocksCurrentScope_WithoutLifecycleSideEffects()
    {
        var handler = new ConsumeWorkflowDecisionHandler();

        var response = await handler.Handle(
            new ConsumeWorkflowDecisionCommand(CreateMessage("approved")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ConsumeWorkflowDecisionHandler.ScopeBlockedReason, response.Errors!);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task WorkflowDecisionHandler_BlocksRejectedDecision_WithoutMutatingDraft()
    {
        var handler = new ConsumeWorkflowDecisionHandler();

        var response = await handler.Handle(
            new ConsumeWorkflowDecisionCommand(CreateMessage("rejected")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ConsumeWorkflowDecisionHandler.ScopeBlockedReason, response.Errors!);
    }

    private static WorkflowApprovalDecisionRecordedMessage CreateMessage(string decision)
        => new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("50000000-0000-0000-0000-000000000001"),
            WorkflowApprovalDecisionConsumptionRules.ExpectedEventName,
            decision,
            null,
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            "replay-1",
            1,
            Guid.NewGuid().ToString("D"),
            new Dictionary<string, string>
            {
                ["subjectModule"] = "MOD-0251",
                ["subjectType"] = "employee_draft",
                ["draftSessionId"] = Guid.Parse("02510000-0000-0000-0000-000000000101").ToString("D"),
                ["subjectId"] = Guid.Parse("02510000-0000-0000-0000-000000000101").ToString("D"),
                ["businessKey"] = "MOD-0251:scope-blocked"
            });
}
