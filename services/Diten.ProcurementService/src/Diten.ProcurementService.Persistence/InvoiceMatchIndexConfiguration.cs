using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Invoice (invoices) + MatchException (match_exceptions) collection index tanımları (MOD-0143 §4). Tenant-first
/// indexing (mongo-indexing.md DB-001): her index TenantId (+ LegalEntityId) ile başlar. InvoiceId/IdempotencyKey ve
/// (SupplierId+InvoiceNumber) duplicate anahtarı tenant+LE bazında unique.
/// </summary>
public static class InvoiceMatchIndexConfiguration
{
    public const string InvoiceCollectionName = "invoices";
    public const string MatchExceptionCollectionName = "match_exceptions";

    public static IReadOnlyList<CreateIndexModel<Invoice>> BuildInvoice()
    {
        var b = Builders<Invoice>.IndexKeys;

        return new List<CreateIndexModel<Invoice>>
        {
            // Compound tenant+LE — her sorgunun giriş filtresi (COLLSCAN önleme).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // InvoiceId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.InvoiceId),
                new CreateIndexOptions { Name = "ux_tenant_le_invoiceid", Unique = true }),

            // Duplicate anahtarı: (SupplierId+InvoiceNumber) tenant+LE bazında unique → 409 DUPLICATE_INVOICE.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SupplierId).Ascending(x => x.InvoiceNumber),
                new CreateIndexOptions { Name = "ux_tenant_le_supplier_invoicenumber", Unique = true }),

            // Idempotency-Key — tenant+LE bazında unique (replay-safe capture). Sparse: yalnız key taşıyanlar.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = "ux_tenant_le_idempotencykey", Unique = true, Sparse = true }),

            // supplierId filtresi (liste).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SupplierId),
                new CreateIndexOptions { Name = "ix_tenant_le_supplierid" }),

            // Status filtresi (liste).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<MatchException>> BuildMatchException()
    {
        var b = Builders<MatchException>.IndexKeys;

        return new List<CreateIndexModel<MatchException>>
        {
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // ExceptionId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ExceptionId),
                new CreateIndexOptions { Name = "ux_tenant_le_exceptionid", Unique = true }),

            // reasonCode filtresi (listMatchExceptions reasonCode).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ReasonCode),
                new CreateIndexOptions { Name = "ix_tenant_le_reasoncode" }),

            // Status filtresi (Open kuyruğu).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" }),

            // Resolve Idempotency-Key — tenant+LE bazında unique (replay-safe). Sparse.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ResolveIdempotencyKey),
                new CreateIndexOptions { Name = "ux_tenant_le_resolvekey", Unique = true, Sparse = true })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var invoices = database.GetCollection<Invoice>(InvoiceCollectionName);
        await invoices.Indexes.CreateManyAsync(BuildInvoice(), ct);

        var exceptions = database.GetCollection<MatchException>(MatchExceptionCollectionName);
        await exceptions.Indexes.CreateManyAsync(BuildMatchException(), ct);
    }
}
