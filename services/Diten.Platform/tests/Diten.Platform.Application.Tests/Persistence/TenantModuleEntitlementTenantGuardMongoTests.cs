using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-500 — whose row a write may touch, measured on the real <see cref="TenantModuleEntitlementRepository"/> over a
/// test-owned replica set.
///
/// <para>Every write member answers the same two questions the same way. A caller in a TENANT context is refused
/// another tenant's row before any write is attempted. A caller in the PLATFORM context (a platform administrator;
/// <c>TenantContext.TenantId == Guid.Empty</c>) writes the tenant the row or the route names — and even then the
/// write filter still carries tenant + id + version, so a row object or a route that names the wrong tenant matches
/// nothing.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TenantModuleEntitlementTenantGuardMongoTests
{
    private static readonly Guid Ours = Guid.Parse("60060060-0000-4000-8000-0000000000a1");
    private static readonly Guid Theirs = Guid.Parse("60060060-0000-4000-8000-0000000000b2");

    [Fact]
    public async Task Update_in_the_platform_context_writes_the_rows_own_tenant()
    {
        await using var rig = await Rig.StartAsync(PlatformContext());
        var row = await rig.SeedAsync(Theirs);
        var version = row.RowVersion;

        row.IsEnabled = false;
        await rig.InTransactionAsync(session => rig.Repository.UpdateAsync(session, row, version));

        Assert.False((await rig.StoredAsync(row.Id)).IsEnabled);
    }

    [Fact]
    public async Task Update_in_a_tenant_context_still_writes_its_own_row()
    {
        await using var rig = await Rig.StartAsync(TenantContextFor(Ours));
        var row = await rig.SeedAsync(Ours);
        var version = row.RowVersion;

        row.IsEnabled = false;
        await rig.InTransactionAsync(session => rig.Repository.UpdateAsync(session, row, version));

        Assert.False((await rig.StoredAsync(row.Id)).IsEnabled);
    }

    [Fact]
    public async Task Update_in_a_tenant_context_is_refused_another_tenants_row_and_the_row_is_unchanged()
    {
        await using var rig = await Rig.StartAsync(TenantContextFor(Ours));
        var row = await rig.SeedAsync(Theirs);
        var version = row.RowVersion;

        row.IsEnabled = false;
        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(
            () => rig.InTransactionAsync(session => rig.Repository.UpdateAsync(session, row, version)));

        await rig.AssertUntouchedAsync(row.Id, version);
    }

    [Fact]
    public async Task Update_never_matches_a_stored_row_of_a_tenant_other_than_the_one_the_row_object_names()
    {
        // The platform context has no tenant of its own to compare with, so the FILTER is what stands between a row
        // object that names tenant A and a stored row of tenant B with the same id.
        await using var rig = await Rig.StartAsync(PlatformContext());
        var stored = await rig.SeedAsync(Theirs);
        var forged = new TenantModuleEntitlement
        {
            Id = stored.Id, TenantId = Ours, ModuleCode = stored.ModuleCode, Source = stored.Source, IsEnabled = false
        };

        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(
            () => rig.InTransactionAsync(session => rig.Repository.UpdateAsync(session, forged, stored.RowVersion)));

        await rig.AssertUntouchedAsync(stored.Id, stored.RowVersion);
    }

    [Fact]
    public async Task Update_with_a_version_that_is_not_the_stored_one_is_refused()
    {
        await using var rig = await Rig.StartAsync(PlatformContext());
        var row = await rig.SeedAsync(Theirs);
        var version = row.RowVersion;

        row.IsEnabled = false;
        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(
            () => rig.InTransactionAsync(session => rig.Repository.UpdateAsync(session, row, Guid.NewGuid().ToByteArray())));

        await rig.AssertUntouchedAsync(row.Id, version);
    }

    [Fact]
    public async Task SoftDelete_in_the_platform_context_removes_the_row_of_the_tenant_the_route_names()
    {
        await using var rig = await Rig.StartAsync(PlatformContext());
        var row = await rig.SeedAsync(Theirs);

        await rig.InTransactionAsync(session => rig.Repository.SoftDeleteAsync(session, Theirs, row.Id, row.RowVersion));

        Assert.True((await rig.StoredAsync(row.Id)).IsDeleted);
    }

    [Fact]
    public async Task SoftDelete_in_a_tenant_context_is_refused_another_tenants_row_and_the_row_is_unchanged()
    {
        await using var rig = await Rig.StartAsync(TenantContextFor(Ours));
        var row = await rig.SeedAsync(Theirs);

        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(
            () => rig.InTransactionAsync(session => rig.Repository.SoftDeleteAsync(session, Theirs, row.Id, row.RowVersion)));

        await rig.AssertUntouchedAsync(row.Id, row.RowVersion);
    }

    [Fact]
    public async Task SoftDelete_never_matches_a_row_of_a_tenant_other_than_the_one_it_was_given()
    {
        await using var rig = await Rig.StartAsync(PlatformContext());
        var row = await rig.SeedAsync(Theirs);

        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(
            () => rig.InTransactionAsync(session => rig.Repository.SoftDeleteAsync(session, Ours, row.Id, row.RowVersion)));

        await rig.AssertUntouchedAsync(row.Id, row.RowVersion);
    }

    [Fact]
    public async Task Create_in_a_tenant_context_is_refused_a_row_for_another_tenant_and_nothing_is_stored()
    {
        await using var rig = await Rig.StartAsync(TenantContextFor(Ours));
        var row = NewRow(Theirs);

        await Assert.ThrowsAsync<TenantModuleEntitlementConcurrencyException>(
            () => rig.InTransactionAsync(session => rig.Repository.CreateAsync(session, row)));

        Assert.Equal(0, await rig.Rows.CountDocumentsAsync(FilterDefinition<TenantModuleEntitlement>.Empty));
    }

    [Fact]
    public async Task The_base_repositorys_delete_by_id_is_closed_and_the_row_is_unchanged()
    {
        await using var rig = await Rig.StartAsync(PlatformContext());
        var row = await rig.SeedAsync(Theirs);

        await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => rig.Repository.DeleteAsync(row.Id));

        await rig.AssertUntouchedAsync(row.Id, row.RowVersion);
    }

    private static ITenantContext TenantContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private static ITenantContext PlatformContext()
    {
        // Exactly what TenantResolutionMiddleware does for a platform actor.
        var context = new TenantContext();
        context.SetPlatformContext(Guid.Empty);
        return context;
    }

    private static TenantModuleEntitlement NewRow(Guid tenantId) => new()
    {
        TenantId = tenantId, ModuleCode = "CRM", Source = EntitlementSource.ManualOverride, IsEnabled = true, Reason = "seeded"
    };

    private sealed class Rig : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly PlatformTransactionExecutor _executor;

        private Rig(DisposableMongoReplicaSet mongo, ITenantContext tenantContext)
        {
            _mongo = mongo;
            var database = mongo.CreateDatabase();
            var context = new PlatformDbContext(mongo.Client, database);
            Rows = database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements");
            Repository = new TenantModuleEntitlementRepository(context, tenantContext);
            _executor = new PlatformTransactionExecutor(context);
        }

        public IMongoCollection<TenantModuleEntitlement> Rows { get; }
        public TenantModuleEntitlementRepository Repository { get; }

        public static async Task<Rig> StartAsync(ITenantContext tenantContext) =>
            new(await DisposableMongoReplicaSet.StartAsync(), tenantContext);

        public async Task<TenantModuleEntitlement> SeedAsync(Guid tenantId)
        {
            var row = NewRow(tenantId);
            await Rows.InsertOneAsync(row);
            return row;
        }

        public Task<TenantModuleEntitlement> StoredAsync(Guid id) => Rows.Find(x => x.Id == id).SingleAsync();

        public Task InTransactionAsync(Func<IPlatformTransactionSession, Task> body) =>
            _executor.ExecuteAsync(async (session, _) =>
            {
                await body(session);
                return true;
            });

        public async Task AssertUntouchedAsync(Guid id, byte[] version)
        {
            var stored = await StoredAsync(id);
            Assert.True(stored.IsEnabled);
            Assert.False(stored.IsDeleted);
            Assert.Null(stored.UpdatedAt);
            Assert.Equal(version, stored.RowVersion);
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }
}
