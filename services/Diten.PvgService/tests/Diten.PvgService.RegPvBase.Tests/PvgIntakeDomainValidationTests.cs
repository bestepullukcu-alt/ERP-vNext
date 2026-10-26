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

    // The 16 slice 1 intake fields: the 15 kept fields and ProductNameAsReported, the successor of
    // SuspectProductText (pack "Entity Fields"). The full 34-entry equality is measured by
    // Intake_field_definitions_match_the_34_revision04_intake_field_entries.
    private static readonly PvgIntakeField[] Slice1IntakeFields =
    [
        PvgIntakeField.IntakeChannel,
        PvgIntakeField.SourceType,
        PvgIntakeField.SourceReference,
        PvgIntakeField.ReceivedAtUtc,
        PvgIntakeField.ReporterType,
        PvgIntakeField.ReporterContactSummary,
        PvgIntakeField.PatientSubjectCode,
        PvgIntakeField.EventOnsetDate,
        PvgIntakeField.AdverseEventNarrative,
        PvgIntakeField.ProductNameAsReported,
        PvgIntakeField.Seriousness,
        PvgIntakeField.IntakePriority,
        PvgIntakeField.TriageOutcome,
        PvgIntakeField.TriageReason,
        PvgIntakeField.RouteTargetQueue,
        PvgIntakeField.EvidenceLinkReferences
    ];

    [Fact]
    public void Approved_field_definitions_cover_the_16_MOD_0230_fields()
    {
        var fields = PvgIntakeFieldDefinition.ApprovedFields
            .Where(field => Slice1IntakeFields.Contains(field.Field))
            .ToArray();

        Assert.Equal(16, Slice1IntakeFields.Length);
        Assert.Equal(16, fields.Length);
        Assert.Equal(16, fields.Select(field => field.Field).Distinct().Count());
        Assert.Equal(Slice1IntakeFields.Order(), fields.Select(field => field.Field).Order());
        Assert.Contains(fields, field => field.Field == PvgIntakeField.ReporterContactSummary && field.Sensitivity == PvgFieldSensitivity.Pii);
        Assert.Contains(fields, field => field.Field == PvgIntakeField.PatientSubjectCode && field.Sensitivity == PvgFieldSensitivity.Phi);
        Assert.Contains(fields, field => field.Field == PvgIntakeField.AdverseEventNarrative && field.IsFreeText);
        Assert.Contains(fields, field => field.Field == PvgIntakeField.TriageReason && field.IsFreeText);
    }

    [Fact]
    public void Field_security_governance_candidates_cover_all_16_MOD_0230_user_facing_fields()
    {
        var expectedFields = new Dictionary<PvgIntakeField, (PvgFieldSensitivity Sensitivity, bool IsFreeText)>
        {
            [PvgIntakeField.IntakeChannel] = (PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.SourceType] = (PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.SourceReference] = (PvgFieldSensitivity.Confidential, false),
            [PvgIntakeField.ReceivedAtUtc] = (PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.ReporterType] = (PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.ReporterContactSummary] = (PvgFieldSensitivity.Pii, false),
            [PvgIntakeField.PatientSubjectCode] = (PvgFieldSensitivity.Phi, false),
            [PvgIntakeField.EventOnsetDate] = (PvgFieldSensitivity.Phi, false),
            [PvgIntakeField.AdverseEventNarrative] = (PvgFieldSensitivity.Phi, true),
            [PvgIntakeField.ProductNameAsReported] = (PvgFieldSensitivity.RegulatedSafety, true),
            [PvgIntakeField.Seriousness] = (PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.IntakePriority] = (PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.TriageOutcome] = (PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.TriageReason] = (PvgFieldSensitivity.Phi, true),
            [PvgIntakeField.RouteTargetQueue] = (PvgFieldSensitivity.Confidential, false),
            [PvgIntakeField.EvidenceLinkReferences] = (PvgFieldSensitivity.Confidential, false)
        };

        var fields = PvgIntakeFieldDefinition.ApprovedFields
            .Where(field => expectedFields.ContainsKey(field.Field))
            .ToArray();

        Assert.Equal(16, expectedFields.Count);
        Assert.Equal(expectedFields.Keys.Order(), fields.Select(field => field.Field).Order());
        Assert.Equal(Slice1IntakeFields.Order(), expectedFields.Keys.Order());

        foreach (var field in fields)
        {
            var expected = expectedFields[field.Field];
            Assert.Equal(expected.Sensitivity, field.Sensitivity);
            Assert.Equal(expected.IsFreeText, field.IsFreeText);
        }
    }

    [Fact]
    public void Intake_field_definitions_match_the_34_revision04_intake_field_entries()
    {
        var expectedFields = new Dictionary<PvgIntakeField, (bool IsRequiredAtCreate, PvgFieldSensitivity Sensitivity, bool IsFreeText)>
        {
            [PvgIntakeField.IntakeChannel] = (true, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.SourceType] = (true, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.SourceReference] = (false, PvgFieldSensitivity.Confidential, false),
            [PvgIntakeField.ReceivedAtUtc] = (true, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.ReporterType] = (true, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.ReporterContactSummary] = (false, PvgFieldSensitivity.Pii, false),
            [PvgIntakeField.PatientSubjectCode] = (false, PvgFieldSensitivity.Phi, false),
            [PvgIntakeField.EventOnsetDate] = (false, PvgFieldSensitivity.Phi, false),
            [PvgIntakeField.AdverseEventNarrative] = (true, PvgFieldSensitivity.Phi, true),
            [PvgIntakeField.Seriousness] = (true, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.IntakePriority] = (true, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.TriageOutcome] = (false, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.TriageReason] = (false, PvgFieldSensitivity.Phi, true),
            [PvgIntakeField.RouteTargetQueue] = (false, PvgFieldSensitivity.Confidential, false),
            [PvgIntakeField.EvidenceLinkReferences] = (false, PvgFieldSensitivity.Confidential, false),
            [PvgIntakeField.ProductLines] = (false, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.ProductRole] = (false, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.GlobalProductId] = (false, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.ProductNameAsReported] = (false, PvgFieldSensitivity.RegulatedSafety, true),
            [PvgIntakeField.PresentationGskuId] = (false, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.LotNumber] = (false, PvgFieldSensitivity.RegulatedSafety, true),
            [PvgIntakeField.AwarenessAtUtc] = (true, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.CompanyRecipientPersonId] = (false, PvgFieldSensitivity.Confidential, false),
            [PvgIntakeField.EventCountry] = (true, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.ReportType] = (true, PvgFieldSensitivity.RegulatedSafety, false),
            [PvgIntakeField.ReporterSelectionDirectory] = (false, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.ReporterSelectionId] = (false, PvgFieldSensitivity.Pii, false),
            [PvgIntakeField.RemoveReporterLink] = (false, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.ReporterSource] = (false, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.ReporterSourceId] = (false, PvgFieldSensitivity.Pii, false),
            [PvgIntakeField.ReporterCrmSearchState] = (false, PvgFieldSensitivity.PublicMetadata, false),
            [PvgIntakeField.ReporterNameOrInitials] = (false, PvgFieldSensitivity.Pii, false),
            [PvgIntakeField.ReporterProfession] = (false, PvgFieldSensitivity.Pii, false),
            [PvgIntakeField.ReporterCountry] = (false, PvgFieldSensitivity.Pii, false)
        };

        var fields = PvgIntakeFieldDefinition.ApprovedFields;

        Assert.Equal(34, expectedFields.Count);
        Assert.Equal(34, fields.Count);
        Assert.Equal(34, Enum.GetValues<PvgIntakeField>().Length);
        Assert.Equal(34, fields.Select(field => field.Field).Distinct().Count());
        Assert.Equal(Enum.GetValues<PvgIntakeField>().Order(), fields.Select(field => field.Field).Order());
        Assert.Equal(expectedFields.Keys.Order(), fields.Select(field => field.Field).Order());

        foreach (var field in fields)
        {
            var expected = expectedFields[field.Field];
            Assert.Equal(expected.IsRequiredAtCreate, field.IsRequiredAtCreate);
            Assert.Equal(expected.Sensitivity, field.Sensitivity);
            Assert.Equal(expected.IsFreeText, field.IsFreeText);
        }
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
            [new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, null)],
            "",
            null,
            ["evidence-ref"],
            null,
            null,
            "unknown",
            "Spontaneous",
            null,
            null,
            null,
            null,
            null);

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
    public void Create_validation_rejects_out_of_bounds_MOD_0230_fields_without_echoing_values()
    {
        var request = new PvgCreateIntakeDraftRequest(
            "portal",
            "reporter",
            new string('s', 129),
            DateTimeOffset.UtcNow.AddMinutes(10),
            "healthcare-professional",
            new string('r', 257),
            "PAT 123@unsafe",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            new string('n', 8001),
            [new PvgIntakeProductLineRequest("Suspect", null, new string('p', 513), null, null)],
            "serious",
            "high",
            Enumerable.Range(1, 21).Select(index => $"evidence-ref-{index}").ToArray(),
            DateTimeOffset.UtcNow.AddMinutes(10),
            null,
            "unknown",
            "Spontaneous",
            null,
            null,
            null,
            null,
            null);

        var result = PvgIntakeDraftValidator.ValidateCreate(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        AssertContainsInvalid(result, PvgIntakeField.SourceReference);
        AssertContainsInvalid(result, PvgIntakeField.ReceivedAtUtc);
        AssertContainsInvalid(result, PvgIntakeField.ReporterContactSummary);
        AssertContainsInvalid(result, PvgIntakeField.PatientSubjectCode);
        AssertContainsInvalid(result, PvgIntakeField.EventOnsetDate);
        AssertContainsInvalid(result, PvgIntakeField.AdverseEventNarrative);
        AssertContainsInvalid(result, PvgIntakeField.ProductNameAsReported);
        AssertContainsInvalid(result, PvgIntakeField.EvidenceLinkReferences);
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
            null,
            null,
            null,
            "unknown",
            "Spontaneous",
            null,
            null,
            null,
            null,
            null,
            null);

        var result = PvgIntakeDraftValidator.ValidateUpdate(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        Assert.All(result.Failures, failure => Assert.Equal(PvgValidationReasonCodes.RequiredFieldMissing, failure.ReasonCode));
        AssertValidationResultDoesNotEchoSensitiveValues(result);
    }

    [Fact]
    public void Update_validation_rejects_unsupported_date_and_patient_code_shapes()
    {
        var request = new PvgUpdateIntakeDraftRequest(
            "portal",
            "reporter",
            "source-ref",
            new DateTimeOffset(1899, 12, 31, 23, 59, 59, TimeSpan.Zero),
            "healthcare-professional",
            "summary",
            "PAT-123-45-67",
            new DateOnly(1899, 12, 31),
            "free text narrative with PHI",
            [new PvgIntakeProductLineRequest("Suspect", null, "suspect product", null, null)],
            "serious",
            "high",
            null,
            new DateTimeOffset(1899, 12, 31, 23, 59, 59, TimeSpan.Zero),
            null,
            "unknown",
            "Spontaneous",
            null,
            null,
            null,
            null,
            null,
            null);

        var result = PvgIntakeDraftValidator.ValidateUpdate(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        AssertContainsInvalid(result, PvgIntakeField.ReceivedAtUtc);
        AssertContainsInvalid(result, PvgIntakeField.PatientSubjectCode);
        AssertContainsInvalid(result, PvgIntakeField.EventOnsetDate);
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
    public void Triage_validation_rejects_overlong_reason_without_echoing_values()
    {
        var request = new PvgTriageIntakeDraftRequest(
            PvgTriageOutcome.Duplicate,
            "PVG_TRIAGE_REASON_DUPLICATE",
            new string('t', 1001));

        var result = PvgIntakeDraftValidator.ValidateTriage(
            new PvgServerTenantContext("tenant-secret-123"),
            request);

        Assert.False(result.IsValid);
        AssertContainsInvalid(result, PvgIntakeField.TriageReason);
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

    private static void AssertContainsInvalid(PvgValidationResult result, PvgIntakeField field)
    {
        Assert.Contains(
            result.Failures,
            failure => failure.Field == field &&
                failure.ReasonCode == PvgValidationReasonCodes.FieldValueInvalid);
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
