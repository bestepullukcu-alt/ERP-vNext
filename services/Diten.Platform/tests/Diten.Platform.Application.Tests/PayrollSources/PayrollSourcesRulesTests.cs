using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PayrollSources;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.PayrollSources.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.PayrollSources.Queries;
using Diten.Platform.Application.Features.PayrollSources.Validators;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.PayrollSources;

public sealed class PayrollSourcesRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Create_sets_tenant_server_side_and_request_does_not_expose_tenant_id()
    {
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        var handler = new CreatePayrollExternalSystemProfileHandler(repository, TenantContext(TenantId));

        var response = await handler.Handle(new CreatePayrollExternalSystemProfileCommand(ProfileRequest(code: " adp-main ")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.DoesNotContain(typeof(PayrollExternalSystemProfileRequest).GetProperties(), property => property.Name == "TenantId");
        var created = Assert.Single(repository.Profiles);
        Assert.Equal(TenantId, created.TenantId);
        Assert.Equal("ADP-MAIN", created.Code);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_current_tenant()
    {
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        repository.Add(SourceProfile("ADP-MAIN", TenantId));
        var handler = new CreatePayrollExternalSystemProfileHandler(repository, TenantContext(TenantId));

        var response = await handler.Handle(new CreatePayrollExternalSystemProfileCommand(ProfileRequest(code: " adp-main ")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_cross_tenant_source()
    {
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        var otherTenantSource = SourceProfile("WORKDAY-PAY", OtherTenantId);
        repository.Add(otherTenantSource);
        var handler = new GetPayrollExternalSystemProfileByIdHandler(repository);

        var response = await handler.Handle(new GetPayrollExternalSystemProfileByIdQuery(otherTenantSource.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_404_for_cross_tenant_source()
    {
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        var otherTenantSource = SourceProfile("SAP-PAY", OtherTenantId);
        repository.Add(otherTenantSource);
        var handler = new UpdatePayrollExternalSystemProfileHandler(repository);

        var response = await handler.Handle(new UpdatePayrollExternalSystemProfileCommand(otherTenantSource.Id, ProfileRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Archive_soft_deletes_source_and_sets_deleted_at()
    {
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        var source = SourceProfile("ADP-MAIN", TenantId);
        repository.Add(source);
        var handler = new ArchivePayrollExternalSystemProfileHandler(repository);

        var response = await handler.Handle(new ArchivePayrollExternalSystemProfileCommand(source.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.True(source.IsDeleted);
        Assert.NotNull(source.DeletedAt);
    }

    [Fact]
    public void Create_validator_rejects_raw_secret_and_payroll_payload_values()
    {
        var validator = new CreatePayrollExternalSystemProfileValidator();

        var secretResult = validator.Validate(new CreatePayrollExternalSystemProfileCommand(ProfileRequest(externalPayrollSystemId: "Bearer eyJhbGciOi.secret.token")));
        var payloadResult = validator.Validate(new CreatePayrollExternalSystemProfileCommand(ProfileRequest(notes: "{\"payroll\":{\"gross\":1000}}")));

        Assert.False(secretResult.IsValid);
        Assert.False(payloadResult.IsValid);
    }

    [Fact]
    public void Contract_validator_rejects_payroll_time_attendance_ownership_object_types()
    {
        var validator = new UpdatePayrollContractProfileValidator();
        var request = ContractRequest([PayrollSupportedObjectType.Employee, PayrollSupportedObjectType.TimeAttendance]);

        var result = validator.Validate(new UpdatePayrollContractProfileCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Employee_reference_map_rejects_cross_tenant_mod_0288_reference_fail_closed()
    {
        var source = SourceProfile("ADP-MAIN", TenantId);
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        repository.Add(source);
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var otherTenantOrgUnit = new OrganizationUnit
        {
            TenantId = OtherTenantId,
            Code = "OTHER",
            Name = "Other Tenant Org",
            LegalEntityId = Guid.NewGuid()
        };
        orgUnits.Add(otherTenantOrgUnit);
        var handler = new CreatePayrollEmployeeReferenceMapHandler(repository, orgUnits, new InMemoryPositionRepository(TenantId));
        var request = new PayrollEmployeeReferenceMapRequest("EXT-E-1", null, otherTenantOrgUnit.Id, null, PayrollReferenceMappingState.Mapped);

        var response = await handler.Handle(new CreatePayrollEmployeeReferenceMapCommand(source.Id, request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(repository.EmployeeMaps);
    }

    [Fact]
    public async Task Employee_reference_map_rejects_unresolved_reference_as_mapped()
    {
        var source = SourceProfile("ADP-MAIN", TenantId);
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        repository.Add(source);
        var handler = new CreatePayrollEmployeeReferenceMapHandler(
            repository,
            new InMemoryOrganizationUnitRepository(TenantId),
            new InMemoryPositionRepository(TenantId));
        var request = new PayrollEmployeeReferenceMapRequest("EXT-E-1", null, null, null, PayrollReferenceMappingState.Mapped);

        var response = await handler.Handle(new CreatePayrollEmployeeReferenceMapCommand(source.Id, request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(repository.EmployeeMaps);
    }

    [Fact]
    public async Task Employee_reference_map_accepts_same_tenant_position_reference_as_mapped()
    {
        var source = SourceProfile("ADP-MAIN", TenantId);
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        repository.Add(source);
        var positions = new InMemoryPositionRepository(TenantId);
        var position = new Position
        {
            TenantId = TenantId,
            Code = "POS-1",
            Name = "Payroll Specialist",
            OrganizationUnitId = Guid.NewGuid()
        };
        positions.Add(position);
        var handler = new CreatePayrollEmployeeReferenceMapHandler(
            repository,
            new InMemoryOrganizationUnitRepository(TenantId),
            positions);
        var request = new PayrollEmployeeReferenceMapRequest("EXT-E-1", null, null, position.Id, PayrollReferenceMappingState.Mapped);

        var response = await handler.Handle(new CreatePayrollEmployeeReferenceMapCommand(source.Id, request), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var map = Assert.Single(repository.EmployeeMaps);
        Assert.Equal(PayrollReferenceMappingState.Mapped, map.MappingState);
        Assert.NotNull(map.LastValidatedAt);
    }

    [Fact]
    public async Task Contract_update_rejects_stale_contract_version_change()
    {
        var source = SourceProfile("ADP-MAIN", TenantId);
        var repository = new InMemoryPayrollSourceRepository(TenantId);
        repository.Add(source);
        repository.Add(new PayrollContractProfile
        {
            TenantId = TenantId,
            PayrollExternalSystemProfileId = source.Id,
            ContractVersion = "1.0.0",
            EffectiveFrom = DateTimeOffset.UtcNow,
            SupportedObjectTypes = [PayrollSupportedObjectType.Employee],
            StatusVocabulary = ["READY"]
        });
        var handler = new UpdatePayrollContractProfileHandler(repository);

        var response = await handler.Handle(new UpdatePayrollContractProfileCommand(source.Id, ContractRequest([PayrollSupportedObjectType.Employee], version: "2.0.0")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void Controller_uses_platform_actor_policy_and_hardened_permissions()
    {
        var controllerType = typeof(PayrollSourcesController);
        var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("PlatformActor", authorize.Policy);

        AssertHasPermission(nameof(PayrollSourcesController.GetAll), "platform.payroll-sources.read");
        AssertHasPermission(nameof(PayrollSourcesController.Create), "platform.payroll-sources.create");
        AssertHasPermission(nameof(PayrollSourcesController.Update), "platform.payroll-sources.update");
        AssertHasPermission(nameof(PayrollSourcesController.Archive), "platform.payroll-sources.archive");
        AssertHasPermission(nameof(PayrollSourcesController.UpdateContractProfile), "platform.payroll-sources.update-contract");
        AssertHasPermission(nameof(PayrollSourcesController.CreateEmployeeReferenceMap), "platform.payroll-sources.map-employee-reference");
        AssertHasPermission(nameof(PayrollSourcesController.RecordCycleReference), "platform.payroll-sources.record-cycle");
        AssertHasPermission(nameof(PayrollSourcesController.RecordResultReference), "platform.payroll-sources.record-result");
        AssertHasPermission(nameof(PayrollSourcesController.RecordHealthSnapshot), "platform.payroll-sources.record-health");
        AssertHasPermission(nameof(PayrollSourcesController.GetHealth), "platform.payroll-sources.read-health");
    }

    private static PayrollExternalSystemProfileRequest ProfileRequest(
        string code = "ADP-MAIN",
        string externalPayrollSystemId = "external-payroll-main",
        string? notes = null) =>
        new(
            code,
            "ADP Main Payroll",
            PayrollProviderFamily.ADP,
            externalPayrollSystemId,
            PayrollSourceLifecycleState.Draft,
            "vault://payroll/adp-main",
            "integration-ops",
            notes);

    private static PayrollContractProfileRequest ContractRequest(
        IReadOnlyList<PayrollSupportedObjectType> objectTypes,
        string version = "1.0.0") =>
        new(
            version,
            DateTimeOffset.UtcNow,
            null,
            objectTypes,
            ["READY", "FAILED"],
            [],
            null);

    private static PayrollExternalSystemProfile SourceProfile(string code, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            ProviderFamily = PayrollProviderFamily.OtherExternalPayroll,
            ExternalPayrollSystemId = "external-" + code.ToLowerInvariant(),
            LifecycleState = PayrollSourceLifecycleState.Draft,
            ConnectionProfileReference = "vault://payroll/test"
        };

    private static ITenantContext TenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return tenantContext;
    }

    private static void AssertHasPermission(string methodName, string expectedPermission)
    {
        var method = typeof(PayrollSourcesController).GetMethods()
            .Single(x => x.Name == methodName);
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        var permissionField = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(expectedPermission, permissionField?.GetValue(attribute));
    }
}
