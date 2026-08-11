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
        // BL-057 — MOD-0024's translation of the scopes above into "may I hand work to this position?". It
        // CONSUMES IDataScopeResolver rather than recomputing anything; see TaskAssignmentScopeResolver.
        services.AddScoped<Features.Tasks.Services.ITaskAssignmentScopeResolver,
            Features.Tasks.Services.TaskAssignmentScopeResolver>();
        services.AddScoped<ITenantModuleAccessService, TenantModuleAccessService>();
        services.AddScoped<IActorSafetyGuard, ActorSafetyGuard>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<IPlatformLookupProvider, PlatformLookupProvider>();
        services.AddScoped<Features.ModuleCatalog.Services.IModuleTaxonomyResolver, Features.ModuleCatalog.Services.ModuleTaxonomyResolver>();
        services.AddScoped<IBusinessReferenceDataValidationService, BusinessReferenceDataValidationService>();
        services.AddScoped<IBusinessReferenceDataPublishService, BusinessReferenceDataPublishService>();
        services.AddScoped<IBusinessReferenceDataImportService, BusinessReferenceDataImportService>();
        services.AddScoped<IBusinessReferenceDataImportParser, CsvBusinessReferenceDataImportParser>();
        services.AddScoped<IBusinessReferenceDataImportParser, JsonBusinessReferenceDataImportParser>();
        services.AddScoped<IBusinessReferenceDataImportParser, XlsxBusinessReferenceDataImportParser>();
        // MOD-0028-FU02 QMS folder baseline import services (dependency-free xlsx parsing, deterministic build/hash).
        services.AddScoped<Features.DocumentManagementQmsBaseline.Services.IQmsFolderImportParser,
            Features.DocumentManagementQmsBaseline.Services.XlsxQmsFolderImportParser>();
        // MOD-0028-FU06 register-backed CSV / flat-JSON parsers for the GMG-QMS-LOG-0007 package.
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
        services.AddScoped<Features.Tasks.Services.ITaskApprovalService,
            Features.Tasks.Services.TaskApprovalService>();
        // Phase 3b — the REVIEW handoff: the same engine asked a second question, never a second engine.
        services.AddScoped<Features.Tasks.Services.ITaskReviewService,
            Features.Tasks.Services.TaskReviewService>();

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
        // WC-1b (DCP-004) — Görev Merkezi / Task Center tenant module (entitlement-gated, NOT baseline).
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.WorkAggregation.SelfRegistration.WorkAggregationManifestProvider>();
        // MOD-0024 — Task Engine. Declares its permission keys (so the manifest, not the A1 reflection worker,
        // owns their Module/Scope attribution) and its email notification events.
        services.AddSingleton<Contracts.IModuleManifestProvider, Features.Tasks.SelfRegistration.TaskManifestProvider>();

        return services;
    }
}
