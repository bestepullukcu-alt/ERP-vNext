namespace Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Mongo;

public sealed record IndexKey(string Field, int Direction);
public sealed record IndexDescriptor(string Collection, string Name, bool Unique, IReadOnlyList<IndexKey> Keys, TimeSpan? ExpireAfter = null);

public sealed class DecisionRegistryPersistenceManifest
{
    public const int Version = 1;
    public static DecisionRegistryPersistenceManifest Instance { get; } = new();
    private DecisionRegistryPersistenceManifest() { }

    public IReadOnlyList<string> Collections { get; } =
    [
        "decision_registry_records", "decision_registry_drafts", "decision_registry_revisions",
        "decision_registry_publication_history", "decision_registry_revision_replacements",
        "decision_registry_supersession_links", "decision_registry_idempotency_receipts",
        "decision_registry_audit_intents", "decision_registry_event_outbox"
    ];

    public IReadOnlyList<IndexDescriptor> Indexes { get; } =
    [
        I("decision_registry_records", "ux_dr_records_tenant_decision", true, ("TenantId",1),("DecisionId",1)),
        I("decision_registry_records", "ix_dr_records_tenant_subject_active", false, ("TenantId",1),("SubjectKind",1),("SubjectId",1),("IsDeleted",1),("LifecycleState",1)),
        I("decision_registry_drafts", "ux_dr_drafts_tenant_draft", true, ("TenantId",1),("DraftId",1)),
        I("decision_registry_drafts", "ix_dr_drafts_tenant_decision_active", false, ("TenantId",1),("DecisionId",1),("IsDeleted",1),("UpdatedAt",-1)),
        I("decision_registry_revisions", "ux_dr_revisions_tenant_revision", true, ("TenantId",1),("DecisionRevisionId",1)),
        I("decision_registry_revisions", "ux_dr_revisions_tenant_number", true, ("TenantId",1),("DecisionId",1),("DecisionRevisionNumber",1)),
        I("decision_registry_publication_history", "ux_dr_history_tenant_entry", true, ("TenantId",1),("PublicationHistoryId",1)),
        I("decision_registry_publication_history", "ix_dr_history_tenant_decision_time", false, ("TenantId",1),("DecisionId",1),("OccurredAtUtc",-1)),
        I("decision_registry_revision_replacements", "ux_dr_replacements_tenant_prior", true, ("TenantId",1),("DecisionId",1),("PriorDecisionRevisionId",1)),
        I("decision_registry_revision_replacements", "ix_dr_replacements_tenant_next", false, ("TenantId",1),("DecisionId",1),("NextDecisionRevisionId",1)),
        I("decision_registry_supersession_links", "ux_dr_supersession_tenant_source", true, ("TenantId",1),("SourceDecisionId",1)),
        I("decision_registry_supersession_links", "ix_dr_supersession_tenant_target", false, ("TenantId",1),("TargetDecisionId",1),("EffectiveAtUtc",-1)),
        I("decision_registry_idempotency_receipts", "ux_dr_receipts_tenant_scope_key", true, ("TenantId",1),("Operation",1),("EffectiveActorId",1),("S2SClientId",1),("IdempotencyKeyHash",1)),
        I("decision_registry_audit_intents", "ux_dr_audit_tenant_intent", true, ("TenantId",1),("AuditIntentId",1)),
        I("decision_registry_audit_intents", "ix_dr_audit_tenant_dispatch", false, ("TenantId",1),("DispatchState",1),("NextAttemptAtUtc",1),("OccurredAtUtc",1)),
        I("decision_registry_event_outbox", "ux_dr_outbox_tenant_event", true, ("TenantId",1),("EventId",1)),
        I("decision_registry_event_outbox", "ix_dr_outbox_tenant_claim", false, ("TenantId",1),("Status",1),("NextAttemptAtUtc",1),("CreatedAtUtc",1))
    ];

    public void Validate()
    {
        if (Collections.Count != 9 || Collections.Distinct(StringComparer.Ordinal).Count() != 9) throw new InvalidOperationException("Exactly nine unique collections are required.");
        if (Indexes.Count != 17 || Indexes.DistinctBy(x => x.Name, StringComparer.Ordinal).Count() != 17) throw new InvalidOperationException("Exactly seventeen unique indexes are required.");
        if (Indexes.Any(x => !Collections.Contains(x.Collection, StringComparer.Ordinal) || x.Keys.Count == 0 || x.Keys[0] != new IndexKey("TenantId", 1) || x.ExpireAfter is not null)) throw new InvalidOperationException("Indexes must be owned, tenant-first and non-TTL.");
    }

    private static IndexDescriptor I(string collection, string name, bool unique, params (string Field, int Direction)[] keys) =>
        new(collection, name, unique, keys.Select(x => new IndexKey(x.Field, x.Direction)).ToArray());
}
