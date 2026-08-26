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
         * ⚠ NO DECLARED INDEX — and that is a FINDING, not a decision. NotificationEventDefinitionRepository reads this collection, but the
         * index configuration never named it, so every query against it is a collection scan. It is listed
         * here because the manifest is the registry of what EXISTS; leaving it out is what let it go
         * unindexed unnoticed in the first place. Sizing the right index is backlog, not this round.
         */
        Collection<NotificationEventDefinition>(
            SchemaProfile.Notification,
            PlatformCollections.NotificationEventDefinitions,
            () => Array.Empty<CreateIndexModel<NotificationEventDefinition>>()),
    };
}
