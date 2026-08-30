using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Platform.Common.Authorization.S2S;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Infrastructure.GateI;
using Diten.PpmService.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.PpmService.Tests.GateI.DecisionTrace;

public sealed class S2SOutboundLocalEvidenceTests
{
    private static readonly Guid Tenant = Guid.Parse("01170000-0000-4000-8000-000000000001");
    private static readonly Guid Actor = Guid.Parse("01170000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Canonical_binding_is_byte_identical_to_Platform_Common()
    {
        var profile = S2SOutboundReceiverProfiles.DecisionRegistry;
        var body = Encoding.UTF8.GetBytes("{\"raw\":\"bytes-are-not-reserialized\"}");
        var local = S2SOutboundCanonicalRequestBinding.Compute(
            profile.Method, profile.Path, body, Tenant, profile.Operation, [profile.Permission]);

        var http = new DefaultHttpContext().Request;
        http.Method = profile.Method;
        http.Path = profile.Path;
        http.Body = new MemoryStream(body);
        var shared = await S2SCanonicalRequestBinding.ComputeAsync(
            http, Tenant, profile.Operation, [profile.Permission]);

        Assert.Equal(shared, local);
        Assert.Equal(64, local.Length);
        Assert.All(local, character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public async Task Default_unavailable_proof_returns_503_before_owner_call()
    {
        var handler = new CaptureHandler();
        var ports = GateIOwnerReferenceLocalEvidenceTestHost.Create(
            new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:65534") },
            new UnavailableProof());
        var result = await ports.RelationshipAuthority.ValidateAsync(
            Request(GateIRelationshipKind.GoverningDecision), CancellationToken.None);

        Assert.Equal(503, result.StatusCode);
        Assert.False(result.Accepted);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Default_off_composition_has_no_test_proof_issuer_or_trusted_context()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();

        var proof = provider.GetRequiredService<IS2SOutboundProofProvider>();
        var trusted = provider.GetRequiredService<IGateITrustedMutationContextAccessor>();

        Assert.False(proof.IsAvailable);
        Assert.Null(trusted.Current);
    }

    [Fact]
    public async Task Exact_four_profiles_use_only_named_bearer_family_and_raw_body()
    {
        var handler = new CaptureHandler();
        var provider = S2SOutboundLocalEvidenceTestHost.CreateEphemeralProvider(
            new FixedTimeProvider(DateTimeOffset.Parse("2026-08-28T10:00:00Z")));
        var ports = GateIOwnerReferenceLocalEvidenceTestHost.Create(
            new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:65534") }, provider);

        var cases = new[]
        {
            (GateIRelationshipKind.GoverningDecision, S2SOutboundReceiverProfiles.DecisionRegistry),
            (GateIRelationshipKind.SelectedBudgetVersion, S2SOutboundReceiverProfiles.Budgeting),
            (GateIRelationshipKind.ScenarioVersion, S2SOutboundReceiverProfiles.ScenarioPlanning),
            (GateIRelationshipKind.BenefitOutcome, S2SOutboundReceiverProfiles.OutcomeTracking)
        };

        foreach (var (kind, profile) in cases)
        {
            handler.Reset();
            var request = Request(kind);
            var result = await ports.RelationshipAuthority.ValidateAsync(request, CancellationToken.None);
            Assert.True(result.Accepted);
            Assert.Equal(1, handler.CallCount);
            Assert.Equal(profile.Method, handler.Method);
            Assert.Equal(profile.Path, handler.Path);
            Assert.Equal(request.CanonicalWrapperUtf8.ToArray(), handler.Body);
            Assert.Equal("Bearer", handler.Scheme);
            Assert.NotNull(handler.Token);
            Assert.Equal(3, handler.Token!.Split('.').Length);

            using var header = Decode(handler.Token.Split('.')[0]);
            using var payload = Decode(handler.Token.Split('.')[1]);
            Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
            Assert.Equal("diten-delegated-actor-proof+jwt", header.RootElement.GetProperty("typ").GetString());
            Assert.EndsWith(".test-only", header.RootElement.GetProperty("kid").GetString(), StringComparison.Ordinal);
            Assert.Equal(profile.Audience, payload.RootElement.GetProperty("aud").GetString());
            Assert.Equal(profile.ClientId, payload.RootElement.GetProperty("client_id").GetString());
            Assert.Equal(profile.Operation, payload.RootElement.GetProperty("operation_id").GetString());
            Assert.Equal(profile.Permission, payload.RootElement.GetProperty("permission").GetString());
            Assert.Equal("ppm-gate-i-r3-local-evidence-v1", payload.RootElement.GetProperty("test_identity").GetString());
            Assert.NotEqual("HS256", header.RootElement.GetProperty("alg").GetString());
        }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(503)]
    public async Task Every_owner_preserves_closed_terminal_status(int statusCode)
    {
        foreach (var kind in new[]
                 {
                     GateIRelationshipKind.GoverningDecision,
                     GateIRelationshipKind.SelectedBudgetVersion,
                     GateIRelationshipKind.ScenarioVersion,
                     GateIRelationshipKind.BenefitOutcome
                 })
        {
            var handler = new CaptureHandler(statusCode);
            var ports = GateIOwnerReferenceLocalEvidenceTestHost.Create(
                new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:65534") },
                S2SOutboundLocalEvidenceTestHost.CreateEphemeralProvider());

            var result = await ports.RelationshipAuthority.ValidateAsync(Request(kind), default);

            Assert.Equal(statusCode, result.StatusCode);
            Assert.False(result.Accepted);
            Assert.Equal(1, handler.CallCount);
        }
    }

    [Fact]
    public async Task Cancellation_propagates_without_owner_call()
    {
        var handler = new CaptureHandler();
        var provider = S2SOutboundLocalEvidenceTestHost.CreateEphemeralProvider();
        var ports = GateIOwnerReferenceLocalEvidenceTestHost.Create(
            new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:65534") }, provider);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ports.RelationshipAuthority.ValidateAsync(
                Request(GateIRelationshipKind.GoverningDecision), source.Token));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void Receiver_profile_substitution_changes_binding_and_is_terminal()
    {
        var body = Encoding.UTF8.GetBytes("{\"x\":1}");
        var decision = S2SOutboundReceiverProfiles.DecisionRegistry;
        var budgeting = S2SOutboundReceiverProfiles.Budgeting;
        var baseline = S2SOutboundCanonicalRequestBinding.Compute(
            decision.Method, decision.Path, body, Tenant, decision.Operation, [decision.Permission]);
        var substituted = S2SOutboundCanonicalRequestBinding.Compute(
            budgeting.Method, budgeting.Path, body, Tenant, budgeting.Operation, [budgeting.Permission]);
        Assert.NotEqual(baseline, substituted);
        Assert.False(S2SOutboundCanonicalRequestBinding.FixedTimeMatches(baseline, substituted));
    }

    [Fact]
    public void Receiver_table_is_exact_and_collision_free()
    {
        Assert.Equal(4, S2SOutboundReceiverProfiles.All.Count);
        Assert.Equal(4, S2SOutboundReceiverProfiles.All.Select(profile => profile.OwnerModule).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("/internal/v1/decision-registry/decision-references/validate", S2SOutboundReceiverProfiles.DecisionRegistry.Path);
        Assert.Equal("/internal/v1/fpa/budgeting/budget-version-references/validate", S2SOutboundReceiverProfiles.Budgeting.Path);
        Assert.Equal("/internal/v1/fpa/scenario-planning/references/validate", S2SOutboundReceiverProfiles.ScenarioPlanning.Path);
        Assert.Equal("/internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate", S2SOutboundReceiverProfiles.OutcomeTracking.Path);
        Assert.All(S2SOutboundReceiverProfiles.All, profile => Assert.Equal("POST", profile.Method));
    }

    private static GateIAuthorityValidationRequest Request(GateIRelationshipKind kind) => new(
        kind,
        GateIRelationshipAction.AttachOrReplace,
        Trusted(),
        "ppm.gate-i.local-evidence",
        Encoding.UTF8.GetBytes("{\"canonical\":true}"));

    private static GateITrustedMutationContext Trusted()
    {
        return new(
            Tenant, Actor, Guid.Parse("01170000-0000-4000-8000-000000000003"),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "diten.ppm", "diten-auth-service.test-only",
            "diten-ppm-service", "diten-delegated-actor-proof+jwt", "diten.s2s.delegated.invoke",
            "ppm.gate-i.local-evidence", ["ppm.investment-cases.update"], new string('a', 64),
            1, 1, 1);
    }

    private static JsonDocument Decode(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return JsonDocument.Parse(Convert.FromBase64String(padded));
    }

    private sealed class UnavailableProof : IS2SOutboundProofProvider
    {
        public bool IsAvailable => false;
        public ValueTask<S2SOutboundProofResult> IssueAsync(
            S2SOutboundProofRequest request,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Must not be called.");
    }

    private sealed class CaptureHandler(int statusCode = 200) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Scheme { get; private set; }
        public string? Token { get; private set; }
        public byte[] Body { get; private set; } = [];

        public void Reset()
        {
            CallCount = 0;
            Method = Path = Scheme = Token = null;
            Body = [];
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method.Method;
            Path = request.RequestUri!.AbsolutePath;
            Scheme = request.Headers.Authorization?.Scheme;
            Token = request.Headers.Authorization?.Parameter;
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                Content = new ByteArrayContent([])
            };
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
