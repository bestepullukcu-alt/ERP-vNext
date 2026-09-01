using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.LocalTest;

public sealed class FrontendLocalTestClosureArchitectureTests
{
    [Fact]
    public void Api_surface_is_authenticated_exact_permission_and_provider_unavailable()
    {
        var controller = ReadService(
            "src",
            "Diten.ManagementGovernanceService.Api",
            "Controllers",
            "ProcessModelingLocalTestController.cs");

        Assert.Contains("[Authorize]", controller, StringComparison.Ordinal);
        Assert.Contains("internal/local-test/v1/process-modeling", controller, StringComparison.Ordinal);
        Assert.Contains("ProcessModelingPermissions.ExactPermissions", controller, StringComparison.Ordinal);
        Assert.Contains("ProcessModelingErrors.ProviderUnavailable", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Mongo", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("broker", controller, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(16, ProcessModelingPermissions.ExactPermissions.Count);
    }

    [Fact]
    public void Frontend_boundary_is_default_off_and_never_forwards_credentials()
    {
        var gateway = ReadRepository(
            "frontend",
            "Diten.Web",
            "Services",
            "ManagementGovernance",
            "ProcessModeling",
            "ProcessModelingFrontendGateway.cs");

        Assert.Contains("public bool IsReady => false", gateway, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("AuthTokenCookies", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Tenant-Id", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", gateway, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shared_host_and_tracked_configuration_remain_unmodified_by_process_modeling()
    {
        var program = ReadService(
            "src",
            "Diten.ManagementGovernanceService.Api",
            "Program.cs");

        Assert.DoesNotContain("ProcessModeling", program, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(ServiceRoot(), "appsettings*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(ServiceRoot(), "launchSettings.json", SearchOption.AllDirectories));
    }

    private static string ReadService(params string[] segments) =>
        File.ReadAllText(Path.Combine([ServiceRoot(), .. segments]));

    private static string ReadRepository(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepositoryRoot(), .. segments]));

    private static string ServiceRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null
               && !string.Equals(cursor.Name, "Diten.ManagementGovernanceService", StringComparison.Ordinal))
            cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("Service root not found.");
    }

    private static string RepositoryRoot()
    {
        var cursor = new DirectoryInfo(ServiceRoot());
        while (cursor.Parent is not null
               && !Directory.Exists(Path.Combine(cursor.FullName, ".git"))
               && !File.Exists(Path.Combine(cursor.FullName, ".git")))
            cursor = cursor.Parent;
        return cursor.FullName;
    }
}
