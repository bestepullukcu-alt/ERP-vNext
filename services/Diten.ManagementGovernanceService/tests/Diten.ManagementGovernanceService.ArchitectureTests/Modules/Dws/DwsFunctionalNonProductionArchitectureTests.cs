using Diten.ManagementGovernanceService.Api;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Infrastructure;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsFunctionalNonProductionArchitectureTests
{
    [Fact]
    public async Task Functional_host_is_default_off_and_local_test_is_loopback_5017_only()
    {
        var execution = Program.Main([]);
        await execution.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(execution.IsCompletedSuccessfully);

        var app = Program.BuildLocalTestApp("mongodb://127.0.0.1:65535", "functional_configuration_only");
        try
        {
            Assert.Equal("http://127.0.0.1:5017", app.Configuration[WebHostDefaults.ServerUrlsKey]);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public void Production_composition_does_not_activate_functional_providers_or_local_host()
    {
        var services = new ServiceCollection();
        services.AddDwsInfrastructure();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IFu16DwsFunctionalAuthorization));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMod0117DwsContextValidator));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDwsFunctionalCommandPort));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDwsFunctionalQueryPort));
        Assert.False(DwsInfrastructureBoundary.RuntimeActivationEnabled);
    }

    [Fact]
    public void Self_registration_is_contract_only_and_audit_path_is_non_deliverable()
    {
        Assert.Equal("MOD-0354", DwsSelfRegistration.Contract.ModuleCode);
        Assert.Equal(6, DwsSelfRegistration.Contract.Permissions.Count);
        Assert.Equal(["get_Contract"], typeof(DwsSelfRegistration).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name));

        foreach (var source in FunctionalSources().Select(File.ReadAllText))
        {
            Assert.DoesNotContain("IEventBus", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PublishAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MassTransit", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RabbitMQ", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("audit_events", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<string> FunctionalSources()
    {
        var root = FindRoot();
        return Directory.GetFiles(
                Path.Combine(root, "services/Diten.ManagementGovernanceService/src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => path.Contains("/Features/Dws/Handlers/", StringComparison.Ordinal)
                || path.Contains("/Infrastructure/Modules/Dws/DwsFunctional", StringComparison.Ordinal)
                || path.Contains("/Persistence/Modules/Dws/DwsFunctional", StringComparison.Ordinal)
                || path.EndsWith("/DwsStructuresController.cs", StringComparison.Ordinal))
            .ToArray();
    }

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
