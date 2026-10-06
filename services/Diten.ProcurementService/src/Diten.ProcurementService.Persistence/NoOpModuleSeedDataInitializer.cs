using Diten.ProcurementService.Application.Interfaces;

namespace Diten.ProcurementService.Persistence;

public sealed class NoOpModuleSeedDataInitializer : IModuleSeedDataInitializer
{
    public Task EnsureMinimumDataAsync(int minimumCount = 20, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
