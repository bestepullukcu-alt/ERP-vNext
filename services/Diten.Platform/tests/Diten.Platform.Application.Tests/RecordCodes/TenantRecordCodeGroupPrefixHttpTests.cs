using System.Net;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §4 "Yönetici ucu" — <c>PUT</c> (bodyless alignment) and <c>GET /api/admin/tenants/{tenantId}/record-code-group-prefix</c>
/// over real HTTP: the production <c>TenantResolutionMiddleware</c> (platform context on /api/admin), the production
/// platform-actor policies, the production MediatR pipeline (<c>AuditBehavior</c> lets the command through only because
/// it is named in <c>TransactionOwnedAuditCommands.Authorized</c>), the production door and refusal writer on a real
/// replica set.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class TenantRecordCodeGroupPrefixHttpTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid T = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a4");
    private static readonly Guid U = Guid.Parse("40fc0002-0000-4000-8000-00000000b0b4");
    private static readonly Guid Admin = Guid.Parse("40fc0002-0000-4000-8000-0000000000d4");
    private static readonly string AdminToken = RecordCodeHttpHost.Token("platform_admin", "00000000-0000-0000-0000-000000000001", Admin,
        "platform.record-code-group-prefixes.update", "platform.record-code-group-prefixes.read");

    private readonly DisposableMongoReplicaSet _mongo;

    public TenantRecordCodeGroupPrefixHttpTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    private Task<RecordCodeHttpHost> HostAsync(RecordCodeHostOptions? options = null, string database = "diten_record_code_admin") =>
        RecordCodeHttpHost.StartAsync(_mongo, database, options, T, U);

    private static string Path(Guid tenant) => $"/api/admin/tenants/{tenant}/record-code-group-prefix";

    private static Task<BsonDocument?> SettingAsync(RecordCodeHttpHost host, Guid tenant) =>
        host.Collection("record_code_group_prefixes").Find(new BsonDocument("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard)))
            .FirstOrDefaultAsync()!;

    // ── Owner decision B (2026-11-02) — the three time fields are DateTime, UTC ─────────────────────────────

    /// <summary>
    /// <c>AlignedAtUtc</c>, <c>AllocatedAtUtc</c> and <c>LockedAtUtc</c> are <c>DateTime</c> (not the pack §4
    /// <c>DateTimeOffset</c>; owner decision B, 2026-11-02): written by the production handlers as BSON dates — scalar,
    /// range-sortable — and read back by the production repositories with <see cref="DateTimeKind.Utc"/> at the instant
    /// they were written.
    /// </summary>
    [Fact]
    public async Task DecisionB_the_three_time_fields_are_written_as_BSON_dates_and_read_back_with_Kind_Utc()
    {
        using var host = await HostAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var aligned = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(T), AdminToken));
        var allocated = await RecordCodeHttpHost.ReadAsync(await host.AsMdmGroupAsync(T, "utc"));
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.Equal((200, 200), (aligned.Status, allocated.Status));
        var setting = (await SettingAsync(host, T))!;
        var ledger = await host.Collection("record_code_allocations").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal(
            (BsonType.DateTime, BsonType.DateTime, BsonType.DateTime),
            (setting["AlignedAtUtc"].BsonType, setting["LockedAtUtc"].BsonType, ledger["AllocatedAtUtc"].BsonType));

        var context = new PlatformDbContext(_mongo.Client, host.Database);
        var tenant = new TenantContext();
        tenant.SetTenant(T);
        var read = (await new RecordCodeGroupPrefixRepository(context, tenant).GetAsync(T))!;
        var allocation = (await new RecordCodeAllocationRepository(context, tenant)
            .FindByIdempotencyKeyAsync(T, ledger["CallerService"].AsString, "utc"))!;
        foreach (var value in new[] { read.AlignedAtUtc, read.LockedAtUtc!.Value, allocation.AllocatedAtUtc })
        {
            Assert.Equal(DateTimeKind.Utc, value.Kind);
            Assert.InRange(value, before, after);
        }

        Assert.Equal(setting["LockedAtUtc"].ToUniversalTime(), read.LockedAtUtc.Value);
        Assert.Equal(ledger["AllocatedAtUtc"].ToUniversalTime(), allocation.AllocatedAtUtc);
    }

    // ── AC-43 / AC-22 — success path ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC43_a_platform_admin_aligns_the_route_tenant_in_its_own_context_with_one_in_transaction_record()
    {
        using var host = await HostAsync();

        var put = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(T), AdminToken));
        var get = await host.SendAsync(HttpMethod.Get, Path(T), AdminToken);

        Assert.Equal(200, put.Status);
        var setting = await SettingAsync(host, T);
        Assert.NotNull(setting);
        Assert.Equal("GMG", setting!["GroupPrefix"].AsString);
        Assert.Null(await SettingAsync(host, U));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Contains("\"groupPrefix\":\"GMG\"", await get.Content.ReadAsStringAsync());
        var outbox = await host.Collection("audit_outbox").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.StartsWith("record-code:group-prefix-aligned:", outbox["IdempotencyKey"].AsString);

        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal((T, (Guid?)T), (audit.TenantId, audit.TargetTenantId));
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
        Assert.Equal(Admin, audit.ActorId);
        Assert.Equal("AlignRecordCodeGroupPrefixCommand", audit.RequestType);
        Assert.Equal("RecordCodeGroupPrefix", audit.EntityType);
        Assert.Equal(setting["_id"].AsGuid, audit.EntityId);
        Assert.Equal(AuditOperation.Create, audit.Operation);
        Assert.Equal(AuditCategory.PlatformConfiguration, audit.Category);
        Assert.Equal("Diten.Platform", audit.SourceService);
        Assert.Equal("MOD-0040-FU02", audit.SourceModule);
        Assert.Null(audit.BeforeState);
        Assert.Equal("GMG", audit.AfterState!["GroupPrefix"]);
        Assert.Null(audit.AfterState["LockedAtUtc"]);
        Assert.Equal("platform.record-code.group-prefix.aligned", audit.Metadata["EventName"]);
    }

    [Fact]
    public async Task AC43_the_handler_returns_the_request_to_the_platform_context()
    {
        using var host = await HostAsync();
        ITenantContext? seen = null;
        var handler = new Diten.Platform.Application.Features.RecordCodes.Handlers.CommandHandlers.AlignRecordCodeGroupPrefixHandler(
            new PlatformTransactionExecutor(new PlatformDbContext(_mongo.Client, host.Database)),
            ActivatorUtilities.CreateInstance<Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter>(host.Services.CreateScope().ServiceProvider),
            ActivatorUtilities.CreateInstance<Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter>(host.Services.CreateScope().ServiceProvider),
            new Diten.Platform.Infrastructure.Persistence.Repositories.TenantRegistryRepository(new PlatformDbContext(_mongo.Client, host.Database), Platform()),
            new Diten.Platform.Infrastructure.Persistence.Repositories.RecordCodeGroupPrefixRepository(new PlatformDbContext(_mongo.Client, host.Database), seen = Platform()),
            seen,
            new Diten.Platform.Infrastructure.Services.CurrentUserContext(new Microsoft.AspNetCore.Http.HttpContextAccessor()),
            Microsoft.Extensions.Options.Options.Create(new Diten.Platform.Application.Features.DocumentManagementIdentifiers.DocumentCodingOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Application.Features.RecordCodes.Handlers.CommandHandlers.AlignRecordCodeGroupPrefixHandler>.Instance);

        // The door refuses (no principal here) inside the scope: the scope must still hand the platform context back.
        await Record.ExceptionAsync(() =>
            handler.Handle(new Diten.Platform.Application.Features.RecordCodes.Commands.AlignRecordCodeGroupPrefixCommand(T, false), default));

        Assert.True(seen.IsPlatformContext);
        Assert.Equal(Guid.Empty, seen.TenantId);
    }

    [Fact]
    public async Task AC22_re_aligning_an_unlocked_setting_records_before_and_after_and_the_same_value_records_nothing()
    {
        using var gmg = await HostAsync();
        using var gmx = new RecordCodeHttpHost(_mongo.Client, gmg.Database, new RecordCodeHostOptions { OrgPrefix = "GMX" });

        await gmg.SendAsync(HttpMethod.Put, Path(T), AdminToken);
        await gmg.SendAsync(HttpMethod.Put, Path(T), AdminToken);              // same value: no change, no record
        var second = await RecordCodeHttpHost.ReadAsync(await gmx.SendAsync(HttpMethod.Put, Path(T), AdminToken));

        Assert.Equal(200, second.Status);
        Assert.Equal("GMX", (await SettingAsync(gmg, T))!["GroupPrefix"].AsString);
        var events = (await gmg.DeliveredAuditEventsAsync()).OrderBy(e => e.Operation).ToArray();
        Assert.Equal(2, events.Length);
        var update = Assert.Single(events, e => e.Operation == AuditOperation.Update);
        Assert.Equal("GMG", update.BeforeState!["GroupPrefix"]);
        Assert.True(update.BeforeState.ContainsKey("LockedAtUtc"));
        Assert.Null(update.BeforeState["LockedAtUtc"]);
        Assert.Equal("GMX", update.AfterState!["GroupPrefix"]);
    }

    [Fact]
    public async Task AC22_a_locked_setting_refuses_another_prefix_409_and_accepts_its_own_200()
    {
        using var gmg = await HostAsync();
        using var gmx = new RecordCodeHttpHost(_mongo.Client, gmg.Database, new RecordCodeHostOptions { OrgPrefix = "GMX" });
        await gmg.SeedGroupPrefixAsync(T, "GMG", locked: true);

        var other = await RecordCodeHttpHost.ReadAsync(await gmx.SendAsync(HttpMethod.Put, Path(T), AdminToken));
        var same = await RecordCodeHttpHost.ReadAsync(await gmg.SendAsync(HttpMethod.Put, Path(T), AdminToken));

        Assert.Equal((409, "GROUP_PREFIX_LOCKED"), (other.Status, other.Reason));
        Assert.Equal(200, same.Status);
        Assert.Equal("GMG", (await SettingAsync(gmg, T))!["GroupPrefix"].AsString);
    }

    [Fact]
    public async Task AC22_an_alignment_and_a_first_allocation_at_once_never_overwrite_silently()
    {
        using var gmx = await HostAsync(new RecordCodeHostOptions { OrgPrefix = "GMX" });
        using var gmg = new RecordCodeHttpHost(_mongo.Client, gmx.Database);
        await gmg.SeedGroupPrefixAsync(T, "GMG");
        var bank = RecordCodeHttpHost.TenantUserToken(T, Guid.NewGuid(), "mdm.banks.create");

        var allocation = gmg.AllocateAsMdmAsync(bank, RecordCodeHttpHost.Body("BNK", "Group", null, "g"));
        var alignment = gmx.SendAsync(HttpMethod.Put, Path(T), AdminToken);
        var allocated = await RecordCodeHttpHost.ReadAsync(await allocation);
        var aligned = await RecordCodeHttpHost.ReadAsync(await alignment);

        var setting = (await SettingAsync(gmg, T))!;
        if (allocated.Status == 200)
        {
            // The allocation won: the setting is locked on GMG and the alignment was refused.
            Assert.Equal(("GMG", 409), (setting["GroupPrefix"].AsString, aligned.Status));
            Assert.NotEqual(BsonNull.Value, setting["LockedAtUtc"]);
        }
        else
        {
            // The alignment won: no code under the old prefix, the setting moved unlocked.
            Assert.Equal((409, "GMX"), (allocated.Status, setting["GroupPrefix"].AsString));
            Assert.Equal(BsonNull.Value, setting["LockedAtUtc"]);
        }
    }

    // ── AC-43 — refusals ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC43_an_absent_tenant_is_404_TENANT_NOT_FOUND_recorded_under_the_platform_audit_tenant()
    {
        using var host = await HostAsync();
        var absent = Guid.NewGuid();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(absent), AdminToken));

        Assert.Equal((404, "TENANT_NOT_FOUND"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Equal(0, await host.CountAsync("record_code_group_prefixes"));
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, audit.TenantId);
        Assert.Equal(absent, audit.TargetTenantId);
        AssertRefusal(audit, "TENANT_NOT_FOUND", 404);
    }

    [Fact]
    public async Task AC43_an_empty_route_tenant_is_404_with_no_target_and_the_route_value_in_metadata()
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(Guid.Empty), AdminToken));

        Assert.Equal((404, "TENANT_NOT_FOUND"), (answer.Status, answer.Reason));
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, audit.TenantId);
        Assert.Null(audit.TargetTenantId);
        Assert.Equal(Guid.Empty.ToString("D"), audit.Metadata["RouteTenantId"]);
    }

    [Fact]
    public async Task AC43_AC45_a_body_is_400_in_the_envelope_and_one_Failed_record_in_the_route_tenant()
    {
        using var host = await HostAsync();

        var answer = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(T), AdminToken, "{\"groupPrefix\":\"ABC\"}"));

        Assert.Equal((400, "RECORD_CODE_REQUEST_INVALID"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Null(await SettingAsync(host, T));
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        Assert.Equal((T, (Guid?)T), (audit.TenantId, audit.TargetTenantId));
        Assert.Equal(AuditOperation.Create, audit.Operation);
        AssertRefusal(audit, "RECORD_CODE_REQUEST_INVALID", 400);
    }

    [Theory]
    [InlineData("GM")]      // valid: control below
    [InlineData("gmg")]
    [InlineData("GMG-1")]
    [InlineData("ABCDEF")]
    public async Task AC35_AC43_an_effective_OrgPrefix_that_is_not_a_prefix_is_409_GROUP_PREFIX_SOURCE_INVALID(string orgPrefix)
    {
        using var host = await HostAsync(new RecordCodeHostOptions { OrgPrefix = orgPrefix });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(T), AdminToken));

        if (orgPrefix == "GM")
        {
            Assert.Equal(200, answer.Status);
            return;
        }

        Assert.Equal((409, "GROUP_PREFIX_SOURCE_INVALID"), (answer.Status, answer.Reason));
        Assert.Null(await SettingAsync(host, T));
        var audit = Assert.Single(await host.DeliveredAuditEventsAsync());
        AssertRefusal(audit, "GROUP_PREFIX_SOURCE_INVALID", 409);
        Assert.Equal(orgPrefix, audit.Metadata["EffectiveOrgPrefix"]);
    }

    [Fact]
    public async Task AC43_a_locked_refusal_is_recorded_with_the_locked_and_effective_prefixes()
    {
        using var gmg = await HostAsync();
        using var gmx = new RecordCodeHttpHost(_mongo.Client, gmg.Database, new RecordCodeHostOptions { OrgPrefix = "GMX" });
        await gmg.SeedGroupPrefixAsync(T, "GMG", locked: true);

        await gmx.SendAsync(HttpMethod.Put, Path(T), AdminToken);

        var audit = Assert.Single(await gmg.DeliveredAuditEventsAsync());
        AssertRefusal(audit, "GROUP_PREFIX_LOCKED", 409);
        Assert.Equal(AuditOperation.Update, audit.Operation);
        Assert.Equal(("GMG", "GMX"), ((string)audit.Metadata["LockedGroupPrefix"]!, (string)audit.Metadata["EffectiveOrgPrefix"]!));
    }

    [Fact]
    public async Task AC43_each_refusal_record_is_written_in_its_own_transaction_and_the_business_transaction_writes_nothing()
    {
        var sessions = new SessionRecorder();
        using var gmg = await HostAsync(new RecordCodeHostOptions
        {
            OrgPrefix = "GMX",
            ConfigureServices = services =>
            {
                services.AddScoped<Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter>();
                services.AddScoped<ITransactionalAuditRefusalWriter>(provider =>
                    sessions.Wrap(provider.GetRequiredService<Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter>()));
                services.AddScoped<IRecordCodeGroupPrefixRepository>(provider =>
                    sessions.WrapStore(ActivatorUtilities.CreateInstance<Diten.Platform.Infrastructure.Persistence.Repositories.RecordCodeGroupPrefixRepository>(provider)));
            }
        });
        await gmg.SeedGroupPrefixAsync(T, "GMG", locked: true);

        var answer = await RecordCodeHttpHost.ReadAsync(await gmg.SendAsync(HttpMethod.Put, Path(T), AdminToken));

        Assert.Equal(409, answer.Status);
        var refusalSession = Assert.Single(sessions.RefusalSessions);
        Assert.NotEmpty(sessions.BusinessSessions);
        Assert.DoesNotContain(refusalSession, sessions.BusinessSessions);
        Assert.Equal(0, sessions.BusinessWrites);
    }

    [Fact]
    public async Task AC43_AC47d_a_refusal_whose_record_cannot_be_written_keeps_its_answer()
    {
        using var gmx = await HostAsync(new RecordCodeHostOptions
        {
            OrgPrefix = "GMX",
            ConfigureServices = services => services.AddScoped<ITransactionalAuditRefusalWriter, ThrowingRefusalWriter>()
        });
        await gmx.SeedGroupPrefixAsync(T, "GMG", locked: true);

        var answer = await RecordCodeHttpHost.ReadAsync(await gmx.SendAsync(HttpMethod.Put, Path(T), AdminToken));

        Assert.Equal((409, "GROUP_PREFIX_LOCKED"), (answer.Status, answer.Reason));
    }

    [Fact]
    public async Task AC43_an_alignment_whose_outbox_row_cannot_be_written_leaves_no_setting()
    {
        using var host = await HostAsync(new RecordCodeHostOptions
        {
            ConfigureServices = services => services.AddScoped<ITransactionalAuditOutboxStore, NoOutbox>()
        });

        var answer = await host.SendAsync(HttpMethod.Put, Path(T), AdminToken);

        Assert.True((int)answer.StatusCode >= 500);
        Assert.Null(await SettingAsync(host, T));
    }

    [Fact]
    public async Task AC47a_an_unavailable_alignment_transaction_is_a_503_envelope()
    {
        using var host = await HostAsync(new RecordCodeHostOptions { FaultProbe = new ThreeUnknownCommits() });

        var answer = await RecordCodeHttpHost.ReadAsync(await host.SendAsync(HttpMethod.Put, Path(T), AdminToken));

        Assert.Equal((503, "RECORD_CODE_UNAVAILABLE"), (answer.Status, answer.Reason));
        Assert.True(answer.IsEnvelope);
        Assert.Null(await SettingAsync(host, T));
    }

    // ── AC-35 — one source, three hosts on one database ─────────────────────────────────────────────────────

    [Fact]
    public async Task AC35_after_the_lock_a_changed_OrgPrefix_is_409_mismatch_and_restoring_it_allocates_again()
    {
        using var first = await HostAsync();
        var bank = RecordCodeHttpHost.TenantUserToken(T, Guid.NewGuid(), "mdm.banks.create");
        await first.SendAsync(HttpMethod.Put, Path(T), AdminToken);
        var locked = await RecordCodeHttpHost.ReadAsync(await first.AllocateAsMdmAsync(bank, RecordCodeHttpHost.Body("BNK", "Group", null, "g1")));

        using var second = new RecordCodeHttpHost(_mongo.Client, first.Database, new RecordCodeHostOptions { OrgPrefix = "GMX" });
        var mismatch = await RecordCodeHttpHost.ReadAsync(await second.AllocateAsMdmAsync(bank, RecordCodeHttpHost.Body("BNK", "Group", null, "g2")));

        using var third = new RecordCodeHttpHost(_mongo.Client, first.Database);
        var again = await RecordCodeHttpHost.ReadAsync(await third.AllocateAsMdmAsync(bank, RecordCodeHttpHost.Body("BNK", "Group", null, "g3")));

        Assert.Equal((200, "GMG-BNK-000001"), (locked.Status, locked.Code));
        Assert.Equal((409, "GROUP_PREFIX_MISMATCH"), (mismatch.Status, mismatch.Reason));
        Assert.Equal((200, "GMG-BNK-000002"), (again.Status, again.Code));
        Assert.Equal(0, await first.Collection("record_code_counters").CountDocumentsAsync(new BsonDocument("Prefix", "GMX")));
    }

    // ── AC-44 — administrators ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC44_a_partner_administrator_is_403_on_PUT_and_GET_and_nothing_is_written()
    {
        using var host = await HostAsync(new RecordCodeHostOptions { AdministratorRecord = ActorType.PartnerAdmin });
        var partner = RecordCodeHttpHost.Token("partner_admin", "00000000-0000-0000-0000-000000000001", Guid.NewGuid(),
            "platform.record-code-group-prefixes.update", "platform.record-code-group-prefixes.read");

        var put = await host.SendAsync(HttpMethod.Put, Path(T), partner);
        var get = await host.SendAsync(HttpMethod.Get, Path(T), partner);

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
        Assert.Equal(0, await host.CountAsync("record_code_group_prefixes"));
    }

    [Fact]
    public async Task AC44_a_tenant_user_is_refused_on_the_admin_endpoint()
    {
        using var host = await HostAsync();

        var put = await host.SendAsync(HttpMethod.Put, Path(T),
            RecordCodeHttpHost.TenantUserToken(T, Guid.NewGuid(), "platform.record-code-group-prefixes.update"));

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(0, await host.CountAsync("record_code_group_prefixes"));
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static void AssertRefusal(AuditEvent audit, string reason, int status)
    {
        Assert.Equal(AuditOutcome.Failed, audit.Outcome);
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
        Assert.Equal("AlignRecordCodeGroupPrefixCommand", audit.RequestType);
        Assert.Equal("RecordCodeGroupPrefix", audit.EntityType);
        Assert.Equal("platform.record-code.group-prefix.align-refused", audit.Metadata["EventName"]);
        Assert.Equal(reason, audit.Metadata["ReasonCode"]);
        Assert.Equal(status, Convert.ToInt32(audit.Metadata["HttpStatus"]));
    }

    private static TenantContext Platform()
    {
        var context = new TenantContext();
        context.SetPlatformContext(Guid.Empty);
        return context;
    }

    private sealed class NoOutbox : ITransactionalAuditOutboxStore
    {
        public Task<bool> TryInsertAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, CancellationToken ct = default) =>
            throw new MongoException("outbox lost (test)");
    }

    private sealed class ThreeUnknownCommits : IPlatformTransactionFaultProbe
    {
        public Task BeforeCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) =>
            throw RecordCodeFaults.UnknownCommit();

        public Task AfterCommitAsync(IPlatformTransactionSession session, int commitAttempt, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Records the sessions the business transaction and the refusal writer were given, and any business write.</summary>
    private sealed class SessionRecorder
    {
        public List<Guid> RefusalSessions { get; } = [];
        public List<Guid> BusinessSessions { get; } = [];
        public int BusinessWrites { get; private set; }

        public ITransactionalAuditRefusalWriter Wrap(ITransactionalAuditRefusalWriter inner) => new Refusals(this, inner);
        public IRecordCodeGroupPrefixRepository WrapStore(IRecordCodeGroupPrefixRepository inner) => new Store(this, inner);

        private sealed class Refusals(SessionRecorder recorder, ITransactionalAuditRefusalWriter inner) : ITransactionalAuditRefusalWriter
        {
            public Task<bool> TryEnqueueRefusalAsync(IPlatformTransactionSession session, AuditOutboxWriteRequest request, AuditOutcome outcome, CancellationToken ct = default)
            {
                recorder.RefusalSessions.Add(session.TransactionId);
                return inner.TryEnqueueRefusalAsync(session, request, outcome, ct);
            }
        }

        private sealed class Store(SessionRecorder recorder, IRecordCodeGroupPrefixRepository inner) : IRecordCodeGroupPrefixRepository
        {
            public Task<Diten.Platform.Domain.Entities.RecordCodes.RecordCodeGroupPrefix?> GetAsync(Guid tenantId, CancellationToken ct = default) => inner.GetAsync(tenantId, ct);

            public Task<Diten.Platform.Domain.Entities.RecordCodes.RecordCodeGroupPrefix?> GetAsync(IPlatformTransactionSession session, Guid tenantId, CancellationToken ct = default)
            {
                recorder.BusinessSessions.Add(session.TransactionId);
                return inner.GetAsync(session, tenantId, ct);
            }

            public Task<RecordCodeGroupPrefixAlignment> AlignIfUnlockedAsync(IPlatformTransactionSession session, Guid tenantId, string groupPrefix, string alignedBy, DateTime alignedAtUtc, CancellationToken ct = default)
            {
                recorder.BusinessWrites++;
                return inner.AlignIfUnlockedAsync(session, tenantId, groupPrefix, alignedBy, alignedAtUtc, ct);
            }

            public Task<RecordCodeGroupPrefixLockResult> LockAsync(IPlatformTransactionSession session, Guid tenantId, string prefix, DateTime lockedAtUtc, CancellationToken ct = default)
            {
                recorder.BusinessWrites++;
                return inner.LockAsync(session, tenantId, prefix, lockedAtUtc, ct);
            }
        }
    }
}
