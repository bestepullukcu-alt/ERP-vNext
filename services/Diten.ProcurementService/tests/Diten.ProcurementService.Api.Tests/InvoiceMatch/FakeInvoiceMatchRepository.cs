using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;

namespace Diten.ProcurementService.Api.Tests.InvoiceMatch;

/// <summary>
/// In-memory IInvoiceMatchRepository test double. Models the PRODUCTION repository's binding contract: every
/// read/write over both the invoice and match-exception stores is scoped to (TenantId, LegalEntityId, IsDeleted=false),
/// exactly as InvoiceMatchRepository's tenant filters enforce via Mongo. Two instances over the SAME shared stores but
/// DIFFERENT legal entities let handler tests prove cross-LE isolation (a record written under LE-A is invisible to an
/// LE-B scope → handler 404). The Mongo filter itself is pinned in InvoiceMatchRepository.cs (no Mongo in unit env).
/// </summary>
public sealed class FakeInvoiceMatchRepository : IInvoiceMatchRepository
{
    private readonly List<Invoice> _invoices;
    private readonly List<MatchException> _exceptions;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public FakeInvoiceMatchRepository(
        List<Invoice> sharedInvoiceStore,
        List<MatchException> sharedExceptionStore,
        Guid tenantId,
        Guid legalEntityId)
    {
        _invoices = sharedInvoiceStore;
        _exceptions = sharedExceptionStore;
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
    }

    private IEnumerable<Invoice> ScopedInvoices()
        => _invoices.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    private IEnumerable<MatchException> ScopedExceptions()
        => _exceptions.Where(x => x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted);

    // ── Invoice ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<Invoice>> GetAllAsync(string? supplierId = null, InvoiceStatus? status = null, CancellationToken cancellationToken = default)
    {
        var items = ScopedInvoices();
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            items = items.Where(x => x.SupplierId == supplierId);
        }
        if (status.HasValue)
        {
            items = items.Where(x => x.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<Invoice>>(items.ToList());
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedInvoices().FirstOrDefault(x => x.Id == id));

    public Task<Invoice?> GetByInvoiceIdAsync(string invoiceId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedInvoices().FirstOrDefault(x => x.InvoiceId == invoiceId));

    public Task<Invoice?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedInvoices().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));

    public Task<bool> ExistsByInvoiceNumberAsync(string supplierId, string invoiceNumber, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedInvoices().Any(x => x.SupplierId == supplierId && x.InvoiceNumber == invoiceNumber));

    public Task<Invoice> CreateAsync(Invoice entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _invoices.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(Invoice entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var existing = _invoices.FirstOrDefault(x =>
            x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted
            && x.Id == entity.Id && x.Version == expectedVersion);
        if (existing is null)
        {
            return Task.FromResult(false);
        }

        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version = expectedVersion + 1;
        if (!ReferenceEquals(existing, entity))
        {
            _invoices.Remove(existing);
            _invoices.Add(entity);
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var e = ScopedInvoices().FirstOrDefault(x => x.Id == id);
        if (e is null)
        {
            return Task.FromResult(false);
        }
        e.IsDeleted = true;
        e.DeletedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var e in ScopedInvoices().Where(x => ids.Contains(x.Id)).ToList())
        {
            e.IsDeleted = true;
            e.DeletedAt = DateTimeOffset.UtcNow;
            count++;
        }
        return Task.FromResult(count);
    }

    // ── Match Exception kuyruğu ──────────────────────────────────────────────────

    public Task<MatchException> CreateExceptionAsync(MatchException entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _exceptions.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<MatchException?> GetExceptionByExceptionIdAsync(string exceptionId, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedExceptions().FirstOrDefault(x => x.ExceptionId == exceptionId));

    public Task<MatchException?> GetExceptionByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(ScopedExceptions().FirstOrDefault(x => x.ResolveIdempotencyKey == idempotencyKey));

    public Task<IReadOnlyList<MatchException>> ListExceptionsAsync(MatchExceptionReason? reasonCode = null, CancellationToken cancellationToken = default)
    {
        var items = ScopedExceptions();
        if (reasonCode.HasValue)
        {
            items = items.Where(x => x.ReasonCode == reasonCode.Value);
        }
        return Task.FromResult<IReadOnlyList<MatchException>>(items.ToList());
    }

    public Task<bool> UpdateExceptionAsync(MatchException entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var existing = _exceptions.FirstOrDefault(x =>
            x.TenantId == _tenantId && x.LegalEntityId == _legalEntityId && !x.IsDeleted
            && x.Id == entity.Id && x.Version == expectedVersion);
        if (existing is null)
        {
            return Task.FromResult(false);
        }

        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version = expectedVersion + 1;
        if (!ReferenceEquals(existing, entity))
        {
            _exceptions.Remove(existing);
            _exceptions.Add(entity);
        }
        return Task.FromResult(true);
    }
}
