using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
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
        var interfaceDefinitionCollection = database.GetCollection<InterfaceDefinition>("platform_interface_definitions");
        var interfaceDiscoveryBatchCollection = database.GetCollection<InterfaceDiscoveryBatch>("platform_interface_discovery_batches");
        var interfaceDiscoveryDiffCollection = database.GetCollection<InterfaceDiscoveryDiffItem>("platform_interface_discovery_diff_items");
        var interfaceActiveSnapshotCollection = database.GetCollection<InterfaceActiveSnapshot>("platform_interface_active_snapshots");
        var moduleCatalogDocuments = database.GetCollection<BsonDocument>("platform_module_catalog");
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

        await tenantCollection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_tenants_code" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Slug),
                new CreateIndexOptions { Unique = true, Name = "ux_tenants_slug" }),
            new CreateIndexModel<Tenant>(
                Builders<Tenant>.IndexKeys.Ascending(x => x.Domain),
                new CreateIndexOptions { Unique = true, Name = "ux_tenants_domain" }),
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
                new CreateIndexOptions { Unique = true, Name = "ux_tenant_domains_domain_name" }),
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
}
