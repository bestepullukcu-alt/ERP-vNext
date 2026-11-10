namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
public sealed class NoOpSupplierPerformanceCommitProbe : ISupplierPerformanceCommitProbe { public Task AtAsync(string boundary, CancellationToken ct) => Task.CompletedTask; }
