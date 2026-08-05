using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.S2S;
using Diten.AuthService.Persistence.Settings;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class S2SProofAcceptanceTransactionTests
{
    [Fact]
    public async Task Success_and_parallel_replay_are_atomic_and_metadata_neutral()
    {
        await using var f = await Fixture.CreateAsync(); var request = f.Request();
        var p0 = await f.PrincipalAsync(); var c0 = await f.CredentialAsync();
        var results = await Task.WhenAll(f.AcceptAsync(request), f.AcceptAsync(request));
        Assert.Equal(1, results.Count(x => x.Kind == S2SProofAcceptanceKind.Accepted));
        Assert.Equal(1, results.Count(x => x.Kind == S2SProofAcceptanceKind.Replay));
        var p1 = await f.PrincipalAsync(); var c1 = await f.CredentialAsync();
        Assert.Equal(1, p1.ProofValidationFence); Assert.Equal(1, c1.ProofValidationFence); Assert.Equal(1, await f.ReceiptCountAsync());
        Assert.Equal((p0.PrincipalVersion, p0.CredentialGeneration, p0.UpdatedAt, p0.UpdatedBy), (p1.PrincipalVersion, p1.CredentialGeneration, p1.UpdatedAt, p1.UpdatedBy));
        Assert.Equal((c0.Generation, c0.UpdatedAt, c0.UpdatedBy), (c1.Generation, c1.UpdatedAt, c1.UpdatedBy));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Same_jti_or_nonce_alternate_token_is_replay_without_fence_residue(bool sameJti)
    {
        await using var f = await Fixture.CreateAsync(); var first = f.Request(); Assert.Equal(S2SProofAcceptanceKind.Accepted, (await f.AcceptAsync(first)).Kind);
        var r = first.ReplayReceipt; var second = first with { ReplayReceipt = new S2SReplayReceipt(r.Issuer,
            sameJti ? r.Jti : Guid.NewGuid().ToString("D"), sameJti ? Guid.NewGuid().ToString("D") : r.Nonce,
            "alternate", r.ExpiresAtUtc, first.AcceptedAtUtc) };
        Assert.Equal(S2SProofAcceptanceKind.Replay, (await f.AcceptAsync(second)).Kind);
        Assert.Equal(1, (await f.PrincipalAsync()).ProofValidationFence); Assert.Equal(1, (await f.CredentialAsync()).ProofValidationFence); Assert.Equal(1, await f.ReceiptCountAsync());
    }

    [Theory]
    [InlineData("principal-suspend")]
    [InlineData("principal-revoke")]
    [InlineData("principal-retire")]
    [InlineData("principal-generation")]
    [InlineData("credential-revoke")]
    [InlineData("credential-expire")]
    [InlineData("credential-generation")]
    public async Task State_mutation_winner_rejects_stale_snapshot_and_rolls_back(string mutation)
    {
        await using var f = await Fixture.CreateAsync(); var request = f.Request(); await f.MutateAsync(mutation);
        Assert.Equal(S2SProofAcceptanceKind.StaleAuthority, (await f.AcceptAsync(request)).Kind);
        Assert.Equal(0, (await f.PrincipalAsync()).ProofValidationFence); Assert.Equal(0, (await f.CredentialAsync()).ProofValidationFence); Assert.Equal(0, await f.ReceiptCountAsync());
    }

    [Fact]
    public async Task Cancellation_has_no_residue()
    {
        await using var f = await Fixture.CreateAsync(); using var c = new CancellationTokenSource(); c.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.AcceptAsync(f.Request(), c.Token));
        Assert.Equal(0, (await f.PrincipalAsync()).ProofValidationFence); Assert.Equal(0, (await f.CredentialAsync()).ProofValidationFence); Assert.Equal(0, await f.ReceiptCountAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly MongoClient _cleanup; private readonly string _name; private readonly S2SMongoContext _context;
        private readonly ServicePrincipal _p; private readonly ServiceCredentialDescriptor _c; private readonly DateTimeOffset _now;
        private Fixture(MongoClient cleanup, string name, S2SMongoContext context, ServicePrincipal p, ServiceCredentialDescriptor c, DateTimeOffset now)
        { _cleanup = cleanup; _name = name; _context = context; _p = p; _c = c; _now = now; }
        public static async Task<Fixture> CreateAsync()
        {
            var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? throw new InvalidOperationException("MONGO_TEST_URI required");
            var name = $"fu16_tx_{Guid.NewGuid():N}"; var context = new S2SMongoContext(new MongoDbSettings { ConnectionString = uri, DatabaseName = name });
            await S2SMongoIndexInitializer.EnsureAsync(context); var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var p = new ServicePrincipal(Guid.NewGuid(), "gate-i-producer", "Gate I", ["MOD-0007"], ["diten-fpa-service"], [DelegatedActorProofV1.ExactScope], now.AddMinutes(-1), now.AddHours(1), "test");
            p.AdvanceCredentialGeneration(1, "test", now); p.TransitionTo(ServicePrincipalStatus.Active, "test", now);
            var c = new ServiceCredentialDescriptor(Guid.NewGuid(), p.ServicePrincipalId, "gate-i-kid", "RS256", 3072, "memory-only", "thumbprint", now.AddMinutes(-1), now.AddHours(1), 1, null, "test"); c.TransitionTo(ServiceCredentialStatus.Active, "test", now);
            Assert.True(await new ServicePrincipalRepository(context).TryCreateAsync(p, CancellationToken.None)); Assert.True(await new ServiceCredentialDescriptorRepository(context).TryCreateAsync(c, CancellationToken.None));
            return new(new MongoClient(uri), name, context, p, c, now);
        }
        public S2SProofAcceptanceRequest Request() => new(_p.ServicePrincipalId, _p.ClientId, _p.PrincipalVersion, _p.NotBeforeUtc, _p.ExpiresAtUtc,
            _c.CredentialId, _c.Generation, _c.Kid, _c.NotBeforeUtc, _c.ExpiresAtUtc, _now,
            new S2SReplayReceipt(DelegatedActorProofV1.ExactIssuer, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), "hash", _now.AddMinutes(5), _now));
        public Task<S2SProofAcceptanceResult> AcceptAsync(S2SProofAcceptanceRequest r, CancellationToken ct = default) => new S2SProofAcceptanceCoordinator(_context).TryAcceptAsync(r, ct);
        public Task<ServicePrincipal> PrincipalAsync() => _context.ServicePrincipals.Find(FilterDefinition<ServicePrincipal>.Empty).SingleAsync();
        public Task<ServiceCredentialDescriptor> CredentialAsync() => _context.ServiceCredentialDescriptors.Find(FilterDefinition<ServiceCredentialDescriptor>.Empty).SingleAsync();
        public Task<long> ReceiptCountAsync() => _context.ReplayReceipts.CountDocumentsAsync(FilterDefinition<S2SReplayReceipt>.Empty);
        public async Task MutateAsync(string mutation)
        {
            if (mutation.StartsWith("principal", StringComparison.Ordinal))
            {
                UpdateDefinition<ServicePrincipal> u = mutation == "principal-generation"
                    ? Builders<ServicePrincipal>.Update.Inc(x => x.CredentialGeneration, 1).Inc(x => x.PrincipalVersion, 1)
                    : Builders<ServicePrincipal>.Update.Set(x => x.Status, mutation == "principal-suspend" ? ServicePrincipalStatus.Suspended : mutation == "principal-revoke" ? ServicePrincipalStatus.Revoked : ServicePrincipalStatus.Retired).Inc(x => x.PrincipalVersion, 1);
                await _context.ServicePrincipals.UpdateOneAsync(FilterDefinition<ServicePrincipal>.Empty, u); return;
            }
            var u2 = mutation == "credential-revoke" ? Builders<ServiceCredentialDescriptor>.Update.Set(x => x.Status, ServiceCredentialStatus.Revoked)
                : mutation == "credential-expire" ? Builders<ServiceCredentialDescriptor>.Update.Set(x => x.Status, ServiceCredentialStatus.Previous)
                : Builders<ServiceCredentialDescriptor>.Update.Set(x => x.Generation, 2);
            await _context.ServiceCredentialDescriptors.UpdateOneAsync(FilterDefinition<ServiceCredentialDescriptor>.Empty, u2);
        }
        public async ValueTask DisposeAsync() => await _cleanup.DropDatabaseAsync(_name);
    }
}
