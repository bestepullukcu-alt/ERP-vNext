using Diten.MdmService.Application.Features.LegalEntity;
using Diten.MdmService.Application.Features.LegalEntity.Commands;
using Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityCommandTests
{
    // ── MOD-0220 finish — new statutory fields + deferred-field relaxation ──

    [Fact]
    public async Task Create_persists_new_statutory_fields_and_maps_them_to_the_detail_dto()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLegalEntityRepository(tenantId, []);
        var handler = new CreateLegalEntityHandler(repository);

        var incorporation = new DateTimeOffset(2019, 3, 14, 0, 0, 0, TimeSpan.Zero);
        var dissolution = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

        var response = await handler.Handle(
            new CreateLegalEntityCommand(LegalEntityTestData.ValidRequest(
                code: "LE-VAT",
                vatNumber: "TR1234567890",
                placeOfIncorporation: "Ankara, TR",
                incorporationDate: incorporation,
                dissolutionDate: dissolution)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var created = Assert.Single(repository.Entities);
        Assert.Equal("TR1234567890", created.VatNumber);
        Assert.Equal("Ankara, TR", created.PlaceOfIncorporation);
        Assert.Equal(incorporation, created.IncorporationDate);
        Assert.Equal(dissolution, created.DissolutionDate);

        var dto = LegalEntityMappings.ToDetailDto(created);
        Assert.Equal("TR1234567890", dto.VatNumber);
        Assert.Equal("Ankara, TR", dto.PlaceOfIncorporation);
        Assert.Equal(incorporation, dto.IncorporationDate);
        Assert.Equal(dissolution, dto.DissolutionDate);
    }

    [Fact]
    public async Task Create_succeeds_without_address_and_defaults_role_to_legal_entity()
    {
        // The registered-address sub-form and OrganizationRole are deferred from the UI; a create with neither
        // must still succeed and default the base role so downstream (Organization) selects get a coherent value.
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLegalEntityRepository(tenantId, []);
        var handler = new CreateLegalEntityHandler(repository);

        var response = await handler.Handle(
            new CreateLegalEntityCommand(LegalEntityTestData.ValidRequest(
                code: "LE-NOADDR", organizationRoleCode: "", registeredAddressJson: null)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var created = Assert.Single(repository.Entities);
        Assert.Equal("LEGALENTITY", created.OrganizationRoleCode);
        Assert.Equal(string.Empty, created.RegisteredAddressJson);
        Assert.Equal(LegalEntityOperationalStatus.Draft, created.OperationalStatus);
    }

    [Fact]
    public void Lookup_dto_exposes_code_and_name_for_the_organization_select()
    {
        var entity = new LegalEntity
        {
            Id = Guid.NewGuid(),
            Code = "ACME-TR",
            LegalName = "Acme Manufacturing Inc.",
            DisplayName = "Acme",
            OperationalStatus = LegalEntityOperationalStatus.Active
        };

        var dto = LegalEntityMappings.ToLookupDto(entity);

        Assert.Equal("ACME-TR", dto.Code);
        Assert.Equal("Acme Manufacturing Inc.", dto.LegalName);
        Assert.Equal("Acme", dto.DisplayName);
        Assert.Equal("ACTIVE", dto.LifecycleState);
        Assert.True(dto.Referenceable);
    }

    [Fact]
    public async Task Create_rejects_duplicate_code_in_current_tenant()
    {
        var tenantId = Guid.NewGuid();
        var existing = new LegalEntity { TenantId = tenantId, Code = "LE-001", LegalName = "Existing" };
        var repository = new InMemoryLegalEntityRepository(tenantId, [existing]);
        var handler = new CreateLegalEntityHandler(repository);

        var response = await handler.Handle(
            new CreateLegalEntityCommand(LegalEntityTestData.ValidRequest(code: "LE-001", legalName: "Duplicate")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Create_sets_tenant_server_side_and_starts_in_draft()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLegalEntityRepository(tenantId, []);
        var handler = new CreateLegalEntityHandler(repository);

        var response = await handler.Handle(
            new CreateLegalEntityCommand(LegalEntityTestData.ValidRequest(code: "LE-002")),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var created = Assert.Single(repository.Entities);
        Assert.Equal(tenantId, created.TenantId);
        Assert.Equal(LegalEntityOperationalStatus.Draft, created.OperationalStatus);
        Assert.False(created.IsReferenceable);
        Assert.Equal(100, created.CompletenessScore); // all required fields supplied
    }

    [Fact]
    public async Task Create_rejects_unknown_parent()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLegalEntityRepository(tenantId, []);
        var handler = new CreateLegalEntityHandler(repository);

        var response = await handler.Handle(
            new CreateLegalEntityCommand(LegalEntityTestData.ValidRequest(
                organizationRoleCode: "BRANCH", parentLegalEntityId: Guid.NewGuid())),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Create_payload_does_not_accept_tenant_id()
    {
        var properties = typeof(Features.LegalEntity.LegalEntityWriteRequest).GetProperties().Select(x => x.Name);
        Assert.DoesNotContain("TenantId", properties);
    }

    [Fact]
    public async Task Update_maps_fields_and_preserves_operational_status()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Active);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new UpdateLegalEntityHandler(repository);

        var response = await handler.Handle(
            new UpdateLegalEntityCommand(entity.Id, LegalEntityTestData.ValidRequest(code: entity.Code, legalName: "Renamed")),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal("Renamed", entity.LegalName);
        Assert.Equal(LegalEntityOperationalStatus.Active, entity.OperationalStatus); // lifecycle-owned, untouched
    }

    [Fact]
    public async Task Activate_promotes_draft_to_active()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Draft);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ActivateLegalEntityHandler(repository);

        var response = await handler.Handle(new ActivateLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(LegalEntityOperationalStatus.Active, entity.OperationalStatus);
        Assert.True(entity.IsReferenceable);
    }

    [Fact]
    public async Task Activate_rejects_already_active_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Active);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ActivateLegalEntityHandler(repository);

        var response = await handler.Handle(new ActivateLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(LegalEntityOperationalStatus.Active, entity.OperationalStatus);
    }

    // İŞB — a Suspended entity can be RESUMED back to Active.
    [Fact]
    public async Task Activate_resumes_suspended_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Suspended);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ActivateLegalEntityHandler(repository);

        var response = await handler.Handle(new ActivateLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(LegalEntityOperationalStatus.Active, entity.OperationalStatus);
    }

    // İŞB — an Archived entity can be RESTORED back to Active.
    [Fact]
    public async Task Activate_restores_archived_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Archived);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ActivateLegalEntityHandler(repository);

        var response = await handler.Handle(new ActivateLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(LegalEntityOperationalStatus.Active, entity.OperationalStatus);
        Assert.True(entity.IsReferenceable);
    }

    [Fact]
    public async Task Suspend_requires_active_state()
    {
        var tenantId = Guid.NewGuid();
        var draft = CreateEntity(tenantId, LegalEntityOperationalStatus.Draft);
        var repository = new InMemoryLegalEntityRepository(tenantId, [draft]);
        var handler = new SuspendLegalEntityHandler(repository);

        var rejected = await handler.Handle(new SuspendLegalEntityCommand(draft.Id), CancellationToken.None);
        Assert.False(rejected.IsSuccessful);
        Assert.Equal(409, rejected.StatusCode);

        draft.OperationalStatus = LegalEntityOperationalStatus.Active;
        var accepted = await handler.Handle(new SuspendLegalEntityCommand(draft.Id), CancellationToken.None);
        Assert.True(accepted.IsSuccessful);
        Assert.Equal(LegalEntityOperationalStatus.Suspended, draft.OperationalStatus);
    }

    [Fact]
    public async Task Archive_accepts_active_and_suspended()
    {
        var tenantId = Guid.NewGuid();
        var active = CreateEntity(tenantId, LegalEntityOperationalStatus.Active);
        var suspended = CreateEntity(tenantId, LegalEntityOperationalStatus.Suspended);
        var repository = new InMemoryLegalEntityRepository(tenantId, [active, suspended]);
        var handler = new ArchiveLegalEntityHandler(repository);

        Assert.True((await handler.Handle(new ArchiveLegalEntityCommand(active.Id), CancellationToken.None)).IsSuccessful);
        Assert.Equal(LegalEntityOperationalStatus.Archived, active.OperationalStatus);
        Assert.True((await handler.Handle(new ArchiveLegalEntityCommand(suspended.Id), CancellationToken.None)).IsSuccessful);
        Assert.Equal(LegalEntityOperationalStatus.Archived, suspended.OperationalStatus);
    }

    [Fact]
    public async Task Archive_rejects_draft_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Draft);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ArchiveLegalEntityHandler(repository);

        var response = await handler.Handle(new ArchiveLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(LegalEntityOperationalStatus.Draft, entity.OperationalStatus);
    }

    private static LegalEntity CreateEntity(Guid tenantId, LegalEntityOperationalStatus operationalStatus)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"LE-{Guid.NewGuid():N}"[..12],
            LegalName = "Contoso Legal Entity",
            DisplayName = "Contoso",
            LegalFormCode = "CORPORATION",
            OrganizationRoleCode = "LEGALENTITY",
            CountryCode = "TR",
            BaseCurrencyCode = "TRY",
            RegisteredAddressJson = LegalEntityTestData.ValidAddressJson,
            OperationalStatus = operationalStatus
        };
}
