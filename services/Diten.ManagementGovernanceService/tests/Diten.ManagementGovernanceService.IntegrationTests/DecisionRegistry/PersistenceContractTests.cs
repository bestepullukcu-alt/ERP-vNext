using Diten.ManagementGovernanceService.Application.DecisionRegistry;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Migrations;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Mongo;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Transactions;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.DecisionRegistry;

public sealed class PersistenceContractTests
{
    [Fact] public void Manifest_is_exact_tenant_first_and_non_ttl()
    { var manifest=DecisionRegistryPersistenceManifest.Instance; manifest.Validate(); Assert.Equal(9,manifest.Collections.Count); Assert.Equal(17,manifest.Indexes.Count); Assert.All(manifest.Indexes,x=>{Assert.Equal(new IndexKey("TenantId",1),x.Keys[0]);Assert.Null(x.ExpireAfter);}); }

    [Fact] public void Initial_migration_is_quarantine_only()
    { var evidence=new DecisionRegistryInitialMigration().Inspect(); Assert.Equal(0,evidence.Imported+evidence.Rewritten+evidence.Deleted); Assert.True(evidence.ReadOnlyInventory); }

    [Fact] public async Task Unknown_commit_retries_commit_only_and_body_once()
    { var session=new FakeSession(2); var scope=new IdempotencyScope(Guid.NewGuid(),"publish",Guid.NewGuid(),null,"key"); var runner=new DecisionRegistryTransactionRunner(new FakeFactory(session),new FakeReceipt(),3); var bodies=0; var result=await runner.ExecuteAsync<int>((owned,_)=>{Assert.Same(session,owned);bodies++;return Task.FromResult(new TransactionExecution<int>(42,scope,"payload"));}); Assert.Equal(42,result); Assert.Equal(1,bodies); Assert.Equal(3,session.Commits); }

    [Theory]
    [InlineData(ReceiptReconciliationKind.MatchingCompleted, "ok")]
    [InlineData(ReceiptReconciliationKind.PayloadConflict, "conflict")]
    [InlineData(ReceiptReconciliationKind.MissingOrIndeterminate, "indeterminate")]
    public async Task Exhausted_unknown_commit_reconciles_without_rerunning_body(ReceiptReconciliationKind kind, string outcome)
    {
        var session=new FakeSession(3); var scope=new IdempotencyScope(Guid.NewGuid(),"publish",Guid.NewGuid(),null,"key");
        var receipt=kind==ReceiptReconciliationKind.MatchingCompleted ? new IdempotencyReceipt(Guid.NewGuid(),scope,"payload","result","decision",200,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow) : null;
        var runner=new DecisionRegistryTransactionRunner(new FakeFactory(session),new FakeReceipt(new ReceiptReconciliation(kind,receipt)),3); var bodies=0;
        async Task<int> Run()=>await runner.ExecuteAsync<int>((owned,_)=>{Assert.Same(session,owned);bodies++;return Task.FromResult(new TransactionExecution<int>(42,scope,"payload"));});
        if(outcome=="ok") Assert.Equal(42,await Run());
        else if(outcome=="conflict") await Assert.ThrowsAsync<DecisionRegistryConflictException>(Run);
        else await Assert.ThrowsAsync<DecisionRegistryIndeterminateException>(Run);
        Assert.Equal(1,bodies); Assert.Equal(3,session.Commits);
    }

    private sealed class FakeFactory(FakeSession session):IDecisionRegistryTransactionSessionFactory { public Task<IDecisionRegistryTransactionSession> OpenAsync(CancellationToken ct)=>Task.FromResult<IDecisionRegistryTransactionSession>(session); }
    private sealed class FakeSession(int failures):IDecisionRegistryTransactionSession { public int Commits{get;private set;} public Task BeginAsync(CancellationToken ct)=>Task.CompletedTask; public Task AbortAsync(CancellationToken ct)=>Task.CompletedTask; public ValueTask DisposeAsync()=>ValueTask.CompletedTask; public Task CommitAsync(CancellationToken ct){Commits++;if(Commits<=failures)throw new UnknownDecisionRegistryCommitException("unknown");return Task.CompletedTask;} }
    private sealed class FakeReceipt(ReceiptReconciliation? result=null):IIdempotencyReceiptReader { public Task<ReceiptReconciliation> ReconcileAsync(IdempotencyScope scope,string payloadHash,CancellationToken ct)=>Task.FromResult(result??new ReceiptReconciliation(ReceiptReconciliationKind.MissingOrIndeterminate,null)); }
}
