using Diten.BuildingBlocks.Eventing;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Security;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Services;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Contracts.Events;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Platform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ExceptionBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuditBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            // Innermost: maps BusinessReferenceData (PSS-012) coded domain exceptions to Response<T>.Fail
            // with the correct HTTP status, so its controller can stay thin and free of business error mapping.
            cfg.AddOpenBehavior(typeof(Features.BusinessReferenceData.BusinessReferenceDataExceptionBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(_ => { }, assembly);
        services.AddSingleton<IInterfaceRegistryAuditSink, NullInterfaceRegistryAuditSink>();
        services.AddSingleton<EventPayloadContractValidator>();
        services.AddScoped<IEventBus, EventBus>();
        services.AddScoped<ConsumedEventStore>();
        services.AddScoped<IPlatformCatalogContract, PlatformCatalogContract>();
        services.AddSingleton<ITemporaryAccessProvider, NoOpTemporaryAccessProvider>();
        services.AddScoped<IDataScopeResolver, OrgDataScopeResolver>();
        /*
         * The ONE surface MOD-0024 asks "who sits in which seat" through. Nine files used to inject the
         * assignment repository directly and each re-wrote the active-window rule; BL-071 moves that fact to
         * HCM, and this is the single file that will have to learn it.
         */
        services.AddScoped<Features.Tasks.Services.ITaskSeatDirectory,
            Features.Tasks.Services.TaskSeatDirectory>();
        // BL-057 — MOD-0024's translation of the scopes above into "may I hand work to this position?". It
        // CONSUMES IDataScopeResolver rather than recomputing anything; see TaskAssignmentScopeResolver.
        services.AddScoped<Features.Tasks.Services.ITaskAssignmentScopeResolver,
            Features.Tasks.Services.TaskAssignmentScopeResolver>();
        // BL-023 — turns that resolver's DESCENT into "my team". Walks nothing of its own.
        services.AddScoped<Features.Tasks.Services.ITaskTeamResolver,
            Features.Tasks.Services.TaskTeamResolver>();
        services.AddScoped<ITenantModuleAccessService, TenantModuleAccessService>();
        services.AddScoped<IActorSafetyGuard, ActorSafetyGuard>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<IPlatformLookupProvider, PlatformLookupProvider>();
        // Working Calendar read-only working-day seam — the capability's actual product; consumers call THIS in-process.
        services.AddScoped<Features.WorkingCalendar.Provider.IWorkingCalendarProvider, Features.WorkingCalendar.Provider.WorkingCalendarProvider>();
        services.AddScoped<Features.ModuleCatalog.Services.IModuleTaxonomyResolver, Features.ModuleCatalog.Services.ModuleTaxonomyResolver>();
        services.AddScoped<IBusinessReferenceDataValidationService, BusinessReferenceDataValidationService>();
        services.AddScoped<IBusinessReferenceDataPublicationEligibility, RuntimeBusinessReferenceDataPublicationEligibility>();
        services.AddScoped<IBusinessReferenceDataPublishCheckpointObserver, NoOpBusinessReferenceDataPublishCheckpointObserver>();
        services.AddScoped<IBusinessReferenceDataPublishService, BusinessReferenceDataPublishService>();
        services.AddScoped<IBusinessReferenceDataImportService, BusinessReferenceDataImportService>();
        services.AddScoped<IBusinessReferenceDataImportParser, CsvBusinessReferenceDataImportParser>();
        services.AddScoped<IBusinessReferenceDataImportParser, JsonBusinessReferenceDataImportParser>();
        services.AddScoped<IBusinessReferenceDataImportParser, XlsxBusinessReferenceDataImportParser>();
        // MOD-0028-FU02 QMS folder baseline import services (dependency-free xlsx parsing, deterministic build/hash).
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.IQmsFolderImportParser,
            Features.DocumentManagementQmsBaseline.Services.XlsxQmsFolderImportParser>();
        // QMS register import extension — governance identity pending; CSV / flat-JSON parsers for GMG-QMS-LOG-0007.
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.IQmsFolderImportParser,
            Features.DocumentManagementQmsBaseline.Services.CsvQmsFolderImportParser>();
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.IQmsFolderImportParser,
            Features.DocumentManagementQmsBaseline.Services.FlatJsonQmsFolderImportParser>();
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.QmsFolderTreeValidator>();
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.DottedOutlineTreeBuilder>();
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.QmsBaselineImportService>();
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.BaselineSnapshotHasher>();
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.QmsManualStructureService>();
        services.AddScoped<CompanyInstanceKeyFactory>();
        services.AddScoped<IInstantiationPlanner, InstantiationPlanner>();
        services.AddScoped<InstantiationService>();
        // MOD-0028-FU06 — tenant-owned, company-independent Corporate Collection Instance foundation.
        services.AddScoped<Features.DocumentManagementCorporateCollectionInstances.CorporateCollectionStoragePartitionBuilder>();
        services.AddScoped<Features.DocumentManagementCorporateCollectionInstances.CorporateCollectionFolderAccessEvaluator>();
        services.AddScoped<Features.DocumentManagementCorporateCollectionInstances.CorporateCollectionInstanceProvisioningService>();
        services.AddScoped<Features.DocumentManagementMasterRegister.Services.DocumentLinkScopeCompatibilityValidator>();
        // MOD-0029-FU01 — controlled documents / templates / versioning / sharing services.
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.DocumentKeyFactory>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.DocumentVersioningService>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.DocumentAccessEvaluator>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.ControlledDocumentService>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.TemplateService>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.TemplateSharingService>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.IFolderSharePlanner,
            Features.DocumentManagementControlledDocuments.Services.FolderSharePlanner>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.FolderShareService>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.FolderDocumentService>();
        services.AddScoped<Features.DocumentManagementControlledDocuments.Services.ControlledDocumentExplorerService>();
        services.AddScoped<Features.DocumentManagementTemplateMasters.Services.TemplateMasterService>();
        // MOD-0029-FU06 — Document Master Register (LOG-0001) governance projection service.
        services.AddScoped<Features.DocumentManagementMasterRegister.Services.DocumentMasterRegisterService>();
        // MOD-0029-FU36 — durable controlled-document registration orchestration.
        services.AddScoped<Features.DocumentManagementControlledDocumentRegistration.Services.ControlledDocumentRegistrationService>();
        // MOD-0029-FU07 — Permanent UID / Document Code allocation engine (ledger + atomic sequence counter).
        services.AddScoped<Features.DocumentManagementIdentifiers.Services.DocumentIdentifierAllocationService>();
        // MOD-0029-FU08 — controlled document lifecycle status engine.
        services.AddScoped<Features.DocumentManagementLifecycle.Services.DocumentLifecycleService>();
        // MOD-0029-FU09 — approval route matrix + segregation + evidence + the FU08 approval-gate adapter.
        services.AddScoped<Features.DocumentManagementApproval.Services.DocumentApprovalRouteResolver>();
        services.AddScoped<Features.DocumentManagementApproval.Services.DocumentSegregationRuleEvaluator>();
        services.AddScoped<Features.DocumentManagementApproval.Services.DocumentApprovalService>();
        services.AddScoped<Features.DocumentManagementLifecycle.IApprovedPendingEffectiveGate,
            Features.DocumentManagementApproval.Services.ApprovedPendingEffectiveGate>();
        // MOD-0029-FU10 — non-waivable release gate engine + the FU08 release-gate port adapter.
        services.AddScoped<Features.DocumentManagementReleaseGates.Services.DocumentReleaseGateEvaluator>();
        services.AddScoped<Features.DocumentManagementLifecycle.IReleaseGateEvaluationPort,
            Features.DocumentManagementReleaseGates.Services.ReleaseGateEvaluationPortAdapter>();
        // MOD-0029-FU11 — training matrix + readiness + the FU10 Gate 5 training port adapter.
        services.AddScoped<Features.DocumentManagementTraining.Services.DocumentTrainingMatrixResolver>();
        services.AddScoped<Features.DocumentManagementTraining.Services.DocumentTrainingReadinessEvaluator>();
        services.AddScoped<Features.DocumentManagementTraining.Services.DocumentTrainingService>();
        services.AddScoped<Features.DocumentManagementReleaseGates.ITrainingReadinessPort,
            Features.DocumentManagementTraining.Services.TrainingReadinessPortAdapter>();
        // MOD-0029-FU12 — periodic review / extension / overdue engine.
        services.AddScoped<Features.DocumentManagementPeriodicReview.Services.DocumentPeriodicReviewStatusEvaluator>();
        services.AddScoped<Features.DocumentManagementPeriodicReview.Services.DocumentPeriodicReviewService>();
        // MOD-0029-FU13 — suspension / urgent withdrawal / retirement / temporary-instruction governance.
        services.AddScoped<Features.DocumentManagementSuspension.Services.DocumentSuspensionService>();
        services.AddScoped<Features.DocumentManagementSuspension.Services.DocumentRetirementService>();
        services.AddScoped<Features.DocumentManagementSuspension.Services.TemporaryInstructionService>();
        // MOD-0029-FU16 — repository assessment / DMS boundary + the FU10 Gate 2 repository port adapter.
        services.AddScoped<Features.DocumentManagementRepositoryAssessment.Services.DocumentRepositoryAssessmentEvaluator>();
        services.AddScoped<Features.DocumentManagementRepositoryAssessment.Services.DocumentRepositoryAssessmentService>();
        services.AddScoped<Features.DocumentManagementReleaseGates.IRepositoryReadinessPort,
            Features.DocumentManagementRepositoryAssessment.Services.RepositoryReadinessPortAdapter>();
        // MOD-0029-FU17 — controlled copy / obsolete reconciliation + the FU10 Gate 6 and FU13 withdrawal adapters.
        services.AddScoped<Features.DocumentManagementControlledCopy.Services.DocumentControlledCopyReadinessEvaluator>();
        services.AddScoped<Features.DocumentManagementControlledCopy.Services.DocumentControlledCopyService>();
        services.AddScoped<Features.DocumentManagementReleaseGates.ICopyReconciliationPort,
            Features.DocumentManagementControlledCopy.Services.CopyReconciliationPortAdapter>();
        services.AddScoped<Features.DocumentManagementSuspension.IControlledCopyWithdrawalPort,
            Features.DocumentManagementControlledCopy.Services.ControlledCopyWithdrawalPortAdapter>();
        // MOD-0029-FU14 — external document register / monitoring / impact assessment orchestration.
        services.AddScoped<Features.DocumentManagementExternalDocuments.Services.ExternalDocumentRegisterService>();
        // MOD-0029-FU15 — retention schedule, litigation hold and disposition (no purge engine; evaluation is opt-in).
        services.AddScoped<Features.DocumentManagementRetention.Services.DocumentRetentionTriggerDateResolver>();
        services.AddScoped<Features.DocumentManagementRetention.Services.DocumentLegalHoldEvaluator>();
        services.AddScoped<Features.DocumentManagementRetention.Services.DocumentRetentionEvaluator>();
        services.AddScoped<Features.DocumentManagementRetention.Services.DocumentRetentionPolicyService>();
        services.AddScoped<Features.DocumentManagementRetention.Services.DocumentLegalHoldService>();
        services.AddScoped<Features.DocumentManagementRetention.Services.DocumentDispositionService>();
        // MOD-0029-FU18 — variant translation / site-adoption governance (metadata + evidence only; no content diff).
        services.AddScoped<Features.DocumentManagementVariantLocalization.Services.TemplateVariantLocalizationService>();
        services.AddScoped<Features.DocumentManagementVariantLocalization.Services.TemplateVariantParentChangeEvaluator>();
        // MOD-0029-FU20 — repository downtime + temporary controlled issue (no scheduler; evaluation is explicit).
        services.AddScoped<Features.DocumentManagementDowntime.Services.DocumentRepositoryDowntimeService>();
        services.AddScoped<Features.DocumentManagementDowntime.Services.DocumentTemporaryIssueService>();
        // MOD-0029-FU21 — GDocP correction trail. Additive to the central AuditBehavior; replaces no audit store.
        services.AddScoped<Features.DocumentManagementGDocPCorrection.Services.DocumentGDocPCorrectionEvaluator>();
        services.AddScoped<Features.DocumentManagementGDocPCorrection.Services.DocumentGDocPCorrectionService>();
        services.AddScoped<Features.DocumentManagementGDocPCorrection.Services.DocumentGDocPCorrectionPolicyService>();
        // Extension point for existing update commands; not injected into any of them in this FU.
        services.AddScoped<Features.DocumentManagementGDocPCorrection.Services.IGDocPCorrectionRecorder>(sp =>
            sp.GetRequiredService<Features.DocumentManagementGDocPCorrection.Services.DocumentGDocPCorrectionService>());
        // MOD-0029-FU22 — document-control scoped quality event / deviation / CAPA bridge. Not a QMS module.
        services.AddScoped<Features.DocumentManagementQualityEvent.Services.DocumentQualityEventService>();
        services.AddScoped<Features.DocumentManagementQualityEvent.Services.DocumentDeviationService>();
        services.AddScoped<Features.DocumentManagementQualityEvent.Services.DocumentCapaActionService>();
        services.AddScoped<Features.DocumentManagementQualityEvent.Services.DocumentQualityEventBridgeService>();
        // MOD-0029-FU23 — document-control scoped electronic signature foundation. NOT a regulated/qualified
        // e-signature capability: no external provider, no certificate validation, no compliance claim.
        services.AddScoped<Features.DocumentManagementElectronicSignature.Services.DocumentSignableSubjectResolver>();
        services.AddScoped<Features.DocumentManagementElectronicSignature.Services.DocumentSignatureBoundaryEvaluator>();
        services.AddScoped<Features.DocumentManagementElectronicSignature.Services.DocumentSignaturePolicyService>();
        services.AddScoped<Features.DocumentManagementElectronicSignature.Services.DocumentSignatureRequestService>();
        services.AddScoped<Features.DocumentManagementElectronicSignature.Services.DocumentSignatureService>();
        services.AddScoped<Features.DocumentManagementElectronicSignature.Services.DocumentSignatureVerificationService>();
        // MOD-0029-FU31 — SOP-aligned default governance policy pack seeder (tenant-scoped, idempotent, non-destructive).
        services.AddScoped<Features.DocumentManagementGovernancePolicyPack.DocumentGovernancePolicyPackSeeder>();
        // MOD-0029-FU31A — preview/apply orchestration + append-only application history over the FU31 seeder.
        services.AddScoped<Features.DocumentManagementGovernancePolicyPack.DocumentGovernancePolicyPackApplicationService>();
        // MOD-0029-FU32 — background governance sweep orchestrator. Observer only: no delete, purge, auto-close,
        // auto-approve, auto-effective, auto-disposition, auto-sign or auto-retire anywhere in it.
        services.AddScoped<Features.DocumentManagementGovernanceSweep.DocumentGovernanceSweepService>();
        // MOD-0029-FU03 — template variant governance + drift orchestration.
        services.AddScoped<Features.DocumentManagementTemplateVariants.Services.TemplateVariantService>();
        // MOD-0029-FU04 — document access matrix resolver + orchestration services.
        services.AddScoped<Features.DocumentManagementAccessMatrix.Services.DocumentAccessInheritanceResolver>();
        services.AddScoped<Features.DocumentManagementAccessMatrix.Services.DocumentAccessCompatibilityAdapter>();
        services.AddScoped<Features.DocumentManagementAccessMatrix.Services.DocumentAccessTargetResolver>();
        services.AddScoped<Features.DocumentManagementAccessMatrix.Services.DocumentAccessResolver>();
        services.AddScoped<Features.DocumentManagementAccessMatrix.Services.DocumentAccessMatrixService>();
        // MOD-0029-FU05 access-profile → policy template engine (read-only over MOD-0028, idempotent apply).
        services.AddScoped<Features.DocumentManagementAccessProfileTemplates.AccessProfilePolicyPlanner>();
        // MOD-0028-FU09 read-back reconciliation + provisioning evidence (sidecar, non-destructive).
        services.AddScoped<Features.DocumentManagementReconciliation.ICollectionTreeReadBackProvider,
            Features.DocumentManagementReconciliation.InHouseCollectionTreeReadBackProvider>();
        services.AddScoped<Features.DocumentManagementReconciliation.ICollectionTreeReadBackProvider,
            Features.DocumentManagementReconciliation.GoogleDriveCollectionTreeReadBackProvider>();
        services.AddScoped<Features.DocumentManagementReconciliation.CollectionTreeReconciliationService>();
        services.AddScoped<Features.DocumentManagementReconciliation.ProvisioningEvidenceService>();
        services.AddScoped<Features.DocumentManagementReconciliation.DeviationWorkflowService>();
        services.AddScoped<Features.DocumentManagementReconciliation.BaselineQualificationReadinessService>();
        services.AddScoped<IBusinessReferenceDataGovernanceService, BusinessReferenceDataGovernanceService>();
        // PSS-012 governance adapters. MOD-0023 (workflow) / MOD-0031 (evidence) are not yet implemented.
        // Mock stubs are registered ONLY in Development/Local/Test (governance mode = Mock); every other
        // environment defaults to Disabled mode where the mutation still proceeds but is explicitly marked
        // and audited as governance-disabled rather than silently treated as a successful mock workflow.
        // Governance events always flow to the central MOD-0021 audit trail via the real audit adapter.
        // (FailClosed / Live modes are documented in the pack as follow-up and currently resolve to the
        // Disabled adapter; true FailClosed and Live require GovernanceService + MOD-0023/MOD-0031 work.)
        var businessReferenceDataGovernanceMode = BusinessReferenceDataGovernanceModeResolver.Resolve();
        if (businessReferenceDataGovernanceMode == BusinessReferenceDataGovernanceMode.Mock)
        {
            services.AddScoped<IBusinessReferenceDataWorkflowAdapter, MockBusinessReferenceDataWorkflowAdapter>();
            services.AddScoped<IBusinessReferenceDataPostPublicationReviewHook, MockBusinessReferenceDataPostPublicationReviewHook>();
        }
        else
        {
            services.AddScoped<IBusinessReferenceDataWorkflowAdapter, DisabledBusinessReferenceDataWorkflowAdapter>();
            services.AddScoped<IBusinessReferenceDataPostPublicationReviewHook, DisabledBusinessReferenceDataPostPublicationReviewHook>();
        }
        services.AddScoped<IBusinessReferenceDataEvidenceAdapter, DefaultBusinessReferenceDataEvidenceAdapter>();
        services.AddScoped<IBusinessReferenceDataGovernanceAuditAdapter, AuditServiceBusinessReferenceDataGovernanceAuditAdapter>();
        services.AddScoped<IBusinessReferenceDataEventPublisher, DbBusinessReferenceDataEventPublisher>();
        services.AddScoped<IBusinessReferenceDataConsumerQueryService, BusinessReferenceDataConsumerQueryService>();
        services.AddScoped<IBusinessReferenceDataCatalogLoaderService, BusinessReferenceDataCatalogLoaderService>();
        services.AddScoped<IBusinessReferenceDataActiveMembershipService, BusinessReferenceDataActiveMembershipService>();
        services.AddScoped<ITenantMessagingSettingsResolver, TenantMessagingSettingsResolver>();
        services.AddScoped<Features.Notifications.Services.INotificationLocaleResolver, Features.Notifications.Services.TenantNotificationLocaleResolver>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<Features.Notifications.Services.INotificationEventManifestSyncService, Features.Notifications.Services.NotificationEventManifestSyncService>();
        // MOD-0027-FU04B — eventCode → dispatch adapter (resolves Active event + validates, delegates to the existing
        // QueueEmailNotificationCommand). Producers wiring it is a separate follow-up (FU04B-Tenant / FU04D).
        services.AddScoped<Features.Notifications.Services.INotificationEventDispatchAdapter, Features.Notifications.Services.NotificationEventDispatchAdapter>();
        services.AddScoped<TenantCreatedV1NotificationMapper>();
        services.AddScoped<TenantSuspendedV1NotificationMapper>();
        services.AddScoped<TenantReactivatedV1NotificationMapper>();
        services.AddScoped<INotificationEventMapper<TenantCreatedV1>>(sp => sp.GetRequiredService<TenantCreatedV1NotificationMapper>());
        services.AddScoped<INotificationEventMapper<TenantSuspendedV1>>(sp => sp.GetRequiredService<TenantSuspendedV1NotificationMapper>());
        services.AddScoped<INotificationEventMapper<TenantReactivatedV1>>(sp => sp.GetRequiredService<TenantReactivatedV1NotificationMapper>());
        services.AddSingleton<AuditBehaviorOptions>();
        services.AddSingleton<ISensitiveFieldRedactionRegistry, SensitiveFieldRedactionRegistry>();
        services.AddSingleton<ISensitiveFieldRedactor, SensitiveFieldRedactor>();
        services.AddSingleton<IAuditIdempotencyKeyBuilder, AuditIdempotencyKeyBuilder>();
        services.AddSingleton<IAuditRecursionGuard, AuditRecursionGuard>();
        services.AddScoped<IAuditRetentionPolicyResolver, AuditRetentionPolicyResolver>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditMetaAuditWriter, AuditMetaAuditWriter>();
        services.AddScoped<IJobExecutionLogWriter, JobExecutionLogWriter>();
        services.AddScoped<SchedulerSmokeTestJob>();
        services.AddScoped<DeferredPlatformJobHandler>();
        services.AddScoped<EmailDispatchJob>();
        services.AddScoped<EmailDispatchSweepJob>();
        services.AddScoped<Features.Workflow.BackgroundJobs.WorkflowEscalationSweepJob>();
        // WC-4 — the ONE place a task notification is sent from (opt-out, actor skip, real addresses,
        // never fails the write).
        services.AddScoped<Features.Tasks.Services.ITaskNotificationService,
            Features.Tasks.Services.TaskNotificationService>();

        // MOD-0024 Phase 4 — the recurrence sweep. Registered here so Hangfire can resolve it; whether it RUNS
        // is decided by BackgroundJobs:RegisterStandardJobs + EnabledJobs, both of which default to off.
        services.AddScoped<Features.Tasks.BackgroundJobs.TaskRecurrenceSweepJob>();
        services.AddScoped<Features.Tasks.BackgroundJobs.TaskDueSoonSweepJob>();
        services.AddSingleton<IRecurringJobRegistrar, PlatformRecurringJobRegistrar>();

        // A3 — workflow transition gate (defence-in-depth): business modules inject this and must check it
        // BEFORE committing a state transition. Blocked ⇒ do not commit (not best-effort).
        services.AddScoped<Contracts.IWorkflowTransitionGate, Services.WorkflowTransitionGate>();

        // WC-1 (DCP-004) — read-only work-item projection + provider abstraction. The
        // projection service is pure; providers are registered as an IEnumerable so WC-5 adds more without
        // rewrite. In WC-1 exactly one provider is bound (MOD-0023 approvals). No AuthService/seed touched.
        services.AddScoped<Features.WorkAggregation.Services.IWorkItemProjectionService,
            Features.WorkAggregation.Services.WorkItemProjectionService>();
        services.AddScoped<Features.WorkAggregation.Providers.IWorkItemProvider,
            Features.WorkAggregation.Providers.WorkflowApprovalWorkItemProvider>();
        // MOD-0024 — the SECOND work-item provider. This single line is the only WorkAggregation touch point:
        // WC-1's own code is untouched, which is exactly what the IWorkItemProvider seam exists for.
        services.AddScoped<Features.WorkAggregation.Providers.IWorkItemProvider,
            Features.Tasks.Providers.TaskWorkItemProvider>();

        /*
         * WC-D2 (DCP-004 §2 D2) — the WRITE half, registered as its own IEnumerable beside the read providers.
         *
         * A SIBLING COLLECTION, not extra methods on IWorkItemProvider: that seam declares itself read-only and
         * the aggregation handler's per-provider isolation depends on the claim staying true. A provider that
         * also accepts writes registers here as well; one that does not simply is not in this list, and its
         * actions are refused with WORK_ITEM_PROVIDER_NOT_DISPATCHABLE rather than silently succeeding.
         *
         * BOTH bound providers are dispatchable from the first day of the seam, on purpose: a dispatcher proven
         * on the one provider that already worked would prove nothing.
         */
        services.AddScoped<Features.WorkAggregation.Dispatch.IWorkItemActionDispatcher,
            Features.WorkAggregation.Providers.WorkflowApprovalWorkItemActionDispatcher>();
        services.AddScoped<Features.WorkAggregation.Dispatch.IWorkItemActionDispatcher,
            Features.Tasks.Providers.TaskWorkItemActionDispatcher>();

        // MOD-0024 Task Engine services. The lifecycle service is the SINGLE owner of the lifecycle→normalized
        // map, so the API and the Task Center projection can never disagree.
        services.AddScoped<Features.Tasks.Services.ITaskLifecycleService, Features.Tasks.Services.TaskLifecycleService>();
        services.AddScoped<Features.Tasks.Services.ITaskAssignmentResolver, Features.Tasks.Services.TaskAssignmentResolver>();
        services.AddScoped<Features.Tasks.Services.ITaskFieldDefinitionService,
            Features.Tasks.Services.TaskFieldDefinitionService>();

        /*
         * Configurable fields that point at ANOTHER MODULE'S RECORDS (SAP's check table, Oracle's
         * table-validated value set, ServiceNow's reference field).
         *
         * ⛔ THIS IS THE ONLY PLACE A SOURCE IS NAMED. Nothing downstream knows the word "position": the registry
         * is built from whatever ITaskRecordSource implementations are in the container, and every consumer
         * reaches one through it. Adding the Product module's records is adding a LINE HERE — no switch to
         * extend, no key list to update, no resolver to teach.
         */
        services.AddScoped<Features.Tasks.Services.ITaskRecordSource,
            Features.Tasks.Services.RecordSources.OrganizationUnitRecordSource>();
        services.AddScoped<Features.Tasks.Services.ITaskRecordSource,
            Features.Tasks.Services.RecordSources.PositionRecordSource>();
        services.AddScoped<Features.Tasks.Services.ITaskRecordSourceRegistry,
            Features.Tasks.Services.TaskRecordSourceRegistry>();
        // Phase 2 — the single owner of "may this task be completed?" (blocking checklist items).
        services.AddScoped<Features.Tasks.Services.ITaskChecklistService,
            Features.Tasks.Services.TaskChecklistService>();
        // Phase 3 — the approval handoff to MOD-0023 (charter Binding A). MOD-0024 starts and reads; it never
        // owns approval state.
        /*
         * DCP-005 slice 3 — the ONE place a citation is frozen.
         *
         * ⚠ REGISTERED, and the omission is worth the comment: the handlers take it as an OPTIONAL constructor
         * argument so every existing test construction stays valid, which means a missing registration compiles,
         * passes every unit test, and silently drops every citation at runtime. Measured live on 2026-08-26 —
         * the task saved, the form showed two documents, and the record carried none.
         */
        services.AddScoped<Features.Tasks.Services.TaskDocumentReferenceFreezer>();

        services.AddScoped<Features.Tasks.Services.ITaskApprovalService,
            Features.Tasks.Services.TaskApprovalService>();
        // Phase 3b — the REVIEW handoff: the same engine asked a second question, never a second engine.
        services.AddScoped<Features.Tasks.Services.ITaskReviewService,
            Features.Tasks.Services.TaskReviewService>();
        /*
         * BL-023 Part B — the UPWARD WORK REQUEST handoff: the same engine asked a THIRD question, never a
         * third engine. Beside it, the direction test that decides when to ask it — which reads the resolver's
         * existing ManagerChain scope rather than walking the chain again.
         */
        services.AddScoped<Features.Tasks.Services.ITaskAssignmentDirection,
            Features.Tasks.Services.TaskAssignmentDirection>();
        services.AddScoped<Features.Tasks.Services.ITaskUpwardRequestService,
            Features.Tasks.Services.TaskUpwardRequestService>();

        /*
         * WC-2 — the working-time seam and the SLA decision that rides on it.
         *
         * THIS LINE is what the working calendar (BL: Calendar) replaces: swap the implementation and every SLA
         * answer follows, because nothing downstream does date arithmetic of its own. The 24/7 calculator is a
         * deliberate, honest stand-in — see its own summary before deleting it as a placeholder.
         */
        services.AddSingleton<Features.WorkAggregation.Services.IWorkingTimeCalculator,
            Features.WorkAggregation.Services.TwentyFourSevenWorkingTimeCalculator>();
        services.AddScoped<Features.WorkAggregation.Services.IWorkItemSlaCalculator,
            Features.WorkAggregation.Services.WorkItemSlaCalculator>();

        // MC-3b — Platform-internal modules that self-register their catalog manifest in-process. Collected by
        // PlatformModuleSelfRegistrationWorker at startup. Add a line here for each new self-registering module.
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.Workflow.SelfRegistration.WorkflowManifestProvider>();
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.Organization.SelfRegistration.OrganizationManifestProvider>();
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.DocumentManagement.SelfRegistration.DocumentManagementManifestProvider>();
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.ReferenceData.SelfRegistration.ReferenceDataManifestProvider>();
        // FEAT-BASELINE-MODULES-S1 — Access Governance pilot baseline module (entitlement-free; per-user auth.* gate kept).
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.AccessGovernance.SelfRegistration.AccessGovernanceManifestProvider>();
        // FEAT-BASELINE-MODULES-S2 — Tenant Settings baseline module (Security Settings / Menu Settings; entitlement-free).
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.TenantSettingsModule.SelfRegistration.TenantSettingsManifestProvider>();
        // MOD-0149 — Commercial Suite CRM (Account Foundation). Reconciles the CRM catalog identity + /CRM/Accounts page
        // descriptor (nav-visible=false; static tenant-shell menu owns nav until the MOD-0285 migration).
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.Crm.SelfRegistration.CrmManifestProvider>();
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.Ppm.SelfRegistration.PpmManifestProvider>();
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.WorkingCalendar.SelfRegistration.WorkingCalendarManifestProvider>();
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.WorkingCalendarImport.WorkingCalendarImportManifestProvider>();
        // WC-1b (DCP-004) — Görev Merkezi / Task Center tenant module (entitlement-gated, NOT baseline).
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.WorkAggregation.SelfRegistration.WorkAggregationManifestProvider>();
        // MOD-0024 — Task Engine. Declares its permission keys (so the manifest, not the A1 reflection worker,
        // owns their Module/Scope attribution) and its email notification events.
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.Tasks.SelfRegistration.TaskManifestProvider>();

        return services;
    }
}
