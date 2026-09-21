using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using System.Net.Http.Json;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Infrastructure.Portfolios;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.Portfolios;

// Calls real Auth via a run-owned TLS bridge and kernel-verified Unix socket with disposable mongod.
// The saved-ticket seam supplies the real fixture-login token; the raw incoming header is poisoned.
// Auth uses its login-settings stub; this does not prove Platform integration. This joins two disposable processes. Auth supplies the real HTTP/JWT account facts; PPM uses its real
// Mongo repository/UoW/CAS chain. The explicit PpmAccessAuthorizer seam supplies both Allowed and Forbidden PPM
// entitlement outcomes so service paths can be exercised; it is never evidence of a production entitlement provider,
// PPM API JWT pipeline, or runtime transport.
[Collection(PpmMongoCollection.CollectionName)]
public sealed class PortfolioAuthProviderIntegrationTests(PpmDisposableMongo mongo) : IAsyncLifetime
{
    private readonly PortfolioAuthProviderProcessHost host = new();
    private PortfolioAuthTransportTestHost bridge = null!;
    public async Task InitializeAsync()
    {
        await host.StartAsync();
        try { bridge = new PortfolioAuthTransportTestHost(host); }
        catch { await host.DisposeAsync(); throw; }
    }
    public async Task DisposeAsync()
    {
        try { await bridge.DisposeAsync(); }
        finally { await host.DisposeAsync(); }
        Assert.True(bridge.CleanupVerified);
        Assert.True(host.CleanupVerified);
        Assert.True(host.OutputContainsNoSecrets);
    }

    [Fact]
    public async Task Real_auth_provider_preserves_named_missing_and_too_long_label_states()
    {
        using var client = await host.ClientAsync();
        using var protectedResponse = await client.GetAsync($"api/users/{host.Subjects["human"]:D}/account-assertion");
        // Report only claim-presence booleans, never the disposable token or claim values.
        var payload = client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
        var issuerPresent = claims.RootElement.TryGetProperty("iss", out var iss) && iss.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(iss.GetString());
        var audiencePresent = claims.RootElement.TryGetProperty("aud", out _);
        Assert.True(issuerPresent && audiencePresent, "The isolated login must issue a complete JWT profile; values withheld.");
        Assert.True(protectedResponse.StatusCode == HttpStatusCode.OK,
            $"Real Auth protected assertion status={(int)protectedResponse.StatusCode}; test JWT issuerPresent={issuerPresent}, audiencePresent={audiencePresent}.");
        var authority = bridge.Authority(bridge.Client(), host.TenantId, host.ActorId("pmo"), client.DefaultRequestHeaders.Authorization!.Parameter);

        var named = await authority.EvaluateAsync(Scope(host.Subjects["human"]), default);
        var missing = await authority.EvaluateAsync(Scope(host.Subjects["unnamed"]), default);
        var tooLong = await authority.EvaluateAsync(Scope(host.Subjects["longName"]), default);

        Assert.Equal(PortfolioAuthorityOutcome.Allowed, named.ActorAuthority.Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Allowed, named.TargetEligibility.Outcome);
        Assert.True(named.Active);
        Assert.True(named.NamedHuman);
        Assert.Equal(PortfolioOwnerLabelState.Available, named.LabelState);
        Assert.Equal(PortfolioOwnerLabelState.Missing, missing.LabelState);
        Assert.Null(missing.DisplayLabel);
        Assert.Equal(PortfolioOwnerLabelState.TooLong, tooLong.LabelState);
        Assert.NotNull(tooLong.DisplayLabel);
        Assert.True(tooLong.DisplayLabel!.Length > 200);

        client.DefaultRequestHeaders.Authorization = null;
        using var unauthenticated = await client.GetAsync($"api/users/{host.Subjects["human"]:D}/account-assertion");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    [Fact]
    public async Task Missing_or_untrusted_adapter_composition_stays_unavailable()
    {
        var authority = bridge.Authority(bridge.Client(), host.TenantId, host.ActorId("pmo"), savedToken: null);

        var result = await authority.EvaluateAsync(Scope(host.Subjects["human"]), default);

        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.ActorAuthority.Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.TargetEligibility.Outcome);
    }

    [Fact]
    public async Task Real_auth_http_and_real_ppm_mongo_preserve_owner_write_effects_and_rejections()
    {
        using var authClient = await host.ClientAsync();
        var world = await CreateWorld(authClient, host.ActorId("pmo"));

        var named = await world.Service.ChangeOwner(new(world.Portfolio.Id, host.Subjects["human"], "Initial assignment",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default);
        Assert.Equal(200, named.StatusCode);
        Assert.Equal(2, named.Data!.Version);
        Assert.Equal(2, world.Authority.EvaluateCalls); // pre-mutation assertion + in-transaction reassertion
        var saved = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        Assert.Single(saved!.OwnerAssignments);
        Assert.Equal(2, saved.Version);
        Assert.Equal(2, await world.AuditCount()); // create + owner mutation

        await AssertLabelRejectionAsync(host.Subjects["unnamed"], "PORTFOLIO_OWNER_LABEL_MISSING");
        await AssertLabelRejectionAsync(host.Subjects["longName"], "PORTFOLIO_OWNER_LABEL_TOO_LONG");

        foreach (var target in new[] { host.Subjects["unknown"], host.Subjects["service"], host.Subjects["passive"] })
        {
            var rejected = await world.Service.ChangeOwner(new(world.Portfolio.Id, target, "Ineligible",
                PortfolioOwnerOperation.Transfer, named.Data.AssignmentId, 2, Guid.NewGuid()), default);
            Assert.Equal(403, rejected.StatusCode);
        }
        foreach (var target in new[] { host.Subjects["foreign"], Guid.NewGuid() })
        {
            var rejected = await world.Service.ChangeOwner(new(world.Portfolio.Id, target, "Invisible",
                PortfolioOwnerOperation.Transfer, named.Data.AssignmentId, 2, Guid.NewGuid()), default);
            Assert.Equal(404, rejected.StatusCode);
        }
        saved = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        Assert.Equal(2, saved!.Version);
        Assert.Single(saved.OwnerAssignments);
        Assert.Equal(2, await world.AuditCount());

        // The Auth fixture itself owns this disposable account-kind transition and its token; no production grant is used.
        using (var kindClient = await host.ClientAsync("kindAdmin"))
            Assert.Equal(HttpStatusCode.OK, (await kindClient.PostAsJsonAsync(
                $"api/users/{host.Subjects["mutable"]:D}/account-kind", new { kind = "Human" })).StatusCode);
        var transfer = new ChangePortfolioOwnerCommand(world.Portfolio.Id, host.Subjects["mutable"], "Handover",
            PortfolioOwnerOperation.Transfer, named.Data.AssignmentId, 2, Guid.NewGuid());
        var moved = await world.Service.ChangeOwner(transfer, default);
        Assert.Equal(200, moved.StatusCode);
        Assert.Equal(moved.Data, (await world.Service.ChangeOwner(transfer, default)).Data); // replay has one business effect
        Assert.Equal(409, (await world.Service.ChangeOwner(transfer with { ExpectedVersion = 2, RequestId = Guid.NewGuid() }, default)).StatusCode);
        saved = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        Assert.Equal(3, saved!.Version);
        Assert.Equal(2, saved.OwnerAssignments.Count);
        Assert.Equal(3, await world.AuditCount());
        Assert.Equal(transfer.RequestId, saved.OwnerAssignments.Last().RequestId);
        var detail = await world.Service.GetById(new(world.Portfolio.Id), default);
        Assert.Equal(200, detail.StatusCode);
        Assert.Null(detail.Data!.OwnerHistory); // Real local policy keeps the distinct history permission closed.

        var unrelated = world.ForActor(Guid.NewGuid());
        Assert.Equal(403, (await unrelated.ChangeOwner(new(world.Portfolio.Id, host.Subjects["human"], "Unrelated",
            PortfolioOwnerOperation.Transfer, moved.Data!.AssignmentId, 3, Guid.NewGuid()), default)).StatusCode);
        var noEntitlement = world.WithAccess(false);
        Assert.Equal(403, (await noEntitlement.ChangeOwner(new(world.Portfolio.Id, host.Subjects["human"], "Denied",
            PortfolioOwnerOperation.Transfer, moved.Data.AssignmentId, 3, Guid.NewGuid()), default)).StatusCode);

        var beforeUnavailable = bridge.Requests;
        bridge.FailureStatus = HttpStatusCode.ServiceUnavailable;
        var unavailable = await world.Service.ChangeOwner(new(world.Portfolio.Id, host.Subjects["human"], "Unavailable",
            PortfolioOwnerOperation.Transfer, moved.Data.AssignmentId, 3, Guid.NewGuid()), default);
        bridge.FailureStatus = null;
        Assert.True(bridge.Requests > beforeUnavailable);
        Assert.Equal(503, unavailable.StatusCode);
        saved = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        Assert.Equal(3, saved!.Version);
        Assert.Equal(2, saved.OwnerAssignments.Count);
        Assert.Equal(3, await world.AuditCount());

        async Task AssertLabelRejectionAsync(Guid targetId, string expectedError)
        {
            var rejected = await world.Service.ChangeOwner(new(world.Portfolio.Id, targetId, "Label precondition",
                PortfolioOwnerOperation.Transfer, named.Data.AssignmentId, 2, Guid.NewGuid()), default);
            Assert.Equal(409, rejected.StatusCode);
            Assert.Contains(expectedError, rejected.Errors);
            var unchanged = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
            Assert.Equal(2, unchanged!.Version);
            Assert.Single(unchanged.OwnerAssignments);
            Assert.Equal(2, await world.AuditCount());
        }
    }

    [Fact]
    public async Task Real_auth_candidates_filter_human_from_unknown_and_service_without_hiding_provider_failures()
    {
        using var authClient = await host.ClientAsync();
        var world = await CreateWorld(authClient, host.ActorId("pmo"));
        var candidates = await world.Service.OwnerCandidates(new(world.Portfolio.Id, "", 20), default);

        Assert.Equal(200, candidates.StatusCode);
        Assert.Contains(candidates.Data!, x => x.UserId == host.Subjects["human"]);
        Assert.DoesNotContain(candidates.Data!, x => x.UserId == host.Subjects["unknown"] || x.UserId == host.Subjects["service"]);
    }

    [Fact]
    public async Task Transaction_reassertion_observes_real_auth_account_change_without_second_business_effect()
    {
        using var auth = await host.ClientAsync();
        using var admin = await host.ClientAsync("kindAdmin");
        var world = await CreateWorld(auth, host.ActorId("pmo"));
        var before = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        var bytes = before!.ToBson();
        world.Authority.BeforeSecond = async () =>
        {
            using var response = await admin.PostAsJsonAsync($"api/users/{host.Subjects["human"]:D}/account-kind", new { kind = "Service" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        };
        var requestId = Guid.NewGuid();
        var result = await world.Service.ChangeOwner(new(world.Portfolio.Id, host.Subjects["human"], "Reassert real account facts",
            PortfolioOwnerOperation.Assign, null, 1, requestId), default);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(2, world.Authority.EvaluateCalls);
        var after = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        Assert.Equal(bytes, after!.ToBson()); // Version, embedded history AND replay receipt all unchanged.
        Assert.DoesNotContain(after!.OwnerAssignments, x => x.RequestId == requestId);
        Assert.Equal(1, await world.AuditCount()); // only the earlier create
    }

    [Fact]
    public async Task Real_auth_permission_denial_and_http_target_guard_preserve_zero_owner_effect()
    {
        using var auth = await host.ClientAsync("noPermission");
        using var denied = await auth.GetAsync($"api/users/{host.Subjects["human"]:D}/account-assertion");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); // Must prove permission denial, not an invalid JWT.
        var world = await CreateWorld(auth, host.ActorId("noPermission"));
        var result = await world.Service.ChangeOwner(new(world.Portfolio.Id, host.Subjects["human"], "Denied Auth consumption",
            PortfolioOwnerOperation.Assign, null, 1, Guid.NewGuid()), default);
        Assert.Equal(503, result.StatusCode); // Auth consumption denial is distinct from PPM actor entitlement.
        var saved = await world.Repository.GetByIdAsync(host.TenantId, world.Portfolio.Id, default);
        Assert.Equal(1, saved!.Version); Assert.Empty(saved.OwnerAssignments); Assert.Equal(1, await world.AuditCount());
        await Assert.ThrowsAsync<IOException>(() => auth.GetAsync("http://127.0.0.1/api/users/lookup"));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name!.StartsWith("Diten.AuthService", StringComparison.Ordinal));
    }

    private async Task<PpmWorld> CreateWorld(HttpClient authClient, Guid actorId)
    {
        var database = PpmMongoTestDatabase.Open(mongo.ReplicaSetConnectionString);
        await new PpmMongoIndexInitializer(database).StartAsync(default);
        var context = new PpmMongoContext(database.Client, database);
        var world = new PpmWorld(host.TenantId, actorId, context, authClient, bridge);
        var created = await world.Service.Create(new($"AUTH-{Guid.NewGuid():N}", "Auth provider portfolio", null, null), default);
        Assert.Equal(201, created.StatusCode);
        world.Portfolio = await world.Repository.GetByIdAsync(host.TenantId, created.Data!.Id, default) ?? throw new InvalidOperationException("Portfolio was not persisted.");
        return world;
    }

    private PortfolioAuthorityScope Scope(Guid targetId)
    {
        var portfolioId = Guid.NewGuid();
        var binding = new PortfolioTemporaryNonProductionRecordAccessAuthority(
            enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction).CreateBinding(portfolioId);
        return new(host.TenantId, host.ActorId("pmo"), portfolioId, "assign-owner", 1, TargetUserId: targetId,
            RecordTenantId: host.TenantId, CreatorId: host.ActorId("pmo"),
            LifecycleState: Diten.PpmService.Domain.Entities.PortfolioLifecycleState.Draft,
            TemporaryNonProductionAccessBinding: binding);
    }

    private sealed class PpmWorld : ITenantContext, ICurrentActorContext, ICorrelationContext
    {
        private readonly PpmMongoContext _context;
        private readonly HttpClient _authClient;
        private readonly bool _accessAllowed;
        private readonly PortfolioAuthTransportTestHost _bridge;
        public PpmWorld(Guid tenantId, Guid actorId, PpmMongoContext context, HttpClient authClient, PortfolioAuthTransportTestHost bridge, bool accessAllowed = true)
        {
            TenantId = tenantId; ActorId = actorId; _context = context; _authClient = authClient; _bridge = bridge; _accessAllowed = accessAllowed;
            Repository = new PortfolioRepository(context); Audit = new AuditIntentRepository(context); Unit = new PpmUnitOfWork(context);
            Authority = CreateAuthority(authClient);
            Service = BuildService();
        }
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Portfolio Portfolio { get; set; } = null!;
        public PortfolioRepository Repository { get; }
        public AuditIntentRepository Audit { get; }
        public PpmUnitOfWork Unit { get; }
        public CountingAuthority Authority { get; }
        public PortfolioService Service { get; }
        public async Task<long> AuditCount() => await _context.AuditIntents.CountDocumentsAsync(x => x.TenantId == TenantId && x.EntityId == Portfolio.Id);
        public PortfolioService ForActor(Guid actorId) => Rebuild(actorId, _authClient, _accessAllowed).Service;
        public PortfolioService WithAccess(bool allowed) => Rebuild(ActorId, _authClient, allowed).Service;
        private PpmWorld Rebuild(Guid actorId, HttpClient client, bool accessAllowed)
        {
            var next = new PpmWorld(TenantId, actorId, _context, client, _bridge, accessAllowed) { Portfolio = Portfolio };
            return next;
        }
        private CountingAuthority CreateAuthority(HttpClient client) => new(_bridge.Authority(_bridge.Client(), TenantId, ActorId, client.DefaultRequestHeaders.Authorization?.Parameter));
        private PortfolioService BuildService() => new(Repository, Audit, Unit, this, this, this, new TestPpmAccess(_accessAllowed),
            recordAuthority: new PortfolioTemporaryNonProductionRecordAccessAuthority(true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction),
            ownerAuthority: Authority);
    }

    private sealed class TestPpmAccess(bool allowed) : IPpmAccessAuthorizer
    {
        // Test-only entitlement boundary: a configured Allowed proves only PortfolioService's downstream behavior.
        public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken ct) =>
            Task.FromResult(allowed ? PpmAccessDecision.Allowed : PpmAccessDecision.Forbidden);
    }

    private sealed class CountingAuthority(IPortfolioOwnerActionAuthority inner) : IPortfolioOwnerActionAuthority
    {
        public int EvaluateCalls { get; private set; }
        public Func<Task>? BeforeSecond { get; set; }
        public Task<PortfolioAuthorityEvidence> CanManageAsync(PortfolioAuthorityScope scope, CancellationToken ct) => inner.CanManageAsync(scope, ct);
        public async Task<PortfolioOwnerEvidence> EvaluateAsync(PortfolioAuthorityScope scope, CancellationToken ct)
        { EvaluateCalls++; if (EvaluateCalls == 2 && BeforeSecond is not null) await BeforeSecond(); return await inner.EvaluateAsync(scope, ct); }
        public Task<PortfolioOwnerCandidatesEvidence> CandidatesAsync(PortfolioAuthorityScope scope, CancellationToken ct) => inner.CandidatesAsync(scope, ct);
    }
}

// This class exercises the PPM supervisor itself. No Auth test assembly is loaded into this process.
[Collection(PpmMongoCollection.CollectionName)]
public sealed class PortfolioAuthProviderSupervisorTests
{
    [Theory]
    [InlineData("pid")]
    [InlineData("uid")]
    [InlineData("start")]
    public async Task Wrong_kernel_peer_identity_is_rejected_before_any_http_or_credential_bytes(string mismatch)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("C3 peer acceptance supports macOS only.");
        var root = Directory.CreateTempSubdirectory("ppm-c3-peer-");
        root.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        var path = Path.Combine(root.FullName, "peer.sock");
        try
        {
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path)); listener.Listen(1);
            using var connected = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await connected.ConnectAsync(new UnixDomainSocketEndPoint(path));
            using var accepted = await listener.AcceptAsync();
            Assert.Throws<IOException>(() => PortfolioAuthProviderProcessHost.VerifyWrongPeerForTest(connected, mismatch));
            connected.Dispose();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Assert.Equal(0, await accepted.ReceiveAsync(new byte[1], SocketFlags.None, deadline.Token));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("api-start")]
    [InlineData("seed")]
    [InlineData("cancel-after-hello")]
    [InlineData("timeout-after-hello")]
    public async Task Failed_start_cancellation_and_timeout_clean_only_run_owned_resources(string mode)
    {
        await using var host = new PortfolioAuthProviderProcessHost();
        var failure = await Record.ExceptionAsync(() => host.StartAsync(failureMode: mode));
        Assert.NotNull(failure);
        Assert.True(host.CleanupVerified);
        Assert.True(host.OutputContainsNoSecrets);
        Assert.False(Directory.Exists(host.Root));
    }

    [Fact]
    public async Task Eof_after_ready_cleans_host_api_mongo_and_run_directory()
    {
        await using var host = new PortfolioAuthProviderProcessHost();
        await host.StartAsync();
        using var client = await host.ClientAsync();
        host.DisconnectControlForTest();
        await host.DisposeAsync();
        Assert.True(host.CleanupVerified);
        Assert.True(host.OutputContainsNoSecrets);
        Assert.Equal(6, host.ExitCode);
        Assert.False(Directory.Exists(host.Root));
    }

    [Fact]
    public async Task Environment_is_reduced_before_the_child_runtime_starts()
    {
        var names = new[] { "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "MongoDbSettings__ConnectionString" };
        var original = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, "C3-parent-poison-must-not-reach-child");
            await using var host = new PortfolioAuthProviderProcessHost();
            await host.StartAsync();
            using var client = await host.ClientAsync();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("health/live")).StatusCode);
            Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name!.StartsWith("Diten.AuthService", StringComparison.Ordinal));
            await host.DisposeAsync(); Assert.True(host.CleanupVerified); Assert.True(host.OutputContainsNoSecrets);
        }
        finally { foreach (var (name, value) in original) Environment.SetEnvironmentVariable(name, value); }
        var env = PortfolioAuthProviderProcessHost.CreateEnvironment("/private/tmp/test-run", "run", "/test/dotnet");
        Assert.Equal(6, env.Count);
        Assert.Equal("/private/tmp/test-run/home", env["HOME"]);
        Assert.Equal("/private/tmp/test-run/tmp", env["TMPDIR"]);
        Assert.DoesNotContain(names, env.ContainsKey);
    }

    [Theory]
    [InlineData("{\"type\":\"hello\",\"runId\":\"run\",\"protocolVersion\":\"1.1\",\"hostPid\":2}")]
    [InlineData("{\"type\":\"hello\",\"runId\":\"other\",\"protocolVersion\":\"1.2\",\"hostPid\":2}")]
    [InlineData("{\"type\":\"hello\",\"runId\":\"run\",\"protocolVersion\":\"1.2\",\"hostPid\":2,\"extra\":true}")]
    [InlineData("{\"type\":\"hello\",\"runId\":\"run\",\"protocolVersion\":\"1.2\",\"hostPid\":2,\"hostPid\":3}")]
    [InlineData("{\"type\":\"bye\",\"runId\":\"run\"}")]
    [InlineData("{\"type\":\"hello\",\"runId\":\"run\",\"protocolVersion\":\"1.2\",\"hostPid\":null}")]
    [InlineData("{\"type\":\"error\",\"runId\":\"run\",\"code\":\"api-start-failed\",\"message\":\"secret-canary\"}")]
    public void Strict_frame_parser_rejects_bad_schema_order_version_or_binding_without_echo(string json)
    {
        var error = Assert.Throws<IOException>(() => PortfolioAuthProviderProcessHost.ParseFrame(Encoding.UTF8.GetBytes(json), "run", "hello"));
        Assert.DoesNotContain("secret-canary", error.ToString());
    }

    [Fact]
    public async Task Frames_reject_invalid_utf8_bom_cr_oversize_partial_eof_and_cancel()
    {
        var valid = Encoding.UTF8.GetBytes("{\"type\":\"hello\",\"runId\":\"run\",\"protocolVersion\":\"1.2\",\"hostPid\":2}");
        using var accepted = PortfolioAuthProviderProcessHost.ParseFrame(valid, "run", "hello");
        foreach (var bytes in new[] { new byte[] { 0xff }, new byte[] { 239, 187, 191 }.Concat(valid).ToArray(), valid.Concat(new byte[] { 13 }).ToArray(), new byte[65537] })
            Assert.Throws<IOException>(() => PortfolioAuthProviderProcessHost.ParseFrame(bytes, "run", "hello"));
        using var eof = new MemoryStream(valid);
        await Assert.ThrowsAsync<IOException>(() => PortfolioAuthProviderProcessHost.ReadFrameAsync(eof, "run", "hello", default));
        using var oversized = new MemoryStream(Enumerable.Repeat((byte)'x', 65537).ToArray());
        await Assert.ThrowsAsync<IOException>(() => PortfolioAuthProviderProcessHost.ReadFrameAsync(oversized, "run", "hello", default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var input = new MemoryStream(valid);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PortfolioAuthProviderProcessHost.ReadFrameAsync(input, "run", "hello", cancelled.Token));
    }
}
