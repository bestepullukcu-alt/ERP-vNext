using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-SMALL-02 — the five paths (six handlers) through the REAL last-steward rule over
/// <see cref="StewardWorld"/>: at the tenant's last ACTIVE holder of auth.users.create each answers 409 with its own code
/// and writes NOTHING (no user / assignment / role / grant write, no version bump, no session ended). The directions that
/// can never take the capability away (switching on, disabling what is already off) are never even put to the rule.
/// </summary>
public sealed class LastStewardPathsTests
{
    private readonly StewardWorld _world = new();
    private readonly User _actor;
    private readonly User _steward;
    private readonly Role _admins;

    public LastStewardPathsTests()
    {
        _actor = _world.Person("actor");               // holds nothing: the caller is not the steward
        _steward = _world.Person("steward");
        _admins = _world.StewardRole();
        _world.Assign(_steward, _admins);
    }

    [Fact]
    public async Task Delete_user_is_409_USER_DELETE_LAST_STEWARD_and_deletes_nothing()
    {
        var result = await DeleteUser(_world.Guard()).Handle(new DeleteUserCommand(_steward.Id), CancellationToken.None);

        AssertRefused(result, UserErrorCodes.DeleteLastSteward);
        Assert.False(_steward.IsDeleted);
        AssertNothingWritten();
    }

    [Fact]
    public async Task Disable_is_409_USER_DEACTIVATE_LAST_STEWARD_and_writes_nothing()
    {
        var result = await SetActive(_world.Guard()).Handle(new SetUserActiveStatusCommand(_steward.Id, false), CancellationToken.None);

        AssertRefused(result, UserErrorCodes.DeactivateLastSteward);
        Assert.True(_steward.IsActive);
        AssertNothingWritten();
    }

    [Fact]
    public async Task The_edit_forms_Active_switch_is_the_same_409_and_the_name_is_not_written_either()
    {
        var result = await UpdateUser(_world.Guard()).Handle(
            new UpdateUserCommand(_steward.Id, "Renamed", "Meanwhile", IsActive: false), CancellationToken.None);

        AssertRefused(result, UserErrorCodes.DeactivateLastSteward);
        Assert.True(_steward.IsActive);
        Assert.Equal("Ad", _steward.FirstName);
        AssertNothingWritten();
    }

    [Fact]
    public async Task Revoking_the_users_role_is_409_USER_ROLE_REVOKE_LAST_STEWARD_and_writes_nothing()
    {
        var result = await RevokeRole(_world.Guard()).Handle(new RevokeRoleCommand(_steward.Id, _admins.Id), CancellationToken.None);

        AssertRefused(result, RoleErrorCodes.UserRoleRevokeLastSteward);
        Assert.True(_world.HasAssignment(_steward, _admins));
        Assert.Equal(0, _world.UserRoleRepository.Revokes);
        AssertNothingWritten();
    }

    [Fact]
    public async Task Deleting_the_role_is_409_ROLE_DELETE_LAST_STEWARD_and_deletes_nothing()
    {
        var result = await DeleteRole(_world.Guard()).Handle(new DeleteRoleCommand(_admins.Id), CancellationToken.None);

        AssertRefused(result, RoleErrorCodes.DeleteLastSteward);
        Assert.False(_admins.IsDeleted);
        Assert.Equal(0, _world.RoleDeletes);
        AssertNothingWritten();
    }

    [Fact]
    public async Task Revoking_auth_users_create_from_the_role_is_409_ROLE_PERMISSION_REVOKE_LAST_STEWARD_and_writes_nothing()
    {
        var result = await RevokePermission(_world.Guard()).Handle(
            new RevokePermissionCommand(_admins.Id, StewardWorld.CreateUsers.Id), CancellationToken.None);

        AssertRefused(result, RoleErrorCodes.PermissionRevokeLastSteward);
        Assert.True(_world.HasGrant(_admins, StewardWorld.CreateUsers));
        Assert.Equal(0, _world.RolePermissionRepository.Revokes);
        AssertNothingWritten();
    }

    // ── what the rule must never stop ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Switching_an_account_on_is_never_put_to_the_rule_on_either_door()
    {
        // A rule that refuses everything, asked nothing: the kebab's enable and the form's Active switch.
        var world = new StewardWorld();
        var actor = world.Person("actor");
        var inactive = world.Person("inactive", active: false);
        var guard = RecoveryGuardForTests.Refuses();

        var kebab = await SetActive(guard, world, actor).Handle(new SetUserActiveStatusCommand(inactive.Id, true), CancellationToken.None);
        Assert.True(kebab.IsSuccessful, Codes(kebab));
        inactive.Deactivate();
        var form = await UpdateUser(guard, world, actor).Handle(new UpdateUserCommand(inactive.Id, "Ad", "On", IsActive: true), CancellationToken.None);
        Assert.True(form.IsSuccessful, Codes(form));

        Assert.Empty(guard.Asked);
    }

    [Fact]
    public async Task Disabling_an_account_that_is_already_off_stays_204_and_is_not_put_to_the_rule()
    {
        var world = new StewardWorld();
        var actor = world.Person("actor");
        var inactive = world.Person("inactive", active: false);
        var admins = world.StewardRole();
        world.Assign(inactive, admins);
        var guard = RecoveryGuardForTests.Refuses();

        var result = await SetActive(guard, world, actor).Handle(new SetUserActiveStatusCommand(inactive.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccessful, Codes(result));
        Assert.Equal(204, result.StatusCode);
        Assert.Empty(guard.Asked);
    }

    [Fact]
    public async Task A_managed_grant_is_still_refused_as_managed_first()
    {
        // The order stands: a System grant of auth.users.create on the last holder's role answers GRANT_MANAGED, not the
        // last-steward code — the rule is never reached.
        var world = new StewardWorld();
        var actor = world.Person("actor");
        var steward = world.Person("steward");
        var system = world.RoleWith("SystemAdmins", (StewardWorld.CreateUsers, GrantSource.System));
        world.Assign(steward, system);
        var guard = RecoveryGuardForTests.Refuses();

        var result = await RevokePermission(guard, world).Handle(
            new RevokePermissionCommand(system.Id, StewardWorld.CreateUsers.Id), CancellationToken.None);

        AssertRefused(result, RoleErrorCodes.PermissionGrantManaged);
        Assert.Empty(guard.Asked);
    }

    [Fact]
    public async Task An_unknown_permission_key_is_not_auth_users_create_and_the_revoke_goes_on()
    {
        // The catalog does not know the grant's permission (key null): the rule lets it through, the flow continues.
        var world = new StewardWorld();
        var actor = world.Person("actor");
        var steward = world.Person("steward");
        var unknown = new Permission("legacy", "thing", "do", "Legacy", null);
        var admins = world.RoleWith("Admins", (StewardWorld.CreateUsers, GrantSource.Manual), (unknown, GrantSource.Manual));
        world.Assign(steward, admins);

        var result = await RevokePermission(world.Guard(), world).Handle(
            new RevokePermissionCommand(admins.Id, unknown.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful, Codes(result));
        Assert.Equal(1, world.RolePermissionRepository.Revokes);
        Assert.True(world.HasGrant(admins, StewardWorld.CreateUsers));
    }

    [Theory]
    [InlineData("delete-user")]
    [InlineData("disable")]
    [InlineData("form-switch")]
    [InlineData("revoke-role")]
    [InlineData("delete-role")]
    [InlineData("revoke-permission")]
    public async Task With_a_second_active_holder_the_same_path_goes_through(string path)
    {
        // Non-vacuity for the refusals above: a fresh world, the same steward, plus one more ACTIVE holder (another role).
        var w = new StewardWorld();
        var actor = w.Person("actor");
        var steward = w.Person("steward");
        var other = w.Person("other");
        var admins = w.StewardRole();
        var owners = w.StewardRole("Owners");
        w.Assign(steward, admins);
        w.Assign(other, owners);

        var ok = path switch
        {
            "delete-user" => (await DeleteUser(w.Guard(), w, actor).Handle(new DeleteUserCommand(steward.Id), CancellationToken.None)).IsSuccessful,
            "disable" => (await SetActive(w.Guard(), w, actor).Handle(new SetUserActiveStatusCommand(steward.Id, false), CancellationToken.None)).IsSuccessful,
            "form-switch" => (await UpdateUser(w.Guard(), w, actor).Handle(new UpdateUserCommand(steward.Id, "Ad", "Off", IsActive: false), CancellationToken.None)).IsSuccessful,
            "revoke-role" => (await RevokeRole(w.Guard(), w).Handle(new RevokeRoleCommand(steward.Id, admins.Id), CancellationToken.None)).IsSuccessful,
            "delete-role" => (await DeleteRole(w.Guard(), w, actor).Handle(new DeleteRoleCommand(admins.Id), CancellationToken.None)).IsSuccessful,
            "revoke-permission" => (await RevokePermission(w.Guard(), w).Handle(new RevokePermissionCommand(admins.Id, StewardWorld.CreateUsers.Id), CancellationToken.None)).IsSuccessful,
            _ => throw new ArgumentOutOfRangeException(nameof(path))
        };

        Assert.True(ok, path);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Codes<T>(Response<T> r) => string.Join(",", r.ErrorCodes.Select(e => e.Code));

    private static void AssertRefused<T>(Response<T> result, string code)
    {
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(code, Assert.Single(result.ErrorCodes).Code);
    }

    private void AssertNothingWritten()
    {
        Assert.Equal(0, _world.UserRepository.ConditionalWrites);
        Assert.Equal(0, _world.Tokens.Revocations);
        Assert.Equal(0, _world.Version.IncrementCount);
    }

    private DeleteUserCommandHandler DeleteUser(IRecoveryCapabilityGuard guard) => DeleteUser(guard, _world, _actor);
    private SetUserActiveStatusCommandHandler SetActive(IRecoveryCapabilityGuard guard) => SetActive(guard, _world, _actor);
    private UpdateUserCommandHandler UpdateUser(IRecoveryCapabilityGuard guard) => UpdateUser(guard, _world, _actor);
    private RevokeRoleCommandHandler RevokeRole(IRecoveryCapabilityGuard guard) => RevokeRole(guard, _world);
    private DeleteRoleCommandHandler DeleteRole(IRecoveryCapabilityGuard guard) => DeleteRole(guard, _world, _actor);
    private RevokePermissionCommandHandler RevokePermission(IRecoveryCapabilityGuard guard) => RevokePermission(guard, _world);

    private static DeleteUserCommandHandler DeleteUser(IRecoveryCapabilityGuard guard, StewardWorld w, User actor) =>
        new(w.UserRepository, guard, Tenant(), new Actor(actor.Id), UserAuditForTests.None(), NullLogger<DeleteUserCommandHandler>.Instance);

    private static SetUserActiveStatusCommandHandler SetActive(IRecoveryCapabilityGuard guard, StewardWorld w, User actor) =>
        new(w.UserRepository, w.Tokens, Tenant(), new Actor(actor.Id), UserAuditForTests.None(), new RecordingUserQuotaClient(),
            guard, NullLogger<SetUserActiveStatusCommandHandler>.Instance);

    private static UpdateUserCommandHandler UpdateUser(IRecoveryCapabilityGuard guard, StewardWorld w, User actor) =>
        new(w.UserRepository, w.UserRoleRepository, Tenant(), new AccountKindWriter(UserAuditForTests.None()), UserAuditForTests.None(),
            new RecordingUserQuotaClient(), new Actor(actor.Id), w.Tokens, guard, NullLogger<UpdateUserCommandHandler>.Instance);

    private static RevokeRoleCommandHandler RevokeRole(IRecoveryCapabilityGuard guard, StewardWorld w) =>
        new(w.UserRepository, w.RoleRepository, w.UserRoleRepository, w.Tokens, w.Version, Tenant(), UserAuditForTests.None(),
            guard, NullLogger<RevokeRoleCommandHandler>.Instance);

    private static DeleteRoleCommandHandler DeleteRole(IRecoveryCapabilityGuard guard, StewardWorld w, User actor) =>
        new(w.RoleRepository, w.Version, Tenant(), RoleAuditForTests.Over(new NoLocalAudit()), new Actor(actor.Id), guard,
            NullLogger<DeleteRoleCommandHandler>.Instance);

    private static RevokePermissionCommandHandler RevokePermission(IRecoveryCapabilityGuard guard, StewardWorld w) =>
        new(w.RoleRepository, w.RolePermissionRepository, w.UserRoleRepository, w.UserRepository, w.Tokens, w.Version, Tenant(),
            new StewardWorld.Catalog(), RoleAuditForTests.Over(new NoLocalAudit()), guard);

    private static ITenantContext Tenant()
    {
        var tenant = new FixedTenant();
        tenant.SetTenant(StewardWorld.TenantA);
        return tenant;
    }

    private sealed class FixedTenant : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
    }

    private sealed class Actor(Guid id) : ICurrentUserAccessor
    {
        public Guid? UserId => id;
    }

    private sealed class NoLocalAudit : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
    }
}
