using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

/// <summary>The operators a value filter may use. Closed — anything else is a 400 before it reaches Mongo.</summary>
public enum OrganizationFieldFilterOperator
{
    Equals = 0,
    In = 1,

    /// <summary>Case-insensitive, PREFIX-ANCHORED contains. String types only; an unanchored scan is refused.</summary>
    StartsWith = 2,

    LessThan = 3,
    LessThanOrEqual = 4,
    GreaterThan = 5,
    GreaterThanOrEqual = 6
}

/// <summary>One clause of a value query, already validated against its definition by the handler.</summary>
public sealed record OrganizationFieldValueFilterSpec(
    Guid DefinitionId,
    OrganizationFieldFilterOperator Operator,
    IReadOnlyList<string> Values);

/// <summary>
/// A validated value query. The handler builds this only after it has proved every named definition is active
/// and <c>IsQueryable</c>, and that each operator is legal for its definition's type — so the repository never
/// has to decide policy.
/// </summary>
public sealed record OrganizationFieldValueQuerySpec(
    Guid? OrganizationUnitId,
    IReadOnlyList<OrganizationFieldValueFilterSpec> Filters,
    Guid? SortDefinitionId,
    bool SortDescending,
    int Skip,
    int Take);

/// <summary>MOD-0288-FU02 — tenant-scoped Organization Unit field values, one document per unit+definition.</summary>
public interface IOrganizationFieldValueRepository
{
    /*
     * ⚠ TryInsert, NOT Insert. "One active value per unit per definition" is enforced by a UNIQUE PARTIAL
     * INDEX in the database, not by a read-then-write in the handler that a second writer can slip through.
     * A duplicate-key rejection is therefore an expected outcome, and it comes back as `false`.
     */
    Task<bool> TryInsertAsync(OrganizationFieldValue value, CancellationToken ct = default);

    Task<bool> TryUpdateAsync(OrganizationFieldValue value, int expectedVersion, CancellationToken ct = default);

    Task<bool> TrySoftDeleteAsync(Guid id, int expectedVersion, CancellationToken ct = default);

    Task<OrganizationFieldValue?> GetAsync(Guid organizationUnitId, Guid definitionId, CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationFieldValue>> GetByUnitAsync(Guid organizationUnitId, CancellationToken ct = default);

    /// <summary>Whether ANY non-deleted value still cites this definition. Guards an incompatible type change.</summary>
    Task<bool> AnyForDefinitionAsync(Guid definitionId, CancellationToken ct = default);

    Task<IReadOnlyList<OrganizationFieldValue>> QueryAsync(OrganizationFieldValueQuerySpec spec, CancellationToken ct = default);
}
