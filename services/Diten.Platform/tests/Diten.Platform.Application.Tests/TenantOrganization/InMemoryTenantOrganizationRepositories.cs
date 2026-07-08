using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.TenantOrganization;

internal sealed class InMemoryOrganizationUnitRepository : IOrganizationUnitRepository
{
    private readonly Guid _tenantId;
    private readonly List<OrganizationUnit> _items = [];

    public InMemoryOrganizationUnitRepository(Guid tenantId) => _tenantId = tenantId;

    public Task<OrganizationUnit> CreateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default)
    {
        _items.Add(organizationUnit);
        return Task.FromResult(organizationUnit);
    }

    public Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<OrganizationUnit> result = _items.Where(x => x.TenantId == _tenantId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_items.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.Code == code
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default)
    {
        organizationUnit.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.First(x => x.Id == id && x.TenantId == _tenantId);
        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public void Add(OrganizationUnit item) => _items.Add(item);
}

internal sealed class InMemoryPositionRepository : IPositionRepository
{
    private readonly Guid _tenantId;
    private readonly List<Position> _items = [];

    public InMemoryPositionRepository(Guid tenantId) => _tenantId = tenantId;

    public Task<Position> CreateAsync(Position position, CancellationToken ct = default)
    {
        _items.Add(position);
        return Task.FromResult(position);
    }

    public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Position> result = _items.Where(x => x.TenantId == _tenantId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_items.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.Code == code
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task UpdateAsync(Position position, CancellationToken ct = default)
    {
        position.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.First(x => x.Id == id && x.TenantId == _tenantId);
        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public void Add(Position item) => _items.Add(item);
}

internal sealed class InMemoryPositionAssignmentRepository : IPositionAssignmentRepository
{
    private readonly Guid _tenantId;
    private readonly List<PositionAssignment> _items = [];

    public InMemoryPositionAssignmentRepository(Guid tenantId) => _tenantId = tenantId;

    public Task<PositionAssignment> CreateAsync(PositionAssignment assignment, CancellationToken ct = default)
    {
        _items.Add(assignment);
        return Task.FromResult(assignment);
    }

    public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PositionAssignment> result = _items.Where(x => x.TenantId == _tenantId && !x.IsDeleted).ToList();
        return Task.FromResult(result);
    }

    public Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, Guid? excludeId = null, CancellationToken ct = default)
    {
        var requestedEnd = effectiveTo ?? DateTimeOffset.MaxValue;
        return Task.FromResult(_items.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.PositionId == positionId
            && (!excludeId.HasValue || x.Id != excludeId.Value)
            && x.EffectiveFrom < requestedEnd
            && (x.EffectiveTo == null || x.EffectiveTo > effectiveFrom)));
    }

    public Task UpdateAsync(PositionAssignment assignment, CancellationToken ct = default)
    {
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.First(x => x.Id == id && x.TenantId == _tenantId);
        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public void Add(PositionAssignment item) => _items.Add(item);
}

internal sealed class InMemoryPersonReferenceRepository : IPersonReferenceRepository
{
    private readonly Guid _tenantId;
    private readonly List<PersonReference> _items = [];
    private readonly bool _throwOnRead;

    public InMemoryPersonReferenceRepository(Guid tenantId, bool throwOnRead = false)
    {
        _tenantId = tenantId;
        _throwOnRead = throwOnRead;
    }

    public Task<PersonReference?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ThrowIfConfigured();
        return Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));
    }

    public Task<IReadOnlyList<PersonReference>> SearchAsync(
        string? query,
        PersonReferenceStatus? status,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        ThrowIfConfigured();
        var normalizedQuery = query?.Trim();
        IEnumerable<PersonReference> result = _items.Where(x => x.TenantId == _tenantId && !x.IsDeleted);
        if (status.HasValue)
        {
            result = result.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            result = result.Where(x =>
                x.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || (x.ReferenceCode?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return Task.FromResult<IReadOnlyList<PersonReference>>(result
            .OrderBy(x => x.DisplayName)
            .Skip(skip)
            .Take(take)
            .ToList());
    }

    public Task<IReadOnlyList<PersonReference>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ThrowIfConfigured();
        return Task.FromResult<IReadOnlyList<PersonReference>>(_items
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToList());
    }

    public void Add(PersonReference item) => _items.Add(item);

    private void ThrowIfConfigured()
    {
        if (_throwOnRead)
        {
            throw new InvalidOperationException("Repository unavailable.");
        }
    }
}
