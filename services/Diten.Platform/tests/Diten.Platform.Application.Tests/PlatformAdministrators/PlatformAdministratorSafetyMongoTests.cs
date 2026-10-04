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

    // ── (b) the last active platform super administrator ─────────────────────────────────────────────────

    [Theory]
    [InlineData("toPartner")]
    [InlineData("suspend")]
    [InlineData("disable")]
    [InlineData("dropSuperAdmin")]
    public async Task The_last_active_platform_super_admin_cannot_be_demoted_or_suspended_through_update(string change)
    {
        await using var world = await World.StartAsync();
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
    }

    [Fact]
    public async Task The_last_active_platform_super_admin_cannot_be_suspended_deleted_bulk_deleted_or_stripped()
    {
        await using var world = await World.StartAsync();
        var (last, actor) = await world.LastSuperAdminWorldAsync();

        AssertLastSuperAdminRefusal(await world.SuspendAsync(last, actor));
        AssertLastSuperAdminRefusal(await world.DeleteAsync(last, actor));
        AssertLastSuperAdminRefusal(await world.BulkDeleteAsync(last, actor));
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

        public static async Task<World> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                         .Where(c => c.Name == PlatformCollections.PlatformAdministrators))
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

        public async Task<PlatformAdministrator> SeedAsync(ActorType type, IReadOnlyList<AdministratorRole> roles, AdministratorStatus status = AdministratorStatus.Active)
        {
            var email = $"safety.{Guid.NewGuid():N}@bl521.test";
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
            return await new UpdatePlatformAdministratorHandler(_repository, new NoProvisioning(), user, new ActorSafetyGuard(user, _repository))
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

        public async Task<Response<NoContent>> BulkDeleteAsync(PlatformAdministrator target, PlatformAdministrator actingAs)
        {
            var user = new Actor(actingAs.Email);
            return await new BulkDeletePlatformAdministratorsHandler(_repository, user, new ActorSafetyGuard(user, _repository))
                .Handle(new BulkDeletePlatformAdministratorsCommand([new PlatformAdministratorBulkDeleteItemRequest(target.Id, (await ReadAsync(target.Id)).Version)]), CancellationToken.None);
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

    private sealed class NoProvisioning : IPlatformAdministratorProvisioningService
    {
        public Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct) =>
            Task.FromResult(new PlatformAdministratorProvisioningResult(null, true));

        public Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct) => Task.CompletedTask;
    }
}
