using Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.LegalEntity.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityReferenceValidationTests
{
    [Fact]
    public async Task Active_same_tenant_legal_entity_is_referenceable()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Active);
        var handler = new ValidateLegalEntityReferenceHandler(new InMemoryLegalEntityRepository(tenantId, [entity]));

        var response = await handler.Handle(new ValidateLegalEntityReferenceQuery(entity.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        Assert.Equal(entity.Id, response.Data.LegalEntityId);
        Assert.True(response.Data.Referenceable);
        Assert.Equal("ACTIVE", response.Data.LifecycleState);
    }

    [Fact]
    public async Task Missing_legal_entity_fails_closed()
    {
        var handler = new ValidateLegalEntityReferenceHandler(new InMemoryLegalEntityRepository(Guid.NewGuid(), []));

        var response = await handler.Handle(new ValidateLegalEntityReferenceQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_legal_entity_fails_closed()
    {
        var currentTenantId = Guid.NewGuid();
        var otherTenantEntity = CreateEntity(Guid.NewGuid(), LegalEntityOperationalStatus.Active);
        var handler = new ValidateLegalEntityReferenceHandler(new InMemoryLegalEntityRepository(currentTenantId, [otherTenantEntity]));

        var response = await handler.Handle(new ValidateLegalEntityReferenceQuery(otherTenantEntity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Theory]
    [InlineData(LegalEntityOperationalStatus.Draft)]
    [InlineData(LegalEntityOperationalStatus.Archived)]
    public async Task Non_active_legal_entity_fails_closed(LegalEntityOperationalStatus lifecycleStatus)
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, lifecycleStatus);
        var handler = new ValidateLegalEntityReferenceHandler(new InMemoryLegalEntityRepository(tenantId, [entity]));

        var response = await handler.Handle(new ValidateLegalEntityReferenceQuery(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_legal_entity_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var entity = CreateEntity(tenantId, LegalEntityOperationalStatus.Active);
        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        var handler = new ValidateLegalEntityReferenceHandler(new InMemoryLegalEntityRepository(tenantId, [entity]));

        var response = await handler.Handle(new ValidateLegalEntityReferenceQuery(entity.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    private static LegalEntity CreateEntity(Guid tenantId, LegalEntityOperationalStatus operationalStatus)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"LE-{Guid.NewGuid():N}"[..12],
            LegalName = "Contoso Legal Entity",
            DisplayName = "Contoso",
            OperationalStatus = operationalStatus
        };
}
