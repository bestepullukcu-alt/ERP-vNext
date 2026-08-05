using Diten.ManagementGovernanceService.Application.DecisionRegistry;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.ManagementGovernanceService.IntegrationTests.DecisionRegistry;

public sealed class MongoConcurrentRaceMatrixTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Real_replica_set_concurrent_race_matrix_is_fail_closed()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0007_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri)) return;
        await EnsureReplicaSetAsync();
        var client = new MongoClient(uri);
        var context = new DecisionRegistryMongoContext(client,"mod0007_races_"+Guid.NewGuid().ToString("N"));
        try
        {
            await new DecisionRegistryMongoIndexInitializer(context).InitializeAsync();
            await IdentityRace(client,context);
            await PublicationRace(client,context);
            await DuplicateAndInverseRace(client,context);
            await SupersessionWithdrawalRace(client,context);
            await PublicationDispositionRace(client,context,"Superseded");
            await PublicationDispositionRace(client,context,"Withdrawn");
            await SelfCycleAndTerminalFence(client,context);
        }
        finally { await context.DropAsync(); }
    }

    private async Task IdentityRace(MongoClient client, DecisionRegistryMongoContext context)
    {
        var tenant=Guid.NewGuid(); var decision=Guid.NewGuid(); var barrier=new Barrier(2);
        async Task<RaceResult> Create() => await Run(client,barrier,async session =>
        {
            var writer=new DecisionRegistryMongoMutationWriter(context,NoFault.Instance);
            await writer.WriteCreateAsync(new DecisionRegistryMongoTransactionSession(session),Record(tenant,decision,"Unpublished",1),Receipt(tenant,"create"),Audit(tenant),Outbox(tenant),default);
        });
        var results=await Task.WhenAll(Task.Run(Create),Task.Run(Create)); AssertOneWinner(results);
        Assert.Equal(1,await Count(context,0,tenant,"DecisionId",decision)); Assert.Equal(1,await Count(context,6,tenant)); Assert.Equal(1,await Count(context,7,tenant)); Assert.Equal(1,await Count(context,8,tenant));
        output.WriteLine($"identity statuses={Statuses(results)} records=1 receipts=1 audits=1 outbox=1");
    }

    private async Task PublicationRace(MongoClient client, DecisionRegistryMongoContext context)
    {
        var tenant=Guid.NewGuid(); var decision=Guid.NewGuid(); await context.Collection(0).InsertOneAsync(Record(tenant,decision,"Unpublished",1)); var barrier=new Barrier(2);
        async Task<RaceResult> Publish() => await Run(client,barrier,async session =>
        {
            var writer=new DecisionRegistryMongoMutationWriter(context,NoFault.Instance); var revision=Guid.NewGuid();
            await writer.WritePublicationAsync(new DecisionRegistryMongoTransactionSession(session),Revision(tenant,decision,revision,1),tenant,decision,1,new BsonDocument{{"LifecycleState","Published"},{"CurrentRevisionId",BsonValue.Create(revision)},{"CurrentRevisionNumber",1}},History(tenant,decision,"Published"),null,Receipt(tenant,"publish"),Audit(tenant),Outbox(tenant),default);
        });
        var results=await Task.WhenAll(Task.Run(Publish),Task.Run(Publish)); AssertOneWinner(results);
        Assert.Equal(1,await Count(context,2,tenant,"DecisionId",decision)); Assert.Equal(1,await Count(context,3,tenant,"DecisionId",decision)); await AssertEnvelope(context,tenant,1);
        var record=await One(context,0,tenant,decision); Assert.Equal(2,record["Version"].ToInt32()); Assert.Equal(1,record["CurrentRevisionNumber"].ToInt32());
        output.WriteLine($"publication statuses={Statuses(results)} revisions=1 history=1 receipts=1 audits=1 outbox=1 currentRevisionNumber=1");
    }

    private async Task DuplicateAndInverseRace(MongoClient client, DecisionRegistryMongoContext context)
    {
        var tenant=Guid.NewGuid(); var source=Guid.NewGuid(); var target=Guid.NewGuid(); await SeedPublished(context,tenant,source,target); var barrier=new Barrier(2);
        async Task<RaceResult> Edge(Guid from,Guid to)=>await Run(client,barrier,session=>Supersede(context,session,tenant,from,to,1));
        var results=await Task.WhenAll(Task.Run(()=>Edge(source,target)),Task.Run(()=>Edge(source,target))); AssertOneWinner(results);
        Assert.Equal(1,await Count(context,5,tenant,"SourceDecisionId",source)); await AssertEnvelope(context,tenant,1);
        output.WriteLine($"duplicate-edge statuses={Statuses(results)} edges=1 history=1 receipts=1 audits=1 outbox=1");

        var tenant2=Guid.NewGuid(); var a=Guid.NewGuid(); var b=Guid.NewGuid(); await SeedPublished(context,tenant2,a,b); barrier=new Barrier(2);
        results=await Task.WhenAll(Task.Run(()=>EdgeFor(context,client,barrier,tenant2,a,b)),Task.Run(()=>EdgeFor(context,client,barrier,tenant2,b,a))); AssertOneWinner(results);
        Assert.Equal(1,await Count(context,5,tenant2)); await AssertEnvelope(context,tenant2,1);
        output.WriteLine($"inverse-edge statuses={Statuses(results)} edges=1 history=1 receipts=1 audits=1 outbox=1");
    }

    private static Task<RaceResult> EdgeFor(DecisionRegistryMongoContext context,MongoClient client,Barrier barrier,Guid tenant,Guid source,Guid target)=>Run(client,barrier,session=>Supersede(context,session,tenant,source,target,1));

    private async Task SupersessionWithdrawalRace(MongoClient client,DecisionRegistryMongoContext context)
    {
        var tenant=Guid.NewGuid(); var source=Guid.NewGuid(); var target=Guid.NewGuid(); await SeedPublished(context,tenant,source,target); var barrier=new Barrier(2);
        var results=await Task.WhenAll(
            Task.Run(()=>Run(client,barrier,s=>Supersede(context,s,tenant,source,target,1))),
            Task.Run(()=>Run(client,barrier,s=>Withdraw(context,s,tenant,source,1))));
        AssertOneWinner(results); Assert.Equal(1,await Count(context,3,tenant,"DecisionId",source)); var edges=await Count(context,5,tenant,"SourceDecisionId",source); Assert.InRange(edges,0,1); await AssertEnvelope(context,tenant,1);
        var state=(await One(context,0,tenant,source))["LifecycleState"].AsString; Assert.Contains(state,new[]{"Superseded","Withdrawn"});
        output.WriteLine($"supersede-vs-withdraw statuses={Statuses(results)} state={state} edges={edges} history=1 receipts=1 audits=1 outbox=1");
    }

    private async Task PublicationDispositionRace(MongoClient client,DecisionRegistryMongoContext context,string disposition)
    {
        var tenant=Guid.NewGuid(); var decision=Guid.NewGuid(); var target=Guid.NewGuid(); await context.Collection(0).InsertOneAsync(Record(tenant,decision,"Published",1,1)); await context.Collection(0).InsertOneAsync(Record(tenant,target,"Published",1,1)); var barrier=new Barrier(2);
        async Task Publish(IClientSessionHandle session)
        { var revision=Guid.NewGuid(); await new DecisionRegistryMongoMutationWriter(context,NoFault.Instance).WritePublicationAsync(new DecisionRegistryMongoTransactionSession(session),Revision(tenant,decision,revision,2),tenant,decision,1,new BsonDocument{{"LifecycleState","Published"},{"CurrentRevisionId",BsonValue.Create(revision)},{"CurrentRevisionNumber",2}},History(tenant,decision,"Published"),null,Receipt(tenant,"publish"),Audit(tenant),Outbox(tenant),default); }
        var results=await Task.WhenAll(Task.Run(()=>Run(client,barrier,Publish)),Task.Run(()=>Run(client,barrier,s=>disposition=="Withdrawn"?Withdraw(context,s,tenant,decision,1):Supersede(context,s,tenant,decision,target,1))));
        AssertOneWinner(results); Assert.Equal(1,await Count(context,3,tenant,"DecisionId",decision)); await AssertEnvelope(context,tenant,1);
        var record=await One(context,0,tenant,decision); Assert.Equal(2,record["Version"].ToInt32());
        output.WriteLine($"publish-vs-{disposition.ToLowerInvariant()} statuses={Statuses(results)} state={record["LifecycleState"].AsString} revisions={await Count(context,2,tenant,"DecisionId",decision)} edges={await Count(context,5,tenant,"SourceDecisionId",decision)} history=1 receipts=1 audits=1 outbox=1");
    }

    private static async Task SelfCycleAndTerminalFence(MongoClient client,DecisionRegistryMongoContext context)
    {
        var tenant=Guid.NewGuid(); var a=Guid.NewGuid(); var b=Guid.NewGuid(); var c=Guid.NewGuid(); await SeedPublished(context,tenant,a,b,c);
        Assert.Equal(409,(await Run(client,new Barrier(1),s=>Supersede(context,s,tenant,a,a,1))).Status);
        Assert.Equal(200,(await Run(client,new Barrier(1),s=>Supersede(context,s,tenant,a,b,1))).Status);
        Assert.Equal(200,(await Run(client,new Barrier(1),s=>Supersede(context,s,tenant,b,c,2))).Status);
        Assert.Equal(409,(await Run(client,new Barrier(1),s=>Supersede(context,s,tenant,c,a,2))).Status);
        Assert.Equal(409,(await Run(client,new Barrier(1),s=>Withdraw(context,s,tenant,a,1))).Status);
        Assert.Equal(2,await Count(context,5,tenant)); Assert.Equal(2,await Count(context,3,tenant)); await AssertEnvelope(context,tenant,2);
        var tenant2=Guid.NewGuid(); var x=Guid.NewGuid(); var y=Guid.NewGuid(); var z=Guid.NewGuid(); await SeedPublished(context,tenant2,x,y,z);
        Assert.Equal(200,(await Run(client,new Barrier(1),s=>Supersede(context,s,tenant2,x,y,1))).Status);
        Assert.Equal(409,(await Run(client,new Barrier(1),s=>Supersede(context,s,tenant2,z,x,1))).Status);
        Assert.Equal(1,await Count(context,5,tenant2)); Assert.Equal(1,await Count(context,3,tenant2)); await AssertEnvelope(context,tenant2,1);
    }

    private static async Task Supersede(DecisionRegistryMongoContext context,IClientSessionHandle session,Guid tenant,Guid source,Guid target,int version)
    {
        if(source==target) throw new DecisionRegistryConflictException("Self supersession.");
        var existing=await context.Collection(5).Find(session,Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",tenant),Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("SourceDecisionId",source),Builders<BsonDocument>.Filter.Eq("TargetDecisionId",target)),Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("SourceDecisionId",target),Builders<BsonDocument>.Filter.Eq("TargetDecisionId",source))))).AnyAsync();
        if(existing || await Reaches(context,session,tenant,target,source)) throw new DecisionRegistryConflictException("Duplicate, inverse or cyclic supersession.");
        var endpointVersions=new Dictionary<Guid,int>();
        foreach(var endpoint in new[]{source,target})
        {
            var endpointRecord=await context.Collection(0).Find(session,Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",tenant),Builders<BsonDocument>.Filter.Eq("DecisionId",endpoint),Builders<BsonDocument>.Filter.Eq("LifecycleState","Published"),Builders<BsonDocument>.Filter.Eq("IsDeleted",false))).FirstOrDefaultAsync();
            if(endpointRecord is null) throw new DecisionRegistryConflictException("Published endpoint required.");
            endpointVersions[endpoint]=endpointRecord["Version"].ToInt32();
        }
        if(endpointVersions[source]!=version) throw new DecisionRegistryConflictException("Source version conflict.");
        foreach(var endpoint in new[]{source,target}.OrderBy(x=>x))
        {
            var fenced=await context.Collection(0).UpdateOneAsync(session,Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",tenant),Builders<BsonDocument>.Filter.Eq("DecisionId",endpoint),Builders<BsonDocument>.Filter.Eq("Version",endpointVersions[endpoint]),Builders<BsonDocument>.Filter.Eq("LifecycleState","Published"),Builders<BsonDocument>.Filter.Eq("IsDeleted",false)),Builders<BsonDocument>.Update.Inc("Version",1),cancellationToken:default);
            if(fenced.MatchedCount!=1) throw new DecisionRegistryConflictException("Endpoint lifecycle fence conflict.");
        }
        await new DecisionRegistryMongoMutationWriter(context,NoFault.Instance).WriteDispositionAsync(new DecisionRegistryMongoTransactionSession(session),tenant,source,version+1,new BsonDocument("LifecycleState","Superseded"),History(tenant,source,"Superseded"),Link(tenant,source,target),Receipt(tenant,"supersede"),Audit(tenant),Outbox(tenant),default);
    }

    private static Task Withdraw(DecisionRegistryMongoContext context,IClientSessionHandle session,Guid tenant,Guid source,int version)=>new DecisionRegistryMongoMutationWriter(context,NoFault.Instance).WriteDispositionAsync(new DecisionRegistryMongoTransactionSession(session),tenant,source,version,new BsonDocument("LifecycleState","Withdrawn"),History(tenant,source,"Withdrawn"),null,Receipt(tenant,"withdraw"),Audit(tenant),Outbox(tenant),default);

    private static async Task<bool> Reaches(DecisionRegistryMongoContext context,IClientSessionHandle session,Guid tenant,Guid start,Guid sought)
    { var seen=new HashSet<Guid>(); var current=start; while(seen.Add(current)){if(current==sought)return true;var edge=await context.Collection(5).Find(session,Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",tenant),Builders<BsonDocument>.Filter.Eq("SourceDecisionId",current))).FirstOrDefaultAsync();if(edge is null)return false;current=edge["TargetDecisionId"].AsGuid;} return true; }

    private static async Task<RaceResult> Run(MongoClient client,Barrier barrier,Func<IClientSessionHandle,Task> body)
    { using var session=await client.StartSessionAsync();session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));barrier.SignalAndWait(TimeSpan.FromSeconds(10));try{await body(session);await session.CommitTransactionAsync();return new(200,"OK");}catch(Exception ex){if(session.IsInTransaction)await session.AbortTransactionAsync();var conflict=DecisionRegistryMongoConflictMapper.Map(ex);return new(409,conflict.Message);} }

    private static void AssertOneWinner(RaceResult[] results){Assert.Equal(1,results.Count(x=>x.Status==200));Assert.Equal(1,results.Count(x=>x.Status==409));}
    private static string Statuses(IEnumerable<RaceResult> results)=>string.Join(',',results.Select(x=>x.Status).OrderBy(x=>x));
    private static async Task AssertEnvelope(DecisionRegistryMongoContext c,Guid t,long count){Assert.Equal(count,await Count(c,6,t));Assert.Equal(count,await Count(c,7,t));Assert.Equal(count,await Count(c,8,t));}
    private static Task<long> Count(DecisionRegistryMongoContext c,int i,Guid t)=>c.Collection(i).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("TenantId",t));
    private static Task<long> Count(DecisionRegistryMongoContext c,int i,Guid t,string f,Guid v)=>c.Collection(i).CountDocumentsAsync(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",t),Builders<BsonDocument>.Filter.Eq(f,v)));
    private static Task<BsonDocument> One(DecisionRegistryMongoContext c,int i,Guid t,Guid d)=>c.Collection(i).Find(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",t),Builders<BsonDocument>.Filter.Eq("DecisionId",d))).SingleAsync();
    private static async Task SeedPublished(DecisionRegistryMongoContext c,Guid t,params Guid[] ids){foreach(var id in ids)await c.Collection(0).InsertOneAsync(Record(t,id,"Published",1,1));}
    private static BsonDocument Record(Guid t,Guid d,string state,int version,int current=0)=>Doc(("TenantId",t),("DecisionId",d),("SubjectKind","InvestmentCase"),("SubjectId",Guid.NewGuid()),("IsDeleted",false),("LifecycleState",state),("Version",version),("CurrentRevisionNumber",current));
    private static BsonDocument Revision(Guid t,Guid d,Guid r,int n)=>Doc(("TenantId",t),("DecisionRevisionId",r),("DecisionId",d),("DecisionRevisionNumber",n));
    private static BsonDocument History(Guid t,Guid d,string disposition)=>Doc(("TenantId",t),("PublicationHistoryId",Guid.NewGuid()),("DecisionId",d),("Disposition",disposition),("OccurredAtUtc",DateTime.UtcNow));
    private static BsonDocument Link(Guid t,Guid s,Guid target)=>Doc(("TenantId",t),("SourceDecisionId",s),("TargetDecisionId",target),("EffectiveAtUtc",DateTime.UtcNow));
    private static BsonDocument Receipt(Guid t,string op)=>Doc(("TenantId",t),("ReceiptId",Guid.NewGuid()),("Operation",op),("EffectiveActorId",Guid.NewGuid()),("S2SClientId",BsonNull.Value),("IdempotencyKeyHash",Guid.NewGuid().ToString("N")),("PayloadHash","hash"));
    private static BsonDocument Audit(Guid t)=>Doc(("TenantId",t),("AuditIntentId",Guid.NewGuid()),("DispatchState","Pending"),("NextAttemptAtUtc",DateTime.UtcNow),("OccurredAtUtc",DateTime.UtcNow));
    private static BsonDocument Outbox(Guid t)=>Doc(("TenantId",t),("EventId",Guid.NewGuid()),("Status","Pending"),("NextAttemptAtUtc",DateTime.UtcNow),("CreatedAtUtc",DateTime.UtcNow));
    private static BsonDocument Doc(params(string,object)[] values)=>new(values.Select(x=>new BsonElement(x.Item1,BsonValue.Create(x.Item2))));
    private sealed record RaceResult(int Status,string DomainResult);
    private sealed class NoFault:ITransactionFaultInjector{public static NoFault Instance{get;}=new();public void After(DecisionRegistryFaultPoint point){} }
    private static async Task EnsureReplicaSetAsync(){var direct=new MongoClient("mongodb://127.0.0.1:27019/?directConnection=true&serverSelectionTimeoutMS=2000");try{await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate",new BsonDocument{{"_id","mod0007rs"},{"members",new BsonArray{new BsonDocument{{"_id",0},{"host","127.0.0.1:27019"}}}}}));}catch(MongoCommandException ex)when(ex.CodeName is "AlreadyInitialized" or "AlreadyInitiated"){}for(var i=0;i<40;i++){try{var h=await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello",1));if(h.GetValue("isWritablePrimary",false).ToBoolean())return;}catch(MongoException){}await Task.Delay(200);}throw new TimeoutException();}
}
