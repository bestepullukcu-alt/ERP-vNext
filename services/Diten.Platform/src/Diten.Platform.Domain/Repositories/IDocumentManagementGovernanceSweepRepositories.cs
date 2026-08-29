using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU32 — governance sweep run-history repository contract. Tenant-scoped via the TenantRepository
// ExecutionFilter. Deliberately NO delete method: sweep run history is append-only governance evidence. The single
// UpdateAsync exists only to close out a run that was opened at start (status, CompletedAt, counters) — it is never
// used to revise a completed run.

public interface IDocumentGovernanceSweepRunRepository
{
    Task<DocumentGovernanceSweepRun> CreateAsync(DocumentGovernanceSweepRun run, CancellationToken ct = default);

    /// <summary>Tenant-scoped read — a cross-tenant id resolves to null (no leakage).</summary>
    Task<DocumentGovernanceSweepRun?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Most recent first.</summary>
    Task<IReadOnlyList<DocumentGovernanceSweepRun>> GetAllForTenantAsync(CancellationToken ct = default);

    /// <summary>The latest run of a given sweep key, or null if that sweep never ran.</summary>
    Task<DocumentGovernanceSweepRun?> GetLatestBySweepKeyAsync(string sweepKey, CancellationToken ct = default);

    /// <summary>Closes out a run opened at start. Never used to revise an already-completed run.</summary>
    Task<bool> UpdateAsync(DocumentGovernanceSweepRun run, CancellationToken ct = default);
}
