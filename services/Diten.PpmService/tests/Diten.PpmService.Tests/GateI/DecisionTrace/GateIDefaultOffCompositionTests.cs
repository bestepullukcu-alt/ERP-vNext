using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.GateI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Diten.PpmService.Tests.GateI.DecisionTrace;

public sealed class GateIDefaultOffCompositionTests
{
    [Fact]
    public void All_four_flags_are_absent_or_false_by_default()
    {
        var gate = new GateICompositionGate(new ConfigurationBuilder().Build());
        Assert.False(gate.IsEnabled(GateICompositionLane.DecisionTrace));
        Assert.False(gate.IsEnabled(GateICompositionLane.FundingScenario));
        Assert.False(gate.IsEnabled(GateICompositionLane.BenefitRealization));
    }

    [Fact]
    public void ExcludedV1_approval_required_path_is_503_with_exact_zero_residue()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            [GateICompositionGate.CommonFlag] = "true",
            [GateICompositionGate.DecisionTraceFlag] = "true"
        });
        var result = new GateICompositionPreflight(new GateICompositionGate(configuration))
            .Evaluate(GateICompositionLane.DecisionTrace, requiresExcludedApproval: true);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(GateICompositionPreflight.ExcludedApprovalRequired, result.StableCode);
        Assert.Equal((0, 0, 0, 0, 0),
            (result.ProviderCalls, result.RelationshipWrites, result.ReceiptWrites, result.AuditIntentWrites, result.OutboxWrites));
    }

    [Fact]
    public async Task Decision_provider_registration_is_internal_default_off_and_unavailable()
    {
        var configuration = Configuration(new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var port = provider.GetRequiredService<IDecisionReferenceValidationPort>();
        Assert.NotNull(provider.GetRequiredService<DecisionTraceValidationService>());
        var result = await port.ValidateAsync(null!, null!, CancellationToken.None);
        Assert.Equal(DecisionReferenceProviderResultKind.Unavailable, result.Kind);
    }

    [Fact]
    public void ApprovalOutcome_runtime_types_are_absent_from_composed_assemblies()
    {
        var assemblies = new[]
        {
            typeof(Diten.PpmService.Domain.Entities.InvestmentCase).Assembly,
            typeof(DecisionTraceValidationService).Assembly,
            typeof(GateICompositionGate).Assembly
        };
        Assert.DoesNotContain(assemblies.SelectMany(x => x.GetTypes()),
            type => type.Name.Contains("ApprovalOutcome", StringComparison.Ordinal));
    }

    [Fact]
    public void Both_api_configuration_files_have_only_the_exact_four_false_defaults()
    {
        var root = FindRepositoryRoot();
        foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
                root, "services", "Diten.PpmService", "src", "Diten.PpmService.Api", name)));
            var gateI = document.RootElement.GetProperty("GateI");
            var composition = gateI.GetProperty("Composition");
            Assert.False(composition.GetProperty("Enabled").GetBoolean());
            Assert.False(gateI.GetProperty("DecisionTrace").GetProperty("Enabled").GetBoolean());
            Assert.False(gateI.GetProperty("FundingScenario").GetProperty("Enabled").GetBoolean());
            Assert.False(gateI.GetProperty("BenefitRealization").GetProperty("Enabled").GetBoolean());
            Assert.Equal(4, gateI.EnumerateObject().Count());
            Assert.Single(composition.EnumerateObject());
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static IConfiguration Configuration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
