using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.RecordCodes;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 AC-30 / AC-31 — the tenant predicate of the three record-code stores, on a real replica set. Two tenants
/// can produce the same code TEXT (both <c>GMG-BNK-000001</c>), so every test reads tenant B's own counter document and
/// ledger rows, never the code.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class RecordCodeTenantIsolationMongoTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid A = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a5");
    private static readonly Guid B = Guid.Parse("40fc0002-0000-4000-8000-00000000b0b5");
    private static readonly Guid SystemTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly DisposableMongoReplicaSet _mongo;

    public RecordCodeTenantIsolationMongoTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    // ── AC-30 — the predicate refuses three cases and nothing else ─────────────────────────────────────────

    public static TheoryData<string> Violations => new() { "empty-tenant", "platform-context", "other-tenant" };

    [Theory]
    [MemberData(nameof(Violations))]
    public async Task AC30_every_store_refuses_the_three_violations_and_touches_nothing(string violation)
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_isolation");
        var context = new TenantContext();
        var tenant = A;
        switch (violation)
        {
            case "empty-tenant": context.SetTenant(A); tenant = Guid.Empty; break;
            case "platform-context": context.SetPlatformContext(A); break;
            default: context.SetTenant(B); break;
        }

        var db = new PlatformDbContext(_mongo.Client, database);
        var counters = new RecordCodeCounterRepository(db, context);
        var ledger = new RecordCodeAllocationRepository(db, context);
        var prefixes = new RecordCodeGroupPrefixRepository(db, context);
        var executor = new PlatformTransactionExecutor(db);

        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => counters.NextAsync(tenant, "MYG", "PAY"));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => ledger.FindByIdempotencyKeyAsync(tenant, "Diten.MdmService", "k"));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => ledger.FindLatestPrefixForLegalEntityAsync(tenant, Guid.NewGuid()));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => ledger.ExistsPrefixForOtherLegalEntityAsync(tenant, "MYG", Guid.NewGuid()));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => prefixes.GetAsync(tenant));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => executor.ExecuteAsync(async (session, token) =>
        {
            await ledger.InsertAsync(session, tenant, Allocation(tenant == Guid.Empty ? A : tenant), token);
            return true;
        }));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => executor.ExecuteAsync(async (session, token) =>
            await prefixes.AlignIfUnlockedAsync(session, tenant, "GMG", "admin", DateTime.UtcNow, token)));
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() => executor.ExecuteAsync(async (session, token) =>
            await prefixes.LockAsync(session, tenant, "GMG", DateTime.UtcNow, token)));

        foreach (var name in new[] { "record_code_counters", "record_code_allocations", "record_code_group_prefixes" })
        {
            Assert.Equal(0, await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
    }

    [Fact]
    public async Task AC30_AC38_the_platform_system_tenant_is_not_refused()
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_isolation");
        var context = new TenantContext();
        context.SetTenant(SystemTenant);

        var number = await new RecordCodeCounterRepository(new PlatformDbContext(_mongo.Client, database), context).NextAsync(SystemTenant, "MYG", "PAY");

        Assert.Equal(1L, number);
    }

    [Fact]
    public void AC30_no_store_is_a_hybrid_repository_and_no_entity_is_hybrid()
    {
        Assert.All(new[] { typeof(RecordCodeCounterRepository), typeof(RecordCodeAllocationRepository), typeof(RecordCodeGroupPrefixRepository) },
            store => Assert.False(IsGenericSubclass(store, typeof(HybridRepository<>)), store.Name));
        Assert.All(new[] { typeof(RecordCodeCounter), typeof(RecordCodeAllocation), typeof(RecordCodeGroupPrefix) },
            entity => Assert.True(typeof(TenantScopedEntity).IsAssignableFrom(entity), entity.Name));
    }

    // ── AC-31 — each predicate on its own, read through tenant B's rows ─────────────────────────────────────

    [Fact]
    public async Task AC31a_two_tenants_with_the_same_group_prefix_have_two_counter_documents()
    {
        using var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_isolation_http", null, A, B);
        await host.SeedGroupPrefixAsync(A, "GMG");
        await host.SeedGroupPrefixAsync(B, "GMG");

        var a = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(A), RecordCodeHttpHost.Body("BNK", "Group", null, "k")));
        var b = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(B), RecordCodeHttpHost.Body("BNK", "Group", null, "k")));

        Assert.Equal((200, 200), (a.Status, b.Status));
        var bCounter = await host.Collection("record_code_counters").Find(ByTenant(B)).SingleAsync();
        Assert.Equal(1L, bCounter["LastNumber"].ToInt64());
        Assert.Equal(2, await host.CountAsync("record_code_counters"));
    }

    [Fact]
    public async Task AC31b_the_same_key_in_tenant_B_is_B_s_own_ledger_row()
    {
        using var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_isolation_http", null, A, B);
        await host.SeedGroupPrefixAsync(A, "GMG");
        await host.SeedGroupPrefixAsync(B, "GMG");

        await host.AllocateAsMdmAsync(Bank(A), RecordCodeHttpHost.Body("BNK", "Group", null, "shared"));
        await host.AllocateAsMdmAsync(Bank(B), RecordCodeHttpHost.Body("BNK", "Group", null, "shared"));

        var bRow = await host.Collection("record_code_allocations").Find(ByTenant(B)).SingleAsync();
        Assert.Equal(B, bRow["TenantId"].AsGuid);
        Assert.Equal(2, await host.CountAsync("record_code_allocations"));
    }

    [Fact]
    public async Task AC31c_a_tenant_without_a_setting_never_reads_another_tenants_prefix()
    {
        using var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_isolation_http", null, A, B);
        await host.SeedGroupPrefixAsync(A, "GMG");

        var b = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsMdmAsync(Bank(B), RecordCodeHttpHost.Body("BNK", "Group", null, "k")));

        Assert.Equal((409, "GROUP_PREFIX_MISSING"), (b.Status, b.Reason));
        Assert.Equal(0, await host.Collection("record_code_allocations").CountDocumentsAsync(ByTenant(B)));
    }

    [Fact]
    public async Task AC31d_the_ledger_row_carries_the_token_tenant_even_when_the_context_is_changed_under_it()
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_isolation");
        var context = new TenantContext();
        context.SetTenant(A);
        var db = new PlatformDbContext(_mongo.Client, database);
        var ledger = new RecordCodeAllocationRepository(db, context);

        await new PlatformTransactionExecutor(db).ExecuteAsync(async (session, token) =>
        {
            await ledger.InsertAsync(session, A, Allocation(A), token);
            return true;
        });
        context.SetTenant(B);
        await Assert.ThrowsAsync<RecordCodeTenantPredicateViolationException>(() =>
            new PlatformTransactionExecutor(db).ExecuteAsync(async (session, token) =>
            {
                await ledger.InsertAsync(session, B, Allocation(A), token);
                return true;
            }));

        var rows = await database.GetCollection<BsonDocument>("record_code_allocations").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(A, Assert.Single(rows)["TenantId"].AsGuid);
    }

    [Fact]
    public async Task AC31e_an_X_Tenant_Id_header_is_403_and_issues_nothing()
    {
        using var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_isolation_http", null, A, B);
        await host.SeedGroupPrefixAsync(A, "GMG");

        var answer = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Bank(A), RecordCodeHttpHost.Body("BNK", "Group", null, "k"),
            RecordCodeHttpHost.MdmIdentifier, RecordCodeHttpHost.MdmSecret,
            request => request.Headers.TryAddWithoutValidation("X-Tenant-Id", B.ToString())));

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), (answer.Status, answer.Reason));
        Assert.Equal(0, await host.CountAsync("record_code_allocations"));
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private static string Bank(Guid tenant) => RecordCodeHttpHost.TenantUserToken(tenant, Guid.NewGuid(), "mdm.banks.create");

    private static BsonDocument ByTenant(Guid tenant) => new("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard));

    private static RecordCodeAllocation Allocation(Guid tenant) => new()
    {
        TenantId = tenant, CallerService = "Diten.MdmService", IdempotencyKey = Guid.NewGuid().ToString("N"), RecordType = "BNK",
        PrefixScope = RecordCodePrefixScope.Group, Prefix = "GMG", Sequence = 1, Code = "GMG-BNK-" + Random.Shared.Next(100000, 999999),
        OnBehalfOfUserId = Guid.NewGuid(), CorrelationId = "t", AllocatedAtUtc = DateTime.UtcNow
    };

    private static bool IsGenericSubclass(Type type, Type generic)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == generic) return true;
        }

        return false;
    }
}
