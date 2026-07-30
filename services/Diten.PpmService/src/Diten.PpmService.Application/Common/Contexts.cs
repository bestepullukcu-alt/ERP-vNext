using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;

public interface ITenantContext { Guid TenantId { get; } }
public interface ICurrentActorContext { Guid ActorId { get; } }

/// <summary>Consumes the authoritative, PSS-approved effective-permission result. It never computes roles or grants.</summary>
public interface IEffectivePermissionEvaluator
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken);
}

public interface IPpmEntitlementDecisionClient
{
    Task<bool> IsAllowedAsync(Guid tenantId, CancellationToken cancellationToken);
}

public interface IPpmAccessAuthorizer
{
    Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken cancellationToken);
}

public enum PpmAccessDecision
{
    Allowed,
    Forbidden,
    DependencyUnavailable
}

public sealed class PpmEntitlementDependencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PpmAccessAuthorizer(
    ITenantContext tenant,
    ICurrentActorContext actor,
    IPpmEntitlementDecisionClient entitlement,
    IEffectivePermissionEvaluator permissions) : IPpmAccessAuthorizer
{
    public async Task<PpmAccessDecision> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken)
    {
        if (ApplicationGuard.InvalidContext(tenant, actor))
        {
            return PpmAccessDecision.Forbidden;
        }

        try
        {
            if (!await entitlement.IsAllowedAsync(tenant.TenantId, cancellationToken))
            {
                return PpmAccessDecision.Forbidden;
            }
        }
        catch (PpmEntitlementDependencyException)
        {
            return PpmAccessDecision.DependencyUnavailable;
        }

        return await permissions.HasPermissionAsync(permission, cancellationToken)
            ? PpmAccessDecision.Allowed
            : PpmAccessDecision.Forbidden;
    }
}

public static class PpmAccessDecisionExtensions
{
    public static Response<T> Failure<T>(this PpmAccessDecision decision) =>
        decision == PpmAccessDecision.DependencyUnavailable
            ? Response<T>.Fail("Authoritative entitlement decision is unavailable.", 503)
            : Response<T>.Fail("Forbidden.", 403);
}

public static class PpmPermissions
{
    public const string PortfoliosRead = "ppm.portfolios.read";
    public const string PortfoliosCreate = "ppm.portfolios.create";
    public const string PortfoliosUpdate = "ppm.portfolios.update";
    public const string PortfoliosLifecycle = "ppm.portfolios.change-lifecycle";
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
