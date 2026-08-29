using System.Security.Claims;

namespace Diten.Application.DeliveryExecutionManagement.Shared;

public static class DeliveryExecutionManagementModules
{
    public const string Overview = "delivery-execution-management.overview";
    public const string Initiatives = "delivery-execution-management.initiatives";
    public const string Projects = "delivery-execution-management.projects";
    public const string Programs = "delivery-execution-management.programs";
    public const string DeliveryMap = "delivery-execution-management.delivery-map";
}

public static class DeliveryExecutionManagementPermissions
{
    public const string OverviewView = "delivery-execution-management.overview.view";
    public const string InitiativeView = "delivery-execution-management.initiative.view";
    public const string InitiativeLink = "delivery-execution-management.initiative.link";
    public const string InitiativeUnlink = "delivery-execution-management.initiative.unlink";
    public const string InitiativeSync = "delivery-execution-management.initiative.sync";
    public const string ProjectView = "delivery-execution-management.project.view";
    public const string ProjectLink = "delivery-execution-management.project.link";
    public const string ProjectUnlink = "delivery-execution-management.project.unlink";
    public const string ProjectSync = "delivery-execution-management.project.sync";
    public const string ProgramView = "delivery-execution-management.program.view";
    public const string DeliveryMapView = "delivery-execution-management.delivery-map.view";
}

public interface IDeliveryExecutionManagementAuthorizationService
{
    Task<bool> HasPermissionAsync(string permission, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class DefaultDeliveryExecutionManagementAuthorizationService : IDeliveryExecutionManagementAuthorizationService
{
    public Task<bool> HasPermissionAsync(string permission, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        var hasPermissionClaim = user.Claims.Any(x =>
            string.Equals(x.Type, "permission", StringComparison.Ordinal) &&
            string.Equals(x.Value, permission, StringComparison.Ordinal));

        return Task.FromResult(hasPermissionClaim);
    }
}
