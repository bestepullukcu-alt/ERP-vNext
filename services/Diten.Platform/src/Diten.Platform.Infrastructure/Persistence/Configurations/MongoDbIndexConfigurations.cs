using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories.ESignature;
using Diten.Platform.Infrastructure.Persistence.Repositories;
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
        var modulePageDescriptorCollection = database.GetCollection<ModulePageDescriptor>("platform_module_page_descriptors");
        var modulePageActionDescriptorCollection = database.GetCollection<ModulePageActionDescriptor>("platform_module_page_action_descriptors");
        var platformAdministratorCollection = database.GetCollection<PlatformAdministrator>("platform_administrators");
        var subscriptionPlanCollection = database.GetCollection<SubscriptionPlan>("platform_subscription_plans");
        var tenantSubscriptionCollection = database.GetCollection<TenantSubscription>("tenant_subscriptions");
        var tenantModuleEntitlementCollection = database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements");
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
        var hrisSourceProfileCollection = database.GetCollection<HrisSourceProfile>(HrisSourceCollectionNames.SourceProfiles);
        var hrisMappingProfileCollection = database.GetCollection<HrisMappingProfile>(HrisSourceCollectionNames.MappingProfiles);
        var hrisExternalIdentifierMapCollection = database.GetCollection<HrisExternalIdentifierMap>(HrisSourceCollectionNames.IdentifierMaps);
        var hrisSyncCheckpointCollection = database.GetCollection<HrisSyncCheckpoint>(HrisSourceCollectionNames.SyncCheckpoints);
        var hrisSourceHealthSnapshotCollection = database.GetCollection<HrisSourceHealthSnapshot>(HrisSourceCollectionNames.HealthSnapshots);
        var payrollExternalSystemProfileCollection = database.GetCollection<PayrollExternalSystemProfile>(PayrollSourceCollectionNames.ExternalSystemProfiles);
        var payrollContractProfileCollection = database.GetCollection<PayrollContractProfile>(PayrollSourceCollectionNames.ContractProfiles);
        var payrollEmployeeReferenceMapCollection = database.GetCollection<PayrollEmployeeReferenceMap>(PayrollSourceCollectionNames.EmployeeReferenceMaps);
        var payrollCycleReferenceCollection = database.GetCollection<PayrollCycleReference>(PayrollSourceCollectionNames.CycleReferences);
        var payrollResultReferenceCollection = database.GetCollection<PayrollResultReference>(PayrollSourceCollectionNames.ResultReferences);
        var payrollSourceHealthSnapshotCollection = database.GetCollection<PayrollSourceHealthSnapshot>(PayrollSourceCollectionNames.HealthSnapshots);
        var timeAttendanceProviderProfileCollection = database.GetCollection<TimeAttendanceExternalProviderProfile>(TimeAttendanceProviderCollectionNames.ProviderProfiles);
        var timeAttendanceContractProfileCollection = database.GetCollection<TimeAttendanceContractProfile>(TimeAttendanceProviderCollectionNames.ContractProfiles);
        var timeAttendanceEmployeeReferenceMapCollection = database.GetCollection<TimeAttendanceEmployeeReferenceMap>(TimeAttendanceProviderCollectionNames.EmployeeReferenceMaps);
        var timeAttendanceEventReferenceCollection = database.GetCollection<TimeAttendanceEventReference>(TimeAttendanceProviderCollectionNames.EventReferences);
        var attendanceSummaryReferenceCollection = database.GetCollection<AttendanceSummaryReference>(TimeAttendanceProviderCollectionNames.AttendanceSummaryReferences);
        var timeAttendanceSyncCheckpointCollection = database.GetCollection<TimeAttendanceSyncCheckpoint>(TimeAttendanceProviderCollectionNames.SyncCheckpoints);
        var timeAttendanceProviderHealthSnapshotCollection = database.GetCollection<TimeAttendanceProviderHealthSnapshot>(TimeAttendanceProviderCollectionNames.HealthSnapshots);
        var payrollIntegrationRunCollection = database.GetCollection<PayrollIntegrationRun>(PayrollIntegrationGovernanceCollectionNames.Runs);
        var payrollIntegrationSourceLinkCollection = database.GetCollection<PayrollIntegrationSourceLink>(PayrollIntegrationGovernanceCollectionNames.SourceLinks);
        var payrollIntegrationMappingControlCollection = database.GetCollection<PayrollIntegrationMappingControl>(PayrollIntegrationGovernanceCollectionNames.MappingControls);
        var payrollReconciliationControlCollection = database.GetCollection<PayrollReconciliationControl>(PayrollIntegrationGovernanceCollectionNames.ReconciliationControls);
        var payrollIntegrationExceptionCollection = database.GetCollection<PayrollIntegrationException>(PayrollIntegrationGovernanceCollectionNames.Exceptions);
        var payrollIntegrationRetryReplayRequestCollection = database.GetCollection<PayrollIntegrationRetryReplayRequest>(PayrollIntegrationGovernanceCollectionNames.RetryReplayRequests);
        var payrollIntegrationEvidenceExportReferenceCollection = database.GetCollection<PayrollIntegrationEvidenceExportReference>(PayrollIntegrationGovernanceCollectionNames.EvidenceExportReferences);
        var payrollIntegrationHealthSnapshotCollection = database.GetCollection<PayrollIntegrationHealthSnapshot>(PayrollIntegrationGovernanceCollectionNames.HealthSnapshots);
        var personReferenceProjectionCollection = database.GetCollection<PersonReferenceProjection>(PersonReferenceDirectoryCollectionNames.Projections);
        var personReferenceExternalCorrelationCollection = database.GetCollection<PersonReferenceExternalCorrelation>(PersonReferenceDirectoryCollectionNames.ExternalCorrelations);
        var personReferenceDirectoryHealthSnapshotCollection = database.GetCollection<PersonReferenceDirectoryHealthSnapshot>(PersonReferenceDirectoryCollectionNames.HealthSnapshots);
        var signatureEnvelopeCollection = database.GetCollection<SignatureEnvelope>(ESignatureCollectionNames.Envelopes);
        var signatureParticipantCollection = database.GetCollection<SignatureParticipant>(ESignatureCollectionNames.Participants);
        var signatureAttestationCollection = database.GetCollection<SignerAttestation>(ESignatureCollectionNames.Attestations);
        var signatureVerificationCollection = database.GetCollection<SignatureVerificationArtifact>(ESignatureCollectionNames.Verifications);
        var signatureArtifactCollection = database.GetCollection<InternalSignatureArtifact>(ESignatureCollectionNames.Artifacts);
        var signatureAuditExportCollection = database.GetCollection<SignatureAuditExport>(ESignatureCollectionNames.AuditExports);
        var signatureCounterCollection = database.GetCollection<SignatureEnvelopeCounter>(ESignatureCollectionNames.Counters);
        var moduleCatalogDocuments = database.GetCollection<BsonDocument>("platform_module_catalog");

        await signatureEnvelopeCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SignatureEnvelope>(
                Builders<SignatureEnvelope>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeNumber),
                new CreateIndexOptions<SignatureEnvelope>
                {
                    Unique = true,
                    Name = "ux_signature_envelopes_tenant_number_active",
                    PartialFilterExpression = Builders<SignatureEnvelope>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SignatureEnvelope>(
                Builders<SignatureEnvelope>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_signature_envelopes_tenant_status_created" }),
            new CreateIndexModel<SignatureEnvelope>(
                Builders<SignatureEnvelope>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType)
                    .Ascending(x => x.SubjectId)
                    .Ascending(x => x.SubjectVersion),
                new CreateIndexOptions { Name = "ix_signature_envelopes_subject" })
        });

        await signatureParticipantCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SignatureParticipant>(
                Builders<SignatureParticipant>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeId)
                    .Ascending(x => x.SigningOrder),
                new CreateIndexOptions<SignatureParticipant>
                {
                    Unique = true,
                    Name = "ux_signature_participants_envelope_order_active",
                    PartialFilterExpression = Builders<SignatureParticipant>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SignatureParticipant>(
                Builders<SignatureParticipant>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_signature_participants_envelope_status" })
        });

        await signatureAttestationCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<SignerAttestation>(
                Builders<SignerAttestation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeId)
                    .Ascending(x => x.ParticipantId),
                new CreateIndexOptions<SignerAttestation>
                {
                    Unique = true,
                    Name = "ux_signature_attestations_participant_active",
                    PartialFilterExpression = Builders<SignerAttestation>.Filter.Eq(x => x.IsDeleted, false)
                }));

        await signatureVerificationCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<SignatureVerificationArtifact>(
                Builders<SignatureVerificationArtifact>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeId)
                    .Descending(x => x.VerifiedAt),
                new CreateIndexOptions { Name = "ix_signature_verifications_envelope_verified" }));

        await signatureArtifactCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<InternalSignatureArtifact>(
                Builders<InternalSignatureArtifact>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeId)
                    .Ascending(x => x.ArtifactType),
                new CreateIndexOptions { Name = "ix_signature_artifacts_envelope_type" }));

        await signatureAuditExportCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<SignatureAuditExport>(
                Builders<SignatureAuditExport>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EnvelopeId)
                    .Descending(x => x.RequestedAt),
                new CreateIndexOptions { Name = "ix_signature_audit_exports_envelope_requested" }));

        await signatureCounterCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<SignatureEnvelopeCounter>(
                Builders<SignatureEnvelopeCounter>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Year),
                new CreateIndexOptions { Unique = true, Name = "ux_signature_counters_tenant_year" }));

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

        await hrisSourceProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<HrisSourceProfile>(
                Builders<HrisSourceProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HrisSourceProfile>
                {
                    Unique = true,
                    Name = "ux_hris_sources_tenant_code_active",
                    PartialFilterExpression = Builders<HrisSourceProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrisSourceProfile>(
                Builders<HrisSourceProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LifecycleState)
                    .Ascending(x => x.ProviderKind)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_hris_sources_tenant_lifecycle_provider" })
        });

        await hrisMappingProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<HrisMappingProfile>(
                Builders<HrisMappingProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceProfileId)
                    .Ascending(x => x.IsActive),
                new CreateIndexOptions { Name = "ix_hris_mapping_profiles_source_active" }),
            new CreateIndexModel<HrisMappingProfile>(
                Builders<HrisMappingProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HrisMappingProfile>
                {
                    Unique = true,
                    Name = "ux_hris_mapping_profiles_tenant_code_active",
                    PartialFilterExpression = Builders<HrisMappingProfile>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await hrisExternalIdentifierMapCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<HrisExternalIdentifierMap>(
                Builders<HrisExternalIdentifierMap>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceProfileId)
                    .Ascending(x => x.ExternalObjectType)
                    .Ascending(x => x.ExternalObjectId),
                new CreateIndexOptions<HrisExternalIdentifierMap>
                {
                    Unique = true,
                    Name = "ux_hris_external_ids_source_type_id_active",
                    PartialFilterExpression = Builders<HrisExternalIdentifierMap>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrisExternalIdentifierMap>(
                Builders<HrisExternalIdentifierMap>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.InternalReferenceType)
                    .Ascending(x => x.InternalReferenceId)
                    .Ascending(x => x.MappingState),
                new CreateIndexOptions { Name = "ix_hris_external_ids_internal_reference" })
        });

        await hrisSyncCheckpointCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<HrisSyncCheckpoint>(
                Builders<HrisSyncCheckpoint>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceProfileId)
                    .Ascending(x => x.SyncRunId),
                new CreateIndexOptions<HrisSyncCheckpoint>
                {
                    Unique = true,
                    Name = "ux_hris_sync_checkpoints_source_run_active",
                    PartialFilterExpression = Builders<HrisSyncCheckpoint>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HrisSyncCheckpoint>(
                Builders<HrisSyncCheckpoint>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceProfileId)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_hris_sync_checkpoints_latest" })
        });

        await hrisSourceHealthSnapshotCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<HrisSourceHealthSnapshot>(
                Builders<HrisSourceHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceProfileId)
                    .Descending(x => x.ObservedAt),
                new CreateIndexOptions { Name = "ix_hris_source_health_latest" }),
            new CreateIndexModel<HrisSourceHealthSnapshot>(
                Builders<HrisSourceHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HealthState)
                    .Descending(x => x.ObservedAt),
                new CreateIndexOptions { Name = "ix_hris_source_health_state" })
        });

        await payrollExternalSystemProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollExternalSystemProfile>(
                Builders<PayrollExternalSystemProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<PayrollExternalSystemProfile>
                {
                    Unique = true,
                    Name = "ux_payroll_sources_tenant_code_active",
                    PartialFilterExpression = Builders<PayrollExternalSystemProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollExternalSystemProfile>(
                Builders<PayrollExternalSystemProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LifecycleState)
                    .Ascending(x => x.ProviderFamily)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_payroll_sources_tenant_lifecycle_provider" })
        });

        await payrollContractProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollContractProfile>(
                Builders<PayrollContractProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Ascending(x => x.ContractVersion),
                new CreateIndexOptions<PayrollContractProfile>
                {
                    Unique = true,
                    Name = "ux_payroll_contract_profiles_source_version_active",
                    PartialFilterExpression = Builders<PayrollContractProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollContractProfile>(
                Builders<PayrollContractProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Descending(x => x.EffectiveFrom),
                new CreateIndexOptions { Name = "ix_payroll_contract_profiles_source_effective" })
        });

        await payrollEmployeeReferenceMapCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollEmployeeReferenceMap>(
                Builders<PayrollEmployeeReferenceMap>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Ascending(x => x.ExternalEmployeeReference),
                new CreateIndexOptions<PayrollEmployeeReferenceMap>
                {
                    Unique = true,
                    Name = "ux_payroll_employee_refs_source_external_active",
                    PartialFilterExpression = Builders<PayrollEmployeeReferenceMap>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollEmployeeReferenceMap>(
                Builders<PayrollEmployeeReferenceMap>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MappingState)
                    .Ascending(x => x.PersonReferenceId)
                    .Ascending(x => x.OrganizationUnitReferenceId)
                    .Ascending(x => x.PositionReferenceId),
                new CreateIndexOptions { Name = "ix_payroll_employee_refs_internal_references" })
        });

        await payrollCycleReferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollCycleReference>(
                Builders<PayrollCycleReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Ascending(x => x.ExternalPayCycleId),
                new CreateIndexOptions<PayrollCycleReference>
                {
                    Unique = true,
                    Name = "ux_payroll_cycles_source_external_active",
                    PartialFilterExpression = Builders<PayrollCycleReference>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollCycleReference>(
                Builders<PayrollCycleReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Descending(x => x.PeriodStart),
                new CreateIndexOptions { Name = "ix_payroll_cycles_source_period" })
        });

        await payrollResultReferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollResultReference>(
                Builders<PayrollResultReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Ascending(x => x.ExternalPayrollResultId)
                    .Ascending(x => x.ResultVersion),
                new CreateIndexOptions<PayrollResultReference>
                {
                    Unique = true,
                    Name = "ux_payroll_results_source_external_version_active",
                    PartialFilterExpression = Builders<PayrollResultReference>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollResultReference>(
                Builders<PayrollResultReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollCycleReferenceId)
                    .Descending(x => x.PublishedAt),
                new CreateIndexOptions { Name = "ix_payroll_results_cycle_published" })
        });

        await payrollSourceHealthSnapshotCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollSourceHealthSnapshot>(
                Builders<PayrollSourceHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PayrollExternalSystemProfileId)
                    .Descending(x => x.CheckedAt),
                new CreateIndexOptions { Name = "ix_payroll_source_health_latest" }),
            new CreateIndexModel<PayrollSourceHealthSnapshot>(
                Builders<PayrollSourceHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HealthState)
                    .Descending(x => x.CheckedAt),
                new CreateIndexOptions { Name = "ix_payroll_source_health_state" })
        });

        await timeAttendanceProviderProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeAttendanceExternalProviderProfile>(
                Builders<TimeAttendanceExternalProviderProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TimeAttendanceExternalProviderProfile>
                {
                    Unique = true,
                    Name = "ux_time_attendance_providers_tenant_code_active",
                    PartialFilterExpression = Builders<TimeAttendanceExternalProviderProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TimeAttendanceExternalProviderProfile>(
                Builders<TimeAttendanceExternalProviderProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LifecycleState)
                    .Ascending(x => x.ProviderFamily)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_time_attendance_providers_tenant_lifecycle_provider" })
        });

        await timeAttendanceContractProfileCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeAttendanceContractProfile>(
                Builders<TimeAttendanceContractProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Ascending(x => x.ContractVersion),
                new CreateIndexOptions<TimeAttendanceContractProfile>
                {
                    Unique = true,
                    Name = "ux_time_attendance_contract_profiles_provider_version_active",
                    PartialFilterExpression = Builders<TimeAttendanceContractProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TimeAttendanceContractProfile>(
                Builders<TimeAttendanceContractProfile>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Descending(x => x.EffectiveFrom),
                new CreateIndexOptions { Name = "ix_time_attendance_contract_profiles_provider_effective" })
        });

        await timeAttendanceEmployeeReferenceMapCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeAttendanceEmployeeReferenceMap>(
                Builders<TimeAttendanceEmployeeReferenceMap>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Ascending(x => x.ExternalEmployeeReference),
                new CreateIndexOptions<TimeAttendanceEmployeeReferenceMap>
                {
                    Unique = true,
                    Name = "ux_time_attendance_employee_refs_provider_external_active",
                    PartialFilterExpression = Builders<TimeAttendanceEmployeeReferenceMap>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TimeAttendanceEmployeeReferenceMap>(
                Builders<TimeAttendanceEmployeeReferenceMap>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MappingState)
                    .Ascending(x => x.HrisReferenceId)
                    .Ascending(x => x.PersonReferenceId)
                    .Ascending(x => x.OrganizationUnitReferenceId)
                    .Ascending(x => x.PositionReferenceId),
                new CreateIndexOptions { Name = "ix_time_attendance_employee_refs_internal_references" })
        });

        await timeAttendanceEventReferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeAttendanceEventReference>(
                Builders<TimeAttendanceEventReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<TimeAttendanceEventReference>
                {
                    Unique = true,
                    Name = "ux_time_attendance_events_provider_idempotency_active",
                    PartialFilterExpression = Builders<TimeAttendanceEventReference>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TimeAttendanceEventReference>(
                Builders<TimeAttendanceEventReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Descending(x => x.ProviderTimestamp),
                new CreateIndexOptions { Name = "ix_time_attendance_events_provider_timestamp" })
        });

        await attendanceSummaryReferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AttendanceSummaryReference>(
                Builders<AttendanceSummaryReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Ascending(x => x.ExternalSummaryId),
                new CreateIndexOptions<AttendanceSummaryReference>
                {
                    Unique = true,
                    Name = "ux_time_attendance_summaries_provider_external_active",
                    PartialFilterExpression = Builders<AttendanceSummaryReference>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<AttendanceSummaryReference>(
                Builders<AttendanceSummaryReference>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Descending(x => x.SummaryPeriodStart),
                new CreateIndexOptions { Name = "ix_time_attendance_summaries_provider_period" })
        });

        await timeAttendanceSyncCheckpointCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeAttendanceSyncCheckpoint>(
                Builders<TimeAttendanceSyncCheckpoint>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Ascending(x => x.SyncRunId),
                new CreateIndexOptions<TimeAttendanceSyncCheckpoint>
                {
                    Unique = true,
                    Name = "ux_time_attendance_sync_checkpoints_provider_run_active",
                    PartialFilterExpression = Builders<TimeAttendanceSyncCheckpoint>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TimeAttendanceSyncCheckpoint>(
                Builders<TimeAttendanceSyncCheckpoint>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_time_attendance_sync_checkpoints_latest" })
        });

        await timeAttendanceProviderHealthSnapshotCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TimeAttendanceProviderHealthSnapshot>(
                Builders<TimeAttendanceProviderHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProviderProfileId)
                    .Descending(x => x.CheckedAt),
                new CreateIndexOptions { Name = "ix_time_attendance_provider_health_latest" }),
            new CreateIndexModel<TimeAttendanceProviderHealthSnapshot>(
                Builders<TimeAttendanceProviderHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HealthState)
                    .Descending(x => x.CheckedAt),
                new CreateIndexOptions { Name = "ix_time_attendance_provider_health_state" })
        });

        await payrollIntegrationRunCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationRun>(
                Builders<PayrollIntegrationRun>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunCode),
                new CreateIndexOptions<PayrollIntegrationRun>
                {
                    Unique = true,
                    Name = "ux_payroll_integration_runs_tenant_code_active",
                    PartialFilterExpression = Builders<PayrollIntegrationRun>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollIntegrationRun>(
                Builders<PayrollIntegrationRun>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<PayrollIntegrationRun>
                {
                    Unique = true,
                    Name = "ux_payroll_integration_runs_tenant_idempotency_active",
                    PartialFilterExpression = Builders<PayrollIntegrationRun>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollIntegrationRun>(
                Builders<PayrollIntegrationRun>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Status).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_payroll_integration_runs_tenant_status_created" })
        });

        await payrollIntegrationSourceLinkCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationSourceLink>(
                Builders<PayrollIntegrationSourceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.SourceType).Ascending(x => x.SourceReferenceId),
                new CreateIndexOptions<PayrollIntegrationSourceLink>
                {
                    Unique = true,
                    Name = "ux_payroll_integration_source_links_run_source_active",
                    PartialFilterExpression = Builders<PayrollIntegrationSourceLink>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PayrollIntegrationSourceLink>(
                Builders<PayrollIntegrationSourceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.LinkState),
                new CreateIndexOptions { Name = "ix_payroll_integration_source_links_run_state" })
        });

        await payrollIntegrationMappingControlCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationMappingControl>(
                Builders<PayrollIntegrationMappingControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.MappingScope).Ascending(x => x.ControlState),
                new CreateIndexOptions { Name = "ix_payroll_integration_mapping_controls_run_scope_state" }),
            new CreateIndexModel<PayrollIntegrationMappingControl>(
                Builders<PayrollIntegrationMappingControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SourceReferenceId),
                new CreateIndexOptions { Name = "ix_payroll_integration_mapping_controls_source_reference" })
        });

        await payrollReconciliationControlCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollReconciliationControl>(
                Builders<PayrollReconciliationControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.ReconciliationType).Ascending(x => x.ControlState),
                new CreateIndexOptions { Name = "ix_payroll_reconciliation_controls_run_type_state" })
        });

        await payrollIntegrationExceptionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationException>(
                Builders<PayrollIntegrationException>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.ExceptionState).Ascending(x => x.Severity),
                new CreateIndexOptions { Name = "ix_payroll_integration_exceptions_run_state_severity" }),
            new CreateIndexModel<PayrollIntegrationException>(
                Builders<PayrollIntegrationException>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.AssignedToActorId).Ascending(x => x.ExceptionState),
                new CreateIndexOptions { Name = "ix_payroll_integration_exceptions_assignee_state" })
        });

        await payrollIntegrationRetryReplayRequestCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationRetryReplayRequest>(
                Builders<PayrollIntegrationRetryReplayRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<PayrollIntegrationRetryReplayRequest>
                {
                    Unique = true,
                    Name = "ux_payroll_integration_replay_requests_run_idempotency_active",
                    PartialFilterExpression = Builders<PayrollIntegrationRetryReplayRequest>.Filter.Eq(x => x.IsDeleted, false)
                })
        });

        await payrollIntegrationEvidenceExportReferenceCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationEvidenceExportReference>(
                Builders<PayrollIntegrationEvidenceExportReference>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Ascending(x => x.ExportState).Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_payroll_integration_evidence_exports_run_state_created" })
        });

        await payrollIntegrationHealthSnapshotCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PayrollIntegrationHealthSnapshot>(
                Builders<PayrollIntegrationHealthSnapshot>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RunId).Descending(x => x.CheckedAt),
                new CreateIndexOptions { Name = "ix_payroll_integration_health_run_checked" }),
            new CreateIndexModel<PayrollIntegrationHealthSnapshot>(
                Builders<PayrollIntegrationHealthSnapshot>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.HealthState).Descending(x => x.CheckedAt),
                new CreateIndexOptions { Name = "ix_payroll_integration_health_state_checked" })
        });

        await personReferenceProjectionCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PersonReferenceProjection>(
                Builders<PersonReferenceProjection>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<PersonReferenceProjection>
                {
                    Unique = true,
                    Name = "ux_person_reference_projections_tenant_code_active",
                    PartialFilterExpression = Builders<PersonReferenceProjection>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PersonReferenceProjection>(
                Builders<PersonReferenceProjection>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HrisSourceProfileId)
                    .Ascending(x => x.ReferenceState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_reference_projections_source_state" }),
            new CreateIndexModel<PersonReferenceProjection>(
                Builders<PersonReferenceProjection>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.PositionId)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_reference_projections_org_position" })
        });

        await personReferenceExternalCorrelationCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PersonReferenceExternalCorrelation>(
                Builders<PersonReferenceExternalCorrelation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HrisSourceProfileId)
                    .Ascending(x => x.CorrelationKey),
                new CreateIndexOptions<PersonReferenceExternalCorrelation>
                {
                    Unique = true,
                    Name = "ux_person_reference_correlations_source_key_active",
                    PartialFilterExpression = Builders<PersonReferenceExternalCorrelation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PersonReferenceExternalCorrelation>(
                Builders<PersonReferenceExternalCorrelation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PersonReferenceProjectionId)
                    .Ascending(x => x.CorrelationState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_reference_correlations_projection_state" }),
            new CreateIndexModel<PersonReferenceExternalCorrelation>(
                Builders<PersonReferenceExternalCorrelation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ExternalObjectReference)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = "ix_person_reference_correlations_external_reference" })
        });

        await personReferenceDirectoryHealthSnapshotCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<PersonReferenceDirectoryHealthSnapshot>(
                Builders<PersonReferenceDirectoryHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SnapshotKey)
                    .Descending(x => x.LastCheckedAt),
                new CreateIndexOptions { Name = "ix_person_reference_health_key_checked" }),
            new CreateIndexModel<PersonReferenceDirectoryHealthSnapshot>(
                Builders<PersonReferenceDirectoryHealthSnapshot>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Descending(x => x.LastCheckedAt)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_person_reference_health_latest" })
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

        await moduleCatalogCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModuleCatalogItem>(
                Builders<ModuleCatalogItem>.IndexKeys.Ascending(x => x.ModuleCode),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_module_catalog_module_code" }),
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

        await modulePageDescriptorCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.PageCode),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_module_pages_tenant_module_page_code" }),
            new CreateIndexModel<ModulePageDescriptor>(
                Builders<ModulePageDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ModuleCode)
                    .Ascending(x => x.RoutePath),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_module_pages_tenant_module_route_path" }),
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

        await modulePageActionDescriptorCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ModulePageActionDescriptor>(
                Builders<ModulePageActionDescriptor>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PageDescriptorId)
                    .Ascending(x => x.ActionCode),
                new CreateIndexOptions { Unique = true, Name = "ux_platform_module_page_actions_tenant_page_action" }),
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
