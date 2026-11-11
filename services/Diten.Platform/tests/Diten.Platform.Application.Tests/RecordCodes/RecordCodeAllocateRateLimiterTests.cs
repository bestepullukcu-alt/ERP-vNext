using Diten.Platform.API.Security;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 AC-33 / AC-44 — the endpoint's own rate limiter, over real HTTP with a low test limit: 429 in the envelope,
/// nothing issued, nothing audited (the named departure, K-10 / K-13), the counter metric instead; one tenant's flood never
/// empties another's bucket; platform-actor refusals have their own <c>platform-actor:</c> bucket and, past it, are not
/// written either.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class RecordCodeAllocateRateLimiterTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid A = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a8");
    private static readonly Guid B = Guid.Parse("40fc0002-0000-4000-8000-00000000b0b8");
    private static readonly Guid Myg = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");
    private const string Consumer = "DITENFINANCETREASURYSERVICE";

    private readonly DisposableMongoReplicaSet _mongo;

    public RecordCodeAllocateRateLimiterTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    private async Task<RecordCodeHttpHost> HostAsync(int permits)
    {
        var host = await RecordCodeHttpHost.StartAsync(_mongo, "diten_record_code_limit",
            new RecordCodeHostOptions { PermitsPerMinute = permits }, A, B);
        host.Mdm.Company(Myg, "MYG");
        return host;
    }

    private static string Pay(Guid tenant) => RecordCodeHttpHost.TenantUserToken(tenant, Guid.NewGuid(), "finance-treasury.payment-batches.create");

    private static double Counted(string tenant, string reason) =>
        RecordCodeAllocateRateLimiter.RateLimitedTotal.WithLabels(Consumer, tenant, reason).Value;

    [Fact]
    public async Task AC33_past_the_limit_a_tenant_gets_429_nothing_is_issued_or_audited_and_the_metric_counts_it()
    {
        using var host = await HostAsync(permits: 2);
        var before = Counted(A.ToString("D"), "rate_limited");

        var first = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(A), RecordCodeHttpHost.Body("PAY", null, Myg, "1")));
        var second = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(A), RecordCodeHttpHost.Body("PAY", null, Myg, "2")));
        var third = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(A), RecordCodeHttpHost.Body("PAY", null, Myg, "3")));
        var other = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(B), RecordCodeHttpHost.Body("PAY", null, Myg, "1")));

        Assert.Equal((200, 200), (first.Status, second.Status));
        Assert.Equal((429, "RECORD_CODE_RATE_LIMITED"), (third.Status, third.Reason));
        Assert.True(third.IsEnvelope);
        Assert.DoesNotContain("\"RATE_LIMITED\"", third.Raw);
        Assert.Equal(200, other.Status);
        Assert.Equal(3, await host.CountAsync("record_code_allocations"));
        Assert.Equal(2L, (await host.Collection("record_code_counters")
            .Find(new BsonDocument("TenantId", new BsonBinaryData(A, GuidRepresentation.Standard))).SingleAsync())["LastNumber"].ToInt64());
        Assert.Equal(3, await host.CountAsync(AuditCollectionNames.AuditOutbox)); // three allocations, no row for the 429
        Assert.Equal(before + 1, Counted(A.ToString("D"), "rate_limited"));
    }

    [Fact]
    public async Task AC44_platform_actor_refusals_have_their_own_bucket_past_it_are_not_written_and_tenant_B_keeps_allocating()
    {
        using var host = await HostAsync(permits: 2);
        var admin = RecordCodeHttpHost.Token("platform_admin", B.ToString(), Guid.NewGuid(), "finance-treasury.payment-batches.create");
        var before = Counted(B.ToString("D"), "platform_actor_refusal");

        var refusals = new List<RecordCodeAnswer>();
        for (var i = 0; i < 4; i++)
        {
            refusals.Add(await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(admin, RecordCodeHttpHost.Body("PAY", null, Myg, $"p{i}"))));
        }

        var tenantB1 = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(B), RecordCodeHttpHost.Body("PAY", null, Myg, "b1")));
        var tenantB2 = await RecordCodeHttpHost.ReadAsync(await host.AllocateAsync(Pay(B), RecordCodeHttpHost.Body("PAY", null, Myg, "b2")));

        Assert.All(refusals, r => Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), (r.Status, r.Reason)));
        Assert.Equal(2, await PlatformRecordsAsync(host));
        Assert.Equal(before + 2, Counted(B.ToString("D"), "platform_actor_refusal"));
        Assert.Equal((200, 200), (tenantB1.Status, tenantB2.Status));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task AC44_S72_past_the_limit_a_platform_token_without_a_real_tenant_is_counted_as_platform(string? tenantClaim)
    {
        using var host = await HostAsync(permits: 1);
        var admin = RecordCodeHttpHost.Token("platform_admin", tenantClaim, Guid.NewGuid());
        var before = Counted("platform", "platform_actor_refusal");

        await host.AllocateAsync(admin, RecordCodeHttpHost.Body("PAY", null, Myg, "p1"));
        await host.AllocateAsync(admin, RecordCodeHttpHost.Body("PAY", null, Myg, "p2"));

        Assert.Equal(1, await PlatformRecordsAsync(host));
        Assert.Equal(before + 1, Counted("platform", "platform_actor_refusal"));
    }

    [Fact]
    public async Task AC44_S72_an_empty_guid_tenant_and_no_tenant_share_one_platform_bucket()
    {
        using var host = await HostAsync(permits: 1);
        var empty = RecordCodeHttpHost.Token("platform_admin", Guid.Empty.ToString(), Guid.NewGuid());
        var none = RecordCodeHttpHost.Token("platform_admin", null, Guid.NewGuid());
        var before = Counted("platform", "platform_actor_refusal");

        await host.AllocateAsync(empty, RecordCodeHttpHost.Body("PAY", null, Myg, "e"));   // takes the platform bucket's one permit
        await host.AllocateAsync(none, RecordCodeHttpHost.Body("PAY", null, Myg, "n"));    // same bucket: over the limit

        Assert.Equal(1, await PlatformRecordsAsync(host));
        Assert.Equal(before + 1, Counted("platform", "platform_actor_refusal"));
    }

    [Fact]
    public void AC33_AC44_the_partitions_are_consumer_and_tenant_and_the_platform_actor_one_is_prefixed()
    {
        Assert.Equal($"{Consumer}:{A:D}", RecordCodeAllocateRateLimiter.TenantPartition(Consumer, A));
        Assert.Equal($"platform-actor:{Consumer}:{A:D}", RecordCodeAllocateRateLimiter.PlatformActorPartition(Consumer, A.ToString()));
        Assert.Equal($"platform-actor:{Consumer}:platform", RecordCodeAllocateRateLimiter.PlatformActorPartition(Consumer, Guid.Empty.ToString()));
        Assert.Equal($"platform-actor:{Consumer}:platform", RecordCodeAllocateRateLimiter.PlatformActorPartition(Consumer, null));
        Assert.Null(RecordCodeAllocateRateLimiter.PlatformActorTargetTenant(Guid.Empty.ToString()));
        Assert.Null(RecordCodeAllocateRateLimiter.PlatformActorTargetTenant("x"));

        using var limiter = new RecordCodeAllocateRateLimiter(1);
        Assert.True(limiter.TryAcquire(RecordCodeAllocateRateLimiter.TenantPartition(Consumer, B)));
        Assert.True(limiter.TryAcquire(RecordCodeAllocateRateLimiter.PlatformActorPartition(Consumer, B.ToString())));
        Assert.False(limiter.TryAcquire(RecordCodeAllocateRateLimiter.TenantPartition(Consumer, B)));
    }

    [Fact]
    public void AC33_the_people_search_limiter_is_not_used_by_the_record_code_endpoint()
    {
        var executor = File.ReadAllText(Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src", "Diten.Platform.API", "Security",
            "TrustedRecordCodeRequestExecutor.cs"));
        Assert.DoesNotContain("PeopleSearchRateLimit", executor);
        Assert.DoesNotContain("EnableRateLimiting", File.ReadAllText(Path.Combine(RepoPaths.Root(), "services", "Diten.Platform", "src",
            "Diten.Platform.API", "Controllers", "Internal", "InternalRecordCodesController.cs")));
    }

    private static Task<long> PlatformRecordsAsync(RecordCodeHttpHost host) =>
        host.Collection(AuditCollectionNames.AuditOutbox).CountDocumentsAsync(
            new BsonDocument("TenantId", new BsonBinaryData(AuditTenantIds.PlatformSystemTenantId, GuidRepresentation.Standard)));
}
