using Microsoft.Extensions.Logging.Abstractions;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
public sealed class CapacityLifecycleTests
{
    private sealed class CaptureLeaseStore(CapacityEvaluation evaluation) : ICapacityLeaseStore
    {
        private bool claimed;
        public string? Status { get; private set; }
        public IReadOnlyList<CapacityBottleneck>? Result { get; private set; }
        public Task<IReadOnlyList<CapacityScope>> FindPendingScopesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CapacityScope>>([new(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid())]);
        public Task<CapacityEvaluation?> ClaimAsync(CapacityScope scope,CancellationToken ct)
        {
            if(claimed)return Task.FromResult<CapacityEvaluation?>(null);
            claimed=true;return Task.FromResult<CapacityEvaluation?>(evaluation);
        }
        public Task<CapacityEvaluation?> RenewAsync(CapacityScope scope,CapacityEvaluation lease,CancellationToken ct) =>
            Task.FromResult<CapacityEvaluation?>(null);
        public Task<bool> TerminalAsync(CapacityScope scope,Guid id,int version,long fence,string status,
            IReadOnlyList<CapacityBottleneck> bottlenecks,bool expired,CancellationToken ct)
        {
            Status=status;Result=bottlenecks;return Task.FromResult(true);
        }
    }
    [Theory]
    [InlineData("Finite",1)]
    [InlineData("Infinite",0)]
    public async Task Approved_literal_oracle_only_emits_fixture_result(string mode,int count)
    {
        var evaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=DemandFixtureReader.Tenant,
            LegalEntityId=DemandFixtureReader.LegalEntity,EvaluationMode=mode,ResourceRefs=["line-4"],
            Status="Running",Attempt=1,Fence=1,Version=2};
        var store=new CaptureLeaseStore(evaluation);
        await new CapacityEvaluationExecutor(store,NullLogger<CapacityEvaluationExecutor>.Instance).RunOnceAsync(CancellationToken.None);
        Assert.Equal("CAPACITY-EVAL-FIXTURE-192-01@1",CapacityEvaluationExecutor.OracleId);
        Assert.Equal("Completed",store.Status);
        Assert.Equal(count,store.Result!.Count);
        if(mode=="Finite")
            Assert.Equal(new CapacityBottleneck("line-4","2027-W03","520.000","480.000","40.000","HOUR"),store.Result[0]);
    }
}
