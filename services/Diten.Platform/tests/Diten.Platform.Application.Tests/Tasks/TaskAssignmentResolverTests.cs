using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — the assignment mapping is what lets pools exist WITHOUT rewriting the Task Center: the surface reads
// only the contract triple, never MOD-0024's internal target enum. Asserted per pack §12 K5.
public sealed class TaskAssignmentResolverTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly TaskAssignmentResolver _sut = new();

    [Fact]
    public void Self_assigned_is_owned_and_admitted_immediately()
    {
        var projection = _sut.Resolve(MakeTask(TaskAssignmentTarget.SelfAssigned, assignee: Me));

        Assert.Equal("direct", projection.AssignmentMode);
        Assert.Equal("owned", projection.OwnershipState);
        Assert.Equal("admitted", projection.AdmissionState);
    }

    [Fact]
    public void Person_assigned_waits_at_the_acceptance_gate()
    {
        var projection = _sut.Resolve(MakeTask(TaskAssignmentTarget.Person, assignee: Me));

        Assert.Equal("direct", projection.AssignmentMode);
        Assert.Equal("assigned", projection.OwnershipState);
        Assert.Equal("pendingAcceptance", projection.AdmissionState);
    }

    [Fact]
    public void Person_assigned_becomes_owned_once_work_has_started()
    {
        var task = MakeTask(TaskAssignmentTarget.Person, assignee: Me);
        task.Lifecycle = TaskLifecycle.InProgress;

        var projection = _sut.Resolve(task);

        Assert.Equal("owned", projection.OwnershipState);
        Assert.Equal("admitted", projection.AdmissionState);
    }

    [Fact]
    public void An_unclaimed_pool_task_is_unowned_and_pending_claim()
    {
        var task = MakeTask(TaskAssignmentTarget.PositionPool, assignee: null);
        task.PoolPositionId = Guid.NewGuid();

        var projection = _sut.Resolve(task);

        Assert.Equal("groupQueue", projection.AssignmentMode);
        Assert.Equal("unowned", projection.OwnershipState);
        Assert.Equal("pendingClaim", projection.AdmissionState);
    }

    [Fact]
    public void A_claimed_pool_task_leaves_the_pool_and_reads_as_owned_work()
    {
        var task = MakeTask(TaskAssignmentTarget.PositionPool, assignee: Me);
        task.PoolPositionId = Guid.NewGuid();
        task.Lifecycle = TaskLifecycle.InProgress;

        var projection = _sut.Resolve(task);

        // Still groupQueue (that is how it was offered), but no longer waiting to be claimed.
        Assert.Equal("groupQueue", projection.AssignmentMode);
        Assert.Equal("owned", projection.OwnershipState);
        Assert.Equal("admitted", projection.AdmissionState);
    }

    [Theory]
    [InlineData(TaskAssignmentTarget.SelfAssigned)]
    [InlineData(TaskAssignmentTarget.Person)]
    [InlineData(TaskAssignmentTarget.PositionPool)]
    public void Every_target_maps_to_contract_allowlisted_values(TaskAssignmentTarget target)
    {
        // fixture-contract.js ASSIGNMENT_MODES / OWNERSHIP_STATES / ADMISSION_STATES
        string[] modes = ["direct", "approval", "groupQueue", "offered"];
        string[] ownership = ["unowned", "assigned", "owned", "notApplicable"];
        string[] admission = ["pendingAcceptance", "pendingClaim", "pendingOffer", "admitted", "notApplicable"];

        var task = MakeTask(target, target == TaskAssignmentTarget.PositionPool ? null : Me);
        if (target == TaskAssignmentTarget.PositionPool)
        {
            task.PoolPositionId = Guid.NewGuid();
        }

        var projection = _sut.Resolve(task);

        Assert.Contains(projection.AssignmentMode, modes);
        Assert.Contains(projection.OwnershipState, ownership);
        Assert.Contains(projection.AdmissionState, admission);
    }

    private static TaskItem MakeTask(TaskAssignmentTarget target, Guid? assignee) => new()
    {
        TenantId = Tenant,
        Title = "Sample",
        AssignmentTarget = target,
        AssigneeUserId = assignee,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open
    };
}
