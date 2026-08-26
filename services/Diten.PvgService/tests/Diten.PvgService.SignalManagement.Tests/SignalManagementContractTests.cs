using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.PvgService.Application.SignalManagement;
using Diten.PvgService.Domain.SignalManagement;
using Xunit;

namespace Diten.PvgService.SignalManagement.Tests;

public sealed class SignalManagementContractTests
{
    [Fact]
    public void Command_and_query_contracts_do_not_accept_client_tenant_id()
    {
        var contractTypes = new[]
        {
            typeof(CreateSignalHypothesisContractCommand),
            typeof(AttachSignalMetricDataProductReferenceCommand),
            typeof(MarkSignalReviewDecisionContractCommand),
            typeof(GetSignalContractMetadataByIdQuery),
            typeof(GetSignalContractMetadataListQuery)
        };

        foreach (var property in contractTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.NotEqual("TenantId", property.Name);
            Assert.DoesNotContain("ClientTenant", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(DeniedGuardCases))]
    public void Denied_guards_block_with_reason_code(
        Func<CreateSignalHypothesisContractCommand> commandFactory,
        SignalManagementReasonCode expectedReason)
    {
        var result = SignalManagementContractGuard.Evaluate(commandFactory());

        Assert.False(result.IsAllowed);
        Assert.Equal(expectedReason, result.ReasonCode);
    }

    [Fact]
    public void Missing_or_invalid_correlation_blocks()
    {
        var missing = ValidCreateCommand() with { CorrelationContext = new SignalManagementCorrelationContext("") };
        var invalid = ValidCreateCommand() with { CorrelationContext = new SignalManagementCorrelationContext(new string('c', 129)) };

        var missingResult = SignalManagementContractGuard.Evaluate(missing);
        var invalidResult = SignalManagementContractGuard.Evaluate(invalid);

        Assert.Equal(SignalManagementReasonCode.MissingCorrelationContext, missingResult.ReasonCode);
        Assert.Equal(SignalManagementReasonCode.InvalidCorrelationContext, invalidResult.ReasonCode);
    }

    [Theory]
    [MemberData(nameof(MissingUpstreamReferenceCases))]
    public void Missing_upstream_references_block(
        CreateSignalHypothesisContractCommand command,
        SignalManagementReasonCode expectedReason)
    {
        var result = SignalManagementContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(expectedReason, result.ReasonCode);
    }

    [Fact]
    public void Missing_metric_contract_guard_blocks()
    {
        var command = ValidCreateCommand() with
        {
            MetricContractGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.MetricContractMissing)
        };

        var result = SignalManagementContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.MetricContractMissing, result.ReasonCode);
    }

    [Fact]
    public void Missing_data_product_contract_guard_blocks()
    {
        var command = ValidCreateCommand() with
        {
            DataProductContractGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.DataProductContractMissing)
        };

        var result = SignalManagementContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.DataProductContractMissing, result.ReasonCode);
    }

    [Fact]
    public void Attach_metric_data_product_contract_requires_reference_tokens()
    {
        var command = ValidAttachCommand() with
        {
            MetricReference = new Mod0004MetricReferenceToken("", "threshold-reference-token", true)
        };

        var result = SignalManagementContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.InvalidRequest, result.ReasonCode);
    }

    [Fact]
    public void Missing_threshold_decision_contract_guard_blocks()
    {
        var command = ValidAttachCommand() with
        {
            ThresholdDecisionContractGuard = SignalManagementGuardDecision.Deny(
                SignalManagementReasonCode.ThresholdDecisionContractMissing)
        };

        var result = SignalManagementContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.ThresholdDecisionContractMissing, result.ReasonCode);
    }

    [Fact]
    public void Attach_metric_data_product_contract_requires_threshold_decision_placeholder()
    {
        var command = ValidAttachCommand() with
        {
            ThresholdDecisionPlaceholderReference = new SignalThresholdDecisionPlaceholderReference(
                "",
                "threshold-comparison-reference-token",
                "insufficient-data-rule-reference-token",
                true)
        };

        var result = SignalManagementContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.InvalidRequest, result.ReasonCode);
    }

    [Fact]
    public void Result_serialization_does_not_echo_sensitive_or_raw_values()
    {
        var command = ValidCreateCommand() with
        {
            IntakeReference = new SignalIntakeReference("intake-sensitive-token", true),
            CaseReference = new SignalMinimumCaseReference("case-sensitive-token", "lifecycle-sensitive-token", true),
            CodedOutputReference = new SignalCodedOutputReference("coded-output-sensitive-token", "dictionary-sensitive-token", true),
            ServerTenantContext = new SignalManagementServerTenantContext("tenant-sensitive-token"),
            ActorContext = new SignalManagementActorContext("actor-sensitive-token", "actor-type-sensitive-token"),
            CorrelationContext = new SignalManagementCorrelationContext("correlation-sensitive-token"),
            PermissionDecision = new SignalManagementPermissionDecision(false, SignalManagementReasonCode.PermissionDenied)
        };

        var result = SignalManagementContractGuard.Evaluate(command);
        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("tenant-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("intake-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("case-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lifecycle-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coded-output-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dictionary-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actor-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actor-type-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlation-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reporter-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("free-text-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw source", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Valid_contract_shape_returns_safe_metadata_only()
    {
        var result = SignalManagementContractGuard.Evaluate(ValidAttachCommand());

        Assert.True(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.None, result.ReasonCode);
        Assert.Equal("MOD-0234", result.Metadata["module"]);
        Assert.Equal(nameof(SignalManagementOperation.AttachSignalMetricDataProductReference), result.Metadata["operation"]);
        Assert.Equal("Allowed", result.Metadata["result"]);
    }

    [Fact]
    public void Runtime_shell_endpoint_dashboard_persistence_and_forbidden_operation_types_do_not_exist()
    {
        var forbiddenFragments = new[]
        {
            "Program",
            "Controller",
            "Endpoint",
            "Dashboard",
            "Menu",
            "Shell",
            "DbContext",
            "Repository",
            "Mongo",
            "Migration",
            "Seed",
            "Job",
            "Export",
            "Delete",
            "BulkDelete"
        };

        var typeNames = ContractAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.Contains("SignalManagement", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(typeNames, typeName => typeName.Contains(forbiddenFragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Fake_signal_metric_cohort_and_data_product_stub_contracts_do_not_exist()
    {
        var forbiddenFragments = new[]
        {
            "FakeSignal",
            "FakeMetric",
            "FakeCohort",
            "DataProductStub",
            "SampleSignal",
            "SampleMetric",
            "SampleCohort"
        };

        var typeNames = ContractAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.Contains("SignalManagement", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(typeNames, typeName => typeName.Contains(forbiddenFragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Source_and_test_files_do_not_embed_sample_values_or_invented_external_ids()
    {
        var serviceRoot = FindServiceRoot();
        var files = Directory
            .EnumerateFiles(serviceRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
            .ToArray();

        var forbiddenFragments = new[]
        {
            "MOD-0004-" + "[A-Za-z0-9-]+",
            "MOD-0063-" + "[A-Za-z0-9-]+",
            "threshold-" + "value",
            "cohort-" + "value",
            "aggregate-" + "value",
            "metric-" + "value"
        };
        var forbiddenPattern = new Regex(string.Join("|", forbiddenFragments), RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.False(forbiddenPattern.IsMatch(content), $"Forbidden sample value or invented ID found in {file}.");
        }
    }

    public static IEnumerable<object[]> DeniedGuardCases()
    {
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                PermissionDecision = new SignalManagementPermissionDecision(false, SignalManagementReasonCode.PermissionDenied)
            },
            SignalManagementReasonCode.PermissionDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                FieldPolicyGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.FieldPolicyDenied)
            },
            SignalManagementReasonCode.FieldPolicyDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                EvidenceGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.EvidenceDenied)
            },
            SignalManagementReasonCode.EvidenceDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                AuditIntentMetadataGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.AuditIntentDenied)
            },
            SignalManagementReasonCode.AuditIntentDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                MetricContractGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.MetricContractDenied)
            },
            SignalManagementReasonCode.MetricContractDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                DataProductContractGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.DataProductContractDenied)
            },
            SignalManagementReasonCode.DataProductContractDenied
        };
    }

    public static IEnumerable<object[]> MissingUpstreamReferenceCases()
    {
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                IntakeGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.MissingIntakeReference)
            },
            SignalManagementReasonCode.MissingIntakeReference
        };
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                CaseGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.MissingCaseReference)
            },
            SignalManagementReasonCode.MissingCaseReference
        };
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                CodedOutputGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.MissingCodedOutputReference)
            },
            SignalManagementReasonCode.MissingCodedOutputReference
        };
    }

    private static CreateSignalHypothesisContractCommand ValidCreateCommand() =>
        new(
            ValidIntakeReference(),
            ValidCaseReference(),
            ValidCodedOutputReference(),
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow());

    private static AttachSignalMetricDataProductReferenceCommand ValidAttachCommand() =>
        new(
            "signal-hypothesis-reference-token",
            ValidMetricReference(),
            ValidThresholdDecisionPlaceholderReference(),
            ValidDataProductReference(),
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow(),
            SignalManagementGuardDecision.Allow());

    private static SignalIntakeReference ValidIntakeReference() =>
        new("intake-reference-token", true);

    private static SignalMinimumCaseReference ValidCaseReference() =>
        new("case-reference-token", "lifecycle-reference-token", true);

    private static SignalCodedOutputReference ValidCodedOutputReference() =>
        new("coded-output-reference-token", "dictionary-reference-token", true);

    private static Mod0004MetricReferenceToken ValidMetricReference() =>
        new("metric-reference-token", "threshold-reference-token", true);

    private static SignalThresholdDecisionPlaceholderReference ValidThresholdDecisionPlaceholderReference() =>
        new(
            "threshold-decision-reference-token",
            "threshold-comparison-reference-token",
            "insufficient-data-rule-reference-token",
            true);

    private static Mod0063DataProductCohortReferenceToken ValidDataProductReference() =>
        new("data-product-reference-token", "cohort-reference-token", "lineage-reference-token", true);

    private static SignalManagementServerTenantContext ValidServerTenantContext() =>
        new("server-tenant-context-reference");

    private static SignalManagementActorContext ValidActorContext() =>
        new("actor-reference", "actor-type-reference");

    private static SignalManagementPermissionDecision AllowPermission() =>
        new(true, SignalManagementReasonCode.None);

    private static SignalManagementCorrelationContext ValidCorrelationContext() =>
        new("correlation-reference");

    private static IEnumerable<Assembly> ContractAssemblies()
    {
        yield return typeof(SignalManagementContractGuard).Assembly;
        yield return typeof(SignalHypothesisReference).Assembly;
    }

    private static string FindServiceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "services", "Diten.PvgService");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate services/Diten.PvgService.");
    }
}
