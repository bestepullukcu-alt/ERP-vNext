using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Tests.BrandProduct;

// MOD-0290-FU02 — in-memory doubles that reproduce the two behaviours the handlers depend on:
//   1. every read is tenant-scoped, so a foreign-tenant row is invisible (surfaces as 404, never as data);
//   2. code uniqueness INCLUDES archived rows, because codes are permanently reserved (FU01 §3/§4).

internal sealed class InMemoryBrandRepository : IBrandRepository
{
    private readonly Guid _tenantId;
    private readonly List<Brand> _entities;

    public InMemoryBrandRepository(Guid tenantId, IEnumerable<Brand> entities)
    {
        _tenantId = tenantId;
        _entities = entities.ToList();
    }

    public IReadOnlyList<Brand> Entities => _entities;

    public List<BrandHistoryEntry> History { get; } = [];

    // WP-MDM-BRAND-ACCEPT-01 FIX1 — a COPY, like a read from a real store: a handler that changes what it read must not
    // change the stored brand before its (conditional) write lands.
    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted) is { } stored
            ? Clone(stored)
            : null);

    public Task<BrandWriteOutcome> CreateWithHistoryAsync(Brand brand, BrandHistoryEntry history, CancellationToken cancellationToken = default)
    {
        if (_entities.Any(x => x.TenantId == _tenantId && string.Equals(x.BrandCode, brand.BrandCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(BrandWriteOutcome.DuplicateCode);
        }

        CreateAsync(brand);
        Record(history, brand.Id, brand.Version);
        return Task.FromResult(BrandWriteOutcome.Written);
    }

    public Task<BrandWriteOutcome> UpdateEditableFieldsWithHistoryAsync(
        Brand proposed, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default)
    {
        var stored = Writable(proposed.Id, expectedVersion);
        if (stored is null)
        {
            return Task.FromResult(BrandWriteOutcome.Stale);
        }

        stored.BrandName = proposed.BrandName;
        stored.BrandStatus = proposed.BrandStatus;
        stored.Description = proposed.Description;
        stored.OwnerCompanyId = proposed.OwnerCompanyId;
        stored.BusinessUnitId = proposed.BusinessUnitId;
        stored.TherapeuticAreaId = proposed.TherapeuticAreaId;
        stored.EffectiveFrom = proposed.EffectiveFrom;
        stored.EffectiveTo = proposed.EffectiveTo;
        stored.ExternalReferences = proposed.ExternalReferences;
        stored.UpdatedBy = proposed.UpdatedBy;
        stored.UpdatedAt = DateTimeOffset.UtcNow;
        stored.Version++;
        Record(history, stored.Id, stored.Version);
        return Task.FromResult(BrandWriteOutcome.Written);
    }

    public Task<BrandWriteOutcome> ArchiveWithHistoryAsync(
        Brand archived, int expectedVersion, BrandHistoryEntry history, CancellationToken cancellationToken = default)
    {
        var stored = Writable(archived.Id, expectedVersion);
        if (stored is null)
        {
            return Task.FromResult(BrandWriteOutcome.Stale);
        }

        stored.IsArchived = true;
        stored.BrandStatus = archived.BrandStatus;
        stored.ArchivedAt = archived.ArchivedAt;
        stored.ArchivedBy = archived.ArchivedBy;
        stored.UpdatedBy = archived.UpdatedBy;
        stored.UpdatedAt = DateTimeOffset.UtcNow;
        stored.Version++;
        Record(history, stored.Id, stored.Version);
        return Task.FromResult(BrandWriteOutcome.Written);
    }

    public Task<IReadOnlyList<BrandHistoryEntry>> GetHistoryAsync(Guid brandId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BrandHistoryEntry>>(History.Where(x => x.BrandId == brandId).ToList());

    public Task<Brand?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && x.Id != excludeId
                        && x.ExternalReferences.Any(r =>
                            string.Equals(r.SourceSystem, sourceSystem, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(r.ExternalId, externalId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.BrandCode, StringComparer.Ordinal)
            .FirstOrDefault());

    private Brand? Writable(Guid id, int expectedVersion)
        => _entities.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted && !x.IsArchived
                                         && x.Version == expectedVersion);

    private void Record(BrandHistoryEntry history, Guid brandId, int resultVersion)
    {
        history.TenantId = _tenantId;
        history.BrandId = brandId;
        history.ResultVersion = resultVersion;
        History.Add(history);
    }

    private static Brand Clone(Brand x) => new()
    {
        Id = x.Id, TenantId = x.TenantId, IsDeleted = x.IsDeleted, DeletedAt = x.DeletedAt, CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt, Version = x.Version, BrandCode = x.BrandCode, BrandName = x.BrandName,
        BrandStatus = x.BrandStatus, Description = x.Description, OwnerCompanyId = x.OwnerCompanyId,
        BusinessUnitId = x.BusinessUnitId, TherapeuticAreaId = x.TherapeuticAreaId, EffectiveFrom = x.EffectiveFrom,
        EffectiveTo = x.EffectiveTo, IsArchived = x.IsArchived, ArchivedAt = x.ArchivedAt, ArchivedBy = x.ArchivedBy,
        CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy,
        ExternalReferences = x.ExternalReferences.Select(r => new BrandProductExternalReference
        {
            SourceSystem = r.SourceSystem, ExternalId = r.ExternalId, ExternalCode = r.ExternalCode,
            ExternalName = r.ExternalName, ImportedAt = r.ImportedAt, IsPrimary = r.IsPrimary
        }).ToList()
    };

    public Task<Brand> CreateAsync(Brand entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        _entities.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(Brand entity, CancellationToken cancellationToken = default)
    {
        var index = _entities.FindIndex(x => x.Id == entity.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version++;
        _entities[index] = entity;
        return Task.FromResult(true);
    }

    // Present only because IRepository<T> declares it. No Brand code path calls it: hard delete is forbidden
    // and no DeleteBrandCommand/handler/endpoint exists.
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Brand hard delete is not supported (MOD-0290-FU01 §3).");

    public Task<bool> ExistsByCodeAsync(string brandCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && string.Equals(x.BrandCode, brandCode, StringComparison.OrdinalIgnoreCase)
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Brand> items = _entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderBy(x => x.BrandName)
            .ToList();
        return Task.FromResult(items);
    }
}

internal sealed class InMemoryProductRepository : IProductRepository
{
    private readonly Guid _tenantId;
    private readonly List<Product> _entities;

    public InMemoryProductRepository(Guid tenantId, IEnumerable<Product> entities)
    {
        _tenantId = tenantId;
        _entities = entities.ToList();
    }

    public IReadOnlyList<Product> Entities => _entities;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<Product> CreateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        _entities.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        var index = _entities.FindIndex(x => x.Id == entity.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Version++;
        _entities[index] = entity;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Product hard delete is not supported (MOD-0290-FU01 §4).");

    public Task<bool> ExistsByCodeAsync(string productCode, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && string.Equals(x.ProductCode, productCode, StringComparison.OrdinalIgnoreCase)
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Product> items = _entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderBy(x => x.ProductName)
            .ToList();
        return Task.FromResult(items);
    }

    public Task<bool> TryCreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (_entities.Any(x => x.TenantId == _tenantId && string.Equals(x.ProductCode, product.ProductCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(false);
        }

        CreateAsync(product);
        return Task.FromResult(true);
    }

    public Task<Product?> FindByExternalReferenceAsync(
        string sourceSystem, string externalId, Guid? excludeId, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && x.Id != excludeId
                        && x.ExternalReferences.Any(r =>
                            string.Equals(r.SourceSystem, sourceSystem, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(r.ExternalId, externalId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.ProductCode, StringComparer.Ordinal)
            .FirstOrDefault());

    public Task<IReadOnlyList<Product>> GetByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Product> items = _entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && x.BrandId == brandId)
            .OrderBy(x => x.ProductName)
            .ToList();
        return Task.FromResult(items);
    }
}
