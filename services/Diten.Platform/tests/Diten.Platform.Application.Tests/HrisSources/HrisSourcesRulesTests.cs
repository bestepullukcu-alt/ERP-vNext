using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.HrisSources;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.HrisSources.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.HrisSources.Queries;
using Diten.Platform.Application.Features.HrisSources.Validators;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.HrisSources;

public sealed class HrisSourcesRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Create_sets_tenant_server_side_and_request_does_not_expose_tenant_id()
    {
        var repository = new InMemoryHrisSourceRepository(TenantId);
        var handler = new CreateHrisSourceProfileHandler(repository, TenantContext(TenantId));

        var response = await handler.Handle(new CreateHrisSourceProfileCommand(CreateRequest(code: " wd-main ")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.DoesNotContain(typeof(HrisSourceProfileCreateRequest).GetProperties(), property => property.Name == "TenantId");
        var created = Assert.Single(repository.SourceProfiles);
        Assert.Equal(TenantId, created.TenantId);
        Assert.Equal("WD-MAIN", created.Code);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_current_tenant()
    {
        var repository = new InMemoryHrisSourceRepository(TenantId);
        repository.Add(SourceProfile("WD-MAIN", TenantId));
        var handler = new CreateHrisSourceProfileHandler(repository, TenantContext(TenantId));

        var response = await handler.Handle(new CreateHrisSourceProfileCommand(CreateRequest(code: " wd-main ")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_cross_tenant_source()
    {
        var repository = new InMemoryHrisSourceRepository(TenantId);
        var otherTenantSource = SourceProfile("SF-MAIN", OtherTenantId);
        repository.Add(otherTenantSource);
        var handler = new GetHrisSourceProfileByIdHandler(repository);

        var response = await handler.Handle(new GetHrisSourceProfileByIdQuery(otherTenantSource.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Archive_soft_deletes_source_when_no_active_identifier_maps_exist()
    {
        var repository = new InMemoryHrisSourceRepository(TenantId);
        var source = SourceProfile("ORACLE-MAIN", TenantId);
        repository.Add(source);
        var handler = new ArchiveHrisSourceProfileHandler(repository);

        var response = await handler.Handle(new ArchiveHrisSourceProfileCommand(source.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.True(source.IsDeleted);
        Assert.NotNull(source.DeletedAt);
    }

    [Fact]
    public async Task Archive_rejects_source_with_active_identifier_maps()
    {
        var repository = new InMemoryHrisSourceRepository(TenantId);
        var source = SourceProfile("WD-MAIN", TenantId);
        repository.Add(source);
        repository.AddIdentifierMap(new HrisExternalIdentifierMap
        {
            TenantId = TenantId,
            SourceProfileId = source.Id,
            ExternalObjectType = HrisExternalObjectType.Employee,
            ExternalObjectId = "E-1",
            InternalReferenceType = HrisInternalReferenceType.Person,
            MappingState = HrisMappingState.Mapped
        });
        var handler = new ArchiveHrisSourceProfileHandler(repository);

        var response = await handler.Handle(new ArchiveHrisSourceProfileCommand(source.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.False(source.IsDeleted);
    }

    [Fact]
    public void Create_validator_rejects_raw_secret_and_payload_values()
    {
        var validator = new CreateHrisSourceProfileValidator();

        var secretResult = validator.Validate(new CreateHrisSourceProfileCommand(CreateRequest(connectionReference: "Bearer eyJhbGciOi.secret.token")));
        var payloadResult = validator.Validate(new CreateHrisSourceProfileCommand(CreateRequest(connectionReference: "{\"employee\":\"raw\"}")));

        Assert.False(secretResult.IsValid);
        Assert.False(payloadResult.IsValid);
    }

    [Fact]
    public void Mapping_validator_rejects_payroll_or_time_attendance_object_types()
    {
        var validator = new UpdateHrisMappingProfileValidator();
        var request = new HrisMappingProfileRequest(
            "MAIN",
            "Main mapping",
            "1.0.0",
            "schema-ref/hris/main",
            null,
            true,
            [new HrisExternalIdentifierMapRequest((HrisExternalObjectType)99, "PAY-1", HrisInternalReferenceType.Person, null, HrisMappingState.Unmapped, null)]);

        var result = validator.Validate(new UpdateHrisMappingProfileCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Mapping_update_rejects_cross_tenant_mod_0288_internal_reference_fail_closed()
    {
        var source = SourceProfile("WD-MAIN", TenantId);
        var repository = new InMemoryHrisSourceRepository(TenantId);
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
        var handler = new UpdateHrisMappingProfileHandler(repository, orgUnits, new InMemoryPositionRepository(TenantId));
        var request = MappingRequest(new HrisExternalIdentifierMapRequest(
            HrisExternalObjectType.Org,
            "ORG-1",
            HrisInternalReferenceType.OrganizationUnit,
            otherTenantOrgUnit.Id,
            HrisMappingState.Mapped,
            null));

        var response = await handler.Handle(new UpdateHrisMappingProfileCommand(source.Id, request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(repository.IdentifierMaps);
    }

    [Fact]
    public async Task Mapping_update_rejects_unresolved_reference_as_mapped()
    {
        var source = SourceProfile("WD-MAIN", TenantId);
        var repository = new InMemoryHrisSourceRepository(TenantId);
        repository.Add(source);
        var handler = new UpdateHrisMappingProfileHandler(
            repository,
            new InMemoryOrganizationUnitRepository(TenantId),
            new InMemoryPositionRepository(TenantId));
        var request = MappingRequest(new HrisExternalIdentifierMapRequest(
            HrisExternalObjectType.Job,
            "JOB-1",
            HrisInternalReferenceType.Position,
            null,
            HrisMappingState.Mapped,
            null));

        var response = await handler.Handle(new UpdateHrisMappingProfileCommand(source.Id, request), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(repository.IdentifierMaps);
    }

    [Fact]
    public async Task Mapping_replace_soft_deletes_previous_identifier_maps_with_deleted_at()
    {
        var source = SourceProfile("WD-MAIN", TenantId);
        var repository = new InMemoryHrisSourceRepository(TenantId);
        repository.Add(source);
        repository.AddIdentifierMap(new HrisExternalIdentifierMap
        {
            TenantId = TenantId,
            SourceProfileId = source.Id,
            ExternalObjectType = HrisExternalObjectType.Employee,
            ExternalObjectId = "E-OLD",
            InternalReferenceType = HrisInternalReferenceType.Person,
            MappingState = HrisMappingState.Unmapped
        });
        var orgUnits = new InMemoryOrganizationUnitRepository(TenantId);
        var orgUnit = new OrganizationUnit
        {
            TenantId = TenantId,
            Code = "ROOT",
            Name = "Root",
            LegalEntityId = Guid.NewGuid()
        };
        orgUnits.Add(orgUnit);
        var handler = new UpdateHrisMappingProfileHandler(repository, orgUnits, new InMemoryPositionRepository(TenantId));
        var request = MappingRequest(new HrisExternalIdentifierMapRequest(
            HrisExternalObjectType.Org,
            "ORG-1",
            HrisInternalReferenceType.OrganizationUnit,
            orgUnit.Id,
            HrisMappingState.Mapped,
            null));

        var response = await handler.Handle(new UpdateHrisMappingProfileCommand(source.Id, request), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var oldMap = Assert.Single(repository.IdentifierMaps, x => x.ExternalObjectId == "E-OLD");
        Assert.True(oldMap.IsDeleted);
        Assert.NotNull(oldMap.DeletedAt);
    }

    [Fact]
    public void Checkpoint_validator_rejects_raw_payload_or_secret_leakage()
    {
        var validator = new RecordHrisSyncCheckpointValidator();
        var request = new HrisSyncCheckpointRequest(
            "run-1",
            HrisSyncMode.Manual,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            HrisSyncStatus.Failed,
            "{\"cursor\":\"raw-payload\"}",
            1,
            0,
            1,
            "client_secret=abc");

        var result = validator.Validate(new RecordHrisSyncCheckpointCommand(Guid.NewGuid(), request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Controller_uses_platform_actor_policy_and_hardened_permissions()
    {
        var controllerType = typeof(HrisSourcesController);
        var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("PlatformActor", authorize.Policy);

        AssertHasPermission(nameof(HrisSourcesController.GetAll), "platform.hris-sources.read");
        AssertHasPermission(nameof(HrisSourcesController.Create), "platform.hris-sources.create");
        AssertHasPermission(nameof(HrisSourcesController.Update), "platform.hris-sources.update");
        AssertHasPermission(nameof(HrisSourcesController.Archive), "platform.hris-sources.archive");
        AssertHasPermission(nameof(HrisSourcesController.ValidateConnection), "platform.hris-sources.validate-connection");
        AssertHasPermission(nameof(HrisSourcesController.UpdateMappingProfile), "platform.hris-sources.update-mapping");
        AssertHasPermission(nameof(HrisSourcesController.GetSyncCheckpoint), "platform.hris-sources.read-health");
        AssertHasPermission(nameof(HrisSourcesController.GetHealth), "platform.hris-sources.read-health");
    }

    private static HrisSourceProfileCreateRequest CreateRequest(string code = "WD-MAIN", string connectionReference = "vault://hris/wd-main") =>
        new(
            code,
            "Workday Main",
            HrisProviderKind.Workday,
            "tenant-key",
            connectionReference,
            HrisSourceLifecycleState.Draft,
            HrisSyncMode.Manual,
            null,
            null);

    private static HrisSourceProfile SourceProfile(string code, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = code,
            ProviderKind = HrisProviderKind.Other,
            ConnectionProfileReference = "vault://hris/test",
            LifecycleState = HrisSourceLifecycleState.Draft,
            SyncMode = HrisSyncMode.Manual
        };

    private static HrisMappingProfileRequest MappingRequest(HrisExternalIdentifierMapRequest identifierMap) =>
        new(
            "MAIN",
            "Main mapping",
            "1.0.0",
            "schema-ref/hris/main",
            null,
            true,
            [identifierMap]);

    private static ITenantContext TenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return tenantContext;
    }

    private static void AssertHasPermission(string methodName, string expectedPermission)
    {
        var method = typeof(HrisSourcesController).GetMethods()
            .Single(x => x.Name == methodName);
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        var permissionField = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(expectedPermission, permissionField?.GetValue(attribute));
    }
}
