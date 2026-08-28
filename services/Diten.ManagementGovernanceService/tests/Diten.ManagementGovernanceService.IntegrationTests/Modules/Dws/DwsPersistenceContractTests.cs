using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

public sealed class DwsPersistenceContractTests
{
    [Fact] public void Ownership_manifest_is_exact_and_tenant_first()
    {
        Assert.Equal(8, DwsPersistenceOwnershipManifest.Collections.Count);
        Assert.Equal(14, DwsPersistenceOwnershipManifest.Indexes.Count);
        Assert.Equal(10, DwsPersistenceOwnershipManifest.Transactions.Count);
        Assert.Equal(8, DwsPersistenceOwnershipManifest.Collections.Select(x => x.Name).Distinct().Count());
        Assert.Equal(14, DwsPersistenceOwnershipManifest.Indexes.Select(x => x.Name).Distinct().Count());
        Assert.Equal(new[] { "mg_dws_structure_definitions", "mg_dws_structure_revisions", "mg_dws_structure_nodes", "mg_dws_structural_dependencies", "mg_dws_structure_baselines", "mg_dws_idempotency_receipts", "mg_dws_audit_intents", "mg_dws_outbox_messages" }, DwsPersistenceOwnershipManifest.Collections.Select(x => x.Name));
        Assert.Equal(new[] { "ix_dws_definitions_tenant_context", "ux_dws_revisions_tenant_definition_number", "ux_dws_revisions_tenant_definition_open", "ux_dws_nodes_tenant_revision_logical", "ux_dws_nodes_tenant_revision_code", "ux_dws_nodes_tenant_revision_parent_order", "ux_dws_dependencies_tenant_revision_edge", "ux_dws_baselines_tenant_definition_number", "ix_dws_baselines_tenant_definition_hash", "ux_dws_receipts_tenant_family_key", "ix_dws_receipts_tenant_created", "ux_dws_audit_intents_tenant_identity", "ux_dws_outbox_tenant_event", "ix_dws_outbox_tenant_delivery" }, DwsPersistenceOwnershipManifest.Indexes.Select(x => x.Name));
        Assert.Equal(new[] { "CreateStructure", "UpdateStructureMetadata", "AddStructureNode", "MoveStructureNode", "ReorderStructureNode", "RemoveStructureNode", "AddStructuralDependency", "RemoveStructuralDependency", "CreateStructureBaseline", "CreateNextStructureRevision" }, DwsPersistenceOwnershipManifest.Transactions.Select(x => x.Name));
        Assert.All(DwsPersistenceOwnershipManifest.Indexes, x => { Assert.Equal("TenantId", x.Keys[0]); Assert.False(x.Ttl); });
        Assert.All(DwsPersistenceOwnershipManifest.Transactions, x => Assert.Equal(3, DwsTransactionFamily.TechnicalParticipants.Count));
    }

    [Fact] public async Task Unknown_commit_retries_commit_only_and_body_once()
    {
        var session = new FakeSession(unknownCommits: 2); var factory = new FakeFactory(session); var body = 0;
        var value = await new DwsTransactionCoordinator(factory).ExecuteAsync(async (_, _) => { body++; await Task.Yield(); return 42; }, new Reconciler<int>(false, false, 0), default);
        Assert.Equal(42, value); Assert.Equal(1, body); Assert.Equal(3, session.CommitCount);
    }

    [Fact] public async Task Inactive_session_is_rejected_before_body_execution()
    {
        var body=0;var session=new InactiveSession();
        var error=await Assert.ThrowsAsync<DwsValidationException>(()=>new DwsTransactionCoordinator(new FakeFactory(session)).ExecuteAsync<int>((_,_)=>{body++;return Task.FromResult(1);},new Reconciler<int>(false,false,0),default));
        Assert.Equal(DwsErrors.TransactionUnavailable,error.Code);Assert.Equal(0,body);Assert.Equal(0,session.CommitCount);
    }

    [Fact] public async Task Durable_receipt_reconciles_without_body_replay()
    {
        var session = new FakeSession(unknownCommits: 3); var body = 0;
        var value = await new DwsTransactionCoordinator(new FakeFactory(session)).ExecuteAsync(async (_, _) => { body++; await Task.Yield(); return 42; }, new Reconciler<int>(true, false, 42), default);
        Assert.Equal(42, value); Assert.Equal(1, body); Assert.Equal(3, session.CommitCount);
    }

    [Fact] public async Task Conflicting_receipt_is_409()
    {
        var session = new FakeSession(3);
        var ex = await Assert.ThrowsAsync<DwsConflictException>(() => new DwsTransactionCoordinator(new FakeFactory(session)).ExecuteAsync((_, _) => Task.FromResult(1), new Reconciler<int>(false, true, 0), default));
        Assert.Equal(DwsErrors.IdempotencyConflict, ex.Code);
    }

    [Fact] public async Task Indeterminate_receipt_is_503()
    {
        var session = new FakeSession(3);
        var ex = await Assert.ThrowsAsync<DwsValidationException>(() => new DwsTransactionCoordinator(new FakeFactory(session)).ExecuteAsync((_, _) => Task.FromResult(1), new Reconciler<int>(false, false, 0), default));
        Assert.Equal(DwsErrors.CommitIndeterminate, ex.Code);
    }

    [Fact] public void Atomic_persistence_seam_applies_all_participants_with_CAS_or_rolls_back()
    {
        Assert.Equal(["receipts", "audit-intents", "outbox"], DwsTransactionFamily.TechnicalParticipants);
        Assert.All(DwsPersistenceOwnershipManifest.Transactions, family =>
            Assert.Equal(family.BusinessCollections.Count + 3, family.BusinessCollections.Count + DwsTransactionFamily.TechnicalParticipants.Count));
        var tenant = Guid.NewGuid(); var store = new DwsContractAtomicPersistence();
        store.Seed("revision", new(tenant, 1, "working"));
        var participants = new[] { new DwsAtomicParticipant("revision", tenant, 1, "sealed"), new DwsAtomicParticipant("baseline", tenant, 0, "hash"), new DwsAtomicParticipant("receipt", tenant, 0, "ok"), new DwsAtomicParticipant("audit", tenant, 0, "intent"), new DwsAtomicParticipant("outbox", tenant, 0, "pending") };
        Assert.Throws<InvalidOperationException>(() => store.Execute(tenant, participants, 3));
        Assert.Equal("working", store.Snapshot["revision"].Value); Assert.Single(store.Snapshot);
        store.Execute(tenant, participants); Assert.Equal(5, store.Snapshot.Count); Assert.Equal(2, store.Snapshot["revision"].Version);
        Assert.Throws<InvalidOperationException>(() => store.Execute(tenant, participants));
    }

    [Fact] public void Atomic_persistence_seam_rejects_cross_tenant_without_residue()
    {
        var tenant = Guid.NewGuid(); var store = new DwsContractAtomicPersistence();
        Assert.Throws<InvalidOperationException>(() => store.Execute(tenant, [new("revision", Guid.NewGuid(), 0, "foreign")]));
        Assert.Empty(store.Snapshot);
    }

    private sealed class FakeFactory(IDwsTransactionSession session) : IDwsTransactionSessionFactory { public Task<IDwsTransactionSession> BeginAsync(CancellationToken _) => Task.FromResult(session); }
    private sealed class FakeSession(int unknownCommits) : IDwsTransactionSession
    {
        public bool IsActive => true; public int CommitCount { get; private set; }
        public Task AbortAsync(CancellationToken _) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken _) { CommitCount++; if (CommitCount <= unknownCommits) throw new DwsUnknownCommitException(); return Task.CompletedTask; }
    }
    private sealed class Reconciler<T>(bool match, bool conflict, T? value) : IDwsReceiptReconciler<T> { public Task<DwsReconciliation<T>> ReconcileAsync(CancellationToken _) => Task.FromResult(new DwsReconciliation<T>(match, conflict, value)); }
    private sealed class InactiveSession:IDwsTransactionSession { public bool IsActive=>false;public int CommitCount{get;private set;}public Task CommitAsync(CancellationToken _){CommitCount++;return Task.CompletedTask;}public Task AbortAsync(CancellationToken _)=>Task.CompletedTask; }
}
