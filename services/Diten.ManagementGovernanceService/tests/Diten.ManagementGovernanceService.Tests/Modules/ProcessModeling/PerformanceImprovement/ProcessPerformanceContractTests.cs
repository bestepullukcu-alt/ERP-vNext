using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessPerformanceContractTests
{
    public static TheoryData<string> ExactOperations => new()
    {
        PerformanceImprovementPermissions.KpiRead,
        PerformanceImprovementPermissions.KpiCreate,
        PerformanceImprovementPermissions.KpiArchive
    };

    [Theory]
    [MemberData(nameof(ExactOperations))]
    public void Dependency_gate_When_upstream_contract_is_absent_Returns_exact_503_without_store_invocation(string operation)
    {
        var requestId = Guid.NewGuid();
        var gate = new ProcessKpiBindingDependencyGate();

        var result = gate.Evaluate(operation, requestId);

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("kpi_reference_contract_unavailable", result.Code);
        Assert.Equal(requestId, result.AggregateId);
        Assert.Equal(0, gate.StoreInvocationCount);
    }

    [Fact]
    public void Dependency_gate_When_operation_is_alias_Rejects_without_normalizing_or_invoking_store()
    {
        var gate = new ProcessKpiBindingDependencyGate();

        var act = () => gate.Evaluate(PerformanceImprovementPermissions.KpiRead.ToUpperInvariant());

        var error = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Contains("kpi_operation_forbidden", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, gate.ProviderInvocationCount);
        Assert.Equal(0, gate.BusinessStoreInvocationCount);
        Assert.Equal(0, gate.StoreInvocationCount);
    }

    [Fact]
    public void Dependency_result_Serialization_is_exact_camel_case_and_contains_no_upstream_payload()
    {
        var result = new ProcessKpiBindingDependencyGate().Evaluate(
            PerformanceImprovementPermissions.KpiRead,
            Guid.Parse("11111111-1111-4111-8111-111111111111"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal(new[] { "success", "statusCode", "code", "aggregateId" },
            json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("kpi_reference_contract_unavailable", json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(json.RootElement.EnumerateObject(), property =>
            property.Name.Contains("metric", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("calculation", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("scorecard", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("dashboard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unavailable_contract_Serialization_is_exact_and_has_no_success_payload()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            ProcessPerformanceUnavailableContract.Result.DependencyUnavailable,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal(new[] { "statusCode", "code", "availability" },
            json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(503, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("kpi_reference_contract_unavailable", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("unavailable", json.RootElement.GetProperty("availability").GetString());
    }

    [Fact]
    public void Kpi_operation_inventory_Is_exact_ordinal_three_without_core_permission_reuse()
    {
        var expected = new[]
        {
            PerformanceImprovementPermissions.KpiRead,
            PerformanceImprovementPermissions.KpiCreate,
            PerformanceImprovementPermissions.KpiArchive
        };
        var actual = PerformanceImprovementPermissions.ExactOperationMap
            .Where(pair => pair.Key.Contains(".kpi-bindings.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(expected, actual.Select(pair => pair.Key));
        Assert.All(actual, pair => Assert.Equal(pair.Key, pair.Value));
        Assert.Equal(expected.Length, actual.Select(pair => pair.Key).Distinct(StringComparer.Ordinal).Count());
    }
}
