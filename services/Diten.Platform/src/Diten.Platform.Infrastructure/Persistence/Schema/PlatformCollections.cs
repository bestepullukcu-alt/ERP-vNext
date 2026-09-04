namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>
/// Every platform collection name, declared ONCE.
///
/// WHY THIS EXISTS. Before this file the same name lived twice: as a string literal inside
/// <see cref="Configurations.MongoDbIndexConfigurations"/> and again as a string literal inside the
/// repository that reads the collection. Two strings for one name is a rename that half-lands — the
/// repository writes to a collection the index builder never indexed, and nothing fails: Mongo creates the
/// collection on first write and runs the query without an index. It is slow, not broken, so it ships.
///
/// A handful of names predate this file and are declared next to their owner instead
/// (<c>SeedMarkerStore.CollectionName</c>, <c>AuditCollectionNames.*</c>,
/// <c>PersonReferenceRepository.CollectionName</c>). Those are still SINGLE declarations — the manifest
/// references them rather than re-typing them, which is the property that matters.
/// </summary>
public static class PlatformCollections
{
    public const string ApprovalTasks = "approval_tasks";
    public const string BusinessReferenceDataImportPreviews = "business_reference_data_import_previews";
    public const string BusinessReferenceDataIntegrationEvents = "business_reference_data_integration_events";
    public const string BusinessReferenceDataPublishOperations = "business_reference_data_publish_operations";
    public const string BusinessReferenceDataSets = "business_reference_data_sets";
    public const string BusinessReferenceDataTenantAssignments = "business_reference_data_tenant_assignments";
    public const string BusinessReferenceDataUsageRegistrations = "business_reference_data_usage_registrations";
    public const string BusinessReferenceDataValidationResults = "business_reference_data_validation_results";
    public const string BusinessReferenceDataVersions = "business_reference_data_versions";
    public const string ChecklistRuns = "checklist_runs";
    public const string ChecklistTemplates = "checklist_templates";
    public const string ConsumedEvents = "consumed_events";
    public const string DocumentManagementAccessPolicies = "document_management_access_policies";
    public const string DocumentManagementApprovalEvidence = "document_management_approval_evidence";
    public const string DocumentManagementApprovalRequirements = "document_management_approval_requirements";
    public const string DocumentManagementBaselineReleases = "document_management_baseline_releases";
    public const string DocumentManagementBaselineSnapshotManifests = "document_management_baseline_snapshot_manifests";
    public const string DocumentManagementCapaActions = "document_management_capa_actions";
    public const string DocumentManagementCollectionDefinitions = "document_management_collection_definitions";
    public const string DocumentManagementCollectionDeviations = "document_management_collection_deviations";
    public const string DocumentManagementCollectionInstances = "document_management_collection_instances";
    public const string DocumentManagementCollectionProvisioningEvidence = "document_management_collection_provisioning_evidence";
    public const string DocumentManagementControlledCopies = "document_management_controlled_copies";
    public const string DocumentManagementControlledDocumentRegistrationOperations = "document_management_controlled_document_registration_operations";
    public const string DocumentManagementControlledDocumentVersions = "document_management_controlled_document_versions";
    public const string DocumentManagementControlledDocuments = "document_management_controlled_documents";
    public const string DocumentManagementCopyWithdrawalPlans = "document_management_copy_withdrawal_plans";
    public const string DocumentManagementCorporateCollectionProvisioningOperations = "document_management_corporate_collection_provisioning_operations";
    public const string DocumentManagementDispositionRequests = "document_management_disposition_requests";
    public const string DocumentManagementDocumentFavorites = "document_management_document_favorites";
    public const string DocumentManagementDocumentShares = "document_management_document_shares";
    public const string DocumentManagementDocumentVariants = "document_management_document_variants";
    public const string DocumentManagementDowntimeEscalations = "document_management_downtime_escalations";
    public const string DocumentManagementExternalDocumentImpactAssessments = "document_management_external_document_impact_assessments";
    public const string DocumentManagementExternalDocumentInternalLinks = "document_management_external_document_internal_links";
    public const string DocumentManagementExternalDocumentMonitoringChecks = "document_management_external_document_monitoring_checks";
    public const string DocumentManagementExternalDocuments = "document_management_external_documents";
    public const string DocumentManagementFolderDocumentAccessPolicies = "document_management_folder_document_access_policies";
    public const string DocumentManagementFolderShareOperations = "document_management_folder_share_operations";
    public const string DocumentManagementFolderShareOutcomes = "document_management_folder_share_outcomes";
    public const string DocumentManagementGdocpCorrectionPolicies = "document_management_gdocp_correction_policies";
    public const string DocumentManagementGdocpCorrectionRecords = "document_management_gdocp_correction_records";
    public const string DocumentManagementGdocpCorrectionReviews = "document_management_gdocp_correction_reviews";
    public const string DocumentManagementGovernancePolicyPackApplications = "document_management_governance_policy_pack_applications";
    public const string DocumentManagementGovernanceSweepRuns = "document_management_governance_sweep_runs";
    public const string DocumentManagementIdentifierAllocations = "document_management_identifier_allocations";
    public const string DocumentManagementIdentifierSequenceCounters = "document_management_identifier_sequence_counters";
    public const string DocumentManagementInstantiationOperations = "document_management_instantiation_operations";
    public const string DocumentManagementInstantiationOutcomes = "document_management_instantiation_outcomes";
    public const string DocumentManagementLegalHoldSubjects = "document_management_legal_hold_subjects";
    public const string DocumentManagementLegalHolds = "document_management_legal_holds";
    public const string DocumentManagementLifecycleTransitions = "document_management_lifecycle_transitions";
    public const string DocumentManagementMasterRegister = "document_management_master_register";
    public const string DocumentManagementObsoleteCopyFindings = "document_management_obsolete_copy_findings";
    public const string DocumentManagementPeriodicReviewEscalations = "document_management_periodic_review_escalations";
    public const string DocumentManagementPeriodicReviewExtensions = "document_management_periodic_review_extensions";
    public const string DocumentManagementPeriodicReviews = "document_management_periodic_reviews";
    public const string DocumentManagementQualityDeviations = "document_management_quality_deviations";
    public const string DocumentManagementQualityEventSourceLinks = "document_management_quality_event_source_links";
    public const string DocumentManagementQualityEvents = "document_management_quality_events";
    public const string DocumentManagementReleaseGateEvaluations = "document_management_release_gate_evaluations";
    public const string DocumentManagementReleaseGateEvidence = "document_management_release_gate_evidence";
    public const string DocumentManagementReleaseGateResults = "document_management_release_gate_results";
    public const string DocumentManagementRepositoryAssessmentFindings = "document_management_repository_assessment_findings";
    public const string DocumentManagementRepositoryAssessments = "document_management_repository_assessments";
    public const string DocumentManagementRepositoryDowntimeEvents = "document_management_repository_downtime_events";
    public const string DocumentManagementRetentionPolicies = "document_management_retention_policies";
    public const string DocumentManagementRetentionSubjects = "document_management_retention_subjects";
    public const string DocumentManagementRetirementCases = "document_management_retirement_cases";
    public const string DocumentManagementSignaturePolicies = "document_management_signature_policies";
    public const string DocumentManagementSignatureRecords = "document_management_signature_records";
    public const string DocumentManagementSignatureRequests = "document_management_signature_requests";
    public const string DocumentManagementSignedObjectFingerprints = "document_management_signed_object_fingerprints";
    public const string DocumentManagementSuspensionCases = "document_management_suspension_cases";
    public const string DocumentManagementTemplateDocuments = "document_management_template_documents";
    public const string DocumentManagementTemplateMasterVersions = "document_management_template_master_versions";
    public const string DocumentManagementTemplateMasters = "document_management_template_masters";
    public const string DocumentManagementTemplateVariants = "document_management_template_variants";
    public const string DocumentManagementTemplateVersions = "document_management_template_versions";
    public const string DocumentManagementTemporaryControlledIssues = "document_management_temporary_controlled_issues";
    public const string DocumentManagementTemporaryInstructionControls = "document_management_temporary_instruction_controls";
    public const string DocumentManagementTrainingAssignments = "document_management_training_assignments";
    public const string DocumentManagementTrainingRequirements = "document_management_training_requirements";
    public const string DocumentManagementVariantLocalizationProfiles = "document_management_variant_localization_profiles";
    public const string DocumentManagementVariantParentChangeAssessments = "document_management_variant_parent_change_assessments";
    public const string DocumentManagementVariantReviewEvidence = "document_management_variant_review_evidence";
    public const string DocumentReferenceEntries = "document_reference_entries";
    public const string DocumentReferenceListVersions = "document_reference_list_versions";
    public const string FeatureCategories = "platform_feature_categories";
    public const string InterfaceActiveSnapshots = "platform_interface_active_snapshots";
    public const string InterfaceDefinitions = "platform_interface_definitions";
    public const string InterfaceDiscoveryBatches = "platform_interface_discovery_batches";
    public const string InterfaceDiscoveryDiffItems = "platform_interface_discovery_diff_items";
    public const string JobExecutionLogs = "job_execution_logs";
    public const string ModuleCatalog = "platform_module_catalog";
    public const string ModuleDomains = "platform_module_domains";
    public const string ModulePageActionDescriptors = "platform_module_page_action_descriptors";
    public const string ModulePageDescriptors = "platform_module_page_descriptors";
    public const string ModuleServices = "platform_module_services";
    public const string NotificationDispatches = "notification_dispatches";
    public const string NotificationEventDefinitions = "notification_event_definitions";
    public const string NotificationTemplates = "notification_templates";
    public const string OrganizationUnits = "organization_units";
    public const string OutboxEvents = "outbox_events";
    public const string PlanFeatureMappings = "platform_plan_feature_mappings";
    public const string PlatformAdministrators = "platform_administrators";
    public const string PositionAssignments = "position_assignments";
    public const string Positions = "positions";
    public const string QuotaEvents = "quota_events";
    public const string QuotaUsages = "quota_usages";
    public const string SavedViews = "saved_views";
    public const string SubscriptionFeatures = "platform_subscription_features";
    public const string SubscriptionPlans = "platform_subscription_plans";
    public const string TaskAssignments = "task_assignments";
    public const string TaskComments = "task_comments";
    public const string TaskDependencies = "task_dependencies";
    public const string TaskFieldDefinitions = "task_field_definitions";
    public const string TaskItems = "task_items";
    public const string TaskPersonalOverlays = "task_personal_overlays";
    public const string TaskRecurrenceRules = "task_recurrence_rules";
    public const string TaskTemplates = "task_templates";
    public const string TaskTransitions = "task_transitions";
    public const string TaskTypes = "task_types";
    public const string TaskWatchers = "task_watchers";
    public const string TenantDomains = "tenant_domains";
    public const string TenantLoginSettings = "tenant_login_settings";
    public const string TenantMessagingSettings = "notification_tenant_messaging_settings";
    public const string TenantModuleEntitlements = "tenant_module_entitlements";
    public const string TenantNavDomainPreferences = "tenant_nav_domain_preferences";
    public const string TenantNavPreferences = "tenant_nav_preferences";
    public const string TenantSubscriptions = "tenant_subscriptions";
    public const string Tenants = "tenants";
    public const string UserNotifications = "notification_user_notifications";
    public const string WorkflowInstances = "workflow_instances";
    public const string WorkflowRuntimeAssignmentSnapshots = "workflow_runtime_assignment_snapshots";
    public const string WorkflowSlaRules = "workflow_sla_rules";
    public const string WorkflowTemplateVersions = "workflow_template_versions";
    public const string WorkflowTemplates = "workflow_templates";
    public const string WorkflowTransitionLogs = "workflow_transition_logs";
    public const string WorkingCalendarImportBatches = "working_calendar_import_batches";
    public const string WorkingCalendars = "working_calendars";
}
