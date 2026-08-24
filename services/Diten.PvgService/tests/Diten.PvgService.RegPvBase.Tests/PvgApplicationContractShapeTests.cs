using System.Reflection;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Xunit;

namespace Diten.PvgService.RegPvBase.Tests;

public sealed class PvgApplicationContractShapeTests
{
    private static readonly Type[] ContractTypes =
    [
        typeof(CreateIntakeDraftCommand),
        typeof(UpdateIntakeDraftCommand),
        typeof(TriageIntakeDraftCommand),
        typeof(RouteIntakeDraftCommand),
        typeof(GetIntakeDraftByIdQuery),
        typeof(GetIntakeDraftListQuery)
    ];

    private static readonly string[] SensitiveSamples =
    [
        "tenant-secret-123",
        "actor-secret-456",
        "corr-secret-789",
        "source-ref",
        "evidence-ref",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "triage free-text reason",
        "route free-text reason",
        "queue-safety-review",
        "suspect product",
        "raw exception message",
        "System.InvalidOperationException",
        "client-supplied-id"
    ];

    private static readonly string[] ForbiddenRetentionRuntimeTerms =
    [
        "Retention",
        "LegalHold",
        "Archive",
        "Void",
        "Export",
        "Delete",
        "BulkDelete",
        "Bulk"
    ];

    [Fact]
    public void Slice1_contracts_are_present_for_create_update_triage_route_and_safe_reads()
    {
        Assert.Contains(typeof(CreateIntakeDraftCommand), ContractTypes);
        Assert.Contains(typeof(UpdateIntakeDraftCommand), ContractTypes);
        Assert.Contains(typeof(TriageIntakeDraftCommand), ContractTypes);
        Assert.Contains(typeof(RouteIntakeDraftCommand), ContractTypes);
        Assert.Contains(typeof(GetIntakeDraftByIdQuery), ContractTypes);
        Assert.Contains(typeof(GetIntakeDraftListQuery), ContractTypes);
    }

    [Fact]
    public void Command_and_query_contracts_do_not_accept_client_tenant_id()
    {
        foreach (var contractType in ContractTypes)
        {
            var properties = contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            Assert.DoesNotContain(properties, property => property.Name is "TenantId" or "ClientTenantId");
            Assert.Contains(properties, property => property.Name == "TenantContext" && property.PropertyType == typeof(PvgServerTenantContext));
        }
    }

    [Fact]
    public void Archive_void_export_delete_and_bulk_delete_contracts_do_not_exist()
    {
        var applicationTypes = typeof(CreateIntakeDraftCommand)
            .Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        AssertNoForbiddenNames(applicationTypes);
        AssertNoForbiddenNames(Enum.GetNames<PvgIntakeOperation>());
        AssertNoForbiddenNames(Enum.GetNames<PvgApplicationOutcome>());
    }

    [Fact]
    public void Commands_queries_service_operations_and_dtos_do_not_expose_retention_or_forbidden_runtime_members()
    {
        var publicSurfaceNames = ContractTypes
            .Concat(
            [
                typeof(PvgCreateIntakeDraftRequest),
                typeof(PvgUpdateIntakeDraftRequest),
                typeof(PvgTriageIntakeDraftRequest),
                typeof(PvgRouteIntakeDraftRequest),
                typeof(PvgIntakeDraftSummary),
                typeof(PvgIntakeDraftMutationResult),
                typeof(PvgIntakeDraftQueryResult),
                typeof(PvgApplicationResult),
                typeof(PvgApplicationSuccessMetadata),
                typeof(PvgAuditIntent),
                typeof(SafetyCaseIntake),
                typeof(PvgIntakeDraftApplicationService)
            ])
            .SelectMany(type => PublicMemberNames(type).Append(type.Name))
            .ToArray();

        AssertNoForbiddenNames(publicSurfaceNames);
    }

    [Fact]
    public void Supported_domain_operations_do_not_define_retention_legal_hold_archive_or_void_state()
    {
        var stateNames = typeof(SafetyCaseIntake)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Concat(Enum.GetNames<PvgIntakeStatus>())
            .ToArray();

        AssertNoForbiddenNames(stateNames);
    }

    [Fact]
    public void Supported_mutations_do_not_change_retention_legal_hold_archive_void_or_sensitive_source_state()
    {
        var intake = new SafetyCaseIntake(
            "tenant-secret-123",
            PvgIntakeStatus.IntakeCreated,
            "Portal",
            "Reporter",
            DateTimeOffset.UnixEpoch,
            "HealthcareProfessional",
            "free text narrative with PHI",
            "Serious",
            "High")
        {
            SourceReference = "source-ref",
            ReporterContactSummary = "reporter@example.test",
            PatientSubjectCode = "patient-subject-code",
            SuspectProductText = "suspect product",
            EvidenceLinkReferences = ["evidence-ref"]
        };

        intake.MarkUpdated();
        intake.MarkTriaged(PvgTriageOutcome.Rejected, "triage free-text reason");
        intake.MarkRoutePending("queue-safety-review");

        Assert.Equal(PvgIntakeStatus.RoutePending, intake.Status);
        Assert.Equal(PvgTriageOutcome.Rejected, intake.TriageOutcome);
        Assert.Equal("triage free-text reason", intake.TriageReason);
        Assert.Equal("queue-safety-review", intake.RouteTargetQueue);
        Assert.Equal("source-ref", intake.SourceReference);
        Assert.Equal("reporter@example.test", intake.ReporterContactSummary);
        Assert.Equal("patient-subject-code", intake.PatientSubjectCode);
        Assert.Equal("suspect product", intake.SuspectProductText);
        Assert.Equal(["evidence-ref"], intake.EvidenceLinkReferences);
        AssertNoForbiddenNames(PublicMemberNames(typeof(SafetyCaseIntake)));
    }

    [Theory]
    [InlineData(PvgApplicationReasonCodes.IntakeDraftNotFound)]
    [InlineData(PvgPermissionReasonCodes.ActorContextRequired)]
    [InlineData(PvgPermissionReasonCodes.CorrelationContextRequired)]
    [InlineData(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable)]
    [InlineData(PvgSafeReasonCodes.WorkflowTransitionGateUnavailable)]
    [InlineData(PvgSafeReasonCodes.EvidenceLinkUnavailable)]
    public void Blocked_outputs_use_safe_reason_codes_without_retention_or_sensitive_value_echo(string reasonCode)
    {
        var result = PvgApplicationResult.Blocked(reasonCode);

        Assert.Equal(PvgApplicationOutcome.Blocked, result.Outcome);
        Assert.Null(result.Metadata);
        Assert.Empty(result.ValidationFailures);
        AssertSafeReasonCode(result.ReasonCode);
        AssertNoForbiddenNames([result.ReasonCode!]);
        AssertResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Application_result_success_contains_metadata_only()
    {
        var result = PvgApplicationResult.Succeeded(
            new PvgApplicationSuccessMetadata(
                PvgIntakeOperation.Create,
                PvgIntakeStatus.IntakeCreated,
                DateTimeOffset.UnixEpoch));

        Assert.True(result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Succeeded, result.Outcome);
        Assert.NotNull(result.Metadata);
        Assert.Null(result.ReasonCode);
        Assert.Empty(result.ValidationFailures);
        AssertResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Application_result_blocked_uses_reason_code_only()
    {
        var result = PvgApplicationResult.Blocked(PvgSafeReasonCodes.WorkflowTransitionGateUnavailable);

        Assert.False(result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Blocked, result.Outcome);
        Assert.Equal(PvgSafeReasonCodes.WorkflowTransitionGateUnavailable, result.ReasonCode);
        Assert.Null(result.Metadata);
        Assert.Empty(result.ValidationFailures);
        AssertResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Application_result_invalid_contains_validation_codes_without_submitted_values()
    {
        var result = PvgApplicationResult.Invalid(
        [
            new PvgValidationFailure(PvgIntakeField.AdverseEventNarrative, PvgValidationReasonCodes.RequiredFieldMissing),
            new PvgValidationFailure(PvgIntakeField.TriageReason, PvgValidationReasonCodes.FieldValueInvalid)
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(PvgApplicationOutcome.Invalid, result.Outcome);
        Assert.Equal(PvgValidationReasonCodes.FieldValueInvalid, result.ReasonCode);
        Assert.Null(result.Metadata);
        Assert.Equal(2, result.ValidationFailures.Count);
        AssertResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Application_contract_slice_does_not_add_handlers_controllers_or_persistence_types()
    {
        var applicationTypeNames = typeof(CreateIntakeDraftCommand)
            .Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        Assert.DoesNotContain(applicationTypeNames, name => name.EndsWith("Handler", StringComparison.Ordinal));
        Assert.DoesNotContain(applicationTypeNames, name => name.EndsWith("Controller", StringComparison.Ordinal));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("DbContext", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(applicationTypeNames, name => name.Contains("Mongo", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertResultDoesNotEchoSensitiveValues(PvgApplicationResult result)
    {
        var renderedResult = result.ToString();
        foreach (var sensitiveSample in SensitiveSamples)
        {
            Assert.DoesNotContain(sensitiveSample, renderedResult, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoForbiddenNames(IEnumerable<string> names)
    {
        foreach (var forbiddenTerm in ForbiddenRetentionRuntimeTerms)
        {
            Assert.DoesNotContain(names, name => name.Contains(forbiddenTerm, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> PublicMemberNames(Type type) =>
        type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Field)
            .Select(member => member.Name);

    private static void AssertSafeReasonCode(string? reasonCode)
    {
        Assert.False(string.IsNullOrWhiteSpace(reasonCode));
        Assert.StartsWith("PVG_", reasonCode, StringComparison.Ordinal);
        Assert.All(reasonCode, character =>
            Assert.True(
                char.IsUpper(character) || char.IsDigit(character) || character == '_',
                $"Reason code '{reasonCode}' must contain only safe uppercase token characters."));
    }
}
