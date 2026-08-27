using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Publish;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling;

public sealed class MongoPersistenceTests
{
    [Fact] public async Task Tenant_predicate_and_expected_version_CAS_guardians_are_executable()
    {
        await using var mongo=await Replica.StartAsync();var client=C(mongo.Uri);var db="guardians_"+Guid.NewGuid().ToString("N");var context=new ProcessModelingMongoContext(client,db);var tenant=Guid.NewGuid();var foreign=Guid.NewGuid();var id=Guid.NewGuid();var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);
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
        await using var mongo=await Replica.StartStandaloneAsync();var client=C(mongo.Uri);var context=new ProcessModelingMongoContext(client,"standalone_"+Guid.NewGuid().ToString("N"));var tenant=Guid.NewGuid();var id=Guid.NewGuid();var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);var request=new CoreMutationRequest(tenant,Guid.NewGuid(),"CreateProcessArchitectureCommand","standalone","sha256:"+new string('a',64),0,id);var envelope=new MongoMutationEnvelope(request,new BsonDocument{{"_id",G(id)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0}},"created",Guid.NewGuid(),Guid.NewGuid(),DateTime.UtcNow,"subject:test");
        await Assert.ThrowsAsync<ProcessModelingUnavailableException>(()=>new ProcessModelingMongoTransactionRunner(context).ExecuteAsync<string>(async(s,ct)=>{await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,envelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));Assert.Equal(0,await context.Collection("mg_process_models").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(0,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(0,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(0,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task Durable_receipt_reconciliation_preserves_original_result_and_never_replays_transaction_body()
    {
        await using var mongo=await Replica.StartAsync();
        var commands=new ConcurrentQueue<BsonDocument>();
        var settings=ProcessModelingMongoGuidPolicy.ClientSettings(mongo.Uri);
        var existingConfigurator=settings.ClusterConfigurator;
        settings.ClusterConfigurator=builder=>
        {
            existingConfigurator?.Invoke(builder);
            builder.Subscribe<CommandStartedEvent>(started=>
            {
                if(string.Equals(started.CommandName,"find",StringComparison.Ordinal))commands.Enqueue(started.Command.DeepClone().AsBsonDocument);
            });
        };
        var client=new MongoClient(settings);var db="receipt_"+Guid.NewGuid().ToString("N");var context=new ProcessModelingMongoContext(client,db);
        try
        {
            await new ProcessModelingMongoIndexInitializer(context).InitializeAsync();
            var tenant=Guid.NewGuid();var subject=Guid.NewGuid();var aggregate=Guid.NewGuid();var outcomeEntityId=Guid.NewGuid();var definitionId=Guid.NewGuid();var G=(Guid value)=>new BsonBinaryData(value,GuidRepresentation.Standard);
            await context.Collection("mg_process_definitions").InsertOneAsync(new BsonDocument{{"_id",G(definitionId)},{"TenantId",G(tenant)},{"LifecycleState","Active"},{"IsDeleted",false},{"Version",0}});
            var request=new CoreMutationRequest(tenant,subject,"CreateProcessModelCommand","durable-key","sha256:"+new string('a',64),0,aggregate);
            MongoMutationEnvelope Envelope(CoreMutationRequest x,int status=201,Guid? outcome=null)=>new(x,new BsonDocument{{"_id",G(x.AggregateId)},{"TenantId",G(x.TenantId)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(definitionId)},{"ModelCode","MODEL-"+x.AggregateId.ToString("N")}},"created",Guid.NewGuid(),Guid.NewGuid(),DateTime.UtcNow,"subject:test",status,outcome);
            var writer=new ProcessModelingMongoMutationWriter(context);var reader=new ProcessModelingMongoReceiptReader(context);var runner=new ProcessModelingMongoTransactionRunner(context);

            var first=await runner.ExecuteAsync(async(session,ct)=>
            {
                await writer.WriteAsync(session,Envelope(request,201,outcomeEntityId),ct);
                return new CoreMutationResult(true,201,"created",outcomeEntityId);
            },ct=>reader.ReconcileAsync(request,true,ct));
            Assert.True(first.Accepted);Assert.Equal(201,first.HttpStatus);Assert.Equal(outcomeEntityId,first.AggregateId);

            var retryRequest=request with{AggregateId=Guid.NewGuid()};var duplicateBody=0;
            var duplicate=await runner.ExecuteAsync(async(session,ct)=>
            {
                duplicateBody++;
                await writer.WriteAsync(session,Envelope(retryRequest,202,Guid.NewGuid()),ct);
                return new CoreMutationResult(true,202,"wrong",retryRequest.AggregateId);
            },ct=>reader.ReconcileAsync(retryRequest,true,ct));
            Assert.Equal(1,duplicateBody);Assert.True(duplicate.Accepted);Assert.Equal(201,duplicate.HttpStatus);Assert.Equal("created",duplicate.StableCode);Assert.Equal(outcomeEntityId,duplicate.AggregateId);
            Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,retryRequest.AggregateId));

            var changedSubject=request with{SubjectId=Guid.NewGuid(),AggregateId=Guid.NewGuid()};
            var subjectConflict=await runner.ExecuteAsync(async(session,ct)=>
            {
                await writer.WriteAsync(session,Envelope(changedSubject),ct);
                return new CoreMutationResult(true,201,"wrong",changedSubject.AggregateId);
            },ct=>reader.ReconcileAsync(changedSubject,true,ct));
            Assert.False(subjectConflict.Accepted);Assert.Equal(409,subjectConflict.HttpStatus);Assert.Equal(ProcessModelingErrors.IdempotencyConflict,subjectConflict.StableCode);
            Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,changedSubject.AggregateId));

            var changedPayload=request with{CanonicalPayloadHash="sha256:"+new string('b',64),AggregateId=Guid.NewGuid()};
            var payloadConflict=await runner.ExecuteAsync(async(session,ct)=>
            {
                await writer.WriteAsync(session,Envelope(changedPayload),ct);
                return new CoreMutationResult(true,201,"wrong",changedPayload.AggregateId);
            },ct=>reader.ReconcileAsync(changedPayload,true,ct));
            Assert.False(payloadConflict.Accepted);Assert.Equal(409,payloadConflict.HttpStatus);Assert.Equal(ProcessModelingErrors.IdempotencyConflict,payloadConflict.StableCode);
            Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,changedPayload.AggregateId));

            var legacyRequest=request with{IdempotencyKey="legacy"};
            await context.Collection("mg_process_modeling_idempotency_receipts").InsertOneAsync(new BsonDocument{{"TenantId",G(tenant)},{"SubjectId",G(subject)},{"CommandFamily",request.CommandFamily},{"IdempotencyKey",legacyRequest.IdempotencyKey},{"PayloadHash",request.CanonicalPayloadHash},{"Outcome","legacy"},{"AggregateId",G(aggregate)},{"CreatedAtUtc",DateTime.UtcNow},{"CompletedAtUtc",DateTime.UtcNow}});
            var legacy=await reader.ReconcileAsync(legacyRequest,true);Assert.NotNull(legacy);Assert.False(legacy.Accepted);Assert.Equal(503,legacy.HttpStatus);Assert.Equal(ProcessModelingMongoReceiptReader.SchemaIncompatible,legacy.StableCode);

            var rawAudit=await context.Collection("mg_process_modeling_audit_intents").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            var rawOutbox=await context.Collection("mg_process_modeling_outbox_messages").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            Assert.Equal(aggregate,ProcessModelingMongoGuidPolicy.Read(rawAudit["Payload"]["entityId"]));
            Assert.Equal(aggregate,ProcessModelingMongoGuidPolicy.Read(rawOutbox["AggregateId"]));
            Assert.Equal(ProcessModelingMongoMutationWriter.AuditEventType,rawOutbox["EventType"].AsString);Assert.Equal(1,rawOutbox["EventVersion"].AsInt32);
            Assert.Equal(1,await context.Collection("mg_process_models").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(2,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Contains(commands,command=>command.TryGetValue("find",out var collection)&&collection=="mg_process_modeling_idempotency_receipts"&&command.TryGetValue("readConcern",out var readConcern)&&readConcern["level"]=="majority");

            var unknownRequest=request with{IdempotencyKey="unknown-commit",AggregateId=Guid.NewGuid()};var unknownEnvelope=Envelope(unknownRequest,204,unknownRequest.AggregateId);var unknownBody=0;var commitAttempts=0;IClientSessionHandle? commitSession=null;
            var unknownRunner=new ProcessModelingMongoTransactionRunner(context,3,async(session,attempt,ct)=>
            {
                commitAttempts++;if(commitSession is null)commitSession=session;else Assert.Same(commitSession,session);
                if(attempt==1)await session.CommitTransactionAsync(ct);
                throw new ProcessModelingUnknownCommitException();
            });
            var reconciled=await unknownRunner.ExecuteAsync(async(session,ct)=>
            {
                unknownBody++;await writer.WriteAsync(session,unknownEnvelope,ct);return new CoreMutationResult(true,204,"updated",unknownRequest.AggregateId);
            },ct=>reader.ReconcileAsync(unknownRequest,true,ct));
            Assert.Equal(1,unknownBody);Assert.Equal(3,commitAttempts);Assert.Equal(204,reconciled.HttpStatus);Assert.Equal(unknownRequest.AggregateId,reconciled.AggregateId);
        }
        finally { await client.DropDatabaseAsync(db); }
    }
    [Fact]
    public async Task Dynamic_replica_set_proves_indexes_tenant_cas_and_atomic_participants()
    {
        await using var mongo=await Replica.StartAsync(); var client=C(mongo.Uri); var db="mod0355_"+Guid.NewGuid().ToString("N"); var context=new ProcessModelingMongoContext(client,db);
        try
        {
            await new ProcessModelingMongoIndexInitializer(context).InitializeAsync();
            var snapshots=new List<BsonDocument>();
            foreach(var collection in ProcessModelingPersistenceManifest.Collections){using var cursor=await context.Collection(collection).Indexes.ListAsync(); snapshots.AddRange((await cursor.ToListAsync()).Where(x=>x["name"].AsString!="_id_"));}
            Assert.Equal(16,snapshots.Count); Assert.All(snapshots,x=>{Assert.Equal("TenantId",x["key"].AsBsonDocument.GetElement(0).Name);Assert.False(x.Contains("expireAfterSeconds"));});
            var open=Assert.Single(snapshots,x=>x["name"]=="ux_pm_open_version"); Assert.Equal(2,open["key"].AsBsonDocument.ElementCount); Assert.True(open.Contains("partialFilterExpression"));

            var tenant=Guid.NewGuid(); var foreign=Guid.NewGuid(); var aggregate=Guid.NewGuid(); var definitionId=Guid.NewGuid(); var now=new DateTime(2026,8,26,0,0,0,DateTimeKind.Utc); var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);
            await context.Collection("mg_process_definitions").InsertOneAsync(new BsonDocument{{"_id",G(definitionId)},{"TenantId",G(tenant)},{"LifecycleState","Active"},{"IsDeleted",false},{"Version",0}});
            var request=new CoreMutationRequest(tenant,Guid.NewGuid(),"CreateProcessModelCommand","key","sha256:"+new string('a',64),0,aggregate);
            var business=new BsonDocument{{"_id",G(aggregate)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(definitionId)},{"ModelCode","MODEL"}};
            var envelope=new MongoMutationEnvelope(request,business,"created",Guid.NewGuid(),Guid.NewGuid(),now,"subject:test");
            var runner=new ProcessModelingMongoTransactionRunner(context); var body=0;
            await runner.ExecuteAsync(async(session,ct)=>{body++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(session,envelope,ct);return "ok";},_=>Task.FromResult<string?>(null));
            Assert.Equal(1,body); Assert.NotNull(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,aggregate)); Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",foreign,aggregate));
            Assert.Equal(1,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var rawReceipt=await context.Collection("mg_process_modeling_idempotency_receipts").Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();Assert.Equal(BsonBinarySubType.UuidStandard,rawReceipt["SubjectId"].AsBsonBinaryData.SubType);Assert.Equal(request.SubjectId,ProcessModelingMongoGuidPolicy.Read(rawReceipt["SubjectId"]));Assert.Equal(request.CanonicalPayloadHash,rawReceipt["PayloadHash"].AsString);
            var receiptReader=new ProcessModelingMongoReceiptReader(context);Assert.Equal("created",(await receiptReader.ReconcileAsync(request,true))!.StableCode);Assert.Equal(403,(await receiptReader.ReconcileAsync(request,false))!.HttpStatus);Assert.Equal(409,(await receiptReader.ReconcileAsync(request with{SubjectId=Guid.NewGuid()},true))!.HttpStatus);Assert.Equal(409,(await receiptReader.ReconcileAsync(request with{CanonicalPayloadHash="sha256:"+new string('b',64)},true))!.HttpStatus);
            Assert.Equal(BsonType.DateTime,rawReceipt["CreatedAtUtc"].BsonType);Assert.Equal(now,rawReceipt["CreatedAtUtc"].ToUniversalTime());

            var modelForOpen=Guid.NewGuid();BsonDocument Open(string state)=>new(){{"_id",G(Guid.NewGuid())},{"TenantId",G(tenant)},{"ProcessModelId",G(modelForOpen)},{"RevisionNumber",state=="Draft"?1:2},{"LifecycleState",state},{"IsDeleted",false}};await context.Collection("mg_process_model_versions").InsertOneAsync(Open("Draft"));await Assert.ThrowsAsync<MongoWriteException>(()=>context.Collection("mg_process_model_versions").InsertOneAsync(Open("Review")));
            foreach(var fault in Enum.GetValues<ProcessModelingMutationParticipant>())
            {
                var failedId=Guid.NewGuid();var failedRequest=request with{AggregateId=failedId,IdempotencyKey="fault-"+fault};var failed=envelope with{Request=failedRequest,Business=new BsonDocument{{"_id",G(failedId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(definitionId)},{"ModelCode","FAULT-"+fault}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};
                await Assert.ThrowsAsync<InjectedFault>(()=>runner.ExecuteAsync<string>(async(s,ct)=>{await new ProcessModelingMongoMutationWriter(context,p=>{if(p==fault)throw new InjectedFault();}).WriteAsync(s,failed,ct);return "wrong";},_=>Task.FromResult<string?>(null)));
                Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,failedId));
                Assert.Equal(1,await context.Collection("mg_process_modeling_idempotency_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(1,await context.Collection("mg_process_modeling_audit_intents").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));Assert.Equal(1,await context.Collection("mg_process_modeling_outbox_messages").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            }
            using var session=await client.StartSessionAsync(); session.StartTransaction(); await new ProcessModelingMongoRepository(context).CasAsync(session,"mg_process_models",tenant,aggregate,0,new BsonDocument("Name","Changed"),default); await session.CommitTransactionAsync();
            using var stale=await client.StartSessionAsync(); stale.StartTransaction(); await Assert.ThrowsAsync<ProcessModelingConflictException>(()=>new ProcessModelingMongoRepository(context).CasAsync(stale,"mg_process_models",tenant,aggregate,0,new BsonDocument("Name","Wrong"),default)); await stale.AbortTransactionAsync();

            var retryId=Guid.NewGuid();var retryRequest=request with{AggregateId=retryId,IdempotencyKey="commit-retry"};var retryEnvelope=envelope with{Request=retryRequest,Business=new BsonDocument{{"_id",G(retryId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(definitionId)},{"ModelCode","RETRY"}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};int attempts=0,retryBody=0;
            var retryRunner=new ProcessModelingMongoTransactionRunner(context,3,async(s,a,ct)=>{attempts++;if(a<3)throw new ProcessModelingUnknownCommitException();await s.CommitTransactionAsync(ct);});
            Assert.Equal("ok",await retryRunner.ExecuteAsync(async(s,ct)=>{retryBody++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,retryEnvelope,ct);return "ok";},_=>Task.FromResult<string?>(null)));Assert.Equal(1,retryBody);Assert.Equal(3,attempts);

            var uncertainId=Guid.NewGuid();var uncertainRequest=request with{AggregateId=uncertainId,IdempotencyKey="uncertain"};var uncertainEnvelope=envelope with{Request=uncertainRequest,Business=new BsonDocument{{"_id",G(uncertainId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(definitionId)},{"ModelCode","UNCERTAIN"}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};var uncertainBody=0;
            var uncertainRunner=new ProcessModelingMongoTransactionRunner(context,3,(_,_,_)=>Task.FromException(new ProcessModelingUnknownCommitException()));
            await Assert.ThrowsAsync<ProcessModelingUnavailableException>(()=>uncertainRunner.ExecuteAsync<string>(async(s,ct)=>{uncertainBody++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,uncertainEnvelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));Assert.Equal(1,uncertainBody);Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,uncertainId));

            var durableId=Guid.NewGuid();var durableRequest=request with{AggregateId=durableId,IdempotencyKey="durable"};var durableEnvelope=envelope with{Request=durableRequest,Business=new BsonDocument{{"_id",G(durableId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0},{"ProcessDefinitionId",G(definitionId)},{"ModelCode","DURABLE"}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};var durableBody=0;
            var durableRunner=new ProcessModelingMongoTransactionRunner(context,3,async(s,a,ct)=>{if(a==1)await s.CommitTransactionAsync(ct);throw new ProcessModelingUnknownCommitException();});
            Assert.Equal("created",await durableRunner.ExecuteAsync(async(s,ct)=>{durableBody++;await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,durableEnvelope,ct);return "created";},async ct=>(await receiptReader.ReconcileAsync(durableRequest,true,ct))?.StableCode));Assert.Equal(1,durableBody);

            var publishId=Guid.NewGuid();var publishRequest=request with{AggregateId=publishId,IdempotencyKey="publish",CommandFamily=PublishProcessModelVersionContract.CommandName};var publishEnvelope=envelope with{Request=publishRequest,Business=new BsonDocument{{"_id",G(publishId)},{"TenantId",G(tenant)},{"IsDeleted",false},{"Version",0}},AuditIntentId=Guid.NewGuid(),EventId=Guid.NewGuid()};
            await Assert.ThrowsAsync<ProcessModelingConflictException>(()=>runner.ExecuteAsync<string>(async(s,ct)=>{await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,publishEnvelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));Assert.Null(await new ProcessModelingMongoRepository(context).FindTenantAsync("mg_process_models",tenant,publishId));

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

    [Fact]
    public async Task Closed_dispatcher_enforces_active_parent_server_owned_graph_and_revision_identity()
    {
        await using var mongo=await Replica.StartAsync();var client=C(mongo.Uri);var db="closed_"+Guid.NewGuid().ToString("N");var context=new ProcessModelingMongoContext(client,db);
        try
        {
            await new ProcessModelingMongoIndexInitializer(context).InitializeAsync();
            var tenant=Guid.NewGuid();var foreign=Guid.NewGuid();var subject=Guid.NewGuid();var definition=Guid.NewGuid();var foreignDefinition=Guid.NewGuid();var modelId=Guid.NewGuid();var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);var now=new DateTime(2026,8,28,12,0,0,DateTimeKind.Utc);
            await context.Collection("mg_process_definitions").InsertManyAsync([
                new BsonDocument{{"_id",G(definition)},{"TenantId",G(tenant)},{"LifecycleState","Active"},{"IsDeleted",false},{"Version",0}},
                new BsonDocument{{"_id",G(foreignDefinition)},{"TenantId",G(foreign)},{"LifecycleState","Active"},{"IsDeleted",false},{"Version",0}}]);
            var writer=new ProcessModelingMongoMutationWriter(context);var runner=new ProcessModelingMongoTransactionRunner(context);
            MongoMutationEnvelope Envelope(CoreMutationRequest request,BsonDocument body,int status=201,Guid? outcome=null)=>new(request,body,"accepted",Guid.NewGuid(),Guid.NewGuid(),now,"subject:test",status,outcome);

            var create=new CoreMutationRequest(tenant,subject,"CreateProcessModelCommand","closed-create","sha256:"+new string('1',64),0,modelId);
            var createBody=new BsonDocument{{"_id",G(modelId)},{"TenantId",G(tenant)},{"ProcessDefinitionId",G(definition)},{"ModelCode","MODEL"},{"Name","Model"},{"IsDeleted",false},{"Version",0}};
            await runner.ExecuteAsync(async(s,ct)=>{await writer.WriteAsync(s,Envelope(create,createBody),ct);return "ok";},_=>Task.FromResult<string?>(null));
            var model=await context.Collection("mg_process_models").Find(Builders<BsonDocument>.Filter.Eq("_id",G(modelId))).SingleAsync();
            Assert.Equal(tenant,ProcessModelingMongoGuidPolicy.Read(model["TenantId"]));Assert.Equal(1,model["LatestRevisionNumber"].AsInt32);Assert.Equal(BsonNull.Value,model["PublishedVersionId"]);Assert.False(model["IsDeleted"].AsBoolean);

            var hiddenId=Guid.NewGuid();var hidden=create with{AggregateId=hiddenId,IdempotencyKey="hidden"};var hiddenBody=createBody.DeepClone().AsBsonDocument;hiddenBody["_id"]=G(hiddenId);hiddenBody["ProcessDefinitionId"]=G(foreignDefinition);
            await Assert.ThrowsAsync<ProcessModelingNotFoundException>(()=>runner.ExecuteAsync<string>(async(s,ct)=>{await writer.WriteAsync(s,Envelope(hidden,hiddenBody),ct);return "wrong";},_=>Task.FromResult<string?>(null)));
            Assert.Equal(0,await context.Collection("mg_process_models").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id",G(hiddenId))));

            var overrideId=Guid.NewGuid();var overridden=create with{AggregateId=overrideId,IdempotencyKey="override"};var overrideBody=createBody.DeepClone().AsBsonDocument;overrideBody["_id"]=G(overrideId);overrideBody["PublishedVersionId"]=G(Guid.NewGuid());
            await Assert.ThrowsAsync<InvalidOperationException>(()=>runner.ExecuteAsync<string>(async(s,ct)=>{await writer.WriteAsync(s,Envelope(overridden,overrideBody),ct);return "wrong";},_=>Task.FromResult<string?>(null)));

            var versionId=Guid.NewGuid();await context.Collection("mg_process_model_versions").InsertOneAsync(new BsonDocument{{"_id",G(versionId)},{"TenantId",G(tenant)},{"ProcessModelId",G(modelId)},{"RevisionNumber",1},{"ModelAuthorActorId",G(subject)},{"LifecycleState","Draft"},{"Title","Old"},{"ContentHash","sha256:"+new string('0',64)},{"IsDeleted",false},{"Version",0}});
            var activity=Guid.NewGuid();var control=Guid.NewGuid();var update=new CoreMutationRequest(tenant,subject,"UpdateDraftProcessModelVersionCommand","graph","sha256:"+new string('2',64),0,versionId);
            var graph=new BsonDocument{{"Title","Updated"},{"Description",BsonNull.Value},{"ContentHash","sha256:"+new string('3',64)},
                {"Activities",new BsonArray{new BsonDocument{{"LogicalActivityId",G(activity)},{"ActivityCode","START"},{"Name","Start"},{"Description",BsonNull.Value},{"SortOrder",0}}}},
                {"ControlPoints",new BsonArray{new BsonDocument{{"LogicalControlPointId",G(control)},{"ControlCode","CP"},{"Name","Check"},{"Description",BsonNull.Value},{"LogicalActivityId",G(activity)},{"SortOrder",0}}}},
                {"Relationships",new BsonArray()}};
            await runner.ExecuteAsync(async(s,ct)=>{await writer.WriteAsync(s,Envelope(update,graph,204),ct);return "ok";},_=>Task.FromResult<string?>(null));
            var storedActivity=await context.Collection("mg_process_activities").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            Assert.Equal(tenant,ProcessModelingMongoGuidPolicy.Read(storedActivity["TenantId"]));Assert.Equal(versionId,ProcessModelingMongoGuidPolicy.Read(storedActivity["ProcessModelVersionId"]));Assert.False(storedActivity.Contains("LifecycleState"));
            await context.Collection("mg_process_model_versions").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id",G(versionId)),Builders<BsonDocument>.Update.Set("LifecycleState","Published"));

            var revisionId=Guid.NewGuid();var revision=new CoreMutationRequest(tenant,subject,"CreateProcessModelRevisionCommand","revision","sha256:"+new string('4',64),0,modelId);
            var revisionBody=new BsonDocument{{"Title","Revision 2"},{"Description",BsonNull.Value},{"ContentHash","sha256:"+new string('5',64)}};
            await runner.ExecuteAsync(async(s,ct)=>{await writer.WriteAsync(s,Envelope(revision,revisionBody,201,revisionId),ct);return "ok";},_=>Task.FromResult<string?>(null));
            var storedRevision=await context.Collection("mg_process_model_versions").Find(Builders<BsonDocument>.Filter.Eq("_id",G(revisionId))).SingleAsync();
            Assert.Equal(modelId,ProcessModelingMongoGuidPolicy.Read(storedRevision["ProcessModelId"]));Assert.Equal(subject,ProcessModelingMongoGuidPolicy.Read(storedRevision["ModelAuthorActorId"]));Assert.Equal(2,storedRevision["RevisionNumber"].AsInt32);Assert.Equal("Draft",storedRevision["LifecycleState"].AsString);
            var updatedModel=await context.Collection("mg_process_models").Find(Builders<BsonDocument>.Filter.Eq("_id",G(modelId))).SingleAsync();Assert.Equal(revisionId,ProcessModelingMongoGuidPolicy.Read(updatedModel["OpenVersionId"]));
        }
        finally{await client.DropDatabaseAsync(db);}
    }

    [Fact]
    public async Task Local_test_not_required_publish_is_atomic_idempotent_and_revalidated_while_required_stays_zero_residue()
    {
        await using var mongo=await Replica.StartAsync();var client=C(mongo.Uri);var db="publish_"+Guid.NewGuid().ToString("N");var context=new ProcessModelingMongoContext(client,db);
        try
        {
            await new ProcessModelingMongoIndexInitializer(context).InitializeAsync();
            var tenant=Guid.NewGuid();var requester=Guid.NewGuid();var publisher=Guid.NewGuid();var author=Guid.NewGuid();
            var modelId=Guid.NewGuid();var versionId=Guid.NewGuid();var hash="sha256:"+new string('a',64);
            var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);
            await context.Collection("mg_process_models").InsertOneAsync(new BsonDocument{{"_id",G(modelId)},{"TenantId",G(tenant)},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","PUBLISH-A"},{"OpenVersionId",G(versionId)},{"PublishedVersionId",BsonNull.Value},{"IsDeleted",false},{"Version",0}});
            await context.Collection("mg_process_model_versions").InsertOneAsync(new BsonDocument{{"_id",G(versionId)},{"TenantId",G(tenant)},{"ProcessModelId",G(modelId)},{"ModelAuthorActorId",G(author)},{"ContentHash",hash},{"LifecycleState","Review"},{"IsDeleted",false},{"Version",0}});
            var time=new FixedTimeProvider(new DateTimeOffset(2026,8,28,12,0,0,TimeSpan.Zero));
            var provider=new PublishEvidenceProvider(publisher,PublishApprovalRequirement.NotRequired);
            var executor=new ProcessModelingLocalTestPublishExecutor(new ProcessModelingLocalTestPublishTargetReader(context),provider,
                new ProcessModelingMongoMutationWriter(context),new ProcessModelingMongoReceiptReader(context),
                new ProcessModelingMongoTransactionRunner(context),time);
            var localHash=new string('b',64);
            var command=new ProcessModelingLocalExecutionRequest("PublishProcessModelVersionCommand",versionId,[],
                new(tenant,requester,ProcessModelingPermissions.ExactCommandMap["PublishProcessModelVersionCommand"],"publish-key",localHash,PublishEvidenceProvider.Principal),0);

            var published=await executor.ExecuteAsync(command,default);
            Assert.True(published.Succeeded);Assert.Equal(204,published.StatusCode);Assert.Equal(2,provider.Calls);
            var version=await context.Collection("mg_process_model_versions").Find(Builders<BsonDocument>.Filter.Eq("_id",G(versionId))).SingleAsync();
            Assert.Equal("Published",version["LifecycleState"].AsString);Assert.Equal(1,version["Version"].AsInt32);
            Assert.Equal(time.GetUtcNow().UtcDateTime,version["PublishedAtUtc"].ToUniversalTime());
            var model=await context.Collection("mg_process_models").Find(Builders<BsonDocument>.Filter.Eq("_id",G(modelId))).SingleAsync();
            Assert.Equal(versionId,ProcessModelingMongoGuidPolicy.Read(model["PublishedVersionId"]));Assert.Equal(BsonNull.Value,model["OpenVersionId"]);Assert.Equal(1,model["Version"].AsInt32);
            Assert.Equal(1,await Count("mg_process_modeling_idempotency_receipts"));Assert.Equal(1,await Count("mg_process_modeling_audit_intents"));Assert.Equal(1,await Count("mg_process_modeling_outbox_messages"));

            var duplicate=await executor.ExecuteAsync(command,default);
            Assert.True(duplicate.Succeeded);Assert.Equal(204,duplicate.StatusCode);
            Assert.Equal(1,await Count("mg_process_modeling_idempotency_receipts"));Assert.Equal(1,await Count("mg_process_modeling_audit_intents"));Assert.Equal(1,await Count("mg_process_modeling_outbox_messages"));

            var requiredModel=Guid.NewGuid();var requiredVersion=Guid.NewGuid();
            await context.Collection("mg_process_models").InsertOneAsync(new BsonDocument{{"_id",G(requiredModel)},{"TenantId",G(tenant)},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","PUBLISH-B"},{"OpenVersionId",G(requiredVersion)},{"PublishedVersionId",BsonNull.Value},{"IsDeleted",false},{"Version",0}});
            await context.Collection("mg_process_model_versions").InsertOneAsync(new BsonDocument{{"_id",G(requiredVersion)},{"TenantId",G(tenant)},{"ProcessModelId",G(requiredModel)},{"ModelAuthorActorId",G(author)},{"ContentHash",hash},{"LifecycleState","Review"},{"IsDeleted",false},{"Version",0}});
            var requiredProvider=new PublishEvidenceProvider(publisher,PublishApprovalRequirement.Required);
            var requiredExecutor=new ProcessModelingLocalTestPublishExecutor(new ProcessModelingLocalTestPublishTargetReader(context),requiredProvider,
                new ProcessModelingMongoMutationWriter(context),new ProcessModelingMongoReceiptReader(context),new ProcessModelingMongoTransactionRunner(context),time);
            var required=await requiredExecutor.ExecuteAsync(command with{RouteId=requiredVersion,Context=command.Context with{IdempotencyKey="required"}},default);
            Assert.False(required.Succeeded);Assert.Equal(503,required.StatusCode);Assert.Equal(1,requiredProvider.Calls);
            Assert.Equal("Review",(await context.Collection("mg_process_model_versions").Find(Builders<BsonDocument>.Filter.Eq("_id",G(requiredVersion))).SingleAsync())["LifecycleState"].AsString);
            Assert.Equal(1,await Count("mg_process_modeling_idempotency_receipts"));Assert.Equal(1,await Count("mg_process_modeling_audit_intents"));Assert.Equal(1,await Count("mg_process_modeling_outbox_messages"));

            async Task<long> Count(string collection)=>await context.Collection(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        }
        finally{await client.DropDatabaseAsync(db);}
    }

    [Fact]
    public async Task Changed_precommit_authority_fence_rolls_back_publish_and_every_participant()
    {
        await using var mongo=await Replica.StartAsync();var client=C(mongo.Uri);var db="publish_fence_"+Guid.NewGuid().ToString("N");var context=new ProcessModelingMongoContext(client,db);
        try
        {
            await new ProcessModelingMongoIndexInitializer(context).InitializeAsync();
            var tenant=Guid.NewGuid();var requester=Guid.NewGuid();var publisher=Guid.NewGuid();var author=Guid.NewGuid();var modelId=Guid.NewGuid();var versionId=Guid.NewGuid();
            var G=(Guid x)=>new BsonBinaryData(x,GuidRepresentation.Standard);var hash="sha256:"+new string('c',64);
            await context.Collection("mg_process_models").InsertOneAsync(new BsonDocument{{"_id",G(modelId)},{"TenantId",G(tenant)},{"ProcessDefinitionId",G(Guid.NewGuid())},{"ModelCode","FENCE-A"},{"OpenVersionId",G(versionId)},{"PublishedVersionId",BsonNull.Value},{"IsDeleted",false},{"Version",0}});
            await context.Collection("mg_process_model_versions").InsertOneAsync(new BsonDocument{{"_id",G(versionId)},{"TenantId",G(tenant)},{"ProcessModelId",G(modelId)},{"ModelAuthorActorId",G(author)},{"ContentHash",hash},{"LifecycleState","Review"},{"IsDeleted",false},{"Version",0}});
            var command=new ProcessModelingLocalExecutionRequest("PublishProcessModelVersionCommand",versionId,[],
                new(tenant,requester,ProcessModelingPermissions.ExactCommandMap["PublishProcessModelVersionCommand"],"changed-fence",new string('d',64),PublishEvidenceProvider.Principal),0);
            var wrongBindingExecutor=new ProcessModelingLocalTestPublishExecutor(new ProcessModelingLocalTestPublishTargetReader(context),
                new PublishEvidenceProvider(publisher,PublishApprovalRequirement.NotRequired,false,true),new ProcessModelingMongoMutationWriter(context),
                new ProcessModelingMongoReceiptReader(context),new ProcessModelingMongoTransactionRunner(context),new FixedTimeProvider(new DateTimeOffset(2026,8,28,12,0,0,TimeSpan.Zero)));
            var wrongBinding=await wrongBindingExecutor.ExecuteAsync(command with{Context=command.Context with{IdempotencyKey="wrong-binding"}},default);
            Assert.False(wrongBinding.Succeeded);Assert.Equal(409,wrongBinding.StatusCode);
            foreach(var fault in new Exception[]{new TimeoutException("provider_timeout"),new InvalidOperationException("provider_failure")})
            {
                var failed=await Executor(new FaultingPublishEvidenceProvider(fault)).ExecuteAsync(command with{Context=command.Context with{IdempotencyKey=Guid.NewGuid().ToString("N")}},default);
                Assert.False(failed.Succeeded);Assert.Equal(503,failed.StatusCode);Assert.Equal(PublishApprovalFailureCodes.AuthorityUnavailable,failed.StableCode);
            }
            await Assert.ThrowsAsync<OperationCanceledException>(()=>Executor(new FaultingPublishEvidenceProvider(new OperationCanceledException())).ExecuteAsync(command,default));
            var malformed=await Executor(new NullPublishEvidenceProvider()).ExecuteAsync(command with{Context=command.Context with{IdempotencyKey="malformed"}},default);
            Assert.False(malformed.Succeeded);Assert.Equal(503,malformed.StatusCode);

            var sameFenceChangedTuple=await Executor(new PublishEvidenceProvider(publisher,PublishApprovalRequirement.NotRequired,false,false,true))
                .ExecuteAsync(command with{Context=command.Context with{IdempotencyKey="same-fence-changed-tuple"}},default);
            Assert.False(sameFenceChangedTuple.Succeeded);Assert.Equal(409,sameFenceChangedTuple.StatusCode);

            var probeCalled=false;
            var probe=await Executor(new PublishEvidenceProvider(publisher,PublishApprovalRequirement.NotRequired,false,false,false,async()=>
            {
                probeCalled=true;
                foreach(var collection in new[]{"mg_process_modeling_idempotency_receipts","mg_process_modeling_audit_intents","mg_process_modeling_outbox_messages"})
                    Assert.Equal(0,await context.Collection(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
                Assert.Equal("Review",(await context.Collection("mg_process_model_versions").Find(Builders<BsonDocument>.Filter.Eq("_id",G(versionId))).SingleAsync())["LifecycleState"].AsString);
            })).ExecuteAsync(command with{Context=command.Context with{IdempotencyKey="probe"}},default);
            Assert.True(probe.Succeeded);Assert.True(probeCalled);

            // Reset the probe publication so the changed-fence cases exercise the original Review snapshot.
            await context.Collection("mg_process_model_versions").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id",G(versionId)),
                new BsonDocument("$set",new BsonDocument{{"LifecycleState","Review"},{"Version",0}}));
            await context.Collection("mg_process_models").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id",G(modelId)),
                new BsonDocument("$set",new BsonDocument{{"OpenVersionId",G(versionId)},{"PublishedVersionId",BsonNull.Value},{"Version",0}}));
            foreach(var collection in new[]{"mg_process_modeling_idempotency_receipts","mg_process_modeling_audit_intents","mg_process_modeling_outbox_messages"})
                await context.Collection(collection).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
            var executor=new ProcessModelingLocalTestPublishExecutor(new ProcessModelingLocalTestPublishTargetReader(context),
                new PublishEvidenceProvider(publisher,PublishApprovalRequirement.NotRequired,true),new ProcessModelingMongoMutationWriter(context),
                new ProcessModelingMongoReceiptReader(context),new ProcessModelingMongoTransactionRunner(context),new FixedTimeProvider(new DateTimeOffset(2026,8,28,12,0,0,TimeSpan.Zero)));
            var result=await executor.ExecuteAsync(command,default);
            Assert.False(result.Succeeded);Assert.Equal(409,result.StatusCode);Assert.Equal(PublishApprovalFailureCodes.AuthorityStale,result.StableCode);
            Assert.Equal("Review",(await context.Collection("mg_process_model_versions").Find(Builders<BsonDocument>.Filter.Eq("_id",G(versionId))).SingleAsync())["LifecycleState"].AsString);
            Assert.Equal(versionId,ProcessModelingMongoGuidPolicy.Read((await context.Collection("mg_process_models").Find(Builders<BsonDocument>.Filter.Eq("_id",G(modelId))).SingleAsync())["OpenVersionId"]));
            foreach(var collection in new[]{"mg_process_modeling_idempotency_receipts","mg_process_modeling_audit_intents","mg_process_modeling_outbox_messages"})
                Assert.Equal(0,await context.Collection(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

            var casRequest=new CoreMutationRequest(tenant,publisher,"PublishProcessModelVersionCommand","content-cas","sha256:"+new string('f',64),0,versionId);
            var casEnvelope=new MongoMutationEnvelope(casRequest,new BsonDocument(),"published",Guid.NewGuid(),Guid.NewGuid(),DateTime.UtcNow,"subject:test",204,versionId,"sha256:"+new string('0',64));
            await Assert.ThrowsAsync<ProcessModelingConflictException>(()=>new ProcessModelingMongoTransactionRunner(context).ExecuteAsync<string>(async(s,ct)=>
            {await new ProcessModelingMongoMutationWriter(context).WriteAsync(s,casEnvelope,ct);return "wrong";},_=>Task.FromResult<string?>(null)));
            foreach(var collection in new[]{"mg_process_modeling_idempotency_receipts","mg_process_modeling_audit_intents","mg_process_modeling_outbox_messages"})
                Assert.Equal(0,await context.Collection(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

            ProcessModelingLocalTestPublishExecutor Executor(ILocalTestPublishAuthorizationEvidenceProvider evidence)=>new(
                new ProcessModelingLocalTestPublishTargetReader(context),evidence,new ProcessModelingMongoMutationWriter(context),
                new ProcessModelingMongoReceiptReader(context),new ProcessModelingMongoTransactionRunner(context),
                new FixedTimeProvider(new DateTimeOffset(2026,8,28,12,0,0,TimeSpan.Zero)));
        }
        finally{await client.DropDatabaseAsync(db);}
    }

    private sealed class PublishEvidenceProvider(Guid publisher,PublishApprovalRequirement requirement,bool changeFence=false,bool wrongRequestHash=false,bool changePermissionGate=false,Func<Task>? onRevalidate=null):ILocalTestPublishAuthorizationEvidenceProvider
    {
        public static readonly Guid Principal=Guid.Parse("50000000-0000-0000-0000-000000000005");
        public int Calls { get; private set; }
        public Task<LocalTestPublishAuthorizationEvidence> ResolveAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,CancellationToken cancellationToken)
        {cancellationToken.ThrowIfCancellationRequested();Calls++;return Task.FromResult(Build(target,requestContext,"combined-1"));}
        public async Task<LocalTestPublishAuthorizationEvidence> RevalidateAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,string expectedCompositeFence,CancellationToken cancellationToken)
        {cancellationToken.ThrowIfCancellationRequested();Calls++;if(onRevalidate is not null)await onRevalidate();return Build(target,requestContext,changeFence?"combined-2":expectedCompositeFence);}
        private LocalTestPublishAuthorizationEvidence Build(LocalTestPublishTargetSnapshot target,LocalTestCommandContext context,string composite)
        {
            var now=new DateTime(2026,8,28,12,0,0,DateTimeKind.Utc);
            var provenance=new PublishActorProvenanceV1(target.TenantId,target.ProcessModelId,target.ProcessModelVersionId,target.ContentHash,target.ModelAuthorActorId,context.ActorId,now.AddMinutes(-1));
            var request=provenance.BindPolicyRequest(publisher);
            var decision=new PublishApprovalPolicyDecisionV1(PublishApprovalAuthorityState.Available,requirement,7,now.AddSeconds(-1),now.AddMinutes(1));
            var gateVersion=changePermissionGate&&Calls>1?8:7;
            var gate=new PublishAuthorityRevalidationV1(new(AuthoritativeDecisionState.Allowed,gateVersion,"gate",now.AddSeconds(-1),now.AddMinutes(1)),new(AuthoritativeDecisionState.Allowed,gateVersion,"gate",now.AddSeconds(-1),now.AddMinutes(1)));
            var input=new PublishApprovalAuthorizationInput(true,true,now,provenance,request,decision,request,decision,
                new(Fu16PublishActorProofState.Available,publisher,true,true),gate,gate,gate);
            var bound=wrongRequestHash?new string('e',64):context.RequestHash;
            return new(input,new(target.TenantId,LocalTestExplicitGrantEvidence.ExpectedModuleCode,context.Permission,publisher,
                Principal,bound,gate),composite,bound);
        }
    }
    private sealed class FaultingPublishEvidenceProvider(Exception error):ILocalTestPublishAuthorizationEvidenceProvider
    {
        public Task<LocalTestPublishAuthorizationEvidence> ResolveAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,CancellationToken cancellationToken)=>Task.FromException<LocalTestPublishAuthorizationEvidence>(error);
        public Task<LocalTestPublishAuthorizationEvidence> RevalidateAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,string expectedCompositeFence,CancellationToken cancellationToken)=>Task.FromException<LocalTestPublishAuthorizationEvidence>(error);
    }
    private sealed class NullPublishEvidenceProvider:ILocalTestPublishAuthorizationEvidenceProvider
    {
        public Task<LocalTestPublishAuthorizationEvidence> ResolveAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,CancellationToken cancellationToken)=>Task.FromResult<LocalTestPublishAuthorizationEvidence>(null!);
        public Task<LocalTestPublishAuthorizationEvidence> RevalidateAsync(LocalTestPublishTargetSnapshot target,LocalTestCommandContext requestContext,string expectedCompositeFence,CancellationToken cancellationToken)=>Task.FromResult<LocalTestPublishAuthorizationEvidence>(null!);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now):TimeProvider
    { public override DateTimeOffset GetUtcNow()=>now; }

    private sealed class InjectedFault:Exception;
    private static MongoClient C(string uri)=>new(ProcessModelingMongoGuidPolicy.ClientSettings(uri));

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
