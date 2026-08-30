using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessPerformanceArchitectureTests
{
    [Fact]
    public void Process_performance_Has_exact_empty_persistence_allowlist_and_no_upstream_truth_copy()
    {
        var service = ServiceRoot();
        var source = Directory.GetFiles(Path.Combine(service, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var persistence = Directory.GetFiles(
                Path.Combine(service, "src", "Diten.ManagementGovernanceService.Persistence"),
                "*.cs",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.DoesNotContain(persistence, text => text.Contains("mg_process_kpi_bindings", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(persistence, text => text.Contains("performance_observation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(persistence, text => text.Contains("metric_definition", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(persistence, text => text.Contains("metric_value", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(persistence, text => text.Contains("calculation_contract", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(source, text => text.Contains("class Mod0059", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(source, text => text.Contains("class Mod0060", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(source, text => text.Contains("class Mod0061", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(source, text => text.Contains("MetricFormula", StringComparison.Ordinal));
        Assert.DoesNotContain(source, text => text.Contains("DashboardScorecard", StringComparison.Ordinal));
    }

    [Fact]
    public void Dependency_gate_Has_zero_provider_business_store_and_receipt_side_effect_surface()
    {
        var gate = new ProcessKpiBindingDependencyGate();

        var result = gate.Evaluate(PerformanceImprovementPermissions.KpiRead);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("kpi_reference_contract_unavailable", result.Code);
        Assert.Equal(0, gate.ProviderInvocationCount);
        Assert.Equal(0, gate.BusinessStoreInvocationCount);
        Assert.Equal(0, gate.StoreInvocationCount);
        Assert.Empty(typeof(ProcessKpiBindingDependencyGate).GetConstructors().Single().GetParameters());
    }

    [Fact]
    public void Local_test_controller_Owns_only_exact_routes_and_has_no_workcenter_or_persistence_dependency()
    {
        var controllerPath = Path.Combine(
            ServiceRoot(),
            "src",
            "Diten.ManagementGovernanceService.Api",
            "Controllers",
            "ProcessPerformanceLocalTestController.cs");
        var controller = File.ReadAllText(controllerPath);

        Assert.Contains("internal/local-test/v1/process-modeling", controller, StringComparison.Ordinal);
        Assert.Contains("kpi-bindings", controller, StringComparison.Ordinal);
        Assert.Contains("kpi-bindings/{id:guid}", controller, StringComparison.Ordinal);
        Assert.Contains("kpi-bindings/{id:guid}/archive", controller, StringComparison.Ordinal);
        Assert.Contains("performance-observations", controller, StringComparison.Ordinal);
        Assert.Contains("LocalTestAuthenticationHandler.SchemeName", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowAnonymous", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Mongo", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkCenter", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("MOD-0059", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("MOD-0060", controller, StringComparison.Ordinal);
    }

    private static string ServiceRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !string.Equals(cursor.Name, "Diten.ManagementGovernanceService", StringComparison.Ordinal))
            cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("service_root_missing");
    }
}
