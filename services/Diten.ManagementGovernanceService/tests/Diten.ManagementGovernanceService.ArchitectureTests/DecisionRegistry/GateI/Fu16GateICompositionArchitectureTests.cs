using System.Reflection;
using Diten.ManagementGovernanceService.Api.DecisionRegistry;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.DecisionRegistry.GateI;

public sealed class Fu16GateICompositionArchitectureTests
{
    [Fact]
    public void Controller_selects_exact_named_scheme()
    {
        var attribute = typeof(InternalDecisionReferenceValidationController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(Fu16AuthenticationOptions.Scheme, attribute.AuthenticationSchemes);
    }

    [Fact]
    public void Compile_only_program_and_local_registration_do_not_activate_runtime()
    {
        var root = FindRoot();
        var program = File.ReadAllText(Path.Combine(root, "src/Diten.ManagementGovernanceService.Api/Program.cs"));
        var registration = File.ReadAllText(Path.Combine(root,
            "src/Diten.ManagementGovernanceService.Api/DecisionRegistry/DecisionRegistryGateIModuleRegistration.cs"));
        Assert.DoesNotContain("WebApplication.CreateBuilder", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Run(", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IFu16ValidationKeyProvider", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IFu16AuthorityDecisionClient", registration, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !Directory.Exists(Path.Combine(path.FullName, "src"))) path = path.Parent;
        return path?.FullName ?? throw new InvalidOperationException("Service root not found.");
    }
}
