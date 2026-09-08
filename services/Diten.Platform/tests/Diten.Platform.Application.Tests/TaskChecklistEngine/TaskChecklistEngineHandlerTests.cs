using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TaskChecklistEngine.Commands;
using Diten.Platform.Application.Features.TaskChecklistEngine.Handlers;
using Diten.Platform.Application.Features.TaskChecklistEngine.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.TaskChecklistEngine;

public sealed class TaskChecklistEngineHandlerTests
{
    private static ICurrentUserContext SystemUser()
    {
        var mock = new Mock<ICurrentUserContext>();
        mock.SetupGet(x => x.IsAuthenticated).Returns(false);
        return mock.Object;
    }

    [Fact]
    public async Task CreateWorkTask_ReturnsCreated_AndPersists()
    {
        var repo = new Mock<IWorkTaskRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkTask w, CancellationToken _) => w);

        var handler = new CreateWorkTaskCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(
            new CreateWorkTaskCommand("Provision workspace", null, null, null, null, false),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("Provision workspace", response.Data!.Title);
        Assert.Equal(nameof(WorkTaskStatus.Open), response.Data!.Status);
        repo.Verify(r => r.CreateAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateWorkTask_EmptyTitle_Fails()
    {
        var repo = new Mock<IWorkTaskRepository>();
        var handler = new CreateWorkTaskCommandHandler(repo.Object, SystemUser());

        var response = await handler.Handle(
            new CreateWorkTaskCommand("   ", null, null, null, null, false), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("title_required", response.Errors);
        repo.Verify(r => r.CreateAsync(It.IsAny<WorkTask>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteWorkTask_RequiresEvidence_WhenConfigured_Returns422()
    {
        var taskId = Guid.NewGuid();
        var repo = new Mock<IWorkTaskRepository>();
        repo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkTask
            {
                TenantId = Guid.NewGuid(),
                WorkTaskId = taskId,
                Title = "Sign-off",
                Status = WorkTaskStatus.Open,
                RequiresEvidence = true,
                RowVersion = 1
            });

        var handler = new CompleteWorkTaskCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CompleteWorkTaskCommand(taskId, null), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains("evidence_required", response.Errors);
        repo.Verify(r => r.UpdateAsync(It.IsAny<WorkTask>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteWorkTask_WithEvidence_Completes()
    {
        var taskId = Guid.NewGuid();
        var repo = new Mock<IWorkTaskRepository>();
        repo.Setup(r => r.GetByIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkTask
            {
                TenantId = Guid.NewGuid(),
                WorkTaskId = taskId,
                Title = "Sign-off",
                Status = WorkTaskStatus.Assigned,
                RequiresEvidence = true,
                RowVersion = 3
            });
        repo.Setup(r => r.UpdateAsync(It.IsAny<WorkTask>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CompleteWorkTaskCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CompleteWorkTaskCommand(taskId, "s3://evidence/doc.pdf"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(nameof(WorkTaskStatus.Completed), response.Data!.Status);
        Assert.Equal("s3://evidence/doc.pdf", response.Data!.EvidenceReference);
    }

    [Fact]
    public async Task CreateChecklistTemplate_ReturnsCreated_WithItems()
    {
        var repo = new Mock<IChecklistRepository>();
        repo.Setup(r => r.GetTemplateByCodeAsync("ONBOARD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistTemplate?)null);
        repo.Setup(r => r.CreateTemplateAsync(It.IsAny<ChecklistTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistTemplate t, CancellationToken _) => t);

        var handler = new CreateChecklistTemplateCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CreateChecklistTemplateCommand(
            "ONBOARD", "Onboarding", "New hire steps",
            [new ChecklistTemplateItemInput("Create account", false), new ChecklistTemplateItemInput("Collect signed policy", true)]),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(2, response.Data!.Items.Count);
        Assert.True(response.Data!.Items[1].RequiresEvidence);
    }

    [Fact]
    public async Task CreateChecklistTemplate_DuplicateCode_Fails()
    {
        var repo = new Mock<IChecklistRepository>();
        repo.Setup(r => r.GetTemplateByCodeAsync("ONBOARD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChecklistTemplate { TenantId = Guid.NewGuid(), Code = "ONBOARD", Name = "Existing" });

        var handler = new CreateChecklistTemplateCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CreateChecklistTemplateCommand("ONBOARD", "Onboarding", null, []), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("duplicate_template_code", response.Errors);
    }

    [Fact]
    public async Task StartChecklistRun_TemplateNotFound_Returns404()
    {
        var templateId = Guid.NewGuid();
        var repo = new Mock<IChecklistRepository>();
        repo.Setup(r => r.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistTemplate?)null);

        var handler = new StartChecklistRunCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new StartChecklistRunCommand(templateId, null), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("template_not_found", response.Errors);
    }

    [Fact]
    public async Task StartChecklistRun_CopiesTemplateItems()
    {
        var templateId = Guid.NewGuid();
        var repo = new Mock<IChecklistRepository>();
        repo.Setup(r => r.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChecklistTemplate
            {
                TenantId = Guid.NewGuid(),
                ChecklistTemplateId = templateId,
                Code = "ONBOARD",
                Name = "Onboarding",
                Status = ChecklistTemplateStatus.Active,
                Items =
                [
                    new ChecklistTemplateItem { ItemId = Guid.NewGuid(), Title = "Step A", Sequence = 0, RequiresEvidence = false },
                    new ChecklistTemplateItem { ItemId = Guid.NewGuid(), Title = "Step B", Sequence = 1, RequiresEvidence = true }
                ]
            });
        repo.Setup(r => r.CreateRunAsync(It.IsAny<ChecklistRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChecklistRun r, CancellationToken _) => r);

        var handler = new StartChecklistRunCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new StartChecklistRunCommand(templateId, null), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(2, response.Data!.Items.Count);
        Assert.Equal(nameof(ChecklistRunStatus.InProgress), response.Data!.Status);
    }

    [Fact]
    public async Task CompleteChecklistRunItem_WithEvidence_CompletesRunWhenLastItem()
    {
        var runId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var repo = new Mock<IChecklistRepository>();
        repo.Setup(r => r.GetRunByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChecklistRun
            {
                TenantId = Guid.NewGuid(),
                ChecklistRunId = runId,
                TemplateId = Guid.NewGuid(),
                TemplateCode = "ONBOARD",
                Name = "Onboarding run",
                Status = ChecklistRunStatus.InProgress,
                RowVersion = 1,
                Items = [new ChecklistRunItem { ItemId = itemId, Title = "Collect signed policy", RequiresEvidence = true, Sequence = 0 }]
            });
        repo.Setup(r => r.UpdateRunAsync(It.IsAny<ChecklistRun>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CompleteChecklistRunItemCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CompleteChecklistRunItemCommand(runId, itemId, "s3://evidence/policy.pdf"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(nameof(ChecklistRunStatus.Completed), response.Data!.Status);
        Assert.True(response.Data!.Items[0].IsCompleted);
    }

    [Fact]
    public async Task CompleteChecklistRunItem_EvidenceRequiredMissing_Returns422()
    {
        var runId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var repo = new Mock<IChecklistRepository>();
        repo.Setup(r => r.GetRunByIdAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChecklistRun
            {
                TenantId = Guid.NewGuid(),
                ChecklistRunId = runId,
                Status = ChecklistRunStatus.InProgress,
                RowVersion = 1,
                Items = [new ChecklistRunItem { ItemId = itemId, Title = "Collect signed policy", RequiresEvidence = true, Sequence = 0 }]
            });

        var handler = new CompleteChecklistRunItemCommandHandler(repo.Object, SystemUser());
        var response = await handler.Handle(new CompleteChecklistRunItemCommand(runId, itemId, null), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(422, response.StatusCode);
        Assert.Contains("evidence_required", response.Errors);
        repo.Verify(r => r.UpdateRunAsync(It.IsAny<ChecklistRun>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
