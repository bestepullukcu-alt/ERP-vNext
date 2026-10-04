using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX3 item 2(a) — <c>POST api/platform-auth/platform-admins/sync</c> FINDS THE ACCOUNT BY THE ADDRESS THE RECORD
/// HAD BEFORE THE CHANGE. Before: it looked the user up by the NEW address only, so changing another administrator's e-mail
/// answered 404 here — while Platform had already saved the record — and the account stayed on the old address: the next
/// sign-in or refresh found no record (Platform's 404) and was refused with every session revoked; done to the last super
/// administrator, the count kept counting someone who could no longer get in.
///
/// <para>Real HTTP on the production Api over a test-owned mongod (the shared acceptance host). Disposable platform users
/// only — never the seeded admin.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PlatformAdminSyncPreviousEmailTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public PlatformAdminSyncPreviousEmailTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    [Fact]
    public async Task An_email_change_finds_the_account_by_its_previous_address_and_moves_it_to_the_new_one()
    {
        var user = await SeedAsync();
        var moved = $"moved.{Guid.NewGuid():N}@bl521.test";

        var response = await SyncAsync(moved, previousEmail: user.Email);

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var after = await ByIdAsync(user.Id);
        Assert.Equal(moved, after!.Email);
        Assert.Null(await ByEmailAsync(user.Email));
        Assert.Equal(user.Id, (await ByEmailAsync(moved))!.Id);
    }

    [Fact]
    public async Task The_new_address_of_another_platform_account_is_a_conflict_and_nothing_moves()
    {
        var user = await SeedAsync();
        var other = await SeedAsync();

        var response = await SyncAsync(other.Email, previousEmail: user.Email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(user.Email, (await ByIdAsync(user.Id))!.Email);
        Assert.Equal(other.Email, (await ByIdAsync(other.Id))!.Email);
    }

    /// <summary>A caller that sends no previous address (an older Platform, or no e-mail change) is looked up as before.</summary>
    [Fact]
    public async Task Without_a_previous_address_the_account_is_found_by_its_address_as_before()
    {
        var user = await SeedAsync();

        var response = await SyncAsync(user.Email, previousEmail: null, displayName: "Renamed Account");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await ByIdAsync(user.Id);
        Assert.Equal((user.Email, "Renamed"), (after!.Email, after.FirstName));
    }

    /// <summary>Nothing is attached to whatever already sits on the new address when the previous one is unknown.</summary>
    [Fact]
    public async Task An_unknown_previous_address_is_not_found_and_the_account_on_the_new_address_is_left_alone()
    {
        var bystander = await SeedAsync();

        var response = await SyncAsync(bystander.Email, previousEmail: $"nobody.{Guid.NewGuid():N}@bl521.test", displayName: "Taken Over");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Sync", (await ByIdAsync(bystander.Id))!.FirstName);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SyncAsync(string email, string? previousEmail, string displayName = "Sync Probe")
    {
        // As Platform calls it: the internal key and the platform tenant (Auth's tenant resolution requires one here).
        using var client = _host.Client(tenantHeader: PlatformTenantId);
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key",
            _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey);
        return await client.PostAsJsonAsync("api/platform-auth/platform-admins/sync", new
        {
            email,
            previousEmail,
            userName = $"sync{Guid.NewGuid():N}"[..20],
            displayName,
            actorType = "PlatformAdmin",
            roles = new[] { "SupportAdmin" }
        });
    }

    private async Task<User> SeedAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var user = new User($"sync.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash($"Unused!{Guid.NewGuid():N}"), "Sync", "Probe", PlatformTenantId);
        user.ConfirmEmail();
        user.SetPlatformActorType("platform_admin");
        return await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
    }

    private async Task<User?> ByIdAsync(Guid id)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(id, PlatformTenantId, CancellationToken.None);
    }

    private async Task<User?> ByEmailAsync(string email)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByEmailAndTenantAsync(email, PlatformTenantId, CancellationToken.None);
    }
}
