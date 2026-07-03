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
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
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
        services.AddSingleton<IRecurringJobRegistrar, PlatformRecurringJobRegistrar>();

        // A3 — workflow transition gate (defence-in-depth): business modules inject this and must check it
        // BEFORE committing a state transition. Blocked ⇒ do not commit (not best-effort).
        services.AddScoped<Contracts.IWorkflowTransitionGate, Services.WorkflowTransitionGate>();

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

        return services;
    }
}
