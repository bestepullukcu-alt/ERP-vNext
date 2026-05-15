namespace Diten.Platform.Infrastructure.Persistence;

internal static class AuditCollectionNames
{
    internal const string AuditEvents = "audit_events";
    internal const string AuditEventRetentionPolicies = "audit_event_retention_policies";
    internal const string TenantAuditPreferences = "tenant_audit_preferences";
    internal const string AuditOutbox = "audit_outbox";
}
