namespace Diten.Platform.Application.Contracts;

/// <summary>
/// Reads the DISTINCT permission modules from AuthService (S2S, X-Internal-Api-Key). Best-effort by contract:
/// if AuthService is unreachable it returns an empty list (logged) rather than throwing, so the Module Catalog
/// form degrades gracefully (the ModuleCode select falls back to free-typed tags).
/// </summary>
public interface IAuthPermissionModulesClient
{
    Task<IReadOnlyList<AuthPermissionModule>> GetModulesAsync(CancellationToken ct);
}

public sealed record AuthPermissionModule(string Module, int PermissionCount);
