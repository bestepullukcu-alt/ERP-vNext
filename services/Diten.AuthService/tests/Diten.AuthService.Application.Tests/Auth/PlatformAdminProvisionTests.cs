using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// <c>POST api/platform-auth/platform-admins/provision</c> — BL-521 FIX5 item 11 (and the tenant rule of item 14).
///
/// <para>PROVISION FOLLOWS THE SYNC'S RULE FOR AN EXISTING ACCOUNT. It used to activate an existing account and confirm its
/// address on every call (the invitation resend reaches it), so an administrator Auth had disabled came back — and its address
/// counted as confirmed — with a plain "resend invite". Now the account is switched on only for an Active record (the status
/// travels with the call), and an existing account's address is never confirmed here: only accepting the invitation confirms
/// it. A NEW account is created active and confirmed, as before.</para>
///
/// <para>It touches only the platform tenant's account: the same address in another tenant is another account and is left
/// exactly as it is.</para>
///
/// <para>Real HTTP on the production Api over a test-owned mongod. Disposable platform users only — never the seeded admin.</para>
/// </summary>
[Trait("Category", "Mongo")]
public sealed class PlatformAdminProvisionTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public PlatformAdminProvisionTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    [Fact]
    public async Task A_new_account_is_created_active_and_confirmed_with_the_requested_roles()
    {
        var email = $"provision.{Guid.NewGuid():N}@bl521.test";

        var response = await ProvisionAsync(email, status: "Active", roles: ["BillingAdmin"]);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var created = await ByEmailAsync(email, PlatformTenantId);
        Assert.NotNull(created);
        Assert.True(created!.IsActive);
        Assert.True(created.EmailConfirmed);
        Assert.Equal(["BillingAdmin"], await PlatformRolesAsync(created.Id));
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Suspended", false)]
    [InlineData("Disabled", false)]
    [InlineData(null, false)] // an older Platform that sends no status: an existing account is not switched on
    public async Task An_existing_account_is_activated_only_for_an_active_record_and_its_address_is_never_confirmed_here(string? status, bool activated)
    {
        var user = await SeedAsync(PlatformTenantId, active: false, emailConfirmed: false);

        var response = await ProvisionAsync(user.Email, status: status);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var after = await ByEmailAsync(user.Email, PlatformTenantId);
        Assert.Equal(user.Id, after!.Id);
        Assert.Equal(activated, after.IsActive);
        Assert.False(after.EmailConfirmed);
    }

    [Fact]
    public async Task The_same_address_in_another_tenant_is_another_account_and_is_left_exactly_as_it_is()
    {
        var otherTenant = Guid.NewGuid();
        var bystander = await SeedAsync(otherTenant, active: false, emailConfirmed: false);

        var response = await ProvisionAsync(bystander.Email, status: "Active", roles: ["SuperAdmin"]);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var platformAccount = await ByEmailAsync(bystander.Email, PlatformTenantId);
        Assert.NotNull(platformAccount);
        Assert.NotEqual(bystander.Id, platformAccount!.Id);
        var untouched = await ByEmailAsync(bystander.Email, otherTenant);
        Assert.Equal((bystander.Id, false, false), (untouched!.Id, untouched.IsActive, untouched.EmailConfirmed));
        Assert.Empty(await RolesAsync(bystander.Id, otherTenant));
        Assert.Equal(["SuperAdmin"], await PlatformRolesAsync(platformAccount.Id));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> ProvisionAsync(string email, string? status, string[]? roles = null)
    {
        // As Platform calls it: the internal key and the platform tenant (Auth's tenant resolution requires one here).
        using var client = _host.Client(tenantHeader: PlatformTenantId);
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key",
            _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey);
        return await client.PostAsJsonAsync("api/platform-auth/platform-admins/provision", new
        {
            email,
            userName = $"prov{Guid.NewGuid():N}"[..20],
            displayName = "Provision Probe",
            actorType = "PlatformAdmin",
            roles = roles ?? ["SupportAdmin"],
            requirePasswordChange = true,
            status
        });
    }

    private async Task<User> SeedAsync(Guid tenantId, bool active, bool emailConfirmed)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var user = new User($"prov.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash($"Unused!{Guid.NewGuid():N}"), "Provision", "Probe", tenantId);
        if (emailConfirmed)
        {
            user.ConfirmEmail();
        }

        if (!active)
        {
            user.Deactivate();
        }

        return await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
    }

    private async Task<string[]> PlatformRolesAsync(Guid userId) =>
        (await RolesAsync(userId, PlatformTenantId)).Where(PlatformAdministratorRoleConvergence.IsPlatformRole).OrderBy(r => r, StringComparer.Ordinal).ToArray();

    private async Task<string[]> RolesAsync(Guid userId, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return (await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>().GetRolesByUserAsync(userId, tenantId, CancellationToken.None)).ToArray();
    }

    private async Task<User?> ByEmailAsync(string email, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByEmailAndTenantAsync(email, tenantId, CancellationToken.None);
    }
}
