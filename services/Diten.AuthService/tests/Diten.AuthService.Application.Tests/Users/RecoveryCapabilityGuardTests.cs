using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Xunit;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-SMALL-02 — the last-steward rule on its own, over <see cref="StewardWorld"/>. The five kinds of change,
/// each in five situations (CT matrix), plus the cases where a change shrinks nothing. REFUSE (true) only when the change
/// takes <c>auth.users.create</c> from a holder and no ACTIVE holder is left.
/// </summary>
public sealed class RecoveryCapabilityGuardTests
{
    public static TheoryData<string> Changes => new()
    {
        nameof(RecoveryCapabilityChange.RemoveUser),
        nameof(RecoveryCapabilityChange.DeactivateUser),
        nameof(RecoveryCapabilityChange.RevokeUserRole),
        nameof(RecoveryCapabilityChange.DeleteRole),
        nameof(RecoveryCapabilityChange.RevokeRolePermission),
    };

    /// <summary>The change of the given kind aimed at <paramref name="candidate"/> holding through <paramref name="role"/>.</summary>
    private static RecoveryCapabilityChange Change(string kind, User candidate, Role role) => kind switch
    {
        nameof(RecoveryCapabilityChange.RemoveUser) => new RecoveryCapabilityChange.RemoveUser(candidate.Id),
        nameof(RecoveryCapabilityChange.DeactivateUser) => new RecoveryCapabilityChange.DeactivateUser(candidate.Id),
        nameof(RecoveryCapabilityChange.RevokeUserRole) => new RecoveryCapabilityChange.RevokeUserRole(candidate.Id, role.Id),
        nameof(RecoveryCapabilityChange.DeleteRole) => new RecoveryCapabilityChange.DeleteRole(role.Id),
        nameof(RecoveryCapabilityChange.RevokeRolePermission) =>
            new RecoveryCapabilityChange.RevokeRolePermission(role.Id, RecoveryCapabilityGuard.RecoveryPermission),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static Task<bool> Refuses(StewardWorld world, RecoveryCapabilityChange change) =>
        world.Guard().WouldLeaveNoActiveHolderAsync(StewardWorld.TenantA, change, CancellationToken.None);

    // ── the matrix ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task The_last_active_holder_is_refused(string kind)
    {
        var world = new StewardWorld();
        var candidate = world.Person("candidate");
        var admins = world.StewardRole();
        world.Assign(candidate, admins);

        Assert.True(await Refuses(world, Change(kind, candidate, admins)));
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task Another_active_holder_lets_it_through(string kind)
    {
        // The other holder holds through ANOTHER role, so deleting / stripping the candidate's role leaves them holding.
        var world = new StewardWorld();
        var candidate = world.Person("candidate");
        var other = world.Person("other");
        var admins = world.StewardRole();
        var owners = world.StewardRole("Owners");
        world.Assign(candidate, admins);
        world.Assign(other, owners);

        Assert.False(await Refuses(world, Change(kind, candidate, admins)));
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task A_candidate_who_holds_nothing_is_let_through_even_with_no_active_holder_left(string kind)
    {
        // BL-558: the tenant's only holder is already inactive; the change touches a plain role and its plain member.
        // Nobody loses the capability, so nothing is refused — the tenant can still tidy up.
        var world = new StewardWorld();
        var candidate = world.Person("candidate");
        var inactiveAdmin = world.Person("inactive-admin", active: false);
        var readers = world.PlainRole();
        var admins = world.StewardRole();
        world.Assign(candidate, readers);
        world.Assign(inactiveAdmin, admins);

        Assert.False(await Refuses(world, Change(kind, candidate, readers)));
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task An_inactive_other_holder_does_not_keep_the_tenant_alive(string kind)
    {
        var world = new StewardWorld();
        var candidate = world.Person("candidate");
        var inactive = world.Person("inactive", active: false);
        var admins = world.StewardRole();
        var owners = world.StewardRole("Owners");
        world.Assign(candidate, admins);
        world.Assign(inactive, owners);

        Assert.True(await Refuses(world, Change(kind, candidate, admins)));
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task A_holder_of_another_tenant_does_not_keep_this_tenant_alive(string kind)
    {
        // The assignment row names a user of tenant B; the tenant-A user read finds nobody.
        var world = new StewardWorld();
        var candidate = world.Person("candidate");
        var foreign = world.Person("foreign", tenantId: StewardWorld.TenantB);
        var admins = world.StewardRole();
        var owners = world.StewardRole("Owners");
        world.Assign(candidate, admins);
        world.Assign(foreign, owners);

        Assert.True(await Refuses(world, Change(kind, candidate, admins)));
    }

    // ── changes that shrink nothing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoking_one_role_from_a_user_who_holds_through_another_live_role_is_let_through()
    {
        var world = new StewardWorld();
        var only = world.Person("only");
        var admins = world.StewardRole();
        var owners = world.StewardRole("Owners");
        world.Assign(only, admins);
        world.Assign(only, owners);

        Assert.False(await Refuses(world, new RecoveryCapabilityChange.RevokeUserRole(only.Id, admins.Id)));
    }

    [Fact]
    public async Task Deleting_a_role_whose_members_hold_through_another_role_is_let_through()
    {
        var world = new StewardWorld();
        var only = world.Person("only");
        var admins = world.StewardRole();
        var owners = world.StewardRole("Owners");
        world.Assign(only, admins);
        world.Assign(only, owners);

        Assert.False(await Refuses(world, new RecoveryCapabilityChange.DeleteRole(admins.Id)));
    }

    [Theory]
    [InlineData("auth.users.read")]
    [InlineData(null)]
    public async Task Revoking_another_or_an_unknown_permission_is_let_through(string? key)
    {
        var world = new StewardWorld();
        var only = world.Person("only");
        var admins = world.StewardRole();
        world.Assign(only, admins);

        Assert.False(await Refuses(world, new RecoveryCapabilityChange.RevokeRolePermission(admins.Id, key)));
    }

    // ── the inactive candidate ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Removing_the_only_holder_who_is_inactive_is_refused()
    {
        // No loosening: deleting the inactive sole holder also destroys the way to switch them back on.
        var world = new StewardWorld();
        var inactiveOnly = world.Person("inactive-only", active: false);
        var admins = world.StewardRole();
        world.Assign(inactiveOnly, admins);

        Assert.True(await Refuses(world, new RecoveryCapabilityChange.RemoveUser(inactiveOnly.Id)));
    }

    [Theory]
    [InlineData(nameof(RecoveryCapabilityChange.DeleteRole))]
    [InlineData(nameof(RecoveryCapabilityChange.RevokeRolePermission))]
    [InlineData(nameof(RecoveryCapabilityChange.RevokeUserRole))]
    public async Task Taking_the_capability_only_from_a_deleted_users_leftover_row_is_let_through(string kind)
    {
        // CT C1 (WP-USERS-ROLES-SMALL-02): the tenant's one steward role is held only by a soft-deleted user's leftover
        // assignment row and nobody is active. The change takes the capability from no real person (BL-558), so it
        // goes through — the "is the lost holder a live user" check is what says so.
        var world = new StewardWorld();
        var ghost = world.Person("ghost");
        ghost.IsDeleted = true;
        var admins = world.StewardRole();
        world.Assign(ghost, admins);

        Assert.False(await Refuses(world, Change(kind, ghost, admins)));
    }

    [Fact]
    public async Task A_deleted_user_on_a_role_row_is_not_a_holder()
    {
        var world = new StewardWorld();
        var candidate = world.Person("candidate");
        var ghost = world.Person("ghost");
        ghost.IsDeleted = true;
        var admins = world.StewardRole();
        world.Assign(candidate, admins);
        world.Assign(ghost, admins);

        Assert.True(await Refuses(world, new RecoveryCapabilityChange.DeactivateUser(candidate.Id)));
    }
}
