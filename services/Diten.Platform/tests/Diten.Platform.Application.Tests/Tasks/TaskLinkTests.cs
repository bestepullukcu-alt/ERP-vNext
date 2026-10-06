using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-414 — which door each task link opens.
///
/// <para>Places that SEND a reader to a task (an in-app notification, a meeting's related-task row) open the Task
/// Center detail, which answers for any reader the read rule admits. The work item's own source door stays the
/// record page — it is the way out of the Task Center, not into it.</para>
///
/// <para>⚠ The expected addresses are LITERALS on purpose. Asserting against <see cref="TaskLinks"/> would agree
/// with the builder whatever the builder said, so a builder pointing both doors at the same page would pass.</para>
/// </summary>
public sealed class TaskLinkTests
{
    private static readonly Guid Recipient = TaskTestData.Watcher;

    [Theory]
    [InlineData(TaskNotificationEvents.Mentioned)]
    [InlineData(TaskNotificationEvents.Assigned)]
    public async Task An_in_app_task_notification_sends_the_reader_to_the_task_center_detail(string eventCode)
    {
        var inApp = new FakeUserNotificationRepository();
        var service = new TaskNotificationService(
            new RecordingNotificationDispatchAdapter(),
            new FakeNotificationLocaleResolver(),
            new FakeTaskNotificationRecipientResolver(new[] { (Recipient, "watcher@example.test") }),
            new FakePositionAssignmentRepository(),
            inApp,
            new FakeTenantContext(TaskTestData.Tenant),
            NullLogger<TaskNotificationService>.Instance);
        var task = NewTask();

        await service.NotifyAsync(task, eventCode, [Recipient], TaskTestData.Me, CancellationToken.None);

        var written = Assert.Single(inApp.Written);
        Assert.Equal(Recipient, written.UserId);
        Assert.Equal($"/WorkCenterNext/Details/{task.Id}", written.TargetUrl);
    }

    [Fact]
    public async Task A_meetings_related_task_row_sends_the_reader_to_the_task_center_detail()
    {
        var task = NewTask();
        var resolver = new TaskRelatedRecordResolver(new FakeTaskItemRepository(task));

        var resolved = await resolver.ResolveAsync([task.Id], CancellationToken.None);

        Assert.Equal($"/WorkCenterNext/Details/{task.Id}", resolved[task.Id].Link);
    }

    [Fact]
    public async Task The_work_items_source_door_stays_the_record_page()
    {
        var task = NewTask();
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(),
            new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new FakeTaskTypeRepository());

        var listed = Assert.Single(await provider.GetWorkItemsAsync(
            new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()), CancellationToken.None));
        var byId = await provider.GetWorkItemAsync(
            task, new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()), CancellationToken.None);

        Assert.Equal($"/Tasks/{task.Id}", listed.Source.DeepLink);
        Assert.Equal($"/Tasks/{task.Id}", byId.Source.DeepLink);
    }

    [Fact]
    public void The_builder_names_two_different_doors()
    {
        var id = Guid.Parse("41400000-0000-0000-0000-0000000000c1");

        Assert.Equal("/WorkCenterNext/Details/41400000-0000-0000-0000-0000000000c1", TaskLinks.Detail(id));
        Assert.Equal("/Tasks/41400000-0000-0000-0000-0000000000c1", TaskLinks.Record(id));
    }

    private static TaskItem NewTask() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Link probe",
        Lifecycle = TaskLifecycle.InProgress,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Rival,
        OrganizationUnitId = Guid.NewGuid(),
        EmailNotificationsEnabled = true,
        Version = 1
    };
}
