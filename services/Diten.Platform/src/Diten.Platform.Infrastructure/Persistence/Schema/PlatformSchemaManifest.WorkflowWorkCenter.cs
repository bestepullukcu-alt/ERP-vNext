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
                    // Creator scope — this is what ListByCreatorAsync reads (BL-016, the Outbox tab) + due-date sweeps.
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
         * BL-279 — SIZED FROM THE REPOSITORY, NOT FROM THE MANIFEST MEMBERSHIP. DocumentReferenceListRepository
         * reads this collection two ways, and BOTH were measured as full scans of the register (717 rows in
         * diten_personalization_dev, COLLSCAN + blocking SORT on every call):
         *
         *   SearchAsync           {TenantId, DeletedAt:null, ListVersionId} [+ regex $or] sort DocumentCode limit N
         *   GetEntriesByUidsAsync {TenantId, DeletedAt:null, ListVersionId, DocumentUid $in} sort DocumentCode
         *
         * ⚠ NOTE THE SOFT-DELETE COLUMN. This repository scopes on DeletedAt, NOT the inherited IsDeleted —
         * it reaches the collection through a raw GetCollection, not through TenantRepository's ExecutionFilter.
         * Putting IsDeleted in these keys would index a field the query never mentions.
         *
         * TWO indexes, and the second one is EARNED BY MEASUREMENT rather than by symmetry: with the code index
         * alone the UID lookup still examined 358 documents; with its own index it examines 1. That is the join
         * key a task freezes when it cites a document, so it is the path that must not scan.
         */
        Collection<DocumentReferenceEntry>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.DocumentReferenceEntries,
            () => new CreateIndexModel<DocumentReferenceEntry>[]
            {
                    // ESR: equality on tenant+version, then DocumentCode IS the sort — the blocking SORT
                    // disappears and .Limit(n) can stop early instead of ordering the whole register.
                    new CreateIndexModel<DocumentReferenceEntry>(
                        Builders<DocumentReferenceEntry>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ListVersionId)
                            .Ascending(x => x.DocumentCode),
                        new CreateIndexOptions { Name = "ix_document_reference_entries_tenant_version_code" }),
                    new CreateIndexModel<DocumentReferenceEntry>(
                        Builders<DocumentReferenceEntry>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ListVersionId)
                            .Ascending(x => x.DocumentUid),
                        new CreateIndexOptions { Name = "ix_document_reference_entries_tenant_version_uid" })

            }),
        /*
         * BL-279 — ONE index, and deliberately no sort key. TaskCommentRepository reads exactly two shapes:
         *
         *   ListByTaskIdAsync   {TenantId, IsDeleted:false, TaskItemId}
         *   ListByTaskIdsAsync  {TenantId, IsDeleted:false, TaskItemId $in}   (the list page's batch read)
         *
         * ⚠ NO SORT FIELD IN THE KEY, AND THAT IS NOT AN OVERSIGHT. Both callers order the result IN MEMORY on
         * CreatedAt (BL-030): a DateTimeOffset is stored as the BSON ARRAY [ticks, offsetMinutes], and the
         * Id tie-break makes a server-side sort a parallel-array sort, which fails at runtime. Adding
         * CreatedAt to this key would index a sort the query is forbidden from asking Mongo to perform.
         *
         * Shape copied from ix_task_dependencies_tenant_task / ix_task_watchers_tenant_task above — the same
         * "children of one task, tenant-scoped, soft-delete aware" read.
         */
        Collection<TaskComment>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskComments,
            () => new CreateIndexModel<TaskComment>[]
            {
                    new CreateIndexModel<TaskComment>(
                        Builders<TaskComment>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_comments_tenant_task" })
            }),
        /*
         * BL-279 — the same shape as task_comments above, for the same reason: TaskTransitionRepository reads
         * {TenantId, IsDeleted:false, TaskItemId} and its $in batch form, and orders in memory on CreatedAt.
         * The two collections are merged into ONE feed, so they must be indexed identically — an index that
         * made one half server-sortable and left the other in memory is how the halves start interleaving
         * wrongly at the seams.
         */
        Collection<TaskTransition>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskTransitions,
            () => new CreateIndexModel<TaskTransition>[]
            {
                    new CreateIndexModel<TaskTransition>(
                        Builders<TaskTransition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.TaskItemId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_task_transitions_tenant_task" })
            }),
        /*
         * BL-279 — ONE index, which is the measured answer and not the symmetric one. TaskTypeRepository reads:
         *
         *   GetByCodeAsync   {TenantId, IsDeleted:false, Code}
         *   ListActiveAsync  {TenantId, IsDeleted:false, IsActive:true, DeletedAt:null} sort Code
         *   ListAllAsync     {TenantId, IsDeleted:false}                                sort Code
         *
         * ⚠ THE UNIQUENESS IS A CORRECTNESS FIX, NOT A PERFORMANCE ONE. TaskType.Code is documented as
         * "Tenant-unique and IMMUTABLE" because changing a code rewrites the identity of every task opened
         * under it — but nothing enforced that, and the write path is a read-then-insert that two concurrent
         * callers both pass. Partial on IsDeleted:false so a retired type's code can be reused, matching
         * ux_task_field_definitions_tenant_code_active on the sibling this slice is modelled on.
         *
         * ⚠ A SECOND INDEX {TenantId, IsActive, Code} WAS MEASURED AND REJECTED. It is what the sibling
         * TaskFieldDefinition carries, so symmetry argued for it — but with the unique index alone ListActive
         * already runs FETCH->IXSCAN with no blocking SORT at identical cost (docs=2, keys=2), because
         * {TenantId, Code} is a sorted-by-Code walk of one tenant's types with IsActive/DeletedAt as cheap
         * residuals. An index that changes no plan is a write cost with no read benefit.
         */
        Collection<TaskType>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.TaskTypes,
            () => new CreateIndexModel<TaskType>[]
            {
                    new CreateIndexModel<TaskType>(
                        Builders<TaskType>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Code),
                        new CreateIndexOptions<TaskType>
                        {
                            Unique = true,
                            Name = "ux_task_types_tenant_code_active",
                            PartialFilterExpression = Builders<TaskType>.Filter.Eq(x => x.IsDeleted, false)
                        })
            }),
        /*
         * BL-279 — ONE index, on the hash and NOT on the date. DocumentReferenceListRepository reads:
         *
         *   FindLiveVersionByHashAsync {TenantId, IsDeleted:false, ContentHash, WithdrawnAt:null}
         *   ListVersionsAsync          {TenantId, IsDeleted:false}                    sort ImportedAt desc
         *   GetLatestVersionAsync      {TenantId, IsDeleted:false, WithdrawnAt:null}  sort ImportedAt desc, limit 1
         *
         * The hash lookup runs on EVERY import and is the one that must not scan; it is now FETCH->IXSCAN.
         * The two sorted reads keep a SORT stage, over a collection that holds ONE ROW PER IMPORT (three rows
         * in diten_personalization_dev after months of use) — bounded work, so no index is earned for them.
         *
         * ⚠ AN {TenantId, IsDeleted, ImportedAt} INDEX WAS BUILT, MEASURED AND REJECTED — and the reason is
         * the one this codebase already paid for once (BL-030). ImportedAt is a DateTimeOffset, stored as the
         * BSON ARRAY [ticks, offsetMinutes], so any index over it is MULTIKEY: Mongo emits one key per array
         * ELEMENT and compares documents by the extreme element. Probed with mixed offsets, the DESCENDING
         * read this repository actually performs stays correct (ticks dominate the offset), but the ASCENDING
         * order is wrong — v3,v1,v5,v4,v2 for rows whose true order is v1..v5. That wrongness is in the DATA
         * SHAPE and reproduces identically on a COLLSCAN, so it is not a regression this index would cause —
         * but declaring the index would silently bless a sort key whose ordering is accidental, and buy
         * nothing measurable for three rows. Fixing the storage shape is backlog, not an index.
         *
         * ⚠ NOT UNIQUE ON ContentHash, ON PURPOSE. Identical bytes are refused only while a live version
         * holds them: a WITHDRAWN version keeps its hash and is not soft-deleted, so the same hash may
         * legitimately appear twice. A partial-unique on IsDeleted:false would reject that lawful re-import.
         */
        Collection<DocumentReferenceListVersion>(
            SchemaProfile.WorkflowWorkCenter,
            PlatformCollections.DocumentReferenceListVersions,
            () => new CreateIndexModel<DocumentReferenceListVersion>[]
            {
                    new CreateIndexModel<DocumentReferenceListVersion>(
                        Builders<DocumentReferenceListVersion>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.ContentHash)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_document_reference_list_versions_tenant_hash" })
            }),
    };
}
