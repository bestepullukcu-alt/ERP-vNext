using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// ATT-FIX1 (8b) — the internal sweeps (display names for Platform's meetings and people searches among them) read a
/// tenant's users page by page. Skip/Limit WITHOUT an order is not a paging: MongoDB may hand the same user out on two
/// pages and none on a third once a tenant passes one page (500). Measured on the real repository over a test-owned
/// mongod: 1200 users read 500 at a time come back complete, once each, in one stable order (by id).
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UserTenantPagingMongoTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public UserTenantPagingMongoTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    [Fact]
    public async Task A_tenants_users_read_page_by_page_come_back_complete_once_each_in_id_order()
    {
        var tenant = Guid.NewGuid();
        var users = _host.Database.GetCollection<User>("users");
        await users.InsertManyAsync(Enumerable.Range(0, 1200)
            .Select(i => new User($"paging-{tenant:N}-{i}@acme.test", "hash:x", "Kişi", $"No {i}", tenant)));

        var repository = new UserRepository(_host.Database, new FixedTenant(tenant));
        var read = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            read.AddRange((await repository.GetAllByTenantAsync(tenant, page, 500, CancellationToken.None)).Select(u => u.Id));
        }

        Assert.Equal(1200, read.Count);
        Assert.Equal(1200, read.Distinct().Count());
        var byId = (await users.Find(u => u.TenantId == tenant).SortBy(u => u.Id).ToListAsync()).Select(u => u.Id);
        Assert.Equal(byId, read);
    }

    private sealed class FixedTenant(Guid tenant) : ITenantContext
    {
        public Guid TenantId => tenant;
        public bool IsResolved => true;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { }
        public void SetPlatformContext(Guid targetTenantId) { }
    }
}
