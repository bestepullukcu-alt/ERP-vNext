using Diten.Application.EnterpriseStrategy.Shared;
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
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> PermissionAliases =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DeliveryExecutionManagementPermissions.OverviewView] = BuildPermissionSet(DeliveryExecutionManagementPermissions.OverviewView),
            [DeliveryExecutionManagementPermissions.InitiativeView] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.InitiativeView,
                EnterpriseStrategyPermissions.InitiativeView),
            [DeliveryExecutionManagementPermissions.InitiativeLink] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.InitiativeLink,
                EnterpriseStrategyPermissions.InitiativeLink),
            [DeliveryExecutionManagementPermissions.InitiativeUnlink] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.InitiativeUnlink,
                EnterpriseStrategyPermissions.InitiativeUnlink),
            [DeliveryExecutionManagementPermissions.InitiativeSync] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.InitiativeSync,
                EnterpriseStrategyPermissions.InitiativeSync),
            [DeliveryExecutionManagementPermissions.ProjectView] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.ProjectView,
                EnterpriseStrategyPermissions.ProjectView),
            [DeliveryExecutionManagementPermissions.ProjectLink] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.ProjectLink,
                EnterpriseStrategyPermissions.ProjectLink),
            [DeliveryExecutionManagementPermissions.ProjectUnlink] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.ProjectUnlink,
                EnterpriseStrategyPermissions.ProjectUnlink),
            [DeliveryExecutionManagementPermissions.ProjectSync] = BuildPermissionSet(
                DeliveryExecutionManagementPermissions.ProjectSync,
                EnterpriseStrategyPermissions.ProjectSync),
            [DeliveryExecutionManagementPermissions.ProgramView] = BuildPermissionSet(DeliveryExecutionManagementPermissions.ProgramView),
            [DeliveryExecutionManagementPermissions.DeliveryMapView] = BuildPermissionSet(DeliveryExecutionManagementPermissions.DeliveryMapView)
        };

    public Task<bool> HasPermissionAsync(string permission, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (IsDevelopmentPermissionBootstrapEnabled() &&
            PermissionAliases.ContainsKey(permission))
        {
            return Task.FromResult(true);
        }

        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        var acceptedPermissions = PermissionAliases.TryGetValue(permission, out var aliases)
            ? aliases
            : BuildPermissionSet(permission);

        var hasPermissionClaim = user.Claims.Any(x =>
            string.Equals(x.Type, "permission", StringComparison.OrdinalIgnoreCase) &&
            acceptedPermissions.Contains(x.Value));

        return Task.FromResult(hasPermissionClaim);
    }

    private static IReadOnlySet<string> BuildPermissionSet(params string[] permissions)
        => new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

    private static bool IsDevelopmentPermissionBootstrapEnabled()
    {
        var enforcePermissions = Environment.GetEnvironmentVariable("DITEN_DELIVERY_EXECUTION_ENFORCE_PERMISSIONS");
        if (string.IsNullOrWhiteSpace(enforcePermissions))
        {
            enforcePermissions = Environment.GetEnvironmentVariable("DITEN_ESBP_ENFORCE_PERMISSIONS");
        }
        if (!string.IsNullOrWhiteSpace(enforcePermissions))
        {
            return !(string.Equals(enforcePermissions, "1", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(enforcePermissions, "true", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(enforcePermissions, "yes", StringComparison.OrdinalIgnoreCase));
        }

        var configured = Environment.GetEnvironmentVariable("DITEN_DELIVERY_EXECUTION_DEV_PERMISSION_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("DITEN_ESBP_DEV_PERMISSION_BOOTSTRAP");
        }

        if (string.IsNullOrWhiteSpace(configured))
            return true;

        return string.Equals(configured, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(configured, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
