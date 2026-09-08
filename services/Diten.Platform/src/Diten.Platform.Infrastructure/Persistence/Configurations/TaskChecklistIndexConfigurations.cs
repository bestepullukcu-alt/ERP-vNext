using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// MOD-0024 Task &amp; Checklist Engine index configuration. Compound tenant-scoped indexes with
/// a partial filter on IsDeleted=false, mirroring the platform BusinessReferenceData convention.
/// </summary>
public static class TaskChecklistIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var tasks = database.GetCollection<WorkTask>("platform_tasks");
        var assignments = database.GetCollection<TaskAssignment>("platform_task_assignments");
        var templates = database.GetCollection<ChecklistTemplate>("platform_checklist_templates");
        var runs = database.GetCollection<ChecklistRun>("platform_checklist_runs");

        await tasks.Indexes.CreateOneAsync(new CreateIndexModel<WorkTask>(
            Builders<WorkTask>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.IsDeleted),
            new CreateIndexOptions { Name = "ix_platform_tasks_tenant_status_deleted" }));

        await tasks.Indexes.CreateOneAsync(new CreateIndexModel<WorkTask>(
            Builders<WorkTask>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.WorkTaskId),
            new CreateIndexOptions<WorkTask>
            {
                Name = "ux_platform_tasks_tenant_id",
                Unique = true,
                PartialFilterExpression = Builders<WorkTask>.Filter.Eq(x => x.IsDeleted, false)
            }));

        await assignments.Indexes.CreateOneAsync(new CreateIndexModel<TaskAssignment>(
            Builders<TaskAssignment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.WorkTaskId).Ascending(x => x.IsActive),
            new CreateIndexOptions { Name = "ix_platform_task_assignments_tenant_task_active" }));

        await templates.Indexes.CreateOneAsync(new CreateIndexModel<ChecklistTemplate>(
            Builders<ChecklistTemplate>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Code),
            new CreateIndexOptions<ChecklistTemplate>
            {
                Name = "ux_platform_checklist_templates_tenant_code",
                Unique = true,
                PartialFilterExpression = Builders<ChecklistTemplate>.Filter.Eq(x => x.IsDeleted, false)
            }));

        await runs.Indexes.CreateOneAsync(new CreateIndexModel<ChecklistRun>(
            Builders<ChecklistRun>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.IsDeleted),
            new CreateIndexOptions { Name = "ix_platform_checklist_runs_tenant_status_deleted" }));
    }
}
