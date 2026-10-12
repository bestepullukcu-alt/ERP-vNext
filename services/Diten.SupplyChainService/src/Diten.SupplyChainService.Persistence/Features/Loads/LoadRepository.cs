using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using System.Numerics;
using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Persistence.Features.Loads;
public sealed class LoadRepository(IMongoDatabase db,ILoadCommitProbe probe):ILoadRepository
{
 private IMongoCollection<LoadPlan> Loads=>db.GetCollection<LoadPlan>("loads");
 private IMongoCollection<BsonDocument> Receipts=>db.GetCollection<BsonDocument>("loads_receipts").WithReadConcern(ReadConcern.Majority);
 private static BsonDocument Scope(LoadScope s) {s.EnsureTrusted();return new(){{"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()}};}
 private static BsonDocument Visible(LoadScope s) {var f=Scope(s);f.Add("IsDeleted",false);return f;}
 public async Task<IReadOnlyList<LoadPlan>> QueryAsync(LoadScope s,LoadStatus? status,Guid? carrier,CancellationToken ct)
 {var f=Visible(s);if(status is not null)f.Add("Status",status.ToString());if(carrier is not null)f.Add("CarrierId",carrier.ToString());try{return await Loads.Find(f).ToListAsync(ct);}catch(Exception ex)when(ex is MongoException or TimeoutException){throw new LoadFailureException(503,"PERSISTENCE_UNAVAILABLE");}}
 private static LoadMutationResult Replay(BsonDocument receipt,string fingerprint,Guid root)
 {if(receipt["CorrelationRoot"].AsString!=root.ToString())return LoadMutationResult.Error(409,"CORRELATION_ROOT_MISMATCH");if(receipt["Fingerprint"].AsString!=fingerprint)return LoadMutationResult.Error(409,"IDEMPOTENCY_KEY_REUSED");return new(Guid.Parse(receipt["LoadId"].AsString),receipt["LoadNumber"].AsString,receipt["ResultingStatus"].AsString,true,receipt["StatusCode"].AsInt32);}
 public async Task<LoadMutationResult> MutateAsync(LoadScope scope,Guid? id,string key,string fingerprint,Guid root,LoadPlan? create,LoadStatus? target,string? occurredAt,string? note,Func<LoadPlan,Guid?,CancellationToken,Task<string>> observe,CancellationToken ct)
 {
 var identity=Scope(scope);identity.Add("Operation",id is null?"createLoadPlan":"transitionLoad");identity.Add("TargetId",id?.ToString()??"create");identity.Add("IdempotencyKey",key);
 var numberCollisions=0;
 for(var attempt=0;attempt<12;attempt++)
 {
 bool commitAttempted=false;
 try
 {
 using var session=await db.Client.StartSessionAsync(cancellationToken:ct);
 session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority,maxCommitTime:TimeSpan.FromSeconds(5)));
 try
 {
 var receipt=await Receipts.Find(session,identity,new FindOptions{Collation=Collation.Simple}).FirstOrDefaultAsync(ct);
 if(receipt is not null){await session.AbortTransactionAsync(ct);return Replay(receipt,fingerprint,root);}
 LoadPlan load;string? from=null;Guid? cause=null;
 if(id is null)
 {
 load=JsonSerializer.Deserialize<LoadPlan>(JsonSerializer.Serialize(create))!;
 if(load.ShipmentIds.Distinct().Count()!=load.ShipmentIds.Count)throw new LoadFailureException(422,"DUPLICATE_SHIPMENT");
 var stopSequences=load.Stops.Select(x=>BigInteger.Parse(x.Sequence,System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
 if(!stopSequences.SequenceEqual(Enumerable.Range(1,load.Stops.Count).Select(x=>new BigInteger(x)))||!load.Stops.Any(x=>x.Action=="Pickup")||!load.Stops.Any(x=>x.Action=="Delivery"))throw new LoadFailureException(422,"INVALID_LOAD_STOPS");
 load.Id=probe.NewId();load.LoadNumber="LOAD-"+load.Id.ToString("N").ToUpperInvariant();load.TenantId=scope.TenantId;load.LegalEntityId=scope.LegalEntityId;load.CreatedBy=scope.ActorId;load.CreatedAt=DateTimeOffset.UtcNow;load.Status=LoadStatus.Draft;load.Version=1;load.CorrelationRoot=root;
 }
 else
 {
 var f=Visible(scope);f.Add("_id",id.Value.ToString());load=await Loads.Find(session,f).FirstOrDefaultAsync(ct)??throw new LoadFailureException(404,"LOAD_NOT_FOUND");
 if(load.CorrelationRoot!=root)throw new LoadFailureException(409,"CORRELATION_ROOT_MISMATCH");
 if(!LoadLifecycle.Allows(load.Status,target!.Value))throw new LoadFailureException(422,"INVALID_LOAD_TRANSITION");
 from=load.Status.ToString();cause=load.LastEventId;
 }
 // Re-entering a fresh transaction re-observes remote state. Replay above never observes.
 var observations=id is null||LoadLifecycle.NeedsReferences(target!.Value)?await observe(load,id,ct):"[]";
 var assignments=db.GetCollection<BsonDocument>("load_assignments");
 if(id is null)
 {
 foreach(var shipment in load.ShipmentIds)
 {var f=Scope(scope);f.Add("ShipmentId",shipment.ToString());if(await assignments.Find(session,f).AnyAsync(ct))throw new LoadFailureException(409,"SHIPMENT_ALREADY_ASSIGNED");f.Add("LoadId",load.Id.ToString());f.Add("_id",Guid.NewGuid().ToString());await assignments.InsertOneAsync(session,f,cancellationToken:ct);await probe.AtAsync("assignment",ct);}
 await Loads.InsertOneAsync(session,load,cancellationToken:ct);
 }
 else
 {
 var version=load.Version;load.Status=target!.Value;load.Version++;load.UpdatedAt=DateTimeOffset.UtcNow;load.UpdatedBy=scope.ActorId;
 if(target==LoadStatus.Cancelled)foreach(var shipment in load.ShipmentIds){var release=Scope(scope);release.Add("ShipmentId",shipment.ToString());release.Add("LoadId",load.Id.ToString());await assignments.DeleteOneAsync(session,release,cancellationToken:ct);await probe.AtAsync("assignment",ct);}
 var f=Visible(scope);f.Add("_id",load.Id.ToString());f.Add("Version",version);
 var changed=await Loads.ReplaceOneAsync(session,f,load,cancellationToken:ct);if(changed.ModifiedCount!=1)throw new LoadFailureException(503,"PERSISTENCE_UNAVAILABLE");
 }
 await probe.AtAsync("entity",ct);
 var eventId=Guid.NewGuid();load.LastEventId=eventId;
 var final=Visible(scope);final.Add("_id",load.Id.ToString());final.Add("Version",load.Version);await Loads.ReplaceOneAsync(session,final,load,cancellationToken:ct);
 var saved=identity.DeepClone().AsBsonDocument;saved.Add("_id",Guid.NewGuid().ToString());saved.Add("Fingerprint",fingerprint);saved.Add("CorrelationRoot",root.ToString());saved.Add("LoadId",load.Id.ToString());saved.Add("LoadNumber",load.LoadNumber);saved.Add("ResultingStatus",load.Status.ToString());saved.Add("StatusCode",id is null?201:200);saved.Add("ActorId",scope.ActorId.ToString());
 await Receipts.InsertOneAsync(session,saved,cancellationToken:ct);await probe.AtAsync("receipt",ct);
 var audit=Scope(scope);audit.Add("_id",Guid.NewGuid().ToString());audit.Add("LoadId",load.Id.ToString());audit.Add("Version",load.Version);audit.Add("ActorId",scope.ActorId.ToString());audit.Add("CorrelationRoot",root.ToString());audit.Add("FromStatus",from is null?BsonNull.Value:from);audit.Add("ToStatus",load.Status.ToString());audit.Add("Note",note is null?BsonNull.Value:note);audit.Add("CommittedAt",DateTime.UtcNow);audit.Add("References",BsonDocument.Parse("{\"items\":"+observations+"}")["items"]);
 await db.GetCollection<BsonDocument>("loads_audit").InsertOneAsync(session,audit,cancellationToken:ct);await probe.AtAsync("audit",ct);
 var envelope=new {eventId,eventType=id is null?"LoadCreated":"Load"+load.Status,occurredAt=id is null?load.CreatedAt.ToString("O"):occurredAt,correlationId=root,causationId=cause,aggregateType="Load",aggregateId=load.Id,payload=new{loadId=load.Id,loadNumber=load.LoadNumber,status=load.Status.ToString(),shipmentIds=load.ShipmentIds},contractVersion="v1"};
 var pending=Scope(scope);pending.Add("_id",Guid.NewGuid().ToString());pending.Add("EventId",eventId.ToString());pending.Add("LoadId",load.Id.ToString());pending.Add("Status","Pending");pending.Add("Envelope",BsonDocument.Parse(JsonSerializer.Serialize(envelope)));
 await db.GetCollection<BsonDocument>("loads_outbox").InsertOneAsync(session,pending,cancellationToken:ct);await probe.AtAsync("event",ct);await probe.AtAsync("beforeCommit",ct);
 commitAttempted=true;
 for(var commit=0;;commit++){try{await session.CommitTransactionAsync(ct);break;}catch(MongoException ex)when(ex.HasErrorLabel("UnknownTransactionCommitResult")&&commit<2){}}
 await probe.AtAsync("afterCommit",ct);
 return new(load.Id,load.LoadNumber,load.Status.ToString(),false,id is null?201:200);
 }
 catch{if(!commitAttempted&&session.IsInTransaction)try{await session.AbortTransactionAsync(CancellationToken.None);}catch(MongoException){}throw;}
 }
 catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
 catch(LoadFailureException ex){return LoadMutationResult.Error(ex.Status,ex.Code);}
 catch(MongoException ex)when(!commitAttempted&&(ex.HasErrorLabel("TransientTransactionError")||ex is MongoWriteException{WriteError.Category:ServerErrorCategory.DuplicateKey}))
 {
 if(ex is MongoWriteException w && (w.Message.Contains("load_number")||w.Message.Contains("_id_")) && ++numberCollisions>=3)return LoadMutationResult.Error(503,"PERSISTENCE_UNAVAILABLE");
 await Task.Delay(10*(attempt+1),ct);
 }
 catch(Exception ex)when(ex is MongoException or TimeoutException)
 {
 if(commitAttempted)try{var recovered=await Receipts.Find(identity,new FindOptions{Collation=Collation.Simple}).FirstOrDefaultAsync(ct);if(recovered is not null)return Replay(recovered,fingerprint,root);}catch(MongoException){}
 return LoadMutationResult.Error(503,"PERSISTENCE_UNAVAILABLE");
 }
 catch(Exception){return LoadMutationResult.Error(500,"INTERNAL_ERROR");}
 }
 return LoadMutationResult.Error(503,"PERSISTENCE_UNAVAILABLE");
 }
}
