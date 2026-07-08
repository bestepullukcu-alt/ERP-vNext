using System.Reflection;
using System.Text.Json;
using Diten.HcmService.Api.Controllers.Hcm;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Handlers;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class EmployeeMasterContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void RegistryRowContract_SerializesExpectedCamelCaseShape_WithoutTenantId()
    {
        var row = new EmployeeRegistryRowResponse(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "EMP-D10-2026-000001",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Example Person",
            "employee",
            "full_time",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Diten Legal",
            "draft",
            "standard",
            new DateOnly(2026, 06, 20),
            DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
            1,
            "\"1\"",
            new EmployeeRowActions(true, false, false, false, false, false));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(row, JsonOptions));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("employeeId", out _));
        Assert.True(root.TryGetProperty("employeeNumber", out _));
        Assert.True(root.TryGetProperty("etag", out _));
        Assert.False(root.TryGetProperty("tenantId", out _));
    }

    [Fact]
    public void ClientWriteContracts_DoNotAcceptTenantId()
    {
        var writeContracts = new[]
        {
            typeof(EmployeeProfilePatchRequest),
            typeof(EmploymentRecordPatchRequest),
            typeof(EmployeeStatusCommandRequest),
            typeof(EmployeeDocumentLinkRequest),
            typeof(DataQualityCasePatchRequest),
            typeof(EmployeeDraftCreateRequest),
            typeof(EmployeeDraftPatchRequest),
            typeof(DraftSubmitRequest)
        };

        foreach (var contractType in writeContracts)
        {
            Assert.DoesNotContain(
                contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => string.Equals(property.Name, "TenantId", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void PatchContractValidators_RequireETag_AndIdempotencyKey()
    {
        AssertRequiresConcurrencyAndIdempotency(
            new EmployeeProfilePatchRequestValidator(),
            new EmployeeProfilePatchRequest(string.Empty, "Ada", null, "Lovelace", null, null, null, null, null, null, "standard", string.Empty));

        AssertRequiresConcurrencyAndIdempotency(
            new EmploymentRecordPatchRequestValidator(),
            new EmploymentRecordPatchRequest(string.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, "permanent", null, null, "active", string.Empty));

        AssertRequiresConcurrencyAndIdempotency(
            new EmployeeStatusCommandRequestValidator(),
            new EmployeeStatusCommandRequest(string.Empty, "active", new DateOnly(2026, 06, 20), null, null, null, null, string.Empty));

        AssertRequiresConcurrencyAndIdempotency(
            new EmployeeDocumentLinkRequestValidator(),
            new EmployeeDocumentLinkRequest(string.Empty, Guid.NewGuid(), "contract", "restricted_hr", Guid.NewGuid(), string.Empty));

        AssertRequiresConcurrencyAndIdempotency(
            new DataQualityCasePatchRequestValidator(),
            new DataQualityCasePatchRequest(string.Empty, Guid.NewGuid(), "open", null, string.Empty));
    }

    [Fact]
    public void ActivationReadinessContract_ReturnsRequiredBeforeActivationFailures()
    {
        var errors = EmployeeMasterValidationContracts.ValidateActivationReadiness(
            new EmployeeActivationReadinessContract(
                LegalFirstName: "",
                LegalLastName: "",
                HireDate: null,
                PersonId: Guid.Empty,
                PersonReferenceIsSameTenant: false,
                LegalEntityId: Guid.Empty,
                LegalEntityReferenceIsSameTenant: false,
                WorkerType: "unknown",
                EmploymentType: "unknown",
                EmployeeNumberIsUniqueForTenant: false));

        Assert.Contains("legal_first_name_required_before_activation", errors);
        Assert.Contains("legal_last_name_required_before_activation", errors);
        Assert.Contains("hire_date_required_before_activation", errors);
        Assert.Contains("person_reference_must_resolve_same_tenant", errors);
        Assert.Contains("legal_entity_reference_must_resolve_same_tenant", errors);
        Assert.Contains("worker_type_must_be_reference_data_code", errors);
        Assert.Contains("employment_type_must_be_reference_data_code", errors);
        Assert.Contains("employee_number_must_be_unique_for_tenant", errors);
    }

    [Fact]
    public void AuditPayloadBuilder_UsesSafeMetadataOnly_AndRejectsPiiKeys()
    {
        var payload = EmployeeAuditPayloadBuilder.ProfileUpdated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "corr-1",
            "idempotency-hash",
            ["legal_first_name", "work_email"],
            2);

        Assert.Equal(EmployeeAuditEventNames.ProfileUpdated, payload.EventName);
        Assert.Contains("changed_fields", payload.Metadata.Keys);
        Assert.DoesNotContain(payload.Metadata.Keys, key => key.Contains("before", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(payload.Metadata.Keys, key => key.Contains("after", StringComparison.OrdinalIgnoreCase));

        Assert.False(EmployeeAuditPayloadBuilder.IsSafeMetadata(new Dictionary<string, string>
            {
                ["reason_note"] = "raw free text"
            }));
    }

    [Fact]
    public void ReferenceDataContract_ContainsSeedCategories_AndValidatesCodes()
    {
        Assert.Contains(EmployeeReferenceDataContracts.SeedContracts, seed => seed.Category == "employee_status");
        Assert.Contains(EmployeeReferenceDataContracts.SeedContracts, seed => seed.Category == "worker_type");
        Assert.True(EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.EmployeeStatuses, "active"));
        Assert.False(EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.EmployeeStatuses, "made_up"));
        Assert.True(EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.ContractTypes, "fixed_term"));
        Assert.False(EmployeeReferenceDataContracts.IsAllowed(EmployeeReferenceDataContracts.ContractTypes, "unsupported"));
    }

    [Fact]
    public void ContractSlice_LifecycleHandlersAreScopeBlocked()
    {
        Assert.Equal(
            "mod0251_lifecycle_activation_not_enabled",
            SubmitEmployeeDraftHandler.ScopeBlockedReason);
        Assert.Equal(
            "mod0251_workflow_decision_consumption_not_enabled",
            ConsumeWorkflowDecisionHandler.ScopeBlockedReason);
        Assert.Empty(typeof(SubmitEmployeeDraftHandler).GetConstructors().Single().GetParameters());
        Assert.Empty(typeof(ConsumeWorkflowDecisionHandler).GetConstructors().Single().GetParameters());
    }

    [Fact]
    public void WorkflowIntegrationContracts_DefineDecisionBoundary_AndLifecycleBlocker()
    {
        var invalid = new WorkflowApprovalDecisionRecordedMessage(
            Guid.Empty,
            Guid.Empty,
            "other.event",
            "approved",
            null,
            Guid.Empty,
            "",
            0,
            "",
            new Dictionary<string, string>());

        var errors = WorkflowApprovalDecisionConsumptionRules.ValidateEnvelope(invalid);

        Assert.Contains("tenant_required", errors);
        Assert.Contains("workflow_instance_required", errors);
        Assert.Contains("unexpected_workflow_event", errors);
        Assert.Contains("idempotency_key_required", errors);
        Assert.Contains("replay_key_required", errors);
        Assert.Contains("decision_version_required", errors);
        Assert.Contains("decision_by_required", errors);
        Assert.Contains("subject_module_mismatch", errors);
        Assert.Contains("subject_type_mismatch", errors);
        Assert.Contains("draft_session_required", errors);
        Assert.Contains("subject_id_required", errors);
        Assert.Contains("business_key_required", errors);
        Assert.Equal("MOD-0021", EmployeeActivationAuditReadiness.RequiredAuditOwner);
        Assert.False(EmployeeActivationAuditReadiness.GovernedAuditAppendReady);
        Assert.Contains("not enabled", EmployeeActivationAuditReadiness.Blocker);
    }

    [Fact]
    public void SubmitAndWorkflowDecisionEndpoints_RemainRoutesButAreLifecycleBoundaryOnly()
    {
        var submit = typeof(EmployeeDraftsController).GetMethod(nameof(EmployeeDraftsController.Submit));
        var workflow = typeof(EmployeeDraftsController).GetMethod(nameof(EmployeeDraftsController.ConsumeWorkflowDecision));

        Assert.NotNull(submit);
        Assert.Equal("{draftSessionId:guid}/submit", Assert.Single(submit!.GetCustomAttributes<HttpPostAttribute>()).Template);
        Assert.Equal("mod0251.employee.submit", Assert.Single(submit.GetCustomAttributes<Diten.HcmService.Infrastructure.Authorization.HasPermissionAttribute>()).Permission);

        Assert.NotNull(workflow);
        Assert.Equal("workflow-decisions", Assert.Single(workflow!.GetCustomAttributes<HttpPostAttribute>()).Template);
    }

    private static void AssertRequiresConcurrencyAndIdempotency<T>(AbstractValidator<T> validator, T request)
    {
        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ETag");
        Assert.Contains(result.Errors, error => error.PropertyName == "IdempotencyKey");
    }
}
