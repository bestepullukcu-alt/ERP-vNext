using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace Diten.SafetyStockService.Persistence;

public sealed class SafetyStockMongoContext
{
    public SafetyStockMongoContext(IConfiguration configuration)
    {
        var uri = configuration["Mongo:ConnectionString"];
        var database = configuration["Mongo:DatabaseName"];
        if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("Mongo:ConnectionString and Mongo:DatabaseName are required for policy persistence.");

        Client = new MongoClient(MongoClientSettings.FromConnectionString(uri));
        var db = Client.GetDatabase(database);
        Policies = db.GetCollection<PolicyDocument>("mod0191_policies");
        Revisions = db.GetCollection<PolicyRevisionDocument>("mod0191_policy_revisions");
        Audit = db.GetCollection<PolicyAuditDocument>("mod0191_policy_audit");
        Receipts = db.GetCollection<PolicyCommandReceiptDocument>("mod0191_policy_receipts");
        ManualQuantities = db.GetCollection<ManualQuantityDocument>("mod0191_manual_quantities");
        ManualQuantityReceipts = db.GetCollection<ManualQuantityReceiptDocument>("mod0191_manual_quantity_receipts");
    }

    public IMongoClient Client { get; }
    public IMongoCollection<PolicyDocument> Policies { get; }
    public IMongoCollection<PolicyRevisionDocument> Revisions { get; }
    public IMongoCollection<PolicyAuditDocument> Audit { get; }
    public IMongoCollection<PolicyCommandReceiptDocument> Receipts { get; }
    public IMongoCollection<ManualQuantityDocument> ManualQuantities { get; }
    public IMongoCollection<ManualQuantityReceiptDocument> ManualQuantityReceipts { get; }
}
