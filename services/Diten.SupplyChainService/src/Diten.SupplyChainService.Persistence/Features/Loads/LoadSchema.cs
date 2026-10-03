using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.Loads;
public sealed class LoadSchema(IMongoDatabase db):IHostedService
{
 public async Task StartAsync(CancellationToken ct)
 {
 var hello=await db.RunCommandAsync<BsonDocument>(new BsonDocument("hello",1),cancellationToken:ct);
 if(!hello.Contains("setName")&&hello.GetValue("msg","").AsString!="isdbgrid")throw new InvalidOperationException("Loads require replica-set transactions.");
 foreach(var n in new[]{"loads","load_assignments","loads_receipts","loads_audit","loads_outbox"}) await Index(n,"scope",false,ct);
 await Index("loads","load_number",true,ct,"LoadNumber");
 await Index("load_assignments","load_assignment",true,ct,"ShipmentId");
 await Index("loads_receipts","load_receipt",true,ct,"Operation","TargetId","IdempotencyKey");
 await Index("loads_outbox","load_event",true,ct,"EventId");
 using var session=await db.Client.StartSessionAsync(cancellationToken:ct);session.StartTransaction();
 try {await db.GetCollection<BsonDocument>("loads").Find(session,new BsonDocument{{"TenantId",Guid.Empty.ToString()},{"LegalEntityId",Guid.Empty.ToString()}}).FirstOrDefaultAsync(ct);} finally{await session.AbortTransactionAsync(ct);}
 }
 private Task<string> Index(string collection,string name,bool unique,CancellationToken ct,params string[] tail)
 { var keys=new BsonDocument{{"TenantId",1},{"LegalEntityId",1}};foreach(var field in tail)keys.Add(field,1);return db.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys,new CreateIndexOptions{Name=name,Unique=unique,Collation=Collation.Simple}),cancellationToken:ct); }
 public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
}
