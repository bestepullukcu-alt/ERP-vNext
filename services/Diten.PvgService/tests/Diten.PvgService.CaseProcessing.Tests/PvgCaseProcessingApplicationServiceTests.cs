using System.Text.Json;
using Diten.PvgService.Application.CaseProcessing;
using Diten.PvgService.Domain.CaseProcessing;
using Xunit;

namespace Diten.PvgService.CaseProcessing.Tests;

public sealed class PvgCaseProcessingApplicationServiceTests
{
    private static readonly string[] SensitiveSamples =
    [
        "tenant-secret-123",
        "tenant-other-456",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "raw-external-reference",
        "raw exception message",
        "System.InvalidOperationException",
        "at Diten."
    ];

    [Fact]
    public void Accept_handoff_validation_runs_before_mutation()
    {
        var service = new PvgCaseProcessingApplicationService();

        var result = service.AcceptMod0230Handoff(ValidAcceptCommand() with { HandoffReference = null! });

        Assert.False(result.Result.Succeeded);
        Assert.Equal(PvgCaseProcessingOutcome.Blocked, result.Result.Outcome);
        Assert.Null(result.CaseProcessingId);
        Assert.Contains(PvgCaseProcessingReasonCodes.Mod0230HandoffRequired, result.Result.ReasonCodes);
        Assert.Empty(service.ListMetadata(ListQuery()).Items);
        AssertSafe(result);
    }

    [Fact]
    public void Accept_handoff_succeeds_with_metadata_only_when_all_guards_allow()
    {
        var service = new PvgCaseProcessingApplicationService();

        var accepted = service.AcceptMod0230Handoff(ValidAcceptCommand());

        Assert.True(accepted.Result.Succeeded);
        Assert.NotNull(accepted.CaseProcessingId);
        Assert.Equal("AcceptMod0230Handoff", accepted.Result.Metadata!.Operation);
        Assert.Equal("pvg.mod0231.case-processing.accept-handoff", accepted.Result.Metadata.RequiredPermission);
        Assert.Equal("PVG_CASE_PROCESSOR", accepted.Result.Metadata.ActorKind);
        Assert.True(accepted.Result.Metadata.HasCorrelation);

        var listed = service.ListMetadata(ListQuery());

        Assert.True(listed.Result.Succeeded);
        Assert.Single(listed.Items);
        Assert.Equal(accepted.CaseProcessingId, listed.Items[0].CaseProcessingId);
        Assert.Equal(SafetyCaseMasterStatus.HandoffAccepted, listed.Items[0].Status);
        Assert.Equal(SignalMinimumLifecycleState.IntakeAccepted, listed.Items[0].LifecycleState);
        Assert.False(listed.Items[0].HasAssessment);
        Assert.False(listed.Items[0].IsSignalMinimumReady);
        AssertSafe(accepted);
        AssertSafe(listed);
    }

    [Theory]
    [InlineData(nameof(PvgCaseProcessingReasonCodes.PermissionDenied))]
    [InlineData(nameof(PvgCaseProcessingReasonCodes.FieldPolicyDenied))]
    [InlineData(nameof(PvgCaseProcessingReasonCodes.WorkflowGateDenied))]
    [InlineData(nameof(PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied))]
    public void Denied_guards_block_accept_before_mutation(string deniedReasonName)
    {
        var service = new PvgCaseProcessingApplicationService();
        var command = ValidAcceptCommand() with { GuardContext = GuardDeniedBy(deniedReasonName) };

        var result = service.AcceptMod0230Handoff(command);

        Assert.False(result.Result.Succeeded);
        Assert.Equal(PvgCaseProcessingOutcome.Blocked, result.Result.Outcome);
        Assert.Null(result.CaseProcessingId);
        Assert.Contains(ReasonValue(deniedReasonName), result.Result.ReasonCodes);
        Assert.Empty(service.ListMetadata(ListQuery()).Items);
        AssertSafe(result);
    }

    [Fact]
    public void Missing_correlation_blocks_accept_before_mutation()
    {
        var service = new PvgCaseProcessingApplicationService();

        var result = service.AcceptMod0230Handoff(
            ValidAcceptCommand() with { CorrelationContext = new PvgCaseProcessingCorrelationContext("") });

        Assert.False(result.Result.Succeeded);
        Assert.Null(result.CaseProcessingId);
        Assert.Contains(PvgCaseProcessingReasonCodes.CorrelationContextRequired, result.Result.ReasonCodes);
        Assert.Empty(service.ListMetadata(ListQuery()).Items);
        AssertSafe(result);
    }

    [Fact]
    public void Update_assessment_validation_and_guards_block_before_mutation()
    {
        var service = new PvgCaseProcessingApplicationService();
        var accepted = service.AcceptMod0230Handoff(ValidAcceptCommand());

        var invalidAssessment = service.UpdateSignalMinimumAssessment(
            ValidUpdateCommand(accepted.CaseProcessingId!) with { Assessment = InvalidAssessment() });
        var deniedPermission = service.UpdateSignalMinimumAssessment(
            ValidUpdateCommand(accepted.CaseProcessingId!) with { GuardContext = GuardDeniedBy(nameof(PvgCaseProcessingReasonCodes.PermissionDenied)) });

        var metadata = service.GetByIdMetadata(ByIdQuery(accepted.CaseProcessingId!));

        Assert.False(invalidAssessment.Result.Succeeded);
        Assert.False(deniedPermission.Result.Succeeded);
        Assert.Single(metadata.Items);
        Assert.False(metadata.Items[0].HasAssessment);
        Assert.Equal(SignalMinimumLifecycleState.IntakeAccepted, metadata.Items[0].LifecycleState);
        AssertSafe(invalidAssessment);
        AssertSafe(deniedPermission);
    }

    [Fact]
    public void Update_assessment_then_mark_ready_mutates_only_after_guards_pass()
    {
        var service = new PvgCaseProcessingApplicationService();
        var accepted = service.AcceptMod0230Handoff(ValidAcceptCommand());

        var updated = service.UpdateSignalMinimumAssessment(ValidUpdateCommand(accepted.CaseProcessingId!));
        var ready = service.MarkSignalMinimumReady(ValidReadyCommand(accepted.CaseProcessingId!));
        var metadata = service.GetByIdMetadata(ByIdQuery(accepted.CaseProcessingId!));

        Assert.True(updated.Result.Succeeded);
        Assert.True(ready.Result.Succeeded);
        Assert.Single(metadata.Items);
        Assert.True(metadata.Items[0].HasAssessment);
        Assert.True(metadata.Items[0].IsSignalMinimumReady);
        Assert.Equal(SafetyCaseMasterStatus.SignalMinimumReady, metadata.Items[0].Status);
        Assert.Equal(SignalMinimumLifecycleState.SignalMinimumReady, metadata.Items[0].LifecycleState);
        AssertSafe(updated);
        AssertSafe(ready);
        AssertSafe(metadata);
    }

    [Fact]
    public void Mark_ready_without_assessment_fails_closed_before_ready_mutation()
    {
        var service = new PvgCaseProcessingApplicationService();
        var accepted = service.AcceptMod0230Handoff(ValidAcceptCommand());

        var ready = service.MarkSignalMinimumReady(ValidReadyCommand(accepted.CaseProcessingId!));
        var metadata = service.GetByIdMetadata(ByIdQuery(accepted.CaseProcessingId!));

        Assert.False(ready.Result.Succeeded);
        Assert.Equal(PvgCaseProcessingOutcome.Blocked, ready.Result.Outcome);
        Assert.Contains(PvgCaseProcessingReasonCodes.AssessmentRequired, ready.Result.ReasonCodes);
        Assert.Single(metadata.Items);
        Assert.False(metadata.Items[0].IsSignalMinimumReady);
        Assert.Equal(SignalMinimumLifecycleState.IntakeAccepted, metadata.Items[0].LifecycleState);
        AssertSafe(ready);
    }

    [Fact]
    public void Cross_tenant_read_and_write_do_not_leak_existence()
    {
        var service = new PvgCaseProcessingApplicationService();
        var accepted = service.AcceptMod0230Handoff(ValidAcceptCommand());

        var crossTenantUpdate = service.UpdateSignalMinimumAssessment(
            ValidUpdateCommand(accepted.CaseProcessingId!) with { TenantContext = OtherTenantContext() });
        var crossTenantRead = service.GetByIdMetadata(ByIdQuery(accepted.CaseProcessingId!) with { TenantContext = OtherTenantContext() });
        var crossTenantList = service.ListMetadata(ListQuery() with { TenantContext = OtherTenantContext() });

        Assert.Equal(PvgCaseProcessingOutcome.NotFound, crossTenantUpdate.Result.Outcome);
        Assert.Equal(PvgCaseProcessingOutcome.NotFound, crossTenantRead.Result.Outcome);
        Assert.Empty(crossTenantRead.Items);
        Assert.Empty(crossTenantList.Items);
        AssertSafe(crossTenantUpdate);
        AssertSafe(crossTenantRead);
        AssertSafe(crossTenantList);
    }

    [Fact]
    public void Read_metadata_requires_context_and_guards_before_returning_results()
    {
        var service = new PvgCaseProcessingApplicationService();
        var accepted = service.AcceptMod0230Handoff(ValidAcceptCommand());

        var missingActor = service.GetByIdMetadata(
            ByIdQuery(accepted.CaseProcessingId!) with { ActorContext = new PvgCaseProcessingActorContext("", "") });
        var missingCorrelation = service.GetByIdMetadata(
            ByIdQuery(accepted.CaseProcessingId!) with { CorrelationContext = new PvgCaseProcessingCorrelationContext("") });
        var deniedPermission = service.ListMetadata(
            ListQuery() with { GuardContext = GuardDeniedBy(nameof(PvgCaseProcessingReasonCodes.PermissionDenied)) });
        var deniedFieldPolicy = service.ListMetadata(
            ListQuery() with { GuardContext = GuardDeniedBy(nameof(PvgCaseProcessingReasonCodes.FieldPolicyDenied)) });

        Assert.Empty(missingActor.Items);
        Assert.Empty(missingCorrelation.Items);
        Assert.Empty(deniedPermission.Items);
        Assert.Empty(deniedFieldPolicy.Items);
        Assert.Contains(PvgCaseProcessingReasonCodes.ActorContextRequired, missingActor.Result.ReasonCodes);
        Assert.Contains(PvgCaseProcessingReasonCodes.CorrelationContextRequired, missingCorrelation.Result.ReasonCodes);
        Assert.Contains(PvgCaseProcessingReasonCodes.PermissionDenied, deniedPermission.Result.ReasonCodes);
        Assert.Contains(PvgCaseProcessingReasonCodes.FieldPolicyDenied, deniedFieldPolicy.Result.ReasonCodes);
        AssertSafe(missingActor);
        AssertSafe(missingCorrelation);
        AssertSafe(deniedPermission);
        AssertSafe(deniedFieldPolicy);
    }

    [Fact]
    public void Runtime_infrastructure_types_remain_absent()
    {
        var applicationTypeNames = typeof(PvgCaseProcessingApplicationService)
            .Assembly
            .GetTypes()
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        var forbiddenFragments = new[]
        {
            "Archive",
            "Void",
            "Export",
            "Delete",
            "BulkDelete",
            "Controller",
            "Program",
            "Health",
            "Mongo",
            "DbContext",
            "Repository",
            "Partner",
            "OpenAI"
        };

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(applicationTypeNames, name => name.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static AcceptMod0230HandoffCommand ValidAcceptCommand() =>
        new(TenantContext(), ActorContext(), CorrelationContext(), ValidHandoffReference(), AllowedGuards());

    private static UpdateSignalMinimumAssessmentCommand ValidUpdateCommand(string caseProcessingId) =>
        new(TenantContext(), ActorContext(), CorrelationContext(), caseProcessingId, ValidAssessment(), AllowedGuards());

    private static MarkSignalMinimumReadyCommand ValidReadyCommand(string caseProcessingId) =>
        new(TenantContext(), ActorContext(), CorrelationContext(), caseProcessingId, AllowedGuards());

    private static GetCaseProcessingMetadataByIdQuery ByIdQuery(string caseProcessingId) =>
        new(TenantContext(), ActorContext(), CorrelationContext(), AllowedGuards(), caseProcessingId);

    private static GetCaseProcessingMetadataListQuery ListQuery() =>
        new(TenantContext(), ActorContext(), CorrelationContext(), AllowedGuards(), 1, 10, null);

    private static PvgCaseProcessingServerTenantContext TenantContext() => new("tenant-secret-123");

    private static PvgCaseProcessingServerTenantContext OtherTenantContext() => new("tenant-other-456");

    private static PvgCaseProcessingActorContext ActorContext() => new("actor-1", "PVG_CASE_PROCESSOR");

    private static PvgCaseProcessingCorrelationContext CorrelationContext() => new("corr-123");

    private static PvgCaseProcessingGuardContext AllowedGuards() =>
        new(
            PvgCaseProcessingPermissionDecision.Allowed(),
            PvgCaseProcessingPortDecision.Allowed(),
            PvgCaseProcessingPortDecision.Allowed(),
            PvgCaseProcessingPortDecision.Allowed());

    private static PvgCaseProcessingGuardContext GuardDeniedBy(string deniedReasonName) =>
        deniedReasonName switch
        {
            nameof(PvgCaseProcessingReasonCodes.PermissionDenied) => AllowedGuards() with
            {
                PermissionDecision = PvgCaseProcessingPermissionDecision.Denied()
            },
            nameof(PvgCaseProcessingReasonCodes.FieldPolicyDenied) => AllowedGuards() with
            {
                FieldPolicyDecision = PvgCaseProcessingPortDecision.Denied(PvgCaseProcessingReasonCodes.FieldPolicyDenied)
            },
            nameof(PvgCaseProcessingReasonCodes.WorkflowGateDenied) => AllowedGuards() with
            {
                WorkflowGateDecision = PvgCaseProcessingPortDecision.Denied(PvgCaseProcessingReasonCodes.WorkflowGateDenied)
            },
            nameof(PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied) => AllowedGuards() with
            {
                EvidenceCompletenessDecision = PvgCaseProcessingPortDecision.Denied(PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(deniedReasonName), deniedReasonName, null)
        };

    private static string ReasonValue(string deniedReasonName) =>
        deniedReasonName switch
        {
            nameof(PvgCaseProcessingReasonCodes.PermissionDenied) => PvgCaseProcessingReasonCodes.PermissionDenied,
            nameof(PvgCaseProcessingReasonCodes.FieldPolicyDenied) => PvgCaseProcessingReasonCodes.FieldPolicyDenied,
            nameof(PvgCaseProcessingReasonCodes.WorkflowGateDenied) => PvgCaseProcessingReasonCodes.WorkflowGateDenied,
            nameof(PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied) => PvgCaseProcessingReasonCodes.EvidenceCompletenessDenied,
            _ => throw new ArgumentOutOfRangeException(nameof(deniedReasonName), deniedReasonName, null)
        };

    private static Mod0230HandoffReference ValidHandoffReference() =>
        new(
            "intake-1",
            "PVG-INTAKE-1",
            DateTimeOffset.UtcNow,
            "AcceptedForProcessing",
            "SafetyReview",
            ["evidence-link-1"]);

    private static SignalMinimumAssessment ValidAssessment() =>
        new(
            "High",
            "Valid",
            "case validity reason",
            "PVGProcessing",
            DateTimeOffset.UtcNow.AddDays(1),
            "Product exposure assessed",
            "Seriousness confirmed",
            "Event assessment summary",
            "Expectedness pending",
            "Complete",
            null,
            "Relevant",
            "Signal relevance reason",
            "Ready",
            "Signal handoff summary",
            "Internal processing note");

    private static SignalMinimumAssessment InvalidAssessment() =>
        ValidAssessment() with
        {
            CaseProcessingPriority = "",
            CaseValidityStatus = "",
            ProcessingOwnerQueue = "",
            ProductExposureAssessment = "",
            SeriousnessConfirmed = "",
            EventAssessmentSummary = "",
            EvidenceCompletenessStatus = "",
            SignalRelevanceFlag = "",
            SignalHandoffReadiness = "",
            ProcessingNotesInternal = "free text narrative with PHI"
        };

    private static void AssertSafe(object result)
    {
        var serialized = JsonSerializer.Serialize(result);
        foreach (var sensitiveSample in SensitiveSamples)
        {
            Assert.DoesNotContain(sensitiveSample, serialized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
