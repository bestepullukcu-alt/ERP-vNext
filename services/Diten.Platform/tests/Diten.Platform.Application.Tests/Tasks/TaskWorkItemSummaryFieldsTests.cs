using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WHAT THE TASK ACTUALLY SAYS — the four fields the detail page could not show.
///
/// <para><b>Measured (2026-08-12, live /WorkCenterNext/Details/…).</b> The page told the reader "15 days
/// overdue" and never said WHAT the due date was; it showed no description, no start date, no estimate and no
/// tags. The cause was not the screen: the WC-1 projection carried <c>title, dueAt, assignee, requester,
/// priority, gates, subtasks, activity</c> and nothing else, while the task entity has held
/// <c>Description</c>, <c>StartAt</c>, <c>EstimateHours</c> and <c>Tags</c> since Phase 1. The create form
/// collects all four; none of them survived the projection.</para>
///
/// <para><b>Additive and optional.</b> Every field is omitted when absent, exactly like <c>priority</c>: a
/// provider that has nothing to say must say nothing rather than emit an empty string the client then has to
/// tell apart from a real one. <c>summary</c> travels as a DISPLAY label because it is text a person typed —
/// the same treatment <c>title</c> gets.</para>
/// </summary>
public sealed class TaskWorkItemSummaryFieldsTests
{
    [Fact]
    public async Task The_description_travels_as_a_display_label()
    {
        var task = SelfTask();
        task.Description = "Q3 nakit akışını kontrol et";

        var item = await ProjectAsync(task);

        Assert.NotNull(item.Summary);
        Assert.Equal(WorkItemContract.LabelDisplay, item.Summary!.Kind);
        Assert.Equal("Q3 nakit akışını kontrol et", item.Summary.Text);
    }

    [Fact]
    public async Task The_planning_fields_travel()
    {
        var task = SelfTask();
        task.StartAt = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);
        task.EstimateHours = 6.5m;

        var item = await ProjectAsync(task);

        Assert.Equal(task.StartAt, item.StartAt);
        Assert.Equal(6.5m, item.EstimateHours);
    }

    [Fact]
    public async Task The_tags_travel_in_the_order_they_were_stored()
    {
        var task = SelfTask();
        task.Tags = ["finans", "q3"];

        var item = await ProjectAsync(task);

        Assert.Equal(["finans", "q3"], item.Tags);
    }

    [Fact]
    public async Task An_ABSENT_field_is_omitted_rather_than_emitted_empty()
    {
        /*
         * The distinction the whole page depends on: the summary card prints a row only when there is something
         * to print. An empty string or an empty array would each render as a labelled blank — the "Son tarih: —"
         * shape the round exists to remove.
         */
        var item = await ProjectAsync(SelfTask());

        Assert.Null(item.Summary);
        Assert.Null(item.StartAt);
        Assert.Null(item.EstimateHours);
        Assert.Null(item.Tags);
    }

    [Fact]
    public async Task A_WHITESPACE_description_counts_as_absent()
    {
        // A task saved with a stray space in the description must not produce an empty description block.
        var task = SelfTask();
        task.Description = "   ";

        var item = await ProjectAsync(task);

        Assert.Null(item.Summary);
    }

    [Fact]
    public async Task The_item_still_satisfies_the_executable_contract()
    {
        // The new fields are additive; nothing about the existing shape may shift because of them.
        var task = SelfTask();
        task.Description = "x";
        task.StartAt = DateTimeOffset.UtcNow;
        task.EstimateHours = 1m;
        task.Tags = ["a"];

        var item = await ProjectAsync(task);

        Assert.Equal("task", item.WorkIntent);
        Assert.Equal(WorkItemContract.ProviderCodeTasks, item.LifecycleOwner);
        Assert.NotNull(item.Concurrency);
        Assert.NotEmpty(item.WorkItemCapabilities);
    }

    private static async Task<WorkItemProjectionDto> ProjectAsync(TaskItem task)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository());

        var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
        var items = await provider.GetWorkItemsAsync(actor, CancellationToken.None);
        return Assert.Single(items);
    }

    private static TaskItem SelfTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Write the report",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };
}
