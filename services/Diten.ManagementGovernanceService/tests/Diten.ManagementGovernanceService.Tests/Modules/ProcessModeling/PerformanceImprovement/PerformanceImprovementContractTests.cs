using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.PerformanceImprovement;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementContractTests
{
    private static readonly DateTime Now = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Opportunity_fields_are_exact_normalized_and_forward_only()
    {
        var x = NewOpportunity("  Résumé  ", "  measurable problem  ", "  better flow  ", 2);

        Assert.Equal("Résumé", x.Title);
        Assert.Equal("measurable problem", x.ProblemStatement);
        Assert.Equal("better flow", x.ExpectedOutcome);
        Assert.Equal(ProcessImprovementOpportunity.State.Open, x.LifecycleState);

        x.Qualify(0, Now.AddMinutes(1));
        Assert.Equal(ProcessImprovementOpportunity.State.Qualified, x.LifecycleState);
        x.RegisterOutcome(1, Now.AddMinutes(2));
        x.Close(1, 2, Now.AddMinutes(3));
        Assert.Equal(ProcessImprovementOpportunity.State.Closed, x.LifecycleState);
        Assert.Throws<InvalidOperationException>(() => x.Update("x", "y", "z", 1, 3, Now.AddMinutes(4)));
    }

    [Fact]
    public void Qualify_requires_expected_outcome_and_positive_priority()
    {
        var missingOutcome = NewOpportunity("title", "problem", null, 1);
        Assert.Throws<InvalidOperationException>(() => missingOutcome.Qualify(0, Now.AddMinutes(1)));
        var missingPriority = NewOpportunity("title", "problem", "expected", null);
        Assert.Throws<InvalidOperationException>(() => missingPriority.Qualify(0, Now.AddMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NewOpportunity("title", "problem", "expected", 0));
    }

    [Fact]
    public void Close_requires_outcome_and_expected_version()
    {
        var x = NewOpportunity("title", "problem", "expected", 1);
        x.Qualify(0, Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => x.Close(0, 1, Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => x.Close(1, 0, Now.AddMinutes(2)));
    }

    [Fact]
    public void Outcome_is_immutable_and_observed_time_is_server_utc()
    {
        var id = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var x = new ProcessImprovementOutcome(id, tenant, Now, owner, "  observed result  ", Now);
        Assert.Equal("observed result", x.Summary);
        Assert.Equal(Now, x.ObservedAtUtc);
        Assert.DoesNotContain(typeof(ProcessImprovementOutcome).GetProperties(), p => p.SetMethod?.IsPublic == true);
        Assert.Throws<ArgumentException>(() => new ProcessImprovementOutcome(id, tenant, Now, owner, "x", DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void Additive_permission_map_is_exact_ordinal_nine_by_nine()
    {
        Assert.Equal(9, PerformanceImprovementPermissions.ExactOperationMap.Count);
        Assert.All(PerformanceImprovementPermissions.ExactOperationMap, x => Assert.Equal(x.Key, x.Value));
        Assert.Equal(9, PerformanceImprovementPermissions.ExactOperationMap.Keys.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(PerformanceImprovementPermissions.ExactOperationMap.Keys, x => x.Contains('*') || x != x.Trim());
    }

    [Theory]
    [InlineData(PerformanceImprovementPermissions.KpiRead)]
    [InlineData(PerformanceImprovementPermissions.KpiCreate)]
    [InlineData(PerformanceImprovementPermissions.KpiArchive)]
    public void Kpi_dependency_gate_is_exact_503_with_zero_store_invocation(string operation)
    {
        var gate = new ProcessKpiBindingDependencyGate();
        var result = gate.Evaluate(operation, Guid.NewGuid());
        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("kpi_reference_contract_unavailable", result.Code);
        Assert.Equal(0, gate.StoreInvocationCount);
    }

    [Fact]
    public async Task Coordinator_enforces_exact_permission_and_never_normalizes()
    {
        var store = new FakeStore();
        var coordinator = new PerformanceImprovementCoordinator(store);
        var valid = Request(PerformanceImprovementContract.Command.CreateOpportunity, PerformanceImprovementPermissions.ImprovementCreate);
        Assert.True((await coordinator.ExecuteAsync(valid)).Success);
        Assert.Equal(1, store.Calls);

        var alias = valid with { Permission = valid.Permission.ToUpperInvariant(), IdempotencyKey = "second" };
        var denied = await coordinator.ExecuteAsync(alias);
        Assert.Equal(403, denied.StatusCode);
        Assert.Equal(1, store.Calls);
    }

    private static ProcessImprovementOpportunity NewOpportunity(string title, string problem, string? expected, int? priority) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Now, Guid.NewGuid(), title, problem, expected, priority);

    private static PerformanceImprovementContract.MutationRequest Request(PerformanceImprovementContract.Command command, string permission) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, command, permission, "key", new string('a', 64), 0,
            "title", "problem", "expected", 1, null, Now);

    private sealed class FakeStore : PerformanceImprovementContract.IMutationStore
    {
        public int Calls { get; private set; }
        public Task<PerformanceImprovementContract.MutationResult> ExecuteAsync(PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new PerformanceImprovementContract.MutationResult(true, 200, "accepted", request.AggregateId));
        }
    }
}
