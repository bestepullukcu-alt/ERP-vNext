using System.Net;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// CT guards (package 3a acceptance, 2026-09-24). Three sabotages of the reader left the agent's 74 tests green:
/// the role-name lookup without its <c>role.TenantId</c> match, a search that uses only the FIRST token, and a
/// <c>roleId</c>-only call falling through to the legacy unfiltered list. Each is pinned here over the real API.
/// </summary>
public sealed partial class UserListQueryTests
{
    [Fact]
    public async Task A_row_never_shows_the_name_of_a_role_that_belongs_to_another_tenant_even_if_an_assignment_points_at_it()
    {
        // A local assignment (this tenant's row) that points at the FOREIGN tenant's role — corrupt data, but the
        // one shape that would leak a foreign role's name into this tenant's list without the role.TenantId match.
        var world = await WorldAsync();
        var cem = world.Live.Single(u => u.Key == "cem"); // no roles of its own
        var userRoles = _host.Database.GetCollection<UserRole>("userRoles");
        var stray = new UserRole(cem.Id, world.ForeignAuditorsRoleId, world.TenantId, "ct-guard");
        await userRoles.InsertOneAsync(stray);
        try
        {
            var reply = await ListAsync($"start=0&length=10&search={Uri.EscapeDataString(cem.Email)}");
            Assert.Equal(HttpStatusCode.OK, reply.Status);
            var row = Assert.Single(reply.Items);
            Assert.Empty(row.GetProperty("roles").EnumerateArray());
        }
        finally
        {
            await userRoles.DeleteOneAsync(Builders<UserRole>.Filter.Eq(r => r.Id, stray.Id));
        }
    }

    [Fact]
    public async Task Every_search_token_must_match_so_two_words_from_two_different_people_find_nobody()
    {
        var world = await WorldAsync();
        var both = await ListAsync("start=0&length=100&search=" + Uri.EscapeDataString("Ada Yılmaz"));
        Assert.Equal(new[] { "ada" }, both.Keys(world));

        var crossed = await ListAsync("start=0&length=100&search=" + Uri.EscapeDataString("Ada Demir")); // Ada Yılmaz + Cem Demir
        Assert.Equal(0, crossed.FilteredTotal);
        Assert.Empty(crossed.Items);
    }

    [Fact]
    public async Task A_roleId_alone_selects_the_list_contract_and_filters_to_the_holders()
    {
        var world = await WorldAsync();
        var reply = await ListAsync($"roleId={world.AuditorsRoleId}");
        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.True(reply.Success, "a roleId-only call fell through to the legacy unfiltered shape");
        Assert.Equal(new[] { "ada", "bora" }, reply.Keys(world).OrderBy(k => k).ToArray());
        Assert.Equal(2, reply.FilteredTotal);
    }
}
