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
    /// Tenants, module catalog, subscriptions, quotas, the interface registry, job logs and saved views —
/// the slice every tenant-aware test needs and almost every other slice depends on.
    /// </summary>
    private static readonly SchemaCollection[] CoreCollections =
    {
        Collection<JobExecutionLog>(
            SchemaProfile.Core,
            PlatformCollections.JobExecutionLogs,
            () => new CreateIndexModel<JobExecutionLog>[]
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

            }),
        Collection<SavedView>(
            SchemaProfile.Core,
            PlatformCollections.SavedViews,
            () => new CreateIndexModel<SavedView>[]
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

            }),
        Collection<Tenant>(
            SchemaProfile.Core,
            PlatformCollections.Tenants,
            () => new CreateIndexModel<Tenant>[]
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

            }),
        Collection<TenantDomain>(
            SchemaProfile.Core,
            PlatformCollections.TenantDomains,
            () => new CreateIndexModel<TenantDomain>[]
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

            }),
        Collection<TenantLoginSettings>(
            SchemaProfile.Core,
            PlatformCollections.TenantLoginSettings,
            () => new CreateIndexModel<TenantLoginSettings>[]
            {
                    new CreateIndexModel<TenantLoginSettings>(
                        Builders<TenantLoginSettings>.IndexKeys.Ascending(x => x.TenantRefId),
                        new CreateIndexOptions { Unique = true, Name = "ux_tenant_login_settings_tenant_ref_id" })

            }),
        Collection<ModuleCatalogItem>(
            SchemaProfile.Core,
            PlatformCollections.ModuleCatalog,
            () => new CreateIndexModel<ModuleCatalogItem>[]
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

            }),
        Collection<ModuleDomain>(
            SchemaProfile.Core,
            PlatformCollections.ModuleDomains,
            () => new CreateIndexModel<ModuleDomain>[]
            {
                    new CreateIndexModel<ModuleDomain>(
                        Builders<ModuleDomain>.IndexKeys.Ascending(x => x.IsActive),
                        new CreateIndexOptions { Name = "ix_platform_module_domains_active" }),
                    new CreateIndexModel<ModuleDomain>(
                        Builders<ModuleDomain>.IndexKeys.Ascending(x => x.SortOrder),
                        new CreateIndexOptions { Name = "ix_platform_module_domains_sort_order" })
,
                    new CreateIndexModel<ModuleDomain>(
                        Builders<ModuleDomain>.IndexKeys.Ascending(x => x.CodeKey),
                        new CreateIndexOptions<ModuleDomain>
                        {
                            Unique = true,
                            Name = "ux_platform_module_domains_code_key",
                            // Uniqueness yalnız canlı kayıtlar arası — soft-deleted kod aynı normalized key ile yeni insert'i bloke etmez.
                            PartialFilterExpression = Builders<ModuleDomain>.Filter.Eq(x => x.IsDeleted, false)
                        })
            },
            failureHint:
                "The OLD Code-based unique index has already been dropped, so platform_module_domains now has NO domain-code uniqueness protection. Most likely a null/empty CodeKey slipped through — check the ModuleDomainDeduplicationMigration log."),
        Collection<ModuleService>(
            SchemaProfile.Core,
            PlatformCollections.ModuleServices,
            () => new CreateIndexModel<ModuleService>[]
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

            }),
        Collection<SeedMarker>(
            SchemaProfile.Core,
            SeedMarkerStore.CollectionName,
            () => new CreateIndexModel<SeedMarker>[]
            {
                    new CreateIndexModel<SeedMarker>(
                                Builders<SeedMarker>.IndexKeys.Ascending(x => x.Key),
                                new CreateIndexOptions { Unique = true, Name = "ux_platform_seed_markers_key" })
            }),
        Collection<ModulePageDescriptor>(
            SchemaProfile.Core,
            PlatformCollections.ModulePageDescriptors,
            () => new CreateIndexModel<ModulePageDescriptor>[]
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

            }),
        Collection<ModulePageActionDescriptor>(
            SchemaProfile.Core,
            PlatformCollections.ModulePageActionDescriptors,
            () => new CreateIndexModel<ModulePageActionDescriptor>[]
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

            }),
        Collection<PlatformAdministrator>(
            SchemaProfile.Core,
            PlatformCollections.PlatformAdministrators,
            () => new CreateIndexModel<PlatformAdministrator>[]
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

            }),
        Collection<SubscriptionPlan>(
            SchemaProfile.Core,
            PlatformCollections.SubscriptionPlans,
            () => new CreateIndexModel<SubscriptionPlan>[]
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

            }),
        Collection<TenantSubscription>(
            SchemaProfile.Core,
            PlatformCollections.TenantSubscriptions,
            () => new CreateIndexModel<TenantSubscription>[]
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

            }),
        Collection<TenantModuleEntitlement>(
            SchemaProfile.Core,
            PlatformCollections.TenantModuleEntitlements,
            () => new CreateIndexModel<TenantModuleEntitlement>[]
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

            }),
        Collection<TenantNavPreference>(
            SchemaProfile.Core,
            PlatformCollections.TenantNavPreferences,
            () => new CreateIndexModel<TenantNavPreference>[]
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

            }),
        Collection<TenantNavDomainPreference>(
            SchemaProfile.Core,
            PlatformCollections.TenantNavDomainPreferences,
            () => new CreateIndexModel<TenantNavDomainPreference>[]
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

            }),
        Collection<QuotaUsage>(
            SchemaProfile.Core,
            PlatformCollections.QuotaUsages,
            () => new CreateIndexModel<QuotaUsage>[]
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

            }),
        Collection<QuotaEvent>(
            SchemaProfile.Core,
            PlatformCollections.QuotaEvents,
            () => new CreateIndexModel<QuotaEvent>[]
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

            }),
        Collection<FeatureDefinition>(
            SchemaProfile.Core,
            PlatformCollections.SubscriptionFeatures,
            () => new CreateIndexModel<FeatureDefinition>[]
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

            }),
        Collection<FeatureCategory>(
            SchemaProfile.Core,
            PlatformCollections.FeatureCategories,
            () => new CreateIndexModel<FeatureCategory>[]
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

            }),
        Collection<PlanFeatureMapping>(
            SchemaProfile.Core,
            PlatformCollections.PlanFeatureMappings,
            () => new CreateIndexModel<PlanFeatureMapping>[]
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

            }),
        Collection<InterfaceDefinition>(
            SchemaProfile.Core,
            PlatformCollections.InterfaceDefinitions,
            () => new CreateIndexModel<InterfaceDefinition>[]
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

            }),
        Collection<InterfaceDiscoveryBatch>(
            SchemaProfile.Core,
            PlatformCollections.InterfaceDiscoveryBatches,
            () => new CreateIndexModel<InterfaceDiscoveryBatch>[]
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

            }),
        Collection<InterfaceDiscoveryDiffItem>(
            SchemaProfile.Core,
            PlatformCollections.InterfaceDiscoveryDiffItems,
            () => new CreateIndexModel<InterfaceDiscoveryDiffItem>[]
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

            }),
        Collection<InterfaceActiveSnapshot>(
            SchemaProfile.Core,
            PlatformCollections.InterfaceActiveSnapshots,
            () => new CreateIndexModel<InterfaceActiveSnapshot>[]
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

            }),
    };
}
