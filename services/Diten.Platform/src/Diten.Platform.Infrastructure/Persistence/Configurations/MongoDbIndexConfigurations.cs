using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<SavedView>("saved_views");
        var tenantCollection = database.GetCollection<Tenant>("tenants");
        var tenantDomainCollection = database.GetCollection<TenantDomain>("tenant_domains");
        var tenantLoginSettingsCollection = database.GetCollection<TenantLoginSettings>("tenant_login_settings");
        var moduleCatalogCollection = database.GetCollection<ModuleCatalogItem>("platform_module_catalog");
        var moduleDomainCollection = database.GetCollection<ModuleDomain>("platform_module_domains");
        var moduleServiceCollection = database.GetCollection<ModuleService>("platform_module_services");
        var seedMarkerCollection = database.GetCollection<SeedMarker>(SeedMarkerStore.CollectionName);
        var modulePageDescriptorCollection = database.GetCollection<ModulePageDescriptor>("platform_module_page_descriptors");
        var modulePageActionDescriptorCollection = database.GetCollection<ModulePageActionDescriptor>("platform_module_page_action_descriptors");
        var platformAdministratorCollection = database.GetCollection<PlatformAdministrator>("platform_administrators");
        var subscriptionPlanCollection = database.GetCollection<SubscriptionPlan>("platform_subscription_plans");
        var tenantSubscriptionCollection = database.GetCollection<TenantSubscription>("tenant_subscriptions");
        var tenantModuleEntitlementCollection = database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements");
        var tenantNavPreferenceCollection = database.GetCollection<TenantNavPreference>("tenant_nav_preferences");
        var tenantNavDomainPreferenceCollection = database.GetCollection<TenantNavDomainPreference>("tenant_nav_domain_preferences");
        var quotaUsageCollection = database.GetCollection<QuotaUsage>("quota_usages");
        var quotaEventCollection = database.GetCollection<QuotaEvent>("quota_events");
        var featureDefinitionCollection = database.GetCollection<FeatureDefinition>("platform_subscription_features");
        var featureCategoryCollection = database.GetCollection<FeatureCategory>("platform_feature_categories");
        var planFeatureMappingCollection = database.GetCollection<PlanFeatureMapping>("platform_plan_feature_mappings");
        var businessReferenceDataSetCollection = database.GetCollection<BusinessReferenceDataSet>("business_reference_data_sets");
        var businessReferenceDataVersionCollection = database.GetCollection<BusinessReferenceDataVersion>("business_reference_data_versions");
        var businessReferenceDataUsageRegistrationCollection = database.GetCollection<BusinessReferenceDataUsageRegistration>("business_reference_data_usage_registrations");
        var businessReferenceDataImportPreviewCollection = database.GetCollection<BusinessReferenceDataImportPreview>("business_reference_data_import_previews");
        var businessReferenceDataIntegrationEventCollection = database.GetCollection<BusinessReferenceDataIntegrationEvent>("business_reference_data_integration_events");
        var interfaceDefinitionCollection = database.GetCollection<InterfaceDefinition>("platform_interface_definitions");
        var interfaceDiscoveryBatchCollection = database.GetCollection<InterfaceDiscoveryBatch>("platform_interface_discovery_batches");
        var interfaceDiscoveryDiffCollection = database.GetCollection<InterfaceDiscoveryDiffItem>("platform_interface_discovery_diff_items");
        var interfaceActiveSnapshotCollection = database.GetCollection<InterfaceActiveSnapshot>("platform_interface_active_snapshots");
        var auditEventCollection = database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents);
        var auditRetentionPolicyCollection = database.GetCollection<AuditEventRetentionPolicy>(AuditCollectionNames.AuditEventRetentionPolicies);
        var tenantAuditPreferenceCollection = database.GetCollection<TenantAuditPreference>(AuditCollectionNames.TenantAuditPreferences);
        var auditOutboxCollection = database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox);
        var outboxEventCollection = database.GetCollection<OutboxEvent>("outbox_events");
        var consumedEventCollection = database.GetCollection<ConsumedEvent>("consumed_events");
        var jobExecutionLogCollection = database.GetCollection<JobExecutionLog>("job_execution_logs");
        var tenantMessagingSettingsCollection = database.GetCollection<TenantMessagingSettings>("notification_tenant_messaging_settings");
        var notificationTemplateCollection = database.GetCollection<NotificationTemplate>("notification_templates");
        var notificationDispatchCollection = database.GetCollection<NotificationDispatch>("notification_dispatches");
        var organizationUnitCollection = database.GetCollection<OrganizationUnit>("organization_units");
        var positionCollection = database.GetCollection<Position>("positions");
        var positionAssignmentCollection = database.GetCollection<PositionAssignment>("position_assignments");
        var moduleCatalogDocuments = database.GetCollection<BsonDocument>("platform_module_catalog");
        var baselineReleaseCollection = database.GetCollection<BaselineRelease>("document_management_baseline_releases");
        var collectionDefinitionCollection = database.GetCollection<CollectionDefinition>("document_management_collection_definitions");
        var baselineSnapshotManifestCollection = database.GetCollection<BaselineSnapshotManifest>("document_management_baseline_snapshot_manifests");
        var collectionInstanceCollection = database.GetCollection<CollectionInstance>("document_management_collection_instances");
        var instantiationOperationCollection = database.GetCollection<InstantiationOperation>("document_management_instantiation_operations");
        var instantiationOutcomeCollection = database.GetCollection<InstantiationOutcome>("document_management_instantiation_outcomes");
        // MOD-0029-FU01 — controlled documents / templates / versions / shares.
        var controlledDocumentCollection = database.GetCollection<ControlledDocument>("document_management_controlled_documents");
        var controlledDocumentVersionCollection = database.GetCollection<ControlledDocumentVersion>("document_management_controlled_document_versions");
        var templateDocumentCollection = database.GetCollection<TemplateDocument>("document_management_template_documents");
        var templateVersionCollection = database.GetCollection<TemplateVersion>("document_management_template_versions");
        var templateMasterCollection = database.GetCollection<TemplateMaster>("document_management_template_masters");
        var templateMasterVersionCollection = database.GetCollection<TemplateMasterVersion>("document_management_template_master_versions");
        var templateVariantCollection = database.GetCollection<TemplateVariant>("document_management_template_variants");
        var documentAccessPolicyCollection = database.GetCollection<DocumentAccessPolicyEntry>("document_management_access_policies");
        var folderDocumentAccessPolicyCollection = database.GetCollection<FolderDocumentAccessPolicy>("document_management_folder_document_access_policies");
        var documentShareCollection = database.GetCollection<DocumentShareRecord>("document_management_document_shares");
        var folderShareOperationCollection = database.GetCollection<FolderShareOperation>("document_management_folder_share_operations");
        var folderShareOutcomeCollection = database.GetCollection<FolderShareOutcome>("document_management_folder_share_outcomes");
        var documentFavoriteCollection = database.GetCollection<DocumentFavorite>("document_management_document_favorites");
        var workflowTemplateCollection = database.GetCollection<WorkflowTemplate>("workflow_templates");
        var workflowTemplateVersionCollection = database.GetCollection<WorkflowTemplateVersion>("workflow_template_versions");
        var workflowInstanceCollection = database.GetCollection<WorkflowInstance>("workflow_instances");
        var approvalTaskCollection = database.GetCollection<ApprovalTask>("approval_tasks");
        var runtimeAssignmentSnapshotCollection = database.GetCollection<RuntimeAssignmentSnapshot>("workflow_runtime_assignment_snapshots");
        var workflowTransitionLogCollection = database.GetCollection<WorkflowTransitionLog>("workflow_transition_logs");
        var workflowSlaRuleCollection = database.GetCollection<SlaEscalationRule>("workflow_sla_rules");

        await baselineReleaseCollection.Indexes.CreateManyAsync(new[]
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
        });

        await collectionDefinitionCollection.Indexes.CreateManyAsync(new[]
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
        });

        await baselineSnapshotManifestCollection.Indexes.CreateManyAsync(new[]
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
        });

        await collectionInstanceCollection.Indexes.CreateManyAsync(new[]
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
        });

        await instantiationOperationCollection.Indexes.CreateManyAsync(new[]
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
        });

        await instantiationOutcomeCollection.Indexes.CreateManyAsync(new[]
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
        });

        // MOD-0029-FU01 — controlled documents / templates / versions / shares. Tenant-first compound indexes;
        // unique constraints tenant-scoped and partial on IsDeleted == false (no hard delete).
        await controlledDocumentCollection.Indexes.CreateManyAsync(new[]
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
        });

        await controlledDocumentVersionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ControlledDocumentVersion>(
                Builders<ControlledDocumentVersion>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DocumentId).Ascending(x => x.VersionNumber),
                new CreateIndexOptions<ControlledDocumentVersion>
                {
                    Unique = true,
                    Name = "ux_dm_controlled_document_versions_tenant_doc_number_active",
                    PartialFilterExpression = Builders<ControlledDocumentVersion>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await templateDocumentCollection.Indexes.CreateManyAsync(new[]
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
        });

        await templateVersionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TemplateVersion>(
                Builders<TemplateVersion>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateId).Ascending(x => x.VersionNumber),
                new CreateIndexOptions<TemplateVersion>
                {
                    Unique = true,
                    Name = "ux_dm_template_versions_tenant_template_number_active",
                    PartialFilterExpression = Builders<TemplateVersion>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        // MOD-0029-FU02 — corporate template master library. Tenant-first indexes; unique constraints are
        // partial on IsDeleted == false so soft-deleted records do not block reuse.
        await templateMasterCollection.Indexes.CreateManyAsync(new[]
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
        });

        await templateMasterVersionCollection.Indexes.CreateManyAsync(new[]
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
        });

        // MOD-0029-FU03 — template variant governance + drift. Unique on tenant + scope + code is partial on
        // IsDeleted == false so soft-deleted records do not block reuse of a variant code within a scope.
        await templateVariantCollection.Indexes.CreateManyAsync(new[]
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
        });

        // MOD-0029-FU04 — document access matrix policies. Target/principal lookups drive the effective-access
        // resolver; the unique key prevents duplicate (target + principal + effect) rows among non-deleted policies.
        await documentAccessPolicyCollection.Indexes.CreateManyAsync(new[]
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
        });

        await folderDocumentAccessPolicyCollection.Indexes.CreateManyAsync(new[]
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
        });

        await documentShareCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentShareRecord>(
                Builders<DocumentShareRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ItemKind).Ascending(x => x.ItemId).Ascending(x => x.TargetCompanyId),
                new CreateIndexOptions { Name = "ix_dm_document_shares_item_target" }),
            new CreateIndexModel<DocumentShareRecord>(
                Builders<DocumentShareRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TargetCompanyId),
                new CreateIndexOptions { Name = "ix_dm_document_shares_target_company" })
        });

        await folderShareOperationCollection.Indexes.CreateManyAsync(new[]
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
        });

        await folderShareOutcomeCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<FolderShareOutcome>(
                Builders<FolderShareOutcome>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId).Ascending(x => x.ItemKey),
                new CreateIndexOptions<FolderShareOutcome>
                {
                    Unique = true,
                    Name = "ux_dm_folder_share_outcomes_tenant_operation_item_active",
                    PartialFilterExpression = Builders<FolderShareOutcome>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await documentFavoriteCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentFavorite>(
                Builders<DocumentFavorite>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.UserId).Ascending(x => x.DocumentId),
                new CreateIndexOptions<DocumentFavorite>
                {
                    Unique = true,
                    Name = "ux_dm_document_favorites_tenant_user_document_active",
                    PartialFilterExpression = Builders<DocumentFavorite>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        // MOD-0023 Batch 01 — workflow engine, tenant-first compound indexes. Unique constraints are
        // tenant-scoped and partial on IsDeleted == false so soft-deleted rows never block re-use.
        await workflowTemplateCollection.Indexes.CreateManyAsync(new[]
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
        });

        await workflowTemplateVersionCollection.Indexes.CreateManyAsync(new[]
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
        });

        await workflowInstanceCollection.Indexes.CreateManyAsync(new[]
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
        });

        await approvalTaskCollection.Indexes.CreateManyAsync(new[]
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
        });

        await runtimeAssignmentSnapshotCollection.Indexes.CreateManyAsync(new[]
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
        });

        await DropIndexIfExistsAsync(workflowTransitionLogCollection.Indexes, "ix_workflow_transition_logs_tenant_instance_sequence");
        await workflowTransitionLogCollection.Indexes.CreateManyAsync(new[]
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
        });

        await workflowSlaRuleCollection.Indexes.CreateManyAsync(new[]
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
        });

        await organizationUnitCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<OrganizationUnit>(
                Builders<OrganizationUnit>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<OrganizationUnit>
                {
                    Unique = true,
                    Name = "ux_organization_units_tenant_code_active",
                    PartialFilterExpression = Builders<OrganizationUnit>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<OrganizationUnit>(
                Builders<OrganizationUnit>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.ParentOrganizationUnitId)
                    .Ascending(x => x.IsDeleted)
                    .Ascending(x => x.IsArchived),
                new CreateIndexOptions { Name = "ix_organization_units_tree_scope" })
        });

        await positionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Position>(
                Builders<Position>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<Position>
                {
                    Unique = true,
                    Name = "ux_positions_tenant_code_active",
                    PartialFilterExpression = Builders<Position>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<Position>(
                Builders<Position>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.ReportsToPositionId)
                    .Ascending(x => x.IsDeleted)
                    .Ascending(x => x.IsArchived),
                new CreateIndexOptions { Name = "ix_positions_org_reporting_scope" })
        });

        await positionAssignmentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PositionAssignment>(
                Builders<PositionAssignment>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PositionId)
                    .Ascending(x => x.EffectiveFrom)
                    .Ascending(x => x.EffectiveTo)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_position_assignments_position_interval" }),
            new CreateIndexModel<PositionAssignment>(
                Builders<PositionAssignment>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.EffectiveFrom)
                    .Ascending(x => x.EffectiveTo)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_position_assignments_user_interval" })
        });
        await outboxEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<OutboxEvent>(
                Builders<OutboxEvent>.IndexKeys.Ascending(x => x.EventId),
                new CreateIndexOptions { Unique = true, Name = "ux_outbox_events_event_id" }),
            new CreateIndexModel<OutboxEvent>(
                Builders<OutboxEvent>.IndexKeys.Ascending(x => x.EventName),
                new CreateIndexOptions { Name = "ix_outbox_events_event_name" }),
            new CreateIndexModel<OutboxEvent>(
                Builders<OutboxEvent>.IndexKeys.Ascending(x => x.CorrelationId),
                new CreateIndexOptions { Name = "ix_outbox_events_correlation_id" }),
            new CreateIndexModel<OutboxEvent>(
                Builders<OutboxEvent>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.NextAttemptAtUtc),
                new CreateIndexOptions { Name = "ix_outbox_events_status_next_attempt" })
        });

        await consumedEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ConsumedEvent>(
                Builders<ConsumedEvent>.IndexKeys
                    .Ascending(x => x.EventId)
                    .Ascending(x => x.ConsumerName),
                new CreateIndexOptions { Unique = true, Name = "ux_consumed_events_event_consumer" })
        });

        await jobExecutionLogCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<JobExecutionLog>(
                Builders<JobExecutionLog>.IndexKeys
                    .Ascending(x => x.ServiceName)
                    .Ascending(x => x.JobName)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_job_execution_logs_service_job_started" }),
            new CreateIndexModel<JobExecutionLog>(
                Builders<JobExecutionLog>.IndexKeys.Ascending(x => x.CorrelationId),
                new CreateIndexOptions { Name = "ix_job_execution_logs_correlation_id" }),
            new CreateIndexModel<JobExecutionLog>(
                Builders<JobExecutionLog>.IndexKeys
                    .Ascending(x => x.Status)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_job_execution_logs_status_started" }),
            new CreateIndexModel<JobExecutionLog>(
                Builders<JobExecutionLog>.IndexKeys.Ascending(x => x.RecurringJobId),
                new CreateIndexOptions { Name = "ix_job_execution_logs_recurring_job_id" })
        });

        await tenantMessagingSettingsCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantMessagingSettings>(
                Builders<TenantMessagingSettings>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderCode)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_notification_settings_tenant_provider_deleted" }),
            new CreateIndexModel<TenantMessagingSettings>(
                Builders<TenantMessagingSettings>.IndexKeys
                    .Ascending(x => x.IsPlatformDefault)
                    .Ascending(x => x.IsEnabled)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_notification_settings_platform_default_active" }),
            new CreateIndexModel<TenantMessagingSettings>(
                Builders<TenantMessagingSettings>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsPlatformDefault),
                new CreateIndexOptions<TenantMessagingSettings>
                {
                    Unique = true,
                    Name = "ux_notification_settings_scope",
                    PartialFilterExpression = Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await notificationTemplateCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<NotificationTemplate>(
                Builders<NotificationTemplate>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsPlatformDefault)
                    .Ascending(x => x.Locale)
                    .Ascending(x => x.Channel)
                    .Ascending(x => x.TemplateKey)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_notification_templates_scope_locale_channel_key_deleted" }),
            new CreateIndexModel<NotificationTemplate>(
                Builders<NotificationTemplate>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsPlatformDefault)
                    .Ascending(x => x.Locale)
                    .Ascending(x => x.Channel)
                    .Ascending(x => x.TemplateKey),
                new CreateIndexOptions<NotificationTemplate>
                {
                    Unique = true,
                    Name = "ux_notification_templates_active_scope_locale_channel_key",
                    PartialFilterExpression = Builders<NotificationTemplate>.Filter.And(
                        Builders<NotificationTemplate>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<NotificationTemplate>.Filter.Eq(x => x.Status, Domain.Enums.NotificationTemplateStatus.Active))
                }),
            new CreateIndexModel<NotificationTemplate>(
                Builders<NotificationTemplate>.IndexKeys
                    .Ascending(x => x.IsPlatformDefault)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Locale)
                    .Ascending(x => x.Channel)
                    .Ascending(x => x.TemplateKey),
                new CreateIndexOptions { Name = "ix_notification_templates_default_resolution" })
        });

        await notificationDispatchCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<NotificationDispatch>(
                Builders<NotificationDispatch>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_notification_dispatches_tenant_deleted" }),
            new CreateIndexModel<NotificationDispatch>(
                Builders<NotificationDispatch>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Descending(x => x.QueuedAt),
                new CreateIndexOptions { Name = "ix_notification_dispatches_tenant_status_queued" }),
            new CreateIndexModel<NotificationDispatch>(
                Builders<NotificationDispatch>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.TemplateKey),
                new CreateIndexOptions { Name = "ix_notification_dispatches_tenant_template" }),
            new CreateIndexModel<NotificationDispatch>(
                Builders<NotificationDispatch>.IndexKeys.Ascending(x => x.ProviderMessageId),
                new CreateIndexOptions<NotificationDispatch>
                {
                    Name = "ix_notification_dispatches_provider_message_id",
                    PartialFilterExpression = Builders<NotificationDispatch>.Filter.Exists(x => x.ProviderMessageId, true)
                }),
            new CreateIndexModel<NotificationDispatch>(
                Builders<NotificationDispatch>.IndexKeys.Ascending(x => x.CorrelationId),
                new CreateIndexOptions<NotificationDispatch>
                {
                    Name = "ix_notification_dispatches_correlation_id",
                    PartialFilterExpression = Builders<NotificationDispatch>.Filter.Exists(x => x.CorrelationId, true)
                }),
            new CreateIndexModel<NotificationDispatch>(
                Builders<NotificationDispatch>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.NextRetryAt),
                new CreateIndexOptions<NotificationDispatch>
                {
                    Name = "ix_notification_dispatches_retry_sweep",
                    PartialFilterExpression = Builders<NotificationDispatch>.Filter.And(
                        Builders<NotificationDispatch>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<NotificationDispatch>.Filter.Eq(x => x.Status, Domain.Enums.NotificationDispatchStatus.Failed),
                        Builders<NotificationDispatch>.Filter.Exists(x => x.NextRetryAt, true))
                })
        });

        await collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.Status)),
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.IsDefault)
                    .Ascending(x => x.Status)),
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.ViewName)
                    .Ascending(x => x.Status))
        });

        await SoftDeleteDomainsForDeletedTenantsAsync(tenantCollection, tenantDomainCollection);
        await DropIndexIfExistsAsync(tenantCollection.Indexes, "ux_tenants_code");
        await DropIndexIfExistsAsync(tenantCollection.Indexes, "ux_tenants_slug");
        await DropIndexIfExistsAsync(tenantCollection.Indexes, "ux_tenants_domain");
        await DropIndexIfExistsAsync(tenantDomainCollection.Indexes, "ux_tenant_domains_domain_name");

        await tenantCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions<Tenant>
                {
                    Unique = true,
                    Name = "ux_tenants_code",
                    PartialFilterExpression = Builders<Tenant>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Slug),
                new CreateIndexOptions<Tenant>
                {
                    Unique = true,
                    Name = "ux_tenants_slug",
                    PartialFilterExpression = Builders<Tenant>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Domain),
                new CreateIndexOptions<Tenant>
                {
                    Unique = true,
                    Name = "ux_tenants_domain",
                    PartialFilterExpression = Builders<Tenant>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Region)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_tenants_status_region_createdat" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.PlanId),
                new CreateIndexOptions { Name = "ix_tenants_plan_id" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.SubscriptionStatus),
                new CreateIndexOptions { Name = "ix_tenants_subscription_status" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.TrialEndDateUtc),
                new CreateIndexOptions { Name = "ix_tenants_trial_end_date_utc" })
        });

        await tenantDomainCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantDomain>(
                Builders<TenantDomain>.IndexKeys.Ascending(x => x.DomainName),
                new CreateIndexOptions<TenantDomain>
                {
                    Unique = true,
                    Name = "ux_tenant_domains_domain_name",
                    PartialFilterExpression = Builders<TenantDomain>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TenantDomain>(
                Builders<TenantDomain>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsPrimary),
                new CreateIndexOptions { Name = "ix_tenant_domains_tenantid_primary" }),
            new CreateIndexModel<TenantDomain>(
                Builders<TenantDomain>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_domains_tenantid_status" })
        });

        await tenantLoginSettingsCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantLoginSettings>(
                Builders<TenantLoginSettings>.IndexKeys.Ascending(x => x.TenantRefId),
                new CreateIndexOptions { Unique = true, Name = "ux_tenant_login_settings_tenant_ref_id" })
        });

        // Eski non-partial unique index'i düşür; aksi halde aynı isimle partial yeniden oluşturmak IndexOptionsConflict verir.
        await DropIndexIfExistsAsync(moduleCatalogCollection.Indexes, "ux_platform_module_catalog_module_code");
        await moduleCatalogCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.ModuleCode),
                new CreateIndexOptions<ModuleCatalogItem>
                {
                    Unique = true,
                    Name = "ux_platform_module_catalog_module_code",
                    // Uniqueness yalnız canlı kayıtlar arasında geçerli; soft-deleted kod aynı kodla yeni insert'i bloke etmez.
                    PartialFilterExpression = Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_status" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Domain),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_domain" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.Service),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_service" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.IsTenantAssignable),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_assignable" }),
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_module_catalog_sort_order" })
        });
        await DropIndexIfExistsAsync(moduleCatalogCollection.Indexes, "ix_platform_module_catalog_category");
        await moduleCatalogDocuments.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("Category"),
            Builders<BsonDocument>.Update.Unset("Category"));

        // FIX-DOMAIN-DEDUP — uniqueness moves from the raw Code to the NORMALIZED CodeKey (UPPERCASE, no separators)
        // so two live rows can never share a domain that differs only by separators/case
        // (e.g. "MASTER-DATA-MANAGEMENT" vs "MASTERDATAMANAGEMENT"). ModuleDomainDeduplicationMigration runs BEFORE
        // this (in DI startup) to collapse existing duplicates and backfill CodeKey, so the index builds cleanly.
        // The old Code-based unique index is dropped.
        await DropIndexIfExistsAsync(moduleDomainCollection.Indexes, "ux_platform_module_domains_code");
        await DropIndexIfExistsAsync(moduleDomainCollection.Indexes, "ux_platform_module_domains_code_key");
        // Non-unique indexes first (always safe to (re)build).
        await moduleDomainCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModuleDomain>(
                Builders<ModuleDomain>.IndexKeys.Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_platform_module_domains_active" }),
            new CreateIndexModel<ModuleDomain>(
                Builders<ModuleDomain>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_module_domains_sort_order" })
        });

        // The unique CodeKey index is built in its OWN call, AFTER ModuleDomainDeduplicationMigration has
        // deduped + backfilled CodeKey on every live row. If it still fails (e.g. a null/empty CodeKey slipped
        // through), surface it LOUDLY and rethrow — the OLD Code-based unique index was already dropped, so a
        // swallowed failure here would leave platform_module_domains with NO uniqueness protection at all.
        try
        {
            await moduleDomainCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ModuleDomain>(
                    Builders<ModuleDomain>.IndexKeys.Ascending(x => x.CodeKey),
                    new CreateIndexOptions<ModuleDomain>
                    {
                        Unique = true,
                        Name = "ux_platform_module_domains_code_key",
                        // Uniqueness yalnız canlı kayıtlar arası — soft-deleted kod aynı normalized key ile yeni insert'i bloke etmez.
                        PartialFilterExpression = Builders<ModuleDomain>.Filter.Eq(x => x.IsDeleted, false)
                    }));
        }
        catch (MongoException ex)
        {
            Console.Error.WriteLine(
                "[MongoDbIndexConfigurations] ERROR: failed to build unique index 'ux_platform_module_domains_code_key' on " +
                $"platform_module_domains: {ex.Message}. The collection currently has NO domain-code uniqueness protection " +
                "(likely a null/empty CodeKey — check the ModuleDomainDeduplicationMigration log).");
            throw;
        }

        await DropIndexIfExistsAsync(moduleServiceCollection.Indexes, "ux_platform_module_services_code");
        await moduleServiceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModuleService>(
                Builders<ModuleService>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions<ModuleService>
                {
                    Unique = true,
                    Name = "ux_platform_module_services_code",
                    // UI #C3e: uniqueness yalnız canlı kayıtlar arası — soft-deleted kod aynı kodla yeni insert'i bloke etmez.
                    PartialFilterExpression = Builders<ModuleService>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ModuleService>(
                Builders<ModuleService>.IndexKeys.Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_platform_module_services_active" }),
            new CreateIndexModel<ModuleService>(
                Builders<ModuleService>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_module_services_sort_order" })
        });

        await seedMarkerCollection.Indexes.CreateOneAsync(new CreateIndexModel<SeedMarker>(
            Builders<SeedMarker>.IndexKeys.Ascending(x => x.Key),
            new CreateIndexOptions { Unique = true, Name = "ux_platform_seed_markers_key" }));

        // FIX C: page-code + route-path uniqueness must be PARTIAL (live-only), mirroring the catalog C3e pattern, so a
        // soft-deleted page frees its route/pagecode → operator/manifest can re-open the same route (reclaim works).
        await DropIndexIfExistsAsync(modulePageDescriptorCollection.Indexes, "ux_platform_module_pages_tenant_module_page_code");
        await DropIndexIfExistsAsync(modulePageDescriptorCollection.Indexes, "ux_platform_module_pages_tenant_module_route_path");
        await modulePageDescriptorCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.PageCode),
                new CreateIndexOptions<ModulePageDescriptor>
                {
                    Unique = true,
                    Name = "ux_platform_module_pages_tenant_module_page_code",
                    PartialFilterExpression = Builders<ModulePageDescriptor>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.RoutePath),
                new CreateIndexOptions<ModulePageDescriptor>
                {
                    Unique = true,
                    Name = "ux_platform_module_pages_tenant_module_route_path",
                    PartialFilterExpression = Builders<ModulePageDescriptor>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_module_pages_tenant_module_sort_order" }),
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_platform_module_pages_tenant_status" }),
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PageType),
                new CreateIndexOptions { Name = "ix_platform_module_pages_tenant_page_type" })
        });

        // FIX C: action-code uniqueness PARTIAL (live-only) too — a soft-deleted action frees its (page, actionCode).
        await DropIndexIfExistsAsync(modulePageActionDescriptorCollection.Indexes, "ux_platform_module_page_actions_tenant_page_action");
        await modulePageActionDescriptorCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModulePageActionDescriptor>(
                Builders<ModulePageActionDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PageDescriptorId)
                    .Ascending(x => x.ActionCode),
                new CreateIndexOptions<ModulePageActionDescriptor>
                {
                    Unique = true,
                    Name = "ux_platform_module_page_actions_tenant_page_action",
                    PartialFilterExpression = Builders<ModulePageActionDescriptor>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ModulePageActionDescriptor>(
                Builders<ModulePageActionDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.PageCode),
                new CreateIndexOptions { Name = "ix_platform_module_page_actions_tenant_module_page" }),
            new CreateIndexModel<ModulePageActionDescriptor>(
                Builders<ModulePageActionDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PermissionKey),
                new CreateIndexOptions { Name = "ix_platform_module_page_actions_tenant_permission" })
        });

        await platformAdministratorCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PlatformAdministrator>(
                Builders<PlatformAdministrator>.IndexKeys.Ascending(x => x.NormalizedEmail),
                new CreateIndexOptions<PlatformAdministrator>
                {
                    Unique = true,
                    Name = "ux_platform_administrators_normalized_email",
                    PartialFilterExpression = Builders<PlatformAdministrator>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PlatformAdministrator>(
                Builders<PlatformAdministrator>.IndexKeys.Ascending(x => x.NormalizedUserName),
                new CreateIndexOptions<PlatformAdministrator>
                {
                    Unique = true,
                    Name = "ux_platform_administrators_normalized_username",
                    PartialFilterExpression = Builders<PlatformAdministrator>.Filter.And(
                        Builders<PlatformAdministrator>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<PlatformAdministrator>.Filter.Exists(x => x.NormalizedUserName, true),
                        Builders<PlatformAdministrator>.Filter.Gt(x => x.NormalizedUserName, string.Empty))
                }),
            new CreateIndexModel<PlatformAdministrator>(
                Builders<PlatformAdministrator>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.ActorType),
                new CreateIndexOptions { Name = "ix_platform_administrators_status_actor_type" }),
            new CreateIndexModel<PlatformAdministrator>(
                Builders<PlatformAdministrator>.IndexKeys.Ascending(x => x.PartnerId),
                new CreateIndexOptions { Name = "ix_platform_administrators_partner_id" })
        });

        await subscriptionPlanCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_subscription_plans_code" }),
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_platform_subscription_plans_is_active" }),
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.IsTrialPlan),
                new CreateIndexOptions { Name = "ix_platform_subscription_plans_is_trial_plan" }),
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_subscription_plans_sort_order" }),
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.IsDefault),
                new CreateIndexOptions { Name = "ix_platform_subscription_plans_is_default" }),
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.IsDefault).Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_platform_subscription_plans_is_default_is_active" }),
            new CreateIndexModel<SubscriptionPlan>(
                Builders<SubscriptionPlan>.IndexKeys.Ascending(x => x.IncludedModuleKeys),
                new CreateIndexOptions { Name = "ix_platform_subscription_plans_included_module_keys" })
        });

        await tenantSubscriptionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantSubscription>(
                Builders<TenantSubscription>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_subscriptions_tenant_status" }),
            new CreateIndexModel<TenantSubscription>(
                Builders<TenantSubscription>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PlanId),
                new CreateIndexOptions { Name = "ix_tenant_subscriptions_tenant_plan" }),
            new CreateIndexModel<TenantSubscription>(
                Builders<TenantSubscription>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions<TenantSubscription>
                {
                    Unique = true,
                    Name = "ux_tenant_subscriptions_one_current",
                    PartialFilterExpression = Builders<TenantSubscription>.Filter.And(
                        Builders<TenantSubscription>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<TenantSubscription>.Filter.In(x => x.Status, TenantSubscriptionStatuses.Current))
                })
        });

        await tenantModuleEntitlementCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantModuleEntitlement>(
                Builders<TenantModuleEntitlement>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode),
                new CreateIndexOptions { Name = "ix_tenant_module_entitlements_tenant_module" }),
            new CreateIndexModel<TenantModuleEntitlement>(
                Builders<TenantModuleEntitlement>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.Source),
                new CreateIndexOptions<TenantModuleEntitlement>
                {
                    Unique = true,
                    Name = "ux_tenant_module_entitlements_active_source",
                    PartialFilterExpression = Builders<TenantModuleEntitlement>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TenantModuleEntitlement>(
                Builders<TenantModuleEntitlement>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Source),
                new CreateIndexOptions { Name = "ix_tenant_module_entitlements_tenant_source" }),
            new CreateIndexModel<TenantModuleEntitlement>(
                Builders<TenantModuleEntitlement>.IndexKeys.Ascending(x => x.ExpiryDateUtc),
                new CreateIndexOptions { Name = "ix_tenant_module_entitlements_expiry" })
        });

        await tenantNavPreferenceCollection.Indexes.CreateManyAsync(new[]
        {
            // FEAT-TENANT-NAV-PREFS — one live preference per (TenantId, ModuleCode).
            new CreateIndexModel<TenantNavPreference>(
                Builders<TenantNavPreference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode),
                new CreateIndexOptions<TenantNavPreference>
                {
                    Unique = true,
                    Name = "ux_tenant_nav_preferences_tenant_module",
                    PartialFilterExpression = Builders<TenantNavPreference>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await tenantNavDomainPreferenceCollection.Indexes.CreateManyAsync(new[]
        {
            // FEAT-NAVPREFS-DOMAINS — one live preference per (TenantId, DomainCode).
            new CreateIndexModel<TenantNavDomainPreference>(
                Builders<TenantNavDomainPreference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.DomainCode),
                new CreateIndexOptions<TenantNavDomainPreference>
                {
                    Unique = true,
                    Name = "ux_tenant_nav_domain_preferences_tenant_domain",
                    PartialFilterExpression = Builders<TenantNavDomainPreference>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await quotaUsageCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<QuotaUsage>(
                Builders<QuotaUsage>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.QuotaKey),
                new CreateIndexOptions<QuotaUsage>
                {
                    Unique = true,
                    Name = "ux_quota_usages_tenant_quota_key",
                    PartialFilterExpression = Builders<QuotaUsage>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<QuotaUsage>(
                Builders<QuotaUsage>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PeriodEnd),
                new CreateIndexOptions { Name = "ix_quota_usages_tenant_period_end" })
        });

        await quotaEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<QuotaEvent>(
                Builders<QuotaEvent>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.QuotaKey)
                    .Ascending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_quota_events_tenant_quota_occurred" }),
            new CreateIndexModel<QuotaEvent>(
                Builders<QuotaEvent>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.QuotaKey)
                    .Ascending(x => x.Source)
                    .Ascending(x => x.OperationId)
                    .Ascending(x => x.SourceReference)
                    .Ascending(x => x.IsRejected),
                new CreateIndexOptions { Name = "ix_quota_events_idempotency" })
        });

        await featureDefinitionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<FeatureDefinition>(
                Builders<FeatureDefinition>.IndexKeys.Ascending(x => x.FeatureCode),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_subscription_features_code" }),
            new CreateIndexModel<FeatureDefinition>(
                Builders<FeatureDefinition>.IndexKeys.Ascending(x => x.FeatureSlug),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_subscription_features_slug" }),
            new CreateIndexModel<FeatureDefinition>(
                Builders<FeatureDefinition>.IndexKeys.Ascending(x => x.CategoryId),
                new CreateIndexOptions { Name = "ix_platform_subscription_features_category_id" }),
            new CreateIndexModel<FeatureDefinition>(
                Builders<FeatureDefinition>.IndexKeys.Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_platform_subscription_features_status" }),
            new CreateIndexModel<FeatureDefinition>(
                Builders<FeatureDefinition>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_subscription_features_sort_order" })
        });

        await featureCategoryCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<FeatureCategory>(
                Builders<FeatureCategory>.IndexKeys.Ascending(x => x.CategoryCode),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_feature_categories_code" }),
            new CreateIndexModel<FeatureCategory>(
                Builders<FeatureCategory>.IndexKeys.Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_platform_feature_categories_status" }),
            new CreateIndexModel<FeatureCategory>(
                Builders<FeatureCategory>.IndexKeys.Ascending(x => x.SortOrder),
                new CreateIndexOptions { Name = "ix_platform_feature_categories_sort_order" })
        });

        await businessReferenceDataSetCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataSet>(
                Builders<BusinessReferenceDataSet>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SetCode),
                new CreateIndexOptions<BusinessReferenceDataSet>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_sets_tenant_code",
                    PartialFilterExpression = Builders<BusinessReferenceDataSet>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<BusinessReferenceDataSet>(
                Builders<BusinessReferenceDataSet>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_business_reference_data_sets_tenant_status_deleted" })
        });

        await businessReferenceDataVersionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataVersion>(
                Builders<BusinessReferenceDataVersion>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.BusinessReferenceDataSetId)
                    .Ascending(x => x.VersionNumber),
                new CreateIndexOptions<BusinessReferenceDataVersion>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_versions_set_number",
                    PartialFilterExpression = Builders<BusinessReferenceDataVersion>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<BusinessReferenceDataVersion>(
                Builders<BusinessReferenceDataVersion>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_business_reference_data_versions_tenant_status_deleted" })
        });

        await businessReferenceDataUsageRegistrationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataUsageRegistration>(
                Builders<BusinessReferenceDataUsageRegistration>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SetCode)
                    .Ascending(x => x.ConsumerModule)
                    .Ascending(x => x.ConsumerName),
                new CreateIndexOptions<BusinessReferenceDataUsageRegistration>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_usage_consumer",
                    PartialFilterExpression = Builders<BusinessReferenceDataUsageRegistration>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await businessReferenceDataImportPreviewCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataImportPreview>(
                Builders<BusinessReferenceDataImportPreview>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PreviewId),
                new CreateIndexOptions<BusinessReferenceDataImportPreview>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_import_previews_tenant_id",
                    PartialFilterExpression = Builders<BusinessReferenceDataImportPreview>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<BusinessReferenceDataImportPreview>(
                Builders<BusinessReferenceDataImportPreview>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ix_business_reference_data_import_previews_expiry" })
        });

        await businessReferenceDataIntegrationEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataIntegrationEvent>(
                Builders<BusinessReferenceDataIntegrationEvent>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.BusinessReferenceDataVersionId)
                    .Ascending(x => x.EventName)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<BusinessReferenceDataIntegrationEvent>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_events_idempotency",
                    PartialFilterExpression = Builders<BusinessReferenceDataIntegrationEvent>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await planFeatureMappingCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PlanFeatureMapping>(
                Builders<PlanFeatureMapping>.IndexKeys
                    .Ascending(x => x.SubscriptionPlanId)
                    .Ascending(x => x.FeatureDefinitionId),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_plan_feature_mappings_plan_feature" }),
            new CreateIndexModel<PlanFeatureMapping>(
                Builders<PlanFeatureMapping>.IndexKeys.Ascending(x => x.SubscriptionPlanId),
                new CreateIndexOptions { Name = "ix_platform_plan_feature_mappings_plan_id" }),
            new CreateIndexModel<PlanFeatureMapping>(
                Builders<PlanFeatureMapping>.IndexKeys.Ascending(x => x.FeatureDefinitionId),
                new CreateIndexOptions { Name = "ix_platform_plan_feature_mappings_feature_id" }),
            new CreateIndexModel<PlanFeatureMapping>(
                Builders<PlanFeatureMapping>.IndexKeys.Ascending(x => x.AvailabilityStatus),
                new CreateIndexOptions { Name = "ix_platform_plan_feature_mappings_availability_status" })
        });

        await interfaceDefinitionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<InterfaceDefinition>(
                Builders<InterfaceDefinition>.IndexKeys
                    .Ascending(x => x.InterfaceCode)
                    .Ascending(x => x.InterfaceVersion),
                new CreateIndexOptions<InterfaceDefinition>
                {
                    Unique = true,
                    Name = "ux_platform_interface_definitions_code_version",
                    PartialFilterExpression = Builders<InterfaceDefinition>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<InterfaceDefinition>(
                Builders<InterfaceDefinition>.IndexKeys.Ascending(x => x.OwnerModuleCode),
                new CreateIndexOptions { Name = "ix_platform_interface_definitions_owner_module" })
        });

        await interfaceDiscoveryBatchCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<InterfaceDiscoveryBatch>(
                Builders<InterfaceDiscoveryBatch>.IndexKeys
                    .Ascending(x => x.SourceService)
                    .Ascending(x => x.SourceModuleCode)
                    .Ascending(x => x.ManifestHash),
                new CreateIndexOptions<InterfaceDiscoveryBatch>
                {
                    Unique = true,
                    Name = "ux_platform_interface_batches_manifest_hash",
                    PartialFilterExpression = Builders<InterfaceDiscoveryBatch>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<InterfaceDiscoveryBatch>(
                Builders<InterfaceDiscoveryBatch>.IndexKeys.Descending(x => x.ImportedAtUtc),
                new CreateIndexOptions { Name = "ix_platform_interface_batches_imported_at" })
        });

        await interfaceDiscoveryDiffCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<InterfaceDiscoveryDiffItem>(
                Builders<InterfaceDiscoveryDiffItem>.IndexKeys
                    .Ascending(x => x.BatchId)
                    .Ascending(x => x.InterfaceCode)
                    .Ascending(x => x.EndpointKey),
                new CreateIndexOptions<InterfaceDiscoveryDiffItem>
                {
                    Unique = true,
                    Name = "ux_platform_interface_diffs_batch_interface_endpoint",
                    PartialFilterExpression = Builders<InterfaceDiscoveryDiffItem>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<InterfaceDiscoveryDiffItem>(
                Builders<InterfaceDiscoveryDiffItem>.IndexKeys
                    .Ascending(x => x.BatchId)
                    .Ascending(x => x.ChangeType),
                new CreateIndexOptions { Name = "ix_platform_interface_diffs_batch_change_type" })
        });

        await interfaceActiveSnapshotCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<InterfaceActiveSnapshot>(
                Builders<InterfaceActiveSnapshot>.IndexKeys
                    .Ascending(x => x.InterfaceCode)
                    .Ascending(x => x.InterfaceVersion),
                new CreateIndexOptions<InterfaceActiveSnapshot>
                {
                    Unique = true,
                    Name = "ux_platform_interface_active_snapshots_code_version",
                    PartialFilterExpression = Builders<InterfaceActiveSnapshot>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await auditEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Descending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_events_tenant_occurred" }),
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys
                    .Ascending(x => x.TargetTenantId)
                    .Descending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_events_target_tenant_occurred" }),
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys
                    .Ascending(x => x.ActorId)
                    .Descending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_events_actor_occurred" }),
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys
                    .Ascending(x => x.Category)
                    .Descending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_events_category_occurred" }),
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys
                    .Ascending(x => x.EntityType)
                    .Ascending(x => x.EntityId)
                    .Descending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_events_entity_occurred" }),
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys
                    .Ascending(x => x.Operation)
                    .Descending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_events_operation_occurred" }),
            new CreateIndexModel<AuditEvent>(
                Builders<AuditEvent>.IndexKeys.Ascending(x => x.CorrelationId),
                new CreateIndexOptions { Name = "ix_audit_events_correlation_id" })
        });

        await auditRetentionPolicyCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuditEventRetentionPolicy>(
                Builders<AuditEventRetentionPolicy>.IndexKeys
                    .Ascending(x => x.Category)
                    .Ascending(x => x.PlanTierCode),
                new CreateIndexOptions<AuditEventRetentionPolicy>
                {
                    Unique = true,
                    Name = "ux_audit_retention_policies_category_plan_tier",
                    PartialFilterExpression = Builders<AuditEventRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<AuditEventRetentionPolicy>(
                Builders<AuditEventRetentionPolicy>.IndexKeys
                    .Ascending(x => x.IsActive)
                    .Ascending(x => x.Category),
                new CreateIndexOptions { Name = "ix_audit_retention_policies_active" })
        });

        await tenantAuditPreferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TenantAuditPreference>(
                Builders<TenantAuditPreference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Category),
                new CreateIndexOptions<TenantAuditPreference>
                {
                    Unique = true,
                    Name = "ux_tenant_audit_preferences_tenant_category",
                    PartialFilterExpression = Builders<TenantAuditPreference>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await auditOutboxCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuditOutboxMessage>(
                Builders<AuditOutboxMessage>.IndexKeys.Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "ux_audit_outbox_idempotency_key" }),
            new CreateIndexModel<AuditOutboxMessage>(
                Builders<AuditOutboxMessage>.IndexKeys.Ascending(x => x.CorrelationId),
                new CreateIndexOptions { Name = "ix_audit_outbox_correlation_id" }),
            new CreateIndexModel<AuditOutboxMessage>(
                Builders<AuditOutboxMessage>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.NextAttemptAtUtc),
                new CreateIndexOptions { Name = "ix_audit_outbox_status_next_attempt" }),
            new CreateIndexModel<AuditOutboxMessage>(
                Builders<AuditOutboxMessage>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Descending(x => x.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_audit_outbox_tenant_created" })
        });
    }

    private static async Task DropIndexIfExistsAsync<TDocument>(IMongoIndexManager<TDocument> indexes, string indexName)
    {
        try
        {
            await indexes.DropOneAsync(indexName);
        }
        catch (MongoCommandException ex) when (ex.CodeName is "IndexNotFound" or "NamespaceNotFound")
        {
        }
    }

    private static async Task SoftDeleteDomainsForDeletedTenantsAsync(
        IMongoCollection<Tenant> tenantCollection,
        IMongoCollection<TenantDomain> tenantDomainCollection)
    {
        var deletedTenantIds = await tenantCollection
            .Find(Builders<Tenant>.Filter.Eq(x => x.IsDeleted, true))
            .Project(x => x.Id)
            .ToListAsync();

        if (deletedTenantIds.Count == 0)
        {
            return;
        }

        var filter = Builders<TenantDomain>.Filter.And(
            Builders<TenantDomain>.Filter.In(x => x.TenantId, deletedTenantIds),
            Builders<TenantDomain>.Filter.Eq(x => x.IsDeleted, false));
        var update = Builders<TenantDomain>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.Status, TenantDomainStatus.Inactive)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await tenantDomainCollection.UpdateManyAsync(filter, update);
    }
}
