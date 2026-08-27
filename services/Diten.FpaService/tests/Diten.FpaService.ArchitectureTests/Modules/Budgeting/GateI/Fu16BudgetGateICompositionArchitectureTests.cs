using System.Reflection;
using Diten.FpaService.Api.Modules.Budgeting;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;

namespace Diten.FpaService.ArchitectureTests.Modules.Budgeting.GateI;

public sealed class Fu16BudgetGateICompositionArchitectureTests
{
    [Fact]
    public void Controller_uses_exact_named_scheme()
    {
        var attribute = typeof(InternalBudgetVersionValidationController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(Fu16AuthenticationOptions.Scheme, attribute.AuthenticationSchemes);
    }

    [Fact]
    public void Evidence_registration_does_not_activate_host_or_authority_dependencies()
    {
        var root = FindRoot();
        var registration = File.ReadAllText(Path.Combine(root,
            "src/Diten.FpaService.Api/Modules/Budgeting/BudgetingModuleRegistration.cs"));
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
