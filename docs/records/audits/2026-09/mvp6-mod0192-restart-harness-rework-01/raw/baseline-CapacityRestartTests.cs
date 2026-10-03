using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
[CollectionDefinition("CapacityMongo",DisableParallelization=true)]
public sealed class CapacityMongoCollection { }
[Collection("CapacityMongo")]
public sealed class CapacityRestartTests
{
    private const string Connection = "mongodb://127.0.0.1:57192/?replicaSet=rsmod192&serverSelectionTimeoutMS=5000";
    private const string Database = "DitenSupplyChain_Mod0192_Test";
    private static readonly CapacityScope Scope=new(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.Parse("19200000-0000-4000-8000-000000000003"));
    [Fact]
    public async Task ChildWorker()
    {
        var mode=Environment.GetEnvironmentVariable("MOD192_CHILD_MODE");
        if(mode is null)return;
        var db=new MongoClient(Connection).GetDatabase(Database);
        var leaseStore=new CapacityLeaseStore(db);
        var lease=await leaseStore.ClaimAsync(Scope,CancellationToken.None);
        if(mode=="claim-terminal" && lease is not null)
        {
            var result=new CapacityBottleneck("line-4","2027-W03","520.000","480.000","40.000","HOUR");
            Assert.True(await leaseStore.TerminalAsync(Scope,lease.Id,lease.Version,lease.Fence,"Completed",[result],false,CancellationToken.None));
        }
    }
    private static Process StartWorker(string mode)
    {
        var project=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../Diten.SupplyChainService.Tests.csproj"));
        var info=new ProcessStartInfo("dotnet") {
            WorkingDirectory=Path.GetDirectoryName(project)!,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false };
        foreach(var arg in new[]{"test",project,"--no-build","--no-restore","--filter","FullyQualifiedName~CapacityRestartTests.ChildWorker","-v:q"})info.ArgumentList.Add(arg);
        info.Environment["DOTNET_ROLL_FORWARD"]="Major";
        info.Environment["MOD192_CHILD_MODE"]=mode;
        return Process.Start(info)!;
    }
    private static async Task AssertSuccess(Process process)
    {
        await process.WaitForExitAsync();
        var output=await process.StandardOutput.ReadToEndAsync();
        var error=await process.StandardError.ReadToEndAsync();
        Assert.True(process.ExitCode==0,$"Child exit {process.ExitCode}: {output} {error}");
    }
    [Fact]
    public async Task Separate_processes_claim_once_and_restart_recovers_after_server_lease_expiry()
    {
        if(Environment.GetEnvironmentVariable("MOD192_CHILD_MODE") is not null)return;
        var db=new MongoClient(Connection).GetDatabase(Database);
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await db.DropCollectionAsync(name);
        await new CapacitySchema(db).StartAsync(CancellationToken.None);
        var evaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=Scope.TenantId,LegalEntityId=Scope.LegalEntityId,
            CapacityPlanId=Guid.NewGuid(),ScenarioId=Guid.NewGuid(),Status="Accepted",EvaluationMode="Finite",ResourceRefs=["line-4"],
            SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid()};
        await db.GetCollection<CapacityEvaluation>("capacity_evaluations").InsertOneAsync(evaluation);
        await db.GetCollection<BsonDocument>("capacity_active_slots").InsertOneAsync(new BsonDocument {
            {"_id",Guid.NewGuid().ToString()},{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},
            {"PlanId",evaluation.CapacityPlanId.ToString()},{"ScenarioId",evaluation.ScenarioId.ToString()},
            {"EvaluationId",evaluation.Id.ToString()} });
        using var a=StartWorker("claim-only");using var b=StartWorker("claim-only");
        await Task.WhenAll(AssertSuccess(a),AssertSuccess(b));
        var first=await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==evaluation.Id).FirstAsync();
        Assert.Equal("Running",first.Status);Assert.Equal(1,first.Attempt);Assert.Equal(1,first.Fence);
        // This waits for the actual server-time 30-second lease; no app clock controls reclaim.
        await Task.Delay(TimeSpan.FromSeconds(31));
        using var restart=StartWorker("claim-terminal");await AssertSuccess(restart);
        var terminal=await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==evaluation.Id).FirstAsync();
        Assert.Equal("Completed",terminal.Status);Assert.Equal(2,terminal.Attempt);Assert.Equal(2,terminal.Fence);
        Assert.False(await new CapacityLeaseStore(db).TerminalAsync(Scope,evaluation.Id,first.Version,first.Fence,"Failed",[],false,CancellationToken.None));
        Assert.Equal(1,await db.GetCollection<BsonDocument>("capacity_outbox").CountDocumentsAsync(new BsonDocument("EventType","capacity.evaluation.completed.v1")));
        Assert.Equal(0,await db.GetCollection<BsonDocument>("capacity_active_slots").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
}
