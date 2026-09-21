using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Invoice + Match-Exception Mongo repository (MOD-0143). Tek yazıcı (K15). HER sorgu Tenant + LegalEntity +
/// IsDeleted=false ile filtrelenir (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404).
/// Soft-delete zorunlu; hard delete YOK. State geçişi (match/resolve) optimistic concurrency: Version filtresi +
/// increment. Modül ÖDEME YÜRÜTMEZ — yalnız match outcome + exception kuyruğunu persist eder.
/// </summary>
public sealed class InvoiceMatchRepository : IInvoiceMatchRepository
{
    private readonly IMongoCollection<Invoice> _invoices;
    private readonly IMongoCollection<MatchException> _exceptions;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public InvoiceMatchRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _invoices = database.GetCollection<Invoice>(InvoiceMatchIndexConfiguration.InvoiceCollectionName);
        _exceptions = database.GetCollection<MatchException>(InvoiceMatchIndexConfiguration.MatchExceptionCollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    // ── Invoice ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(string? supplierId = null, InvoiceStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filter = InvoiceTenantFilter();
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filter &= Builders<Invoice>.Filter.Eq(x => x.SupplierId, supplierId);
        }
        if (status.HasValue)
        {
            filter &= Builders<Invoice>.Filter.Eq(x => x.Status, status.Value);
        }
        return await _invoices.Find(filter).SortBy(x => x.InvoiceId).ToListAsync(cancellationToken);
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.Eq(x => x.Id, id));
        return await _invoices.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Invoice?> GetByInvoiceIdAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.Eq(x => x.InvoiceId, invoiceId));
        return await _invoices.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Invoice?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _invoices.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsByInvoiceNumberAsync(string supplierId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.Eq(x => x.SupplierId, supplierId),
            Builders<Invoice>.Filter.Eq(x => x.InvoiceNumber, invoiceNumber));
        return await _invoices.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<Invoice> CreateAsync(Invoice entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _invoices.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(Invoice entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.Eq(x => x.Id, entity.Id),
            Builders<Invoice>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _invoices.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.Eq(x => x.Id, id));

        var update = Builders<Invoice>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _invoices.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Invoice>.Filter.And(
            InvoiceTenantFilter(),
            Builders<Invoice>.Filter.In(x => x.Id, ids));

        var update = Builders<Invoice>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _invoices.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    // ── Match Exception kuyruğu ──────────────────────────────────────────────────

    public async Task<MatchException> CreateExceptionAsync(MatchException entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _exceptions.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<MatchException?> GetExceptionByExceptionIdAsync(string exceptionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MatchException>.Filter.And(
            ExceptionTenantFilter(),
            Builders<MatchException>.Filter.Eq(x => x.ExceptionId, exceptionId));
        return await _exceptions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MatchException?> GetExceptionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MatchException>.Filter.And(
            ExceptionTenantFilter(),
            Builders<MatchException>.Filter.Eq(x => x.ResolveIdempotencyKey, idempotencyKey));
        return await _exceptions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchException>> ListExceptionsAsync(MatchExceptionReason? reasonCode = null, CancellationToken cancellationToken = default)
    {
        var filter = ExceptionTenantFilter();
        if (reasonCode.HasValue)
        {
            filter &= Builders<MatchException>.Filter.Eq(x => x.ReasonCode, reasonCode.Value);
        }
        return await _exceptions.Find(filter).SortBy(x => x.ExceptionId).ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateExceptionAsync(MatchException entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<MatchException>.Filter.And(
            ExceptionTenantFilter(),
            Builders<MatchException>.Filter.Eq(x => x.Id, entity.Id),
            Builders<MatchException>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _exceptions.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her Invoice sorgusunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<Invoice> InvoiceTenantFilter()
        => Builders<Invoice>.Filter.And(
            Builders<Invoice>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<Invoice>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<Invoice>.Filter.Eq(x => x.IsDeleted, false));

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her MatchException sorgusunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<MatchException> ExceptionTenantFilter()
        => Builders<MatchException>.Filter.And(
            Builders<MatchException>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<MatchException>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<MatchException>.Filter.Eq(x => x.IsDeleted, false));
}
