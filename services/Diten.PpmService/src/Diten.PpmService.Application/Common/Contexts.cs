namespace Diten.PpmService.Application.Common;

public interface ITenantContext { Guid TenantId { get; } }
public interface ICurrentActorContext { Guid ActorId { get; } }

/// <summary>Consumes the authoritative, PSS-approved effective-permission result. It never computes roles or grants.</summary>
public interface IEffectivePermissionEvaluator
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken);
}

public static class PpmPermissions
{
    public const string PortfoliosRead = "ppm.portfolios.read";
    public const string PortfoliosCreate = "ppm.portfolios.create";
    public const string PortfoliosUpdate = "ppm.portfolios.update";
    public const string PortfoliosArchive = "ppm.portfolios.archive";
    public const string InitiativesRead = "ppm.initiatives.read";
    public const string InitiativesCreate = "ppm.initiatives.create";
    public const string InitiativesUpdate = "ppm.initiatives.update";
    public const string InitiativesLifecycle = "ppm.initiatives.change-lifecycle";
    public const string ProgramsRead = "ppm.programs.read";
    public const string ProgramsCreate = "ppm.programs.create";
    public const string ProgramsUpdate = "ppm.programs.update";
    public const string ProgramsLifecycle = "ppm.programs.change-lifecycle";
    public const string ProjectsRead = "ppm.projects.read";
    public const string ProjectsCreate = "ppm.projects.create";
    public const string ProjectsUpdate = "ppm.projects.update";
    public const string ProjectsLifecycle = "ppm.projects.change-lifecycle";
}
