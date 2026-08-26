using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0023 workflow engine and MOD-0024 task engine — the two that WorkCenter tests exercise together.
    /// </summary>
    private static readonly SchemaCollection[] WorkflowWorkCenterCollections =
    {
        Collection<WorkflowTemplate>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.WorkflowTemplates,
            () => new CreateIndexModel<WorkflowTemplate>[]
            {
                    new CreateIndexModel<WorkflowTemplate>(
                        Builders<WorkflowTemplate>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TemplateCode),
                        new CreateIndexOptions<WorkflowTemplate>
                        {
                            Unique = true,
                            Name = "ux_workflow_templates_tenant_code_active",
                            PartialFilterExpression = Builders<WorkflowTemplate>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<WorkflowTemplate>(
                        Builders<WorkflowTemplate>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_workflow_templates_tenant_status_deleted" })

            }),
        Collection<WorkflowTemplateVersion>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.WorkflowTemplateVersions,
            () => new CreateIndexModel<WorkflowTemplateVersion>[]
            {
                    new CreateIndexModel<WorkflowTemplateVersion>(
                        Builders<WorkflowTemplateVersion>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TemplateId)
                            .Ascending(x => x.VersionNumber),
                        new CreateIndexOptions<WorkflowTemplateVersion>
                        {
                            Unique = true,
                            Name = "ux_workflow_template_versions_tenant_template_number_active",
                            PartialFilterExpression = Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<WorkflowTemplateVersion>(
                        Builders<WorkflowTemplateVersion>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TemplateId)
                            .Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_workflow_template_versions_tenant_template_status" }),
                    new CreateIndexModel<WorkflowTemplateVersion>(
                        Builders<WorkflowTemplateVersion>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TemplateId)
                            .Ascending(x => x.IsImmutable),
                        new CreateIndexOptions { Name = "ix_workflow_template_versions_tenant_template_immutable" })

            }),
        Collection<WorkflowInstance>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.WorkflowInstances,
            () => new CreateIndexModel<WorkflowInstance>[]
            {
                    new CreateIndexModel<WorkflowInstance>(
                        Builders<WorkflowInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ObjectRef)
                            .Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_workflow_instances_tenant_objectref_status" }),
                    new CreateIndexModel<WorkflowInstance>(
                        Builders<WorkflowInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ObjectType)
                            .Ascending(x => x.ObjectId)
                            .Descending(x => x.StartedAt),
                        new CreateIndexOptions { Name = "ix_workflow_instances_tenant_object_started" }),
                    new CreateIndexModel<WorkflowInstance>(
                        Builders<WorkflowInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ObjectRef)
                            .Descending(x => x.StartedAt),
                        new CreateIndexOptions { Name = "ix_workflow_instances_tenant_objectref_started" }),
                    new CreateIndexModel<WorkflowInstance>(
                        Builders<WorkflowInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.WorkflowTemplateId),
                        new CreateIndexOptions { Name = "ix_workflow_instances_tenant_template" }),
                    new CreateIndexModel<WorkflowInstance>(
                        Builders<WorkflowInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.IdempotencyKey),
                        new CreateIndexOptions<WorkflowInstance>
                        {
                            Unique = true,
                            Name = "ux_workflow_instances_tenant_idempotency_key_active",
                            // MongoDB partial indexes do not allow $ne; $type is supported and matches only
                            // documents where IdempotencyKey is a set (non-null) string — excludes null/missing.
                            PartialFilterExpression = Builders<WorkflowInstance>.Filter.And(
                                Builders<WorkflowInstance>.Filter.Eq(x => x.IsDeleted, false),
                                Builders<WorkflowInstance>.Filter.Type(x => x.IdempotencyKey, BsonType.String))
                        })

            }),
        Collection<ApprovalTask>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.ApprovalTasks,
            () => new CreateIndexModel<ApprovalTask>[]
            {
                    new CreateIndexModel<ApprovalTask>(
                        Builders<ApprovalTask>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.WorkflowInstanceId)
                            .Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_approval_tasks_tenant_instance_status" }),
                    new CreateIndexModel<ApprovalTask>(
                        Builders<ApprovalTask>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_approval_tasks_tenant_status" }),
                    new CreateIndexModel<ApprovalTask>(
                        Builders<ApprovalTask>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.DueAt),
                        new CreateIndexOptions { Name = "ix_approval_tasks_tenant_status_due" }),
                    new CreateIndexModel<ApprovalTask>(
                        Builders<ApprovalTask>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.AssignmentSnapshotId),
                        new CreateIndexOptions { Name = "ix_approval_tasks_tenant_assignment_snapshot" })

            }),
        Collection<RuntimeAssignmentSnapshot>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.WorkflowRuntimeAssignmentSnapshots,
            () => new CreateIndexModel<RuntimeAssignmentSnapshot>[]
            {
                    new CreateIndexModel<RuntimeAssignmentSnapshot>(
                        Builders<RuntimeAssignmentSnapshot>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.WorkflowInstanceId),
                        new CreateIndexOptions { Name = "ix_workflow_assignment_snapshots_tenant_instance" }),
                    new CreateIndexModel<RuntimeAssignmentSnapshot>(
                        Builders<RuntimeAssignmentSnapshot>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ApprovalTaskId),
                        new CreateIndexOptions { Name = "ix_workflow_assignment_snapshots_tenant_task" }),
                    new CreateIndexModel<RuntimeAssignmentSnapshot>(
                        Builders<RuntimeAssignmentSnapshot>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ResolvedPrincipalId),
                        new CreateIndexOptions { Name = "ix_workflow_assignment_snapshots_tenant_principal" })

            }),
        Collection<WorkflowTransitionLog>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.WorkflowTransitionLogs,
            () => new CreateIndexModel<WorkflowTransitionLog>[]
            {
                    new CreateIndexModel<WorkflowTransitionLog>(
                        Builders<WorkflowTransitionLog>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.WorkflowInstanceId)
                            .Ascending(x => x.SequenceNo),
                        new CreateIndexOptions<WorkflowTransitionLog>
                        {
                            Unique = true,
                            Name = "ux_workflow_transition_logs_tenant_instance_sequence_active",
                            PartialFilterExpression = Builders<WorkflowTransitionLog>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<WorkflowTransitionLog>(
                        Builders<WorkflowTransitionLog>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ApprovalTaskId)
                            .Ascending(x => x.Action)
                            .Ascending(x => x.IdempotencyKey),
                        new CreateIndexOptions<WorkflowTransitionLog>
                        {
                            Unique = true,
                            Name = "ux_workflow_transition_logs_task_action_idempotency_active",
                            // MongoDB partial indexes do not allow $ne; $type is supported and matches only
                            // documents where the key is set (Guid -> Binary, IdempotencyKey -> String),
                            // excluding null/missing so the unique constraint behaves like a sparse index.
                            PartialFilterExpression = Builders<WorkflowTransitionLog>.Filter.And(
                                Builders<WorkflowTransitionLog>.Filter.Eq(x => x.IsDeleted, false),
                                Builders<WorkflowTransitionLog>.Filter.Type(x => x.ApprovalTaskId, BsonType.Binary),
                                Builders<WorkflowTransitionLog>.Filter.Type(x => x.IdempotencyKey, BsonType.String))
                        })

            }),
        Collection<SlaEscalationRule>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.WorkflowSlaRules,
            () => new CreateIndexModel<SlaEscalationRule>[]
            {
                    new CreateIndexModel<SlaEscalationRule>(
                        Builders<SlaEscalationRule>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TemplateId)
                            .Ascending(x => x.StageCode)
                            .Ascending(x => x.StepCode)
                            .Ascending(x => x.IsActive),
                        new CreateIndexOptions { Name = "ix_workflow_sla_rules_tenant_template_step_active" }),
                    new CreateIndexModel<SlaEscalationRule>(
                        Builders<SlaEscalationRule>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.IsActive),
                        new CreateIndexOptions { Name = "ix_workflow_sla_rules_tenant_active" })

            }),
        Collection<TaskItem>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskItems,
            () => new CreateIndexModel<TaskItem>[]
            {
                    // "My work": tasks I hold, ordered by deadline.
                    new CreateIndexModel<TaskItem>(
                        Builders<TaskItem>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.AssigneeUserId)
                            .Ascending(x => x.Lifecycle)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_items_tenant_assignee_lifecycle" }),
                    // "Pool": unclaimed work offered to a position (AssigneeUserId == null).
                    new CreateIndexModel<TaskItem>(
                        Builders<TaskItem>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.PoolPositionId)
                            .Ascending(x => x.AssigneeUserId)
                            .Ascending(x => x.Lifecycle)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_items_tenant_pool_position_unclaimed" }),
                    // Organization-scoped filtering/reporting (pack §12 K6).
                    new CreateIndexModel<TaskItem>(
                        Builders<TaskItem>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OrganizationUnitId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_items_tenant_org_unit" }),
                    // Creator scope (feeds the later Outbox surface) + due-date sweeps.
                    new CreateIndexModel<TaskItem>(
                        Builders<TaskItem>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.CreatedByUserId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_items_tenant_creator" }),
                    new CreateIndexModel<TaskItem>(
                        Builders<TaskItem>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.DueAt)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_items_tenant_due_at" })

            }),
        Collection<TaskAssignment>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskAssignments,
            () => new CreateIndexModel<TaskAssignment>[]
            {
                    new CreateIndexModel<TaskAssignment>(
                        Builders<TaskAssignment>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.OccurredAt),
                        new CreateIndexOptions { Name = "ix_task_assignments_tenant_task_occurred" })
            }),
        Collection<TaskDependency>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskDependencies,
            () => new CreateIndexModel<TaskDependency>[]
            {
                    new CreateIndexModel<TaskDependency>(
                        Builders<TaskDependency>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_dependencies_tenant_task" }),
                    new CreateIndexModel<TaskDependency>(
                        Builders<TaskDependency>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.DependsOnTaskItemId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_dependencies_tenant_predecessor" })

            }),
        Collection<TaskWatcher>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskWatchers,
            () => new CreateIndexModel<TaskWatcher>[]
            {
                    new CreateIndexModel<TaskWatcher>(
                        Builders<TaskWatcher>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_watchers_tenant_task" }),
                    // Backs the later "İzlediklerim" filter (pack §12 K3).
                    new CreateIndexModel<TaskWatcher>(
                        Builders<TaskWatcher>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.UserId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_watchers_tenant_user" })

            }),
        Collection<TaskPersonalOverlay>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskPersonalOverlays,
            () => new CreateIndexModel<TaskPersonalOverlay>[]
            {
                    new CreateIndexModel<TaskPersonalOverlay>(
                        Builders<TaskPersonalOverlay>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.UserId),
                        new CreateIndexOptions { Unique = true, Name = "ux_task_personal_overlays_tenant_task_user" }),
                    // Backs the projection's page read: one reader's overlays across the tasks currently on screen.
                    new CreateIndexModel<TaskPersonalOverlay>(
                        Builders<TaskPersonalOverlay>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.UserId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_personal_overlays_tenant_user" })

            }),
        Collection<TaskFieldDefinition>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskFieldDefinitions,
            () => new CreateIndexModel<TaskFieldDefinition>[]
            {
                    new CreateIndexModel<TaskFieldDefinition>(
                        Builders<TaskFieldDefinition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<TaskFieldDefinition>
                        {
                            Unique = true,
                            Name = "ux_task_field_definitions_tenant_code_active",
                            PartialFilterExpression = Builders<TaskFieldDefinition>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<TaskFieldDefinition>(
                        Builders<TaskFieldDefinition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.IsActive)
                            .Ascending(x => x.SortOrder),
                        new CreateIndexOptions { Name = "ix_task_field_definitions_tenant_active_order" })

            }),
        Collection<ChecklistTemplate>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.ChecklistTemplates,
            () => new CreateIndexModel<ChecklistTemplate>[]
            {
                    new CreateIndexModel<ChecklistTemplate>(
                        Builders<ChecklistTemplate>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<ChecklistTemplate>
                        {
                            Unique = true,
                            Name = "ux_checklist_templates_tenant_code_active",
                            PartialFilterExpression = Builders<ChecklistTemplate>.Filter.Eq(x => x.IsDeleted, false)
                        })
            }),
        Collection<ChecklistRun>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.ChecklistRuns,
            () => new CreateIndexModel<ChecklistRun>[]
            {
                    new CreateIndexModel<ChecklistRun>(
                        Builders<ChecklistRun>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_checklist_runs_tenant_task" })
            }),
        Collection<TaskTemplate>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskTemplates,
            () => new CreateIndexModel<TaskTemplate>[]
            {
                    new CreateIndexModel<TaskTemplate>(
                        Builders<TaskTemplate>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<TaskTemplate>
                        {
                            Unique = true,
                            Name = "ux_task_templates_tenant_code_active",
                            PartialFilterExpression = Builders<TaskTemplate>.Filter.Eq(x => x.IsDeleted, false)
                        })
            }),
        Collection<TaskRecurrenceRule>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskRecurrenceRules,
            () => new CreateIndexModel<TaskRecurrenceRule>[]
            {
                    new CreateIndexModel<TaskRecurrenceRule>(
                        Builders<TaskRecurrenceRule>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.IsActive)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_recurrence_rules_tenant_active" })
            }),
        /*
         * ⚠ NO DECLARED INDEX — and that is a FINDING, not a decision. TaskRepositories reads this collection, but the
         * index configuration never named it, so every query against it is a collection scan. It is listed
         * here because the manifest is the registry of what EXISTS; leaving it out is what let it go
         * unindexed unnoticed in the first place. Sizing the right index is backlog, not this round.
         */
        Collection<DocumentReferenceEntry>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.DocumentReferenceEntries,
            () => Array.Empty<CreateIndexModel<DocumentReferenceEntry>>()),
        /*
         * ⚠ NO DECLARED INDEX — a FINDING, not a decision. TaskCommentRepository reads this collection, and the index
         * configuration never named it, so every query against it is a collection scan. It was invisible to
         * the first contract check because the name is passed to the generic repository base as a
         * constructor argument, not written inside a GetCollection<T>("…") call — see BL-279. Sizing the
         * right tenant-first index is backlog; being in the registry is not.
         */
        Collection<TaskComment>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskComments,
            () => Array.Empty<CreateIndexModel<TaskComment>>()),
        /*
         * ⚠ NO DECLARED INDEX — a FINDING, not a decision. TaskTransitionRepository reads this collection, and the index
         * configuration never named it, so every query against it is a collection scan. It was invisible to
         * the first contract check because the name is passed to the generic repository base as a
         * constructor argument, not written inside a GetCollection<T>("…") call — see BL-279. Sizing the
         * right tenant-first index is backlog; being in the registry is not.
         */
        Collection<TaskTransition>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskTransitions,
            () => Array.Empty<CreateIndexModel<TaskTransition>>()),
        /*
         * ⚠ NO DECLARED INDEX — a FINDING, not a decision. TaskTypeRepository reads this collection, and the index
         * configuration never named it, so every query against it is a collection scan. It was invisible to
         * the first contract check because the name is passed to the generic repository base as a
         * constructor argument, not written inside a GetCollection<T>("…") call — see BL-279. Sizing the
         * right tenant-first index is backlog; being in the registry is not.
         */
        Collection<TaskType>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskTypes,
            () => Array.Empty<CreateIndexModel<TaskType>>()),
        /*
         * ⚠ NO DECLARED INDEX — a FINDING, not a decision. DocumentReferenceListRepository reads this collection, and the index
         * configuration never named it, so every query against it is a collection scan. It was invisible to
         * the first contract check because the name is passed to the generic repository base as a
         * constructor argument, not written inside a GetCollection<T>("…") call — see BL-279. Sizing the
         * right tenant-first index is backlog; being in the registry is not.
         */
        Collection<DocumentReferenceListVersion>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.DocumentReferenceListVersions,
            () => Array.Empty<CreateIndexModel<DocumentReferenceListVersion>>()),
    };
}
