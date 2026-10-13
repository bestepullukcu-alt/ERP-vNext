using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.SandopPlans;
using Diten.SupplyChainService.Application.Features.SandopPlans;
namespace Diten.SupplyChainService.Persistence.Features.SandopPlans;
public sealed class SandopRepository(IMongoDatabase db):ISandopRepository
{
 IMongoCollection<BsonDocument> Plans=>db.GetCollection<BsonDocument>("sandop_plans");
 IMongoCollection<BsonDocument> Snapshots=>db.GetCollection<BsonDocument>("sandop_snapshots");
 IMongoCollection<BsonDocument> SignOffs=>db.GetCollection<BsonDocument>("sandop_sign_offs");
 IMongoCollection<BsonDocument> Receipts=>db.GetCollection<BsonDocument>("sandop_receipts");
 IMongoCollection<BsonDocument> Audit=>db.GetCollection<BsonDocument>("sandop_audit");
 IMongoCollection<BsonDocument> Outbox=>db.GetCollection<BsonDocument>("sandop_outbox");
 static BsonDocument Scope(SandopScope s){s.EnsureTrusted();return new(){{"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()}};}
 static BsonDocument Active(SandopScope s){var d=Scope(s);d.Add("IsDeleted",false);return d;}
 static BsonDocument PlanFilter(SandopScope s,Guid id){var d=Active(s);d.Add("_id",id.ToString());return d;}
 static BsonDocument ReceiptFilter(SandopScope s,SandopAction action,Guid? id,string key){var d=Scope(s);d.Add("Operation",action.ToString());d.Add("TargetId",id?.ToString()??"create");d.Add("Key",key);return d;}
 static string S(BsonDocument d,string field)=>d[field].AsString;
 static SandopResult Replay(BsonDocument receipt,string fingerprint)=>S(receipt,"Fingerprint")==fingerprint?new(receipt["StatusCode"].AsInt32,null,S(receipt,"Body")):SandopResult.Error(409,"IDEMPOTENCY_KEY_REUSED");
 static string PlanBody(BsonDocument p)=>JsonSerializer.Serialize(new{sandopPlanId=S(p,"_id"),name=S(p,"Name"),horizonStart=S(p,"HorizonStart"),horizonEnd=S(p,"HorizonEnd"),demandPlanId=S(p,"DemandPlanId"),demandPlanVersion=S(p,"DemandPlanVersion"),status=S(p,"Status"),currentSnapshotId=p["CurrentSnapshotId"].IsBsonNull?null:S(p,"CurrentSnapshotId"),createdAt=S(p,"CreatedAt"),contractVersion="v1"});
 static string SnapshotBody(BsonDocument s)=>JsonSerializer.Serialize(new{snapshotId=S(s,"_id"),sandopPlanId=S(s,"PlanId"),provenance=JsonDocument.Parse(S(s,"Provenance")).RootElement,supplyInputRefs=JsonDocument.Parse(S(s,"SupplyInputRefs")).RootElement,capturedAt=S(s,"CapturedAt"),contractVersion="v1"});
 static string SignOffBody(BsonDocument s)=>JsonSerializer.Serialize(new{signOffId=S(s,"_id"),sandopPlanId=S(s,"PlanId"),snapshotId=S(s,"SnapshotId"),role=S(s,"Role"),decision=S(s,"Decision"),comment=s["Comment"].IsBsonNull?null:S(s,"Comment"),decidedBy=S(s,"DecidedBy"),decidedAt=S(s,"DecidedAt"),contractVersion="v1"},new JsonSerializerOptions{DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull});
 public async Task<SandopResult> ReadAsync(SandopScope scope,SandopAction action,Guid planId,CancellationToken ct)
 { scope.EnsureTrusted();try{var p=await Plans.Find(PlanFilter(scope,planId)).FirstOrDefaultAsync(ct);if(p is null)return SandopResult.Error(404,"UNKNOWN_SANDOP_PLAN");
 if(action==SandopAction.Get)return new(200,null,PlanBody(p));
 if(action==SandopAction.ListSnapshots){var f=Active(scope);f.Add("PlanId",planId.ToString());var rows=await Snapshots.Find(f).Sort(Builders<BsonDocument>.Sort.Ascending("Sequence")).ToListAsync(ct);return new(200,null,SandopProjection.Items(rows.Select(SnapshotBody)));}
 if(action==SandopAction.ListSignOffs){var f=Active(scope);f.Add("PlanId",planId.ToString());var rows=await SignOffs.Find(f).Sort(Builders<BsonDocument>.Sort.Ascending("DecidedAt")).ToListAsync(ct);return new(200,null,SandopProjection.Items(rows.Select(SignOffBody)));}
 return SandopResult.Error(400,"INVALID_REQUEST");}catch(Exception ex)when(ex is MongoException or TimeoutException){return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE");} }
 public async Task<SandopResult> MutateAsync(SandopScope scope,SandopAction action,Guid? planId,string key,string fingerprint,Guid correlation,JsonElement body,IDemandFixtureReader fixture,CancellationToken ct)
 { scope.EnsureTrusted();if(action is not (SandopAction.Create or SandopAction.Capture or SandopAction.SignOff)||string.IsNullOrEmpty(key))return SandopResult.Error(400,"INVALID_REQUEST");
 var invalid=SandopWire.Validate(action,body);if(invalid is not null)return invalid;
 var rf=ReceiptFilter(scope,action,planId,key);
 // For target operations, scoped existence precedes receipt exposure.
 if(planId is not null){try{if(!await Plans.Find(PlanFilter(scope,planId.Value)).AnyAsync(ct))return SandopResult.Error(404,"UNKNOWN_SANDOP_PLAN");}catch(Exception ex)when(ex is MongoException or TimeoutException){return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE");}}
 try{var saved=await Receipts.Find(rf,new FindOptions{Collation=Collation.Simple}).FirstOrDefaultAsync(ct);if(saved is not null)return Replay(saved,fingerprint);}catch(Exception ex)when(ex is MongoException or TimeoutException){return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE");}
 for(var attempt=0;attempt<3;attempt++)
 { bool commitAttempted=false;
 IClientSessionHandle session;
 try { session=await db.Client.StartSessionAsync(cancellationToken:ct); }
 catch(Exception ex) when(ex is MongoException or TimeoutException) { return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE"); }
 using var sessionLifetime=session;
 try{session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority,maxCommitTime:TimeSpan.FromSeconds(5)));
 var saved=await Receipts.Find(session,rf,new FindOptions{Collation=Collation.Simple}).FirstOrDefaultAsync(ct);if(saved is not null){await session.AbortTransactionAsync(ct);return Replay(saved,fingerprint);}
 BsonDocument? p=null;if(planId is not null){p=await Plans.Find(session,PlanFilter(scope,planId.Value)).FirstOrDefaultAsync(ct);if(p is null){await session.AbortTransactionAsync(ct);return SandopResult.Error(404,"UNKNOWN_SANDOP_PLAN");}}
 string result,eventType;object payload;Guid aggregateId;DateTimeOffset now=DateTimeOffset.UtcNow;
 if(action==SandopAction.Create)
 { var demandId=body.GetProperty("demandPlanId").GetString()!;var version=body.GetProperty("demandPlanVersion").GetString()!;
 if(!await fixture.MatchesAsync(scope,demandId,version,null,ct)){await session.AbortTransactionAsync(ct);return SandopResult.Error(422,"INVALID_DEMAND_REFERENCE");}
 var duplicate=Active(scope);duplicate.Add("HorizonStart",body.GetProperty("horizonStart").GetString());duplicate.Add("HorizonEnd",body.GetProperty("horizonEnd").GetString());duplicate.Add("DemandPlanId",demandId);duplicate.Add("DemandPlanVersion",version);
 if(await Plans.Find(session,duplicate).AnyAsync(ct)){await session.AbortTransactionAsync(ct);return SandopResult.Error(409,"SANDOP_PLAN_ALREADY_EXISTS");}
 aggregateId=Guid.NewGuid();p=Scope(scope);p.Add("_id",aggregateId.ToString());p.Add("IsDeleted",false);p.Add("DeletedAt",BsonNull.Value);p.Add("Name",body.GetProperty("name").GetString());p.Add("HorizonStart",body.GetProperty("horizonStart").GetString());p.Add("HorizonEnd",body.GetProperty("horizonEnd").GetString());p.Add("DemandPlanId",demandId);p.Add("DemandPlanVersion",version);p.Add("Status","Draft");p.Add("CurrentSnapshotId",BsonNull.Value);p.Add("Version",1);p.Add("CreatedAt",now.ToString("O"));p.Add("CreatedBy",scope.ActorId.ToString());
 await Plans.InsertOneAsync(session,p,cancellationToken:ct);result=PlanBody(p);eventType="sandop.plan.created.v1";payload=new{sandopPlanId=aggregateId,demandPlanId=demandId,demandPlanVersion=version};
 }
 else if(action==SandopAction.Capture)
 { aggregateId=planId!.Value;if(S(p!,"Status") is not ("Draft" or "InReview")){await session.AbortTransactionAsync(ct);return SandopResult.Error(409,"SANDOP_PLAN_STATE_CONFLICT");}
 var demandId=body.GetProperty("demandPlanId").GetString()!;var version=body.GetProperty("demandPlanVersion").GetString()!;var checksum=body.GetProperty("sourceChecksum").GetString()!;
 if(demandId!=S(p!,"DemandPlanId")||version!=S(p!,"DemandPlanVersion")||!await fixture.MatchesAsync(scope,demandId,version,checksum,ct)){await session.AbortTransactionAsync(ct);return SandopResult.Error(422,"INVALID_DEMAND_REFERENCE");}
 var seq=(int)p!["Version"].ToInt64();var snapshotId=Guid.NewGuid();var captured=DateTimeOffset.Parse(body.GetProperty("sourceCapturedAt").GetString()!);
 var provenance=new{demandPlanId=demandId,demandPlanVersion=version,sourceContract="DEMAND",sourceContractVersion="v1",sourceCapturedAt=captured,sourceChecksum=checksum};
 var refs=body.TryGetProperty("supplyInputRefs",out var r)?r.GetRawText():"[]";
 var s=Scope(scope);s.Add("_id",snapshotId.ToString());s.Add("PlanId",aggregateId.ToString());s.Add("Sequence",seq);s.Add("IsDeleted",false);s.Add("DeletedAt",BsonNull.Value);s.Add("Provenance",JsonSerializer.Serialize(provenance));s.Add("SupplyInputRefs",refs);s.Add("CapturedAt",now.ToString("O"));
 await Snapshots.InsertOneAsync(session,s,cancellationToken:ct);
 var before=p["Version"].ToInt64();var pf=PlanFilter(scope,aggregateId);pf.Add("Version",before);var update=Builders<BsonDocument>.Update.Set("Status","InReview").Set("CurrentSnapshotId",snapshotId.ToString()).Inc("Version",1);
 if((await Plans.UpdateOneAsync(session,pf,update,cancellationToken:ct)).ModifiedCount!=1)throw new MongoException("Concurrent plan version change");
 result=SnapshotBody(s);eventType="sandop.snapshot.captured.v1";payload=new{sandopPlanId=aggregateId,snapshotId,provenance};
 }
 else
 { aggregateId=planId!.Value;if(S(p!,"Status")!="InReview"){await session.AbortTransactionAsync(ct);return SandopResult.Error(409,"SANDOP_SIGN_OFF_STATE_CONFLICT");}
 var snapshotId=Guid.Parse(body.GetProperty("snapshotId").GetString()!);var sf=Active(scope);sf.Add("_id",snapshotId.ToString());sf.Add("PlanId",aggregateId.ToString());
 if(!await Snapshots.Find(session,sf).AnyAsync(ct)){await session.AbortTransactionAsync(ct);return SandopResult.Error(422,"INVALID_SNAPSHOT_REFERENCE");}
 var role=body.GetProperty("role").GetString()!;var decision=body.GetProperty("decision").GetString()!;var dup=Active(scope);dup.Add("SnapshotId",snapshotId.ToString());dup.Add("Role",role);
 if(await SignOffs.Find(session,dup).AnyAsync(ct)){await session.AbortTransactionAsync(ct);return SandopResult.Error(409,"SIGN_OFF_ALREADY_RECORDED");}
 var signOffId=Guid.NewGuid();var s=Scope(scope);s.Add("_id",signOffId.ToString());s.Add("PlanId",aggregateId.ToString());s.Add("SnapshotId",snapshotId.ToString());s.Add("Role",role);s.Add("Decision",decision);s.Add("Comment",body.TryGetProperty("comment",out var c)?c.GetString():BsonNull.Value);s.Add("DecidedBy",scope.ActorId.ToString());s.Add("DecidedAt",now.ToString("O"));s.Add("IsDeleted",false);s.Add("DeletedAt",BsonNull.Value);
 await SignOffs.InsertOneAsync(session,s,cancellationToken:ct);result=SignOffBody(s);eventType="sandop.sign-off.recorded.v1";payload=new{sandopPlanId=aggregateId,snapshotId,signOffId,role,decision};
 }
 var receipt=rf.DeepClone().AsBsonDocument;receipt.Add("_id",Guid.NewGuid().ToString());receipt.Add("Fingerprint",fingerprint);receipt.Add("StatusCode",201);receipt.Add("Body",result);receipt.Add("ActorId",scope.ActorId.ToString());receipt.Add("OriginalCorrelationId",correlation.ToString());await Receipts.InsertOneAsync(session,receipt,cancellationToken:ct);
 var audit=Scope(scope);audit.Add("_id",Guid.NewGuid().ToString());audit.Add("PlanId",aggregateId.ToString());audit.Add("Operation",action.ToString());audit.Add("ActorId",scope.ActorId.ToString());audit.Add("CorrelationId",correlation.ToString());audit.Add("OccurredAt",now.ToString("O"));await Audit.InsertOneAsync(session,audit,cancellationToken:ct);
 var ev=new{eventId=Guid.NewGuid(),eventType,occurredAt=now,correlationId=correlation,causationId=(Guid?)null,payload,contractVersion="v1"};
 var outbox=Scope(scope);outbox.Add("_id",Guid.NewGuid().ToString());outbox.Add("PlanId",aggregateId.ToString());outbox.Add("Status","Pending");outbox.Add("Envelope",JsonSerializer.Serialize(ev));await Outbox.InsertOneAsync(session,outbox,cancellationToken:ct);
 commitAttempted=true;await session.CommitTransactionAsync(ct);return new(201,null,result);
 }catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
 catch(Exception ex)when(ex is MongoException or TimeoutException)
 { if(!commitAttempted&&session.IsInTransaction)try{await session.AbortTransactionAsync(CancellationToken.None);}catch(Exception abortError)when(abortError is MongoException or TimeoutException){}
 if(commitAttempted){try{var saved=await Receipts.Find(rf,new FindOptions{Collation=Collation.Simple}).FirstOrDefaultAsync(ct);if(saved is not null)return Replay(saved,fingerprint);}catch(Exception receiptError)when(receiptError is MongoException or TimeoutException){}return SandopResult.Error(503,"COMMIT_RESULT_UNRESOLVED");}
 if(ex is MongoException m&&(m.HasErrorLabel("TransientTransactionError")||m.Message.Contains("Concurrent plan version change")))continue;
 if(ex is MongoWriteException{WriteError.Category:ServerErrorCategory.DuplicateKey})
 { try { var resolved=await Receipts.Find(rf,new FindOptions{Collation=Collation.Simple}).FirstOrDefaultAsync(ct);if(resolved is not null)return Replay(resolved,fingerprint); } catch(Exception receiptError) when(receiptError is MongoException or TimeoutException) { return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE"); }
 if(action==SandopAction.SignOff)return SandopResult.Error(409,"SIGN_OFF_ALREADY_RECORDED");if(action==SandopAction.Create)return SandopResult.Error(409,"SANDOP_PLAN_ALREADY_EXISTS");continue;}
 return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE");}
 catch{if(session.IsInTransaction)try{await session.AbortTransactionAsync(CancellationToken.None);}catch(MongoException){}return SandopResult.Error(500,"INTERNAL_ERROR");}
 }return SandopResult.Error(503,"DEPENDENCY_UNAVAILABLE");
 }
}
