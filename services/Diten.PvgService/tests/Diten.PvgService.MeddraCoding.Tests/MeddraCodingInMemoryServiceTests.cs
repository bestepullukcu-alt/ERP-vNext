using System.Text.Json;
using Diten.PvgService.Application.MeddraCoding;
using Diten.PvgService.Domain.MeddraCoding;
using Xunit;

namespace Diten.PvgService.MeddraCoding.Tests;

public sealed class MeddraCodingInMemoryServiceTests
{
    [Fact]
    public void Validation_runs_before_create_mutation()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var command = ValidCreateCommand() with
        {
            SourceTermReference = new Mod0231SourceTermReference("", "case-processing-reference", "lifecycle-state-reference", true)
        };

        var result = service.CreateCodingWorkItem(command);

        Assert.False(result.Result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.InvalidRequest, result.Result.ReasonCode);
        Assert.Equal(0, service.StoredItemCount);
    }

    [Fact]
    public void Validation_runs_before_propose_mutation()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var createResult = service.CreateCodingWorkItem(ValidCreateCommand());
        var workItemReference = createResult.Records.Single().CodingWorkItemReference;
        var command = ValidProposeCommand(workItemReference) with
        {
            ProposedTerm = new MeddraCodedTermReference(
                new MeddraDictionaryVersionReference("dictionary-version-reference", "codeset-version-reference", false),
                "coded-term-reference-token",
                "hierarchy-reference-token")
        };

        var result = service.ProposeCodedTerm(command);
        var stored = service.GetByIdMetadata(ValidGetByIdQuery(workItemReference));

        Assert.False(result.Result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.InvalidRequest, result.Result.ReasonCode);
        Assert.Equal(MeddraCodingReviewStatus.Draft, stored.Records.Single().ReviewStatus);
        Assert.False(stored.Records.Single().HasProposedTerm);
    }

    [Theory]
    [MemberData(nameof(DeniedCreateGuardCases))]
    public void Denied_create_guards_block_mutation(CreateMeddraCodingWorkItemCommand command, MeddraCodingReasonCode expectedReason)
    {
        var service = new InMemoryMeddraCodingApplicationService();

        var result = service.CreateCodingWorkItem(command);

        Assert.False(result.Result.IsAllowed);
        Assert.Equal(expectedReason, result.Result.ReasonCode);
        Assert.Equal(0, service.StoredItemCount);
    }

    [Fact]
    public void Missing_or_invalid_correlation_blocks_mutation()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var missing = ValidCreateCommand() with { CorrelationContext = new PvgCorrelationContext("") };
        var invalid = ValidCreateCommand() with { CorrelationContext = new PvgCorrelationContext(new string('c', 129)) };

        var missingResult = service.CreateCodingWorkItem(missing);
        var invalidResult = service.CreateCodingWorkItem(invalid);

        Assert.Equal(MeddraCodingReasonCode.MissingCorrelationContext, missingResult.Result.ReasonCode);
        Assert.Equal(MeddraCodingReasonCode.InvalidCorrelationContext, invalidResult.Result.ReasonCode);
        Assert.Equal(0, service.StoredItemCount);
    }

    [Fact]
    public void Missing_codeset_or_dictionary_governance_blocks_mutation()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var command = ValidCreateCommand() with
        {
            DictionaryGovernanceGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.DictionaryGovernanceMissing)
        };

        var result = service.CreateCodingWorkItem(command);

        Assert.False(result.Result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.DictionaryGovernanceMissing, result.Result.ReasonCode);
        Assert.Equal(0, service.StoredItemCount);
    }

    [Fact]
    public void Missing_mod0231_handoff_blocks_create()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var command = ValidCreateCommand() with
        {
            SourceTermHandoffGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.MissingSourceTermHandoff)
        };

        var result = service.CreateCodingWorkItem(command);

        Assert.False(result.Result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.MissingSourceTermHandoff, result.Result.ReasonCode);
        Assert.Equal(0, service.StoredItemCount);
    }

    [Fact]
    public void Full_in_memory_flow_returns_metadata_only()
    {
        var service = new InMemoryMeddraCodingApplicationService();

        var createResult = service.CreateCodingWorkItem(ValidCreateCommand());
        var workItemReference = createResult.Records.Single().CodingWorkItemReference;
        var proposeResult = service.ProposeCodedTerm(ValidProposeCommand(workItemReference));
        var reviewResult = service.MarkCodingReviewed(ValidReviewedCommand(workItemReference));
        var getResult = service.GetByIdMetadata(ValidGetByIdQuery(workItemReference));
        var listResult = service.ListMetadata(ValidListQuery());

        Assert.True(createResult.Result.IsAllowed);
        Assert.True(proposeResult.Result.IsAllowed);
        Assert.True(reviewResult.Result.IsAllowed);
        Assert.True(getResult.Result.IsAllowed);
        Assert.True(listResult.Result.IsAllowed);
        Assert.Equal(MeddraCodingReviewStatus.Reviewed, getResult.Records.Single().ReviewStatus);
        Assert.True(getResult.Records.Single().HasProposedTerm);
        Assert.Single(listResult.Records);
    }

    [Fact]
    public void Cross_tenant_read_and_write_do_not_leak_existence()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var createResult = service.CreateCodingWorkItem(ValidCreateCommand());
        var workItemReference = createResult.Records.Single().CodingWorkItemReference;
        var otherTenantContext = new PvgServerTenantContext("other-server-tenant-context-reference");

        var crossTenantRead = service.GetByIdMetadata(ValidGetByIdQuery(workItemReference) with
        {
            ServerTenantContext = otherTenantContext
        });
        var missingRead = service.GetByIdMetadata(ValidGetByIdQuery("missing-work-item-reference") with
        {
            ServerTenantContext = otherTenantContext
        });
        var crossTenantWrite = service.ProposeCodedTerm(ValidProposeCommand(workItemReference) with
        {
            ServerTenantContext = otherTenantContext
        });

        Assert.False(crossTenantRead.Result.IsAllowed);
        Assert.False(missingRead.Result.IsAllowed);
        Assert.False(crossTenantWrite.Result.IsAllowed);
        Assert.Equal(MeddraCodingReasonCode.NotFound, crossTenantRead.Result.ReasonCode);
        Assert.Equal(MeddraCodingReasonCode.NotFound, missingRead.Result.ReasonCode);
        Assert.Equal(MeddraCodingReasonCode.NotFound, crossTenantWrite.Result.ReasonCode);
        Assert.Empty(crossTenantRead.Records);
        Assert.Empty(missingRead.Records);
        Assert.Empty(crossTenantWrite.Records);
    }

    [Fact]
    public void Blocked_result_serialization_does_not_echo_sensitive_or_raw_values()
    {
        var service = new InMemoryMeddraCodingApplicationService();
        var command = ValidCreateCommand() with
        {
            SourceTermReference = new Mod0231SourceTermReference(
                "raw-source-reference-sensitive-value",
                "raw-case-reference-sensitive-value",
                "raw-lifecycle-reference-sensitive-value",
                true),
            ServerTenantContext = new PvgServerTenantContext("tenant-sensitive-value"),
            ActorContext = new PvgActorContext("actor-sensitive-value", "actor-type-sensitive-value"),
            CorrelationContext = new PvgCorrelationContext("correlation-sensitive-value"),
            PermissionDecision = new PvgPermissionDecision(false, MeddraCodingReasonCode.PermissionDenied)
        };

        var result = service.CreateCodingWorkItem(command);
        var serialized = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("tenant-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-source-reference-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-case-reference-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-lifecycle-reference-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actor-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actor-type-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlation-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reporter-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("free-text-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw external", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dictionary-sensitive-value", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coded-term-sensitive-token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", serialized, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> DeniedCreateGuardCases()
    {
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                PermissionDecision = new PvgPermissionDecision(false, MeddraCodingReasonCode.PermissionDenied)
            },
            MeddraCodingReasonCode.PermissionDenied
        };
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                FieldPolicyGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.FieldPolicyDenied)
            },
            MeddraCodingReasonCode.FieldPolicyDenied
        };
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                AuditIntentMetadataGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.AuditIntentDenied)
            },
            MeddraCodingReasonCode.AuditIntentDenied
        };
        yield return new object[]
        {
            ValidCreateCommand() with
            {
                DictionaryGovernanceGuard = PvgGuardDecision.Deny(MeddraCodingReasonCode.DictionaryGovernanceDenied)
            },
            MeddraCodingReasonCode.DictionaryGovernanceDenied
        };
        yield return new object[]
        {
            ValidCreateCommand() with
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

    private static ProposeMeddraCodedTermCommand ValidProposeCommand(string workItemReference) =>
        new(
            workItemReference,
            ValidCodedTermReference(),
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow());

    private static MarkMeddraCodingReviewedCommand ValidReviewedCommand(string workItemReference) =>
        new(
            workItemReference,
            MeddraCodingReviewStatus.Reviewed,
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow(),
            PvgGuardDecision.Allow());

    private static GetMeddraCodingMetadataByIdQuery ValidGetByIdQuery(string workItemReference) =>
        new(
            workItemReference,
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
            PvgGuardDecision.Allow());

    private static GetMeddraCodingMetadataListQuery ValidListQuery() =>
        new(
            ValidServerTenantContext(),
            ValidActorContext(),
            AllowPermission(),
            ValidCorrelationContext(),
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
}
