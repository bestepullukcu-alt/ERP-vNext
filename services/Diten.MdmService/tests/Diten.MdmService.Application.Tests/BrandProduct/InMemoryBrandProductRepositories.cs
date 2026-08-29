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

    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_entities.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

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

    public Task<IReadOnlyList<Product>> GetByBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Product> items = _entities
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && x.BrandId == brandId)
            .OrderBy(x => x.ProductName)
            .ToList();
        return Task.FromResult(items);
    }
}
