namespace Diten.DevEnablementService.Application.Interfaces;

/// <summary>
/// Ensures minimum demo data for modules that need immediate list visibility.
/// </summary>
public interface IModuleSeedDataInitializer
{
    Task EnsureMinimumDataAsync(int minimumCount = 20, CancellationToken ct = default);
}
