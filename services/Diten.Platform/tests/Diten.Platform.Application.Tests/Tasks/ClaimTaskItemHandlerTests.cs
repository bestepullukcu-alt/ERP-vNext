using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — the pool CLAIM race. Two people pressing "Üzerime al" at the same instant must not both become the
// owner; the conditional write on the expected version decides, and the loser gets a controlled 409.
public sealed class ClaimTaskItemHandlerTests
{
    private static readonly Guid PositionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Two_simultaneous_claims_produce_exactly_one_owner()
    {
        var task = PoolTask();
        var repository = new FakeTaskItemRepository(task);
        var assignments = new FakeTaskAssignmentRepository();
        var positionAssignments = new FakePositionAssignmentRepository(
            Holder(TaskTestData.Me), Holder(TaskTestData.Rival));

        var meHandler = Handler(repository, assignments, positionAssignments, TaskTestData.Me);
        var rivalHandler = Handler(repository, assignments, positionAssignments, TaskTestData.Rival);

        // Both callers read version 1 and then race.
        var first = await meHandler.Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);
        var second = await rivalHandler.Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(TaskReasonCodes.AlreadyClaimed, second.ReasonCode);

        // Exactly one holder, and it is the winner.
        var stored = Assert.Single(repository.Items);
        Assert.Equal(TaskTestData.Me, stored.AssigneeUserId);

        // Only the successful claim is recorded in history.
        Assert.Single(assignments.Events, e => e.EventType == TaskAssignmentEventType.Claimed);
    }

    [Fact]
    public async Task A_user_who_does_not_hold_the_position_cannot_claim()
    {
        var task = PoolTask();
        var repository = new FakeTaskItemRepository(task);
        // Only the rival holds the position; "Me" does not.
        var positionAssignments = new FakePositionAssignmentRepository(Holder(TaskTestData.Rival));

        var handler = Handler(repository, new FakeTaskAssignmentRepository(), positionAssignments, TaskTestData.Me);

        var response = await handler.Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Null(repository.Items[0].AssigneeUserId);
    }

    [Fact]
    public async Task A_directly_assigned_task_is_not_claimable()
    {
        var task = PoolTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.AssigneeUserId = TaskTestData.Rival;

        var handler = Handler(
            new FakeTaskItemRepository(task),
            new FakeTaskAssignmentRepository(),
            new FakePositionAssignmentRepository(Holder(TaskTestData.Me)),
            TaskTestData.Me);

        var response = await handler.Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.NotClaimable, response.ReasonCode);
    }

    [Fact]
    public async Task A_closed_pool_task_cannot_be_claimed()
    {
        var task = PoolTask();
        task.Lifecycle = TaskLifecycle.Cancelled;

        var handler = Handler(
            new FakeTaskItemRepository(task),
            new FakeTaskAssignmentRepository(),
            new FakePositionAssignmentRepository(Holder(TaskTestData.Me)),
            TaskTestData.Me);

        var response = await handler.Handle(
            new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.InvalidState, response.ReasonCode);
    }

    [Fact]
    public async Task Another_tenants_task_is_invisible_rather_than_forbidden()
    {
        var foreign = PoolTask();
        // Simulate a row belonging to a different tenant: the repository filter must hide it entirely.
        var repository = new FakeTaskItemRepository();
        var handler = Handler(
            repository,
            new FakeTaskAssignmentRepository(),
            new FakePositionAssignmentRepository(Holder(TaskTestData.Me)),
            TaskTestData.Me);

        var response = await handler.Handle(
            new ClaimTaskItemCommand(foreign.Id, new ClaimTaskItemRequest(1), "corr"), CancellationToken.None);

        // 404 with no detail — the caller learns nothing about the other tenant's data.
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(TaskReasonCodes.NotFound, response.ReasonCode);
    }

    private static ClaimTaskItemHandler Handler(
        FakeTaskItemRepository tasks,
        FakeTaskAssignmentRepository assignments,
        FakePositionAssignmentRepository positionAssignments,
        Guid actor)
        => new(tasks, assignments, positionAssignments,
            new FakeCurrentUserContext(actor), new FakeTenantContext(TaskTestData.Tenant),
            new FakeTaskNotificationService(), NullLogger<ClaimTaskItemHandler>.Instance);

    private static TaskItem PoolTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Pooled work",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = PositionId,
        AssigneeUserId = null,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        EffectiveTo = null
    };
}
