using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-439 — the second half of "Bilgi bekle": the person a waiting task is asking gets the question, and answers it.
///
/// <para>Three people, never two: <see cref="Holder"/> parks the task, <see cref="Asked"/> is named in
/// <c>WaitingOnUserId</c>, and <see cref="Bystander"/> is anybody else. With only two, "the one who may answer" and
/// "the one who may not" collapse into holder-versus-not, and a rule that admitted every non-holder would pass.</para>
///
/// <para>Every lever runs through the REAL handler, the REAL read rule and the REAL provider. Only the stores are
/// doubles; the HTTP + Mongo round trip is <see cref="TaskInquiryHttpMongoTests"/>.</para>
/// </summary>
/// <summary>
/// CT guards for WP-PSS-TASK-INQUIRY-02 (BL-439) — the sabotage that did NOT go red during acceptance.
///
/// <para>C6: <c>TaskWorkItemProvider</c> sends <c>InquiryAnswer</c> only while the answer is the latest word on
/// OPEN work — never on finished work (<c>InquiryAnswer: terminal ? null : …</c>). The agent's suite proved
/// "until it is parked again" but not "never on finished work", so removing the <c>terminal</c> guard stayed
/// green. This test is that missing measurement.</para>
/// </summary>
public sealed class TaskInquiryCtTests
{
    private static readonly Guid Holder = TaskTestData.Me;
    private static readonly Guid Asked = TaskTestData.Other;

    [Fact]
    public async Task On_finished_work_the_holders_card_no_longer_says_who_answered()
    {
        var unit = new OrganizationUnit
        {
            TenantId = TaskTestData.Tenant, Code = "OU-CT", Name = "Kalite",
            LegalEntityId = Guid.NewGuid(), Status = OrgUnitStatus.Active
        };
        var position = new Position
        {
            TenantId = TaskTestData.Tenant, Code = "POS-CT", Name = "Uzman",
            OrganizationUnitId = unit.Id, Status = PositionStatus.Active
        };
        var seats = new[] { Holder, Asked }
            .Select(user => new PositionAssignment
            {
                TenantId = TaskTestData.Tenant, PositionId = position.Id,
                UserId = user, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
            })
            .ToList();
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant,
            Title = "Lot 42 serbest bırakma",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Holder,
            CreatedByUserId = Holder,
            OrganizationUnitId = unit.Id,
            Lifecycle = TaskLifecycle.InProgress,
            Version = 1
        };
        task.CloseAcceptanceGate(Holder);
        var tasks = new FakeTaskItemRepository(task);
        var notifications = new FakeTaskNotificationService();

        var asked = await new InquireTaskItemHandler(
                tasks, new TaskLifecycleService(), new FakeCurrentUserContext(Holder),
                new FakePositionAssignmentRepository([.. seats]),
                new FakePositionRepository([position]),
                new FakeOrganizationUnitRepository([unit]),
                notifications, NullLogger<InquireTaskItemHandler>.Instance)
            .Handle(
                new InquireTaskItemCommand(task.Id, new InquireTaskItemRequest(task.Version, "Hangi lot?", Asked), "corr"),
                CancellationToken.None);
        Assert.Equal(204, asked.StatusCode);

        var answered = await new AnswerInquiryHandler(
                tasks, tasks.Transitions, new TaskLifecycleService(), new FakeCurrentUserContext(Asked),
                new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(), notifications,
                NullLogger<AnswerInquiryHandler>.Instance)
            .Handle(
                new AnswerInquiryCommand(task.Id, new AnswerInquiryRequest(task.Version, "Lot 42."), "corr"),
                CancellationToken.None);
        Assert.Equal(204, answered.StatusCode);

        var provider = new TaskWorkItemProvider(
            tasks,
            new FakePositionAssignmentRepository([.. seats]),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver((Holder, TaskTestData.MeDisplayName), (Asked, "Ayşe Yılmaz")),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            tasks.Transitions,
            new FakeTaskPersonalOverlayRepository(),
            new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(),
            new FakePositionRepository([position]),
            new FakeOrganizationUnitRepository([unit]),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());
        var holder = new WorkItemActor(Holder, IsPlatformActor: false,
            new HashSet<string> { TaskPermissions.Read, TaskPermissions.Update, TaskPermissions.Complete });

        // Control: on running work the card DOES say who answered — so the null below is the terminal rule, not
        // a field that was never written.
        var running = (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!;
        var openCard = await provider.GetWorkItemAsync(running, holder, CancellationToken.None);
        Assert.NotNull(openCard);
        Assert.NotNull(openCard!.InquiryAnswer);
        Assert.Equal(Asked.ToString(), openCard.InquiryAnswer!.AnsweredBy!.Id);

        // The work finishes. The answer stays in the history; the card stops repeating it.
        running.Lifecycle = TaskLifecycle.Done;
        Assert.True(await tasks.UpdateAsync(running, running.Version, CancellationToken.None));
        var finished = (await tasks.GetByIdAsync(task.Id, CancellationToken.None))!;
        Assert.Equal(TaskLifecycle.Done, finished.Lifecycle);

        var doneCard = await provider.GetWorkItemAsync(finished, holder, CancellationToken.None);
        Assert.NotNull(doneCard);
        Assert.Null(doneCard!.InquiryAnswer);
        Assert.Single(tasks.Transitions.Events, e => e.Kind == TaskTransitionKind.InquiryAnswered);
    }
}
