using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
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
        var moduleCatalogDocuments = database.GetCollection<BsonDocument>("platform_module_catalog");
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
