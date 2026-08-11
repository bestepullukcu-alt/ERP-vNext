using System.Reflection;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Xunit;

namespace Diten.PvgService.RegPvBase.Tests;

public sealed class PvgIntakeDomainValidationTests
{
    private static readonly string[] SensitiveSamples =
    [
        "tenant-secret-123",
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "triage free-text reason",
        "queue-safety-review",
        "client-supplied-case-id"
    ];

    [Fact]
    public void Approved_field_definitions_cover_the_16_MOD_0230_fields()
    {
        var fields = PvgIntakeFieldDefinition.ApprovedFields;

        Assert.Equal(16, fields.Count);
        Assert.Equal(16, fields.Select(field => field.Field).Distinct().Count());
        Assert.Equal(Enum.GetValues<PvgIntakeField>().Order(), fields.Select(field => field.Field).Order());
        Assert.Contains(fields, field => field.Field == PvgIntakeField.ReporterContactSummary && field.Sensitivity == PvgFieldSensitivity.Pii);
        Assert.Contains(fields, field => field.Field == PvgIntakeField.PatientSubjectCode && field.Sensitivity == PvgFieldSensitivity.Phi);
        Assert.Contains(fields, field => field.Field == PvgIntakeField.AdverseEventNarrative && field.IsFreeText);
        Assert.Contains(fields, field => field.Field == PvgIntakeField.TriageReason && field.IsFreeText);
    }

    [Fact]
    public void Draft_request_shapes_do_not_accept_client_tenant_or_forbidden_operations()
    {
        var requestTypes = new[]
        {
            typeof(PvgCreateIntakeDraftRequest),
            typeof(PvgUpdateIntakeDraftRequest),
            typeof(PvgTriageIntakeDraftRequest),
            typeof(PvgRouteIntakeDraftRequest)
        };

        foreach (var requestType in requestTypes)
        {
            var names = requestType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.DoesNotContain("TenantId", names);
            Assert.DoesNotContain("ClientTenantId", names);
            AssertNoForbiddenNames(names);
        }
    }

    [Fact]
    public void Create_validation_requires_baseline_fields_without_echoing_sensitive_values()
    {
        var request = new PvgCreateIntakeDraftRequest(
            "",
            " ",
            "client-supplied-case-id",
            null,
            null,
            "reporter@example.test",
            "patient-subject-code",
            null,
            " ",
            "suspect product",
            "",
            null,
            ["evidence-ref"]);

        var result = PvgIntakeDraftValidator.ValidateCreate(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.IntakeChannel);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.SourceType);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.ReceivedAtUtc);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.ReporterType);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.AdverseEventNarrative);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.Seriousness);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.IntakePriority);
        AssertValidationResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Update_validation_requires_baseline_fields_without_echoing_sensitive_values()
    {
        var request = new PvgUpdateIntakeDraftRequest(
            null,
            null,
            "client-supplied-case-id",
            null,
            "",
            "reporter@example.test",
            "patient-subject-code",
            null,
            "free text narrative with PHI",
            null,
            " ",
            "",
            null);

        var result = PvgIntakeDraftValidator.ValidateUpdate(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        Assert.All(result.Failures, failure => Assert.Equal(PvgValidationReasonCodes.RequiredFieldMissing, failure.ReasonCode));
        AssertValidationResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Triage_validation_requires_safe_outcome_and_reason_code_without_echoing_values()
    {
        var request = new PvgTriageIntakeDraftRequest(
            PvgTriageOutcome.Rejected,
            "free text narrative with PHI",
            "triage free-text reason");

        var result = PvgIntakeDraftValidator.ValidateTriage(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Failures,
            failure => failure.Field == PvgIntakeField.TriageReason &&
                failure.ReasonCode == PvgValidationReasonCodes.FieldValueInvalid);
        AssertValidationResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Route_validation_requires_server_context_and_queue_without_echoing_values()
    {
        var request = new PvgRouteIntakeDraftRequest(" ");

        var result = PvgIntakeDraftValidator.ValidateRoute(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Field == PvgIntakeField.RouteTargetQueue);
        AssertValidationResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Missing_server_tenant_context_fails_closed_without_tenant_echo()
    {
        var request = new PvgRouteIntakeDraftRequest("queue-safety-review");

        var result = PvgIntakeDraftValidator.ValidateRoute(null, request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.ReasonCode == PvgValidationReasonCodes.TenantContextRequired);
        AssertValidationResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Intake_statuses_exclude_archive_void_export_delete_and_bulk_delete()
    {
        AssertNoForbiddenNames(Enum.GetNames<PvgIntakeStatus>());
        AssertNoForbiddenNames(Enum.GetNames<PvgIntakeOperation>());
    }

    private static void AssertValidationResultDoesNotEchoSensitiveValues(PvgValidationResult result)
    {
        var renderedResult = result.ToString();
        foreach (var sensitiveSample in SensitiveSamples)
        {
            Assert.DoesNotContain(sensitiveSample, renderedResult, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoForbiddenNames(IEnumerable<string> names)
    {
        Assert.DoesNotContain("Archive", names);
        Assert.DoesNotContain("Void", names);
        Assert.DoesNotContain("Export", names);
        Assert.DoesNotContain("Delete", names);
        Assert.DoesNotContain("BulkDelete", names);
    }
}
