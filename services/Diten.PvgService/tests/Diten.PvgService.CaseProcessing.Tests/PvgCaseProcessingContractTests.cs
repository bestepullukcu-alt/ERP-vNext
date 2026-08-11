using System.Reflection;
using System.Text.Json;
using Diten.PvgService.Application.CaseProcessing;
using Diten.PvgService.Domain.CaseProcessing;
using Xunit;

namespace Diten.PvgService.CaseProcessing.Tests;

public sealed class PvgCaseProcessingContractTests
{
    private static readonly string[] SensitiveSamples =
    [
        "tenant-secret-123",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "raw-external-reference",
        "raw exception message",
        "System.InvalidOperationException",
        "at Diten."
    ];

    [Fact]
    public void Command_and_query_contracts_do_not_accept_client_tenant_id()
    {
        var contractTypes = new[]
        {
            typeof(AcceptMod0230HandoffCommand),
            typeof(UpdateSignalMinimumAssessmentCommand),
            typeof(MarkSignalMinimumReadyCommand),
            typeof(GetCaseProcessingMetadataByIdQuery),
            typeof(GetCaseProcessingMetadataListQuery),
            typeof(Mod0230HandoffReference),
            typeof(SignalMinimumAssessment)
        };

        foreach (var contractType in contractTypes)
        {
            Assert.DoesNotContain(
                contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public),
                property => property.Name.Equals("TenantId", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Archive_void_export_delete_and_bulk_delete_contracts_do_not_exist()
    {
        var forbiddenNames = new[] { "Archive", "Void", "Export", "Delete", "BulkDelete" };
        var typeNames = typeof(AcceptMod0230HandoffCommand).Assembly
            .GetTypes()
            .Concat(typeof(SafetyCaseMaster).Assembly.GetTypes())
            .Select(type => type.Name)
            .ToArray();

        foreach (var forbiddenName in forbiddenNames)
        {
            Assert.DoesNotContain(typeNames, name => name.Contains(forbiddenName, StringComparison.OrdinalIgnoreCase));
        }

        var enumNames = Enum.GetNames<SafetyCaseMasterStatus>()
            .Concat(Enum.GetNames<SignalMinimumLifecycleState>())
            .ToArray();

        foreach (var forbiddenName in forbiddenNames)
        {
            Assert.DoesNotContain(enumNames, name => name.Contains(forbiddenName, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Missing_mod0230_handoff_blocks_without_sensitive_echo()
    {
        var command = new AcceptMod0230HandoffCommand(
            TenantContext(),
            ActorContext(),
            CorrelationContext(),
            null!,
            AllowedGuards());

        var validation = PvgCaseProcessingValidator.ValidateAcceptHandoff(command);
        var result = PvgCaseProcessingValidator.ToResult(validation);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.Mod0230HandoffRequired);
        Assert.Equal(PvgCaseProcessingOutcome.Blocked, result.Outcome);
        AssertDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Missing_tenant_actor_permission_and_correlation_fail_closed()
    {
        var missingTenant = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { TenantContext = new PvgCaseProcessingServerTenantContext("") });
        var missingActor = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { ActorContext = new PvgCaseProcessingActorContext("", "") });
        var missingPermission = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { GuardContext = AllowedGuards() with { PermissionDecision = null } });
        var missingCorrelation = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { CorrelationContext = new PvgCaseProcessingCorrelationContext("") });
        var invalidCorrelation = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { CorrelationContext = new PvgCaseProcessingCorrelationContext("invalid correlation with spaces") });

        Assert.Contains(missingTenant.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.TenantContextRequired);
        Assert.Contains(missingActor.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.ActorContextRequired);
        Assert.Contains(missingPermission.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.PermissionContextRequired);
        Assert.Contains(missingCorrelation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.CorrelationContextRequired);
        Assert.Contains(invalidCorrelation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.CorrelationContextInvalid);
    }

    [Fact]
    public void Missing_field_workflow_and_evidence_guards_fail_closed()
    {
        var missingFieldPolicy = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { GuardContext = AllowedGuards() with { FieldPolicyDecision = null } });
        var missingWorkflowGate = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { GuardContext = AllowedGuards() with { WorkflowGateDecision = null } });
        var missingEvidenceCompleteness = PvgCaseProcessingValidator.ValidateAcceptHandoff(
            ValidAcceptCommand() with { GuardContext = AllowedGuards() with { EvidenceCompletenessDecision = null } });

        Assert.Contains(missingFieldPolicy.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.FieldPolicyRequired);
        Assert.Contains(missingWorkflowGate.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.WorkflowGateRequired);
        Assert.Contains(missingEvidenceCompleteness.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.EvidenceCompletenessRequired);
    }

    [Fact]
    public void Denied_guard_decisions_block_without_sensitive_echo()
    {
        var command = ValidAcceptCommand() with
        {
            GuardContext = AllowedGuards() with
            {
                PermissionDecision = PvgCaseProcessingPermissionDecision.Denied(),
                FieldPolicyDecision = PvgCaseProcessingPortDecision.Denied(PvgCaseProcessingReasonCodes.FieldPolicyDenied),
                WorkflowGateDecision = PvgCaseProcessingPortDecision.Denied(PvgCaseProcessingReasonCodes.WorkflowGateDenied),
                EvidenceCompletenessDecision = PvgCaseProcessingPortDecision.Denied(PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied)
            }
        };

        var validation = PvgCaseProcessingValidator.ValidateAcceptHandoff(command);
        var result = PvgCaseProcessingValidator.ToResult(validation);

        Assert.Contains(validation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.PermissionDenied);
        Assert.Contains(validation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.FieldPolicyDenied);
        Assert.Contains(validation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.WorkflowGateDenied);
        Assert.Contains(validation.Failures, failure => failure.ReasonCode == PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied);
        AssertDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Assessment_validation_requires_minimum_fields_without_echoing_values()
    {
        var command = new UpdateSignalMinimumAssessmentCommand(
            TenantContext(),
            ActorContext(),
            CorrelationContext(),
            "case-processing-1",
            new SignalMinimumAssessment(
                "",
                "",
                "free text narrative with PHI",
                "",
                DateTimeOffset.UtcNow,
                "",
                "",
                "",
                null,
                "",
                null,
                "",
                "reporter@example.test",
                "",
                "raw-external-reference",
                "raw exception message"),
            AllowedGuards());

        var validation = PvgCaseProcessingValidator.ValidateUpdateAssessment(command);
        var result = PvgCaseProcessingValidator.ToResult(validation);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.CaseProcessingPriority));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.CaseValidityStatus));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.ProcessingOwnerQueue));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.ProductExposureAssessment));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.SeriousnessConfirmed));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.EventAssessmentSummary));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.EvidenceCompletenessStatus));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.SignalRelevanceFlag));
        Assert.Contains(validation.Failures, failure => failure.Field == nameof(SignalMinimumAssessment.SignalHandoffReadiness));
        AssertDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Safety_case_master_accepts_handoff_with_server_tenant_only()
    {
        var acceptedAt = DateTimeOffset.UtcNow;
        var master = SafetyCaseMaster.AcceptHandoff(
            "case-processing-1",
            "tenant-secret-123",
            ValidHandoffReference(),
            acceptedAt);

        Assert.Equal("tenant-secret-123", master.TenantId);
        Assert.Equal(SafetyCaseMasterStatus.HandoffAccepted, master.Status);
        Assert.Equal(SignalMinimumLifecycleState.IntakeAccepted, master.LifecycleState);
        Assert.Null(master.Assessment);
        Assert.Equal(acceptedAt, master.CreatedAtUtc);
    }

    [Fact]
    public void Runtime_infrastructure_contracts_are_absent()
    {
        var applicationTypeNames = typeof(AcceptMod0230HandoffCommand).Assembly
            .GetTypes()
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Controller", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Program", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Health", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Mongo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("DbContext", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Partner", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase));
    }

    private static AcceptMod0230HandoffCommand ValidAcceptCommand() =>
        new(TenantContext(), ActorContext(), CorrelationContext(), ValidHandoffReference(), AllowedGuards());

    private static PvgCaseProcessingServerTenantContext TenantContext() => new("tenant-secret-123");

    private static PvgCaseProcessingActorContext ActorContext() => new("actor-1", "PVG_CASE_PROCESSOR");

    private static PvgCaseProcessingCorrelationContext CorrelationContext() => new("corr-123");

    private static PvgCaseProcessingGuardContext AllowedGuards() =>
        new(
            PvgCaseProcessingPermissionDecision.Allowed(),
            PvgCaseProcessingPortDecision.Allowed(),
            PvgCaseProcessingPortDecision.Allowed(),
            PvgCaseProcessingPortDecision.Allowed());

    private static Mod0230HandoffReference ValidHandoffReference() =>
        new(
            "intake-1",
            "PVG-INTAKE-1",
            DateTimeOffset.UtcNow,
            "AcceptedForProcessing",
            "SafetyReview",
            ["evidence-link-1"]);

    private static void AssertDoesNotEchoSensitiveValues(PvgCaseProcessingResult result)
    {
        var serialized = JsonSerializer.Serialize(result);

        foreach (var sensitiveSample in SensitiveSamples)
        {
            Assert.DoesNotContain(sensitiveSample, serialized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
