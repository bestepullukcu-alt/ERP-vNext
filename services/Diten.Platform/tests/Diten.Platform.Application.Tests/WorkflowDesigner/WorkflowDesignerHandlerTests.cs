using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkflowDesigner.Commands;
using Diten.Platform.Application.Features.WorkflowDesigner.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkflowDesigner;

public sealed class WorkflowDesignerHandlerTests
{
    private static ICurrentUserContext SystemUser()
    {
        var mock = new Mock<ICurrentUserContext>();
        mock.SetupGet(x => x.IsAuthenticated).Returns(false);
        return mock.Object;
    }

    [Fact]
    public async Task CreateDefinition_ReturnsCreated_Draft_WithSteps()
    {
        var repo = new Mock<IWorkflowDefinitionRepository>();
        repo.Setup(r => r.GetLatestVersionNumberAsync("APPROVAL", It.IsAny<CancellationToken>())).ReturnsAsync(0);
        repo.Setup(r => r.CreateAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowDefinition d, CancellationToken _) => d);

        var handler = new CreateWorkflowDefinitionCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CreateWorkflowDefinitionCommand(
            "APPROVAL", "Approval Flow", null,
            [new WorkflowStepInput("Manager review", "manager", false), new WorkflowStepInput("Finance sign-off", "finance", true)],
            [new SlaRuleInput(0, 24, "Notify", "manager")]), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(nameof(WorkflowDefinitionStatus.Draft), response.Data!.Status);
        Assert.Equal(2, response.Data!.Steps.Count);
        Assert.Equal(1, response.Data!.VersionNumber);
    }

    [Fact]
    public async Task CreateDefinition_DuplicateCode_Fails()
    {
        var repo = new Mock<IWorkflowDefinitionRepository>();
        repo.Setup(r => r.GetLatestVersionNumberAsync("APPROVAL", It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var handler = new CreateWorkflowDefinitionCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CreateWorkflowDefinitionCommand("APPROVAL", "x", null, [], []), CancellationToken.None);
        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("duplicate_definition_code", response.Errors);
    }

    [Fact]
    public async Task Publish_NoSteps_Fails()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IWorkflowDefinitionRepository>();
        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowDefinition { TenantId = Guid.NewGuid(), WorkflowDefinitionId = id, Code = "A", Name = "A", Status = WorkflowDefinitionStatus.Draft, Steps = [], RowVersion = 1 });
        var handler = new PublishWorkflowDefinitionCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new PublishWorkflowDefinitionCommand(id, 1), CancellationToken.None);
        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("definition_has_no_steps", response.Errors);
    }

    [Fact]
    public async Task Publish_Draft_Succeeds()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IWorkflowDefinitionRepository>();
        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowDefinition
            {
                TenantId = Guid.NewGuid(), WorkflowDefinitionId = id, Code = "A", Name = "A",
                Status = WorkflowDefinitionStatus.Draft, RowVersion = 1,
                Steps = [new WorkflowStep { StepId = Guid.NewGuid(), Name = "Step 1", Sequence = 0 }]
            });
        repo.Setup(r => r.GetPublishedByCodeAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowDefinition?)null);
        repo.Setup(r => r.UpdateAsync(It.IsAny<WorkflowDefinition>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new PublishWorkflowDefinitionCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new PublishWorkflowDefinitionCommand(id, 1), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(nameof(WorkflowDefinitionStatus.Published), response.Data!.Status);
        Assert.NotNull(response.Data!.PublishedAt);
    }

    [Fact]
    public async Task StartInstance_NoPublishedDefinition_Returns404()
    {
        var defs = new Mock<IWorkflowDefinitionRepository>();
        defs.Setup(r => r.GetPublishedByCodeAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync((WorkflowDefinition?)null);
        var runtime = new Mock<IWorkflowRuntimeRepository>();
        var handler = new StartWorkflowInstanceCommandHandler(defs.Object, runtime.Object, SystemUser());
        var response = await handler.Handle(new StartWorkflowInstanceCommand("A", "invoice-1"), CancellationToken.None);
        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("no_published_definition", response.Errors);
    }

    [Fact]
    public async Task StartInstance_CreatesRunningInstance_AndFirstApprovalTask()
    {
        var defs = new Mock<IWorkflowDefinitionRepository>();
        defs.Setup(r => r.GetPublishedByCodeAsync("A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowDefinition
            {
                TenantId = Guid.NewGuid(), WorkflowDefinitionId = Guid.NewGuid(), Code = "A", Name = "A",
                VersionNumber = 1, Status = WorkflowDefinitionStatus.Published,
                Steps = [new WorkflowStep { StepId = Guid.NewGuid(), Name = "Manager review", Sequence = 0, ApproverRole = "manager" }],
                SlaRules = [new SlaEscalationRule { RuleId = Guid.NewGuid(), StepSequence = 0, SlaHours = 24, EscalationAction = WorkflowEscalationAction.Notify }]
            });
        var runtime = new Mock<IWorkflowRuntimeRepository>();
        runtime.Setup(r => r.CreateInstanceAsync(It.IsAny<WorkflowInstance>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowInstance i, CancellationToken _) => i);
        runtime.Setup(r => r.CreateTaskAsync(It.IsAny<ApprovalTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApprovalTask t, CancellationToken _) => t);

        var handler = new StartWorkflowInstanceCommandHandler(defs.Object, runtime.Object, SystemUser());
        var response = await handler.Handle(new StartWorkflowInstanceCommand("A", "invoice-1"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(nameof(WorkflowInstanceStatus.Running), response.Data!.Status);
        Assert.Single(response.Data!.Steps);
        runtime.Verify(r => r.CreateTaskAsync(It.IsAny<ApprovalTask>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_EvidenceRequiredMissing_Returns422()
    {
        var taskId = Guid.NewGuid();
        var runtime = new Mock<IWorkflowRuntimeRepository>();
        runtime.Setup(r => r.GetTaskByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalTask { TenantId = Guid.NewGuid(), ApprovalTaskId = taskId, Status = ApprovalTaskStatus.Pending, RequiresEvidence = true, StepSequence = 0, RowVersion = 1 });

        var handler = new ApproveApprovalTaskCommandHandler(runtime.Object, SystemUser());
        var response = await handler.Handle(new ApproveApprovalTaskCommand(taskId, null, null), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains("evidence_required", response.Errors);
        runtime.Verify(r => r.UpdateTaskAsync(It.IsAny<ApprovalTask>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_LastStep_CompletesInstance()
    {
        var taskId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var runtime = new Mock<IWorkflowRuntimeRepository>();
        runtime.Setup(r => r.GetTaskByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalTask { TenantId = Guid.NewGuid(), ApprovalTaskId = taskId, WorkflowInstanceId = instanceId, Status = ApprovalTaskStatus.Pending, RequiresEvidence = false, StepSequence = 0, RowVersion = 1 });
        runtime.Setup(r => r.GetInstanceByIdAsync(instanceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowInstance
            {
                TenantId = Guid.NewGuid(), WorkflowInstanceId = instanceId, Status = WorkflowInstanceStatus.Running, CurrentStepSequence = 0, RowVersion = 1,
                Steps = [new WorkflowInstanceStep { StepId = Guid.NewGuid(), Name = "Step 1", Sequence = 0 }]
            });
        runtime.Setup(r => r.UpdateTaskAsync(It.IsAny<ApprovalTask>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runtime.Setup(r => r.UpdateInstanceAsync(It.IsAny<WorkflowInstance>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new ApproveApprovalTaskCommandHandler(runtime.Object, SystemUser());
        var response = await handler.Handle(new ApproveApprovalTaskCommand(taskId, null, null), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(nameof(WorkflowInstanceStatus.Completed), response.Data!.Status);
        runtime.Verify(r => r.CreateTaskAsync(It.IsAny<ApprovalTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_NonLastStep_AdvancesAndCreatesNextTask()
    {
        var taskId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var runtime = new Mock<IWorkflowRuntimeRepository>();
        runtime.Setup(r => r.GetTaskByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalTask { TenantId = Guid.NewGuid(), ApprovalTaskId = taskId, WorkflowInstanceId = instanceId, Status = ApprovalTaskStatus.Pending, RequiresEvidence = false, StepSequence = 0, RowVersion = 1 });
        runtime.Setup(r => r.GetInstanceByIdAsync(instanceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowInstance
            {
                TenantId = Guid.NewGuid(), WorkflowInstanceId = instanceId, Status = WorkflowInstanceStatus.Running, CurrentStepSequence = 0, RowVersion = 1,
                Steps =
                [
                    new WorkflowInstanceStep { StepId = Guid.NewGuid(), Name = "Step 1", Sequence = 0 },
                    new WorkflowInstanceStep { StepId = Guid.NewGuid(), Name = "Step 2", Sequence = 1 }
                ]
            });
        runtime.Setup(r => r.UpdateTaskAsync(It.IsAny<ApprovalTask>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runtime.Setup(r => r.UpdateInstanceAsync(It.IsAny<WorkflowInstance>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runtime.Setup(r => r.CreateTaskAsync(It.IsAny<ApprovalTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApprovalTask t, CancellationToken _) => t);

        var handler = new ApproveApprovalTaskCommandHandler(runtime.Object, SystemUser());
        var response = await handler.Handle(new ApproveApprovalTaskCommand(taskId, null, null), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(nameof(WorkflowInstanceStatus.Running), response.Data!.Status);
        Assert.Equal(1, response.Data!.CurrentStepSequence);
        runtime.Verify(r => r.CreateTaskAsync(It.IsAny<ApprovalTask>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_SetsInstanceRejected()
    {
        var taskId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var runtime = new Mock<IWorkflowRuntimeRepository>();
        runtime.Setup(r => r.GetTaskByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalTask { TenantId = Guid.NewGuid(), ApprovalTaskId = taskId, WorkflowInstanceId = instanceId, Status = ApprovalTaskStatus.Pending, StepSequence = 0, RowVersion = 1 });
        runtime.Setup(r => r.GetInstanceByIdAsync(instanceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowInstance { TenantId = Guid.NewGuid(), WorkflowInstanceId = instanceId, Status = WorkflowInstanceStatus.Running, RowVersion = 1, Steps = [new WorkflowInstanceStep { Sequence = 0, Name = "Step 1" }] });
        runtime.Setup(r => r.UpdateTaskAsync(It.IsAny<ApprovalTask>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        runtime.Setup(r => r.UpdateInstanceAsync(It.IsAny<WorkflowInstance>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new RejectApprovalTaskCommandHandler(runtime.Object, SystemUser());
        var response = await handler.Handle(new RejectApprovalTaskCommand(taskId, "insufficient"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(nameof(WorkflowInstanceStatus.Rejected), response.Data!.Status);
    }
}
