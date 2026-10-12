using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>Inserts service-to-service access rows into <c>inventory_audit</c> (WP-0173-PM2). Insert only.</summary>
public sealed class InternalAccessAuditWriter : IInternalAccessAuditWriter
{
    private readonly IMongoCollection<InternalServiceAccessAuditRecord> _audit;

    public InternalAccessAuditWriter(IMongoDatabase database)
        => _audit = database.GetCollection<InternalServiceAccessAuditRecord>(InventoryCollections.Audit);

    public Task WriteAsync(InternalServiceAccessAuditRecord record, CancellationToken cancellationToken = default)
        => _audit.InsertOneAsync(record, cancellationToken: cancellationToken);
}
