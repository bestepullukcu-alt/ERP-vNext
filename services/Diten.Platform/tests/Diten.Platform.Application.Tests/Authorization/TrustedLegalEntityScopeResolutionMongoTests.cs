using Diten.Platform.Application.Authorization;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class TrustedLegalEntityScopeResolutionMongoTests : IAsyncLifetime
{
    private readonly MongoClient _client = new("mongodb://127.0.0.1:27017");
    private readonly string _databaseName = $"FU21_{Guid.NewGuid():N}";
    private IMongoDatabase _database = null!;
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() { _database = _client.GetDatabase(_databaseName); return Task.CompletedTask; }
    public Task DisposeAsync() => _client.DropDatabaseAsync(_databaseName);

    [Fact]
    public async Task Real_Mongo_preserves_multiple_candidates_and_tenant_isolation_without_MDM_callback()
    {
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid(); var user = Guid.NewGuid();
        var le1 = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var le2 = Guid.Parse("20000000-0000-0000-0000-000000000000");
        await SeedAsync(tenantA, user, le2); await SeedAsync(tenantA, user, le1); await SeedAsync(tenantA, user, le2);
        await SeedAsync(tenantB, user, Guid.Parse("90000000-0000-0000-0000-000000000000"));

        var resultA = await Resolver(tenantA).ResolveAsync(tenantA, user, default);
        var resultB = await Resolver(tenantB).ResolveAsync(tenantB, user, default);

        Assert.Equal(new[] { le1, le2 }, resultA.LegalEntityIds);
        Assert.Single(resultB.LegalEntityIds);
        Assert.DoesNotContain(resultB.LegalEntityIds[0], resultA.LegalEntityIds);
        Assert.Equal(4, await _database.GetCollection<PositionAssignment>("position_assignments").CountDocumentsAsync(FilterDefinition<PositionAssignment>.Empty));
    }

    [Fact]
    public async Task Real_unavailable_Mongo_repository_chain_maps_to_exact_503()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(150);
        settings.ConnectTimeout = TimeSpan.FromMilliseconds(150);
        var unavailableClient = new MongoClient(settings);
        var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var tenant = new TenantContext(); tenant.SetTenant(tenantId);
        var db = new PlatformDbContext(unavailableClient, unavailableClient.GetDatabase($"FU21_DOWN_{Guid.NewGuid():N}"));
        var candidate = new OrgDataScopeCandidateResolver(
            new OrganizationUnitRepository(db, tenant), new PositionRepository(db, tenant),
            new PositionAssignmentRepository(db, tenant), new FixedTimeProvider(Now),
            new MongoOrgDataScopeCandidateAvailabilityClassifier());
        var handler = new ResolveTrustedLegalEntityScopeHandler(candidate, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ResolveTrustedLegalEntityScopeQuery(
            tenantId, userId, "product-item-sku-master", "mdm.gskus.read"), default);

        Assert.False(response.IsSuccessful); Assert.Equal(503, response.StatusCode);
    }

    private OrgDataScopeCandidateResolver Resolver(Guid tenantId)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId);
        var db = new PlatformDbContext(_client, _database);
        return new(new OrganizationUnitRepository(db, tenant), new PositionRepository(db, tenant),
            new PositionAssignmentRepository(db, tenant), new FixedTimeProvider(Now),
            new MongoOrgDataScopeCandidateAvailabilityClassifier());
    }

    private async Task SeedAsync(Guid tenantId, Guid userId, Guid legalEntityId)
    {
        var tenant = new TenantContext(); tenant.SetTenant(tenantId); var db = new PlatformDbContext(_client, _database);
        var units = new OrganizationUnitRepository(db, tenant); var positions = new PositionRepository(db, tenant); var assignments = new PositionAssignmentRepository(db, tenant);
        var unit = await units.CreateAsync(new OrganizationUnit { Id = Guid.NewGuid(), TenantId = tenantId, Code = Guid.NewGuid().ToString("N"), Name = "Unit", LegalEntityId = legalEntityId });
        var position = await positions.CreateAsync(new Position { Id = Guid.NewGuid(), TenantId = tenantId, Code = Guid.NewGuid().ToString("N"), Name = "Position", OrganizationUnitId = unit.Id });
        await assignments.CreateAsync(new PositionAssignment { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, PositionId = position.Id, EffectiveFrom = Now.AddDays(-1) });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}
