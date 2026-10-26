using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.Mrp;

public sealed class LeadTimeRevisionDocument
{
    public string Id { get; set; } = "";
    public int Days { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveUntilUtc { get; set; }
    public string State { get; set; } = "Draft";
    public List<string> Contributors { get; set; } = [];
    public string? ReviewerId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class LeadTimePolicyDocument
{
    [BsonId] public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string LegalEntityId { get; set; } = "";
    public string SkuId { get; set; } = "";
    public string WarehouseId { get; set; } = "";
    public string CurrentRevisionId { get; set; } = "";
    public long Version { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<LeadTimeRevisionDocument> Revisions { get; set; } = [];
}

public sealed class LeadTimeAuditDocument
{
    [BsonId] public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string LegalEntityId { get; set; } = "";
    public string SkuId { get; set; } = "";
    public string WarehouseId { get; set; } = "";
    public string PolicyId { get; set; } = "";
    public string RevisionId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string PreviousState { get; set; } = "";
    public string State { get; set; } = "";
    public int Days { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveUntilUtc { get; set; }
    public string? Reason { get; set; }
    public long Version { get; set; }
    public DateTime AtUtc { get; set; }
}

public sealed class LeadTimeReceiptDocument
{
    [BsonId] public string Id { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string PolicyId { get; set; } = "";
    public string RevisionId { get; set; } = "";
    public string LegalEntityId { get; set; } = "";
    public string SkuId { get; set; } = "";
    public string WarehouseId { get; set; } = "";
    public string State { get; set; } = "";
    public long Version { get; set; }
    public string Outcome { get; set; } = "";
}

public sealed class LeadTimeMongoContext
{
    public LeadTimeMongoContext(IConfiguration configuration)
    {
        var uri = configuration["Mongo:ConnectionString"];
        var database = configuration["Mongo:SupplyChainDatabaseName"];
        if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("Mongo connection and shared database must be configured.");

        Client = new MongoClient(MongoClientSettings.FromConnectionString(uri));
        var db = Client.GetDatabase(database);
        Policies = db.GetCollection<LeadTimePolicyDocument>("mod0189_lead_time_policies");
        Audit = db.GetCollection<LeadTimeAuditDocument>("mod0189_lead_time_audit");
        Receipts = db.GetCollection<LeadTimeReceiptDocument>("mod0189_lead_time_receipts");
    }

    public IMongoClient Client { get; }
    public IMongoCollection<LeadTimePolicyDocument> Policies { get; }
    public IMongoCollection<LeadTimeAuditDocument> Audit { get; }
    public IMongoCollection<LeadTimeReceiptDocument> Receipts { get; }
}
