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
        var existing = new LegalEntity
        {
            TenantId = tenantId,
            Code = "LE-001",
            LegalName = "Existing",
            LifecycleStatus = LegalEntityLifecycleStatus.Draft
        };
        var repository = new InMemoryLegalEntityRepository(tenantId, [existing]);
        var handler = new CreateLegalEntityHandler(repository);

        var response = await handler.Handle(new CreateLegalEntityCommand("LE-001", "Duplicate", null), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Create_sets_tenant_server_side()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryLegalEntityRepository(tenantId, []);
        var handler = new CreateLegalEntityHandler(repository);

        var response = await handler.Handle(new CreateLegalEntityCommand("LE-002", "Created", "Created Display"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var created = Assert.Single(repository.Entities);
        Assert.Equal(tenantId, created.TenantId);
        Assert.Equal(LegalEntityLifecycleStatus.Draft, created.LifecycleStatus);
    }

    [Fact]
    public void Create_payload_does_not_accept_tenant_id()
    {
        var properties = typeof(CreateLegalEntityCommand).GetProperties().Select(x => x.Name);

        Assert.DoesNotContain("TenantId", properties);
    }

    [Fact]
    public async Task Activate_rejects_archived_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityLifecycleStatus.Archived);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ActivateLegalEntityHandler(repository);

        var response = await handler.Handle(new ActivateLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(LegalEntityLifecycleStatus.Archived, entity.LifecycleStatus);
    }

    [Fact]
    public async Task Activate_rejects_already_active_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityLifecycleStatus.Active);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ActivateLegalEntityHandler(repository);

        var response = await handler.Handle(new ActivateLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(LegalEntityLifecycleStatus.Active, entity.LifecycleStatus);
    }

    [Fact]
    public async Task Archive_rejects_draft_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityLifecycleStatus.Draft);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ArchiveLegalEntityHandler(repository);

        var response = await handler.Handle(new ArchiveLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(LegalEntityLifecycleStatus.Draft, entity.LifecycleStatus);
    }

    [Fact]
    public async Task Archive_rejects_already_archived_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityLifecycleStatus.Archived);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ArchiveLegalEntityHandler(repository);

        var response = await handler.Handle(new ArchiveLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(LegalEntityLifecycleStatus.Archived, entity.LifecycleStatus);
    }

    [Fact]
    public async Task Archive_accepts_active_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityLifecycleStatus.Active);
        var repository = new InMemoryLegalEntityRepository(tenantId, [entity]);
        var handler = new ArchiveLegalEntityHandler(repository);

        var response = await handler.Handle(new ArchiveLegalEntityCommand(entity.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(LegalEntityLifecycleStatus.Archived, entity.LifecycleStatus);
    }

    private static LegalEntity CreateEntity(Guid tenantId, LegalEntityLifecycleStatus lifecycleStatus)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"LE-{Guid.NewGuid():N}"[..12],
            LegalName = "Contoso Legal Entity",
            DisplayName = "Contoso",
            LifecycleStatus = lifecycleStatus
        };
}
