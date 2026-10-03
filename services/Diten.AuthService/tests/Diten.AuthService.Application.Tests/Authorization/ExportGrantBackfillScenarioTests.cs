using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Tests.Roles;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// BL-452 / WP-ROLES-CLOSE-01 — the one-way export backfill as an AUTHORITY WRITER, scenario by scenario, for BOTH
/// export keys (<c>auth.users.export</c>, <c>auth.roles.export</c>): the production
/// <see cref="ExportGrantBackfillRunner"/> and the production <see cref="DataSeeder"/> on the test-owned mongod.
///
/// <para>OLD and BORN are what they are in production: the <c>CreatedAt</c> of the tenant's oldest role document
/// against the <c>CreatedAt</c> of the export permission. An old tenant's roles are therefore written with a
/// <c>CreatedAt</c> before the key's; a tenant opened after the feature goes through the production provisioning
/// service and gets "now".</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class ExportGrantBackfillScenarioTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public ExportGrantBackfillScenarioTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    public static TheoryData<string> Keys => new() { "users", "roles" };

    private static ExportGrantBackfill.KeyPair Pair(string key) => key == "users" ? ExportGrantBackfill.Users : ExportGrantBackfill.Roles;
    private static ExportGrantBackfill.KeyPair Other(ExportGrantBackfill.KeyPair pair) => pair == ExportGrantBackfill.Users ? ExportGrantBackfill.Roles : ExportGrantBackfill.Users;

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");
    private IMongoCollection<Role> RoleCol => _host.Database.GetCollection<Role>("roles");
    private IMongoCollection<RolePermission> Grants => _host.Database.GetCollection<RolePermission>("rolePermissions");
    private IMongoCollection<AuthAuditLog> Audit => _host.Database.GetCollection<AuthAuditLog>("authAuditLogs");
    private IMongoCollection<PermissionReconciliationMark> Marks => _host.Database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);

    private Task<ExportGrantBackfillRunner.Result> RunAsync(
        Func<ExportGrantBackfillRunner.Stage, ExportGrantBackfill.KeyPair, Guid, Task>? hook = null, ILogger? logger = null)
        => ExportGrantBackfillRunner.RunAsync(_host.Database, logger ?? NullLogger.Instance, hook);

    // ── the base case ────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task An_old_tenants_reading_roles_receive_export_once_a_role_without_read_does_not_and_a_second_start_writes_nothing(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var auditors = await OldRoleAsync(tenant, "Auditors", pair.ReadKey);
        var writers = await OldRoleAsync(tenant, "Writers", "auth.roles.create"); // no read → no export

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(auditors, pair));
        Assert.False(await HoldsAsync(writers, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, readers, auditors); // one row PER ROLE, not per tenant
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(tenant, pair)).Origin);
        var grants = await Grants.CountDocumentsAsync(g => g.TenantId == tenant);
        var audit = await Audit.CountDocumentsAsync(a => a.TenantId == tenant);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.Equal(grants, await Grants.CountDocumentsAsync(g => g.TenantId == tenant));
        Assert.Equal(audit, await Audit.CountDocumentsAsync(a => a.TenantId == tenant));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task The_audit_row_of_a_backfilled_grant_names_the_role_the_key_and_the_system_actor(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        var row = Assert.Single(await AuditRowsAsync(tenant, pair.AuditSource));
        Assert.Equal(ExportGrantBackfill.AuditEventName, row.EventName);
        Assert.Equal(Guid.Empty, row.UserId); // no person
        using var metadata = JsonDocument.Parse(row.Metadata);
        Assert.Equal(readers, metadata.RootElement.GetProperty("roleId").GetGuid());
        Assert.Equal("Readers", metadata.RootElement.GetProperty("roleName").GetString());
        Assert.Equal(pair.ExportKey, metadata.RootElement.GetProperty("permissionKey").GetString());
        Assert.Equal("system", metadata.RootElement.GetProperty("actor").GetString());
        Assert.Equal(pair.AuditSource, metadata.RootElement.GetProperty("source").GetString());
    }

    // ── FIX2 item 1: old or born is read off a stored fact ────────────────────────────────────────────────

    // (a) The state the build on main leaves for a tenant opened after its last restart.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_tenant_whose_roles_are_newer_than_the_key_is_born_no_grant_is_written_whoever_already_exports(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var admin = await RoleAsync(tenant, "Admin", old: false, system: true, GrantSource.System, pair.ReadKey, pair.ExportKey);
        var viewer = await RoleAsync(tenant, "Viewer", old: false, system: true, GrantSource.System, pair.ReadKey);
        var readers = await RoleAsync(tenant, "Readers", old: false, system: false, GrantSource.Manual, pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(viewer, pair));
        Assert.False(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(admin, pair));
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, pair)).Origin);
        Assert.Empty(await AuditRowsAsync(tenant, pair.AuditSource));
    }

    // (d) A role document whose CreatedAt is missing / default cannot date the tenant: no grant.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_tenant_whose_role_carries_no_CreatedAt_is_treated_as_born(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await RoleAtAsync(tenant, "Readers", default, system: false, GrantSource.Manual, pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, pair)).Origin);
    }

    // The entity's CreatedAt has an initializer ("now"): a document that does not STORE the field would read as the
    // moment it was loaded. For the export key that would make every tenant look older than the key — so the key's age
    // counts only when it is stored. Without it: nothing is granted.
    [Theory, MemberData(nameof(Keys))]
    public async Task When_the_export_keys_catalog_row_stores_no_CreatedAt_nothing_is_granted(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var raw = _host.Database.GetCollection<BsonDocument>("permissions");
        var stored = (await raw.Find(new BsonDocument("Key", pair.ExportKey)).SingleAsync())["CreatedAt"];
        await raw.UpdateOneAsync(new BsonDocument("Key", pair.ExportKey), new BsonDocument("$unset", new BsonDocument("CreatedAt", "")));
        try
        {
            await RunAsync();
        }
        finally
        {
            await raw.UpdateOneAsync(new BsonDocument("Key", pair.ExportKey), new BsonDocument("$set", new BsonDocument("CreatedAt", stored)));
        }

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, pair)).Origin);
    }

    // The same for a role document: one that stores no CreatedAt cannot date its tenant.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_role_document_that_stores_no_CreatedAt_does_not_make_its_tenant_old(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        await _host.Database.GetCollection<BsonDocument>("roles").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(readers, GuidRepresentation.Standard)),
            new BsonDocument("$unset", new BsonDocument("CreatedAt", "")));
        var unset = await _host.Database.GetCollection<Role>("roles").CountDocumentsAsync(
            Builders<Role>.Filter.Eq(r => r.Id, readers) & Builders<Role>.Filter.Exists(r => r.CreatedAt, false));
        Assert.True(unset == 1, "the fixture did not remove CreatedAt from the role document");

        await RunAsync();

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, pair)).Origin);
    }

    // Each key decides for itself: old for one, born for the other. The two keys are seeded milliseconds apart, so the
    // roles key's catalog date is moved a day later for the duration of the test (restored after).
    [Fact]
    public async Task A_tenant_older_than_one_key_and_newer_than_the_other_is_backfilled_for_the_first_only()
    {
        var raw = _host.Database.GetCollection<BsonDocument>("permissions");
        var stored = (await raw.Find(new BsonDocument("Key", ExportGrantBackfill.Roles.ExportKey)).SingleAsync())["CreatedAt"];
        var usersAt = await KeyCreatedAtAsync(ExportGrantBackfill.Users);
        await Permissions.UpdateOneAsync(p => p.Key == ExportGrantBackfill.Roles.ExportKey, Builders<Permission>.Update.Set(p => p.CreatedAt, usersAt.AddDays(1)));
        try
        {
            var tenant = Guid.NewGuid();
            var between = usersAt.AddHours(12); // after the users key, a day minus 12 h before the roles key
            var readers = await RoleAtAsync(tenant, "Readers", between, system: false, GrantSource.Manual, ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);

            await RunAsync();

            Assert.False(await HoldsAsync(readers, ExportGrantBackfill.Users)); // the tenant came after this key → born
            Assert.True(await HoldsAsync(readers, ExportGrantBackfill.Roles));  // … and before this one → old
            Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, ExportGrantBackfill.Users)).Origin);
            Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(tenant, ExportGrantBackfill.Roles)).Origin);
        }
        finally
        {
            await raw.UpdateOneAsync(new BsonDocument("Key", ExportGrantBackfill.Roles.ExportKey), new BsonDocument("$set", new BsonDocument("CreatedAt", stored)));
        }
    }

    // ── FIX3 item 1: the ROLE must predate the key too ───────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task In_an_unmarked_old_tenant_only_roles_that_predate_the_key_receive_it(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var oldReaders = await OldRoleAsync(tenant, "OldReaders", pair.ReadKey);
        // Opened by the administrator AFTER the key existed, with read and on purpose without export. Its read grant's
        // date is left unknown, so that only the ROLE's own age can keep it out (the read-grant rule is tested apart).
        var keyAt = await KeyCreatedAtAsync(pair);
        var newReaders = await RoleAtAsync(tenant, "NewReaders", keyAt.AddDays(1), system: false, GrantSource.Manual);
        await GrantAsync(newReaders, tenant, GrantSource.Manual, pair.ReadKey, default(DateTimeOffset));

        await RunAsync();

        Assert.True(await HoldsAsync(oldReaders, pair));
        Assert.False(await HoldsAsync(newReaders, pair));
        Assert.DoesNotContain(await AuditRowsAsync(tenant, pair.AuditSource), r => r.Metadata.Contains(newReaders.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task An_old_role_whose_read_grant_was_given_after_the_key_does_not_receive_it(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var keyAt = await KeyCreatedAtAsync(pair);
        var anchor = await OldRoleAsync(tenant, "Anchor", "auth.roles.create"); // dates the tenant as old
        var lateRead = await RoleAtAsync(tenant, "LateRead", keyAt.AddDays(-30), system: false, GrantSource.Manual);
        await GrantAsync(lateRead, tenant, GrantSource.Manual, pair.ReadKey, keyAt.AddDays(1)); // read given after the key

        await RunAsync();

        Assert.False(await HoldsAsync(lateRead, pair));
        Assert.False(await HoldsAsync(anchor, pair));
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(tenant, pair)).Origin);
    }

    // ── FIX3 item 2: the clock-skew allowance, both sides of the boundary ────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task A_tenant_set_up_inside_the_clock_skew_allowance_is_born_and_one_just_outside_it_is_old(string key)
    {
        var pair = Pair(key);
        var keyAt = await KeyCreatedAtAsync(pair);
        var inside = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var insideReaders = await RoleAtAsync(inside, "Readers", keyAt - ExportGrantBackfill.ClockSkewAllowance + TimeSpan.FromSeconds(1), system: false, GrantSource.Manual, pair.ReadKey);
        var outsideReaders = await RoleAtAsync(outside, "Readers", keyAt - ExportGrantBackfill.ClockSkewAllowance - TimeSpan.FromSeconds(1), system: false, GrantSource.Manual, pair.ReadKey);

        await RunAsync();

        Assert.False(await HoldsAsync(insideReaders, pair));
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(inside, pair)).Origin);
        Assert.True(await HoldsAsync(outsideReaders, pair));
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(outside, pair)).Origin);
    }

    // ── FIX3 item 3: one broken document fails its own tenant only ───────────────────────────────────────

    [Fact]
    public async Task A_role_document_that_does_not_deserialize_fails_only_its_tenant_for_both_keys()
    {
        var broken = Guid.NewGuid();
        var healthy = Guid.NewGuid();
        var brokenReaders = await OldRoleAsync(broken, "Readers", ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);
        var healthyReaders = await OldRoleAsync(healthy, "Readers", ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);
        var badId = Guid.NewGuid();
        var rawRoles = _host.Database.GetCollection<BsonDocument>("roles");
        await rawRoles.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(badId, GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(broken, GuidRepresentation.Standard) },
            { "Name", "SECRET-NAME-in-the-document" },
            // A date that is not one: the serializer's exception quotes it — neither the document nor the exception's
            // message may reach the log.
            { "CreatedAt", "SECRET-CONTENT-not-a-date" },
            { "IsDeleted", false }
        });
        var logs = new CapturingLoggerProvider();
        ExportGrantBackfillRunner.Result result;
        try
        {
            result = await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await rawRoles.DeleteOneAsync(new BsonDocument("_id", new BsonBinaryData(badId, GuidRepresentation.Standard)));
        }

        Assert.Contains(broken, result.FailedTenants);
        Assert.DoesNotContain(healthy, result.FailedTenants);
        foreach (var pair in ExportGrantBackfill.Keys)
        {
            Assert.False(await HoldsAsync(brokenReaders, pair));
            Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == broken && m.Key == pair.ExportKey)); // retried next start
            Assert.True(await HoldsAsync(healthyReaders, pair)); // both keys went on for everybody else
        }

        var entry = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(badId.ToString()));
        Assert.Contains(broken.ToString(), entry.Message);
        Assert.Contains("Exception", entry.Message);
        Assert.DoesNotContain("SECRET", entry.Message);
        Assert.All(logs.Entries, e => Assert.DoesNotContain("SECRET", e.Message));

        await RunAsync(); // the document is gone: the next start settles the tenant
        foreach (var pair in ExportGrantBackfill.Keys) Assert.True(await HoldsAsync(brokenReaders, pair));
    }

    // ── FIX3 item 5: an audit row the run KNOWS did not take effect is withdrawn ─────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task When_the_grant_insert_fails_after_its_audit_row_a_not_applied_row_withdraws_it_once(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();
        // The grants collection refuses this tenant's export grant (its read grant is already in).
        var refuse = Builders<RolePermission>.Filter.Not(
                Builders<RolePermission>.Filter.Eq(g => g.TenantId, tenant) & Builders<RolePermission>.Filter.Eq(g => g.PermissionId, export.Id))
            .Render(Grants.DocumentSerializer, Grants.Settings.SerializerRegistry);
        await _host.Database.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", "rolePermissions" }, { "validator", refuse } });
        ExportGrantBackfillRunner.Result first;
        try
        {
            first = await RunAsync();
        }
        finally
        {
            await _host.Database.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", "rolePermissions" }, { "validator", new BsonDocument() } });
        }

        Assert.Contains(tenant, first.FailedTenants);
        Assert.False(await HoldsAsync(readers, pair));
        var granted = Assert.Single(await AuditRowsAsync(tenant, pair.AuditSource), r => r.EventName == ExportGrantBackfill.AuditEventName);
        var withdrawn = Assert.Single(await AuditRowsAsync(tenant, pair.AuditSource), r => r.EventName == ExportGrantBackfill.GrantNotAppliedEventName);
        Assert.Contains(granted.Id.ToString(), withdrawn.Metadata, StringComparison.OrdinalIgnoreCase); // it names what it withdraws
        Assert.Equal(ExportGrantBackfill.NotAppliedAuditId(tenant, readers, pair.ExportKey), withdrawn.Id); // THE row: one id however often
        using (var meta = JsonDocument.Parse(withdrawn.Metadata))
        {
            // The reason is the exception's TYPE — the server's message quotes the document it refused.
            Assert.Equal(typeof(MongoWriteException).FullName, meta.RootElement.GetProperty("reason").GetString());
        }

        await AgeAuditRowsAsync(tenant);
        var second = await RunAsync(); // the refusal is gone; option B: the decided role is not granted again

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Contains((tenant, readers, pair.ExportKey), second.GrantsNotRepeated);
        Assert.Single(await AuditRowsAsync(tenant, pair.AuditSource), r => r.EventName == ExportGrantBackfill.GrantNotAppliedEventName); // not twice
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // A correction that did not happen (the grant was revoked between the read and the update) leaves no row.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_source_correction_that_changes_nothing_writes_no_audit_row(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var viewer = await RoleAsync(tenant, "Viewer", old: true, system: true, GrantSource.System, pair.ReadKey, pair.ExportKey);
        var readers = await RoleAsync(tenant, "Readers", old: true, system: false, GrantSource.System, pair.ReadKey, pair.ExportKey);
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = pair.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = null });
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();

        await RunAsync(async (stage, p, roleId) =>
        {
            // An administrator revokes the custom role's grant between the correction's read and its update.
            if (stage == ExportGrantBackfillRunner.Stage.SourceCorrectionRead && p == pair && roleId == readers)
                await Grants.DeleteOneAsync(g => g.RoleId == readers && g.PermissionId == export.Id);
        });

        var rows = await AuditRowsAsync(tenant, pair.CorrectionAuditSource);
        Assert.Single(rows, r => r.Metadata.Contains(viewer.ToString(), StringComparison.OrdinalIgnoreCase)); // a change that happened
        Assert.DoesNotContain(rows, r => r.Metadata.Contains(readers.ToString(), StringComparison.OrdinalIgnoreCase)); // none that did not
        Assert.False(await HoldsAsync(readers, pair));
    }

    // ── FIX3: rules that had no test ─────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task A_soft_deleted_export_key_is_not_in_the_catalog_nothing_is_granted_or_marked(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        await Permissions.UpdateOneAsync(p => p.Key == pair.ExportKey, Builders<Permission>.Update.Set(p => p.IsDeleted, true));
        try
        {
            await RunAsync();
        }
        finally
        {
            await Permissions.UpdateOneAsync(p => p.Key == pair.ExportKey, Builders<Permission>.Update.Set(p => p.IsDeleted, false));
        }

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task A_missing_read_key_skips_its_pair_without_failing_the_run_and_the_other_pair_goes_on(string key)
    {
        var pair = Pair(key);
        var other = Other(pair);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey, other.ReadKey);
        await Permissions.UpdateOneAsync(p => p.Key == pair.ReadKey, Builders<Permission>.Update.Set(p => p.IsDeleted, true));
        ExportGrantBackfillRunner.Result result;
        try
        {
            result = await RunAsync();
        }
        finally
        {
            await Permissions.UpdateOneAsync(p => p.Key == pair.ReadKey, Builders<Permission>.Update.Set(p => p.IsDeleted, false));
        }

        Assert.DoesNotContain(tenant, result.FailedTenants);
        Assert.False(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(readers, other));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task The_backfill_still_runs_when_an_earlier_seed_step_fails(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        var stepFailed = false;
        await DataSeeder.SeedAsync(_host.Database, false, null, () =>
        {
            stepFailed = true;
            throw new InvalidOperationException("a seed step failed");
        });

        Assert.True(stepFailed, "the seed steps did not run, so nothing failed before the backfill");
        Assert.True(await HoldsAsync(readers, pair));
    }

    [Fact]
    public async Task A_role_without_a_tenant_does_not_make_the_run_read_role_documents()
    {
        await RunAsync();
        var orphan = await OldRoleAsync(Guid.Empty, "Orphans-" + Guid.NewGuid().ToString("N")[..8], ExportGrantBackfill.Users.ReadKey);
        try
        {
            var commands = new ConcurrentQueue<(string Name, string Collection)>();
            var settings = _host.Database.Client.Settings.Clone();
            settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(e =>
            {
                if (e.Command.TryGetValue(e.CommandName, out var target) && target.IsString) commands.Enqueue((e.CommandName, target.AsString));
            });
            var observed = new MongoClient(settings).GetDatabase(_host.Database.DatabaseNamespace.DatabaseName);

            await ExportGrantBackfillRunner.RunAsync(observed, NullLogger.Instance);

            Assert.DoesNotContain(commands, c => c.Name == "find" && c.Collection == "roles");
            Assert.DoesNotContain(commands, c => c.Collection == "rolePermissions");
        }
        finally
        {
            await Grants.DeleteManyAsync(g => g.RoleId == orphan);
            await RoleCol.DeleteOneAsync(r => r.Id == orphan);
        }
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task Corrections_are_counted_also_when_no_tenant_is_left_to_backfill(string key)
    {
        var pair = Pair(key);
        var other = Other(pair);
        var tenant = Guid.NewGuid();
        await RoleAsync(tenant, "Readers", old: true, system: false, GrantSource.System, pair.ReadKey, pair.ExportKey);
        await RunAsync(); // settle everything else first
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = pair.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = null });
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = other.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = ExportGrantBackfill.OriginBackfilled });

        var result = await RunAsync(); // every tenant is settled: the run returns early — with the correction counted

        Assert.Equal(1, result.SourcesCorrected);
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task Cancellation_stops_the_run_and_is_not_counted_as_a_tenant_failure(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        using var cts = new CancellationTokenSource();
        var logs = new CapturingLoggerProvider();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExportGrantBackfillRunner.RunAsync(_host.Database, logs.CreateLogger<ExportGrantBackfillScenarioTests>(),
            (stage, p, id) =>
            {
                if (p != pair || stage != ExportGrantBackfillRunner.Stage.TenantGrantsWritten || id != tenant) return Task.CompletedTask;
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }, cts.Token));

        Assert.DoesNotContain(logs.Entries, e => e.Level >= LogLevel.Error); // not logged as a tenant failure
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    [Fact]
    public async Task The_seeders_summary_line_is_Information()
    {
        var tenant = Guid.NewGuid();
        await OldRoleAsync(tenant, "Readers", ExportGrantBackfill.Users.ReadKey);
        var logs = new CapturingLoggerProvider();

        await DataSeeder.SeedAsync(_host.Database, false, logs.CreateLogger<ExportGrantBackfillScenarioTests>());

        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Information && e.Message.StartsWith("Export backfill:", StringComparison.Ordinal));
    }

    // FIX2 item 7 — ONE definition of a tenant's age: its oldest role DOCUMENT, deleted or not.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_deleted_old_role_makes_the_tenant_old_but_receives_nothing_itself(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var gone = await RoleAsync(tenant, "Gone", old: true, system: false, GrantSource.Manual, pair.ReadKey);
        await RoleCol.UpdateOneAsync(r => r.Id == gone, Builders<Role>.Update.Set(r => r.IsDeleted, true));
        var readers = await RoleAsync(tenant, "Readers", old: false, system: false, GrantSource.Manual, pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        // The deleted old role dates the tenant: it is OLD (marked backfilled, not born) …
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(tenant, pair)).Origin);
        Assert.False(await HoldsAsync(gone, pair));    // … a deleted role is not granted anything …
        Assert.False(await HoldsAsync(readers, pair)); // … and a role opened after the key is no candidate (FIX3 item 1).
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task A_tenant_with_only_deleted_roles_is_marked_so_that_a_later_role_does_not_make_it_old(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var gone = await RoleAsync(tenant, "Gone", old: true, system: false, GrantSource.Manual, pair.ReadKey);
        await RoleCol.UpdateOneAsync(r => r.Id == gone, Builders<Role>.Update.Set(r => r.IsDeleted, true));

        await DataSeeder.SeedAsync(_host.Database);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));

        // A reading role opened later, on purpose without export, stays without it.
        var later = await RoleAsync(tenant, "Readers", old: false, system: false, GrantSource.Manual, pair.ReadKey);
        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(later, pair));
    }

    // A grant that was taken away (soft-deleted document) is not "held": the role is not a reader.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_role_whose_read_grant_is_deleted_is_not_a_reader(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var former = await OldRoleAsync(tenant, "FormerReaders", pair.ReadKey);
        var read = await Permissions.Find(p => p.Key == pair.ReadKey).SingleAsync();
        await Grants.UpdateOneAsync(g => g.RoleId == former && g.PermissionId == read.Id, Builders<RolePermission>.Update.Set(g => g.IsDeleted, true));

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(former, pair));
        Assert.Empty(await AuditRowsAsync(tenant, pair.AuditSource));
    }

    // ── marks ────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task A_mark_for_one_key_does_not_settle_the_tenant_for_the_other(string key)
    {
        var pair = Pair(key);
        var other = Other(pair);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey, other.ReadKey);
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = other.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = ExportGrantBackfill.OriginBackfilled });

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(readers, pair));   // this key was still open
        Assert.False(await HoldsAsync(readers, other)); // that key was settled — whatever the role holds
    }

    // A mark written by an earlier build (no Origin) is a valid mark for ITS key: the tenant is not processed again.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_mark_without_an_Origin_still_settles_the_tenant_for_its_own_key(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey); // export was taken away after the earlier backfill
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = pair.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = null });

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Empty(await AuditRowsAsync(tenant, pair.AuditSource));
    }

    // ── S-A (FIX2 item 2, CT decision: option B) ─────────────────────────────────────────────────────────
    //
    // The audit row is the decision record: whoever writes it, and only they, writes the grant. No transaction is
    // assumed. What a run that stops half-way leaves, by WHERE it stopped — three roles, it stops at the second:
    //   • AuditWritten        role 2 has its audit row and no grant → the next start does NOT repeat it (it cannot tell
    //                         this from an administrator's revoke) and says so; role 3 is still granted.
    //   • GrantWritten        role 2 is complete → the next start grants role 3.
    //   • TenantGrantsWritten every role is complete → the next start only marks.
    // In every case: no double audit row, the tenant ends up marked, and a second later start changes nothing.

    [Theory, MemberData(nameof(Keys))]
    public async Task S_A_Stopped_between_a_roles_audit_row_and_its_grant_the_next_start_does_not_repeat_that_grant_warns_and_finishes_the_rest(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = await ThreeOldReadersAsync(tenant, pair);
        var (_, second) = await StopAtSecondRoleAsync(pair, tenant, roles, ExportGrantBackfillRunner.Stage.AuditWritten);
        var logs = new CapturingLoggerProvider();
        await AgeAuditRowsAsync(tenant); // the next start comes later than a slow run could still be on its way

        var next = await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>()); // the next start

        Assert.False(await HoldsAsync(second, pair)); // closed: not repeated
        foreach (var role in roles.Where(r => r != second)) Assert.True(await HoldsAsync(role, pair));
        Assert.Equal([(tenant, second, pair.ExportKey)], next.GrantsNotRepeated.Where(x => x.TenantId == tenant));
        // Visible: one Warning naming the tenant, the role and the key — ids only.
        var warning = Assert.Single(logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(second.ToString()));
        Assert.Contains(tenant.ToString(), warning.Message);
        Assert.Contains(pair.ExportKey, warning.Message);
        Assert.DoesNotContain("Readers-", warning.Message); // not the role's name
        // Three audit rows — one per decision ever taken — and no second row for the role that was not repeated.
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));

        await AssertALaterStartChangesNothingAsync(tenant);
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task S_A_Stopped_after_a_roles_grant_the_next_start_grants_the_remaining_roles(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = await ThreeOldReadersAsync(tenant, pair);
        await StopAtSecondRoleAsync(pair, tenant, roles, ExportGrantBackfillRunner.Stage.GrantWritten);

        var next = await RunAsync();

        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        Assert.DoesNotContain(next.GrantsNotRepeated, x => x.TenantId == tenant);
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
        await AssertALaterStartChangesNothingAsync(tenant);
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task S_A_Stopped_after_every_grant_and_before_the_mark_the_next_start_touches_no_grant_and_marks(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = await ThreeOldReadersAsync(tenant, pair);
        await StopAtSecondRoleAsync(pair, tenant, roles, ExportGrantBackfillRunner.Stage.TenantGrantsWritten);
        var grantsBefore = await Grants.CountDocumentsAsync(g => g.TenantId == tenant);
        var versions = new RoleAssignmentVersionRepository(_host.Database);
        var versionBefore = await versions.GetAsync(tenant, CancellationToken.None);

        var next = await RunAsync();

        Assert.Equal(0, next.GrantsWritten);
        // The interrupted run wrote the grants and never reached the version bump: this start bumps it, with no grant of its own.
        Assert.True(await versions.GetAsync(tenant, CancellationToken.None) > versionBefore, "the tenant's cached authorization was not invalidated");
        Assert.Equal(grantsBefore, await Grants.CountDocumentsAsync(g => g.TenantId == tenant));
        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // × "an administrator took the grant away in between": at every stop point, what was revoked stays revoked.
    [Theory]
    [InlineData("users", ExportGrantBackfillRunner.Stage.AuditWritten)]
    [InlineData("users", ExportGrantBackfillRunner.Stage.GrantWritten)]
    [InlineData("users", ExportGrantBackfillRunner.Stage.TenantGrantsWritten)]
    [InlineData("roles", ExportGrantBackfillRunner.Stage.AuditWritten)]
    [InlineData("roles", ExportGrantBackfillRunner.Stage.GrantWritten)]
    [InlineData("roles", ExportGrantBackfillRunner.Stage.TenantGrantsWritten)]
    public async Task S_A_A_grant_an_administrator_revoked_after_an_interrupted_run_is_never_handed_back(
        string key, ExportGrantBackfillRunner.Stage stopAt)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = await ThreeOldReadersAsync(tenant, pair);
        var (first, _) = await StopAtSecondRoleAsync(pair, tenant, roles, stopAt);
        Assert.True(await HoldsAsync(first, pair)); // written by the interrupted run; the tenant is NOT marked yet
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));

        // The administrator takes it away: the product's own repository call (a hard delete), the one RevokePermissionCommand makes.
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();
        using (var scope = _host.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<Diten.AuthService.Application.Common.TenantContext>().SetTenant(tenant);
            await scope.ServiceProvider.GetRequiredService<Diten.AuthService.Application.Common.Interfaces.IRolePermissionRepository>()
                .RevokeAsync(first, export.Id, tenant, CancellationToken.None);
        }

        await AgeAuditRowsAsync(tenant);
        var next = await RunAsync();
        await DataSeeder.SeedAsync(_host.Database); // and the start after that

        Assert.False(await HoldsAsync(first, pair)); // NOT handed back
        Assert.Contains((tenant, first, pair.ExportKey), next.GrantsNotRepeated);
        Assert.Single((await AuditRowsAsync(tenant, pair.AuditSource)), r => r.Metadata.Contains(first.ToString(), StringComparison.OrdinalIgnoreCase)); // records stay consistent: one row, no second "granted"
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // Two instances, and the one that owns a role's decision dies before writing its grant: the other one does not
    // write it either. Closed — and visible.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_A_When_the_instance_that_wrote_a_roles_audit_row_dies_the_other_instance_does_not_grant_it(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = new[] { await OldRoleAsync(tenant, "Readers-1", pair.ReadKey), await OldRoleAsync(tenant, "Readers-2", pair.ReadKey) };
        Guid? owned = null;
        ExportGrantBackfillRunner.Result? other = null;

        var dying = await RunAsync(async (stage, p, id) =>
        {
            if (owned is not null || p != pair || stage != ExportGrantBackfillRunner.Stage.AuditWritten || !roles.Contains(id)) return;
            owned = id;
            other = await RunAsync(); // the second instance runs from start to finish …
            throw new InvalidOperationException("… and the first one dies before its grant");
        });

        Assert.Contains(tenant, dying.FailedTenants);
        Assert.NotNull(owned);
        Assert.False(await HoldsAsync(owned!.Value, pair));                              // nobody granted it
        Assert.True(await HoldsAsync(roles.Single(r => r != owned), pair));              // the other role is complete
        // FIX5 — the survivor met a row written moments ago: it could not tell a slow writer from a dead one, so it said
        // nothing and left the tenant unmarked …
        Assert.DoesNotContain(other!.GrantsNotRepeated, x => x.TenantId == tenant);
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));

        await AgeAuditRowsAsync(tenant);
        var later = await RunAsync(); // … and a later start, when the writer cannot still be on its way, says so and settles it
        Assert.Contains((tenant, owned.Value, pair.ExportKey), later.GrantsNotRepeated);
        Assert.False(await HoldsAsync(owned.Value, pair));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        await AssertALaterStartChangesNothingAsync(tenant);
    }

    // ── S-B ──────────────────────────────────────────────────────────────────────────────────────────────

    // What a first start that failed in an EARLIER seed step leaves behind in an OLD tenant: the template already gave
    // the system Admin the new key, the backfill never ran. (The failing step itself cannot be injected into the
    // seeder; its outcome is. The backfill now also runs in its own step, after a failed seed step.)
    [Theory, MemberData(nameof(Keys))]
    public async Task S_B_After_a_first_start_that_failed_before_the_backfill_the_old_tenant_is_still_processed_in_full(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var admin = await RoleAsync(tenant, "Admin", old: true, system: true, GrantSource.System, pair.ReadKey, pair.ExportKey);
        var viewer = await RoleAsync(tenant, "Viewer", old: true, system: true, GrantSource.System, pair.ReadKey);
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(viewer, pair));
        Assert.True(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(admin, pair)); // still exactly one grant (HoldsAsync counts == 1)
        await AssertOneAuditRowPerGrantAsync(tenant, pair, viewer, readers);
    }

    // ── S-C ──────────────────────────────────────────────────────────────────────────────────────────────

    // The host started on an empty database: the production seeder created the keys, then the default tenant's roles.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_C_On_a_brand_new_database_the_default_tenants_Viewer_reads_but_does_not_export(string key)
    {
        var pair = Pair(key);
        var viewer = await RoleCol.Find(r => r.TenantId == DefaultTenantId && r.Name == DefaultRolePermissionTemplate.ViewerRole).SingleAsync();
        var admin = await RoleCol.Find(r => r.TenantId == DefaultTenantId && r.Name == DefaultRolePermissionTemplate.AdminRole).SingleAsync();

        await DataSeeder.SeedAsync(_host.Database); // and however many starts follow

        Assert.True(await HoldsKeyAsync(viewer.Id, pair.ReadKey));
        Assert.False(await HoldsAsync(viewer.Id, pair));
        Assert.True(await HoldsAsync(admin.Id, pair)); // the template gives it to Admin
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(DefaultTenantId, pair)).Origin);
        Assert.Empty(await AuditRowsAsync(DefaultTenantId, pair.AuditSource));
    }

    // ── S-D ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task S_D_A_tenant_provisioned_after_the_feature_never_receives_the_backfill(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        Guid custom;
        using (var scope = _host.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<Diten.AuthService.Application.Common.TenantContext>().SetTenant(tenant);
            // The production path a new tenant takes (Admin + Viewer from the template) …
            await scope.ServiceProvider.GetRequiredService<Diten.AuthService.Application.Common.Interfaces.IRoleProvisioningService>().EnsureDefaultRolesAsync(tenant);
            // … and a reading role its administrator then opens ON PURPOSE without export.
            var role = await scope.ServiceProvider.GetRequiredService<Diten.AuthService.Application.Common.Interfaces.IRoleRepository>()
                .CreateAsync(new Role("Readers", "Readers", "opened without export", tenant), CancellationToken.None);
            custom = role.Id;
            await GrantAsync(custom, tenant, GrantSource.Manual, pair.ReadKey);
        }

        var viewer = await RoleCol.Find(r => r.TenantId == tenant && r.Name == DefaultRolePermissionTemplate.ViewerRole).SingleAsync();

        await DataSeeder.SeedAsync(_host.Database);
        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsKeyAsync(viewer.Id, pair.ReadKey));
        Assert.False(await HoldsAsync(viewer.Id, pair));
        Assert.False(await HoldsAsync(custom, pair));
        Assert.Empty(await AuditRowsAsync(tenant, pair.AuditSource));
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, pair)).Origin);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // ── S-E ──────────────────────────────────────────────────────────────────────────────────────────────

    // Platform deployed first: its catalog sync created the key in the old AuthService and the entitlement sync handed
    // it to Admin as a MODULE grant — before this AuthService ever ran its backfill. The tenant predates the key.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_E_In_an_old_tenant_an_Admin_that_already_holds_the_key_as_a_module_grant_does_not_make_the_tenant_done(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var admin = await RoleAsync(tenant, "Admin", old: true, system: true, GrantSource.Module, pair.ReadKey, pair.ExportKey);
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var auditors = await OldRoleAsync(tenant, "Auditors", pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(auditors, pair));
        Assert.True(await HoldsAsync(admin, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, readers, auditors);
    }

    // ── S-F ──────────────────────────────────────────────────────────────────────────────────────────────

    // Deterministic interleaving: instance A has planned and written the first audit row when instance B runs from
    // start to finish. A then meets B's audit rows, grants and mark as duplicate keys — and must not fail the tenant.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_F_Two_instances_starting_together_leave_one_grant_one_audit_row_and_one_mark_and_neither_fails(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = new[] { await OldRoleAsync(tenant, "Readers-1", pair.ReadKey), await OldRoleAsync(tenant, "Readers-2", pair.ReadKey) };
        var otherInstanceRan = false;
        var bLogs = new CapturingLoggerProvider();

        var a = await RunAsync(async (stage, p, id) =>
        {
            if (otherInstanceRan || p != pair || stage != ExportGrantBackfillRunner.Stage.AuditWritten || !roles.Contains(id)) return;
            otherInstanceRan = true;
            var b = await RunAsync(logger: bLogs.CreateLogger<ExportGrantBackfillScenarioTests>());
            Assert.DoesNotContain(tenant, b.FailedTenants);
            // FIX5 — B met A's row while A was still on its way to the grant: B cannot verify it, so it warns nobody,
            // reports nothing and does NOT mark the tenant (A, or the next start, does).
            Assert.DoesNotContain(b.GrantsNotRepeated, x => x.TenantId == tenant);
            Assert.DoesNotContain(bLogs.Entries, e => e.Level >= LogLevel.Warning && e.Message.Contains(tenant.ToString()));
            Assert.Contains(bLogs.Entries, e => e.Level == LogLevel.Information && e.Message.Contains(tenant.ToString()) && e.Message.Contains("could not be verified"));
            Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
        });

        Assert.True(otherInstanceRan);
        Assert.DoesNotContain(tenant, a.FailedTenants); // a duplicate key is "already done", not a failure
        // A met B's audit rows — and B's grants with them: nothing is missing, so nothing is reported (FIX3 item 4).
        Assert.DoesNotContain(a.GrantsNotRepeated, x => x.TenantId == tenant);
        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task S_F_Four_instances_racing_for_real_leave_the_same_single_result(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = new[] { await OldRoleAsync(tenant, "Readers-1", pair.ReadKey), await OldRoleAsync(tenant, "Readers-2", pair.ReadKey) };

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => RunAsync())));

        Assert.All(results, r => Assert.DoesNotContain(tenant, r.FailedTenants));
        Assert.All(results, r => Assert.DoesNotContain(r.GrantsNotRepeated, x => x.TenantId == tenant));
        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // ── S-G ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task S_G_Export_backfilled_onto_a_custom_role_can_be_taken_away_on_the_products_own_path_and_stays_away(string key)
    {
        var pair = Pair(key);
        var world = RoleEndpointWorld.Create(_host, "Rana", "Steward"); // a person of the old tenant who administers roles
        var tenant = world.TenantId;
        var admin = await RoleAsync(tenant, "Admin", old: true, system: true, GrantSource.System, pair.ReadKey);
        var viewer = await RoleAsync(tenant, "Viewer", old: true, system: true, GrantSource.System, pair.ReadKey);
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        await DataSeeder.SeedAsync(_host.Database);
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();

        // Admin: the template gives it export → template-managed, locked on the screen like the rest of its baseline.
        // Viewer and the custom role: nothing re-provisions the grant → it is the administrator's to take away.
        Assert.Equal(GrantSource.System, (await GrantRowAsync(admin, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(viewer, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(readers, export.Id)).GrantSource);

        using var client = world.Client();
        // The real DELETE api/roles/{id}/permissions/{permissionId} → RevokePermissionCommand.
        var revoked = await client.DeleteAsync($"api/roles/{readers}/permissions/{export.Id}");
        Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync());
        Assert.False(await HoldsAsync(readers, pair));

        var locked = await client.DeleteAsync($"api/roles/{admin}/permissions/{export.Id}");
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains(RoleErrorCodes.PermissionGrantManaged, await locked.Content.ReadAsStringAsync());

        await DataSeeder.SeedAsync(_host.Database); // a restart
        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(readers, pair)); // not handed back
        Assert.True(await HoldsAsync(viewer, pair));
        Assert.True(await HoldsAsync(admin, pair));
    }

    // ── S-H ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task S_H_A_role_without_a_tenant_is_not_processed_and_the_empty_tenant_is_never_marked(string key)
    {
        var pair = Pair(key);
        var orphan = await OldRoleAsync(Guid.Empty, "Orphans-" + Guid.NewGuid().ToString("N")[..8], pair.ReadKey);
        try
        {
            await DataSeeder.SeedAsync(_host.Database);

            Assert.False(await HoldsAsync(orphan, pair));
            Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == Guid.Empty));
            Assert.Empty(await AuditRowsAsync(Guid.Empty, pair.AuditSource));
        }
        finally
        {
            // The fixture is test-owned; it must not sit in the shared test database as a tenant-less role.
            await Grants.DeleteManyAsync(g => g.RoleId == orphan);
            await RoleCol.DeleteOneAsync(r => r.Id == orphan);
        }
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task One_tenants_backfill_writes_nothing_into_another_tenant(string key)
    {
        var pair = Pair(key);
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var myReaders = await OldRoleAsync(mine, "Readers", pair.ReadKey);
        var theirWriters = await OldRoleAsync(theirs, "Writers", "auth.roles.create");

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(myReaders, pair));
        Assert.False(await HoldsAsync(theirWriters, pair));
        Assert.Empty(await AuditRowsAsync(theirs, pair.AuditSource));
        Assert.All(await AuditRowsAsync(mine, pair.AuditSource), row => Assert.Equal(mine, row.TenantId));
    }

    // ── FIX2 item 3: one tenant's failure is that tenant's ───────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task One_tenants_failure_does_not_stop_the_other_tenants_or_the_other_key_and_is_logged_without_its_message(string key)
    {
        var pair = Pair(key);
        var other = Other(pair);
        var failing = Guid.NewGuid();
        var healthy = Guid.NewGuid();
        var failingReaders = await OldRoleAsync(failing, "Readers", pair.ReadKey, other.ReadKey);
        var healthyReaders = await OldRoleAsync(healthy, "Readers", pair.ReadKey, other.ReadKey);
        var logs = new CapturingLoggerProvider();

        var result = await RunAsync((stage, p, id) =>
            p == pair && stage == ExportGrantBackfillRunner.Stage.AuditWritten && id == failingReaders
                ? throw new InvalidOperationException("SECRET-DOCUMENT-CONTENT must never be logged")
                : Task.CompletedTask, logs.CreateLogger<ExportGrantBackfillScenarioTests>());

        // The failing tenant: unmarked for this key, retried later …
        Assert.Contains(failing, result.FailedTenants);
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == failing && m.Key == pair.ExportKey));
        // … and the SAME tenant's other key went on, as did the other tenant, for both keys.
        Assert.True(await HoldsAsync(failingReaders, other));
        Assert.True(await HoldsAsync(healthyReaders, pair));
        Assert.True(await HoldsAsync(healthyReaders, other));
        Assert.DoesNotContain(healthy, result.FailedTenants);

        var entry = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains(failing.ToString(), entry.Message);
        Assert.Contains(pair.ExportKey, entry.Message);
        Assert.Contains(typeof(InvalidOperationException).FullName!, entry.Message);
        Assert.DoesNotContain("SECRET-DOCUMENT-CONTENT", entry.Message); // the type, never the message

        // The next start settles the failing tenant. The hook failed AFTER this role's audit row was written, so its
        // grant is not repeated (option B) — the tenant is marked and the role is reported.
        await AgeAuditRowsAsync(failing);
        var next = await RunAsync();
        Assert.DoesNotContain(failing, next.FailedTenants);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == failing && m.Key == pair.ExportKey));
        Assert.Contains((failing, failingReaders, pair.ExportKey), next.GrantsNotRepeated);
    }

    // A write error that is NOT a duplicate key is not swallowed: here the audit collection refuses this tenant's rows.
    // It also proves the order — audit row BEFORE grant: when the audit row cannot be written, no grant is.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_write_error_other_than_a_duplicate_key_fails_the_tenant_and_no_grant_is_written_without_its_audit_row(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var logs = new CapturingLoggerProvider();
        var refuseThisTenant = Builders<AuthAuditLog>.Filter.Ne(a => a.TenantId, tenant)
            .Render(Audit.DocumentSerializer, Audit.Settings.SerializerRegistry);
        await _host.Database.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", "authAuditLogs" }, { "validator", refuseThisTenant } });
        ExportGrantBackfillRunner.Result result;
        try
        {
            result = await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await _host.Database.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", "authAuditLogs" }, { "validator", new BsonDocument() } });
        }

        Assert.Contains(tenant, result.FailedTenants);
        Assert.False(await HoldsAsync(readers, pair)); // no audit row → no grant
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(tenant.ToString()) && e.Message.Contains("MongoWriteException"));

        await RunAsync(); // the validator is gone: the next start finishes it
        Assert.True(await HoldsAsync(readers, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, readers);
    }

    // The tenant's cached authorization snapshots are invalidated as soon as ITS grants are written — also when a
    // tenant processed after it fails.
    [Theory, MemberData(nameof(Keys))]
    public async Task The_role_assignment_version_of_a_backfilled_tenant_is_bumped_even_when_a_later_tenant_fails(string key)
    {
        var pair = Pair(key);
        // Tenants are processed in id order: `first` sorts before `last`.
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(g => g).ToArray();
        var (first, last) = (ids[0], ids[1]);
        await OldRoleAsync(first, "Readers", pair.ReadKey);
        var lastReaders = await OldRoleAsync(last, "Readers", pair.ReadKey);
        var versions = new RoleAssignmentVersionRepository(_host.Database);
        var before = await versions.GetAsync(first, CancellationToken.None);
        var bornBefore = await versions.GetAsync(DefaultTenantId, CancellationToken.None);

        var result = await RunAsync((stage, p, id) =>
            p == pair && id == lastReaders ? throw new InvalidOperationException("later tenant fails") : Task.CompletedTask);

        Assert.Contains(last, result.FailedTenants);
        Assert.True(await versions.GetAsync(first, CancellationToken.None) > before, "the backfilled tenant's version was not bumped");
        Assert.Equal(bornBefore, await versions.GetAsync(DefaultTenantId, CancellationToken.None)); // nothing changed there
    }

    // ── FIX2 item 4: the one-time SOURCE correction of what an earlier build wrote ────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task System_filed_export_grants_of_an_earlier_build_become_Manual_once_can_then_be_revoked_and_nothing_is_added_or_removed(string key)
    {
        var pair = Pair(key);
        var world = RoleEndpointWorld.Create(_host, "Rana", "Steward");
        var tenant = world.TenantId;
        // What the earlier build left: every reading role holds export as a SYSTEM grant, no audit row, a mark without Origin.
        var admin = await RoleAsync(tenant, "Admin", old: true, system: true, GrantSource.System, pair.ReadKey, pair.ExportKey);
        var viewer = await RoleAsync(tenant, "Viewer", old: true, system: true, GrantSource.System, pair.ReadKey, pair.ExportKey);
        var readers = await RoleAsync(tenant, "Readers", old: true, system: false, GrantSource.System, pair.ReadKey, pair.ExportKey);
        var moduleRole = await RoleAsync(tenant, "ModuleReaders", old: true, system: false, GrantSource.Module, pair.ReadKey, pair.ExportKey);
        var noExport = await OldRoleAsync(tenant, "Plain", pair.ReadKey); // export was taken away by hand after the earlier backfill? it never had one
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = pair.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = null });
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();
        // The grants this code may touch: the read / export permissions of both keys. (Another seed step — the tenant
        // Admin self-service reconcile — adds its own keys to a role named "Admin"; that is not this step's doing.)
        var backfillKeys = ExportGrantBackfill.Keys.SelectMany(k => new[] { k.ReadKey, k.ExportKey }).ToList();
        var backfillPermissions = (await Permissions.Find(p => backfillKeys.Contains(p.Key)).ToListAsync()).Select(p => p.Id).ToList();
        async Task<List<(Guid, Guid)>> HeldAsync() => (await Grants.Find(g => g.TenantId == tenant && backfillPermissions.Contains(g.PermissionId)).ToListAsync())
            .Select(g => (g.RoleId, g.PermissionId)).OrderBy(x => x).ToList();
        var grantsBefore = await HeldAsync();

        await DataSeeder.SeedAsync(_host.Database);

        // Sources: only the two the template does not serve changed.
        Assert.Equal(GrantSource.System, (await GrantRowAsync(admin, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(viewer, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(readers, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Module, (await GrantRowAsync(moduleRole, export.Id)).GrantSource);
        // GRANTS NOTHING, REMOVES NOTHING: the same (role, permission) pairs as before.
        Assert.Equal(grantsBefore, await HeldAsync());
        Assert.False(await HoldsAsync(noExport, pair));
        // One audit row per corrected grant, its own event and source; no "granted" row.
        var rows = await AuditRowsAsync(tenant, pair.CorrectionAuditSource);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(ExportGrantBackfill.SourceCorrectedEventName, r.EventName));
        Assert.Single(rows, r => r.Metadata.Contains(viewer.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Single(rows, r => r.Metadata.Contains(readers.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Empty((await AuditRowsAsync(tenant, pair.AuditSource)).Where(r => r.EventName == ExportGrantBackfill.AuditEventName));
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(tenant, pair)).Origin);

        // The corrected grant can now be taken away on the product's own path …
        using var client = world.Client();
        var revoked = await client.DeleteAsync($"api/roles/{readers}/permissions/{export.Id}");
        Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync());

        // … and a second start writes nothing and does not hand it back.
        var auditBefore = await Audit.CountDocumentsAsync(a => a.TenantId == tenant);
        var second = await RunAsync();
        Assert.Equal(0, second.SourcesCorrected);
        Assert.Equal(0, second.GrantsWritten);
        Assert.False(await HoldsAsync(readers, pair));
        Assert.Equal(auditBefore, await Audit.CountDocumentsAsync(a => a.TenantId == tenant));
    }

    // ── FIX2 item 5: reads are bounded by what is left to do ─────────────────────────────────────────────

    [Fact]
    public async Task When_every_tenant_is_settled_no_role_and_no_grant_document_is_read()
    {
        await RunAsync(); // settle whatever other tests left
        var commands = new ConcurrentQueue<(string Name, string Collection)>();
        var settings = _host.Database.Client.Settings.Clone();
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(e =>
        {
            if (e.Command.TryGetValue(e.CommandName, out var target) && target.IsString) commands.Enqueue((e.CommandName, target.AsString));
        });
        var observed = new MongoClient(settings).GetDatabase(_host.Database.DatabaseNamespace.DatabaseName);

        var result = await ExportGrantBackfillRunner.RunAsync(observed, NullLogger.Instance);

        Assert.Equal(0, result.GrantsWritten);
        var seen = commands.ToArray();
        // The roles collection is asked ONE indexed question — "which tenants are not settled?" — and no document.
        Assert.Equal([("distinct", "roles")], seen.Where(c => c.Collection == "roles"));
        Assert.DoesNotContain(seen, c => c.Collection == "rolePermissions");
        Assert.DoesNotContain(seen, c => c.Collection == "authAuditLogs");
        Assert.DoesNotContain(seen, c => c.Name is "insert" or "update" or "delete");
    }

    [Fact]
    public async Task With_one_unsettled_tenant_only_its_roles_and_only_the_four_permissions_grants_are_read()
    {
        await RunAsync();
        var tenant = Guid.NewGuid();
        await OldRoleAsync(tenant, "Readers", ExportGrantBackfill.Users.ReadKey);
        var finds = new ConcurrentQueue<BsonDocument>();
        var settings = _host.Database.Client.Settings.Clone();
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(e =>
        {
            if (e.CommandName == "find") finds.Enqueue(e.Command.DeepClone().AsBsonDocument);
        });
        var observed = new MongoClient(settings).GetDatabase(_host.Database.DatabaseNamespace.DatabaseName);

        await ExportGrantBackfillRunner.RunAsync(observed, NullLogger.Instance);

        var roleFinds = finds.Where(f => f["find"] == "roles").ToList();
        Assert.NotEmpty(roleFinds);
        Assert.All(roleFinds, f =>
        {
            Assert.True(f["filter"].ToJson().Contains("TenantId"), "roles are read without a tenant filter");
            Assert.True(f.Contains("projection"), "whole role documents are read");
        });
        var grantFind = Assert.Single(finds, f => f["find"] == "rolePermissions");
        var filter = grantFind["filter"].ToJson();
        Assert.Contains("TenantId", filter);
        Assert.Contains("PermissionId", filter);
        Assert.True(grantFind.Contains("projection"), "whole grant documents are read");
    }

    // ── FIX4 item 1: an UNKNOWN outcome withdraws nothing ────────────────────────────────────────────────

    // The grant insert reached the server and was written, then the acknowledgement was lost (a timeout after the
    // send). The run cannot know the outcome: it writes no "not applied" row; the next start sees the grant.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_grant_written_whose_acknowledgement_was_lost_is_not_withdrawn(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        var first = await ExportGrantBackfillRunner.RunAsync(_host.Database, NullLogger.Instance, grantWriter: async (col, row, ct) =>
        {
            await col.InsertOneAsync(row, cancellationToken: ct);
            if (row.TenantId == tenant) throw new TimeoutException("the acknowledgement was lost");
            return true;
        });

        Assert.Contains(tenant, first.FailedTenants);
        Assert.True(await HoldsAsync(readers, pair)); // it IS there
        Assert.Empty((await AuditRowsAsync(tenant, pair.AuditSource)).Where(r => r.EventName == ExportGrantBackfill.GrantNotAppliedEventName));

        var next = await RunAsync();
        Assert.DoesNotContain(next.GrantsNotRepeated, x => x.TenantId == tenant); // nothing missing, nothing reported
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
        Assert.Single(await AuditRowsAsync(tenant, pair.AuditSource));
    }

    // ── FIX4 item 2: a read grant that STORES no CreatedAt has no date ────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task An_old_roles_read_grant_without_a_stored_CreatedAt_leaves_the_decision_to_the_roles_age(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var read = await Permissions.Find(p => p.Key == pair.ReadKey).SingleAsync();
        var rawGrants = _host.Database.GetCollection<BsonDocument>("rolePermissions");
        var grantId = (await Grants.Find(g => g.RoleId == readers && g.PermissionId == read.Id).SingleAsync()).Id;
        await rawGrants.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(grantId, GuidRepresentation.Standard)),
            new BsonDocument("$unset", new BsonDocument("CreatedAt", ""))); // the real shape: the field is ABSENT
        Assert.Equal(1, await Grants.CountDocumentsAsync(Builders<RolePermission>.Filter.Eq(g => g.Id, grantId) & Builders<RolePermission>.Filter.Exists(g => g.CreatedAt, false)));

        await RunAsync();

        Assert.True(await HoldsAsync(readers, pair)); // the role predates the key; an undated read grant does not block it
    }

    // ── FIX4 item 4: a broken mark, and a role whose TenantId is not a tenant id ─────────────────────────

    [Fact]
    public async Task A_broken_mark_and_a_role_with_a_text_TenantId_affect_no_one_else()
    {
        var withBrokenMark = Guid.NewGuid();
        var healthy = Guid.NewGuid();
        var brokenMarkReaders = await OldRoleAsync(withBrokenMark, "Readers", ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);
        var healthyReaders = await OldRoleAsync(healthy, "Readers", ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);
        var badMarkId = Guid.NewGuid();
        var textRoleId = Guid.NewGuid();
        var rawMarks = _host.Database.GetCollection<BsonDocument>(PermissionReconciliationMark.CollectionName);
        var rawRoles = _host.Database.GetCollection<BsonDocument>("roles");
        await rawMarks.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(badMarkId, GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(withBrokenMark, GuidRepresentation.Standard) },
            { "Key", ExportGrantBackfill.Users.ExportKey },
            { "ReconciledAtUtc", new BsonDocument("SECRET", "not a date") } // does not deserialize
        });
        await rawRoles.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(textRoleId, GuidRepresentation.Standard) },
            { "TenantId", "SECRET-text-tenant" }, // not a tenant id — $nin matches it too
            { "Name", "Orphan" },
            { "IsDeleted", false }
        });
        var logs = new CapturingLoggerProvider();
        ExportGrantBackfillRunner.Result result;
        try
        {
            result = await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await rawMarks.DeleteOneAsync(new BsonDocument("_id", new BsonBinaryData(badMarkId, GuidRepresentation.Standard)));
            await rawRoles.DeleteOneAsync(new BsonDocument("_id", new BsonBinaryData(textRoleId, GuidRepresentation.Standard)));
        }

        // The tenant with the unreadable mark is left alone (not treated as unmarked), logged with the mark's id …
        Assert.Contains(withBrokenMark, result.FailedTenants);
        Assert.False(await HoldsAsync(brokenMarkReaders, ExportGrantBackfill.Users));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(badMarkId.ToString()) && e.Message.Contains(withBrokenMark.ToString()));
        // … the role with a text TenantId is skipped and named by its id …
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(textRoleId.ToString()));
        // … and everybody else, for both keys, went on.
        Assert.DoesNotContain(healthy, result.FailedTenants);
        foreach (var pair in ExportGrantBackfill.Keys) Assert.True(await HoldsAsync(healthyReaders, pair));
        Assert.All(logs.Entries, e => Assert.DoesNotContain("SECRET", e.Message));
    }

    // ── FIX4 item 6: a document id that is not an identifier is never printed ────────────────────────────

    [Fact]
    public async Task A_broken_document_whose_id_is_not_an_identifier_is_named_by_type_only()
    {
        var tenant = Guid.NewGuid();
        await OldRoleAsync(tenant, "Readers", ExportGrantBackfill.Users.ReadKey);
        var rawRoles = _host.Database.GetCollection<BsonDocument>("roles");
        var odd = new BsonDocument("SECRET-in-the-id", 42);
        await rawRoles.InsertOneAsync(new BsonDocument
        {
            { "_id", odd },
            { "TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard) },
            { "Name", "Odd" },
            { "IsDeleted", false }
        });
        var logs = new CapturingLoggerProvider();
        try
        {
            await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await rawRoles.DeleteOneAsync(new BsonDocument("_id", odd));
        }

        var entry = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(tenant.ToString()));
        Assert.Contains("<Document>", entry.Message);
        Assert.DoesNotContain("SECRET", entry.Message);
    }

    // ── FIX4 item 5: the seeder's own catch logs the type only ────────────────────────────────────────────

    [Fact]
    public async Task The_seeders_catch_writes_the_exception_type_and_never_its_message()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await DataSeeder.SeedAsync(_host.Database, false, null,
                () => throw new InvalidOperationException("SECRET-outer", new FormatException("SECRET-inner")));
        }
        finally
        {
            Console.SetOut(original);
        }

        var text = output.ToString();
        Assert.Contains("Critical Seeding Error: " + typeof(InvalidOperationException).FullName, text);
        Assert.Contains("Inner: " + typeof(FormatException).FullName, text);
        Assert.DoesNotContain("SECRET", text);
    }

    // ── FIX4: a broken catalog row of one key ────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task A_catalog_row_that_does_not_deserialize_skips_its_key_and_the_other_key_goes_on(string key)
    {
        var pair = Pair(key);
        var other = Other(pair);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey, other.ReadKey);
        var raw = _host.Database.GetCollection<BsonDocument>("permissions");
        var stored = (await raw.Find(new BsonDocument("Key", pair.ExportKey)).SingleAsync())["DisplayName"];
        await raw.UpdateOneAsync(new BsonDocument("Key", pair.ExportKey), new BsonDocument("$set", new BsonDocument("DisplayName", new BsonDocument("SECRET", 1))));
        var logs = new CapturingLoggerProvider();
        try
        {
            await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await raw.UpdateOneAsync(new BsonDocument("Key", pair.ExportKey), new BsonDocument("$set", new BsonDocument("DisplayName", stored)));
        }

        Assert.False(await HoldsAsync(readers, pair));
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
        Assert.True(await HoldsAsync(readers, other));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("catalog row"));
        Assert.All(logs.Entries, e => Assert.DoesNotContain("SECRET", e.Message));
    }

    // ── FIX5 item 1: a mark whose tenant cannot be read stops its KEY for everybody ───────────────────────

    public static TheoryData<string> UnreadableTenantIds => new() { "text", "binary-not-a-uuid" };

    [Theory, MemberData(nameof(UnreadableTenantIds))]
    public async Task A_mark_whose_TenantId_cannot_be_read_stops_its_key_for_every_tenant_and_the_other_key_goes_on(string form)
    {
        var mine = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var myReaders = await OldRoleAsync(mine, "Readers", ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);
        var otherReaders = await OldRoleAsync(elsewhere, "Readers", ExportGrantBackfill.Users.ReadKey, ExportGrantBackfill.Roles.ReadKey);
        var markId = Guid.NewGuid();
        var rawMarks = _host.Database.GetCollection<BsonDocument>(PermissionReconciliationMark.CollectionName);
        await rawMarks.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(markId, GuidRepresentation.Standard) },
            // The tenant this mark was meant for, in a form that is not a tenant id.
            { "TenantId", form == "text" ? (BsonValue)mine.ToString() : new BsonBinaryData(mine.ToByteArray(), BsonBinarySubType.Binary) },
            { "Key", ExportGrantBackfill.Users.ExportKey },
            { "ReconciledAtUtc", DateTime.UtcNow },
            { "Origin", ExportGrantBackfill.OriginBackfilled }
        });
        var logs = new CapturingLoggerProvider();
        try
        {
            await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await rawMarks.DeleteOneAsync(new BsonDocument("_id", new BsonBinaryData(markId, GuidRepresentation.Standard)));
        }

        // Nobody decides the users key on this start — not the tenant the mark may have meant, not any other …
        Assert.False(await HoldsAsync(myReaders, ExportGrantBackfill.Users));
        Assert.False(await HoldsAsync(otherReaders, ExportGrantBackfill.Users));
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.Key == ExportGrantBackfill.Users.ExportKey && (m.TenantId == mine || m.TenantId == elsewhere)));
        // … the roles key goes on …
        Assert.True(await HoldsAsync(myReaders, ExportGrantBackfill.Roles));
        Assert.True(await HoldsAsync(otherReaders, ExportGrantBackfill.Roles));
        // … and the log names the document and the key.
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(markId.ToString()));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(ExportGrantBackfill.Users.ExportKey) && e.Message.Contains("decides nothing"));

        await RunAsync(); // the broken mark is gone: the next start decides the users key again
        Assert.True(await HoldsAsync(otherReaders, ExportGrantBackfill.Users));
    }

    // ── FIX5 item 3: a text id is printed bounded and without control characters ─────────────────────────

    [Fact]
    public async Task A_text_document_id_is_logged_bounded_and_with_its_line_breaks_replaced()
    {
        var tenant = Guid.NewGuid();
        await OldRoleAsync(tenant, "Readers", ExportGrantBackfill.Users.ReadKey);
        var rawRoles = _host.Database.GetCollection<BsonDocument>("roles");
        var id = "line1\nFORGED log line " + new string('x', 100);
        await rawRoles.InsertOneAsync(new BsonDocument
        {
            { "_id", id }, // a text id — the role's Guid Id cannot be read from it
            { "TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard) },
            { "Name", "Odd" },
            { "IsDeleted", false }
        });
        var logs = new CapturingLoggerProvider();
        try
        {
            await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await rawRoles.DeleteOneAsync(new BsonDocument("_id", id));
        }

        var entry = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(tenant.ToString()));
        Assert.Contains("line1?FORGED log line ", entry.Message);
        Assert.DoesNotContain("\n", entry.Message);
        Assert.Contains(new string('x', 64 - "line1\nFORGED log line ".Length), entry.Message);
        Assert.DoesNotContain(new string('x', 64 - "line1\nFORGED log line ".Length + 1), entry.Message);
    }

    // ── FIX5: rules that had no test of their own ────────────────────────────────────────────────────────

    // A failure BEFORE the server answered (a timeout on the way): the outcome is unknown, nothing is withdrawn.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_grant_write_that_times_out_without_reaching_the_server_withdraws_nothing(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        var first = await ExportGrantBackfillRunner.RunAsync(_host.Database, NullLogger.Instance, grantWriter: (col, row, ct) =>
            row.TenantId == tenant ? throw new TimeoutException("never reached the server") : col.InsertOneAsync(row, cancellationToken: ct).ContinueWith(_ => true, ct));

        Assert.Contains(tenant, first.FailedTenants);
        Assert.False(await HoldsAsync(readers, pair));
        Assert.Empty((await AuditRowsAsync(tenant, pair.AuditSource)).Where(r => r.EventName == ExportGrantBackfill.GrantNotAppliedEventName));
    }

    // A write error from the server while the grant IS there (here: the insert landed, the retry is the duplicate):
    // the grant is in, so nothing is withdrawn.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_write_error_while_the_grant_is_there_withdraws_nothing(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        var first = await ExportGrantBackfillRunner.RunAsync(_host.Database, NullLogger.Instance, grantWriter: async (col, row, ct) =>
        {
            await col.InsertOneAsync(row, cancellationToken: ct);
            if (row.TenantId == tenant) await col.InsertOneAsync(row, cancellationToken: ct); // a server write error (duplicate key)
            return true;
        });

        Assert.Contains(tenant, first.FailedTenants);
        Assert.True(await HoldsAsync(readers, pair));
        Assert.Empty((await AuditRowsAsync(tenant, pair.AuditSource)).Where(r => r.EventName == ExportGrantBackfill.GrantNotAppliedEventName));
    }

    // The source correction of an earlier build reads the tenant's roles; one that cannot be read stops the
    // correction of THAT tenant: nothing is changed, the mark keeps no Origin and it is tried again.
    [Theory, MemberData(nameof(Keys))]
    public async Task A_source_correction_with_an_unreadable_role_of_its_tenant_changes_nothing_and_is_retried(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var viewer = await RoleAsync(tenant, "Viewer", old: true, system: true, GrantSource.System, pair.ReadKey, pair.ExportKey);
        await Marks.InsertOneAsync(new PermissionReconciliationMark { TenantId = tenant, Key = pair.ExportKey, ReconciledAtUtc = DateTime.UtcNow, Origin = null });
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();
        var brokenId = Guid.NewGuid();
        var rawRoles = _host.Database.GetCollection<BsonDocument>("roles");
        await rawRoles.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(brokenId, GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard) },
            { "Name", new BsonDocument("not", "a name") }, // does not deserialize
            { "IsDeleted", false }
        });
        ExportGrantBackfillRunner.Result result;
        try
        {
            result = await RunAsync();
        }
        finally
        {
            await rawRoles.DeleteOneAsync(new BsonDocument("_id", new BsonBinaryData(brokenId, GuidRepresentation.Standard)));
        }

        Assert.Contains(tenant, result.FailedTenants);
        Assert.Equal(0, result.SourcesCorrected);
        Assert.Equal(GrantSource.System, (await GrantRowAsync(viewer, export.Id)).GrantSource);
        Assert.Null((await MarkAsync(tenant, pair)).Origin);

        await RunAsync(); // readable again: corrected
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(viewer, export.Id)).GrantSource);
    }

    // A role whose TenantId is binary but no UUID: logged by id, skipped — it never stops the run.
    [Fact]
    public async Task A_role_whose_TenantId_is_binary_but_no_uuid_is_skipped_and_the_run_goes_on()
    {
        var healthy = Guid.NewGuid();
        var healthyReaders = await OldRoleAsync(healthy, "Readers", ExportGrantBackfill.Users.ReadKey);
        var oddId = Guid.NewGuid();
        var rawRoles = _host.Database.GetCollection<BsonDocument>("roles");
        await rawRoles.InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(oddId, GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(Guid.NewGuid().ToByteArray(), BsonBinarySubType.Binary) },
            { "Name", "Odd" },
            { "IsDeleted", false }
        });
        var logs = new CapturingLoggerProvider();
        try
        {
            await RunAsync(logger: logs.CreateLogger<ExportGrantBackfillScenarioTests>());
        }
        finally
        {
            await rawRoles.DeleteOneAsync(new BsonDocument("_id", new BsonBinaryData(oddId, GuidRepresentation.Standard)));
        }

        Assert.True(await HoldsAsync(healthyReaders, ExportGrantBackfill.Users));
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(oddId.ToString()));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Three reading roles of an old tenant, in the order the runner processes them (by role id).</summary>
    private async Task<Guid[]> ThreeOldReadersAsync(Guid tenant, ExportGrantBackfill.KeyPair pair)
        => (new[]
        {
            await OldRoleAsync(tenant, "Readers-1", pair.ReadKey),
            await OldRoleAsync(tenant, "Readers-2", pair.ReadKey),
            await OldRoleAsync(tenant, "Readers-3", pair.ReadKey)
        }).OrderBy(id => id).ToArray();

    /// <summary>
    /// A start that fails inside this tenant: at the SECOND role's <paramref name="stage"/> (for
    /// TenantGrantsWritten: after all three grants, before the mark). Returns the first and the second role processed.
    /// </summary>
    private async Task<(Guid First, Guid Second)> StopAtSecondRoleAsync(ExportGrantBackfill.KeyPair pair, Guid tenant, Guid[] roles, ExportGrantBackfillRunner.Stage stage)
    {
        var seen = 0;
        var result = await RunAsync((at, p, id) =>
        {
            if (p != pair || at != stage) return Task.CompletedTask;
            var mine = at == ExportGrantBackfillRunner.Stage.TenantGrantsWritten ? id == tenant : roles.Contains(id);
            if (mine && (at == ExportGrantBackfillRunner.Stage.TenantGrantsWritten || ++seen == 2))
            {
                throw new InvalidOperationException("the write failed here");
            }

            return Task.CompletedTask;
        });
        Assert.Contains(tenant, result.FailedTenants);
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey)); // not marked: not finished
        return (roles[0], roles[1]);
    }

    /// <summary>The tenant's audit rows as a later start sees them: written longer ago than a slow run could still be on
    /// its way to its grant (<see cref="ExportGrantBackfillRunner.OwnerGrace"/>).</summary>
    private Task AgeAuditRowsAsync(Guid tenant)
        => Audit.UpdateManyAsync(a => a.TenantId == tenant,
            Builders<AuthAuditLog>.Update.Set(a => a.OccurredAt, DateTimeOffset.UtcNow - ExportGrantBackfillRunner.OwnerGrace - TimeSpan.FromMinutes(1)));

    private async Task AssertALaterStartChangesNothingAsync(Guid tenant)
    {
        var grants = await Grants.CountDocumentsAsync(g => g.TenantId == tenant);
        var audit = await Audit.CountDocumentsAsync(a => a.TenantId == tenant);
        var later = await RunAsync();
        Assert.DoesNotContain(later.GrantsNotRepeated, x => x.TenantId == tenant); // a settled tenant is not scanned again
        Assert.Equal(grants, await Grants.CountDocumentsAsync(g => g.TenantId == tenant));
        Assert.Equal(audit, await Audit.CountDocumentsAsync(a => a.TenantId == tenant));
    }

    /// <summary>A custom role of an OLD tenant (it existed before the export keys), holding <paramref name="keys"/> by hand.</summary>
    private Task<Guid> OldRoleAsync(Guid tenant, string name, params string[] keys)
        => RoleAsync(tenant, name, old: true, system: false, GrantSource.Manual, keys);

    private async Task<Guid> RoleAsync(Guid tenant, string name, bool old, bool system, GrantSource source, params string[] keys)
    {
        // "Old" = before BOTH export keys entered the catalog; "new" = after both.
        var created = (await Permissions.Find(p => p.Key == ExportGrantBackfill.Users.ExportKey || p.Key == ExportGrantBackfill.Roles.ExportKey).ToListAsync())
            .Select(p => p.CreatedAt).ToList();
        return await RoleAtAsync(tenant, name, old ? created.Min().AddDays(-30) : created.Max().AddMinutes(5), system, source, keys);
    }

    /// <summary>A role document written straight into the collection with the given <c>CreatedAt</c>.</summary>
    private async Task<Guid> RoleAtAsync(Guid tenant, string name, DateTimeOffset createdAt, bool system, GrantSource source, params string[] keys)
    {
        var role = new Role(name, name, "export backfill fixture", tenant) { CreatedAt = createdAt };
        if (system) role.MarkAsSystem();
        await RoleCol.InsertOneAsync(role);
        // The role's grants are as old as the role (an old tenant's read grant predates the key too).
        foreach (var key in keys) await GrantAsync(role.Id, tenant, source, key, createdAt == default ? null : createdAt);
        return role.Id;
    }

    private async Task GrantAsync(Guid roleId, Guid tenant, GrantSource source, string key, DateTimeOffset? createdAt = null)
    {
        var permission = await Permissions.Find(p => p.Key == key).SingleAsync();
        var grant = source switch
        {
            GrantSource.System => RolePermission.SystemGrant(roleId, permission.Id, tenant, "system"),
            GrantSource.Module => RolePermission.ModuleGrant(roleId, permission.Id, tenant, "system", "access-governance"),
            _ => RolePermission.ManualGrant(roleId, permission.Id, tenant, "export-backfill-fixture")
        };
        await Grants.InsertOneAsync(grant);
        if (createdAt is { } at) await Grants.UpdateOneAsync(g => g.Id == grant.Id, Builders<RolePermission>.Update.Set(g => g.CreatedAt, at));
    }

    private async Task<DateTimeOffset> KeyCreatedAtAsync(ExportGrantBackfill.KeyPair pair)
        => (await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync()).CreatedAt;

    private Task<bool> HoldsAsync(Guid roleId, ExportGrantBackfill.KeyPair pair) => HoldsKeyAsync(roleId, pair.ExportKey);

    /// <summary>EXACTLY one live grant of the key on the role (a duplicate is a failure, not a pass).</summary>
    private async Task<bool> HoldsKeyAsync(Guid roleId, string key)
    {
        var id = (await Permissions.Find(p => p.Key == key).SingleAsync()).Id;
        return await Grants.CountDocumentsAsync(g => g.RoleId == roleId && g.PermissionId == id && g.IsDeleted == false) == 1;
    }

    private async Task<RolePermission> GrantRowAsync(Guid roleId, Guid permissionId)
        => await Grants.Find(g => g.RoleId == roleId && g.PermissionId == permissionId).SingleAsync();

    private async Task<PermissionReconciliationMark> MarkAsync(Guid tenant, ExportGrantBackfill.KeyPair pair)
        => await Marks.Find(m => m.TenantId == tenant && m.Key == pair.ExportKey).SingleAsync();

    /// <summary>The tenant's audit rows that name <paramref name="source"/> as where they came from.</summary>
    private async Task<List<AuthAuditLog>> AuditRowsAsync(Guid tenant, string source)
    {
        var rows = await Audit.Find(a => a.TenantId == tenant).ToListAsync();
        return rows.Where(r => r.Metadata.Contains($"\"source\":\"{source}\"", StringComparison.Ordinal)).ToList();
    }

    private async Task AssertOneAuditRowPerGrantAsync(Guid tenant, ExportGrantBackfill.KeyPair pair, params Guid[] roles)
    {
        var rows = await AuditRowsAsync(tenant, pair.AuditSource);
        Assert.Equal(roles.Length, rows.Count);
        foreach (var role in roles)
        {
            Assert.Single(rows, r => r.Metadata.Contains(role.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
