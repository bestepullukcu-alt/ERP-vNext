using System.Reflection;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Xunit;

namespace Diten.PvgService.RegPvBase.Tests;

public sealed class DenyAllRegPvBasePortTests
{
    private static readonly string[] SensitiveSamples =
    [
        "tenant-123",
        "actor-456",
        "case-789",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "queue-safety-review",
        "evidence-ref-001"
    ];

    [Fact]
    public async Task FieldSecurityPolicy_denies_by_default_without_echoing_sensitive_inputs()
    {
        var request = new PvgFieldSecurityRequest(
            PvgIntakeOperation.Create,
            "detail",
            "AdverseEventNarrative",
            "tenant-123",
            "actor-456",
            "patient-subject-code",
            "free text narrative with PHI");

        var decision = await new DenyAllFieldSecurityPolicy().EvaluateAsync(request);

        AssertDeniedSafely(decision, PvgSafeReasonCodes.FieldSecurityPolicyUnavailable);
    }

    [Fact]
    public async Task WorkflowTransitionGate_denies_by_default_without_echoing_sensitive_inputs()
    {
        var request = new PvgWorkflowTransitionRequest(
            PvgIntakeOperation.Route,
            "tenant-123",
            "case-789",
            "actor-456",
            "Triaged",
            "Routed",
            "queue-safety-review",
            "free text narrative with PHI");

        var decision = await new DenyAllWorkflowTransitionGate().EvaluateAsync(request);

        AssertDeniedSafely(decision, PvgSafeReasonCodes.WorkflowTransitionGateUnavailable);
    }

    [Fact]
    public async Task EvidenceLinkPort_denies_by_default_without_echoing_sensitive_inputs()
    {
        var request = new PvgEvidenceLinkRequest(
            PvgIntakeOperation.Triage,
            "tenant-123",
            "case-789",
            "actor-456",
            "evidence-ref-001",
            "free text narrative with PHI");

        var decision = await new DenyAllEvidenceLinkPort().EvaluateAsync(request);

        AssertDeniedSafely(decision, PvgSafeReasonCodes.EvidenceLinkUnavailable);
    }

    [Fact]
    public void DenyAllAdapters_are_stateless_and_do_not_reference_persistence_or_network_libraries()
    {
        var adapterTypes = new[]
        {
            typeof(DenyAllFieldSecurityPolicy),
            typeof(DenyAllWorkflowTransitionGate),
            typeof(DenyAllEvidenceLinkPort)
        };

        foreach (var adapterType in adapterTypes)
        {
            Assert.Empty(adapterType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.Empty(adapterType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        var referencedAssemblyNames = typeof(DenyAllFieldSecurityPolicy)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("MongoDB.Driver", referencedAssemblyNames);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencedAssemblyNames);
        Assert.DoesNotContain("System.Net.Http", referencedAssemblyNames);
        Assert.DoesNotContain("System.Net.Sockets", referencedAssemblyNames);
    }

    [Fact]
    public void Slice1_operations_exclude_archive_void_export_delete_and_bulk_delete()
    {
        var operationNames = Enum.GetNames<PvgIntakeOperation>();

        Assert.DoesNotContain("Archive", operationNames);
        Assert.DoesNotContain("Void", operationNames);
        Assert.DoesNotContain("Export", operationNames);
        Assert.DoesNotContain("Delete", operationNames);
        Assert.DoesNotContain("BulkDelete", operationNames);
    }

    private static void AssertDeniedSafely(PvgPortDecision decision, string expectedReasonCode)
    {
        Assert.False(decision.IsAllowed);
        Assert.False(decision.IsSatisfied);
        Assert.Equal(expectedReasonCode, decision.ReasonCode);

        var renderedDecision = decision.ToString();
        foreach (var sensitiveSample in SensitiveSamples)
        {
            Assert.DoesNotContain(sensitiveSample, renderedDecision, StringComparison.OrdinalIgnoreCase);
        }
    }
}
