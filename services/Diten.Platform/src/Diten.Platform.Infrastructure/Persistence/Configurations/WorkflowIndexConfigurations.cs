using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// MOD-0023 Workflow Designer index configuration. Tenant-scoped compound indexes with a partial
/// filter on IsDeleted=false, mirroring the platform BusinessReferenceData / MOD-0024 convention.
/// </summary>
public static class WorkflowIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var definitions = database.GetCollection<WorkflowDefinition>("platform_workflow_definitions");
        var instances = database.GetCollection<WorkflowInstance>("platform_workflow_instances");
        var tasks = database.GetCollection<ApprovalTask>("platform_workflow_approval_tasks");

        await definitions.Indexes.CreateOneAsync(new CreateIndexModel<WorkflowDefinition>(
            Builders<WorkflowDefinition>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Code).Ascending(x => x.VersionNumber),
            new CreateIndexOptions<WorkflowDefinition>
            {
                Name = "ux_platform_workflow_definitions_tenant_code_version",
                Unique = true,
                PartialFilterExpression = Builders<WorkflowDefinition>.Filter.Eq(x => x.IsDeleted, false)
            }));

        await definitions.Indexes.CreateOneAsync(new CreateIndexModel<WorkflowDefinition>(
            Builders<WorkflowDefinition>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.IsDeleted),
            new CreateIndexOptions { Name = "ix_platform_workflow_definitions_tenant_status_deleted" }));

        await instances.Indexes.CreateOneAsync(new CreateIndexModel<WorkflowInstance>(
            Builders<WorkflowInstance>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.IsDeleted),
            new CreateIndexOptions { Name = "ix_platform_workflow_instances_tenant_status_deleted" }));

        await tasks.Indexes.CreateOneAsync(new CreateIndexModel<ApprovalTask>(
            Builders<ApprovalTask>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.IsDeleted),
            new CreateIndexOptions { Name = "ix_platform_workflow_approval_tasks_tenant_status_deleted" }));

        await tasks.Indexes.CreateOneAsync(new CreateIndexModel<ApprovalTask>(
            Builders<ApprovalTask>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.WorkflowInstanceId),
            new CreateIndexOptions { Name = "ix_platform_workflow_approval_tasks_tenant_instance" }));
    }
}
