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
    /// The audit trail. Separated from Core because a test that exercises RBAC or governance needs it
/// and a test that does not should not pay for four collections it never reads.
    /// </summary>
    private static readonly SchemaCollection[] AccessGovernanceCollections =
    {
        Collection<AuditEvent>(
            SchemaProfile.AccessGovernance,
            AuditCollectionNames.AuditEvents,
            () => new CreateIndexModel<AuditEvent>[]
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

            }),
        Collection<AuditEventRetentionPolicy>(
            SchemaProfile.AccessGovernance,
            AuditCollectionNames.AuditEventRetentionPolicies,
            () => new CreateIndexModel<AuditEventRetentionPolicy>[]
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

            }),
        Collection<TenantAuditPreference>(
            SchemaProfile.AccessGovernance,
            AuditCollectionNames.TenantAuditPreferences,
            () => new CreateIndexModel<TenantAuditPreference>[]
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

            }),
        Collection<AuditOutboxMessage>(
            SchemaProfile.AccessGovernance,
            AuditCollectionNames.AuditOutbox,
            () => new CreateIndexModel<AuditOutboxMessage>[]
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

            }),
    };
}
