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
        "patient-subject-code",
        "reporter@example.test",
        "free text narrative with PHI",
        "triage free-text reason",
        "raw exception message",
        "System.InvalidOperationException",
        "client-supplied-id"
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
        Assert.DoesNotContain(names, name => name.Contains("Archive", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Void", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Export", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("BulkDelete", StringComparison.OrdinalIgnoreCase));
    }
}
