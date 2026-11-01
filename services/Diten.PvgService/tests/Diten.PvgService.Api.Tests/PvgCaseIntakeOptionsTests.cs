using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PvgService.Api;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
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

// WP-0230-DL4D (MOD-0230 DL-4 part D): AC-34. GET /options returns the PVG option sets from PvgIntakeOptionSets only,
// behind the validated JWT and the list endpoint's tenant context check, with no tenant data. Every request goes through
// the host's real middleware chain (UseRouting, UseAuthentication, UseAuthorization, endpoints) over a DefaultHttpContext;
// no server is started. Keys are generated at run time and every identity value is fake: nothing secret is committed.
public sealed class PvgCaseIntakeOptionsTests
{
    private const string Issuer = "pvg-test-issuer";
    private const string Audience = "pvg-test-audience";
    private const string CorrelationId = "corr-fake-options";
    private const string ListPath = PvgCaseIntakeTriageEndpoints.RoutePrefix;
    private const string OptionsPath = PvgCaseIntakeTriageEndpoints.RoutePrefix + "/options";

    private static readonly string Secret = NewKey();
    private static readonly string OtherSecret = NewKey();
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    // ---- T1 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Options_returns_exactly_the_option_set_codes_in_ordinal_order()
    {
        using var harness = Harness.Create();

        var response = await harness.SendAsync("GET", OptionsPath, TenantOnlyToken(PvgTestIdentity.TenantA), TenantOnlyHeaders(PvgTestIdentity.TenantA));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        AssertOptionsBody(response.Raw);
    }

    // ---- T2 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Options_endpoint_holds_no_copy_of_the_option_codes()
    {
        var apiRoot = Path.Combine(FindRepositoryRoot(), "services", "Diten.PvgService", "src", "Diten.PvgService.Api");
        var files = new List<string>();
        Walk(new DirectoryInfo(apiRoot), files);

        Assert.Contains(files, file => string.Equals(Path.GetFileName(file), "PvgCaseIntakeTriageEndpoints.cs", StringComparison.Ordinal));

        var literals = AllOptionCodes().Select(code => "\"" + code + "\"").ToArray();
        var matches = files
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                return literals
                    .Where(literal => content.Contains(literal, StringComparison.Ordinal))
                    .Select(literal => $"{Path.GetRelativePath(apiRoot, file)} · {literal}");
            })
            .ToArray();

        Assert.True(matches.Length == 0, "Option code string literals in the Api project: " + string.Join("; ", matches));
    }

    // ---- T3 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("no-bearer")]
    [InlineData("other-key")]
    public async Task Options_without_a_valid_token_is_401(string row)
    {
        using var harness = Harness.Create();
        var token = row == "no-bearer"
            ? null
            : PvgTestIdentity.CreateToken(OtherSecret, Issuer, Audience, TenantOnlyClaims(PvgTestIdentity.TenantA), DateTime.UtcNow.AddMinutes(30));

        var response = await harness.SendAsync("GET", OptionsPath, token, TenantOnlyHeaders(PvgTestIdentity.TenantA));

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        AssertCarriesNoCode(response.Raw);
        Assert.Equal(0, harness.Gate.CallCount);
        Assert.Equal(0, harness.Store.CallCount);
    }

    // ---- T4 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("header-missing")]
    [InlineData("header-differs")]
    public async Task Options_with_a_missing_or_different_tenant_is_refused_without_codes(string row)
    {
        using var harness = Harness.Create();
        string? tenantHeader = row == "header-missing" ? null : PvgTestIdentity.TenantB;
        var expectedReasonCode = row == "header-missing"
            ? PvgValidationReasonCodes.TenantContextRequired
            : PvgCaseIntakeRequestContext.TenantContextMismatch;

        var options = await harness.SendAsync("GET", OptionsPath, TenantOnlyToken(PvgTestIdentity.TenantA), TenantOnlyHeaders(tenantHeader));
        var list = await harness.SendAsync("GET", ListPath, FullContextToken(PvgTestIdentity.TenantA), FullContextHeaders(tenantHeader));

        Assert.NotEqual(StatusCodes.Status200OK, list.StatusCode);
        Assert.Equal(list.StatusCode, options.StatusCode);
        Assert.Equal(expectedReasonCode, list.Body().ReasonCode);
        Assert.Equal(list.Body().ReasonCode, options.Body().ReasonCode);
        AssertCarriesNoCode(options.Raw);
    }

    // ---- T5 -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Options_answer_is_the_same_for_every_tenant_and_reads_no_store()
    {
        using var harness = Harness.Create();
        var seededIds = (await harness.SeedAsync(PvgTestIdentity.TenantA, 2, "narrative-fake-a"))
            .Concat(await harness.SeedAsync(PvgTestIdentity.TenantB, 3, "narrative-fake-b"))
            .ToArray();
        harness.ResetCounters();

        var tenantA = await harness.SendAsync("GET", OptionsPath, TenantOnlyToken(PvgTestIdentity.TenantA), TenantOnlyHeaders(PvgTestIdentity.TenantA));
        var tenantB = await harness.SendAsync("GET", OptionsPath, TenantOnlyToken(PvgTestIdentity.TenantB), TenantOnlyHeaders(PvgTestIdentity.TenantB));

        Assert.Equal(StatusCodes.Status200OK, tenantA.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, tenantB.StatusCode);
        Assert.Equal(Encoding.UTF8.GetBytes(tenantA.Raw), Encoding.UTF8.GetBytes(tenantB.Raw));

        foreach (var forbidden in new[] { PvgTestIdentity.TenantA, PvgTestIdentity.TenantB, "narrative-fake", "pvg-draft-" }.Concat(seededIds))
        {
            Assert.DoesNotContain(forbidden, tenantA.Raw, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0, harness.Store.CallCount);
        Assert.Equal(0, harness.Gate.CallCount);
    }

    // ---- T6 -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("POST-triage")]
    [InlineData("POST-route")]
    public async Task Options_segment_is_reserved_and_never_read_as_an_intake_draft_id(string row)
    {
        using var harness = Harness.Create();
        harness.ResetCounters();

        if (row == "GET")
        {
            var get = await harness.SendAsync("GET", OptionsPath, TenantOnlyToken(PvgTestIdentity.TenantA), TenantOnlyHeaders(PvgTestIdentity.TenantA));

            Assert.Equal(StatusCodes.Status200OK, get.StatusCode);
            AssertOptionsBody(get.Raw);
            Assert.Equal(0, harness.Store.CallCount);
            return;
        }

        var (method, path, body) = row switch
        {
            "PUT" => ("PUT", OptionsPath, ValidUpdateJson),
            "POST-triage" => (
                "POST",
                OptionsPath + "/triage",
                JsonSerializer.Serialize(new PvgCaseIntakeTriageRequest(PvgTriageOutcome.Triaged, null, "triage-reason-fake", 1), WebOptions)),
            _ => (
                "POST",
                OptionsPath + "/route",
                JsonSerializer.Serialize(new PvgCaseIntakeRouteRequest("route-queue-fake", 1), WebOptions))
        };

        // Full actor and correlation context: without the reserved segment the request would reach the service.
        var response = await harness.SendAsync(method, path, FullContextToken(PvgTestIdentity.TenantA), FullContextHeaders(PvgTestIdentity.TenantA), body);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(0, harness.Store.CallCount);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private const string ValidUpdateJson =
        """
        {
          "intakeChannel": "Portal",
          "sourceType": "Reporter",
          "receivedAtUtc": "2020-03-01T08:00:00Z",
          "reporterType": "HealthcareProfessional",
          "adverseEventNarrative": "narrative-fake",
          "seriousness": "Serious",
          "intakePriority": "High",
          "expectedVersion": 1
        }
        """;

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static IEnumerable<string> AllOptionCodes() =>
        PvgIntakeOptionSets.ReportTypes
            .Concat(PvgIntakeOptionSets.ProductRoles)
            .Append(PvgIntakeOptionSets.UnknownCountry);

    private static void AssertOptionsBody(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        Assert.Equal(
            new[] { "productRoles", "reportTypes", "unknownCountry" },
            root.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToArray());

        var reportTypes = root.GetProperty("reportTypes").EnumerateArray().Select(code => code.GetString()).ToArray();
        var productRoles = root.GetProperty("productRoles").EnumerateArray().Select(code => code.GetString()).ToArray();
        var unknownCountry = root.GetProperty("unknownCountry").GetString();

        Assert.Equal(PvgIntakeOptionSets.ReportTypes.Order(StringComparer.Ordinal).ToArray(), reportTypes);
        Assert.Equal(new[] { "Literature", "Other", "Solicited", "Spontaneous", "Study" }, reportTypes);
        Assert.Equal(PvgIntakeOptionSets.ProductRoles.Order(StringComparer.Ordinal).ToArray(), productRoles);
        Assert.Equal(new[] { "Concomitant", "Suspect" }, productRoles);
        Assert.Equal(PvgIntakeOptionSets.UnknownCountry, unknownCountry);
        Assert.Equal("unknown", unknownCountry);
    }

    private static void AssertCarriesNoCode(string raw)
    {
        foreach (var code in AllOptionCodes())
        {
            Assert.DoesNotContain(code, raw, StringComparison.Ordinal);
        }
    }

    // The options request shape: a token with tenant_id only (no sub, no actor_type) and no X-Correlation-Id header.
    private static List<Claim> TenantOnlyClaims(string tenant) =>
        [new Claim(PvgCaseIntakeRequestContext.TenantClaimType, tenant)];

    private static string TenantOnlyToken(string tenant) =>
        PvgTestIdentity.CreateToken(Secret, Issuer, Audience, TenantOnlyClaims(tenant), DateTime.UtcNow.AddMinutes(30));

    // X-Tenant-Id only; omitted when null.
    private static List<(string Name, string Value)> TenantOnlyHeaders(string? tenantHeader) =>
        tenantHeader is null ? [] : [(PvgCaseIntakeRequestContext.TenantHeader, tenantHeader)];

    // The full request context the record endpoints need: subject, actor type, tenant, and a correlation id.
    private static string FullContextToken(string tenant) =>
        PvgTestIdentity.CreateToken(
            Secret,
            Issuer,
            Audience,
            [
                new Claim(PvgCaseIntakeRequestContext.SubjectClaimType, "actor-a"),
                new Claim(PvgCaseIntakeRequestContext.TenantClaimType, tenant),
                new Claim(PvgCaseIntakeRequestContext.ActorKindClaimType, "tenant_user")
            ],
            DateTime.UtcNow.AddMinutes(30));

    private static List<(string Name, string Value)> FullContextHeaders(string? tenantHeader)
    {
        var headers = TenantOnlyHeaders(tenantHeader);
        headers.Add((PvgCaseIntakeRequestContext.CorrelationIdHeader, CorrelationId));
        return headers;
    }

    // The worktree that holds this test source file (four levels above its directory), as the source-text tests do.
    private static string FindRepositoryRoot([CallerFilePath] string testSourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testSourcePath)!, "..", "..", "..", ".."));

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj" };

    private static void Walk(DirectoryInfo directory, List<string> files)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            if (entry is DirectoryInfo subdirectory)
            {
                if (!ExcludedDirectoryNames.Contains(subdirectory.Name))
                {
                    Walk(subdirectory, files);
                }
            }
            else if (entry.Name.EndsWith(".cs", StringComparison.Ordinal))
            {
                files.Add(entry.FullName);
            }
        }
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

    // Counts every call and allows it.
    private sealed class RecordingPermissionGate : IPvgPermissionGate
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public void Reset() => Interlocked.Exchange(ref _callCount, 0);

        public ValueTask<PvgPermissionDecision> EvaluateAsync(
            PvgPermissionRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return ValueTask.FromResult(PvgPermissionDecision.Allowed());
        }
    }

    // Counts every call, then hands it to the host's in-memory store.
    private sealed class RecordingIntakeDraftStore(IPvgIntakeDraftStore inner) : IPvgIntakeDraftStore
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public void Reset() => Interlocked.Exchange(ref _callCount, 0);

        public ValueTask<string> AddAsync(
            PvgPersistenceTenantScope tenantScope,
            SafetyCaseIntake intake,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return inner.AddAsync(tenantScope, intake, cancellationToken);
        }

        public ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(
            PvgPersistenceTenantScope tenantScope,
            string intakeDraftId,
            long? expectedVersion,
            SafetyCaseIntake intake,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return inner.ReplaceAsync(tenantScope, intakeDraftId, expectedVersion, intake, cancellationToken);
        }

        public ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
            PvgPersistenceTenantScope tenantScope,
            string intakeDraftId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return inner.FindByIdAsync(tenantScope, intakeDraftId, cancellationToken);
        }

        public ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
            PvgPersistenceListScope listScope,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return inner.ListAsync(listScope, cancellationToken);
        }

        public ValueTask<int> CountAsync(
            PvgPersistenceTenantScope tenantScope,
            PvgIntakeStatus? status,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return inner.CountAsync(tenantScope, status, cancellationToken);
        }
    }

    // The host built through the setup method, with its own Configure run into a request delegate: every request
    // runs the whole middleware chain over a DefaultHttpContext; no server is started.
    private sealed class Harness : IDisposable
    {
        private readonly IWebHost _host;
        private readonly RequestDelegate _pipeline;

        private Harness(IWebHost host, RecordingPermissionGate gate, RecordingIntakeDraftStore store)
        {
            _host = host;
            Gate = gate;
            Store = store;
            var startup = host.Services.GetRequiredService<IStartup>();
            var app = host.Services.GetRequiredService<IApplicationBuilderFactory>().CreateBuilder(new FeatureCollection());
            startup.Configure(app);
            _pipeline = app.Build();
        }

        public RecordingPermissionGate Gate { get; }

        public RecordingIntakeDraftStore Store { get; }

        public static Harness Create()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Secret"] = Secret,
                    ["JwtSettings:Issuer"] = Issuer,
                    ["JwtSettings:Audience"] = Audience
                })
                .Build();
            var gate = new RecordingPermissionGate();
            var store = new RecordingIntakeDraftStore(new InMemoryPvgIntakeDraftRepository());
            var host = PvgServiceApiHost
                .CreatePvgServiceWebHostBuilder(
                    "Development",
                    configuration,
                    "http://127.0.0.1:0",
                    services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IPvgPermissionGate>(gate));
                        services.Replace(ServiceDescriptor.Singleton<IPvgIntakeDraftStore>(store));
                    })
                .Build();

            return new Harness(host, gate, store);
        }

        public void ResetCounters()
        {
            Gate.Reset();
            Store.Reset();
        }

        public async Task<string[]> SeedAsync(string tenant, int count, string narrative)
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
