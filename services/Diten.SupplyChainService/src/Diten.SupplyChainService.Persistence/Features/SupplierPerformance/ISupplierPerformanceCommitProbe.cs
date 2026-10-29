namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
/// <summary>Test seam at the write boundaries of the create transaction (entity, receipt, beforeCommit, commit, afterCommit).</summary>
public interface ISupplierPerformanceCommitProbe { Task AtAsync(string boundary, CancellationToken ct); }
