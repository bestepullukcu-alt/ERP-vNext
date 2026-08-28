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
    /// MOD-0029 controlled documents, template masters and variants, shares and access policies. The
/// largest profile, and the one that most tests do NOT need.
    /// </summary>
    private static readonly SchemaCollection[] DocumentManagementCollections =
    {
        Collection<BaselineRelease>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementBaselineReleases,
            () => new CreateIndexModel<BaselineRelease>[]
            {
                    new CreateIndexModel<BaselineRelease>(
                        Builders<BaselineRelease>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId),
                        new CreateIndexOptions<BaselineRelease>
                        {
                            Unique = true,
                            Name = "ux_dm_baseline_releases_tenant_baseline_id_active",
                            PartialFilterExpression = Builders<BaselineRelease>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<BaselineRelease>(
                        Builders<BaselineRelease>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_dm_baseline_releases_tenant_status_deleted" })

            }),
        Collection<CollectionDefinition>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCollectionDefinitions,
            () => new CreateIndexModel<CollectionDefinition>[]
            {
                    new CreateIndexModel<CollectionDefinition>(
                        Builders<CollectionDefinition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId)
                            .Ascending(x => x.CanonicalId),
                        new CreateIndexOptions<CollectionDefinition>
                        {
                            Unique = true,
                            Name = "ux_dm_collection_definitions_tenant_baseline_canonical_active",
                            PartialFilterExpression = Builders<CollectionDefinition>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<CollectionDefinition>(
                        Builders<CollectionDefinition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId)
                            .Ascending(x => x.ParentCanonicalId)
                            .Ascending(x => x.PathSegment),
                        new CreateIndexOptions<CollectionDefinition>
                        {
                            Unique = true,
                            Name = "ux_dm_collection_definitions_sibling_segment_active",
                            PartialFilterExpression = Builders<CollectionDefinition>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<CollectionDefinition>(
                        Builders<CollectionDefinition>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId)
                            .Ascending(x => x.DisplayOrder),
                        new CreateIndexOptions { Name = "ix_dm_collection_definitions_tree_order" })

            }),
        Collection<BaselineSnapshotManifest>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementBaselineSnapshotManifests,
            () => new CreateIndexModel<BaselineSnapshotManifest>[]
            {
                    new CreateIndexModel<BaselineSnapshotManifest>(
                        Builders<BaselineSnapshotManifest>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId),
                        new CreateIndexOptions<BaselineSnapshotManifest>
                        {
                            Unique = true,
                            Name = "ux_dm_baseline_snapshot_manifests_tenant_baseline_active",
                            PartialFilterExpression = Builders<BaselineSnapshotManifest>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<CollectionInstance>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCollectionInstances,
            () => new CreateIndexModel<CollectionInstance>[]
            {
                    new CreateIndexModel<CollectionInstance>(
                        Builders<CollectionInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.InstanceKey),
                        new CreateIndexOptions<CollectionInstance>
                        {
                            Unique = true,
                            Name = "ux_dm_collection_instances_tenant_instance_key_active",
                            PartialFilterExpression = Builders<CollectionInstance>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<CollectionInstance>(
                        Builders<CollectionInstance>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.CompanyId)
                            .Ascending(x => x.BaselineReleaseId)
                            .Ascending(x => x.InstanceToken)
                            .Ascending(x => x.FullPath),
                        new CreateIndexOptions { Name = "ix_dm_collection_instances_company_baseline_path" })

            }),
        Collection<InstantiationOperation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementInstantiationOperations,
            () => new CreateIndexModel<InstantiationOperation>[]
            {
                    new CreateIndexModel<InstantiationOperation>(
                        Builders<InstantiationOperation>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OperationId),
                        new CreateIndexOptions<InstantiationOperation>
                        {
                            Unique = true,
                            Name = "ux_dm_instantiation_operations_tenant_operation_active",
                            PartialFilterExpression = Builders<InstantiationOperation>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<InstantiationOperation>(
                        Builders<InstantiationOperation>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.CorrelationId),
                        new CreateIndexOptions { Name = "ix_dm_instantiation_operations_correlation" })

            }),
        Collection<InstantiationOutcome>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementInstantiationOutcomes,
            () => new CreateIndexModel<InstantiationOutcome>[]
            {
                    new CreateIndexModel<InstantiationOutcome>(
                        Builders<InstantiationOutcome>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OperationId)
                            .Ascending(x => x.NodeKey),
                        new CreateIndexOptions<InstantiationOutcome>
                        {
                            Unique = true,
                            Name = "ux_dm_instantiation_outcomes_tenant_operation_node_active",
                            PartialFilterExpression = Builders<InstantiationOutcome>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<InstantiationOutcome>(
                        Builders<InstantiationOutcome>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.OperationId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.Retryable),
                        new CreateIndexOptions { Name = "ix_dm_instantiation_outcomes_retry" })

            }),
        Collection<ControlledDocument>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementControlledDocuments,
            () => new CreateIndexModel<ControlledDocument>[]
            {
                    new CreateIndexModel<ControlledDocument>(
                        Builders<ControlledDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DocumentKey),
                        new CreateIndexOptions<ControlledDocument>
                        {
                            Unique = true,
                            Name = "ux_dm_controlled_documents_tenant_key_active",
                            PartialFilterExpression = Builders<ControlledDocument>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<ControlledDocument>(
                        Builders<ControlledDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CollectionInstanceId),
                        new CreateIndexOptions { Name = "ix_dm_controlled_documents_collection_instance" }),
                    new CreateIndexModel<ControlledDocument>(
                        Builders<ControlledDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OwnerCompanyId),
                        new CreateIndexOptions { Name = "ix_dm_controlled_documents_owner_company" })

            }),
        Collection<ControlledDocumentVersion>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementControlledDocumentVersions,
            () => new CreateIndexModel<ControlledDocumentVersion>[]
            {
                    new CreateIndexModel<ControlledDocumentVersion>(
                        Builders<ControlledDocumentVersion>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DocumentId).Ascending(x => x.VersionNumber),
                        new CreateIndexOptions<ControlledDocumentVersion>
                        {
                            Unique = true,
                            Name = "ux_dm_controlled_document_versions_tenant_doc_number_active",
                            PartialFilterExpression = Builders<ControlledDocumentVersion>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<TemplateDocument>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemplateDocuments,
            () => new CreateIndexModel<TemplateDocument>[]
            {
                    new CreateIndexModel<TemplateDocument>(
                        Builders<TemplateDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateKey),
                        new CreateIndexOptions<TemplateDocument>
                        {
                            Unique = true,
                            Name = "ux_dm_template_documents_tenant_key_active",
                            PartialFilterExpression = Builders<TemplateDocument>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<TemplateDocument>(
                        Builders<TemplateDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CollectionInstanceId),
                        new CreateIndexOptions { Name = "ix_dm_template_documents_collection_instance" }),
                    new CreateIndexModel<TemplateDocument>(
                        Builders<TemplateDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OwnerCompanyId),
                        new CreateIndexOptions { Name = "ix_dm_template_documents_owner_company" })

            }),
        Collection<TemplateVersion>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemplateVersions,
            () => new CreateIndexModel<TemplateVersion>[]
            {
                    new CreateIndexModel<TemplateVersion>(
                        Builders<TemplateVersion>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateId).Ascending(x => x.VersionNumber),
                        new CreateIndexOptions<TemplateVersion>
                        {
                            Unique = true,
                            Name = "ux_dm_template_versions_tenant_template_number_active",
                            PartialFilterExpression = Builders<TemplateVersion>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<TemplateMaster>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemplateMasters,
            () => new CreateIndexModel<TemplateMaster>[]
            {
                    new CreateIndexModel<TemplateMaster>(
                        Builders<TemplateMaster>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.MasterCode),
                        new CreateIndexOptions<TemplateMaster>
                        {
                            Unique = true,
                            Name = "ux_dm_template_masters_tenant_code_active",
                            PartialFilterExpression = Builders<TemplateMaster>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<TemplateMaster>(
                        Builders<TemplateMaster>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_dm_template_masters_tenant_status" }),
                    new CreateIndexModel<TemplateMaster>(
                        Builders<TemplateMaster>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Classification),
                        new CreateIndexOptions { Name = "ix_dm_template_masters_tenant_classification" }),
                    new CreateIndexModel<TemplateMaster>(
                        Builders<TemplateMaster>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CollectionDefinitionId).Ascending(x => x.CanonicalId),
                        new CreateIndexOptions { Name = "ix_dm_template_masters_collection_canonical" }),
                    new CreateIndexModel<TemplateMaster>(
                        Builders<TemplateMaster>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.VariantPolicy),
                        new CreateIndexOptions { Name = "ix_dm_template_masters_tenant_variant_policy" })

            }),
        Collection<TemplateMasterVersion>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemplateMasterVersions,
            () => new CreateIndexModel<TemplateMasterVersion>[]
            {
                    new CreateIndexModel<TemplateMasterVersion>(
                        Builders<TemplateMasterVersion>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateMasterId).Ascending(x => x.VersionNumber),
                        new CreateIndexOptions<TemplateMasterVersion>
                        {
                            Unique = true,
                            Name = "ux_dm_template_master_versions_tenant_master_number_active",
                            PartialFilterExpression = Builders<TemplateMasterVersion>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<TemplateMasterVersion>(
                        Builders<TemplateMasterVersion>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateMasterId).Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_dm_template_master_versions_master_status" })

            }),
        Collection<TemplateVariant>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemplateVariants,
            () => new CreateIndexModel<TemplateVariant>[]
            {
                    new CreateIndexModel<TemplateVariant>(
                        Builders<TemplateVariant>.IndexKeys
                            .Ascending(x => x.TenantId).Ascending(x => x.ScopeType).Ascending(x => x.ScopeId).Ascending(x => x.VariantCode),
                        new CreateIndexOptions<TemplateVariant>
                        {
                            Unique = true,
                            Name = "ux_dm_template_variants_tenant_scope_code_active",
                            PartialFilterExpression = Builders<TemplateVariant>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<TemplateVariant>(
                        Builders<TemplateVariant>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateMasterId),
                        new CreateIndexOptions { Name = "ix_dm_template_variants_tenant_master" }),
                    new CreateIndexModel<TemplateVariant>(
                        Builders<TemplateVariant>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ScopeType).Ascending(x => x.ScopeId),
                        new CreateIndexOptions { Name = "ix_dm_template_variants_tenant_scope" }),
                    new CreateIndexModel<TemplateVariant>(
                        Builders<TemplateVariant>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_dm_template_variants_tenant_status" }),
                    new CreateIndexModel<TemplateVariant>(
                        Builders<TemplateVariant>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ApprovalStatus),
                        new CreateIndexOptions { Name = "ix_dm_template_variants_tenant_approval_status" }),
                    new CreateIndexModel<TemplateVariant>(
                        Builders<TemplateVariant>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LinkedTemplateDocumentId),
                        new CreateIndexOptions<TemplateVariant>
                        {
                            Name = "ix_dm_template_variants_tenant_linked_document",
                            PartialFilterExpression = Builders<TemplateVariant>.Filter.Exists(x => x.LinkedTemplateDocumentId)
                        })

            }),
        Collection<DocumentAccessPolicyEntry>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementAccessPolicies,
            () => new CreateIndexModel<DocumentAccessPolicyEntry>[]
            {
                    new CreateIndexModel<DocumentAccessPolicyEntry>(
                        Builders<DocumentAccessPolicyEntry>.IndexKeys
                            .Ascending(x => x.TenantId).Ascending(x => x.TargetType).Ascending(x => x.TargetId)
                            .Ascending(x => x.PrincipalType).Ascending(x => x.PrincipalId).Ascending(x => x.Effect),
                        new CreateIndexOptions<DocumentAccessPolicyEntry>
                        {
                            Unique = true,
                            Name = "ux_dm_access_policies_target_principal_effect_active",
                            PartialFilterExpression = Builders<DocumentAccessPolicyEntry>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<DocumentAccessPolicyEntry>(
                        Builders<DocumentAccessPolicyEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TargetType).Ascending(x => x.TargetId),
                        new CreateIndexOptions { Name = "ix_dm_access_policies_tenant_target" }),
                    new CreateIndexModel<DocumentAccessPolicyEntry>(
                        Builders<DocumentAccessPolicyEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PrincipalType).Ascending(x => x.PrincipalId),
                        new CreateIndexOptions { Name = "ix_dm_access_policies_tenant_principal" }),
                    new CreateIndexModel<DocumentAccessPolicyEntry>(
                        Builders<DocumentAccessPolicyEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status),
                        new CreateIndexOptions { Name = "ix_dm_access_policies_tenant_status" }),
                    new CreateIndexModel<DocumentAccessPolicyEntry>(
                        Builders<DocumentAccessPolicyEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Effect),
                        new CreateIndexOptions { Name = "ix_dm_access_policies_tenant_effect" }),
                    new CreateIndexModel<DocumentAccessPolicyEntry>(
                        Builders<DocumentAccessPolicyEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ValidTo),
                        new CreateIndexOptions<DocumentAccessPolicyEntry>
                        {
                            Name = "ix_dm_access_policies_tenant_valid_to",
                            PartialFilterExpression = Builders<DocumentAccessPolicyEntry>.Filter.Exists(x => x.ValidTo)
                        })

            }),
        Collection<FolderDocumentAccessPolicy>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementFolderDocumentAccessPolicies,
            () => new CreateIndexModel<FolderDocumentAccessPolicy>[]
            {
                    new CreateIndexModel<FolderDocumentAccessPolicy>(
                        Builders<FolderDocumentAccessPolicy>.IndexKeys
                            .Ascending(x => x.TenantId).Ascending(x => x.CollectionInstanceId).Ascending(x => x.TargetType).Ascending(x => x.TargetId),
                        new CreateIndexOptions<FolderDocumentAccessPolicy>
                        {
                            Unique = true,
                            Name = "ux_dm_folder_access_policies_tenant_instance_target_active",
                            PartialFilterExpression = Builders<FolderDocumentAccessPolicy>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<DocumentShareRecord>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementDocumentShares,
            () => new CreateIndexModel<DocumentShareRecord>[]
            {
                    new CreateIndexModel<DocumentShareRecord>(
                        Builders<DocumentShareRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ItemKind).Ascending(x => x.ItemId).Ascending(x => x.TargetCompanyId),
                        new CreateIndexOptions { Name = "ix_dm_document_shares_item_target" }),
                    new CreateIndexModel<DocumentShareRecord>(
                        Builders<DocumentShareRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TargetCompanyId),
                        new CreateIndexOptions { Name = "ix_dm_document_shares_target_company" })

            }),
        Collection<FolderShareOperation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementFolderShareOperations,
            () => new CreateIndexModel<FolderShareOperation>[]
            {
                    new CreateIndexModel<FolderShareOperation>(
                        Builders<FolderShareOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
                        new CreateIndexOptions<FolderShareOperation>
                        {
                            Unique = true,
                            Name = "ux_dm_folder_share_operations_tenant_operation_active",
                            PartialFilterExpression = Builders<FolderShareOperation>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<FolderShareOperation>(
                        Builders<FolderShareOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CorrelationId),
                        new CreateIndexOptions { Name = "ix_dm_folder_share_operations_correlation" })

            }),
        Collection<FolderShareOutcome>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementFolderShareOutcomes,
            () => new CreateIndexModel<FolderShareOutcome>[]
            {
                    new CreateIndexModel<FolderShareOutcome>(
                        Builders<FolderShareOutcome>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId).Ascending(x => x.ItemKey),
                        new CreateIndexOptions<FolderShareOutcome>
                        {
                            Unique = true,
                            Name = "ux_dm_folder_share_outcomes_tenant_operation_item_active",
                            PartialFilterExpression = Builders<FolderShareOutcome>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        Collection<DocumentFavorite>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementDocumentFavorites,
            () => new CreateIndexModel<DocumentFavorite>[]
            {
                    new CreateIndexModel<DocumentFavorite>(
                        Builders<DocumentFavorite>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.UserId).Ascending(x => x.DocumentId),
                        new CreateIndexOptions<DocumentFavorite>
                        {
                            Unique = true,
                            Name = "ux_dm_document_favorites_tenant_user_document_active",
                            PartialFilterExpression = Builders<DocumentFavorite>.Filter.Eq(x => x.IsDeleted, false)
                        })

            }),
        /*
         * BL-279 — TWO indexes, sized from ProvisioningEvidenceRepository:
         *
         *   GetByCollectionInstanceAsync {TenantId, IsDeleted:false, CollectionInstanceId} -> FirstOrDefault
         *   GetByBaselineAsync           {TenantId, IsDeleted:false, BaselineReleaseId}
         *
         * ⚠ THE UNIQUE INDEX CLOSES A RACE, IT DOES NOT JUST SPEED A LOOKUP. ProvisioningEvidenceService
         * upserts a node's evidence as read-then-write: GetByCollectionInstance, and insert when nothing came
         * back. Two concurrent read-backs of the same folder therefore both find nothing and both insert, and
         * from then on FirstOrDefault silently returns whichever row Mongo reaches first — the entity's
         * "the same node's evidence is upserted" stops being true and the QA trail forks. Partial on
         * IsDeleted:false so a soft-deleted node can be provisioned again, matching every other ux_dm_*_active
         * index in this profile.
         *
         * ⚠ THIS COLLECTION DOES NOT EXIST IN ANY LIVE DATABASE YET, so the evidence below is from a seeded
         * copy (3,000 rows, 6 tenants x 10 baselines), not from production: 3,000 documents examined before,
         * 1 (instance) and 100 (baseline) after.
         */
        Collection<DocumentCollectionProvisioningEvidence>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCollectionProvisioningEvidence,
            () => new CreateIndexModel<DocumentCollectionProvisioningEvidence>[]
            {
                    new CreateIndexModel<DocumentCollectionProvisioningEvidence>(
                        Builders<DocumentCollectionProvisioningEvidence>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.CollectionInstanceId),
                        new CreateIndexOptions<DocumentCollectionProvisioningEvidence>
                        {
                            Unique = true,
                            Name = "ux_dm_collection_provisioning_evidence_tenant_instance_active",
                            PartialFilterExpression = Builders<DocumentCollectionProvisioningEvidence>.Filter.Eq(x => x.IsDeleted, false)
                        }),
                    new CreateIndexModel<DocumentCollectionProvisioningEvidence>(
                        Builders<DocumentCollectionProvisioningEvidence>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_dm_collection_provisioning_evidence_tenant_baseline" })

            }),
        /*
         * BL-279 — ONE index serving BOTH reads of DocumentCollectionDeviationRepository:
         *
         *   GetByBaselineAsync     {TenantId, IsDeleted:false, BaselineReleaseId}
         *   GetOpenByBaselineAsync {TenantId, IsDeleted:false, BaselineReleaseId, Status:Open}
         *
         * The {TenantId, BaselineReleaseId} prefix answers the first and the Status column narrows the second,
         * so a second index would only duplicate a prefix this one already covers. Measured on the same seeded
         * copy: 3,000 documents examined before, 100 after, on both reads.
         *
         * ⚠ NO UNIQUE INDEX, DELIBERATELY. The entity documents detection as idempotent — "re-running a
         * read-back updates an existing OPEN deviation rather than duplicating it" — but it never names the
         * key that identity is judged on, and the reconciliation service does not read by one. Guessing that
         * key (path + type? path + type + severity?) and enforcing it would turn a lawful second deviation on
         * the same folder into a write that throws in production. Naming it is backlog, not an index.
         */
        Collection<DocumentCollectionDeviation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCollectionDeviations,
            () => new CreateIndexModel<DocumentCollectionDeviation>[]
            {
                    new CreateIndexModel<DocumentCollectionDeviation>(
                        Builders<DocumentCollectionDeviation>.IndexKeys
                            .Ascending(x => x.TenantId)
                            .Ascending(x => x.BaselineReleaseId)
                            .Ascending(x => x.Status)
                            .Ascending(x => x.IsDeleted),
                        new CreateIndexOptions { Name = "ix_dm_collection_deviations_tenant_baseline_status" })
            }),
    };
}
