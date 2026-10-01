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
    private const string DatabaseName = "diten_platform_fu21_itest";
    private readonly HashSet<Guid> _tenantIds = [];
    private IMongoDatabase _database = null!;
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() { _database = _client.GetDatabase(DatabaseName); return Task.CompletedTask; }
    public async Task DisposeAsync()
    {
        if (_tenantIds.Count == 0)
        {
            return;
        }

        await _database.GetCollection<PositionAssignment>("position_assignments")
            .DeleteManyAsync(x => _tenantIds.Contains(x.TenantId));
        await _database.GetCollection<Position>("positions")
            .DeleteManyAsync(x => _tenantIds.Contains(x.TenantId));
        await _database.GetCollection<OrganizationUnit>("organization_units")
            .DeleteManyAsync(x => _tenantIds.Contains(x.TenantId));
    }

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
        Assert.Equal(4, await _database.GetCollection<PositionAssignment>("position_assignments")
            .CountDocumentsAsync(x => x.TenantId == tenantA || x.TenantId == tenantB));
    }

    [Fact]
    public async Task Same_scoped_resolver_keeps_Tenant_A_and_B_memo_entries_isolated()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var legalEntityA = Guid.NewGuid();
        var legalEntityB = Guid.NewGuid();
        await SeedAsync(tenantA, userId, legalEntityA);
        await SeedAsync(tenantB, userId, legalEntityB);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantA);
        var db = new PlatformDbContext(_client, _database);
        var resolver = new OrgDataScopeCandidateResolver(
            new OrgDataScopeCandidateFactReader(db, tenantContext),
            new FixedTimeProvider(Now),
            new MongoOrgDataScopeCandidateAvailabilityClassifier());

        var first = await resolver.ResolveAsync(tenantA, userId, default);
        tenantContext.SetTenant(tenantB);
        var second = await resolver.ResolveAsync(tenantB, userId, default);

        Assert.Equal(new[] { legalEntityA }, first.LegalEntityIds);
        Assert.Equal(new[] { legalEntityB }, second.LegalEntityIds);
    }

    [Fact]
    public async Task Fact_reader_rejects_argument_tenant_that_differs_from_scoped_context()
    {
        var scopedTenant = Guid.NewGuid();
        var argumentTenant = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(scopedTenant);
        var reader = new OrgDataScopeCandidateFactReader(
            new PlatformDbContext(_client, _database), tenantContext);

        await Assert.ThrowsAsync<OrgDataScopeCandidateContractException>(() =>
            reader.ResolveLegalEntityIdsAsync(argumentTenant, Guid.NewGuid(), Now, 200, default));
    }

    [Fact]
    public async Task Real_Mongo_evaluates_effective_interval_by_UTC_instant_across_offsets()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expected = Guid.NewGuid();
        await SeedAsync(tenantId, userId, expected, Now.AddHours(-2).ToOffset(TimeSpan.FromHours(-5)));
        await SeedAsync(tenantId, userId, Guid.NewGuid(), Now.AddMinutes(30).ToOffset(TimeSpan.FromHours(-4)));
        await SeedAsync(
            tenantId,
            userId,
            Guid.NewGuid(),
            Now.AddDays(-1).ToOffset(TimeSpan.FromHours(3)),
            Now.AddMinutes(-30).ToOffset(TimeSpan.FromHours(5)));
        await SeedAsync(tenantId, userId, Guid.NewGuid(), Now.AddDays(-1), Now);

        var result = await Resolver(tenantId).ResolveAsync(tenantId, userId, default);

        Assert.Equal(new[] { expected }, result.LegalEntityIds);
    }

    [Fact]
    public async Task Real_Mongo_stops_at_201_distinct_active_positions_and_fails_closed()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantIds.Add(tenantId);
        var assignments = Enumerable.Range(0, 201).Select(_ => new PositionAssignment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, PositionId = Guid.NewGuid(),
            EffectiveFrom = Now.AddDays(-1)
        });
        await _database.GetCollection<PositionAssignment>("position_assignments").InsertManyAsync(assignments);

        await Assert.ThrowsAsync<OrgDataScopeCandidateContractException>(() =>
            Resolver(tenantId).ResolveAsync(tenantId, userId, default));
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
        var db = new PlatformDbContext(unavailableClient, unavailableClient.GetDatabase("diten_platform_fu21_unavailable_itest"));
        var candidate = new OrgDataScopeCandidateResolver(
            new OrgDataScopeCandidateFactReader(db, tenant), new FixedTimeProvider(Now),
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
        return new(new OrgDataScopeCandidateFactReader(db, tenant), new FixedTimeProvider(Now),
            new MongoOrgDataScopeCandidateAvailabilityClassifier());
    }

    private async Task SeedAsync(
        Guid tenantId,
        Guid userId,
        Guid legalEntityId,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null)
    {
        _tenantIds.Add(tenantId);
        var tenant = new TenantContext(); tenant.SetTenant(tenantId); var db = new PlatformDbContext(_client, _database);
        var units = new OrganizationUnitRepository(db, tenant); var positions = new PositionRepository(db, tenant); var assignments = new PositionAssignmentRepository(db, tenant);
        var unit = await units.CreateAsync(new OrganizationUnit { Id = Guid.NewGuid(), TenantId = tenantId, Code = Guid.NewGuid().ToString("N"), Name = "Unit", LegalEntityId = legalEntityId });
        var position = await positions.CreateAsync(new Position { Id = Guid.NewGuid(), TenantId = tenantId, Code = Guid.NewGuid().ToString("N"), Name = "Position", OrganizationUnitId = unit.Id });
        await assignments.CreateAsync(new PositionAssignment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, PositionId = position.Id,
            EffectiveFrom = effectiveFrom ?? Now.AddDays(-1), EffectiveTo = effectiveTo
        });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
}
