using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Approval;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.Approval;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.Approval;

public sealed class ApprovalSliceArchitectureTests
{
    [Fact]
    public void Approval_slice_contains_only_authorized_contract_and_test_roots()
    {
        var serviceRoot = ServiceRoot();
        var files = Directory.GetFiles(serviceRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Approval{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(serviceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);
        Assert.All(files, path => Assert.True(
            path.StartsWith("src/Diten.ManagementGovernanceService.Domain/Modules/ProcessModeling/Approval/", StringComparison.Ordinal) ||
            path.StartsWith("src/Diten.ManagementGovernanceService.Application/Modules/ProcessModeling/Approval/", StringComparison.Ordinal) ||
            path.StartsWith("tests/Diten.ManagementGovernanceService.Tests/Modules/ProcessModeling/Approval/", StringComparison.Ordinal) ||
            path.StartsWith("tests/Diten.ManagementGovernanceService.IntegrationTests/Modules/ProcessModeling/Approval/", StringComparison.Ordinal) ||
            path.StartsWith("tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/ProcessModeling/Approval/", StringComparison.Ordinal),
            path));
    }

    [Fact]
    public void Approval_production_contracts_have_no_runtime_persistence_or_external_adapter_dependency()
    {
        var serviceRoot = ServiceRoot();
        var sourceFiles = Directory.GetFiles(Path.Combine(serviceRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Approval{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
        var source = string.Join('\n', sourceFiles.Select(File.ReadAllText));
        foreach (var forbidden in new[]
        {
            "MongoDB", "IMongo", "DbContext", "HttpClient", "RabbitMQ", "MassTransit", "Controller",
            "Endpoint", "IHostedService", "BackgroundService", "IServiceCollection", "ApprovalOutcomeVersion",
            "appsettings", "CAND-CAP"
        })
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contract_shapes_and_non_executable_result_are_compiled_invariants()
    {
        Assert.Equal(8, typeof(PublishActorProvenanceV1).GetProperties(BindingFlags.Instance | BindingFlags.Public).Length);
        Assert.Equal(7, typeof(PublishApprovalPolicyRequestV1).GetProperties(BindingFlags.Instance | BindingFlags.Public).Length);
        Assert.Equal(4, Enum.GetValues<PublishApprovalAuthorityState>().Length);
        Assert.Equal(2, Enum.GetValues<PublishApprovalRequirement>().Length);
        Assert.Equal(3, typeof(ApprovalOutcomeReferenceV1).GetProperties(BindingFlags.Instance | BindingFlags.Public).Length);
        Assert.False(new PublishApprovalAuthorizationResult(503, PublishApprovalFailureCodes.RuntimeUnavailable, true).IsExecutable);
        Assert.True(typeof(IFu16PublishActorProofBoundary).IsInterface);
        Assert.True(typeof(IPublishApprovalPolicyDecisionProvider).IsInterface);
        Assert.True(typeof(IApprovalOutcomeDecisionProvider).IsInterface);
        Assert.Equal(5, Enum.GetValues<AuthoritativeDecisionState>().Length);
        Assert.Equal(9, typeof(ApprovalOutcomeBindingV1).GetProperties(BindingFlags.Instance | BindingFlags.Public).Length);
    }

    [Fact]
    public void External_mutation_evidence_is_hash_bound_expected_red_and_names_compiled_tests()
    {
        var serviceRoot = ServiceRoot();
        var manifestPath = Path.Combine(serviceRoot, "tests", "Diten.ManagementGovernanceService.ArchitectureTests",
            "Modules", "ProcessModeling", "Approval", "approval-mutation-evidence-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.Equal("approval-mutation-evidence-v1", document.RootElement.GetProperty("schemaVersion").GetString());
        var mutations = document.RootElement.GetProperty("mutations").EnumerateArray().ToArray();
        Assert.Equal(5, mutations.Length);
        Assert.Equal(5, mutations.Select(x => x.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());

        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Test configuration not found.");
        foreach (var mutation in mutations)
        {
            Assert.Equal(0, mutation.GetProperty("compileExit").GetInt32());
            Assert.NotEqual(0, mutation.GetProperty("targetedExit").GetInt32());
            Assert.Equal("expected-red", mutation.GetProperty("disposition").GetString());
            Assert.False(string.IsNullOrWhiteSpace(mutation.GetProperty("observedInvariant").GetString()));

            var sourcePath = Path.Combine(serviceRoot, mutation.GetProperty("sourcePath").GetString()!);
            var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
            Assert.Equal(mutation.GetProperty("restoredSha256").GetString(), actualHash);

            var testTypeName = mutation.GetProperty("testType").GetString()!;
            var projectName = testTypeName.Contains(".IntegrationTests.", StringComparison.Ordinal)
                ? "Diten.ManagementGovernanceService.IntegrationTests"
                : testTypeName.Contains(".ArchitectureTests.", StringComparison.Ordinal)
                    ? "Diten.ManagementGovernanceService.ArchitectureTests"
                    : "Diten.ManagementGovernanceService.Tests";
            var assemblyPath = Path.Combine(serviceRoot, "tests", projectName, "bin", configuration, "net8.0", $"{projectName}.dll");
            var assembly = Assembly.LoadFrom(assemblyPath);
            var testType = assembly.GetType(testTypeName, throwOnError: true)!;
            Assert.NotNull(testType.GetMethod(mutation.GetProperty("testMethod").GetString()!, BindingFlags.Public | BindingFlags.Instance));
        }
    }

    [Fact]
    public void No_composition_persistence_api_or_runtime_file_changed_for_approval_slice()
    {
        var serviceRoot = ServiceRoot();
        Assert.DoesNotContain(Directory.GetFiles(serviceRoot, "*Approval*", SearchOption.AllDirectories), path =>
            !path.Contains($"{Path.DirectorySeparatorChar}Approval{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string ServiceRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !string.Equals(cursor.Name, "Diten.ManagementGovernanceService", StringComparison.Ordinal))
            cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("Service root not found.");
    }
}
