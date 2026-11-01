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
/// SuperAdmin); (2a) an update reaches Auth FIRST and the record is written only if Auth took it; (2c) the system seed
/// administrator's e-mail does not change, it is not suspended through the update path, and the bulk delete does not
/// delete it.</para>
///
/// <para>FIX4 — (1) NOBODY's e-mail changes (409 <see cref="AdminSafetyReasonCodes.EmailImmutable"/>): FIX3's move of it and
/// its "the last super administrator's e-mail change is a demotion" rule are gone; (2) a record write refused after the
/// sync converges Auth to the record as it is NOW, never to the values this request loaded.</para>
///
/// <para>Measured on the production handlers, the production <see cref="ActorSafetyGuard"/> and the production
/// <see cref="PlatformAdministratorRepository"/> (its count query included) over a test-owned replica set — never the
/// shared database, never the seeded admin.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class PlatformAdministratorSafetyMongoTests : IClassFixture<ReplicaSetClassFixture>
{
    // TEST-HIZ-01: one replica set for the class (ReplicaSetClassFixture); each test starts on this database, emptied.
    private const string DatabaseName = "diten_platform_tx_admin_safety";

    private readonly DisposableMongoReplicaSet _mongo;

    public PlatformAdministratorSafetyMongoTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    // ── (a) own actor type, status, e-mail ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("actorType")]
    [InlineData("status")]
    [InlineData("email")]
    public async Task An_administrator_cannot_change_its_own_actor_type_status_or_email(string field)
    {
        await using var world = await World.StartAsync(_mongo);
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
        await using var world = await World.StartAsync(_mongo);
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
        await using var world = await World.StartAsync(_mongo);
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
        await using var world = await World.StartAsync(_mongo);
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
    public async Task The_last_active_platform_super_admin_cannot_be_demoted_or_suspended_through_update(string change)
    {
        await using var world = await World.StartAsync(_mongo);
        var (last, actor) = await world.LastSuperAdminWorldAsync();

        var response = await world.UpdateAsync(last, actingAs: actor, change switch
        {
            "toPartner" => r => r with { ActorType = "PartnerAdmin", PartnerId = Guid.NewGuid(), AllowedTenantIds = [Guid.NewGuid()] },
            "suspend" => r => r with { Status = "Suspended" },
            "disable" => r => r with { Status = "Disabled" },
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
        await using var world = await World.StartAsync(_mongo);
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
        await using var world = await World.StartAsync(_mongo);
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
        await using var world = await World.StartAsync(_mongo);
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var partner = await world.SeedAsync(ActorType.PartnerAdmin, [AdministratorRole.SuperAdmin]);

        var response = await world.SuspendAsync(partner, actor);

        Assert.True(response.StatusCode == 204, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdministratorStatus.Suspended, (await world.ReadAsync(partner.Id)).Status);
    }

    // ── FIX4 item 1 — an administrator's e-mail never changes ────────────────────────────────────────────

    /// <summary>
    /// The e-mail is the identity key Platform and Auth match an administrator by; FIX3's way of moving it (Auth first under
    /// the previous address, put back on a refused write) left windows in which the two disagreed. Now nobody changes it —
    /// not even for the last super administrator, where FIX3 counted the move as a demotion: 409, nothing written, nothing
    /// sent to Auth. A new address is invited as a new administrator.
    /// </summary>
    [Theory]
    [InlineData("ordinary")]
    [InlineData("lastSuperAdmin")]
    public async Task Another_administrators_email_cannot_be_changed_409_and_nothing_reaches_auth(string who)
    {
        await using var world = await World.StartAsync(_mongo);
        PlatformAdministrator target, actor;
        if (who == "lastSuperAdmin")
        {
            (target, actor) = await world.LastSuperAdminWorldAsync();
        }
        else
        {
            actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
            target = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        }

        var before = await world.ReadAsync(target.Id);

        var response = await world.UpdateAsync(target, actingAs: actor, r => r with { Email = $"moved.{Guid.NewGuid():N}@bl521.test" });

        Assert.True(response.StatusCode == 409, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal(AdminSafetyReasonCodes.EmailImmutable, response.ReasonCode);
        Assert.Equal(AdminSafetyMessages.EmailImmutable, Assert.Single(response.Errors));
        var after = await world.ReadAsync(target.Id);
        Assert.Equal((before.Email, before.NormalizedEmail, before.Version), (after.Email, after.NormalizedEmail, after.Version));
        Assert.Empty(world.Auth.Synced);
    }

    /// <summary>The comparison is on the normalized address: other letter case or surrounding spaces are the same address,
    /// so the edit goes through — and Auth is asked about the account under its one address.</summary>
    [Fact]
    public async Task The_same_address_in_other_letter_case_or_with_spaces_is_not_a_change()
    {
        await using var world = await World.StartAsync(_mongo);
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);

        var response = await world.UpdateAsync(target, actingAs: actor, r => r with { Email = $"  {target.Email.ToUpperInvariant()} ", DisplayName = "Same Address" });

        Assert.True(response.StatusCode == 204, $"{response.StatusCode}: {string.Join("; ", response.Errors)}");
        Assert.Equal((target.Email, "Same Address"), ((await world.ReadAsync(target.Id)).Email, (await world.ReadAsync(target.Id)).DisplayName));
        Assert.Equal(target.Email, Assert.Single(world.Auth.Synced).Email);
    }

    // ── FIX4 item 2 — a refused write converges Auth to the record as it is NOW ────────────────────────

    /// <summary>Auth took the change, then the record write was refused because someone else saved the record in between
    /// (with other roles). FIX3 put back the values THIS request had loaded (SupportAdmin) — over the other save. Now the
    /// record is read again and Auth gets what it holds now (BillingAdmin).</summary>
    [Fact]
    public async Task When_the_record_write_is_refused_auth_follows_the_record_as_it_now_is_not_as_it_was_loaded()
    {
        await using var world = await World.StartAsync(_mongo);
        var actor = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SuperAdmin]);
        var target = await world.SeedAsync(ActorType.PlatformAdmin, [AdministratorRole.SupportAdmin]);
        var concurrentSaveDone = false;
        world.Auth.OnSync = async _ =>
        {
            if (concurrentSaveDone) return;
            concurrentSaveDone = true;
            await world.SaveRolesAsync(target.Id, AdministratorRole.BillingAdmin); // another user's save while Auth was synced
        };

        var response = await world.UpdateAsync(target, actingAs: actor, r => r with { Roles = ["ReadOnly"] });

        Assert.Equal(409, response.StatusCode);
        Assert.Equal([AdministratorRole.BillingAdmin], (await world.ReadAsync(target.Id)).Roles);
        Assert.Equal(2, world.Auth.Synced.Count);
        Assert.Equal(("ReadOnly", "BillingAdmin"), (world.Auth.Synced[0].Roles.Single(), world.Auth.Synced[1].Roles.Single()));
        Assert.Equal(target.Email, world.Auth.Synced[1].Email);
    }

    // ── FIX3 item 2(c) — the system seed administrator ───────────────────────────────────────────────────

    [Theory]
    [InlineData("email")]
    [InlineData("suspend")]
    [InlineData("disable")]
    public async Task The_system_seed_administrators_email_and_status_do_not_change_through_update(string change)
    {
        await using var world = await World.StartAsync(_mongo);
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
        await using var world = await World.StartAsync(_mongo);
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

        public static async Task<World> StartAsync(DisposableMongoReplicaSet mongo)
        {
            var database = await ReplicaSetClassFixture.FreshDatabaseAsync(mongo, DatabaseName);
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

        /// <summary>Another user's save of the record: other roles, the next version.</summary>
        public Task SaveRolesAsync(Guid id, AdministratorRole role) =>
            _database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators)
                .UpdateOneAsync(x => x.Id == id, Builders<PlatformAdministrator>.Update.Set(x => x.Roles, [role]).Inc(x => x.Version, 1));

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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask; // the replica set is the class fixture's
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
