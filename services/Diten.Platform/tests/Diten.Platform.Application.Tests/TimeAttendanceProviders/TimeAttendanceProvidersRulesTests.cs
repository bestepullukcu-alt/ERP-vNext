using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TimeAttendanceProviders;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeAttendanceProviders;

public sealed class TimeAttendanceProvidersRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Create_sets_tenant_server_side_and_request_does_not_expose_tenant_id()
    {
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        var handler = new CreateTimeAttendanceProviderProfileHandler(repository, TenantContext(TenantId));

        var response = await handler.Handle(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(code: " ukg-main ")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.DoesNotContain(typeof(TimeAttendanceProviderProfileRequest).GetProperties(), property => property.Name == "TenantId");
        var created = Assert.Single(repository.Profiles);
        Assert.Equal(TenantId, created.TenantId);
        Assert.Equal("UKG-MAIN", created.Code);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_current_tenant()
    {
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        repository.Add(ProviderProfile("UKG-MAIN", TenantId));
        var handler = new CreateTimeAttendanceProviderProfileHandler(repository, TenantContext(TenantId));

        var response = await handler.Handle(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(code: " ukg-main ")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_cross_tenant_provider()
    {
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        var otherTenantProvider = ProviderProfile("KRONOS-MAIN", OtherTenantId);
        repository.Add(otherTenantProvider);
        var handler = new GetTimeAttendanceProviderProfileByIdHandler(repository);

        var response = await handler.Handle(new GetTimeAttendanceProviderProfileByIdQuery(otherTenantProvider.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_404_for_cross_tenant_provider()
    {
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        var otherTenantProvider = ProviderProfile("UKG-OTHER", OtherTenantId);
        repository.Add(otherTenantProvider);
        var handler = new UpdateTimeAttendanceProviderProfileHandler(repository);

        var response = await handler.Handle(new UpdateTimeAttendanceProviderProfileCommand(otherTenantProvider.Id, ProfileRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Archive_soft_deletes_provider_and_sets_deleted_at()
    {
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        var provider = ProviderProfile("UKG-MAIN", TenantId);
        repository.Add(provider);
        var handler = new ArchiveTimeAttendanceProviderProfileHandler(repository);

        var response = await handler.Handle(new ArchiveTimeAttendanceProviderProfileCommand(provider.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.True(provider.IsDeleted);
        Assert.NotNull(provider.DeletedAt);
    }

    [Fact]
    public void Create_validator_rejects_raw_secret_and_time_attendance_payload_values()
    {
        var validator = new CreateTimeAttendanceProviderProfileValidator();

        var secretResult = validator.Validate(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(externalProviderAccountId: "Bearer eyJhbGciOi.secret.token")));
        var payloadResult = validator.Validate(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(notes: "{\"attendance\":{\"clock\":\"in\"}}")));

        Assert.False(secretResult.IsValid);
        Assert.False(payloadResult.IsValid);
    }

    [Fact]
    public void Create_validator_rejects_biometric_geolocation_and_payroll_markers()
    {
        var validator = new CreateTimeAttendanceProviderProfileValidator();

        var biometricResult = validator.Validate(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(notes: "fingerprint template")));
        var geolocationResult = validator.Validate(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(notes: "latitude=40 longitude=29")));
        var payrollResult = validator.Validate(new CreateTimeAttendanceProviderProfileCommand(ProfileRequest(notes: "payroll gross export")));

        Assert.False(biometricResult.IsValid);
        Assert.False(geolocationResult.IsValid);
        Assert.False(payrollResult.IsValid);
    }

    [Fact]
    public void Contract_validator_rejects_internal_attendance_leave_roster_payroll_object_types()
    {
        var validator = new UpdateTimeAttendanceContractProfileValidator();
        var request = ContractRequest([
            TimeAttendanceSupportedObjectType.Employee,
            TimeAttendanceSupportedObjectType.LeaveAccrual,
            TimeAttendanceSupportedObjectType.RosterOptimization,
            TimeAttendanceSupportedObjectType.PayrollCalculation]);

        var result = validator.Validate(new UpdateTimeAttendanceContractProfileCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Employee_reference_map_rejects_cross_tenant_mod_0288_reference_fail_closed()
    {
        var provider = ProviderProfile("UKG-MAIN", TenantId);
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        repository.Add(provider);
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var otherTenantOrgUnit = new OrganizationUnit
        {
            TenantId = OtherTenantId,
            Code = "OTHER",
            Name = "Other Tenant Org",
            LegalEntityId = Guid.NewGuid()
        };
        orgUnits.Add(otherTenantOrgUnit);
        var handler = new CreateTimeAttendanceEmployeeReferenceMapHandler(repository, orgUnits, new InMemoryPositionRepository(TenantId));
        var request = new TimeAttendanceEmployeeReferenceMapRequest("EXT-E-1", null, null, otherTenantOrgUnit.Id, null, TimeAttendanceReferenceMappingState.Mapped);

        var response = await handler.Handle(new CreateTimeAttendanceEmployeeReferenceMapCommand(provider.Id, request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(repository.EmployeeMaps);
    }

    [Fact]
    public async Task Employee_reference_map_rejects_unavailable_hris_and_person_validators_fail_closed()
    {
        var provider = ProviderProfile("UKG-MAIN", TenantId);
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        repository.Add(provider);
        var handler = new CreateTimeAttendanceEmployeeReferenceMapHandler(
            repository,
            new InMemoryOrganizationUnitRepository(TenantId),
            new InMemoryPositionRepository(TenantId));

        var hrisResponse = await handler.Handle(
            new CreateTimeAttendanceEmployeeReferenceMapCommand(
                provider.Id,
                new TimeAttendanceEmployeeReferenceMapRequest("EXT-HRIS", Guid.NewGuid(), null, null, null, TimeAttendanceReferenceMappingState.Mapped)),
            CancellationToken.None);
        var personResponse = await handler.Handle(
            new CreateTimeAttendanceEmployeeReferenceMapCommand(
                provider.Id,
                new TimeAttendanceEmployeeReferenceMapRequest("EXT-PERSON", null, Guid.NewGuid(), null, null, TimeAttendanceReferenceMappingState.Mapped)),
            CancellationToken.None);

        Assert.False(hrisResponse.IsSuccessful);
        Assert.Equal(404, hrisResponse.StatusCode);
        Assert.False(personResponse.IsSuccessful);
        Assert.Equal(404, personResponse.StatusCode);
        Assert.Empty(repository.EmployeeMaps);
    }

    [Fact]
    public async Task Employee_reference_map_rejects_unresolved_reference_as_mapped()
    {
        var provider = ProviderProfile("UKG-MAIN", TenantId);
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        repository.Add(provider);
        var handler = new CreateTimeAttendanceEmployeeReferenceMapHandler(
            repository,
            new InMemoryOrganizationUnitRepository(TenantId),
            new InMemoryPositionRepository(TenantId));
        var request = new TimeAttendanceEmployeeReferenceMapRequest("EXT-E-1", null, null, null, null, TimeAttendanceReferenceMappingState.Mapped);

        var response = await handler.Handle(new CreateTimeAttendanceEmployeeReferenceMapCommand(provider.Id, request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(repository.EmployeeMaps);
    }

    [Fact]
    public async Task Employee_reference_map_accepts_same_tenant_position_reference_as_mapped()
    {
        var provider = ProviderProfile("UKG-MAIN", TenantId);
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        repository.Add(provider);
        var positions = new InMemoryPositionRepository(TenantId);
        var position = new Position
        {
            TenantId = TenantId,
            Code = "POS-1",
            Name = "Time Attendance Specialist",
            OrganizationUnitId = Guid.NewGuid()
        };
        positions.Add(position);
        var handler = new CreateTimeAttendanceEmployeeReferenceMapHandler(
            repository,
            new InMemoryOrganizationUnitRepository(TenantId),
            positions);
        var request = new TimeAttendanceEmployeeReferenceMapRequest("EXT-E-1", null, null, null, position.Id, TimeAttendanceReferenceMappingState.Mapped);

        var response = await handler.Handle(new CreateTimeAttendanceEmployeeReferenceMapCommand(provider.Id, request), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var map = Assert.Single(repository.EmployeeMaps);
        Assert.Equal(TimeAttendanceReferenceMappingState.Mapped, map.MappingState);
        Assert.NotNull(map.LastValidatedAt);
    }

    [Fact]
    public void Event_validator_rejects_leave_payroll_and_roster_event_types()
    {
        var validator = new RecordTimeAttendanceEventReferenceValidator();
        var providerId = Guid.NewGuid();

        var leaveResult = validator.Validate(new RecordTimeAttendanceEventReferenceCommand(providerId, EventRequest(TimeAttendanceEventType.LeaveRequest)));
        var payrollResult = validator.Validate(new RecordTimeAttendanceEventReferenceCommand(providerId, EventRequest(TimeAttendanceEventType.PayrollRun)));
        var rosterResult = validator.Validate(new RecordTimeAttendanceEventReferenceCommand(providerId, EventRequest(TimeAttendanceEventType.RosterOptimization)));

        Assert.False(leaveResult.IsValid);
        Assert.False(payrollResult.IsValid);
        Assert.False(rosterResult.IsValid);
    }

    [Fact]
    public async Task Checkpoint_and_health_return_404_for_cross_tenant_provider()
    {
        var repository = new InMemoryTimeAttendanceProviderRepository(TenantId);
        var otherTenantProvider = ProviderProfile("UKG-OTHER", OtherTenantId);
        repository.Add(otherTenantProvider);
        var checkpointHandler = new RecordTimeAttendanceSyncCheckpointHandler(repository);
        var healthHandler = new RecordTimeAttendanceProviderHealthSnapshotHandler(repository);

        var checkpointResponse = await checkpointHandler.Handle(new RecordTimeAttendanceSyncCheckpointCommand(otherTenantProvider.Id, CheckpointRequest()), CancellationToken.None);
        var healthResponse = await healthHandler.Handle(new RecordTimeAttendanceProviderHealthSnapshotCommand(otherTenantProvider.Id, HealthRequest()), CancellationToken.None);

        Assert.False(checkpointResponse.IsSuccessful);
        Assert.Equal(404, checkpointResponse.StatusCode);
        Assert.False(healthResponse.IsSuccessful);
        Assert.Equal(404, healthResponse.StatusCode);
    }

    [Fact]
    public void Controller_uses_platform_actor_policy_and_hardened_permissions()
    {
        var controllerType = typeof(TimeAttendanceProvidersController);
        var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("PlatformActor", authorize.Policy);

        AssertHasPermission(nameof(TimeAttendanceProvidersController.GetAll), "platform.time-attendance-providers.read");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.Create), "platform.time-attendance-providers.create");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.Update), "platform.time-attendance-providers.update");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.Archive), "platform.time-attendance-providers.archive");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.UpdateContractProfile), "platform.time-attendance-providers.update-contract");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.CreateEmployeeReferenceMap), "platform.time-attendance-providers.map-employee-reference");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.RecordEventReference), "platform.time-attendance-providers.record-event");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.RecordAttendanceSummaryReference), "platform.time-attendance-providers.record-summary");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.RecordSyncCheckpoint), "platform.time-attendance-providers.record-checkpoint");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.RecordHealthSnapshot), "platform.time-attendance-providers.record-health");
        AssertHasPermission(nameof(TimeAttendanceProvidersController.GetHealth), "platform.time-attendance-providers.read-health");
    }

    private static TimeAttendanceProviderProfileRequest ProfileRequest(
        string code = "UKG-MAIN",
        string externalProviderAccountId = "external-time-attendance-main",
        string? notes = null) =>
        new(
            code,
            "UKG Main Time Attendance",
            TimeAttendanceProviderFamily.UKG,
            externalProviderAccountId,
            TimeAttendanceProviderLifecycleState.Draft,
            "vault://time-attendance/ukg-main",
            "integration-ops",
            notes);

    private static TimeAttendanceContractProfileRequest ContractRequest(IReadOnlyList<TimeAttendanceSupportedObjectType> objectTypes) =>
        new(
            "1.0.0",
            DateTimeOffset.UtcNow,
            null,
            objectTypes,
            ["READY", "FAILED"],
            [],
            null);

    private static TimeAttendanceEventReferenceRequest EventRequest(TimeAttendanceEventType eventType) =>
        new(
            "EXT-EVT-1",
            eventType,
            "EXT-E-1",
            DateTimeOffset.UtcNow,
            "UTC",
            TimeAttendanceProcessingState.Accepted,
            Guid.NewGuid().ToString("N"),
            null);

    private static TimeAttendanceSyncCheckpointRequest CheckpointRequest() =>
        new(
            "sync-1",
            TimeAttendanceSyncMode.Manual,
            DateTimeOffset.UtcNow,
            null,
            "checkpoint-1",
            1,
            1,
            0,
            TimeAttendanceSyncStatus.Completed,
            null);

    private static TimeAttendanceProviderHealthSnapshotRequest HealthRequest() =>
        new(TimeAttendanceProviderHealthState.Ready, DateTimeOffset.UtcNow, "healthy", null);

    private static TimeAttendanceExternalProviderProfile ProviderProfile(string code, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            ProviderFamily = TimeAttendanceProviderFamily.OtherExternalTimeAttendance,
            ExternalProviderAccountId = "external-" + code.ToLowerInvariant(),
            LifecycleState = TimeAttendanceProviderLifecycleState.Draft,
            ConnectionProfileReference = "vault://time-attendance/test"
        };

    private static ITenantContext TenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return tenantContext;
    }

    private static void AssertHasPermission(string methodName, string expectedPermission)
    {
        var method = typeof(TimeAttendanceProvidersController).GetMethods()
            .Single(x => x.Name == methodName);
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        var permissionField = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(expectedPermission, permissionField?.GetValue(attribute));
    }
}
