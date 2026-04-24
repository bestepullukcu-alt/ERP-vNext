using Diten.DevEnablementService.Application.Interfaces;

namespace Diten.DevEnablementService.Persistence;

public sealed class NoOpModuleSeedDataInitializer : IModuleSeedDataInitializer
{
    public Task EnsureMinimumDataAsync(int minimumCount = 20, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
