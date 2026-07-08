using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class EmployeeAuditAdapterContractTests
{
    [Fact]
    public void DraftAuditEventMapsToMod0021AppendShape_WithSafeMetadata()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var mapped = EmployeeAuditAdapterMapper.MapDraftEvent(
            new DraftAuditEvent(
                "employee_draft.updated",
                tenantId,
                actorId.ToString(),
                draftId,
                correlationId.ToString(),
                "hash-1",
                new Dictionary<string, string>
                {
                    ["step_code"] = "employment",
                    ["version"] = "2"
                }),
            DateTimeOffset.Parse("2026-06-20T00:00:00Z"));

        Assert.Equal(correlationId, mapped.CorrelationId);
        Assert.Equal("employee_draft.updated", mapped.RequestType);
        Assert.Equal("TenantUser", mapped.ActorType);
        Assert.Equal(actorId, mapped.ActorId);
        Assert.Equal(tenantId, mapped.TargetTenantId);
        Assert.Equal("Diten.HcmService", mapped.SourceService);
        Assert.Equal("MOD-0251", mapped.SourceModule);
        Assert.Equal("System", mapped.Category);
        Assert.Equal("EmployeeDraftSession", mapped.EntityType);
        Assert.Equal(draftId, mapped.EntityId);
        Assert.Equal("Update", mapped.Operation);
        Assert.Equal("Succeeded", mapped.Outcome);
        Assert.False(mapped.IsAuthoritativePersistence);
        Assert.True(EmployeeAuditAdapterMapper.IsSafeMetadata(mapped.Metadata));
    }

    [Fact]
    public void EmployeeAuditPayloadMapsCategoryEntityOperationAndOutcome()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        var mapped = EmployeeAuditAdapterMapper.MapEmployeeEvent(
            EmployeeAuditPayloadBuilder.AccessDenied(
                tenantId,
                actorId,
                "mod0251.employee.view_sensitive",
                "Employee",
                employeeId,
                Guid.NewGuid().ToString()));

        Assert.Equal(EmployeeAuditEventNames.AccessDenied, mapped.RequestType);
        Assert.Equal("Security", mapped.Category);
        Assert.Equal("AccessDenied", mapped.EntityType);
        Assert.Equal(employeeId, mapped.EntityId);
        Assert.Equal("PermissionDenied", mapped.Operation);
        Assert.Equal("Denied", mapped.Outcome);
        Assert.Equal("Diten.HcmService", mapped.SourceService);
        Assert.Equal("MOD-0251", mapped.SourceModule);
    }

    [Fact]
    public void AuditAdapterRejectsPiiSecretAndBeforeAfterMetadataKeys()
    {
        var unsafeDraft = new DraftAuditEvent(
            "employee_draft.updated",
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString(),
            "hash-1",
            new Dictionary<string, string>
            {
                ["legal_first_name"] = "redacted-in-test"
            });

        Assert.Throws<ArgumentException>(() => EmployeeAuditAdapterMapper.MapDraftEvent(unsafeDraft));

        Assert.False(EmployeeAuditAdapterMapper.IsSafeMetadata(new Dictionary<string, object?>
        {
            ["government_identifier_token"] = "redacted-in-test"
        }));

        Assert.False(EmployeeAuditAdapterMapper.IsSafeMetadata(new Dictionary<string, object?>
        {
            ["before"] = "redacted-in-test"
        }));
    }

    [Fact]
    public void ProfileUpdateMappingCarriesChangedFieldNamesOnly()
    {
        var mapped = EmployeeAuditAdapterMapper.MapEmployeeEvent(
            EmployeeAuditPayloadBuilder.ProfileUpdated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid().ToString(),
                "hash-2",
                ["legal_first_name", "work_email"],
                4));

        Assert.Equal(EmployeeAuditEventNames.ProfileUpdated, mapped.RequestType);
        Assert.Equal("Employee", mapped.EntityType);
        Assert.Equal("Update", mapped.Operation);
        Assert.True(mapped.Metadata.ContainsKey("changed_fields"));
        Assert.DoesNotContain(mapped.Metadata.Keys, key => key.Contains("before", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(mapped.Metadata.Keys, key => key.Contains("after", StringComparison.OrdinalIgnoreCase));
        Assert.True(EmployeeAuditAdapterMapper.IsSafeMetadata(mapped.Metadata));
    }
}
