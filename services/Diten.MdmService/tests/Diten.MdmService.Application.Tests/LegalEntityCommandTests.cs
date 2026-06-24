using Diten.MdmService.Application.Features.LegalEntity.Commands;
using Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityCommandTests
{
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
