using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// WP-MVP1-SEC-ESIGN1 item 1 — the ORDER of the signature-request lists, measured on the production
/// <see cref="ESignRequestRepository"/> over a test-owned replica set (never 27017):
/// <list type="bullet">
/// <item><c>ListForSignerAsync</c> ("İmza İsteklerim"): due date ascending, then the NEWER request first on the same due date
/// (<c>SortBy(DueAtUtc).ThenByDescending(RequestedAtUtc)</c>) — the whole sequence is asserted, not just the head;</item>
/// <item><c>ListOverdueAsync</c> (the overdue sweep): only Waiting requests past their due date, due date ascending;</item>
/// <item>another tenant's request never enters either list (the TenantId filter), even for the same signer id.</item>
/// </list>
/// ESignatureHttpMongoTests reads the list but never asserted its order (CT, closing line of MOD-0022).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class ESignatureRequestListOrderMongoTests : IClassFixture<ESignatureRequestListOrderMongoTests.Mongo>
{
    public sealed class Mongo : IAsyncLifetime
    {
        public DisposableMongoReplicaSet ReplicaSet { get; private set; } = null!;

        public async Task InitializeAsync() => ReplicaSet = await DisposableMongoReplicaSet.StartAsync();

        public async Task DisposeAsync() => await ReplicaSet.DisposeAsync();
    }

    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private const string Signer = "user-signer-1";

    private readonly DisposableMongoReplicaSet _mongo;

    public ESignatureRequestListOrderMongoTests(Mongo mongo) => _mongo = mongo.ReplicaSet;

    private static ESignatureRequest Request(Guid tenant, string number, DateTime requestedAtUtc, DateTime dueAtUtc,
        ESignatureRequestStatus status = ESignatureRequestStatus.Waiting, string signer = Signer) => new()
    {
        TenantId = tenant,
        RequestNumber = number,
        Module = "workflow",
        SubjectType = "workflow-task",
        SubjectId = "subject-" + number,
        SubjectDisplay = number,
        MeaningCode = "APPROVE",
        RequestedBy = "user-requester",
        RequestedByName = "Requester",
        RequestedAtUtc = requestedAtUtc,
        SignerUserId = signer,
        DueAtUtc = dueAtUtc,
        IdempotencyKey = "key-" + number + "-" + Guid.NewGuid().ToString("N"),
        Status = status
    };

    private async Task<(ESignRequestRepository Repository, Guid Tenant, Guid Other)> SeedAsync(params Func<Guid, Guid, ESignatureRequest>[] rows)
    {
        var context = new PlatformDbContext(_mongo.Client, _mongo.CreateDatabase());
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        await context.GetCollection<ESignatureRequest>(PlatformCollections.ESignatureRequests)
            .InsertManyAsync(rows.Select(r => r(tenant, other)));
        return (new ESignRequestRepository(context), tenant, other);
    }

    [Fact]
    public async Task The_signer_list_is_due_date_ascending_then_the_newer_request_first_and_stays_in_its_tenant()
    {
        var (repository, tenant, _) = await SeedAsync(
            (t, _) => Request(t, "a", Now.AddHours(-4), Now.AddDays(3)),
            (t, _) => Request(t, "b", Now.AddHours(-3), Now.AddDays(1)),
            (t, _) => Request(t, "c1", Now.AddHours(-2), Now.AddDays(2)),   // the OLDER of the two with the same due date
            (t, _) => Request(t, "c2", Now.AddHours(-1), Now.AddDays(2)),   // the NEWER one
            (_, o) => Request(o, "x-other-tenant", Now.AddHours(-5), Now.AddHours(1))); // same signer, other tenant, earliest due

        var (items, total, waiting) = await repository.ListForSignerAsync(tenant, Signer, new ESignatureRequestQuery(null, null, null, null), 0, 50);

        Assert.Equal(["b", "c2", "c1", "a"], items.Select(i => i.RequestNumber).ToArray());
        Assert.All(items, i => Assert.Equal(tenant, i.TenantId));
        Assert.Equal(4, total);
        Assert.Equal(4, waiting);
    }

    [Fact]
    public async Task The_overdue_list_is_waiting_requests_past_due_ascending_and_stays_in_its_tenant()
    {
        var (repository, tenant, other) = await SeedAsync(
            (t, _) => Request(t, "late-2", Now.AddDays(-5), Now.AddDays(-2)),
            (t, _) => Request(t, "late-3", Now.AddDays(-5), Now.AddDays(-1)),
            (t, _) => Request(t, "late-1", Now.AddDays(-5), Now.AddDays(-3)),
            (t, _) => Request(t, "not-due", Now.AddDays(-5), Now.AddDays(1)),
            (t, _) => Request(t, "late-but-signed", Now.AddDays(-5), Now.AddDays(-4), ESignatureRequestStatus.Signed),
            (_, o) => Request(o, "late-other-tenant", Now.AddDays(-5), Now.AddDays(-6)));

        var overdue = await repository.ListOverdueAsync(tenant, Now, 50);

        Assert.Equal(["late-1", "late-2", "late-3"], overdue.Select(i => i.RequestNumber).ToArray());
        Assert.All(overdue, i => Assert.Equal(tenant, i.TenantId));
        Assert.Equal(["late-other-tenant"], (await repository.ListOverdueAsync(other, Now, 50)).Select(i => i.RequestNumber).ToArray());
    }
}
