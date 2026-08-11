using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.PvgService.Application.MeddraCoding;
using Diten.PvgService.Domain.MeddraCoding;
using Xunit;

namespace Diten.PvgService.MeddraCoding.Tests;

public sealed class MeddraCodingContractTests
{
    [Fact]
    public void Command_and_query_contracts_do_not_accept_client_tenant_id()
    {
        var contractTypes = new[]
        {
            typeof(CreateMeddraCodingWorkItemCommand),
            typeof(ProposeMeddraCodedTermCommand),
            typeof(MarkMeddraCodingReviewedCommand),
            typeof(GetMeddraCodingMetadataByIdQuery),
            typeof(GetMeddraCodingMetadataListQuery)
        };

        foreach (var property in contractTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.NotEqual("TenantId", property.Name);
            Assert.DoesNotContain("ClientTenant", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(DeniedGuardCases))]
    public void Denied_guards_block_with_reason_code(Func<CreateMeddraCodingWorkItemCommand> commandFactory, MeddraCodingReasonCode expectedReason)
    {
        var result = MeddraCodingContractGuard.Evaluate(commandFactory());

        Assert.False(result.IsAllowed);
        Assert.Equal(expectedReason, result.ReasonCode);
    }

    [Fact]
    public void Missing_or_invalid_correlation_blocks()
    {
        var missing = ValidCreateCommand() with { CorrelationContext = new PvgCorrelationContext("") };
        var invalid = ValidCreateCommand() with { CorrelationContext = new PvgCorrelationContext(new string('c', 129)) };

        var missingResult = MeddraCodingContractGuard.Evaluate(missing);
        var invalidResult = MeddraCodingContractGuard.Evaluate(invalid);

        Assert.Equal(MeddraCodingReasonCode.MissingCorrelationContext, missingResult.ReasonCode);
        Assert.Equal(MeddraCodingReasonCode.InvalidCorrelationContext, invalidResult.ReasonCode);
    }

    [Fact]
    public void Missing_codeset_or_dictionary_governance_blocks()
    {
        var command = ValidProposeCommand() with
        {
            DictionaryGovernanceGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.DictionaryGovernanceMissing)
        };

        var result = MeddraCodingContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.DictionaryGovernanceMissing, result.ReasonCode);
    }

    [Fact]
    public void Missing_mod0231_source_term_handoff_blocks()
    {
        var command = ValidCreateCommand() with
        {
            SourceTermHandoffGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.MissingSourceTermHandoff)
        };

        var result = MeddraCodingContractGuard.Evaluate(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.MissingSourceTermHandoff, result.ReasonCode);
    }

    [Fact]
    public void Result_serialization_does_not_echo_sensitive_or_raw_values()
    {
        var command = ValidCreateCommand() with
        {
            SourceTermReference = new Mod0231SourceTermReference(
                "source-ref-sensitive-value",
                "case-ref-sensitive-value",
                "lifecycle-sensitive-value",
                true),
            ServerTenantContext = new PvgServerTenantContext("tenant-sensitive-value"),
            ActorContext = new PvgActorContext("actor-sensitive-value", "role-sensitive-value"),
            CorrelationContext = new PvgCorrelationContext("correlation-sensitive-value"),
            PermissionDecision = new PvgPermissionDecision(false, MeddraCodingReasonCode.PermissionDenied)
        };

        var result = MeddraCodingContractGuard.Evaluate(command);
        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("tenant-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source-ref-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("case-ref-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lifecycle-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actor-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlation-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reporter-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("free-text-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw external", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_delete_bulk_delete_and_dictionary_import_cache_search_contracts_do_not_exist()
    {
        var forbiddenFragments = new[]
        {
            "Export",
            "Delete",
            "BulkDelete",
            "Import",
            "Cache",
            "Search"
        };

        var contractTypes = ContractAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.Contains("MeddraCoding", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(contractTypes, typeName => typeName.Contains(forbiddenFragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Runtime_infrastructure_types_are_not_added()
    {
        var forbiddenFragments = new[]
        {
            "Program",
            "Controller",
            "Endpoint",
            "DbContext",
            "Repository",
            "Mongo",
            "Migration",
            "Seed",
            "Job"
        };

        var typeNames = ContractAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(typeNames, typeName => typeName.Contains(forbiddenFragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Source_and_test_files_do_not_embed_dictionary_sample_codes()
    {
        var serviceRoot = FindServiceRoot();
        var files = Directory
            .EnumerateFiles(serviceRoot, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".csproj", StringComparison.Ordinal))
            .ToArray();

        var dictionaryCodePattern = new Regex(@"\b\d{8}\b", RegexOptions.Compiled);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.False(dictionaryCodePattern.IsMatch(content), $"Dictionary-like code literal found in {file}.");
        }
    }

    [Fact]
    public void Valid_contract_shape_allows_safe_metadata_only()
    {
        var result = MeddraCodingContractGuard.Evaluate(ValidProposeCommand());

        Assert.True(result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.None, result.ReasonCode);
        Assert.Equal("MOD-0232", result.Metadata["module"]);
        Assert.Equal(nameof(MeddraCodingOperation.ProposeCodedTerm), result.Metadata["operation"]);
        Assert.Equal("Allowed", result.Metadata["result"]);
    }

    public static IEnumerable<object[]> DeniedGuardCases()
    {
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                PermissionDecision = new PvgPermissionDecision(false, MeddraCodingReasonCode.PermissionDenied)
            },
            MeddraCodingReasonCode.PermissionDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                FieldPolicyGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.FieldPolicyDenied)
            },
            MeddraCodingReasonCode.FieldPolicyDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                AuditIntentMetadataGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.AuditIntentDenied)
            },
            MeddraCodingReasonCode.AuditIntentDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                DictionaryGovernanceGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.DictionaryGovernanceDenied)
            },
            MeddraCodingReasonCode.DictionaryGovernanceDenied
        };
        yield return new object[]
        {
            () => ValidCreateCommand() with
            {
                SourceTermHandoffGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.SourceTermHandoffDenied)
            },
            MeddraCodingReasonCode.SourceTermHandoffDenied
        };
    }

    private static CreateMeddraCodingWorkItemCommand ValidCreateCommand() =>
        new(
            ValidSourceTermReference(),
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow());

    private static ProposeMeddraCodedTermCommand ValidProposeCommand() =>
        new(
            "coding-work-item-reference",
            ValidCodedTermReference(),
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow());

    private static Mod0231SourceTermReference ValidSourceTermReference() =>
        new("source-term-reference", "case-processing-reference", "lifecycle-state-reference", true);

    private static MeddraCodedTermReference ValidCodedTermReference() =>
        new(
            new MeddraDictionaryVersionReference("dictionary-version-reference", "codeset-version-reference", true),
            "coded-term-reference-token",
            "hierarchy-reference-token");

    private static PvgServerTenantContext ValidServerTenantContext() => new("server-tenant-context-reference");

    private static PvgActorContext ValidActorContext() => new("actor-reference", "actor-type-reference");

    private static PvgPermissionDecision AllowPermission() => new(true, MeddraCodingReasonCode.None);

    private static PvgCorrelationContext ValidCorrelationContext() => new("correlation-reference");

    private static IEnumerable<Assembly> ContractAssemblies()
    {
        yield return typeof(MeddraCodingContractGuard).Assembly;
        yield return typeof(MeddraCodingAssignmentDraft).Assembly;
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
