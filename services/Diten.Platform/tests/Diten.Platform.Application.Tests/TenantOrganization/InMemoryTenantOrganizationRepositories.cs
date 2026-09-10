using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.TenantOrganization;

internal sealed class InMemoryOrganizationUnitRepository
    : IOrganizationUnitRepository, IOrganizationReportingGraphRepository
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

    // MOD-0288-FU02 — the batched hop read the two-line cycle guard walks with.
    public Task<IReadOnlyList<OrganizationUnit>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        BatchedReadCalls++;
        IReadOnlyList<OrganizationUnit> result = _items
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && ids.Contains(x.Id))
            .ToList();
        return Task.FromResult(result);
    }

    /// <summary>How many level reads the guard issued. Lets a test assert BATCHING rather than assume it.</summary>
    public int BatchedReadCalls { get; private set; }

    /*
     * ⚠ THIS FAKE IS NOT THE CONCURRENCY PROOF, AND IT MUST NOT BE MISTAKEN FOR ONE. It reproduces the CAS
     * contract so the handler tests can exercise the loser's path; it runs in one process and proves nothing
     * about two. The guarantee is demonstrated against a real Mongo, with two genuinely concurrent writers,
     * in OrganizationMatrixReportingTests.Two_concurrent_reparentings_cannot_both_land.
     */
    public Task<long> ReadStructureTokenAsync(CancellationToken ct = default)
    {
        var observed = _structureToken;

        /*
         * The interleaving the guard exists for, made deterministic: another writer's parent mutation lands
         * AFTER this caller read the token and BEFORE it writes. Setting it outside the read would not
         * reproduce it — the caller would simply read the newer value and win.
         */
        if (RaceOnNextRead)
        {
            RaceOnNextRead = false;
            _structureToken++;
        }

        return Task.FromResult(observed);
    }

    /// <summary>Arms one simulated concurrent parent mutation, landing between the next read and its write.</summary>
    public bool RaceOnNextRead { get; set; }

    public Task<bool> TryAdvanceStructureTokenAsync(long expectedToken, CancellationToken ct = default)
    {
        if (_structureToken != expectedToken)
        {
            return Task.FromResult(false);
        }

        _structureToken++;
        return Task.FromResult(true);
    }

    private long _structureToken;

    public void Add(OrganizationUnit item) => _items.Add(item);
}

// ── MOD-0288-FU02 ────────────────────────────────────────────────────────────────────────────────────────

internal sealed class InMemoryOrganizationFieldDefinitionRepository : IOrganizationFieldDefinitionRepository
{
    private readonly Guid _tenantId;
    private readonly List<OrganizationFieldDefinition> _items = [];

    public InMemoryOrganizationFieldDefinitionRepository(Guid tenantId) => _tenantId = tenantId;

    public Task<OrganizationFieldDefinition> CreateAsync(OrganizationFieldDefinition definition, CancellationToken ct = default)
    {
        _items.Add(definition);
        return Task.FromResult(definition);
    }

    /*
     * ⚠ READS RETURN A COPY, BECAUSE A REAL REPOSITORY DOES. Mongo hands back a fresh deserialization, so a
     * handler that mutates what it read and then loses the conditional write has changed nothing. A fake that
     * hands back the stored instance aliases the two, and the difference is not cosmetic: it makes a REFUSED
     * update look as though it landed, which is the opposite of what the test is checking.
     */
    public Task<OrganizationFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Copy(_items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted)));

    public Task<IReadOnlyList<OrganizationFieldDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<OrganizationFieldDefinition> result =
            _items.Where(x => x.TenantId == _tenantId && !x.IsDeleted).Select(Copy).ToList()!;
        return Task.FromResult(result);
    }

    private static OrganizationFieldDefinition? Copy(OrganizationFieldDefinition? source) => source is null
        ? null
        : new OrganizationFieldDefinition
        {
            Id = source.Id,
            TenantId = source.TenantId,
            Code = source.Code,
            Name = source.Name,
            DataType = source.DataType,
            IsRequired = source.IsRequired,
            IsActive = source.IsActive,
            IsQueryable = source.IsQueryable,
            ValidationRules = source.ValidationRules,
            DisplayOrder = source.DisplayOrder,
            Classification = source.Classification,
            DeletedAt = source.DeletedAt,
            IsDeleted = source.IsDeleted,
            Version = source.Version,
            UpdatedAt = source.UpdatedAt
        };

    public Task<bool> TryUpdateAsync(OrganizationFieldDefinition definition, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == definition.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        definition.Version = expectedVersion + 1;
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        _items[_items.IndexOf(stored)] = definition;
        return Task.FromResult(true);
    }

    public void Add(OrganizationFieldDefinition item) => _items.Add(item);
}

internal sealed class InMemoryOrganizationFieldValueRepository : IOrganizationFieldValueRepository
{
    private readonly Guid _tenantId;
    private readonly List<OrganizationFieldValue> _items = [];

    public InMemoryOrganizationFieldValueRepository(Guid tenantId) => _tenantId = tenantId;

    // Mirrors the UNIQUE PARTIAL INDEX, which is where production actually enforces this.
    public Task<bool> TryInsertAsync(OrganizationFieldValue value, CancellationToken ct = default)
    {
        var clash = _items.Any(x =>
            x.TenantId == value.TenantId
            && !x.IsDeleted
            && x.OrganizationUnitId == value.OrganizationUnitId
            && x.DefinitionId == value.DefinitionId);

        if (clash)
        {
            return Task.FromResult(false);
        }

        _items.Add(value);
        return Task.FromResult(true);
    }

    public Task<bool> TryUpdateAsync(OrganizationFieldValue value, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == value.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        value.Version = expectedVersion + 1;
        value.UpdatedAt = DateTimeOffset.UtcNow;
        _items[_items.IndexOf(stored)] = value;
        return Task.FromResult(true);
    }

    public Task<bool> TrySoftDeleteAsync(Guid id, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        stored.IsDeleted = true;
        stored.DeletedAt = DateTimeOffset.UtcNow;
        stored.Version = expectedVersion + 1;
        return Task.FromResult(true);
    }

    // Copies on read, for the reason spelled out on the definition repository above.
    public Task<OrganizationFieldValue?> GetAsync(Guid organizationUnitId, Guid definitionId, CancellationToken ct = default) =>
        Task.FromResult(Copy(_items.FirstOrDefault(x =>
            x.TenantId == _tenantId && !x.IsDeleted
            && x.OrganizationUnitId == organizationUnitId && x.DefinitionId == definitionId)));

    public Task<IReadOnlyList<OrganizationFieldValue>> GetByUnitAsync(Guid organizationUnitId, CancellationToken ct = default)
    {
        IReadOnlyList<OrganizationFieldValue> result = _items
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted && x.OrganizationUnitId == organizationUnitId)
            .Select(Copy)
            .ToList()!;
        return Task.FromResult(result);
    }

    private static OrganizationFieldValue? Copy(OrganizationFieldValue? source) => source is null
        ? null
        : new OrganizationFieldValue
        {
            Id = source.Id,
            TenantId = source.TenantId,
            OrganizationUnitId = source.OrganizationUnitId,
            DefinitionId = source.DefinitionId,
            ValueType = source.ValueType,
            Value = source.Value,
            Classification = source.Classification,
            DeletedAt = source.DeletedAt,
            IsDeleted = source.IsDeleted,
            Version = source.Version,
            UpdatedAt = source.UpdatedAt
        };

    public Task<bool> AnyForDefinitionAsync(Guid definitionId, CancellationToken ct = default) =>
        Task.FromResult(_items.Any(x => x.TenantId == _tenantId && !x.IsDeleted && x.DefinitionId == definitionId));

    public Task<IReadOnlyList<OrganizationFieldValue>> QueryAsync(OrganizationFieldValueQuerySpec spec, CancellationToken ct = default)
    {
        IEnumerable<OrganizationFieldValue> rows = _items.Where(x => x.TenantId == _tenantId && !x.IsDeleted);

        if (spec.OrganizationUnitId.HasValue)
        {
            rows = rows.Where(x => x.OrganizationUnitId == spec.OrganizationUnitId.Value);
        }

        foreach (var clause in spec.Filters)
        {
            var c = clause;
            rows = rows.Where(x => x.DefinitionId == c.DefinitionId && Matches(x.Value, c));
        }

        rows = spec.SortDefinitionId.HasValue
            ? spec.SortDescending
                ? rows.OrderByDescending(x => x.Value, StringComparer.Ordinal)
                : rows.OrderBy(x => x.Value, StringComparer.Ordinal)
            : rows.OrderBy(x => x.Id);

        IReadOnlyList<OrganizationFieldValue> result = rows.Skip(spec.Skip).Take(spec.Take).Select(Copy).ToList()!;
        return Task.FromResult(result);
    }

    private static bool Matches(string? value, OrganizationFieldValueFilterSpec clause) => clause.Operator switch
    {
        OrganizationFieldFilterOperator.Equals => string.Equals(value, clause.Values[0], StringComparison.Ordinal),
        OrganizationFieldFilterOperator.In => clause.Values.Contains(value, StringComparer.Ordinal),
        OrganizationFieldFilterOperator.StartsWith =>
            value is not null && value.StartsWith(clause.Values[0], StringComparison.OrdinalIgnoreCase),
        OrganizationFieldFilterOperator.LessThan => string.CompareOrdinal(value, clause.Values[0]) < 0,
        OrganizationFieldFilterOperator.LessThanOrEqual => string.CompareOrdinal(value, clause.Values[0]) <= 0,
        OrganizationFieldFilterOperator.GreaterThan => string.CompareOrdinal(value, clause.Values[0]) > 0,
        OrganizationFieldFilterOperator.GreaterThanOrEqual => string.CompareOrdinal(value, clause.Values[0]) >= 0,
        _ => false
    };

    public void Add(OrganizationFieldValue item) => _items.Add(item);
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
