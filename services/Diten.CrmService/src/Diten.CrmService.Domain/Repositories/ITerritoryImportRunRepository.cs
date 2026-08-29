using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0151 FU08 import run history. <b>Append-only by construction</b>: the interface exposes an insert and two
/// reads and deliberately has no update or delete member, so "history was rewritten" is not expressible in code.
/// </summary>
public interface ITerritoryImportRunRepository
{
    Task InsertAsync(TerritoryImportRun run, CancellationToken cancellationToken);

    /// <summary>Runs of one model, newest first.</summary>
    Task<IReadOnlyList<TerritoryImportRun>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    /// <summary>Previous applies of the same file (same tenant + model + hash) — used to report a re-run.</summary>
    Task<IReadOnlyList<TerritoryImportRun>> ListByFileHashAsync(
        Guid tenantId, Guid modelId, string fileHash, CancellationToken cancellationToken);
}
