using System.Reflection;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.PersonReferenceDirectory;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Validators;
using Diten.Platform.Application.Tests.HrisSources;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.Platform.Application.Tests.PersonReferenceDirectory;

public sealed class PersonReferenceDirectoryRulesTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Create_sets_tenant_server_side_and_request_does_not_expose_tenant_id()
    {
        var fixture = CreateFixture();
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(new CreatePersonReferenceProjectionCommand(CreateRequest(code: " pr-001 ", hrisSourceProfileId: fixture.HrisSource.Id)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.DoesNotContain(typeof(PersonReferenceProjectionCreateRequest).GetProperties(), property => property.Name == "TenantId");
        var created = Assert.Single(fixture.PersonReferences.Projections);
        Assert.Equal(TenantId, created.TenantId);
        Assert.Equal("PR-001", created.Code);
    }

    [Fact]
    public async Task Create_rejects_duplicate_active_person_reference_code()
    {
        var fixture = CreateFixture();
        fixture.PersonReferences.Add(Projection("PR-001", fixture.HrisSource.Id, TenantId));
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(new CreatePersonReferenceProjectionCommand(CreateRequest(code: " pr-001 ", hrisSourceProfileId: fixture.HrisSource.Id)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_cross_tenant_projection()
    {
        var fixture = CreateFixture();
        var otherTenantProjection = Projection("OTHER", fixture.HrisSource.Id, OtherTenantId);
        fixture.PersonReferences.Add(otherTenantProjection);
        var handler = new GetPersonReferenceProjectionByIdHandler(fixture.PersonReferences);

        var response = await handler.Handle(new GetPersonReferenceProjectionByIdQuery(otherTenantProjection.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Archive_soft_deletes_projection_with_deleted_at()
    {
        var fixture = CreateFixture();
        var projection = Projection("PR-001", fixture.HrisSource.Id, TenantId);
        fixture.PersonReferences.Add(projection);
        var handler = new ArchivePersonReferenceProjectionHandler(fixture.PersonReferences);

        var response = await handler.Handle(new ArchivePersonReferenceProjectionCommand(projection.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.True(projection.IsDeleted);
        Assert.NotNull(projection.DeletedAt);
    }

    [Fact]
    public async Task Create_fails_closed_when_hris_source_is_missing_or_cross_tenant()
    {
        var fixture = CreateFixture(addHrisSource: false);
        fixture.HrisSources.Add(HrisSource(Guid.NewGuid(), OtherTenantId));
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(
            new CreatePersonReferenceProjectionCommand(CreateRequest(hrisSourceProfileId: fixture.HrisSources.SourceProfiles.Single().Id)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(fixture.PersonReferences.Projections);
    }

    [Fact]
    public async Task Create_fails_closed_when_organization_unit_is_cross_tenant()
    {
        var fixture = CreateFixture();
        var orgUnit = OrganizationUnit(OtherTenantId);
        fixture.OrganizationUnits.Add(orgUnit);
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(
            new CreatePersonReferenceProjectionCommand(CreateRequest(hrisSourceProfileId: fixture.HrisSource.Id, organizationUnitId: orgUnit.Id)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Create_fails_closed_when_position_is_cross_tenant()
    {
        var fixture = CreateFixture();
        var position = Position(OtherTenantId, Guid.NewGuid());
        fixture.Positions.Add(position);
        var handler = fixture.CreateHandler();

        var response = await handler.Handle(
            new CreatePersonReferenceProjectionCommand(CreateRequest(hrisSourceProfileId: fixture.HrisSource.Id, positionId: position.Id)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validated_state_requires_validated_same_tenant_correlation()
    {
        var fixture = CreateFixture();
        var projection = Projection("PR-001", fixture.HrisSource.Id, TenantId);
        fixture.PersonReferences.Add(projection);
        var handler = fixture.ValidateHandler();

        var response = await handler.Handle(new ValidatePersonReferenceProjectionCommand(projection.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(PersonReferenceState.Deferred, projection.ReferenceState);
        Assert.Null(projection.LastValidatedAt);
    }

    [Fact]
    public async Task Validated_state_succeeds_after_hris_correlation_and_org_position_validate()
    {
        var fixture = CreateFixture();
        var orgUnit = OrganizationUnit(TenantId);
        fixture.OrganizationUnits.Add(orgUnit);
        var position = Position(TenantId, orgUnit.Id);
        fixture.Positions.Add(position);
        var projection = Projection("PR-001", fixture.HrisSource.Id, TenantId, orgUnit.Id, position.Id);
        var correlation = Correlation(projection.Id, fixture.HrisSource.Id, TenantId, PersonReferenceCorrelationState.Validated);
        projection.PrimaryExternalCorrelationId = correlation.Id;
        fixture.PersonReferences.Add(projection);
        fixture.PersonReferences.AddCorrelation(correlation);
        var handler = fixture.ValidateHandler();

        var response = await handler.Handle(new ValidatePersonReferenceProjectionCommand(projection.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(PersonReferenceState.Validated, projection.ReferenceState);
        Assert.NotNull(projection.LastValidatedAt);
    }

    [Fact]
    public async Task Correlation_rejects_duplicate_active_key()
    {
        var fixture = CreateFixture();
        var projection = Projection("PR-001", fixture.HrisSource.Id, TenantId);
        fixture.PersonReferences.Add(projection);
        fixture.PersonReferences.AddCorrelation(Correlation(projection.Id, fixture.HrisSource.Id, TenantId, PersonReferenceCorrelationState.Validated, "CK-001"));
        var handler = fixture.CorrelationHandler();

        var response = await handler.Handle(
            new UpsertPersonReferenceExternalCorrelationCommand(projection.Id, null, CorrelationRequest(correlationKey: "CK-001")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public void Validators_reject_raw_payload_secret_pii_payroll_provider_and_tep_markers()
    {
        var createValidator = new CreatePersonReferenceProjectionValidator();
        var correlationValidator = new UpsertPersonReferenceExternalCorrelationValidator();
        var healthValidator = new RecordPersonReferenceDirectoryHealthValidator();

        var rawPayload = createValidator.Validate(new CreatePersonReferenceProjectionCommand(CreateRequest(displayName: "{\"employee\":\"raw\"}")));
        var rawSecret = correlationValidator.Validate(new UpsertPersonReferenceExternalCorrelationCommand(Guid.NewGuid(), null, CorrelationRequest(externalObjectReference: "Bearer eyJhbGciOi.secret.token")));
        var pii = createValidator.Validate(new CreatePersonReferenceProjectionCommand(CreateRequest(displayName: "dob: 1970-01-01")));
        var payroll = healthValidator.Validate(new RecordPersonReferenceDirectoryHealthCommand(new PersonReferenceDirectoryHealthRequest("health-1", 1, 1, 0, DateTimeOffset.UtcNow, "payroll bank tax payslip")));
        var tep = createValidator.Validate(new CreatePersonReferenceProjectionCommand(CreateRequest(correlationKey: "candidate-profile-1")));

        Assert.False(rawPayload.IsValid);
        Assert.False(rawSecret.IsValid);
        Assert.False(pii.IsValid);
        Assert.False(payroll.IsValid);
        Assert.False(tep.IsValid);
    }

    [Fact]
    public void Correlation_validator_rejects_non_employee_object_type()
    {
        var validator = new UpsertPersonReferenceExternalCorrelationValidator();
        var request = new PersonReferenceExternalCorrelationRequest(
            (PersonReferenceExternalObjectType)99,
            "EXT-001",
            "CK-001",
            PersonReferenceCorrelationState.Deferred,
            "v1",
            null);

        var result = validator.Validate(new UpsertPersonReferenceExternalCorrelationCommand(Guid.NewGuid(), null, request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Controller_uses_platform_actor_policy_and_pack_permissions()
    {
        var controllerType = typeof(PersonReferenceDirectoryController);
        var authorize = Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("PlatformActor", authorize.Policy);

        AssertHasPermission(nameof(PersonReferenceDirectoryController.GetAll), "platform.person-reference-directory.read");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.Create), "platform.person-reference-directory.create");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.Update), "platform.person-reference-directory.update");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.Archive), "platform.person-reference-directory.archive");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.Validate), "platform.person-reference-directory.validate");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.GetCorrelations), "platform.person-reference-directory.correlation.manage");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.CreateCorrelation), "platform.person-reference-directory.correlation.manage");
        AssertHasPermission(nameof(PersonReferenceDirectoryController.GetHealth), "platform.person-reference-directory.health.read");
    }

    private static PersonReferenceFixture CreateFixture(bool addHrisSource = true)
    {
        var hrisSources = new InMemoryHrisSourceRepository(TenantId);
        var hrisSource = HrisSource(Guid.NewGuid(), TenantId);
        if (addHrisSource)
        {
            hrisSources.Add(hrisSource);
        }

        return new PersonReferenceFixture(
            new InMemoryPersonReferenceDirectoryRepository(TenantId),
            hrisSources,
            new InMemoryOrganizationUnitRepository(TenantId),
            new InMemoryPositionRepository(TenantId),
            hrisSource,
            TenantContext(TenantId));
    }

    private static PersonReferenceProjectionCreateRequest CreateRequest(
        string code = "PR-001",
        string displayName = "Person Reference",
        Guid? hrisSourceProfileId = null,
        Guid? organizationUnitId = null,
        Guid? positionId = null,
        PersonReferenceState referenceState = PersonReferenceState.Deferred,
        string sourceContractVersion = "v1",
        string correlationKey = "CK-001") =>
        new(
            code,
            displayName,
            hrisSourceProfileId ?? Guid.NewGuid(),
            organizationUnitId,
            positionId,
            referenceState,
            sourceContractVersion,
            correlationKey,
            null);

    private static PersonReferenceExternalCorrelationRequest CorrelationRequest(
        string externalObjectReference = "E-001",
        string correlationKey = "CK-002",
        PersonReferenceCorrelationState state = PersonReferenceCorrelationState.Validated) =>
        new(
            PersonReferenceExternalObjectType.Employee,
            externalObjectReference,
            correlationKey,
            state,
            "v1",
            DateTimeOffset.UtcNow);

    private static HrisSourceProfile HrisSource(Guid id, Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Id = id,
            Code = $"HRIS-{id:N}"[..16],
            DisplayName = "HRIS Source",
            ProviderKind = HrisProviderKind.Other,
            ConnectionProfileReference = "vault://hris/test",
            LifecycleState = HrisSourceLifecycleState.Active,
            SyncMode = HrisSyncMode.Manual
        };

    private static PersonReferenceProjection Projection(
        string code,
        Guid hrisSourceProfileId,
        Guid tenantId,
        Guid? organizationUnitId = null,
        Guid? positionId = null) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            ReferenceDisplayName = "Person Reference",
            HrisSourceProfileId = hrisSourceProfileId,
            OrganizationUnitId = organizationUnitId,
            PositionId = positionId,
            ReferenceState = PersonReferenceState.Deferred,
            SourceContractVersion = "v1",
            CorrelationKey = $"{code}-CK"
        };

    private static PersonReferenceExternalCorrelation Correlation(
        Guid projectionId,
        Guid hrisSourceProfileId,
        Guid tenantId,
        PersonReferenceCorrelationState state,
        string correlationKey = "CK-001") =>
        new()
        {
            TenantId = tenantId,
            PersonReferenceProjectionId = projectionId,
            HrisSourceProfileId = hrisSourceProfileId,
            ExternalObjectType = PersonReferenceExternalObjectType.Employee,
            ExternalObjectReference = "E-001",
            CorrelationKey = correlationKey,
            CorrelationState = state,
            SourceContractVersion = "v1"
        };

    private static OrganizationUnit OrganizationUnit(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = "ORG-001",
            Name = "Org Unit",
            LegalEntityId = Guid.NewGuid()
        };

    private static Position Position(Guid tenantId, Guid organizationUnitId) =>
        new()
        {
            TenantId = tenantId,
            Code = "POS-001",
            Name = "Position",
            OrganizationUnitId = organizationUnitId
        };

    private static ITenantContext TenantContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return tenantContext;
    }

    private static void AssertHasPermission(string methodName, string expectedPermission)
    {
        var method = typeof(PersonReferenceDirectoryController).GetMethods()
            .Single(x => x.Name == methodName);
        var attribute = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        var permissionField = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(expectedPermission, permissionField?.GetValue(attribute));
    }

    private sealed record PersonReferenceFixture(
        InMemoryPersonReferenceDirectoryRepository PersonReferences,
        InMemoryHrisSourceRepository HrisSources,
        InMemoryOrganizationUnitRepository OrganizationUnits,
        InMemoryPositionRepository Positions,
        HrisSourceProfile HrisSource,
        ITenantContext TenantContext)
    {
        public CreatePersonReferenceProjectionHandler CreateHandler() =>
            new(PersonReferences, HrisSources, OrganizationUnits, Positions, TenantContext);

        public ValidatePersonReferenceProjectionHandler ValidateHandler() =>
            new(PersonReferences, HrisSources, OrganizationUnits, Positions);

        public UpsertPersonReferenceExternalCorrelationHandler CorrelationHandler() =>
            new(PersonReferences, HrisSources, OrganizationUnits, Positions);
    }
}
