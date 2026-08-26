namespace Diten.ManagementGovernanceService.Persistence.Modules.Dws;

public sealed record DwsCollection(string Name, string Owner);
public sealed record DwsIndex(string Name, string Collection, IReadOnlyList<string> Keys, bool Unique, string? PartialFilter = null, bool Ttl = false);
public sealed record DwsTransactionFamily(string Name, IReadOnlyList<string> BusinessCollections)
{
    public static IReadOnlyList<string> TechnicalParticipants { get; } = new[] { "receipts", "audit-intents", "outbox" };
}

public static class DwsPersistenceOwnershipManifest
{
    public static IReadOnlyList<DwsCollection> Collections { get; } = new DwsCollection[]
    {
        new("mg_dws_structure_definitions", "StructureDefinition"), new("mg_dws_structure_revisions", "StructureRevision"),
        new("mg_dws_structure_nodes", "StructureNode"), new("mg_dws_structural_dependencies", "StructuralDependency"),
        new("mg_dws_structure_baselines", "StructureBaseline"), new("mg_dws_idempotency_receipts", "IdempotencyReceipt"),
        new("mg_dws_audit_intents", "AuditIntent"), new("mg_dws_outbox_messages", "OutboxMessage")
    };

    private static readonly string[] Tenant = ["TenantId"];
    private static string[] Keys(params string[] rest) => Tenant.Concat(rest).ToArray();

    public static IReadOnlyList<DwsIndex> Indexes { get; } = new DwsIndex[]
    {
        new("ix_dws_definitions_tenant_context", "definitions", Keys("ExternalContextReference.ContractName", "ExternalContextReference.ContractVersion", "ExternalContextReference.ContextKind", "ExternalContextReference.ContextId", "IsDeleted"), false),
        new("ux_dws_revisions_tenant_definition_number", "revisions", Keys("StructureDefinitionId", "RevisionNumber"), true, "IsDeleted=false"),
        new("ux_dws_revisions_tenant_definition_open", "revisions", Keys("StructureDefinitionId"), true, "IsDeleted=false, IsSealed=false"),
        new("ux_dws_nodes_tenant_revision_logical", "nodes", Keys("StructureRevisionId", "LogicalNodeId"), true, "IsDeleted=false"),
        new("ux_dws_nodes_tenant_revision_code", "nodes", Keys("StructureRevisionId", "Code"), true, "IsDeleted=false"),
        new("ux_dws_nodes_tenant_revision_parent_order", "nodes", Keys("StructureRevisionId", "ParentLogicalNodeId", "SiblingOrder"), true, "IsDeleted=false"),
        new("ux_dws_dependencies_tenant_revision_edge", "dependencies", Keys("StructureRevisionId", "FromLogicalNodeId", "ToLogicalNodeId"), true, "IsDeleted=false"),
        new("ux_dws_baselines_tenant_definition_number", "baselines", Keys("StructureDefinitionId", "BaselineNumber"), true, "IsDeleted=false"),
        new("ix_dws_baselines_tenant_definition_hash", "baselines", Keys("StructureDefinitionId", "CanonicalizationVersion", "ContentHash"), false),
        new("ux_dws_receipts_tenant_family_key", "receipts", Keys("CommandFamily", "IdempotencyKey"), true),
        new("ix_dws_receipts_tenant_created", "receipts", Keys("CreatedAtUtc"), false),
        new("ux_dws_audit_intents_tenant_identity", "audit intents", Keys("AuditIntentId"), true),
        new("ux_dws_outbox_tenant_event", "outbox", Keys("EventId"), true),
        new("ix_dws_outbox_tenant_delivery", "outbox", Keys("DeliveryState", "NextAttemptAtUtc"), false)
    };

    public static IReadOnlyList<DwsTransactionFamily> Transactions { get; } = new DwsTransactionFamily[]
    {
        new("CreateStructure", ["definitions", "revisions"]), new("UpdateStructureMetadata", ["revisions"]),
        new("AddStructureNode", ["revisions", "nodes"]), new("MoveStructureNode", ["revisions", "nodes"]),
        new("ReorderStructureNode", ["revisions", "nodes"]), new("RemoveStructureNode", ["revisions", "nodes", "dependencies"]),
        new("AddStructuralDependency", ["revisions", "dependencies"]), new("RemoveStructuralDependency", ["revisions", "dependencies"]),
        new("CreateStructureBaseline", ["definitions", "revisions", "baselines"]),
        new("CreateNextStructureRevision", ["definitions", "revisions", "nodes", "dependencies"])
    };
}

public sealed record DwsAtomicParticipant(string Name, Guid TenantId, int ExpectedVersion, string Value);
public sealed record DwsAtomicDocument(Guid TenantId, int Version, string Value);
public interface IDwsAtomicPersistence
{
    IReadOnlyDictionary<string, DwsAtomicDocument> Snapshot { get; }
    void Execute(Guid tenantId, IReadOnlyList<DwsAtomicParticipant> participants, int? failAfterParticipant = null);
}

public sealed class DwsContractAtomicPersistence : IDwsAtomicPersistence
{
    private Dictionary<string, DwsAtomicDocument> _state = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, DwsAtomicDocument> Snapshot => _state;
    public void Seed(string name, DwsAtomicDocument document) => _state.Add(name, document);
    public void Execute(Guid tenantId, IReadOnlyList<DwsAtomicParticipant> participants, int? failAfterParticipant = null)
    {
        if (tenantId == Guid.Empty || participants.Count == 0) throw new InvalidOperationException("dws_transaction_unavailable");
        var staged = new Dictionary<string, DwsAtomicDocument>(_state, StringComparer.Ordinal);
        for (var index = 0; index < participants.Count; index++)
        {
            var participant = participants[index];
            if (participant.TenantId != tenantId) throw new InvalidOperationException("dws_resource_not_found");
            if (staged.TryGetValue(participant.Name, out var current) && (current.TenantId != tenantId || current.Version != participant.ExpectedVersion)) throw new InvalidOperationException("dws_concurrency_conflict");
            if (!staged.ContainsKey(participant.Name) && participant.ExpectedVersion != 0) throw new InvalidOperationException("dws_concurrency_conflict");
            staged[participant.Name] = new(tenantId, checked(participant.ExpectedVersion + 1), participant.Value);
            if (failAfterParticipant == index + 1) throw new InvalidOperationException("injected_fault");
        }
        _state = staged;
    }
}

public enum DwsIsolationDimension { ProjectGraph, TypeNamespace, DependencyInjection, Repository, CollectionIndex, EntityBase, Permission, AuditOutbox, Contract, SessionTransaction, MigrationBootstrap, NegativeGuard }
public sealed record DwsSiblingEvidence(string Name, string ImmutableCheckpoint, string ModuleRoot, string AssemblyPrefix, string CollectionPrefix, string PermissionPrefix);
public static class DwsIsolationEvidenceManifest
{
    public static IReadOnlyList<DwsSiblingEvidence> Siblings { get; } = new[]
    {
        new DwsSiblingEvidence("ProcessModeling", "204ed1aa3dd0bdfeec2a3e4a89db386e1b845621", "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/Modules/ProcessModeling/", "Diten.ManagementGovernanceService.*.Modules.ProcessModeling", "mg_process_", "management-governance.process-modeling."),
        new DwsSiblingEvidence("DecisionRegistry", "2d354a97bfbe09ed665e44dba8665181d2a56d78", "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Domain/DecisionRegistry/", "Diten.ManagementGovernanceService.*.DecisionRegistry", "decision_registry_", "management-governance.decision-")
    };
    public static IReadOnlyList<(DwsSiblingEvidence Sibling, DwsIsolationDimension Dimension)> ExactEvidence { get; } = Siblings.SelectMany(s => Enum.GetValues<DwsIsolationDimension>().Select(d => (s, d))).ToArray();
}
