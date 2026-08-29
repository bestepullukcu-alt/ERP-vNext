using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface ITerritoryAssignmentRuleRepository
{
    Task<TerritoryAssignmentRule?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, Guid modelId, string ruleCode, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>All non-deleted rules of a model, ordered by Priority (lower first) then RuleCode.</summary>
    Task<IReadOnlyList<TerritoryAssignmentRule>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    Task InsertAsync(TerritoryAssignmentRule rule, CancellationToken cancellationToken);

    Task UpdateAsync(TerritoryAssignmentRule rule, CancellationToken cancellationToken);
}

/// <summary>
/// FU03 read-only account seam. MOD-0151 needs Account attributes as preview INPUT only; it deliberately does not
/// take a dependency on <see cref="IAccountRepository"/> (which exposes writes) so the boundary stays one-directional
/// and testable: this interface has no mutating member, therefore FU03 cannot touch the MOD-0149 master (pack §11.1).
/// </summary>
public interface ITerritoryAccountReader
{
    Task<IReadOnlyList<TerritoryAccountSnapshot>> GetByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);

    /// <summary>Non-deleted accounts of the tenant, capped by <paramref name="limit"/>. Returns the projection the
    /// preview matcher needs — never the full Account aggregate.</summary>
    Task<IReadOnlyList<TerritoryAccountSnapshot>> ListForPreviewAsync(Guid tenantId, int limit, CancellationToken cancellationToken);

    /// <summary>Total non-deleted account count for the tenant (preview reports how much of the base was scanned).</summary>
    Task<long> CountAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>Read-only account projection consumed by the FU03 preview matcher. Not persisted by MOD-0151.</summary>
public sealed record TerritoryAccountSnapshot(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? AccountType,
    string? AccountCategory,
    string? Status,
    string? CountryRef,
    string? CityRef,
    string? DistrictRef);
