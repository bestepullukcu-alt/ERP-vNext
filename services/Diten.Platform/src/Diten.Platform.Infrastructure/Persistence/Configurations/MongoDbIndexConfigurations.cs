using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    private const string CorporateActiveInstanceIndexName =
        "ux_dm_collection_instances_corporate_owner_baseline_node_active";

    public static async Task ReconcileDevelopmentIndexesAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<CollectionInstance>("document_management_collection_instances");
        using var cursor = await collection.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var existing = indexes.FirstOrDefault(index =>
            index.TryGetValue("name", out var name)
            && name.IsString
            && string.Equals(name.AsString, CorporateActiveInstanceIndexName, StringComparison.Ordinal));

        if (existing is null)
        {
            return;
        }

        var isCompatible = existing.TryGetValue("partialFilterExpression", out var filterValue)
            && filterValue.IsBsonDocument
            && filterValue.AsBsonDocument.TryGetValue(nameof(CollectionInstance.IsDeleted), out var isDeleted)
            && isDeleted.IsBoolean
            && !isDeleted.AsBoolean
            && filterValue.AsBsonDocument.TryGetValue(nameof(CollectionInstance.CollectionScopeType), out var scopeType)
            && scopeType.IsInt32
            && scopeType.AsInt32 == (int)CollectionScopeType.Corporate
            && filterValue.AsBsonDocument.TryGetValue(nameof(CollectionInstance.InstanceStatus), out var status)
            && status.IsInt32
            && status.AsInt32 == (int)CollectionInstanceStatus.Active;

        if (!isCompatible)
        {
            // Development/test reconciliation only. The caller deliberately never invokes this in production.
            // No documents are changed; only this exact, obsolete index definition is replaced at startup.
            await collection.Indexes.DropOneAsync(CorporateActiveInstanceIndexName);
        }
    }

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
        var businessReferenceDataTenantAssignmentCollection = database.GetCollection<BusinessReferenceDataTenantAssignment>("business_reference_data_tenant_assignments");
        var businessReferenceDataPublishOperationCollection = database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations");
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
        var personReferenceCollection = database.GetCollection<PersonReference>(PersonReferenceRepository.CollectionName);
        var moduleCatalogDocuments = database.GetCollection<BsonDocument>("platform_module_catalog");
        var baselineReleaseCollection = database.GetCollection<BaselineRelease>("document_management_baseline_releases");
        var collectionDefinitionCollection = database.GetCollection<CollectionDefinition>("document_management_collection_definitions");
        var baselineSnapshotManifestCollection = database.GetCollection<BaselineSnapshotManifest>("document_management_baseline_snapshot_manifests");
        var collectionInstanceCollection = database.GetCollection<CollectionInstance>("document_management_collection_instances");
        var instantiationOperationCollection = database.GetCollection<InstantiationOperation>("document_management_instantiation_operations");
        var corporateProvisioningOperationCollection = database.GetCollection<CorporateCollectionInstanceProvisioningOperation>(
            "document_management_corporate_collection_provisioning_operations");
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
        // MOD-0029-FU36 — durable registration operation; references only, no content bytes.
        var controlledDocumentRegistrationCollection =
            database.GetCollection<ControlledDocumentRegistrationOperation>(
                ControlledDocumentRegistrationRepository.CollectionName);
        // MOD-0029-FU07 — identifier allocation ledger + sequence counter.
        var documentIdentifierAllocationCollection = database.GetCollection<DocumentIdentifierAllocation>("document_management_identifier_allocations");
        var documentIdentifierSequenceCounterCollection = database.GetCollection<DocumentIdentifierSequenceCounter>("document_management_identifier_sequence_counters");
        // MOD-0029-FU08 — lifecycle transition records.
        var documentLifecycleTransitionCollection = database.GetCollection<DocumentLifecycleTransitionRecord>("document_management_lifecycle_transitions");
        // MOD-0029-FU09 — approval requirements + evidence.
        var documentApprovalRequirementCollection = database.GetCollection<DocumentApprovalRequirement>("document_management_approval_requirements");
        var documentApprovalEvidenceCollection = database.GetCollection<DocumentApprovalEvidence>("document_management_approval_evidence");
        // MOD-0029-FU10 — release gate evaluations / results / manual evidence.
        var documentReleaseGateEvaluationCollection = database.GetCollection<DocumentReleaseGateEvaluation>("document_management_release_gate_evaluations");
        var documentReleaseGateResultCollection = database.GetCollection<DocumentReleaseGateResult>("document_management_release_gate_results");
        var documentReleaseGateEvidenceCollection = database.GetCollection<DocumentReleaseGateEvidence>("document_management_release_gate_evidence");
        // MOD-0029-FU11 — training matrix requirements + assignments.
        var documentTrainingRequirementCollection = database.GetCollection<DocumentTrainingMatrixRequirement>("document_management_training_requirements");
        var documentTrainingAssignmentCollection = database.GetCollection<DocumentTrainingAssignment>("document_management_training_assignments");
        // MOD-0029-FU12 — periodic reviews / extensions / escalations.
        var documentPeriodicReviewCollection = database.GetCollection<DocumentPeriodicReview>("document_management_periodic_reviews");
        var documentPeriodicReviewExtensionCollection = database.GetCollection<DocumentPeriodicReviewExtension>("document_management_periodic_review_extensions");
        var documentPeriodicReviewEscalationCollection = database.GetCollection<DocumentPeriodicReviewEscalation>("document_management_periodic_review_escalations");
        // MOD-0029-FU13 — suspension / retirement cases + temporary-instruction control.
        var documentSuspensionCaseCollection = database.GetCollection<DocumentSuspensionCase>("document_management_suspension_cases");
        var documentRetirementCaseCollection = database.GetCollection<DocumentRetirementCase>("document_management_retirement_cases");
        var temporaryInstructionControlCollection = database.GetCollection<TemporaryInstructionControl>("document_management_temporary_instruction_controls");
        // MOD-0029-FU16 — repository assessments + findings.
        var documentRepositoryAssessmentCollection = database.GetCollection<DocumentRepositoryAssessment>("document_management_repository_assessments");
        var documentRepositoryAssessmentFindingCollection = database.GetCollection<DocumentRepositoryAssessmentFinding>("document_management_repository_assessment_findings");
        // MOD-0029-FU17 — controlled copies / withdrawal plans / obsolete findings.
        var documentControlledCopyCollection = database.GetCollection<DocumentControlledCopy>("document_management_controlled_copies");
        var documentCopyWithdrawalPlanCollection = database.GetCollection<DocumentCopyWithdrawalPlan>("document_management_copy_withdrawal_plans");
        var documentObsoleteCopyFindingCollection = database.GetCollection<DocumentObsoleteCopyFinding>("document_management_obsolete_copy_findings");
        // MOD-0029-FU14 — external document register, monitoring checks, impact assessments, internal links.
        var externalDocumentCollection = database.GetCollection<ExternalDocumentRegisterEntry>("document_management_external_documents");
        var externalDocumentMonitoringCheckCollection = database.GetCollection<ExternalDocumentMonitoringCheck>("document_management_external_document_monitoring_checks");
        var externalDocumentImpactAssessmentCollection = database.GetCollection<ExternalDocumentImpactAssessment>("document_management_external_document_impact_assessments");
        var externalDocumentInternalLinkCollection = database.GetCollection<ExternalDocumentInternalLink>("document_management_external_document_internal_links");
        // MOD-0029-FU15 — retention policies, retention subject snapshots, legal holds, hold membership, dispositions.
        var retentionPolicyCollection = database.GetCollection<DocumentRetentionPolicy>("document_management_retention_policies");
        var retentionSubjectCollection = database.GetCollection<DocumentRetentionSubject>("document_management_retention_subjects");
        var legalHoldCollection = database.GetCollection<DocumentLegalHold>("document_management_legal_holds");
        var legalHoldSubjectCollection = database.GetCollection<DocumentLegalHoldSubject>("document_management_legal_hold_subjects");
        var dispositionRequestCollection = database.GetCollection<DocumentDispositionRequest>("document_management_disposition_requests");
        // MOD-0029-FU18 — variant localization profiles, review evidence, parent change assessments.
        var variantLocalizationProfileCollection = database.GetCollection<TemplateVariantLocalizationProfile>("document_management_variant_localization_profiles");
        var variantReviewEvidenceCollection = database.GetCollection<TemplateVariantReviewEvidence>("document_management_variant_review_evidence");
        var variantParentChangeAssessmentCollection = database.GetCollection<TemplateVariantParentChangeAssessment>("document_management_variant_parent_change_assessments");
        // MOD-0029-FU20 — repository downtime events, temporary controlled issues, downtime escalations.
        var downtimeEventCollection = database.GetCollection<DocumentRepositoryDowntimeEvent>("document_management_repository_downtime_events");
        var temporaryIssueCollection = database.GetCollection<DocumentTemporaryControlledIssue>("document_management_temporary_controlled_issues");
        var downtimeEscalationCollection = database.GetCollection<DocumentDowntimeEscalation>("document_management_downtime_escalations");
        // MOD-0029-FU21 — GDocP correction records, policies, reviews.
        var gdocpCorrectionRecordCollection = database.GetCollection<DocumentGDocPCorrectionRecord>("document_management_gdocp_correction_records");
        var gdocpCorrectionPolicyCollection = database.GetCollection<DocumentGDocPCorrectionPolicy>("document_management_gdocp_correction_policies");
        var gdocpCorrectionReviewCollection = database.GetCollection<DocumentGDocPCorrectionReview>("document_management_gdocp_correction_reviews");
        // MOD-0029-FU22 — quality events, GxP deviations, CAPA actions, source links.
        var qualityEventCollection = database.GetCollection<DocumentQualityEvent>("document_management_quality_events");
        var qualityDeviationCollection = database.GetCollection<DocumentDeviation>("document_management_quality_deviations");
        var capaActionCollection = database.GetCollection<DocumentCAPAAction>("document_management_capa_actions");
        var qualityEventSourceLinkCollection = database.GetCollection<DocumentQualityEventSourceLink>("document_management_quality_event_source_links");
        // MOD-0029-FU23 — signature policies, requests, records, signed-object fingerprints.
        var signaturePolicyCollection = database.GetCollection<DocumentSignaturePolicy>("document_management_signature_policies");
        var signatureRequestCollection = database.GetCollection<DocumentSignatureRequest>("document_management_signature_requests");
        var signatureRecordCollection = database.GetCollection<DocumentSignatureRecord>("document_management_signature_records");
        var signedObjectFingerprintCollection = database.GetCollection<DocumentSignedObjectFingerprint>("document_management_signed_object_fingerprints");
        // MOD-0029-FU31A — governance policy pack application history (append-only).
        var governancePolicyPackApplicationCollection = database.GetCollection<DocumentGovernancePolicyPackApplication>("document_management_governance_policy_pack_applications");
        // MOD-0029-FU32 — governance sweep run history (append-only).
        var governanceSweepRunCollection = database.GetCollection<DocumentGovernanceSweepRun>("document_management_governance_sweep_runs");
        var workflowTemplateCollection = database.GetCollection<WorkflowTemplate>("workflow_templates");
        var workflowTemplateVersionCollection = database.GetCollection<WorkflowTemplateVersion>("workflow_template_versions");
        var workflowInstanceCollection = database.GetCollection<WorkflowInstance>("workflow_instances");
        var approvalTaskCollection = database.GetCollection<ApprovalTask>("approval_tasks");
        var runtimeAssignmentSnapshotCollection = database.GetCollection<RuntimeAssignmentSnapshot>("workflow_runtime_assignment_snapshots");
        var workflowTransitionLogCollection = database.GetCollection<WorkflowTransitionLog>("workflow_transition_logs");
        var workflowSlaRuleCollection = database.GetCollection<SlaEscalationRule>("workflow_sla_rules");

        await controlledDocumentRegistrationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<ControlledDocumentRegistrationOperation>
                {
                    Unique = true,
                    Name = "ux_dm_registration_tenant_idempotency_active",
                    PartialFilterExpression = Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ControlledDocumentId),
                new CreateIndexOptions<ControlledDocumentRegistrationOperation>
                {
                    Unique = true,
                    Name = "ux_dm_registration_tenant_document_active",
                    // Partial indexes don't support $ne/$not, so match on the GUID's BSON type
                    // (subtype-4 binary) to include only rows where ControlledDocumentId is set.
                    PartialFilterExpression = Builders<ControlledDocumentRegistrationOperation>.Filter.And(
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Type(x => x.ControlledDocumentId, BsonType.Binary))
                }),
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MasterRegisterEntryId),
                new CreateIndexOptions<ControlledDocumentRegistrationOperation>
                {
                    Unique = true,
                    Name = "ux_dm_registration_tenant_register_active",
                    // Partial indexes don't support $ne/$not, so match on the GUID's BSON type
                    // (subtype-4 binary) to include only rows where MasterRegisterEntryId is set.
                    PartialFilterExpression = Builders<ControlledDocumentRegistrationOperation>.Filter.And(
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Type(x => x.MasterRegisterEntryId, BsonType.Binary))
                }),
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Descending(x => x.UpdatedAt),
                new CreateIndexOptions { Name = "ix_dm_registration_tenant_status_updated" })
        });

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
            ,
            new CreateIndexModel<CollectionInstance>(
                Builders<CollectionInstance>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CollectionScopeType)
                    .Ascending(x => x.ScopeOwnerId)
                    .Ascending(x => x.BaselineReleaseId)
                    .Ascending(x => x.CanonicalId),
                new CreateIndexOptions<CollectionInstance>
                {
                    Unique = true,
                    Name = CorporateActiveInstanceIndexName,
                    PartialFilterExpression = Builders<CollectionInstance>.Filter.And(
                        Builders<CollectionInstance>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<CollectionInstance>.Filter.Eq(x => x.CollectionScopeType, CollectionScopeType.Corporate),
                        // Only Active nodes belong to the unique active-tree constraint. Blocked, Superseded and
                        // Archived nodes are lifecycle history and must not prevent a later Active tree.
                        Builders<CollectionInstance>.Filter.Eq(x => x.InstanceStatus, CollectionInstanceStatus.Active))
                })
        });

        await corporateProvisioningOperationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<CorporateCollectionInstanceProvisioningOperation>(
                Builders<CorporateCollectionInstanceProvisioningOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<CorporateCollectionInstanceProvisioningOperation>
                {
                    Unique = true,
                    Name = "ux_dm_corporate_provisioning_tenant_idempotency_active",
                    PartialFilterExpression = Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<CorporateCollectionInstanceProvisioningOperation>(
                Builders<CorporateCollectionInstanceProvisioningOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CorporateOwnerId)
                    .Ascending(x => x.BaselineReleaseId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_corporate_provisioning_owner_baseline_status" })
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

        // MOD-0029-FU07 — identifier allocation ledger. The unique index is DELIBERATELY NOT partial on IsDeleted:
        // never-reuse (SOP §6.3) must see cancelled/abandoned/soft-deleted rows, so the DB enforces uniqueness across
        // the full history of allocated values per tenant + identifier type.
        await documentIdentifierAllocationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentIdentifierAllocation>(
                Builders<DocumentIdentifierAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IdentifierType).Ascending(x => x.IdentifierValue),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_dm_identifier_allocations_tenant_type_value_never_reuse"
                }),
            new CreateIndexModel<DocumentIdentifierAllocation>(
                Builders<DocumentIdentifierAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_identifier_allocations_register_entry" })
        });

        // MOD-0029-FU07 — one sequence counter per (tenant, type, prefix, domain, type-code) key.
        await documentIdentifierSequenceCounterCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentIdentifierSequenceCounter>(
                Builders<DocumentIdentifierSequenceCounter>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.IdentifierType)
                    .Ascending(x => x.Prefix).Ascending(x => x.DomainCode).Ascending(x => x.TypeCode),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_dm_identifier_sequence_counters_key"
                })
        });

        // MOD-0029-FU08 — lifecycle transition history, queried by register entry.
        await documentLifecycleTransitionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentLifecycleTransitionRecord>(
                Builders<DocumentLifecycleTransitionRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.PerformedAt),
                new CreateIndexOptions { Name = "ix_dm_lifecycle_transitions_register_entry" })
        });

        // MOD-0029-FU09 — approval requirements (unique per entry+key for idempotent resolve) and evidence history.
        await documentApprovalRequirementCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentApprovalRequirement>(
                Builders<DocumentApprovalRequirement>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.RequirementKey),
                new CreateIndexOptions<DocumentApprovalRequirement>
                {
                    Unique = true,
                    Name = "ux_dm_approval_requirements_entry_key_active",
                    PartialFilterExpression = Builders<DocumentApprovalRequirement>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await documentApprovalEvidenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentApprovalEvidence>(
                Builders<DocumentApprovalEvidence>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.PerformedAt),
                new CreateIndexOptions { Name = "ix_dm_approval_evidence_register_entry" }),
            new CreateIndexModel<DocumentApprovalEvidence>(
                Builders<DocumentApprovalEvidence>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequirementId),
                new CreateIndexOptions { Name = "ix_dm_approval_evidence_requirement" })
        });

        // MOD-0029-FU10 — release gate evaluations (latest by entry+time), results (by evaluation), manual evidence.
        await documentReleaseGateEvaluationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentReleaseGateEvaluation>(
                Builders<DocumentReleaseGateEvaluation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Descending(x => x.EvaluatedAt),
                new CreateIndexOptions { Name = "ix_dm_release_gate_evaluations_entry_time" })
        });
        await documentReleaseGateResultCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentReleaseGateResult>(
                Builders<DocumentReleaseGateResult>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.EvaluationId).Ascending(x => x.GateNumber),
                new CreateIndexOptions { Name = "ix_dm_release_gate_results_evaluation" })
        });
        await documentReleaseGateEvidenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentReleaseGateEvidence>(
                Builders<DocumentReleaseGateEvidence>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.GateKey).Descending(x => x.VerificationDate),
                new CreateIndexOptions { Name = "ix_dm_release_gate_evidence_entry_gate_time" })
        });

        // MOD-0029-FU11 — training requirements (unique per entry+key for idempotent resolve) + assignments.
        await documentTrainingRequirementCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentTrainingMatrixRequirement>(
                Builders<DocumentTrainingMatrixRequirement>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.RequirementKey),
                new CreateIndexOptions<DocumentTrainingMatrixRequirement>
                {
                    Unique = true,
                    Name = "ux_dm_training_requirements_entry_key_active",
                    PartialFilterExpression = Builders<DocumentTrainingMatrixRequirement>.Filter.Eq(x => x.IsDeleted, false)
                })
        });
        await documentTrainingAssignmentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentTrainingAssignment>(
                Builders<DocumentTrainingAssignment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.RequirementId),
                new CreateIndexOptions { Name = "ix_dm_training_assignments_entry_requirement" })
        });

        // MOD-0029-FU12 — periodic reviews (unique cycle per entry), extensions, escalations. Overdue sweeps query by
        // tenant + due date, so the due-date index carries the review status too.
        await documentPeriodicReviewCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentPeriodicReview>(
                Builders<DocumentPeriodicReview>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.ReviewNumber),
                new CreateIndexOptions<DocumentPeriodicReview>
                {
                    Unique = true,
                    Name = "ux_dm_periodic_reviews_entry_number_active",
                    PartialFilterExpression = Builders<DocumentPeriodicReview>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentPeriodicReview>(
                Builders<DocumentPeriodicReview>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ReviewStatus).Ascending(x => x.ReviewDueDate),
                new CreateIndexOptions { Name = "ix_dm_periodic_reviews_status_due" })
        });
        await documentPeriodicReviewExtensionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentPeriodicReviewExtension>(
                Builders<DocumentPeriodicReviewExtension>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PeriodicReviewId),
                new CreateIndexOptions { Name = "ix_dm_periodic_review_extensions_review" })
        });
        await documentPeriodicReviewEscalationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentPeriodicReviewEscalation>(
                Builders<DocumentPeriodicReviewEscalation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PeriodicReviewId).Ascending(x => x.EscalationType),
                new CreateIndexOptions { Name = "ix_dm_periodic_review_escalations_review_type" })
        });

        // MOD-0029-FU13 — suspension / retirement cases (unique case number per entry) + one temporary-instruction
        // control per entry.
        await documentSuspensionCaseCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentSuspensionCase>(
                Builders<DocumentSuspensionCase>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CaseNumber),
                new CreateIndexOptions<DocumentSuspensionCase>
                {
                    Unique = true,
                    Name = "ux_dm_suspension_cases_entry_number_active",
                    PartialFilterExpression = Builders<DocumentSuspensionCase>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSuspensionCase>(
                Builders<DocumentSuspensionCase>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CaseStatus),
                new CreateIndexOptions { Name = "ix_dm_suspension_cases_status" })
        });
        await documentRetirementCaseCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentRetirementCase>(
                Builders<DocumentRetirementCase>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CaseNumber),
                new CreateIndexOptions<DocumentRetirementCase>
                {
                    Unique = true,
                    Name = "ux_dm_retirement_cases_entry_number_active",
                    PartialFilterExpression = Builders<DocumentRetirementCase>.Filter.Eq(x => x.IsDeleted, false)
                })
        });
        await temporaryInstructionControlCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TemporaryInstructionControl>(
                Builders<TemporaryInstructionControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions<TemporaryInstructionControl>
                {
                    Unique = true,
                    Name = "ux_dm_temporary_instruction_controls_entry_active",
                    PartialFilterExpression = Builders<TemporaryInstructionControl>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TemporaryInstructionControl>(
                Builders<TemporaryInstructionControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemporaryInstructionStatus).Ascending(x => x.ValidUntil),
                new CreateIndexOptions { Name = "ix_dm_temporary_instruction_controls_status_validity" })
        });

        // MOD-0029-FU16 — repository assessments (unique key per tenant) + findings (unique per assessment+key).
        await documentRepositoryAssessmentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentRepositoryAssessment>(
                Builders<DocumentRepositoryAssessment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RepositoryKey),
                new CreateIndexOptions<DocumentRepositoryAssessment>
                {
                    Unique = true,
                    Name = "ux_dm_repository_assessments_tenant_key_active",
                    PartialFilterExpression = Builders<DocumentRepositoryAssessment>.Filter.Eq(x => x.IsDeleted, false)
                })
        });
        await documentRepositoryAssessmentFindingCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentRepositoryAssessmentFinding>(
                Builders<DocumentRepositoryAssessmentFinding>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RepositoryAssessmentId).Ascending(x => x.FindingKey),
                new CreateIndexOptions<DocumentRepositoryAssessmentFinding>
                {
                    Unique = true,
                    Name = "ux_dm_repository_assessment_findings_assessment_key_active",
                    PartialFilterExpression = Builders<DocumentRepositoryAssessmentFinding>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        // MOD-0029-FU17 — controlled copies (unique number per entry), withdrawal plans, obsolete findings (unique key).
        await documentControlledCopyCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentControlledCopy>(
                Builders<DocumentControlledCopy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CopyNumber),
                new CreateIndexOptions<DocumentControlledCopy>
                {
                    Unique = true,
                    Name = "ux_dm_controlled_copies_entry_number_active",
                    PartialFilterExpression = Builders<DocumentControlledCopy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentControlledCopy>(
                Builders<DocumentControlledCopy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CopyStatus),
                new CreateIndexOptions { Name = "ix_dm_controlled_copies_entry_status" })
        });
        await documentCopyWithdrawalPlanCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentCopyWithdrawalPlan>(
                Builders<DocumentCopyWithdrawalPlan>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.PlanStatus),
                new CreateIndexOptions { Name = "ix_dm_copy_withdrawal_plans_entry_status" })
        });
        await documentObsoleteCopyFindingCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentObsoleteCopyFinding>(
                Builders<DocumentObsoleteCopyFinding>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.FindingKey),
                new CreateIndexOptions<DocumentObsoleteCopyFinding>
                {
                    Unique = true,
                    Name = "ux_dm_obsolete_copy_findings_entry_key_active",
                    PartialFilterExpression = Builders<DocumentObsoleteCopyFinding>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        // MOD-0029-FU14 — external document register. Monitoring-due and impact-overdue are the hot read paths.
        await externalDocumentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ExternalDocumentRegisterEntry>(
                Builders<ExternalDocumentRegisterEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentStatus).Ascending(x => x.NextCheckDueDate),
                new CreateIndexOptions { Name = "ix_dm_external_documents_status_next_check" }),
            new CreateIndexModel<ExternalDocumentRegisterEntry>(
                Builders<ExternalDocumentRegisterEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SourceStatus),
                new CreateIndexOptions { Name = "ix_dm_external_documents_source_status" }),
            new CreateIndexModel<ExternalDocumentRegisterEntry>(
                Builders<ExternalDocumentRegisterEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.MonitoringOwnerUserId),
                new CreateIndexOptions { Name = "ix_dm_external_documents_owner" })
        });
        await externalDocumentMonitoringCheckCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ExternalDocumentMonitoringCheck>(
                Builders<ExternalDocumentMonitoringCheck>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentRegisterEntryId).Descending(x => x.CheckDate),
                new CreateIndexOptions { Name = "ix_dm_external_document_checks_entry_date" })
        });
        await externalDocumentImpactAssessmentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ExternalDocumentImpactAssessment>(
                Builders<ExternalDocumentImpactAssessment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentRegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_external_document_impact_entry" }),
            new CreateIndexModel<ExternalDocumentImpactAssessment>(
                Builders<ExternalDocumentImpactAssessment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.AssessmentStatus).Ascending(x => x.DueDate),
                new CreateIndexOptions { Name = "ix_dm_external_document_impact_status_due" })
        });
        await externalDocumentInternalLinkCollection.Indexes.CreateManyAsync(new[]
        {
            // One live link per (external, internal, type) triple — the link command is idempotent.
            new CreateIndexModel<ExternalDocumentInternalLink>(
                Builders<ExternalDocumentInternalLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentRegisterEntryId)
                    .Ascending(x => x.InternalRegisterEntryId).Ascending(x => x.LinkType),
                new CreateIndexOptions<ExternalDocumentInternalLink>
                {
                    Unique = true,
                    Name = "ux_dm_external_document_links_pair_type_active",
                    PartialFilterExpression = Builders<ExternalDocumentInternalLink>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ExternalDocumentInternalLink>(
                Builders<ExternalDocumentInternalLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.InternalRegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_external_document_links_internal" })
        });

        // MOD-0029-FU15 — retention & litigation hold. The hot read paths are: active policies by subject type,
        // the eligible-subject sweep, and the active-hold lookup that gates every disposition decision.
        await retentionPolicyCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentRetentionPolicy>(
                Builders<DocumentRetentionPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PolicyKey),
                new CreateIndexOptions<DocumentRetentionPolicy>
                {
                    Unique = true,
                    Name = "ux_dm_retention_policies_key_active",
                    PartialFilterExpression = Builders<DocumentRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentRetentionPolicy>(
                Builders<DocumentRetentionPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType).Ascending(x => x.PolicyStatus),
                new CreateIndexOptions { Name = "ix_dm_retention_policies_subject_status" })
        });
        await retentionSubjectCollection.Indexes.CreateManyAsync(new[]
        {
            // One snapshot per governed record — re-evaluation overwrites it in place.
            new CreateIndexModel<DocumentRetentionSubject>(
                Builders<DocumentRetentionSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType).Ascending(x => x.SubjectId),
                new CreateIndexOptions<DocumentRetentionSubject>
                {
                    Unique = true,
                    Name = "ux_dm_retention_subjects_subject_active",
                    PartialFilterExpression = Builders<DocumentRetentionSubject>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentRetentionSubject>(
                Builders<DocumentRetentionSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDispositionEligible)
                    .Ascending(x => x.IsBlockedByLegalHold).Ascending(x => x.RetentionDueDate),
                new CreateIndexOptions { Name = "ix_dm_retention_subjects_eligibility" }),
            new CreateIndexModel<DocumentRetentionSubject>(
                Builders<DocumentRetentionSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_retention_subjects_register_entry" })
        });
        await legalHoldCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentLegalHold>(
                Builders<DocumentLegalHold>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.HoldKey),
                new CreateIndexOptions<DocumentLegalHold>
                {
                    Unique = true,
                    Name = "ux_dm_legal_holds_key_active",
                    PartialFilterExpression = Builders<DocumentLegalHold>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentLegalHold>(
                Builders<DocumentLegalHold>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.HoldStatus).Ascending(x => x.EffectiveFrom),
                new CreateIndexOptions { Name = "ix_dm_legal_holds_status_effective" })
        });
        await legalHoldSubjectCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentLegalHoldSubject>(
                Builders<DocumentLegalHoldSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalHoldId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_legal_hold_subjects_hold_status" }),
            new CreateIndexModel<DocumentLegalHoldSubject>(
                Builders<DocumentLegalHoldSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType).Ascending(x => x.SubjectId),
                new CreateIndexOptions { Name = "ix_dm_legal_hold_subjects_subject" })
        });
        await dispositionRequestCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentDispositionRequest>(
                Builders<DocumentDispositionRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequestNumber),
                new CreateIndexOptions<DocumentDispositionRequest>
                {
                    Unique = true,
                    Name = "ux_dm_disposition_requests_number_active",
                    PartialFilterExpression = Builders<DocumentDispositionRequest>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentDispositionRequest>(
                Builders<DocumentDispositionRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType)
                    .Ascending(x => x.SubjectId).Ascending(x => x.RequestStatus),
                new CreateIndexOptions { Name = "ix_dm_disposition_requests_subject_status" })
        });

        // MOD-0029-FU18 — variant localization. One profile per variant; evidence and assessments are append-only
        // histories read back per variant.
        await variantLocalizationProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TemplateVariantLocalizationProfile>(
                Builders<TemplateVariantLocalizationProfile>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateVariantId),
                new CreateIndexOptions<TemplateVariantLocalizationProfile>
                {
                    Unique = true,
                    Name = "ux_dm_variant_localization_profiles_variant_active",
                    PartialFilterExpression = Builders<TemplateVariantLocalizationProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TemplateVariantLocalizationProfile>(
                Builders<TemplateVariantLocalizationProfile>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ParentTemplateMasterId),
                new CreateIndexOptions { Name = "ix_dm_variant_localization_profiles_parent_master" }),
            new CreateIndexModel<TemplateVariantLocalizationProfile>(
                Builders<TemplateVariantLocalizationProfile>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.VariantLanguageCode).Ascending(x => x.LocalAdoptionStatus),
                new CreateIndexOptions { Name = "ix_dm_variant_localization_profiles_language_adoption" })
        });
        await variantReviewEvidenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TemplateVariantReviewEvidence>(
                Builders<TemplateVariantReviewEvidence>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.TemplateVariantId).Ascending(x => x.EvidenceType).Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_dm_variant_review_evidence_variant_type_created" })
        });
        await variantParentChangeAssessmentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TemplateVariantParentChangeAssessment>(
                Builders<TemplateVariantParentChangeAssessment>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.TemplateVariantId).Descending(x => x.AssessedAt),
                new CreateIndexOptions { Name = "ix_dm_variant_parent_change_assessments_variant_assessed" })
        });

        // MOD-0029-FU20 — downtime. The hot read paths are the open-outage list, the issues of one event, and the
        // outstanding-issue sweep that drives the 3-working-day overdue evaluation.
        await downtimeEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentRepositoryDowntimeEvent>(
                Builders<DocumentRepositoryDowntimeEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DowntimeNumber),
                new CreateIndexOptions<DocumentRepositoryDowntimeEvent>
                {
                    Unique = true,
                    Name = "ux_dm_downtime_events_number_active",
                    PartialFilterExpression = Builders<DocumentRepositoryDowntimeEvent>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentRepositoryDowntimeEvent>(
                Builders<DocumentRepositoryDowntimeEvent>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DowntimeStatus).Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_dm_downtime_events_status_started" }),
            new CreateIndexModel<DocumentRepositoryDowntimeEvent>(
                Builders<DocumentRepositoryDowntimeEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RepositoryAssessmentId),
                new CreateIndexOptions { Name = "ix_dm_downtime_events_repository_assessment" })
        });
        await temporaryIssueCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IssueNumber),
                new CreateIndexOptions<DocumentTemporaryControlledIssue>
                {
                    Unique = true,
                    Name = "ux_dm_temporary_controlled_issues_number_active",
                    PartialFilterExpression = Builders<DocumentTemporaryControlledIssue>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DowntimeEventId).Ascending(x => x.IssueStatus),
                new CreateIndexOptions { Name = "ix_dm_temporary_controlled_issues_event_status" }),
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.IssueStatus).Ascending(x => x.ReconciliationDueDate),
                new CreateIndexOptions { Name = "ix_dm_temporary_controlled_issues_status_due" }),
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_temporary_controlled_issues_register_entry" })
        });
        await downtimeEscalationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentDowntimeEscalation>(
                Builders<DocumentDowntimeEscalation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DowntimeEventId).Ascending(x => x.EscalationType).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_downtime_escalations_event_type_status" })
        });

        // MOD-0029-FU21 — GDocP correction trail. The hot read path is the correction history of one subject; the
        // pending-review queue and the active-policy lookup follow.
        await gdocpCorrectionRecordCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CorrectionNumber),
                new CreateIndexOptions<DocumentGDocPCorrectionRecord>
                {
                    Unique = true,
                    Name = "ux_dm_gdocp_corrections_number_active",
                    PartialFilterExpression = Builders<DocumentGDocPCorrectionRecord>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId).Ascending(x => x.CorrectedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_corrections_subject_corrected" }),
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.ReviewStatus).Ascending(x => x.CorrectedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_corrections_review_status" }),
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.IsHighRiskCorrection).Ascending(x => x.CorrectedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_corrections_high_risk" })
        });
        await gdocpCorrectionPolicyCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentGDocPCorrectionPolicy>(
                Builders<DocumentGDocPCorrectionPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PolicyKey),
                new CreateIndexOptions<DocumentGDocPCorrectionPolicy>
                {
                    Unique = true,
                    Name = "ux_dm_gdocp_correction_policies_key_active",
                    PartialFilterExpression = Builders<DocumentGDocPCorrectionPolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentGDocPCorrectionPolicy>(
                Builders<DocumentGDocPCorrectionPolicy>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.PolicyStatus),
                new CreateIndexOptions { Name = "ix_dm_gdocp_correction_policies_subject_status" })
        });
        await gdocpCorrectionReviewCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentGDocPCorrectionReview>(
                Builders<DocumentGDocPCorrectionReview>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.CorrectionRecordId).Ascending(x => x.ReviewedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_correction_reviews_correction_reviewed" })
        });

        // MOD-0029-FU22 — quality bridge. The hot read paths are the open-event queue, the deviation/CAPA of one
        // event, and the source-link lookup that makes the bridge idempotent.
        await qualityEventCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QualityEventNumber),
                new CreateIndexOptions<DocumentQualityEvent>
                {
                    Unique = true,
                    Name = "ux_dm_quality_events_number_active",
                    PartialFilterExpression = Builders<DocumentQualityEvent>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.EventStatus).Descending(x => x.DetectedAt),
                new CreateIndexOptions { Name = "ix_dm_quality_events_status_detected" }),
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_quality_events_register_entry" }),
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceType).Ascending(x => x.SourceId),
                new CreateIndexOptions { Name = "ix_dm_quality_events_source" })
        });
        await qualityDeviationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentDeviation>(
                Builders<DocumentDeviation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DeviationNumber),
                new CreateIndexOptions<DocumentDeviation>
                {
                    Unique = true,
                    Name = "ux_dm_quality_deviations_number_active",
                    PartialFilterExpression = Builders<DocumentDeviation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentDeviation>(
                Builders<DocumentDeviation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.QualityEventId).Ascending(x => x.DeviationStatus),
                new CreateIndexOptions { Name = "ix_dm_quality_deviations_event_status" }),
            new CreateIndexModel<DocumentDeviation>(
                Builders<DocumentDeviation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DeviationSeverity).Ascending(x => x.DeviationStatus),
                new CreateIndexOptions { Name = "ix_dm_quality_deviations_severity_status" })
        });
        await capaActionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CAPANumber),
                new CreateIndexOptions<DocumentCAPAAction>
                {
                    Unique = true,
                    Name = "ux_dm_capa_actions_number_active",
                    PartialFilterExpression = Builders<DocumentCAPAAction>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DeviationId).Ascending(x => x.ActionStatus),
                new CreateIndexOptions { Name = "ix_dm_capa_actions_deviation_status" }),
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QualityEventId).Ascending(x => x.ActionStatus),
                new CreateIndexOptions { Name = "ix_dm_capa_actions_event_status" }),
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ActionStatus).Ascending(x => x.DueDate),
                new CreateIndexOptions { Name = "ix_dm_capa_actions_status_due" })
        });
        await qualityEventSourceLinkCollection.Indexes.CreateManyAsync(new[]
        {
            // The bridge idempotency lookup.
            new CreateIndexModel<DocumentQualityEventSourceLink>(
                Builders<DocumentQualityEventSourceLink>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceType).Ascending(x => x.SourceId).Ascending(x => x.EventType),
                new CreateIndexOptions { Name = "ix_dm_quality_event_source_links_source_type" }),
            new CreateIndexModel<DocumentQualityEventSourceLink>(
                Builders<DocumentQualityEventSourceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QualityEventId),
                new CreateIndexOptions { Name = "ix_dm_quality_event_source_links_event" })
        });

        // MOD-0029-FU23 — electronic signature foundation. The hot read paths are the policy lookup during signing,
        // the signature history for one subject, and the duplicate-signature check (subject + meaning + fingerprint).
        await signaturePolicyCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentSignaturePolicy>(
                Builders<DocumentSignaturePolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PolicyKey),
                new CreateIndexOptions<DocumentSignaturePolicy>
                {
                    Unique = true,
                    Name = "ux_dm_signature_policies_key_active",
                    PartialFilterExpression = Builders<DocumentSignaturePolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSignaturePolicy>(
                Builders<DocumentSignaturePolicy>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SignableSubjectType).Ascending(x => x.PolicyStatus),
                new CreateIndexOptions { Name = "ix_dm_signature_policies_subject_status" })
        });
        // MOD-0029-FU31A — pack application history. NO unique index on PackKey: the pack is idempotent and may be
        // applied repeatedly, and every run is a separate append-only audit row.
        await governancePolicyPackApplicationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentGovernancePolicyPackApplication>(
                Builders<DocumentGovernancePolicyPackApplication>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.PackKey).Descending(x => x.AppliedAt),
                new CreateIndexOptions { Name = "ix_dm_governance_policy_pack_applications_pack_applied" }),
            new CreateIndexModel<DocumentGovernancePolicyPackApplication>(
                Builders<DocumentGovernancePolicyPackApplication>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.PackVersion),
                new CreateIndexOptions { Name = "ix_dm_governance_policy_pack_applications_version" }),
            new CreateIndexModel<DocumentGovernancePolicyPackApplication>(
                Builders<DocumentGovernancePolicyPackApplication>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.ApplicationStatus),
                new CreateIndexOptions { Name = "ix_dm_governance_policy_pack_applications_status" })
        });
        // MOD-0029-FU32 — sweep run history. No unique index on SweepKey: a sweep is meant to run repeatedly, and
        // every run is a separate append-only evidence row.
        await governanceSweepRunCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentGovernanceSweepRun>(
                Builders<DocumentGovernanceSweepRun>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SweepKey).Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_dm_governance_sweep_runs_key_started" }),
            new CreateIndexModel<DocumentGovernanceSweepRun>(
                Builders<DocumentGovernanceSweepRun>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_governance_sweep_runs_status" }),
            new CreateIndexModel<DocumentGovernanceSweepRun>(
                Builders<DocumentGovernanceSweepRun>.IndexKeys.Ascending(x => x.TenantId)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_dm_governance_sweep_runs_started" })
        });
        await signatureRequestCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentSignatureRequest>(
                Builders<DocumentSignatureRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureRequestNumber),
                new CreateIndexOptions<DocumentSignatureRequest>
                {
                    Unique = true,
                    Name = "ux_dm_signature_requests_number_active",
                    PartialFilterExpression = Builders<DocumentSignatureRequest>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSignatureRequest>(
                Builders<DocumentSignatureRequest>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId),
                new CreateIndexOptions { Name = "ix_dm_signature_requests_subject" }),
            new CreateIndexModel<DocumentSignatureRequest>(
                Builders<DocumentSignatureRequest>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.RequestStatus).Ascending(x => x.DueDate),
                new CreateIndexOptions { Name = "ix_dm_signature_requests_status_due" })
        });
        await signatureRecordCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureNumber),
                new CreateIndexOptions<DocumentSignatureRecord>
                {
                    Unique = true,
                    Name = "ux_dm_signature_records_number_active",
                    PartialFilterExpression = Builders<DocumentSignatureRecord>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId).Descending(x => x.SignedAt),
                new CreateIndexOptions { Name = "ix_dm_signature_records_subject_signed" }),
            // The duplicate-signature guard on the sign path.
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId)
                    .Ascending(x => x.SignatureMeaning).Ascending(x => x.ObjectFingerprint),
                new CreateIndexOptions { Name = "ix_dm_signature_records_subject_meaning_fingerprint" }),
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureRequestId),
                new CreateIndexOptions { Name = "ix_dm_signature_records_request" }),
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureStatus),
                new CreateIndexOptions { Name = "ix_dm_signature_records_status" })
        });
        await signedObjectFingerprintCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<DocumentSignedObjectFingerprint>(
                Builders<DocumentSignedObjectFingerprint>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId).Descending(x => x.GeneratedAt),
                new CreateIndexOptions { Name = "ix_dm_signed_object_fingerprints_subject_generated" })
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

        // Working Calendar & Public Holidays.
        // NOTE: the partial filter uses Eq(IsDeleted,false) — never Ne(...). A $ne/$not partial filter is unsupported
        // and puts the service into a startup crash-loop.
        // NOTE: there is deliberately no index spanning two date fields; Date/ObservedDate are DateOnly inside the
        // embedded day list and are never sorted together at the collection level.
        var workingCalendarCollection = database.GetCollection<WorkingCalendar>("working_calendars");

        // The partial filter gained a CalendarStatus clause (archived rows release their code), and MongoDB refuses
        // to recreate an existing index name with different options (IndexOptionsConflict, code 85) — which would
        // crash-loop startup. Drop first; DropIndexIfExistsAsync swallows IndexNotFound so a fresh database is fine.
        await DropIndexIfExistsAsync(workingCalendarCollection.Indexes, "ux_working_calendars_scope_country_year_code");

        await workingCalendarCollection.Indexes.CreateManyAsync(new[]
        {
            // Scope + country + year + code is the business key. TenantId participates so a country row (null) and a
            // tenant row can legitimately share a code.
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CountryCode)
                    .Ascending(x => x.CalendarYear)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.CalendarCode),
                new CreateIndexOptions<WorkingCalendar>
                {
                    Unique = true,
                    Name = "ux_working_calendars_scope_country_year_code",
                    // Uniqueness holds among LIVE rows only: an archived calendar releases its code so the same
                    // year can be re-entered (there is no delete endpoint). `$in` is used rather than "not
                    // archived" because a partialFilterExpression cannot contain $ne/$not — verified supported on
                    // this server (MongoDB 7.0). The list is shared with the repository guard so the two can never
                    // disagree; a guard looser than this index would surface as an E11000 500 instead of a 409.
                    PartialFilterExpression = Builders<WorkingCalendar>.Filter.And(
                        Builders<WorkingCalendar>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<WorkingCalendar>.Filter.In(x => x.CalendarStatus, WorkingCalendarStatus.CodeHolding))
                }),
            // The provider's hot path: resolve the active calendar for a scope + country + year.
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CountryCode)
                    .Ascending(x => x.CalendarYear)
                    .Ascending(x => x.CalendarStatus)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_working_calendars_resolution" }),
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.CalendarYear),
                new CreateIndexOptions { Name = "ix_working_calendars_org_scope", Sparse = true }),
            new CreateIndexModel<WorkingCalendar>(
                Builders<WorkingCalendar>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.CalendarYear),
                new CreateIndexOptions { Name = "ix_working_calendars_legal_entity_scope", Sparse = true })
        });

        var workingCalendarImportCollection = database.GetCollection<WorkingCalendarImportBatch>("working_calendar_import_batches");
        await workingCalendarImportCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<WorkingCalendarImportBatch>(
                Builders<WorkingCalendarImportBatch>.IndexKeys.Ascending(x => x.BatchCode),
                new CreateIndexOptions<WorkingCalendarImportBatch>
                {
                    Name = "ux_working_calendar_import_batch_code",
                    Unique = true,
                    PartialFilterExpression = Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<WorkingCalendarImportBatch>(
                Builders<WorkingCalendarImportBatch>.IndexKeys.Ascending(x => x.CountryCode)
                    .Ascending(x => x.CalendarYear).Ascending(x => x.ImportStatus),
                new CreateIndexOptions { Name = "ix_working_calendar_import_list" }),
            new CreateIndexModel<WorkingCalendarImportBatch>(
                Builders<WorkingCalendarImportBatch>.IndexKeys.Ascending(x => x.TargetCalendarId)
                    .Ascending(x => x.ImportStatus),
                new CreateIndexOptions { Name = "ix_working_calendar_import_target_status" })
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


        await personReferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PersonReference>(
                Builders<PersonReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Id),
                new CreateIndexOptions<PersonReference>
                {
                    Unique = true,
                    Name = "ux_person_references_tenant_person_id_active",
                    PartialFilterExpression = Builders<PersonReference>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PersonReference>(
                Builders<PersonReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.DisplayName)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_references_tenant_display_name" }),
            new CreateIndexModel<PersonReference>(
                Builders<PersonReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_references_tenant_status" }),
            new CreateIndexModel<PersonReference>(
                Builders<PersonReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ReferenceCode)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_references_tenant_reference_code" })
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

        await businessReferenceDataTenantAssignmentCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataTenantAssignment>(
                Builders<BusinessReferenceDataTenantAssignment>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ConsumerTenantId)
                    .Ascending(x => x.SetCode),
                new CreateIndexOptions<BusinessReferenceDataTenantAssignment>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_tenant_assignments_active",
                    PartialFilterExpression = Builders<BusinessReferenceDataTenantAssignment>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await businessReferenceDataPublishOperationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<BusinessReferenceDataPublishOperation>(
                Builders<BusinessReferenceDataPublishOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<BusinessReferenceDataPublishOperation>
                {
                    Unique = true,
                    Name = "ux_business_reference_data_publish_operations_idempotency",
                    PartialFilterExpression = Builders<BusinessReferenceDataPublishOperation>.Filter.Eq(x => x.IsDeleted, false)
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
