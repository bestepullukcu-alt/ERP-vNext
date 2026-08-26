using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling;

public sealed class MongoPersistenceTests
{
    [Fact] public async Task Tenant_predicate_and_expected_version_CAS_guardians_are_executable()
    {
        await using var mongo=await Replica.StartAsync();var client=new MongoClient(mongo.Uri);var db="guardians_"+Guid.NewGuid().ToString("N");var context=new ProcessModelingMongoContext(client,db);var tenant=Guid.NewGuid();var foreign=Guid.NewGuid();var id=Guid.NewGuid();var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);
        try
        {
            await context.Collection("mg_process_models").InsertOneAsync(new BsonDocument{{"_id",G(id)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"Name","Original"}});
            var repository=new ProcessModelingMongoRepository(context);Assert.Null(await repository.FindTenantAsync("mg_process_models",foreign,id));
            using(var session=await client.StartSessionAsync()){session.StartTransaction();await repository.CasAsync(session,"mg_process_models",tenant,id,0,new BsonDocument("Name","Winner"),default);await session.CommitTransactionAsync();}
            using(var stale=await client.StartSessionAsync()){stale.StartTransaction();await Assert.ThrowsAsync<ProcessModelingConflictException>(()=>repository.CasAsync(stale,"mg_process_models",tenant,id,0,new BsonDocument("Name","Stale"),default));await stale.AbortTransactionAsync();}
            var persisted=await repository.FindTenantAsync("mg_process_models",tenant,id);Assert.NotNull(persisted);Assert.Equal("Winner",persisted["Name"].AsString);Assert.Equal(1,persisted["Version"].AsInt32);
        }
        finally{await client.DropDatabaseAsync(db);}
    }

    [Fact] public async Task Dynamic_standalone_is_typed_unavailable_with_zero_residue()
    {
        await using var mongo=await Replica.StartStandaloneAsync();var client=new MongoClient(mongo.Uri);var context=new ProcessModelingMongoContext(client,"standalone_"+Guid.NewGuid().ToString("N"));var tenant=Guid.NewGuid();var id=Guid.NewGuid();var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);var request=new CoreMutationRequest(tenant,Guid.NewGuid(),"CreateProcessModelCommand","standalone","sha256:"+new string('a',64),0,id);var envelope=new MongoMutationEnvelope(request,new BsonDocument{{"_id",G(id)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0}},"created",Guid.NewGuid(),Guid.NewGuid(),DateTime.UtcNow,"subject:test");
        await Assert.ThrowsAsync<ProcessModelingUnavailableException>(()=>new ProcessModelingMongoTransactionRunner(context).ExecuteAsync<string>(async(s,ct)=>{await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,envelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));Assert.Equal(0,await context.Collection("mg_process_models").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(0,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(0,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(0,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
    [Fact]
    public async Task Dynamic_replica_set_proves_indexes_tenant_cas_and_atomic_participants()
    {
        await using var mongo=await Replica.StartAsync(); var client=new MongoClient(mongo.Uri); var db="mod0355_"+Guid.NewGuid().ToString("N"); var context=new ProcessModelingMongoContext(client,db);
        try
        {
            await new ProcessModelingMongoIndexInitializer(context).InitializeAsync();
            var snapshots=new List<BsonDocument>();
            foreach(var collection in ProcessModelingPersistenceManifest.Collections){using var cursor=await context.Collection(collection).Indexes.ListAsync(); snapshots.AddRange((await cursor.ToListAsync()).Where(x=>x["name"].AsString!="_id_"));}
            Assert.Equal(16,snapshots.Count); Assert.All(snapshots,x=>{Assert.Equal("TenantId",x["key"].AsBsonDocument.GetElement(0).Name);Assert.False(x.Contains("expireAfterSeconds"));});
            var open=Assert.Single(snapshots,x=>x["name"]=="ux_pm_open_version"); Assert.Equal(2,open["key"].AsBsonDocument.ElementCount); Assert.True(open.Contains("partialFilterExpression"));

            var tenant=Guid.NewGuid(); var foreign=Guid.NewGuid(); var aggregate=Guid.NewGuid(); var now=new DateTime(2026,8,26,0,0,0,DateTimeKind.Utc); var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);
            var request=new CoreMutationRequest(tenant,Guid.NewGuid(),"CreateProcessModelCommand","key","sha256:"+new string('a',64),0,aggregate);
            var business=new BsonDocument{{"_id",G(aggregate)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","MODEL"}};
            var envelope=new MongoMutationEnvelope(request,business,"created",Guid.NewGuid(),Guid.NewGuid(),now,"subject:test");
            var runner=new ProcessModelingMongoTransactionRunner(context); var body=0;
            await runner.ExecuteAsync(async(session,ct)=>{body++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(session,envelope,ct);return "ok";},_=>Task.FromResult<string?>(null));
            Assert.Equal(1,body); Assert.NotNull(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,aggregate)); Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",foreign,aggregate));
            Assert.Equal(1,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var rawReceipt=await context.Collection("mg_process_modeling_idempotency_receipts").Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();Assert.Equal(request.SubjectId,rawReceipt["SubjectId"].AsBsonBinaryData.ToGuid(GuidRepresentation.CSharpLegacy));Assert.Equal(request.CanonicalPayloadHash,rawReceipt["PayloadHash"].AsString);
            var receiptReader=new ProcessModelingMongoReceiptReader(context);Assert.Equal("created",(await receiptReader.ReconcileAsync(request,true))!.StableCode);Assert.Equal(403,(await receiptReader.ReconcileAsync(request,false))!.HttpStatus);Assert.Equal(409,(await receiptReader.ReconcileAsync(request with{SubjectId=Guid.NewGuid()},true))!.HttpStatus);Assert.Equal(409,(await receiptReader.ReconcileAsync(request with{CanonicalPayloadHash="sha256:"+new string('b',64)},true))!.HttpStatus);
            Assert.Equal(BsonType.DateTime,rawReceipt["CreatedAtUtc"].BsonType);Assert.Equal(now,rawReceipt["CreatedAtUtc"].ToUniversalTime());

            var modelForOpen=Guid.NewGuid();BsonDocument Open(string state)=>new(){{"_id",G(Guid.NewGuid())},{"TenantId",G(tenant)},{"ProcessModelId",G(modelForOpen)},{"RevisionNumber",state=="Draft"?1:2},{"LifecycleState",state},{"IsDeleted",false}};await context.Collection("mg_process_model_versions").InsertOneAsync(Open("Draft"));await Assert.ThrowsAsync<MongoWriteException>(()=>context.Collection("mg_process_model_versions").InsertOneAsync(Open("Review")));
            foreach(var fault in Enum.GetValues<ProcessModelingMutationParticipant>())
            {
                var failedId=Guid.NewGuid();var failedRequest=request with{AggregateId=failedId,IdempotencyKey="fault-"+fault};var failed=envelope with{Request=failedRequest,Business=new BsonDocument{{"_id",G(failedId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","FAULT-"+fault}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};
                await Assert.ThrowsAsync<InjectedFault>(()=>runner.ExecuteAsync<string>(async(s,ct)=>{await new ProcessModelingMongoMutationWriter(context,p=>{if(p==fault)throw new InjectedFault();}).WriteAsync(s,failed,ct);return "wrong";},_=>Task.FromResult<string?>(null)));
                Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,failedId));
                Assert.Equal(1,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(1,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(1,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            }
            using var session=await client.StartSessionAsync(); session.StartTransaction(); await new ProcessModelingMongoRepository(context).CasAsync(session,"mg_process_models",tenant,aggregate,0,new BsonDocument("Name","Changed"),default); await session.CommitTransactionAsync();
            using var stale=await client.StartSessionAsync(); stale.StartTransaction(); await Assert.ThrowsAsync<ProcessModelingConflictException>(()=>new ProcessModelingMongoRepository(context).CasAsync(stale,"mg_process_models",tenant,aggregate,0,new BsonDocument("Name","Wrong"),default)); await stale.AbortTransactionAsync();

            var retryId=Guid.NewGuid();var retryRequest=request with{AggregateId=retryId,IdempotencyKey="commit-retry"};var retryEnvelope=envelope with{Request=retryRequest,Business=new BsonDocument{{"_id",G(retryId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","RETRY"}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};int attempts=0,retryBody=0;
            var retryRunner=new ProcessModelingMongoTransactionRunner(context,3,async(s,a,ct)=>{attempts++;if(a<3)throw new ProcessModelingUnknownCommitException();await s.CommitTransactionAsync(ct);});
            Assert.Equal("ok",await retryRunner.ExecuteAsync(async(s,ct)=>{retryBody++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,retryEnvelope,ct);return "ok";},_=>Task.FromResult<string?>(null)));Assert.Equal(1,retryBody);Assert.Equal(3,attempts);

            var uncertainId=Guid.NewGuid();var uncertainRequest=request with{AggregateId=uncertainId,IdempotencyKey="uncertain"};var uncertainEnvelope=envelope with{Request=uncertainRequest,Business=new BsonDocument{{"_id",G(uncertainId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","UNCERTAIN"}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};var uncertainBody=0;
            var uncertainRunner=new ProcessModelingMongoTransactionRunner(context,3,(_,_,_)=>Task.FromException(new ProcessModelingUnknownCommitException()));
            await Assert.ThrowsAsync<ProcessModelingUnavailableException>(()=>uncertainRunner.ExecuteAsync<string>(async(s,ct)=>{uncertainBody++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,uncertainEnvelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));Assert.Equal(1,uncertainBody);Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,uncertainId));

            var durableId=Guid.NewGuid();var durableRequest=request with{AggregateId=durableId,IdempotencyKey="durable"};var durableEnvelope=envelope with{Request=durableRequest,Business=new BsonDocument{{"_id",G(durableId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","DURABLE"}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};var durableBody=0;
            var durableRunner=new ProcessModelingMongoTransactionRunner(context,3,async(s,a,ct)=>{if(a==1)await s.CommitTransactionAsync(ct);throw new ProcessModelingUnknownCommitException();});
            Assert.Equal("created",await durableRunner.ExecuteAsync(async(s,ct)=>{durableBody++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,durableEnvelope,ct);return "created";},async ct=>(await receiptReader.ReconcileAsync(durableRequest,true,ct))?.StableCode));Assert.Equal(1,durableBody);

            var publishId=Guid.NewGuid();var publishRequest=request with{AggregateId=publishId,IdempotencyKey="publish",CommandFamily=PublishProcessModelVersionContract.CommandName};var publishEnvelope=envelope with{Request=publishRequest,Business=new BsonDocument{{"_id",G(publishId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};
            await Assert.ThrowsAsync<ProcessModelingUnavailableException>(()=>runner.ExecuteAsync<string>(async(s,ct)=>{await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,publishEnvelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,publishId));

            var raceModelId=Guid.NewGuid();var raceTag="revision-race-"+Guid.NewGuid().ToString("N");
            await context.Collection("mg_process_models").InsertOneAsync(new BsonDocument{{"_id",G(raceModelId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"LatestRevisionNumber",1},{"OpenVersionId",BsonNull.Value}});
            using var barrier=new Barrier(2);
            async Task<bool> AllocateAsync(Guid revisionId)
            {
                using var s=await client.StartSessionAsync();s.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
                try
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                    await new ProcessModelingMongoRepository(context).CasAsync(s,"mg_process_models",tenant,raceModelId,0,new BsonDocument{{"LatestRevisionNumber",2},{"OpenVersionId",G(revisionId)}},default);
                    await context.Collection("mg_process_modeling_idempotency_receipts").InsertOneAsync(s,new BsonDocument{{"TenantId",G(tenant)},{"SubjectId",G(raceModelId)},{"CommandFamily","AllocateProcessModelRevisionCommand"},{"IdempotencyKey",raceTag+revisionId},{"PayloadHash","sha256:"+new string('c',64)},{"Outcome","allocated"},{"AggregateId",G(raceModelId)},{"CreatedAtUtc",now},{"CompletedAtUtc",now}},cancellationToken:default);
                    await context.Collection("mg_process_modeling_audit_intents").InsertOneAsync(s,new BsonDocument{{"TenantId",G(tenant)},{"AuditIntentId",G(Guid.NewGuid())},{"AggregateId",G(raceModelId)},{"CommandFamily","AllocateProcessModelRevisionCommand"},{"ActorProvenance",raceTag},{"CreatedAtUtc",now}},cancellationToken:default);
                    await context.Collection("mg_process_modeling_outbox_messages").InsertOneAsync(s,new BsonDocument{{"TenantId",G(tenant)},{"EventId",G(Guid.NewGuid())},{"AggregateId",G(raceModelId)},{"EventType",raceTag},{"CreatedAtUtc",now}},cancellationToken:default);
                    await s.CommitTransactionAsync();return true;
                }
                catch(Exception ex) when(ex is ProcessModelingConflictException or MongoException)
                { if(s.IsInTransaction)try{await s.AbortTransactionAsync();}catch(MongoException){}return false; }
            }
            var raceResults=await Task.WhenAll(Task.Run(()=>AllocateAsync(Guid.NewGuid())),Task.Run(()=>AllocateAsync(Guid.NewGuid())));
            Assert.Equal(1,raceResults.Count(x=>x));Assert.Equal(1,raceResults.Count(x=>!x));
            var raced=await context.Collection("mg_process_models").Find(Builders<BsonDocument>.Filter.Eq("_id",G(raceModelId))).SingleAsync();Assert.Equal(1,raced["Version"].AsInt32);Assert.Equal(2,raced["LatestRevisionNumber"].AsInt32);Assert.NotEqual(BsonNull.Value,raced["OpenVersionId"]);
            Assert.Equal(1,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("CommandFamily","AllocateProcessModelRevisionCommand")));
            Assert.Equal(1,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ActorProvenance",raceTag)));
            Assert.Equal(1,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("EventType",raceTag)));
        }
        finally { await client.DropDatabaseAsync(db); }
    }
    private sealed class InjectedFault:Exception;

    private sealed class Replica(Process process,string directory,int port,bool replicaSet):IAsyncDisposable
    {
        public string Uri=>replicaSet?$"mongodb://127.0.0.1:{port}/?replicaSet=mod0355rs&serverSelectionTimeoutMS=5000":$"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=5000";
        public static Task<Replica> StartStandaloneAsync()=>StartAsync(false);
        public static async Task<Replica> StartAsync()
            =>await StartAsync(true);
        private static async Task<Replica> StartAsync(bool replicaSet)
        {
            var binary="/opt/homebrew/bin/mongod"; if(!File.Exists(binary)) throw new InvalidOperationException("mongod_required");
            int port; using(var listener=new TcpListener(IPAddress.Loopback,0)){listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;} if(port<27022) return await StartAsync();
            var dir=Path.Combine(Path.GetTempPath(),"mod0355-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var info=new ProcessStartInfo(binary,$"--dbpath {dir} --port {port} --bind_ip 127.0.0.1 {(replicaSet?"--replSet mod0355rs":string.Empty)} --quiet") { RedirectStandardError=true,RedirectStandardOutput=true,UseShellExecute=false };
            var process=Process.Start(info)??throw new InvalidOperationException("mongod_start_failed"); process.BeginOutputReadLine(); process.BeginErrorReadLine(); var direct=new MongoClient($"mongodb://127.0.0.1:{port}/?directConnection=true&serverSelectionTimeoutMS=1000");
            for(var i=0;i<50;i++){try{await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping",1));break;}catch(Exception ex) when(ex is MongoException or TimeoutException){await Task.Delay(100);}}
            if(!replicaSet)return new(process,dir,port,false);
            await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate",new BsonDocument{{"_id","mod0355rs"},{"members",new BsonArray{new BsonDocument{{"_id",0},{"host",$"127.0.0.1:{port}"}}}}}));
            for(var i=0;i<80;i++){try{var h=await direct.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello",1));if(h.GetValue("isWritablePrimary",false).ToBoolean())return new(process,dir,port,true);}catch(Exception ex) when(ex is MongoException or TimeoutException){}await Task.Delay(100);} process.Kill(true);process.WaitForExit(5000);Directory.Delete(dir,true);throw new TimeoutException("replica_primary_timeout");
        }
        public ValueTask DisposeAsync(){if(!process.HasExited){process.Kill(true);process.WaitForExit(5000);}process.Dispose();if(Directory.Exists(directory))Directory.Delete(directory,true);return ValueTask.CompletedTask;}
    }
}
