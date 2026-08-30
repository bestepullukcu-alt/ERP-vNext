using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.GateI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;

public sealed class GateIDefaultOffPersistenceBoundaryTests
{
    [Fact]
    public void Persistence_has_no_ApprovalOutcome_type_or_field()
    {
        var types = typeof(IGateICompositionPersistenceBoundary).Assembly.GetTypes();
        Assert.DoesNotContain(types, type =>
            type.Name.Contains("ApprovalOutcome", StringComparison.Ordinal) ||
            type.GetFields(System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.NonPublic |
                           System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.Static)
                .Any(field => field.Name.Contains("ApprovalOutcome", StringComparison.Ordinal) ||
                              field.FieldType.Name.Contains("ApprovalOutcome", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Default_off_boundary_returns_503_and_has_zero_relationship_receipt_audit_outbox_residue()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:65535",
            ["Mongo:DatabaseName"] = "ppm_gate_i_default_off_no_connection"
        }).Build();
        var services = new ServiceCollection();
        services.AddPpmPersistence(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var boundary = scope.ServiceProvider.GetRequiredService<IGateICompositionPersistenceBoundary>();

        Assert.Equal(503, await boundary.RejectUnavailableAsync());
        Assert.Equal(new GateICompositionResidue(0, 0, 0, 0), boundary.Snapshot());
    }

    [Fact]
    public async Task Default_off_boundary_propagates_cancellation_without_residue()
    {
        var boundary = ResolveBoundary();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await boundary.RejectUnavailableAsync(cancellation.Token));
        Assert.Equal(new GateICompositionResidue(0, 0, 0, 0), boundary.Snapshot());
    }

    private static IGateICompositionPersistenceBoundary ResolveBoundary()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:65535",
            ["Mongo:DatabaseName"] = "ppm_gate_i_default_off_no_connection"
        }).Build();
        var services = new ServiceCollection();
        services.AddPpmPersistence(configuration);
        return services.BuildServiceProvider().GetRequiredService<IGateICompositionPersistenceBoundary>();
    }
}
