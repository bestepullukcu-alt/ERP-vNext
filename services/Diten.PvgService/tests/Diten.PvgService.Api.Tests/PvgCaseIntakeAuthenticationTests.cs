using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PvgService.Api;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diten.PvgService.Api.Tests;

// WP-0230-DL4B (MOD-0230 DL-4 part B): AC-5. The PVG service validates the JWT itself, takes the tenant from the
// Gateway-written X-Tenant-Id (which must equal the token's tenant_id claim) and the actor from the token, and
// ignores X-Diten-* headers. Every request here goes through the host's real middleware chain (UseRouting,
// UseAuthentication, UseAuthorization, endpoints) over a DefaultHttpContext; no server is started.
// Keys are generated at run time and every identity value is fake: nothing secret is committed.
public sealed class PvgCaseIntakeAuthenticationTests
{
    private const string Issuer = "pvg-test-issuer";
    private const string Audience = "pvg-test-audience";
    private const string CorrelationId = "corr-fake-1";
    private const string ListPath = PvgCaseIntakeTriageEndpoints.RoutePrefix;

    private static readonly string Secret = NewKey();
    private static readonly string PreviousSecret = NewKey();
    private static readonly string OtherSecret = NewKey();
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private const string ValidCreateJson =
        """
        {
          "intakeChannel": "Portal",
          "sourceType": "Reporter",
          "sourceReference": "source-ref-fake",
          "receivedAtUtc": "2020-03-01T08:00:00Z",
          "reporterType": "HealthcareProfessional",
          "reporterContactSummary": "contact-fake",
          "patientSubjectCode": "patient-fake-1",
          "eventOnsetDate": "2020-02-01",
          "adverseEventNarrative": "narrative-fake",
          "productLines": [ { "productRole": "Suspect", "productNameAsReported": "product-fake-a", "lotNumber": "lot-1" } ],
          "seriousness": "Serious",
          "intakePriority": "High",
          "awarenessAtUtc": "2020-03-01T08:00:00Z",
          "eventCountry": "unknown",
          "reportType": "Spontaneous",
          "reporterNameOrInitials": "J.D.",
          "reporterProfession": "Pharmacist"
        }
        """;

    // ---- N1 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("GET-list")]
    [InlineData("GET-item")]
    [InlineData("POST-triage")]
    [InlineData("POST-route")]
    public async Task Request_without_bearer_is_rejected_with_401(string endpoint)
    {
        using var harness = Harness.Create();
        var seeded = await harness.SeedAsync(PvgTestIdentity.TenantA, 1);
        var (method, path) = Endpoint(endpoint, seeded[0]);

        var response = await harness.SendAsync(
            method,
            path,
            null,
            Headers(PvgTestIdentity.TenantA, injected: true),
            method is "POST" or "PUT" ? "{}" : null);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Empty(harness.Gate.Calls);
        Assert.Equal(1, await harness.CountAsync(PvgTestIdentity.TenantA));

        var health = await harness.SendAsync("GET", "/health/live", null, []);
        Assert.Equal(StatusCodes.Status200OK, health.StatusCode);
    }

    // ---- N2 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("other-key")]
    [InlineData("expired")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("unsigned")]
    [InlineData("garbage")]
    public async Task Request_with_invalid_bearer_is_rejected_with_401(string kind)
    {
        using var harness = Harness.Create();
        var claims = ValidClaims(PvgTestIdentity.TenantA);
        var future = DateTime.UtcNow.AddMinutes(30);

        var token = kind switch
        {
            "other-key" => PvgTestIdentity.CreateToken(OtherSecret, Issuer, Audience, claims, future),
            "expired" => PvgTestIdentity.CreateToken(Secret, Issuer, Audience, claims, DateTime.UtcNow.AddMinutes(-5)),
            "wrong-issuer" => PvgTestIdentity.CreateToken(Secret, "other-issuer", Audience, claims, future),
            "wrong-audience" => PvgTestIdentity.CreateToken(Secret, Issuer, "other-audience", claims, future),
            "unsigned" => PvgTestIdentity.CreateUnsignedToken(Issuer, Audience, claims, future),
            _ => "not-a-jwt"
        };

        var response = await harness.SendAsync("GET", ListPath, token, Headers(PvgTestIdentity.TenantA));

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Empty(harness.Gate.Calls);
    }

    // ---- N3 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Injected_diten_headers_have_no_effect()
    {
        using var harness = Harness.Create();
        var tenantAIds = await harness.SeedAsync(PvgTestIdentity.TenantA, 2);
        var tenantBIds = await harness.SeedAsync(PvgTestIdentity.TenantB, 3, "tenant-b-secret-narrative");
        var token = Token(ValidClaims(PvgTestIdentity.TenantA, "actor-a", "tenant_user"));

        var list = await harness.SendAsync("GET", ListPath, token, Headers(PvgTestIdentity.TenantA, injected: true));

        Assert.Equal(StatusCodes.Status200OK, list.StatusCode);
        var body = list.Body();
        Assert.Equal(2, body.TotalCount);
        Assert.Equal(
            tenantAIds.Order(StringComparer.Ordinal),
            body.Items.Select(item => item.IntakeDraftId).Order(StringComparer.Ordinal));
        Assert.All(tenantBIds, id => Assert.DoesNotContain(id, list.Raw, StringComparison.Ordinal));

        var seenByGate = Assert.Single(harness.Gate.Calls);
        Assert.Equal(PvgTestIdentity.TenantA, seenByGate.TenantId);
        Assert.Equal("actor-a", seenByGate.ActorId);
        // Measurement (DOĞRULA 11): permission gate saw TenantId = TenantA, ActorId = "actor-a".

        var item = await harness.SendAsync(
            "GET",
            $"{ListPath}/{tenantBIds[0]}",
            token,
            Headers(PvgTestIdentity.TenantA, injected: true));

        Assert.Equal(StatusCodes.Status409Conflict, item.StatusCode);
        Assert.Equal("PVG_INTAKE_DRAFT_NOT_FOUND", item.Body().ReasonCode);
        Assert.DoesNotContain(tenantBIds[0], item.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant-b-secret-narrative", item.Raw, StringComparison.Ordinal);
    }

    // ---- N4 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("claim-mismatch")]
    [InlineData("claim-absent")]
    [InlineData("claim-not-guid")]
    [InlineData("header-missing")]
    [InlineData("header-uppercase")]
    public async Task Tenant_header_and_token_claim_mismatch_is_denied(string row)
    {
        using var harness = Harness.Create();
        await harness.SeedAsync(PvgTestIdentity.TenantA, 1, "tenant-a-secret-narrative");
        await harness.SeedAsync(PvgTestIdentity.TenantB, 1, "tenant-b-secret-narrative");

        var headers = Headers(PvgTestIdentity.TenantA);
        var claims = ValidClaims(PvgTestIdentity.TenantA);
        switch (row)
        {
            case "claim-mismatch":
                headers = Headers(PvgTestIdentity.TenantB);
                break;
            case "claim-absent":
                claims = ValidClaims(null);
                break;
            case "claim-not-guid":
                claims = ValidClaims("tenant-a");
                break;
            case "header-missing":
                headers = Headers(null);
                break;
            case "header-uppercase":
                headers = Headers(PvgTestIdentity.TenantA.ToUpperInvariant());
                break;
        }

        var response = await harness.SendAsync("GET", ListPath, Token(claims), headers);

        switch (row)
        {
            case "header-uppercase":
                Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
                var seenByGate = Assert.Single(harness.Gate.Calls);
                Assert.Equal(PvgTestIdentity.TenantA, seenByGate.TenantId);
                break;
            case "header-missing":
                Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
                Assert.Equal(PvgValidationReasonCodes.TenantContextRequired, response.Body().ReasonCode);
                Assert.Empty(harness.Gate.Calls);
                break;
            default:
                Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
                Assert.Equal(PvgCaseIntakeRequestContext.TenantContextMismatch, response.Body().ReasonCode);
                Assert.Empty(harness.Gate.Calls);
                Assert.DoesNotContain("secret-narrative", response.Raw, StringComparison.Ordinal);
                Assert.Empty(response.Body().Items);
                break;
        }
    }

    // ---- N5 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("no-subject")]
    [InlineData("no-actor-type")]
    [InlineData("both")]
    public async Task Actor_comes_from_token_subject_and_actor_type(string row)
    {
        using var harness = Harness.Create();
        var claims = row switch
        {
            "no-subject" => ValidClaims(PvgTestIdentity.TenantA, null, "tenant_user"),
            "no-actor-type" => ValidClaims(PvgTestIdentity.TenantA, "actor-a", null),
            _ => ValidClaims(PvgTestIdentity.TenantA, "actor-a", "tenant_user")
        };

        var response = await harness.SendAsync(
            "GET",
            ListPath,
            Token(claims),
            Headers(PvgTestIdentity.TenantA, injected: true));

        if (row != "both")
        {
            Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
            Assert.Equal(PvgPermissionReasonCodes.ActorContextRequired, response.Body().ReasonCode);
            Assert.Empty(harness.Gate.Calls);
            return;
        }

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("actor-a", Assert.Single(harness.Gate.Calls).ActorId);

        // The permission request carries no actor kind, so the kind is read from the request context built for
        // the same kind of principal (subject as NameIdentifier, as JwtBearer maps it).
        var context = new DefaultHttpContext();
        PvgTestIdentity.SetTenant(context, PvgTestIdentity.TenantA);
        PvgTestIdentity.SetActor(context, "actor-a", "tenant_user");
        context.Request.Headers[PvgCaseIntakeRequestContext.CorrelationIdHeader] = CorrelationId;
        context.Request.Headers["X-Diten-Actor-Kind"] = "platform_admin";
        var built = PvgCaseIntakeRequestContext.From(context);
        Assert.True(built.IsValid);
        Assert.Equal("actor-a", built.ActorContext!.ActorId);
        Assert.Equal("tenant_user", built.ActorContext.ActorKind);
    }

    // ---- N6 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Valid_token_reaches_the_service_with_server_resolved_context()
    {
        using var harness = Harness.Create();

        var response = await harness.SendAsync(
            "POST",
            ListPath,
            Token(ValidClaims(PvgTestIdentity.TenantA)),
            Headers(PvgTestIdentity.TenantA),
            ValidCreateJson);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("Succeeded", response.Body().Outcome);
        var id = Assert.IsType<string>(response.Body().IntakeDraftId);
        var store = harness.Store;
        Assert.NotNull(await store.FindByIdAsync(new PvgPersistenceTenantScope(PvgTestIdentity.TenantA), id));
        Assert.Null(await store.FindByIdAsync(new PvgPersistenceTenantScope(PvgTestIdentity.TenantB), id));
    }

    // ---- N7 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Token_signed_with_previous_secret_is_accepted()
    {
        using var harness = Harness.Create(previousSecret: PreviousSecret);
        var claims = ValidClaims(PvgTestIdentity.TenantA);
        var future = DateTime.UtcNow.AddMinutes(30);

        var previous = await harness.SendAsync(
            "GET",
            ListPath,
            PvgTestIdentity.CreateToken(PreviousSecret, Issuer, Audience, claims, future),
            Headers(PvgTestIdentity.TenantA));
        Assert.Equal(StatusCodes.Status200OK, previous.StatusCode);

        var current = await harness.SendAsync(
            "GET",
            ListPath,
            PvgTestIdentity.CreateToken(Secret, Issuer, Audience, claims, future),
            Headers(PvgTestIdentity.TenantA));
        Assert.Equal(StatusCodes.Status200OK, current.StatusCode);

        var neither = await harness.SendAsync(
            "GET",
            ListPath,
            PvgTestIdentity.CreateToken(OtherSecret, Issuer, Audience, claims, future),
            Headers(PvgTestIdentity.TenantA));
        Assert.Equal(StatusCodes.Status401Unauthorized, neither.StatusCode);
    }

    // ---- N8 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Missing_jwt_settings_reject_every_request_with_401()
    {
        using var harness = Harness.Create(withJwtSettings: false);

        var response = await harness.SendAsync(
            "GET",
            ListPath,
            Token(ValidClaims(PvgTestIdentity.TenantA)),
            Headers(PvgTestIdentity.TenantA));

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Empty(harness.Gate.Calls);
    }

    // ---- N9 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Expired_token_within_clock_skew_is_accepted()
    {
        using var harness = Harness.Create();
        var token = PvgTestIdentity.CreateToken(
            Secret,
            Issuer,
            Audience,
            ValidClaims(PvgTestIdentity.TenantA),
            DateTime.UtcNow.AddSeconds(-10));

        var response = await harness.SendAsync("GET", ListPath, token, Headers(PvgTestIdentity.TenantA));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static string Token(IEnumerable<Claim> claims) =>
        PvgTestIdentity.CreateToken(Secret, Issuer, Audience, claims, DateTime.UtcNow.AddMinutes(30));

    // A null value leaves the claim out.
    private static List<Claim> ValidClaims(string? tenant, string? subject = "actor-a", string? actorType = "tenant_user")
    {
        var claims = new List<Claim>();
        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        if (tenant is not null)
        {
            claims.Add(new Claim("tenant_id", tenant));
        }

        if (actorType is not null)
        {
            claims.Add(new Claim("actor_type", actorType));
        }

        return claims;
    }

    private static (string Method, string Path) Endpoint(string endpoint, string draftId) =>
        endpoint switch
        {
            "POST" => ("POST", ListPath),
            "PUT" => ("PUT", $"{ListPath}/{draftId}"),
            "GET-list" => ("GET", ListPath),
            "GET-item" => ("GET", $"{ListPath}/{draftId}"),
            "POST-triage" => ("POST", $"{ListPath}/{draftId}/triage"),
            _ => ("POST", $"{ListPath}/{draftId}/route")
        };

    // The Gateway-written X-Tenant-Id (omitted when null) and a correlation id; injected adds the retired headers.
    private static List<(string Name, string Value)> Headers(string? tenantHeader, bool injected = false)
    {
        var headers = new List<(string, string)> { (PvgCaseIntakeRequestContext.CorrelationIdHeader, CorrelationId) };
        if (tenantHeader is not null)
        {
            headers.Add((PvgCaseIntakeRequestContext.TenantHeader, tenantHeader));
        }

        if (injected)
        {
            headers.Add(("X-Diten-Tenant-Context", PvgTestIdentity.TenantB));
            headers.Add(("X-Diten-Actor-Id", "actor-evil"));
            headers.Add(("X-Diten-Actor-Kind", "platform_admin"));
        }

        return headers;
    }

    private sealed record Response(int StatusCode, string Raw)
    {
        public PvgCaseIntakeApiResponse Body() =>
            Assert.IsType<PvgCaseIntakeApiResponse>(
                JsonSerializer.Deserialize<PvgCaseIntakeApiResponse>(Raw, WebOptions));
    }

    // DefaultHttpContext does not report a body; the endpoint's JSON binder asks the server feature whether one exists.
    private sealed class BodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed record GateCall(string? TenantId, string? ActorId);

    private sealed class RecordingPermissionGate : IPvgPermissionGate
    {
        private readonly ConcurrentQueue<GateCall> _calls = new();

        public IReadOnlyCollection<GateCall> Calls => _calls.ToArray();

        public ValueTask<PvgPermissionDecision> EvaluateAsync(
            PvgPermissionRequest request,
            CancellationToken cancellationToken = default)
        {
            _calls.Enqueue(new GateCall(request.TenantId, request.ActorId));
            return ValueTask.FromResult(PvgPermissionDecision.Allowed());
        }
    }

    private sealed class AllowingFieldSecurityPolicy : IPvgFieldSecurityPolicy
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgFieldSecurityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    private sealed class AllowingWorkflowTransitionGate : IPvgWorkflowTransitionGate
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgWorkflowTransitionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    private sealed class AllowingEvidenceLinkPort : IPvgEvidenceLinkPort
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgEvidenceLinkRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    // The host built through the setup method, with its own Configure run into a request delegate: every request
    // runs the whole middleware chain over a DefaultHttpContext; no server is started.
    private sealed class Harness : IDisposable
    {
        private readonly IWebHost _host;
        private readonly RequestDelegate _pipeline;

        private Harness(IWebHost host, RecordingPermissionGate gate)
        {
            _host = host;
            Gate = gate;
            var startup = host.Services.GetRequiredService<IStartup>();
            var app = host.Services.GetRequiredService<IApplicationBuilderFactory>().CreateBuilder(new FeatureCollection());
            startup.Configure(app);
            _pipeline = app.Build();
        }

        public RecordingPermissionGate Gate { get; }

        public IPvgIntakeDraftStore Store => _host.Services.GetRequiredService<IPvgIntakeDraftStore>();

        public static Harness Create(bool withJwtSettings = true, string? previousSecret = null)
        {
            var settings = new Dictionary<string, string?>();
            if (withJwtSettings)
            {
                settings["JwtSettings:Secret"] = Secret;
                settings["JwtSettings:Issuer"] = Issuer;
                settings["JwtSettings:Audience"] = Audience;
                if (previousSecret is not null)
                {
                    settings["JwtSettings:PreviousSecrets:0"] = previousSecret;
                }
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var gate = new RecordingPermissionGate();
            var host = PvgServiceApiHost
                .CreatePvgServiceWebHostBuilder(
                    "Development",
                    configuration,
                    "http://127.0.0.1:0",
                    services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IPvgPermissionGate>(gate));
                        services.Replace(ServiceDescriptor.Singleton<IPvgFieldSecurityPolicy>(new AllowingFieldSecurityPolicy()));
                        services.Replace(ServiceDescriptor.Singleton<IPvgWorkflowTransitionGate>(new AllowingWorkflowTransitionGate()));
                        services.Replace(ServiceDescriptor.Singleton<IPvgEvidenceLinkPort>(new AllowingEvidenceLinkPort()));
                    })
                .Build();

            return new Harness(host, gate);
        }

        public async Task<string[]> SeedAsync(string tenant, int count, string narrative = "narrative-fake")
        {
            var ids = new List<string>();
            for (var index = 0; index < count; index++)
            {
                ids.Add(await Store.AddAsync(
                    new PvgPersistenceTenantScope(tenant),
                    StoredIntake(tenant, narrative, new DateTimeOffset(2020, 3, 1, 8, 0, 0, TimeSpan.Zero).AddMinutes(index))));
            }

            return ids.ToArray();
        }

        public async Task<int> CountAsync(string tenant) =>
            await Store.CountAsync(new PvgPersistenceTenantScope(tenant), null);

        public async Task<Response> SendAsync(
            string method,
            string path,
            string? bearer,
            IEnumerable<(string Name, string Value)> headers,
            string? jsonBody = null)
        {
            using var scope = _host.Services.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Method = method;
            context.Request.Path = path;
            foreach (var (name, value) in headers)
            {
                context.Request.Headers[name] = value;
            }

            if (bearer is not null)
            {
                context.Request.Headers.Authorization = "Bearer " + bearer;
            }

            if (jsonBody is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(jsonBody);
                context.Request.ContentType = "application/json";
                context.Request.ContentLength = bytes.Length;
                context.Request.Body = new MemoryStream(bytes);
                context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature());
            }

            context.Response.Body = new MemoryStream();
            await _pipeline(context);

            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            return new Response(context.Response.StatusCode, await reader.ReadToEndAsync());
        }

        public void Dispose() => _host.Dispose();
    }

    private static SafetyCaseIntake StoredIntake(string tenantId, string narrative, DateTimeOffset receivedAtUtc) =>
        new(
            tenantId,
            PvgIntakeStatus.IntakeCreated,
            "Portal",
            "Reporter",
            receivedAtUtc,
            "HealthcareProfessional",
            narrative,
            "Serious",
            "High")
        {
            SourceReference = "source-ref-fake",
            ReporterContactSummary = "contact-fake",
            PatientSubjectCode = "patient-fake-1",
            EventOnsetDate = new DateOnly(2020, 2, 1),
            EvidenceLinkReferences = [],
            ProductLines = [new PvgIntakeProductLine("Suspect", null, "product-fake-a", null, "lot-1")],
            AwarenessAtUtc = receivedAtUtc,
            EventCountry = "unknown",
            ReportType = "Spontaneous",
            ReporterSource = PvgReporterSource.Manual,
            ReporterSourceId = null,
            ReporterCrmSearchState = PvgReporterCrmSearchState.Pending,
            ReporterNameOrInitials = "J.D.",
            ReporterProfession = "Pharmacist",
            ReporterCountry = null
        };
}
