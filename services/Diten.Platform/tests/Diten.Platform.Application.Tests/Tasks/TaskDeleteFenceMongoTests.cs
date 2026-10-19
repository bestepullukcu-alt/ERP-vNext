using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D2 fix round 4 (CT-WF-D2-FIX4), item 7 — the soft delete that is conditional on the version the
/// caller read, against a REAL mongod: a delete that withdrew a running approval first must not delete around a new start.
/// </summary>
public sealed class TaskDeleteFenceMongoTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    [Fact]
    public async Task A_conditional_delete_deletes_only_a_task_still_at_the_version_that_was_read_and_only_in_its_own_tenant()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var own = Repository(context, Tenant);
        var task = new TaskItem
        {
            TenantId = Tenant,
            OrganizationUnitId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Title = "Delete me",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = TaskTestData.Me,
            CreatedByUserId = TaskTestData.Me,
            Lifecycle = TaskLifecycle.Open
        };
        await own.CreateAsync(task);
        var read = (await own.GetByIdAsync(task.Id))!;

        // Somebody moved the task since it was read (a start landed): the delete is refused and the task stays.
        read.Title = "Moved underneath";
        Assert.True(await own.UpdateAsync(read, read.Version));
        Assert.False(await own.DeleteAsync(task.Id, expectedVersion: read.Version - 1));
        Assert.NotNull(await own.GetByIdAsync(task.Id));

        // Another tenant's repository finds nothing to delete, at any version.
        var current = (await own.GetByIdAsync(task.Id))!;
        Assert.False(await Repository(context, Guid.NewGuid()).DeleteAsync(task.Id, current.Version));
        Assert.NotNull(await own.GetByIdAsync(task.Id));

        // At the version it was read, it goes — once.
        Assert.True(await own.DeleteAsync(task.Id, current.Version));
        Assert.Null(await own.GetByIdAsync(task.Id));
        Assert.False(await own.DeleteAsync(task.Id, current.Version));
    }

    private static TaskItemRepository Repository(PlatformDbContext context, Guid tenant)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenant);
        return new TaskItemRepository(context, tenantContext, new TaskTransitionRepository(context, tenantContext));
    }
}
