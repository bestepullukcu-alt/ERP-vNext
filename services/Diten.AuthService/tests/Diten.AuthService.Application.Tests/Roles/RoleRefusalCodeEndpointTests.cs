using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 (A) — the refusals of the Roles, Role Permissions and User Roles screens carry their stable code
/// in the envelope's <c>errorCodes</c>, over HTTP, through the real Api (auth, tenant resolution, [HasPermission],
/// MediatR pipeline, Mongo). The English sentence stays in <c>errors</c>. A disposable tenant per class.
/// <para>The validator codes (<c>ROLE_NAME_REQUIRED</c> …) are NOT asserted over HTTP here: their passage through
/// <c>ExceptionHandlingBehavior</c> belongs to another branch; <c>RoleErrorCodeContractTests</c> proves them at the
/// validator.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class RoleRefusalCodeEndpointTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly RoleEndpointWorld _world;

    public RoleRefusalCodeEndpointTests(AccountKindAcceptance.AuthTestHost host)
        => _world = RoleEndpointWorld.Create(host, "Rana", "Steward");

    [Fact]
    public async Task Creating_a_role_with_a_taken_name_returns_ROLE_NAME_TAKEN()
    {
        var existing = await _world.NewRoleAsync("taken");
        using var client = _world.Client();

        var response = await client.PostAsJsonAsync("api/roles", new { name = existing.Name, displayName = "Again", description = "" });

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, RoleErrorCodes.NameTaken);
    }

    [Fact]
    public async Task Reading_updating_and_deleting_a_missing_role_return_ROLE_NOT_FOUND()
    {
        var missing = Guid.NewGuid();
        using var client = _world.Client();

        await AssertRefusedAsync(await client.GetAsync($"api/roles/{missing}"), HttpStatusCode.NotFound, RoleErrorCodes.NotFound);
        await AssertRefusedAsync(await client.GetAsync($"api/roles/{missing}/permissions"), HttpStatusCode.NotFound, RoleErrorCodes.NotFound);
        await AssertRefusedAsync(await client.PutAsJsonAsync($"api/roles/{missing}", new { displayName = "x", description = "" }), HttpStatusCode.NotFound, RoleErrorCodes.NotFound);
        await AssertRefusedAsync(await client.DeleteAsync($"api/roles/{missing}"), HttpStatusCode.NotFound, RoleErrorCodes.NotFound);
    }

    [Fact]
    public async Task Deleting_a_system_role_returns_ROLE_SYSTEM_NOT_DELETABLE()
    {
        var system = await _world.NewRoleAsync("sys", system: true);
        using var client = _world.Client();

        await AssertRefusedAsync(await client.DeleteAsync($"api/roles/{system.Id}"), HttpStatusCode.Forbidden, RoleErrorCodes.SystemNotDeletable);
    }

    [Fact]
    public async Task Granting_a_platform_permission_to_a_tenant_role_returns_ROLE_PERMISSION_NOT_TENANT_ASSIGNABLE()
    {
        var role = await _world.NewRoleAsync("esc");
        var platformOnly = (await _world.AllPermissionsAsync()).First(p => !DefaultRolePermissionTemplate.IsTenantAssignable(p));
        using var client = _world.Client();

        var response = await client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId = platformOnly.Id });

        await AssertRefusedAsync(response, HttpStatusCode.Forbidden, RoleErrorCodes.PermissionNotTenantAssignable);
        Assert.Empty(await _world.GrantsAsync(role.Id));
    }

    [Fact]
    public async Task Granting_to_a_missing_role_returns_ROLE_NOT_FOUND()
    {
        var permission = await _world.PermissionAsync("auth.users.read");
        using var client = _world.Client();

        var response = await client.PostAsJsonAsync($"api/roles/{Guid.NewGuid()}/permissions", new { permissionId = permission.Id });

        await AssertRefusedAsync(response, HttpStatusCode.NotFound, RoleErrorCodes.NotFound);
    }

    [Fact]
    public async Task Granting_a_permission_the_role_already_holds_returns_ROLE_PERMISSION_ALREADY_GRANTED_not_a_500()
    {
        var role = await _world.NewRoleAsync("twice");
        var permission = await _world.PermissionAsync("auth.users.read");
        using var client = _world.Client();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId = permission.Id })).StatusCode);
        var auditBefore = (await _world.LocalRowsAsync(RoleAuditEvents.PermissionGranted, role.Id)).Count;

        var again = await client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId = permission.Id });

        await AssertRefusedAsync(again, HttpStatusCode.Conflict, RoleErrorCodes.PermissionAlreadyGranted);
        Assert.Single(await _world.GrantsAsync(role.Id));
        Assert.Equal(auditBefore, (await _world.LocalRowsAsync(RoleAuditEvents.PermissionGranted, role.Id)).Count); // no row for a grant that was not made
    }

    // The double click: both requests are in flight before either has written. One makes the grant; every other one —
    // whether it lost at the handler's check or at the unique index — gets the same coded 409, never a 500.
    [Fact]
    public async Task A_double_click_race_makes_one_grant_and_answers_every_loser_with_the_same_code()
    {
        var role = await _world.NewRoleAsync("race");
        var permission = await _world.PermissionAsync("auth.users.read");
        using var client = _world.Client();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId = permission.Id })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.NoContent))
        {
            await AssertRefusedAsync(loser, HttpStatusCode.Conflict, RoleErrorCodes.PermissionAlreadyGranted);
        }

        Assert.Single(await _world.GrantsAsync(role.Id));
        Assert.Single(await _world.LocalRowsAsync(RoleAuditEvents.PermissionGranted, role.Id));
    }

    [Fact]
    public async Task Removing_a_provisioning_managed_grant_returns_ROLE_PERMISSION_GRANT_MANAGED()
    {
        var role = await _world.NewRoleAsync("managed");
        var permission = await _world.PermissionAsync("auth.users.read");
        await _world.InsertGrantAsync(RolePermission.SystemGrant(role.Id, permission.Id, _world.TenantId, "system"));
        using var client = _world.Client();

        var response = await client.DeleteAsync($"api/roles/{role.Id}/permissions/{permission.Id}");

        await AssertRefusedAsync(response, HttpStatusCode.Conflict, RoleErrorCodes.PermissionGrantManaged);
        Assert.Single(await _world.GrantsAsync(role.Id));
    }

    [Fact]
    public async Task Assigning_a_role_to_a_missing_user_or_a_missing_role_returns_its_own_code()
    {
        var role = await _world.NewRoleAsync("assign");
        var user = await _world.NewUserAsync();
        using var client = _world.Client();

        await AssertRefusedAsync(await client.PostAsJsonAsync($"api/users/{Guid.NewGuid()}/roles", new { roleId = role.Id }), HttpStatusCode.NotFound, RoleErrorCodes.UserRoleUserNotFound);
        await AssertRefusedAsync(await client.PostAsJsonAsync($"api/users/{user}/roles", new { roleId = Guid.NewGuid() }), HttpStatusCode.NotFound, RoleErrorCodes.NotFound);
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"expected {(int)status}, got {(int)response.StatusCode}: {raw}");
        using var doc = JsonDocument.Parse(raw);
        Assert.False(doc.RootElement.GetProperty("isSuccessful").GetBoolean());
        // The sentence stays (English, for logs and other consumers) …
        Assert.NotEmpty(doc.RootElement.GetProperty("errors").EnumerateArray());
        // … and the code travels beside it.
        Assert.True(doc.RootElement.TryGetProperty("errorCodes", out var codes) && codes.ValueKind == JsonValueKind.Array && codes.GetArrayLength() > 0,
            $"the refusal carries no errorCodes: {raw}");
        Assert.Equal(code, codes[0].GetProperty("code").GetString());
    }
}
