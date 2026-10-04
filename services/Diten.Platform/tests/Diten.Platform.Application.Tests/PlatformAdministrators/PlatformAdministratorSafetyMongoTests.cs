using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;
using Diten.Platform.Application.Security;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX2 item 5 — FIX1 made an administrator record's actor type and status take effect on the next request, so
/// the record itself must be protected: (a) an administrator cannot change its OWN actor type, status or e-mail (403,
/// <see cref="AdminSafetyReasonCodes.SelfChangeForbidden"/>); (b) the LAST active platform administrator holding SuperAdmin
/// cannot be demoted, suspended, disabled, stripped of SuperAdmin or deleted (409,
/// <see cref="AdminSafetyReasonCodes.LastActiveSuperAdmin"/>), and a PartnerAdmin record carrying the SuperAdmin role does
/// not count as one.
///
/// <para>FIX3 — (1) nor its own ROLES, on the update path or the role assignment path (a ReadOnly administrator made itself
/// SuperAdmin); (2a) an update reaches Auth FIRST, under the record's previous address, and the record is written only if
/// Auth took it (a write refused afterwards puts Auth back); (2b) moving the last platform super administrator to another
/// e-mail counts as taking it out of the set; (2c) the system seed administrator's e-mail does not change, it is not
/// suspended through the update path, and the bulk delete does not delete it.</para>
///
/// <para>Measured on the production handlers, the production <see cref="ActorSafetyGuard"/> and the production
/// <see cref="PlatformAdministratorRepository"/> (its count query included) over a test-owned replica set — never the
/// shared database, never the seeded admin.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorSafetyMongoTests
{
    // ── (a) own actor type, status, e-mail ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("actorType")]
    [InlineData("status")]
    [InlineData("email")]
    public async Task An_administrator_cannot_change_its_own_actor_type_status_or_email(string field)
    {
        await using var world = await World.StartAsync();
        var self = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]); // not the last: only (a) can refuse
        var before = await world.ReadAsync(self.Id);

        var response = await world.UpdateAsync(self, actingAs: self, field switch
        {
            "actorType" => r => r with { ActorType = "PartnerAdmin", PartnerId = Guid.NewGuid(), AllowedTenantIds = [Guid.NewGuid()] },
            "status" => r => r with { Status = "Suspended" },
            _ => r => r with { Email = $"moved.{Guid.NewGuid():N}@bl521.test" }
        });

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(AdminSafetyReasonCodes.SelfChangeForbidden, response.ReasonCode);
        Assert.Equal(UpdatePlatformAdministratorHandler.SelfChangeMessage, Assert.Single(response.Errors));
        var after = await world.ReadAsync(self.Id);
        Assert.Equal((before.ActorType, before.Status, before.Email, before.Version), (after.ActorType, after.Status, after.Email, after.Version));
    }

    [Fact]
    public async Task An_administrator_may_still_edit_its_own_name()
    {
        await using var world = await World.StartAsync();
        var self = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);

        var response = await world.UpdateAsync(self, actingAs: self, r => r with { DisplayName = "Renamed Self" });

        Assert.Equal(204, response.StatusCode);
        Assert.Equal("Renamed Self", (await world.ReadAsync(self.Id)).DisplayName);
    }

    // ── FIX3 item 1 — own roles, on both paths ───────────────────────────────────────────────────────────

    /// <summary>The reported case: a ReadOnly administrator makes itself SuperAdmin. Update would have synced the roles to
    /// Auth, and the next token would have carried SuperAdmin's permissions to every service that trusts it.</summary>
    [Theory]
    [InlineData("update")]
    [InlineData("assignRoles")]
    public async Task A_read_only_administrator_cannot_give_itself_super_admin_on_either_path(string path)
    {
        await using var world = await World.StartAsync();
        var self = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.ReadOnly]);
        await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var before = await world.ReadAsync(self.Id);

        var response = path == "update"
            ? await world.UpdateAsync(self, actingAs: self, r => r with { Roles = ["SuperAdmin"] })
            : await world.AssignRolesAsync(self, self, ["ReadOnly", "SuperAdmin"]);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(AdminSafetyReasonCodes.SelfChangeForbidden, response.ReasonCode);
        var after = await world.ReadAsync(self.Id);
        Assert.Equal((before.Roles.Single(), before.Version), (after.Roles.Single(), after.Version));
        Assert.Empty(world.Auth.Synced); // nothing reached Auth either
    }

    /// <summary>Removing its own role is the same rule (it was a 409 of its own on the role assignment path).</summary>
    [Fact]
    public async Task An_administrator_cannot_drop_its_own_roles_either()
    {
        await using var world = await World.StartAsync();
        var self = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin, AdministratorRole.BillingAdmin]);
        await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);

        var response = await world.AssignRolesAsync(self, self, ["SuperAdmin"]);

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(AdminSafetyReasonCodes.SelfChangeForbidden, response.ReasonCode);
        Assert.Equal(2, (await world.ReadAsync(self.Id)).Roles.Count);
    }

    // ── (b) the last active platform super administrator ─────────────────────────────────────────────────

    [Theory]
    [InlineData("toPartner")]
    [InlineData("suspend")]
    [InlineData("disable")]
    [InlineData("dropSuperAdmin")]
    [InlineData("email")] // FIX3 item 2(b)
    public async Task The_last_active_platform_super_admin_cannot_be_demoted_or_suspended_through_update(string change)
    {
        await using var world = await World.StartAsync();
        var (last, actor) = await world.LastSuperAdminWorldAsync();

        var response = await world.UpdateAsync(last, actingAs: actor, change switch
        {
            "toPartner" => r => r with { ActorType = "PartnerAdmin", PartnerId = Guid.NewGuid(), AllowedTenantIds = [Guid.NewGuid()] },
            "suspend" => r => r with { Status = "Suspended" },
            "disable" => r => r with { Status = "Disabled" },
            "email" => r => r with { Email = $"moved.{Guid.NewGuid():N}@bl521.test" },
            _ => r => r with { Roles = ["SupportAdmin"] }
        });

        AssertLastSuperAdminRefusal(response);
        await world.AssertStillPlatformSuperAdminAsync(last.Id);
        Assert.Equal(last.Email, (await world.ReadAsync(last.Id)).Email);
        Assert.Empty(world.Auth.Synced);
    }

    [Fact]
    public async Task The_last_active_platform_super_admin_cannot_be_suspended_deleted_bulk_deleted_or_stripped()
    {
        await using var world = await World.StartAsync();
        var (last, actor) = await world.LastSuperAdminWorldAsync();

        AssertLastSuperAdminRefusal(await world.SuspendAsync(last, actor));
        AssertLastSuperAdminRefusal(await world.DeleteAsync(last, actor));
        AssertLastSuperAdminRefusal(await world.BulkDeleteAsync(actor, last));
        AssertLastSuperAdminRefusal(await world.AssignRolesAsync(last, actor, ["SupportAdmin"]));
        await world.AssertStillPlatformSuperAdminAsync(last.Id);
    }

    [Fact]
    public async Task With_a_second_active_platform_super_admin_the_demotion_goes_through()
    {
        await using var world = await World.StartAsync();
        var (last, actor) = await world.LastSuperAdminWorldAsync();
        await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);

        var response = await world.UpdateAsync(last, actingAs: actor, r => r with { ActorType = "PartnerAdmin", PartnerId = Guid.NewGuid(), AllowedTenantIds = [Guid.NewGuid()] });

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(ActorType.PartnerAdmin, (await world.ReadAsync(last.Id)).ActorType);
    }

    /// <summary>A partner record holding SuperAdmin is not protected as a super administrator: it can be suspended even
    /// when no platform super administrator is left to count.</summary>
    [Fact]
    public async Task A_partner_record_holding_super_admin_is_not_protected_as_one()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var partner = await world.SeedAsync(ActorType.PartnerAdmin, [AdministratorRole.SuperAdmin]);

        var response = await world.SuspendAsync(partner, actor);

        Assert.True(response.StatusCode == 204, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdministratorStatus.Suspended, (await world.ReadAsync(partner.Id)).Status);
    }

    // ── FIX3 item 2(a) — Auth first, the record only if Auth took it ─────────────────────────────────────

    /// <summary>Changing another administrator's e-mail: Auth is asked FIRST, under the record's previous address, and
    /// the record follows.</summary>
    [Fact]
    public async Task An_email_change_reaches_auth_first_under_the_previous_address_and_then_the_record()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var moved = $"moved.{Guid.NewGuid():N}@bl521.test";
        world.Auth.OnSync = async _ => Assert.Equal(target.Email, (await world.ReadAsync(target.Id)).Email); // not written yet

        var response = await world.UpdateAsync(target, actingAs: actor, r => r with { Email = moved });

        Assert.True(response.StatusCode == 204, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        var sync = Assert.Single(world.Auth.Synced);
        Assert.Equal((moved, target.Email), (sync.Email, sync.PreviousEmail));
        Assert.Equal(moved, (await world.ReadAsync(target.Id)).Email);
    }

    /// <summary>The reported case: Auth refuses (it does not know the account under that address) — the record is NOT
    /// written. Before, it was already saved and pointed at an address Auth did not have.</summary>
    [Fact]
    public async Task When_auth_refuses_the_sync_the_record_is_not_written()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var before = await world.ReadAsync(target.Id);
        world.Auth.OnSync = request => request.PreviousEmail == target.Email
            ? throw new InvalidOperationException("platform user not found")
            : Task.CompletedTask;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.UpdateAsync(target, actingAs: actor, r => r with { Email = $"moved.{Guid.NewGuid():N}@bl521.test", Roles = ["BillingAdmin"] }));

        var after = await world.ReadAsync(target.Id);
        Assert.Equal((before.Email, before.Roles.Single(), before.Version), (after.Email, after.Roles.Single(), after.Version));
        // whatever Auth may have applied is put back to the record's values, found under the record's address
        var restore = world.Auth.Synced[^1];
        Assert.Equal((target.Email, "SupportAdmin"), (restore.Email, restore.Roles.Single()));
    }

    /// <summary>Auth took the change but the record write is then refused (here: another user saved the record in
    /// between) — Auth is put back to the record's values, found under the NEW address it was just moved to.</summary>
    [Fact]
    public async Task When_the_record_write_is_refused_after_the_sync_auth_is_put_back()
    {
        await using var world = await World.StartAsync();
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var moved = $"moved.{Guid.NewGuid():N}@bl521.test";
        var concurrentEditDone = false;
        world.Auth.OnSync = async _ =>
        {
            if (concurrentEditDone) return;
            concurrentEditDone = true;
            await world.BumpVersionAsync(target.Id); // someone else saved the record while Auth was being synced
        };

        var response = await world.UpdateAsync(target, actingAs: actor, r => r with { Email = moved });

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(target.Email, (await world.ReadAsync(target.Id)).Email);
        Assert.Equal(2, world.Auth.Synced.Count);
        var restore = world.Auth.Synced[1];
        Assert.Equal((target.Email, moved, "SupportAdmin"), (restore.Email, restore.PreviousEmail, restore.Roles.Single()));
    }

    // ── FIX3 item 2(c) — the system seed administrator ───────────────────────────────────────────────────

    [Theory]
    [InlineData("email")]
    [InlineData("suspend")]
    [InlineData("disable")]
    public async Task The_system_seed_administrators_email_and_status_do_not_change_through_update(string change)
    {
        await using var world = await World.StartAsync();
        var seed = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin], email: "admin@diten.com");
        await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]); // not the last: only the seed rule refuses
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var before = await world.ReadAsync(seed.Id);

        var response = await world.UpdateAsync(seed, actingAs: actor, change switch
        {
            "email" => r => r with { Email = $"renamed.seed.{Guid.NewGuid():N}@bl521.test" },
            "suspend" => r => r with { Status = "Suspended" },
            _ => r => r with { Status = "Disabled" }
        });

        Assert.True(response.StatusCode == 409, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdminSafetyReasonCodes.SystemSeedProtected, response.ReasonCode);
        var after = await world.ReadAsync(seed.Id);
        Assert.Equal((before.Email, before.Status, before.Version), (after.Email, after.Status, after.Version));
        Assert.Empty(world.Auth.Synced);
    }

    /// <summary>The seed in a bulk delete refuses the whole request — and nothing else in it is deleted first.</summary>
    [Fact]
    public async Task The_system_seed_administrator_is_not_bulk_deleted_and_nothing_else_in_the_request_is()
    {
        await using var world = await World.StartAsync();
        var seed = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin], email: "admin@diten.com");
        await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var other = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.ReadOnly]);
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);

        var response = await world.BulkDeleteAsync(actor, other, seed);

        Assert.True(response.StatusCode == 409, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdminSafetyReasonCodes.SystemSeedProtected, response.ReasonCode);
        Assert.False((await world.ReadAsync(seed.Id)).IsDeleted);
        Assert.False((await world.ReadAsync(other.Id)).IsDeleted);
    }

    private static void AssertLastSuperAdminRefusal(Response<NoContent> response)
    {
        Assert.True(response.StatusCode == 409, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdminSafetyReasonCodes.LastActiveSuperAdmin, response.ReasonCode);
    }

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World : IAsyncDisposable
    {
        private readonly DisposableMongoReplicaSet _mongo;
        private readonly IMongoDatabase _database;
        private readonly PlatformAdministratorRepository _repository;

        private World(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            _database = database;
            _repository = new PlatformAdministratorRepository(new PlatformDbContext(mongo.Client, database), new TenantContext());
        }

        /// <summary>What the handlers asked Auth to do, in order (FIX3 item 2(a)).</summary>
        public RecordingAuth Auth { get; } = new();

        public static async Task<World> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                         .Where(c => c.Name is PlatformCollections.PlatformAdministrators or PlatformCollections.PlatformAdministratorSafetyGuards))
            {
                await collection.ApplyAsync(database, CancellationToken.None);
            }

            return new World(mongo, database);
        }

        /// <summary>
        /// The target is the ONLY active platform administrator holding SuperAdmin. Around it: a PartnerAdmin record that
        /// holds the SuperAdmin role (must not count), a suspended platform super admin (must not count), and the acting
        /// administrator — a platform administrator without SuperAdmin.
        /// </summary>
        public async Task<(PlatformAdministrator Last, PlatformAdministrator Actor)> LastSuperAdminWorldAsync()
        {
            var last = await SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
            await SeedAsync(ActorType.PartnerAdmin, [AdministratorRole.SuperAdmin]);
            await SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin], AdministratorStatus.Suspended);
            var actor = await SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
            return (last, actor);
        }

        public async Task<PlatformAdministrator> SeedAsync(
            ActorType type,
            IReadOnlyList<AdministratorRole> roles,
            AdministratorStatus status = AdministratorStatus.Active,
            string? email = null)
        {
            email ??= $"safety.{Guid.NewGuid():N}@bl521.test";
            var administrator = new PlatformAdministrator
            {
                Email = email, NormalizedEmail = email, UserName = email, NormalizedUserName = email, DisplayName = "Safety Probe",
                ActorType = type, PartnerId = type == ActorType.PartnerAdmin ? Guid.NewGuid() : null,
                AllowedTenantIds = type == ActorType.PartnerAdmin ? [Guid.NewGuid()] : [],
                Roles = roles.ToList(), Status = status,
                InvitationStatus = AdministratorInvitationStatus.Accepted, Version = 1, CreatedBy = "bl521-fix2"
            };
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(administrator);
            return administrator;
        }

        public async Task<PlatformAdministrator> ReadAsync(Guid id) =>
            await _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).Find(x => x.Id == id).SingleAsync();

        /// <summary>Another user's save of the record: only its version moves.</summary>
        public Task BumpVersionAsync(Guid id) =>
            _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators)
                .UpdateOneAsync(x => x.Id == id, Builders<PlatformAdministrator>.Update.Inc(x => x.Version, 1));

        public async Task AssertStillPlatformSuperAdminAsync(Guid id)
        {
            var record = await ReadAsync(id);
            Assert.False(record.IsDeleted);
            Assert.Equal(AdministratorStatus.Active, record.Status);
            Assert.Equal(ActorType.PlatformAdmin, record.ActorType);
            Assert.Contains(AdministratorRole.SuperAdmin, record.Roles);
        }

        public async Task<Response<NoContent>> UpdateAsync(
            PlatformAdministrator target,
            PlatformAdministrator actingAs,
            Func<UpdatePlatformAdministratorRequest, UpdatePlatformAdministratorRequest> change)
        {
            var current = await ReadAsync(target.Id);
            var request = change(new UpdatePlatformAdministratorRequest(
                current.Email, current.UserName, current.DisplayName, current.ActorType.ToString(), current.PartnerId,
                current.AllowedTenantIds, current.Status.ToString(), current.Roles.Select(r => r.ToString()).ToList(), current.Version));
            var user = new Actor(actingAs.Email);
            return await new UpdatePlatformAdministratorHandler(_repository, Auth, user, new ActorSafetyGuard(user, _repository),
                    NullLogger<UpdatePlatformAdministratorHandler>.Instance)
                .Handle(new UpdatePlatformAdministratorCommand(target.Id, request), CancellationToken.None);
        }

        public async Task<Response<NoContent>> SuspendAsync(PlatformAdministrator target, PlatformAdministrator actingAs)
        {
            var user = new Actor(actingAs.Email);
            return await new SuspendPlatformAdministratorHandler(_repository, user, new ActorSafetyGuard(user, _repository))
                .Handle(new SuspendPlatformAdministratorCommand(target.Id, new PlatformAdministratorStatusRequest("x", (await ReadAsync(target.Id)).Version)), CancellationToken.None);
        }

        public async Task<Response<NoContent>> DeleteAsync(PlatformAdministrator target, PlatformAdministrator actingAs)
        {
            var user = new Actor(actingAs.Email);
            return await new DeletePlatformAdministratorHandler(_repository, user, new ActorSafetyGuard(user, _repository))
                .Handle(new DeletePlatformAdministratorCommand(target.Id, new PlatformAdministratorVersionRequest((await ReadAsync(target.Id)).Version)), CancellationToken.None);
        }

        public async Task<Response<NoContent>> BulkDeleteAsync(PlatformAdministrator actingAs, params PlatformAdministrator[] targets)
        {
            var user = new Actor(actingAs.Email);
            var items = new List<PlatformAdministratorBulkDeleteItemRequest>();
            foreach (var target in targets)
            {
                items.Add(new PlatformAdministratorBulkDeleteItemRequest(target.Id, (await ReadAsync(target.Id)).Version));
            }

            return await new BulkDeletePlatformAdministratorsHandler(_repository, user, new ActorSafetyGuard(user, _repository))
                .Handle(new BulkDeletePlatformAdministratorsCommand(items), CancellationToken.None);
        }

        public async Task<Response<NoContent>> AssignRolesAsync(PlatformAdministrator target, PlatformAdministrator actingAs, IReadOnlyList<string> roles)
        {
            var user = new Actor(actingAs.Email);
            return await new AssignPlatformAdministratorRolesHandler(_repository, user, new ActorSafetyGuard(user, _repository))
                .Handle(new AssignPlatformAdministratorRolesCommand(target.Id, new AssignPlatformAdministratorRolesRequest(roles, (await ReadAsync(target.Id)).Version)), CancellationToken.None);
        }

        public ValueTask DisposeAsync() => _mongo.DisposeAsync();
    }

    private sealed class Actor(string email) : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string? Email => email;
        public string? DisplayName => "Acting Administrator";
        public string ActorName => email;
        public bool IsAuthenticated => true;
    }

    /// <summary>Auth's side of the sync: every request recorded in order; <see cref="OnSync"/> answers it (throw = Auth
    /// refused).</summary>
    private sealed class RecordingAuth : IPlatformAdministratorProvisioningService
    {
        public List<PlatformAdministratorProvisioningSyncRequest> Synced { get; } = [];

        public Func<PlatformAdministratorProvisioningSyncRequest, Task>? OnSync { get; set; }

        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct) =>
            Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));

        public async Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct)
        {
            Synced.Add(request);
            if (OnSync is not null)
            {
                await OnSync(request);
            }
        }
    }
}
