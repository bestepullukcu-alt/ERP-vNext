using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// The temporary RCS-001 counter behind <see cref="IWarehouseRecordCodeAllocator"/> (pack §8.7, §22; Q-07 B — until BL-565, FU-0178-RCS-01).
/// One document per (caller's tenant, company prefix, type) in <c>wms_number_counters</c>, advanced by ONE atomic
/// <c>findOneAndUpdate($inc, upsert)</c> — two concurrent callers can never read the same value (K3 sabotage (s): a read-then-write here makes
/// the concurrency test issue the same number twice). The counter never resets; the format grows past six digits. The first two upserts of
/// a new counter may race on the unique index: the loser simply runs the increment again on the now-existing document.
/// </summary>
public sealed class WarehouseRecordCodeAllocator : IWarehouseRecordCodeAllocator
{
    internal const string TenantField = "TenantId";
    internal const string CompanyField = "CompanyCodePrefix";
    internal const string TypeField = "TypePrefix";
    internal const string SequenceField = "Seq";

    private const int MaxUpsertAttempts = 3;

    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly ITenantContext _tenant;

    public WarehouseRecordCodeAllocator(IMongoDatabase database, ITenantContext tenant)
    {
        _counters = database.GetCollection<BsonDocument>(WarehouseCollections.NumberCounters);
        _tenant = tenant;
    }

    public async Task<string> NextAsync(string companyCodePrefix, string typePrefix, CancellationToken cancellationToken = default)
    {
        if (!WarehouseRecordCodeRules.IsValidCompanyCode(companyCodePrefix))
            throw new ArgumentException("The company prefix must be 2–5 capital letters A–Z.", nameof(companyCodePrefix));
        if (!WarehouseRecordCodeRules.IsKnownTypePrefix(typePrefix))
            throw new ArgumentException($"Unknown record type prefix '{typePrefix}'.", nameof(typePrefix));

        var filter = Builders<BsonDocument>.Filter.Eq(TenantField, _tenant.TenantId.ToString("D"))
                     & Builders<BsonDocument>.Filter.Eq(CompanyField, companyCodePrefix)
                     & Builders<BsonDocument>.Filter.Eq(TypeField, typePrefix);
        var update = Builders<BsonDocument>.Update.Inc(SequenceField, 1L);
        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var counter = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
                return WarehouseRecordCodeRules.Format(companyCodePrefix, typePrefix, counter[SequenceField].ToInt64());
            }
            catch (MongoCommandException ex) when (ex.Code == 11000 && attempt < MaxUpsertAttempts)
            {
                // two first upserts of the same counter: the other one created it — increment the existing document
            }
            catch (MongoException ex)
            {
                throw new WarehouseRecordCodeUnavailableException("The record-code counter could not be written.", ex);
            }
            catch (TimeoutException ex)
            {
                throw new WarehouseRecordCodeUnavailableException("The record-code counter could not be reached.", ex);
            }
        }
    }
}
