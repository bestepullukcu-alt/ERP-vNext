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

    [Fact]
    public async Task Bounded_batch_returns_only_active_same_tenant_non_deleted_requested_entities()
    {
        var tenant = Guid.NewGuid();
        var active = CreateEntity(tenant, LegalEntityOperationalStatus.Active);
        var archived = CreateEntity(tenant, LegalEntityOperationalStatus.Archived);
        var deleted = CreateEntity(tenant, LegalEntityOperationalStatus.Active); deleted.IsDeleted = true;
        var crossTenant = CreateEntity(Guid.NewGuid(), LegalEntityOperationalStatus.Active);
        var repository = new InMemoryLegalEntityRepository(tenant, [active, archived, deleted, crossTenant]);

        var result = await repository.GetReferenceableByIdsAsync(
            new[] { active.Id, archived.Id, deleted.Id, crossTenant.Id }
                .OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray());

        Assert.Equal([active.Id], result.Select(item => item.Id));
    }

    [Fact]
    public async Task Bounded_batch_rejects_noncanonical_input_and_propagates_cancellation()
    {
        var repository = new InMemoryLegalEntityRepository(Guid.NewGuid(), []);
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetReferenceableByIdsAsync([id, id]));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetReferenceableByIdsAsync([Guid.Empty]));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetReferenceableByIdsAsync(
            Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray()));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetReferenceableByIdsAsync([], cancellation.Token));
    }

    [Fact]
    public async Task In_memory_empty_batch_honors_pre_cancelled_token()
    {
        var repository = new InMemoryLegalEntityRepository(Guid.NewGuid(), []);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetReferenceableByIdsAsync(Array.Empty<Guid>(), cancellation.Token));
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
