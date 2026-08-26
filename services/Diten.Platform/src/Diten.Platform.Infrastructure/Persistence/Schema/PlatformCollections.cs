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
    public const string BusinessReferenceDataVersions = "business_reference_data_versions";
    public const string ChecklistRuns = "checklist_runs";
    public const string ChecklistTemplates = "checklist_templates";
    public const string ConsumedEvents = "consumed_events";
    public const string DocumentManagementAccessPolicies = "document_management_access_policies";
    public const string DocumentManagementBaselineReleases = "document_management_baseline_releases";
    public const string DocumentManagementBaselineSnapshotManifests = "document_management_baseline_snapshot_manifests";
    public const string DocumentManagementCollectionDefinitions = "document_management_collection_definitions";
    public const string DocumentManagementCollectionInstances = "document_management_collection_instances";
    public const string DocumentManagementControlledDocumentVersions = "document_management_controlled_document_versions";
    public const string DocumentManagementControlledDocuments = "document_management_controlled_documents";
    public const string DocumentManagementDocumentFavorites = "document_management_document_favorites";
    public const string DocumentManagementDocumentShares = "document_management_document_shares";
    public const string DocumentManagementFolderDocumentAccessPolicies = "document_management_folder_document_access_policies";
    public const string DocumentManagementFolderShareOperations = "document_management_folder_share_operations";
    public const string DocumentManagementFolderShareOutcomes = "document_management_folder_share_outcomes";
    public const string DocumentManagementInstantiationOperations = "document_management_instantiation_operations";
    public const string DocumentManagementInstantiationOutcomes = "document_management_instantiation_outcomes";
    public const string DocumentManagementTemplateDocuments = "document_management_template_documents";
    public const string DocumentManagementTemplateMasterVersions = "document_management_template_master_versions";
    public const string DocumentManagementTemplateMasters = "document_management_template_masters";
    public const string DocumentManagementTemplateVariants = "document_management_template_variants";
    public const string DocumentManagementTemplateVersions = "document_management_template_versions";
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
    public const string TaskDependencies = "task_dependencies";
    public const string TaskFieldDefinitions = "task_field_definitions";
    public const string TaskItems = "task_items";
    public const string TaskPersonalOverlays = "task_personal_overlays";
    public const string TaskRecurrenceRules = "task_recurrence_rules";
    public const string TaskTemplates = "task_templates";
    public const string TaskWatchers = "task_watchers";
    public const string TenantDomains = "tenant_domains";
    public const string TenantLoginSettings = "tenant_login_settings";
    public const string TenantMessagingSettings = "notification_tenant_messaging_settings";
    public const string TenantModuleEntitlements = "tenant_module_entitlements";
    public const string TenantNavDomainPreferences = "tenant_nav_domain_preferences";
    public const string TenantNavPreferences = "tenant_nav_preferences";
    public const string TenantSubscriptions = "tenant_subscriptions";
    public const string Tenants = "tenants";
    public const string WorkflowInstances = "workflow_instances";
    public const string WorkflowRuntimeAssignmentSnapshots = "workflow_runtime_assignment_snapshots";
    public const string WorkflowSlaRules = "workflow_sla_rules";
    public const string WorkflowTemplateVersions = "workflow_template_versions";
    public const string WorkflowTemplates = "workflow_templates";
    public const string WorkflowTransitionLogs = "workflow_transition_logs";
    public const string BusinessReferenceDataValidationResults = "business_reference_data_validation_results";
    public const string DocumentReferenceEntries = "document_reference_entries";
    public const string NotificationEventDefinitions = "notification_event_definitions";
}
