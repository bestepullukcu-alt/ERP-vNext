using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0164 FU02 consent master. Tenant scoped and soft-delete aware. There is deliberately <b>no delete method</b>:
/// closing a record is the soft archive lifecycle, so consent history (including withdrawals) stays readable forever.
/// The evaluation provider reads through <see cref="ListForEvaluationAsync"/> and performs no writes.
/// </summary>
public interface IConsentRecordRepository
{
    Task<ConsentRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted consent records of a tenant (any status, archived included — history must stay
    /// readable). Callers filter as needed.</summary>
    Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only evaluation seam: non-archived records for one subject on one channel. Purpose, scope and the
    /// effective window are filtered in memory by the evaluation engine (EffectiveFrom/EffectiveTo are
    /// DateTimeOffset — stored as a BSON array — so they are never a compound-index key nor a server-side sort key).
    /// </summary>
    Task<IReadOnlyList<ConsentRecord>> ListForEvaluationAsync(
        Guid tenantId, string subjectType, Guid subjectId, string channel, CancellationToken cancellationToken);

    /// <summary>Duplicate-mapping guard: the first non-archived record already carrying this
    /// (SourceSystem, ExternalId) pair. Silent merge is forbidden — a hit is reported as a conflict.</summary>
    Task<ConsentRecord?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken);

    Task InsertAsync(ConsentRecord record, CancellationToken cancellationToken);

    Task UpdateAsync(ConsentRecord record, CancellationToken cancellationToken);
}

/// <summary>
/// MOD-0164 FU02 preference master. Same rules as <see cref="IConsentRecordRepository"/>: tenant scoped, soft-delete
/// aware, no delete method, read-only evaluation seam.
/// </summary>
public interface IPreferenceRecordRepository
{
    Task<PreferenceRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PreferenceRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Non-archived preferences for one subject. The channel match (exact or the <c>all</c> sentinel) and the
    /// effective window are applied in memory by the evaluation engine.</summary>
    Task<IReadOnlyList<PreferenceRecord>> ListForEvaluationAsync(
        Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken);

    Task<PreferenceRecord?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken);

    Task InsertAsync(PreferenceRecord record, CancellationToken cancellationToken);

    Task UpdateAsync(PreferenceRecord record, CancellationToken cancellationToken);
}
