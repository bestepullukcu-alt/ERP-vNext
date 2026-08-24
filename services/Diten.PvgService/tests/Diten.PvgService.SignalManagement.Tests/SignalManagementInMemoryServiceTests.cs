using System.Reflection;
using System.Text.Json;
using Diten.PvgService.Application.SignalManagement;
using Diten.PvgService.Domain.SignalManagement;
using Xunit;

namespace Diten.PvgService.SignalManagement.Tests;

public sealed class SignalManagementInMemoryServiceTests
{
    [Fact]
    public void Create_attach_review_get_and_list_return_metadata_only()
    {
        var service = new InMemorySignalManagementService();

        var create = service.CreateSignalHypothesisContract(ValidCreateCommand());
        var token = create.Contract?.SignalHypothesisReferenceToken;
        var attach = service.AttachMetricDataProductCohortReference(ValidAttachCommand(token!));
        var review = service.MarkReviewDecisionContract(ValidReviewCommand(token!));
        var get = service.GetByIdMetadata(ValidGetByIdQuery(token!));
        var list = service.ListMetadata(ValidListQuery());

        Assert.True(create.IsAllowed);
        Assert.True(attach.IsAllowed);
        Assert.True(review.IsAllowed);
        Assert.True(get.IsAllowed);
        Assert.True(list.IsAllowed);
        Assert.Equal(SignalReviewDecisionStatus.DecisionRecorded, get.Contract?.ReviewDecisionStatus);
        Assert.True(get.Contract?.HasMetricReference);
        Assert.True(get.Contract?.HasThresholdDecisionReference);
        Assert.True(get.Contract?.HasDataProductCohortReference);
        Assert.Single(list.Contracts);
        Assert.Equal("MOD-0234", list.Metadata["module"]);
    }

    [Fact]
    public void Validation_runs_before_create_mutation()
    {
        var service = new InMemorySignalManagementService();
        var command = ValidCreateCommand() with
        {
            IntakeReference = new SignalIntakeReference("", true)
        };

        var result = service.CreateSignalHypothesisContract(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.InvalidRequest, result.ReasonCode);
        Assert.Equal(0, service.StoredContractCount);
    }

    [Theory]
    [MemberData(nameof(DeniedMutationGuardCases))]
    public void Denied_mutation_guards_block_before_state_change(
        Func<CreateSignalHypothesisContractCommand> commandFactory,
        SignalManagementReasonCode expectedReason)
    {
        var service = new InMemorySignalManagementService();

        var result = service.CreateSignalHypothesisContract(commandFactory());

        Assert.False(result.IsAllowed);
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Equal(0, service.StoredContractCount);
    }

    [Fact]
    public void Missing_correlation_blocks_before_state_change()
    {
        var service = new InMemorySignalManagementService();
        var command = ValidCreateCommand() with
        {
            CorrelationContext = new SignalManagementCorrelationContext("")
        };

        var result = service.CreateSignalHypothesisContract(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.MissingCorrelationContext, result.ReasonCode);
        Assert.Equal(0, service.StoredContractCount);
    }

    [Fact]
    public void Missing_upstream_references_block_before_state_change()
    {
        var service = new InMemorySignalManagementService();
        var command = ValidCreateCommand() with
        {
            CodedOutputGuard = SignalManagementGuardDecision.Deny(SignalManagementReasonCode.MissingCodedOutputReference)
        };

        var result = service.CreateSignalHypothesisContract(command);

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.MissingCodedOutputReference, result.ReasonCode);
        Assert.Equal(0, service.StoredContractCount);
    }

    [Fact]
    public void Cross_tenant_read_and_write_return_safe_not_found_without_existence_leak()
    {
        var service = new InMemorySignalManagementService();
        var create = service.CreateSignalHypothesisContract(ValidCreateCommand());
        var token = create.Contract!.SignalHypothesisReferenceToken;

        var otherTenant = new SignalManagementServerTenantContext("other-server-tenant-context");
        var read = service.GetByIdMetadata(ValidGetByIdQuery(token) with { ServerTenantContext = otherTenant });
        var write = service.AttachMetricDataProductCohortReference(ValidAttachCommand(token) with { ServerTenantContext = otherTenant });
        var sameTenantRead = service.GetByIdMetadata(ValidGetByIdQuery(token));

        Assert.False(read.IsAllowed);
        Assert.False(write.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.NotFoundOrUnavailable, read.ReasonCode);
        Assert.Equal(SignalManagementReasonCode.NotFoundOrUnavailable, write.ReasonCode);
        Assert.Null(read.Contract);
        Assert.Null(write.Contract);
        Assert.False(sameTenantRead.Contract?.HasMetricReference);
        Assert.False(sameTenantRead.Contract?.HasThresholdDecisionReference);

        var serialized = JsonSerializer.Serialize(new[] { read, write });
        Assert.DoesNotContain(token, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("other-server-tenant-context", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Result_serialization_does_not_echo_sensitive_or_raw_inputs()
    {
        var service = new InMemorySignalManagementService();
        var command = ValidCreateCommand() with
        {
            IntakeReference = new SignalIntakeReference("intake-sensitive-token", true),
            CaseReference = new SignalMinimumCaseReference("case-sensitive-token", "lifecycle-sensitive-token", true),
            CodedOutputReference = new SignalCodedOutputReference("coded-output-sensitive-token", "dictionary-sensitive-token", true),
            ServerTenantContext = new SignalManagementServerTenantContext("tenant-sensitive-token"),
            ActorContext = new SignalManagementActorContext("actor-sensitive-token", "actor-type-sensitive-token"),
            CorrelationContext = new SignalManagementCorrelationContext("correlation-sensitive-token")
        };

        var created = service.CreateSignalHypothesisContract(command);
        var attached = service.AttachMetricDataProductCohortReference(ValidAttachCommand(created.Contract!.SignalHypothesisReferenceToken) with
        {
            MetricReference = new Mod0004MetricReferenceToken("metric-sensitive-token", "threshold-sensitive-token", true),
            ThresholdDecisionPlaceholderReference = new SignalThresholdDecisionPlaceholderReference(
                "threshold-decision-sensitive-token",
                "threshold-comparison-sensitive-token",
                "insufficient-data-rule-sensitive-token",
                true),
            DataProductCohortReference = new Mod0063DataProductCohortReferenceToken(
                "data-product-sensitive-token",
                "cohort-sensitive-token",
                "lineage-sensitive-token",
                true)
        });
        var serialized = JsonSerializer.Serialize(new[] { created, attached });

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
        Assert.DoesNotContain("metric-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("threshold-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("threshold-decision-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("threshold-comparison-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("insufficient-data-rule-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cohort-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-product-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lineage-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_threshold_decision_placeholder_blocks_before_state_change()
    {
        var service = new InMemorySignalManagementService();
        var create = service.CreateSignalHypothesisContract(ValidCreateCommand());
        var token = create.Contract!.SignalHypothesisReferenceToken;

        var result = service.AttachMetricDataProductCohortReference(ValidAttachCommand(token) with
        {
            ThresholdDecisionPlaceholderReference = new SignalThresholdDecisionPlaceholderReference(
                "",
                "threshold-comparison-reference-token",
                "insufficient-data-rule-reference-token",
                true)
        });
        var sameTenantRead = service.GetByIdMetadata(ValidGetByIdQuery(token));

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.InvalidRequest, result.ReasonCode);
        Assert.False(sameTenantRead.Contract?.HasMetricReference);
        Assert.False(sameTenantRead.Contract?.HasThresholdDecisionReference);
        Assert.False(sameTenantRead.Contract?.HasDataProductCohortReference);
    }

    [Fact]
    public void Denied_threshold_decision_contract_blocks_before_state_change()
    {
        var service = new InMemorySignalManagementService();
        var create = service.CreateSignalHypothesisContract(ValidCreateCommand());
        var token = create.Contract!.SignalHypothesisReferenceToken;

        var result = service.AttachMetricDataProductCohortReference(ValidAttachCommand(token) with
        {
            ThresholdDecisionContractGuard = SignalManagementGuardDecision.Deny(
                SignalManagementReasonCode.ThresholdDecisionContractDenied)
        });
        var sameTenantRead = service.GetByIdMetadata(ValidGetByIdQuery(token));

        Assert.False(result.IsAllowed);
        Assert.Equal(SignalManagementReasonCode.ThresholdDecisionContractDenied, result.ReasonCode);
        Assert.False(sameTenantRead.Contract?.HasMetricReference);
        Assert.False(sameTenantRead.Contract?.HasThresholdDecisionReference);
        Assert.False(sameTenantRead.Contract?.HasDataProductCohortReference);
    }

    [Fact]
    public void Request_contracts_and_service_methods_do_not_accept_client_tenant_id()
    {
        var contractTypes = new[]
        {
            typeof(CreateSignalHypothesisContractCommand),
            typeof(AttachSignalMetricDataProductReferenceCommand),
            typeof(MarkSignalReviewDecisionContractCommand),
            typeof(GetSignalContractMetadataByIdQuery),
            typeof(GetSignalContractMetadataListQuery)
        };

        var properties = contractTypes.SelectMany(type => type.GetProperties()).Select(property => property.Name);
        var parameters = typeof(InMemorySignalManagementService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.Name ?? string.Empty);

        foreach (var name in properties.Concat(parameters))
        {
            Assert.NotEqual("TenantId", name);
            Assert.DoesNotContain("ClientTenant", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Handler_slice_does_not_add_runtime_surface_types_or_forbidden_operations()
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

    public static IEnumerable<object[]> DeniedMutationGuardCases()
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

    private static AttachSignalMetricDataProductReferenceCommand ValidAttachCommand(string signalHypothesisReferenceToken) =>
        new(
            signalHypothesisReferenceToken,
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

    private static MarkSignalReviewDecisionContractCommand ValidReviewCommand(string signalHypothesisReferenceToken) =>
        new(
            signalHypothesisReferenceToken,
            SignalReviewDecisionStatus.DecisionRecorded,
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

    private static GetSignalContractMetadataByIdQuery ValidGetByIdQuery(string signalHypothesisReferenceToken) =>
        new(
            signalHypothesisReferenceToken,
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            SignalManagementGuardDecision.Allow());

    private static GetSignalContractMetadataListQuery ValidListQuery() =>
        new(
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
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
}
