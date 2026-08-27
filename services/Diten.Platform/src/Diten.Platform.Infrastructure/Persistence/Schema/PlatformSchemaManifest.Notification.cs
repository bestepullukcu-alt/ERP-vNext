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
    /// Messaging settings, templates and dispatches.
    /// </summary>
    private static readonly SchemaCollection[] NotificationCollections =
    {
        Collection<TenantMessagingSettings>(
            SchemaProfile.Notification,
            PlatformCollections.TenantMessagingSettings,
            () => new CreateIndexModel<TenantMessagingSettings>[]
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

            }),
        Collection<NotificationTemplate>(
            SchemaProfile.Notification,
            PlatformCollections.NotificationTemplates,
            () => new CreateIndexModel<NotificationTemplate>[]
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

            }),
        Collection<NotificationDispatch>(
            SchemaProfile.Notification,
            PlatformCollections.NotificationDispatches,
            () => new CreateIndexModel<NotificationDispatch>[]
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

            }),
        /*
         * BL-279 — ONE index, and it is the exception the tenant-first rule (DB-001) names rather than a
         * violation of it. NotificationEventDefinition derives from BaseEntity and carries NO TenantId:
         * "Global/platform record (no TenantId): tenants cannot create events." So the rule's platform-catalog
         * clause applies — what is verified here is a global unique key and IsDeleted:false behaviour, not a
         * TenantId prefix that does not exist on the document.
         *
         * The repository's four reads are {IsDeleted:false, EventCode}, {IsDeleted:false, Id}, and two list
         * shapes that filter on IsDeleted (+ optional facets) and sort by EventCode. A single {EventCode} index
         * serves all of them: the point lookup directly, and both lists as an in-order walk that drops the
         * blocking SORT. Measured: COLLSCAN and SORT->COLLSCAN before, FETCH->IXSCAN on both after.
         *
         * ⚠ UNIQUE BECAUSE THE ENTITY SAYS SO AND NOTHING WAS ENFORCING IT. "EventCode is unique + immutable
         * (rename = deprecate + new code)" — and both write paths (the repository's write-path guard and
         * NotificationEventSeed's idempotent upsert) are read-then-write checks on that code, which two
         * concurrent callers both pass. The repository's own comment claimed "a unique index on EventCode is
         * created best-effort at construction"; that code does not exist. This is where it lives now.
         * Partial on IsDeleted:false so a deprecated definition does not block re-registering its code.
         */
        Collection<NotificationEventDefinition>(
            SchemaProfile.Notification,
            PlatformCollections.NotificationEventDefinitions,
            () => new CreateIndexModel<NotificationEventDefinition>[]
            {
                    new CreateIndexModel<NotificationEventDefinition>(
                        Builders<NotificationEventDefinition>.IndexKeys.Ascending(x => x.EventCode),
                        new CreateIndexOptions<NotificationEventDefinition>
                        {
                            Unique = true,
                            Name = "ux_notification_event_definitions_event_code_active",
                            PartialFilterExpression = Builders<NotificationEventDefinition>.Filter.Eq(x => x.IsDeleted, false)
                        })
            }),
    };
}
