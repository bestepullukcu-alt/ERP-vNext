using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.PvgService.Application.RegPvBase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

// The PVG API host started for real on a random loopback port (never 5011), with the four deny-all adapters replaced by
// allow stubs the way PvgCaseIntakeApiContractTests does. The stubs exist only in test code: the non-production branch
// of PvgServiceApiHost stays deny-all. Keys are generated at run time; every identity value is fake.
internal sealed class PvgMongoApiHost : IAsyncDisposable
{
    public const string Issuer = "pvg-mongo-test-issuer";
    public const string Audience = "pvg-mongo-test-audience";
    public const string CorrelationId = "corr-mongo-fake-1";

    private static readonly string Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public const string CreateJson =
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

    private readonly IWebHost _host;
    private readonly HttpClient _client;

    private PvgMongoApiHost(IWebHost host, HttpClient client, string baseAddress)
    {
        _host = host;
        _client = client;
        BaseAddress = baseAddress;
    }

    public string BaseAddress { get; }

    public IServiceProvider Services => _host.Services;

    public static IConfiguration Configuration(string? store, string? connectionString, string? databaseName)
    {
        var settings = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = Secret,
            ["JwtSettings:Issuer"] = Issuer,
            ["JwtSettings:Audience"] = Audience
        };
        if (store is not null)
        {
            settings[PvgServiceApiHost.PersistenceStoreKey] = store;
        }

        if (connectionString is not null)
        {
            settings["Mongo:ConnectionString"] = connectionString;
        }

        if (databaseName is not null)
        {
            settings["Mongo:DatabaseName"] = databaseName;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    // Builds the host the way the service does, with the allow stubs; does not start it.
    public static IWebHost Build(
        IConfiguration configuration,
        string environmentName = "Development",
        Action<IServiceCollection>? configureServices = null) =>
        PvgServiceApiHost
            .CreatePvgServiceWebHostBuilder(
                environmentName,
                configuration,
                "http://127.0.0.1:0",
                services =>
                {
                    services.Replace(ServiceDescriptor.Singleton<IPvgPermissionGate>(new AllowPermissionGate()));
                    services.Replace(ServiceDescriptor.Singleton<IPvgFieldSecurityPolicy>(new AllowFieldSecurityPolicy()));
                    services.Replace(ServiceDescriptor.Singleton<IPvgWorkflowTransitionGate>(new AllowWorkflowTransitionGate()));
                    services.Replace(ServiceDescriptor.Singleton<IPvgEvidenceLinkPort>(new AllowEvidenceLinkPort()));
                    configureServices?.Invoke(services);
                })
            .Build();

    public static Task<PvgMongoApiHost> StartMongoAsync(string connectionString) =>
        StartAsync(Configuration("Mongo", connectionString, PvgMongoTestHarness.SharedDatabaseName));

    public static async Task<PvgMongoApiHost> StartAsync(
        IConfiguration configuration,
        Action<IServiceCollection>? configureServices = null)
    {
        var host = Build(configuration, "Development", configureServices);
        try
        {
            await host.StartAsync();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        var address = host.ServerFeatures.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(40) };
        return new PvgMongoApiHost(host, client, address);
    }

    public static string Token(string tenant, string subject, string actorType = "tenant_user") =>
        PvgTestIdentity.CreateToken(
            Secret,
            Issuer,
            Audience,
            [new Claim("sub", subject), new Claim("tenant_id", tenant), new Claim("actor_type", actorType)],
            DateTime.UtcNow.AddMinutes(30));

    public async Task<ApiResponse> SendAsync(
        HttpMethod method,
        string path,
        string tenant,
        string subject,
        string? jsonBody = null,
        IEnumerable<(string Name, string Value)>? extraHeaders = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(tenant, subject));
        request.Headers.TryAddWithoutValidation(PvgCaseIntakeRequestContext.TenantHeader, tenant);
        request.Headers.TryAddWithoutValidation(PvgCaseIntakeRequestContext.CorrelationIdHeader, CorrelationId);
        foreach (var (name, value) in extraHeaders ?? [])
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        using var response = await _client.SendAsync(request);
        return new ApiResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public async Task<ApiResponse> GetReadyAsync()
    {
        using var response = await _client.GetAsync("/health/ready");
        return new ApiResponse((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public static string UpdateJson(long expectedVersion, params (string Name, JsonNode? Value)[] extra)
    {
        var body = JsonNode.Parse(CreateJson)!.AsObject();
        body["expectedVersion"] = expectedVersion;
        foreach (var (name, value) in extra)
        {
            body[name] = value;
        }

        return body.ToJsonString();
    }

    public static string CreateJsonWith(params (string Name, JsonNode? Value)[] extra)
    {
        var body = JsonNode.Parse(CreateJson)!.AsObject();
        foreach (var (name, value) in extra)
        {
            body[name] = value;
        }

        return body.ToJsonString();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // The host is going away; a stop that fails changes nothing the tests measure.
        }

        _host.Dispose();
    }

    internal sealed record ApiResponse(int StatusCode, string Raw)
    {
        public PvgCaseIntakeApiResponse Body() =>
            Assert.IsType<PvgCaseIntakeApiResponse>(JsonSerializer.Deserialize<PvgCaseIntakeApiResponse>(Raw, WebOptions));
    }

    private sealed class AllowPermissionGate : IPvgPermissionGate
    {
        public ValueTask<PvgPermissionDecision> EvaluateAsync(
            PvgPermissionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PvgPermissionDecision.Allowed());
    }

    private sealed class AllowFieldSecurityPolicy : IPvgFieldSecurityPolicy
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgFieldSecurityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    private sealed class AllowWorkflowTransitionGate : IPvgWorkflowTransitionGate
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgWorkflowTransitionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    private sealed class AllowEvidenceLinkPort : IPvgEvidenceLinkPort
    {
        public ValueTask<PvgPortDecision> EvaluateAsync(
            PvgEvidenceLinkRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }
}
