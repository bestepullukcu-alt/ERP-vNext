using System.Collections.Concurrent;
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
/// <c>POST api/platform-auth/platform-admins/sync</c> — BL-521 FIX4.
///
/// <para>Item 1 — THE ADDRESS ONLY FINDS THE ACCOUNT; IT IS NEVER CHANGED. FIX3 moved the account to a new address
/// (found by a <c>previousEmail</c> Platform sent); the e-mail of a platform administrator is now immutable on Platform, so
/// the sync has nothing to move. A body that still carries <c>previousEmail</c> (a FIX3 Platform) is not read: the account
/// is looked up by its address, and an address no account has is 404 — nothing is renamed or adopted.</para>
///
/// <para>Item 5 — THE ACCOUNT'S STATE FOLLOWS THE RECORD'S. The sync used to activate the account and confirm its address
/// on every call, so an account disabled in Auth came back with any administrator edit. Now: the record is Active → the
/// account is activated; any other status (or none, from an older Platform) leaves the account's state alone; the address
/// is confirmed only by accepting the invitation, never here.</para>
///
/// <para>Item 4 — ROLES FIRST, THE ACCOUNT LAST: when the account write fails, the roles the record took away are already
/// gone and the account was not re-enabled.</para>
///
/// <para>BL-521 FIX5 item 12 — this class was PlatformAdminSyncPreviousEmailTests: since FIX4 no previous address travels, so
/// the name described a rule that is gone. Item 14 — what no test measured: a user name another account owns is 409 and
/// nothing is changed; the sync touches ONLY the platform tenant's account (the same address in another tenant is another
/// account and is neither found nor changed).</para>
///
/// <para>Real HTTP on the production Api over a test-owned mongod. Disposable platform users only — never the seeded
/// admin.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PlatformAdminSyncTests : IClassFixture<PlatformAdminSyncTests.Host>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Host _host;

    public PlatformAdminSyncTests(Host host) => _host = host;

    /// <summary>The account write of these addresses fails (after the roles were written) — item 4's order.</summary>
    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        public static readonly ConcurrentDictionary<string, byte> FailingAccountWrites = new(StringComparer.OrdinalIgnoreCase);

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            var original = services.Last(d => d.ServiceType == typeof(IUserRepository));
            services.Remove(original);
            services.AddScoped<IUserRepository>(sp => new AccountWriteFailsFor(
                (IUserRepository)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!)));
        }
    }

    // ── item 1 — the address is a key ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_sync_finds_the_account_by_its_address_and_updates_it_without_touching_the_address()
    {
        var user = await SeedAsync();

        var response = await SyncAsync(user.Email, displayName: "Renamed Account");

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var after = await ByIdAsync(user.Id);
        Assert.Equal((user.Email, "Renamed"), (after!.Email, after.FirstName));
    }

    /// <summary>A FIX3 Platform's body: a new address plus the previous one. The previous address is not read; the new one
    /// belongs to no account → 404, and the account keeps its address.</summary>
    [Fact]
    public async Task A_previous_address_in_the_body_is_not_read_and_no_account_is_moved()
    {
        var user = await SeedAsync();
        var moved = $"moved.{Guid.NewGuid():N}@bl521.test";

        var response = await SyncAsync(moved, previousEmail: user.Email);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(user.Email, (await ByIdAsync(user.Id))!.Email);
        Assert.Null(await ByEmailAsync(moved));
    }

    /// <summary>Another account's address, sent as a "previous" one, is not adopted either.</summary>
    [Fact]
    public async Task An_unknown_address_is_not_found_and_another_account_named_as_previous_is_left_alone()
    {
        var bystander = await SeedAsync();

        var response = await SyncAsync($"nobody.{Guid.NewGuid():N}@bl521.test", previousEmail: bystander.Email, displayName: "Taken Over");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(("Sync", bystander.Email), ((await ByIdAsync(bystander.Id))!.FirstName, (await ByIdAsync(bystander.Id))!.Email));
    }

    // ── item 5 — the account's state follows the record's ───────────────────────────────────────────────

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Suspended", false)]
    [InlineData("Disabled", false)]
    [InlineData(null, false)] // an older Platform that sends no status: nothing is switched on
    public async Task A_disabled_account_is_activated_only_for_an_active_record_and_the_address_is_never_confirmed_here(string? status, bool activated)
    {
        var user = await SeedAsync(active: false, emailConfirmed: false);

        var response = await SyncAsync(user.Email, status: status);

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var after = await ByIdAsync(user.Id);
        Assert.Equal(activated, after!.IsActive);
        Assert.False(after.EmailConfirmed);
    }

    // ── item 4 — roles first, the account last ──────────────────────────────────────────────────────────

    [Fact]
    public async Task When_the_account_write_fails_the_roles_were_already_converged_and_the_account_was_not_re_enabled()
    {
        var user = await SeedAsync(active: false, roles: ["SuperAdmin"]);
        Host.FailingAccountWrites[user.Email] = 0;

        var response = await SyncAsync(user.Email, roles: ["ReadOnly"], status: "Active");

        Assert.True((int)response.StatusCode >= 500, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(["ReadOnly"], await PlatformRolesAsync(user.Id));
        Assert.False((await ByIdAsync(user.Id))!.IsActive);
    }

    // ── BL-521 FIX5 item 14 — what no test measured ─────────────────────────────────────────────────────

    /// <summary>A user name that ANOTHER account of the platform tenant owns is 409 and the account is changed in no way (not its
    /// user name, not its name, not its roles).</summary>
    [Fact]
    public async Task A_user_name_another_account_owns_is_409_and_nothing_is_changed()
    {
        var owner = await SeedAsync(userName: $"owner{Guid.NewGuid():N}"[..20]);
        var user = await SeedAsync(roles: ["SupportAdmin"]);
        var before = await ByIdAsync(user.Id);

        var response = await SyncAsync(user.Email, userName: owner.UserName, displayName: "Renamed By Conflict", roles: ["SuperAdmin"]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var after = await ByIdAsync(user.Id);
        Assert.Equal((before!.UserName, before.FirstName), (after!.UserName, after.FirstName));
        Assert.Equal(["SupportAdmin"], await PlatformRolesAsync(user.Id));
    }

    /// <summary>The same address in ANOTHER tenant is another account: the sync neither finds it (404 — the platform tenant has
    /// none) nor changes it (names, state, roles).</summary>
    [Fact]
    public async Task A_sync_touches_only_the_platform_tenants_account_never_the_same_address_in_another_tenant()
    {
        var otherTenant = Guid.NewGuid();
        var bystander = await SeedAsync(tenantId: otherTenant, active: false, emailConfirmed: false, roles: ["ReadOnly"]);

        var response = await SyncAsync(bystander.Email, displayName: "Overwritten Name", status: "Active", roles: ["SuperAdmin"]);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var untouched = await ByEmailAsync(bystander.Email, otherTenant);
        Assert.Equal((bystander.Id, "Sync", false, false), (untouched!.Id, untouched.FirstName, untouched.IsActive, untouched.EmailConfirmed));
        Assert.Equal(["ReadOnly"], await RolesAsync(bystander.Id, otherTenant));
        Assert.Null(await ByEmailAsync(bystander.Email, PlatformTenantId));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SyncAsync(
        string email,
        string? previousEmail = null,
        string displayName = "Sync Probe",
        string? status = "Active",
        string[]? roles = null,
        string? userName = null,
        string actorType = "PlatformAdmin")
    {
        // As Platform calls it: the internal key and the platform tenant (Auth's tenant resolution requires one here).
        using var client = _host.Client(tenantHeader: PlatformTenantId);
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key",
            _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey);
        return await client.PostAsJsonAsync("api/platform-auth/platform-admins/sync", new
        {
            email,
            previousEmail,
            userName = userName ?? $"sync{Guid.NewGuid():N}"[..20],
            displayName,
            actorType,
            roles = roles ?? ["SupportAdmin"],
            status
        });
    }

    // Merge WP-CT-LANE-BL521-MERGE-01 FIX1 (CT survivors M1 / M2) — the provision of a NEW account, as Platform calls it.
    private async Task<HttpResponseMessage> ProvisionAsync(string email, string actorType)
    {
        using var client = _host.Client(tenantHeader: PlatformTenantId);
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key",
            _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey);
        return await client.PostAsJsonAsync("api/platform-auth/platform-admins/provision", new
        {
            email,
            userName = $"prov{Guid.NewGuid():N}"[..20],
            displayName = "Provision Probe",
            actorType,
            roles = new[] { "SupportAdmin" },
            requirePasswordChange = true,
            status = "Active"
        });
    }

    /// <summary>FIX1 (M1) — a NEW platform account carries the record's actor type in the STORED row from its creation:
    /// read before any sign-in, so a later sign-in's repair cannot hide a provision that forgot it.</summary>
    [Theory]
    [InlineData("PlatformAdmin", "platform_admin")]
    [InlineData("PartnerAdmin", "partner_admin")]
    public async Task A_new_provisioned_account_is_stored_with_the_records_actor_type_before_any_sign_in(string recordType, string stored)
    {
        var email = $"prov.{Guid.NewGuid():N}@bl521.test";

        var response = await ProvisionAsync(email, recordType);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(stored, (await ByEmailAsync(email))!.PlatformActorType);
    }

    /// <summary>FIX1 (M2) — the sync writes the record's actor type onto the STORED account (a different one, and none at
    /// all), read before any sign-in.</summary>
    [Theory]
    [InlineData("platform_admin", "PartnerAdmin", "partner_admin")]
    [InlineData(null, "PlatformAdmin", "platform_admin")]
    public async Task The_sync_stores_the_records_actor_type_on_the_account_before_any_sign_in(string? before, string recordType, string after)
    {
        var user = await SeedAsync(storedActorType: before);

        var response = await SyncAsync(user.Email, actorType: recordType);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(after, (await ByIdAsync(user.Id))!.PlatformActorType);
    }

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 item 6 (d) — the provision of an EXISTING account (Platform's re-invitation, BL-529's
    /// reset) writes the record's actor type onto the STORED row too (a different one, and none at all), read before any
    /// sign-in: the marker line inside the re-invitation is pinned, not left to a later sign-in's repair.</summary>
    [Theory]
    [InlineData(null, "PartnerAdmin", "partner_admin")]
    [InlineData("platform_admin", "PartnerAdmin", "partner_admin")]
    [InlineData(null, "PlatformAdmin", "platform_admin")]
    public async Task A_re_invited_existing_account_is_stored_with_the_records_actor_type_before_any_sign_in(string? before, string recordType, string after)
    {
        var user = await SeedAsync(storedActorType: before);

        var response = await ProvisionAsync(user.Email, recordType);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var stored = (await ByIdAsync(user.Id))!;
        Assert.Equal(after, stored.PlatformActorType);
        Assert.NotNull(stored.PasswordResetTokenHash); // it went through the re-invitation, not a new account
    }

    private async Task<User> SeedAsync(bool active = true, bool emailConfirmed = true, string[]? roles = null, Guid? tenantId = null, string? userName = null,
        string? storedActorType = "platform_admin")
    {
        var tenant = tenantId ?? PlatformTenantId;
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenant);
        var user = new User($"sync.{Guid.NewGuid():N}@bl521.test", sp.GetRequiredService<IPasswordHasher>().Hash($"Unused!{Guid.NewGuid():N}"), "Sync", "Probe", tenant);
        if (emailConfirmed)
        {
            user.ConfirmEmail();
        }

        if (!active)
        {
            user.Deactivate();
        }

        if (userName is not null)
        {
            user.SetUserName(userName);
        }

        if (storedActorType is not null)
        {
            user.SetPlatformActorType(storedActorType);
        }

        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        foreach (var roleName in roles ?? [])
        {
            var role = await sp.GetRequiredService<IRoleRepository>().UpsertSystemRoleAsync(roleName, roleName, "Platform administrator role", tenant, CancellationToken.None);
            await sp.GetRequiredService<IUserRoleRepository>().AssignAsync(new UserRole(user.Id, role.Id, tenant, "bl521-fix5"), CancellationToken.None);
        }

        return user;
    }

    private async Task<string[]> PlatformRolesAsync(Guid userId) =>
        (await RolesAsync(userId, PlatformTenantId)).Where(PlatformAdministratorRoleConvergence.IsPlatformRole).OrderBy(r => r, StringComparer.Ordinal).ToArray();

    private async Task<string[]> RolesAsync(Guid userId, Guid tenantId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var roles = await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>().GetRolesByUserAsync(userId, tenantId, CancellationToken.None);
        return roles.OrderBy(r => r, StringComparer.Ordinal).ToArray();
    }

    private async Task<User?> ByIdAsync(Guid id)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(id, PlatformTenantId, CancellationToken.None);
    }

    private async Task<User?> ByEmailAsync(string email, Guid? tenantId = null)
    {
        var tenant = tenantId ?? PlatformTenantId;
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenant);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByEmailAndTenantAsync(email, tenant, CancellationToken.None);
    }

    /// <summary>The production user repository, except that the account write of a listed address fails.</summary>
    private sealed class AccountWriteFailsFor(IUserRepository inner) : IUserRepository
    {
        // Merge WP-CT-LANE-BL521-MERGE-01 — the sync's account write is BL-529's targeted TryWriteChangesAsync now (the
        // whole-document UpdateForTenantAsync is gone), so that is where the listed address's write fails.
        public Task<bool> TryWriteChangesAsync(User user, object capturedState, Guid tenantId, UserWriteCondition condition, CancellationToken ct) =>
            Host.FailingAccountWrites.ContainsKey(user.Email)
                ? throw new InvalidOperationException("bl521-fix4: the account write fails here")
                : inner.TryWriteChangesAsync(user, capturedState, tenantId, condition, ct);

        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => inner.GetByEmailAndTenantAsync(email, tenantId, ct);
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => inner.GetByIdAndTenantAsync(id, tenantId, ct);
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => inner.GetAllByTenantAsync(tenantId, page, pageSize, ct);
        public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct) => inner.SearchActiveAsync(tenantId, term, limit, ct);
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => inner.GetCountByTenantAsync(tenantId, ct);
        public Task<User> CreateAsync(User user, CancellationToken ct) => inner.CreateAsync(user, ct);
        public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct) => inner.RecordLoginOutcomeAsync(user, tenantId, ct);
        public Task<bool> TryClaimSignInAsync(Guid userId, Guid tenantId, int maxFailedAttempts, CancellationToken ct) => inner.TryClaimSignInAsync(userId, tenantId, maxFailedAttempts, ct);
        public object CaptureState(User user) => inner.CaptureState(user);
        public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct) => inner.SetPasswordResetTokenAsync(userId, tenantId, tokenHash, expiresAtUtc, ct);
        public Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct) => inner.RecordLoginFailureAsync(userId, tenantId, maxFailedAttempts, lockoutDurationMinutes, ct);
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => inner.SoftDeleteAsync(id, tenantId, ct);
        public Task<bool> SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct) => inner.SetPlatformActorTypeAsync(userId, tenantId, actorType, ct);
        public Task<IReadOnlyCollection<Guid>> VisibleUserIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct) => inner.VisibleUserIdsAsync(tenantId, userIds, ct);
    }
}
