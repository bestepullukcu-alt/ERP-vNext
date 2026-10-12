using System.Net;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-459 — <c>GET internal/users/counts?tenantId=</c>, the numbers Platform's tenant users summary now reads: live users
/// of THAT tenant by lifecycle status, over HTTP on the real Api + a test-owned mongod. Two disposable tenants.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class TenantUserCountsEndpointTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public TenantUserCountsEndpointTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Counts_are_the_tenants_own_live_users_by_status_and_nobody_elses()
    {
        var tenant = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        await AddAsync(tenant, active: 3, invited: 2, inactive: 1, deleted: 2);
        await AddAsync(foreign, active: 7, invited: 0, inactive: 0, deleted: 0);

        var own = await CountsAsync(tenant);
        Assert.Equal((6, 3, 2, 1), own); // deleted users are not users

        Assert.Equal((7, 7, 0, 0), await CountsAsync(foreign));
        Assert.Equal((0, 0, 0, 0), await CountsAsync(Guid.NewGuid())); // a tenant with nobody: zeros, not someone else's
    }

    [Fact]
    public async Task Without_the_internal_key_there_are_no_numbers()
    {
        using var client = _host.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"internal/users/counts?tenantId={Guid.NewGuid()}")).StatusCode);

        using var keyed = KeyedClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await keyed.GetAsync("internal/users/counts")).StatusCode); // no tenant, no count
    }

    private async Task<(long Total, long Active, long Invited, long Inactive)> CountsAsync(Guid tenant)
    {
        using var client = KeyedClient();
        var response = await client.GetAsync($"internal/users/counts?tenantId={tenant}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var r = doc.RootElement;
        Assert.Equal(tenant, r.GetProperty("tenantId").GetGuid());
        return (r.GetProperty("total").GetInt64(), r.GetProperty("active").GetInt64(), r.GetProperty("invited").GetInt64(), r.GetProperty("inactive").GetInt64());
    }

    private HttpClient KeyedClient()
    {
        var client = _host.Client();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key",
            _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey);
        return client;
    }

    private async Task AddAsync(Guid tenant, int active, int invited, int inactive, int deleted)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenant);
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var raw = _host.Database.GetCollection<BsonDocument>("users");

        async Task<User> Add(Action<User> shape)
        {
            var user = new User($"c.{Guid.NewGuid():N}@counts.test", "hash:x", "Count", "Ed", tenant);
            shape(user);
            return await users.CreateAsync(user, CancellationToken.None);
        }

        for (var i = 0; i < active; i++) await Add(u => u.ConfirmEmail());
        for (var i = 0; i < invited; i++) await Add(u => { u.RequirePasswordChange(null); u.Deactivate(); });
        for (var i = 0; i < inactive; i++) await Add(u => { u.ConfirmEmail(); u.Deactivate(); });
        for (var i = 0; i < deleted; i++)
        {
            var gone = await Add(u => u.ConfirmEmail());
            await raw.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(gone.Id, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Set("IsDeleted", true));
        }
    }
}
