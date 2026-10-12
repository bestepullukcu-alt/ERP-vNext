using MongoDB.Bson;using MongoDB.Driver;using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.SandopPlans;
// Q220 (2026-10-03): this was a static helper that only a test ever called, so in a composed host the six
// indexes below were never created — including five UNIQUE ones that SandopRepository depends on to detect a
// duplicate key. Its five peers (Capacity, Carrier, Load, Return, Claim) all register an IHostedService.
// It now follows that pattern. EnsureAsync stays static so the existing caller in SandopContractTests keeps
// working unchanged. No transaction/replica-set assertion was added here: CapacitySchema has one, but adding
// it would change startup behaviour beyond this fix, so it is recorded as a separate open item.
public sealed class SandopSchema(IMongoDatabase db) : IHostedService
{
 public Task StartAsync(CancellationToken ct)=>EnsureAsync(db,ct);
 public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
 public static async Task EnsureAsync(IMongoDatabase db,CancellationToken ct=default)
{
 static CreateIndexModel<BsonDocument> Ix(string name,bool unique,params string[] fields)=>new(Builders<BsonDocument>.IndexKeys.Combine(fields.Select(x=>Builders<BsonDocument>.IndexKeys.Ascending(x))),new CreateIndexOptions{Name=name,Unique=unique,Collation=Collation.Simple});
 await db.GetCollection<BsonDocument>("sandop_plans").Indexes.CreateOneAsync(Ix("sandop_active_horizon_demand",true,"TenantId","LegalEntityId","IsDeleted","HorizonStart","HorizonEnd","DemandPlanId","DemandPlanVersion"),cancellationToken:ct);
 await db.GetCollection<BsonDocument>("sandop_snapshots").Indexes.CreateOneAsync(Ix("sandop_snapshot_sequence",true,"TenantId","LegalEntityId","PlanId","Sequence"),cancellationToken:ct);
 await db.GetCollection<BsonDocument>("sandop_sign_offs").Indexes.CreateOneAsync(Ix("sandop_role_snapshot",true,"TenantId","LegalEntityId","IsDeleted","SnapshotId","Role"),cancellationToken:ct);
 await db.GetCollection<BsonDocument>("sandop_receipts").Indexes.CreateOneAsync(Ix("sandop_receipt_identity",true,"TenantId","LegalEntityId","Operation","TargetId","Key"),cancellationToken:ct);
 await db.GetCollection<BsonDocument>("sandop_audit").Indexes.CreateOneAsync(Ix("sandop_audit_scope",false,"TenantId","LegalEntityId","PlanId"),cancellationToken:ct);
 await db.GetCollection<BsonDocument>("sandop_outbox").Indexes.CreateOneAsync(Ix("sandop_outbox_scope",false,"TenantId","LegalEntityId","Status"),cancellationToken:ct);
} }
