using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using EphemeralMongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 item 12 — the REAL MDM Api in process, over HTTP, against a throwaway mongod.
///
/// <para>WHAT RUNS FOR REAL. Program.cs as shipped: JWT bearer validation with the production settings, the tenant
/// resolution middleware, [HasPermission], the controllers, MediatR and its behaviours, every Mongo repository with its
/// transactions and indexes, the option validation on start, and the recovery runners (called by a test the way the
/// worker calls them — the background workers themselves are removed, so nothing runs on its own schedule).</para>
///
/// <para>WHAT IS REPLACED. Only what lives in another service: the Platform workflow engine
/// (<see cref="FakeWorkflowEngine"/>), Platform's reference data (<see cref="FakeReferenceData"/>), the transport
/// configuration check, and the central audit forwarder. Nothing in production code knows a test runs.</para>
///
/// <para>WHAT IT NEVER TOUCHES. The shared localhost:27017 (the host refuses to start on it and re-checks what the
/// built host took), any real tenant or user, the repository's development JWT secret (a random one is made here, in
/// this process, and the production validation settings are not loosened).</para>
///
/// <para>⚠ The overrides travel as process environment variables: AddPersistence builds the MongoClient while
/// Program.cs registers services, before WebApplicationFactory's configuration hooks apply. They are restored as soon
/// as the host is built. ⚠ One host per test process: AddPersistence registers a global Guid serializer.</para>
/// </summary>
public class MdmHttpHost : IAsyncLifetime
{
    public const string DatabaseName = "diten_mdm_itest_http";
    private const string Issuer = "diten-auth-service";
    private const string Audience = "diten-erp";
    private const int SharedMongoPort = 27017;

    private IMongoRunner? _runner;
    private IMongoRunner? _standalone;
    private readonly object _standaloneGate = new();

    /// <summary>
    /// FIX3 S5: a second throwaway mongod that is NOT a replica set — where a transaction cannot run, as on a
    /// developer's plain mongod. Started on first use, never the shared server, disposed with the host.
    /// </summary>
    public IMongoDatabase StandaloneDatabase
    {
        get
        {
            lock (_standaloneGate)
            {
                _standalone ??= MongoRunner.Run(new MongoRunnerOptions
                {
                    BinaryDirectory = ResolveLocalMongoBinaryDirectory(),
                    UseSingleNodeReplicaSet = false
                });
            }
            if (new MongoUrl(_standalone.ConnectionString).Server.Port == SharedMongoPort)
                throw new InvalidOperationException("The standalone throwaway mongod bound the SHARED port 27017.");
            var settings = MongoClientSettings.FromConnectionString(_standalone.ConnectionString);
#pragma warning disable CS0618 // the host's own Guid representation, as every client of this host uses
            settings.GuidRepresentation = Database.Client.Settings.GuidRepresentation;
#pragma warning restore CS0618
            return new MongoClient(settings).GetDatabase(DatabaseName);
        }
    }
    private WebApplicationFactory<GskusController>? _factory;
    private string? _jwtSecret;

    /// <summary>The approver the three GSKU flows name (CandidatePrincipalIds).</summary>
    public Guid Approver { get; } = Guid.NewGuid();

    public FakeWorkflowEngine Engine { get; } = new();
    // BL-552 flow 2 FIX1 item 15: the services' clock — the real one unless a test moves it on (Shift), so a window is
    // crossed without sleeping through it.
    public ShiftableClock Clock { get; } = new();
    public FakeReferenceData References { get; } = new();
    public RecordingAuditForwarder Audit { get; } = new();
    public FakePlatformReferenceEndpoint PlatformReference { get; } = new();
    // LSKU FIX1: the verified market answers (create and approval time).
    public FakeMarkets Markets { get; } = new();
    // MOD-0290-FU04 S1: the three item lists (MOD-0048-FU01 §4) and the tenant-wide stock answer (O-11).
    public FakeItemReferences ItemReferences { get; } = new();
    public FakeServiceIdentity ServiceIdentity { get; } = new();
    public FakeItemListPlatform ItemListPlatform => _itemListPlatform ??= new(ItemReferences);
    private FakeItemListPlatform? _itemListPlatform;
    public FakeStockAvailability Stock { get; } = new();

    /// <summary>
    /// The trusted legal-entity scope answer (Platform's provider). A tenant a test has not set up gets the answer the
    /// unreachable provider gave before (503), so tenants outside Enforced rollout are unaffected.
    /// </summary>
    public FakeLegalEntityScopes Scopes { get; } = new();
    /// <summary>Every log entry the host writes (level, category, rendered message).</summary>
    public LogSink Logs { get; } = new();

    // LSKU FIX3 item 7: the hosted services the real Program registered (before the host removes them).
    public List<Type> RegisteredHostedServices { get; } = [];
    private Dictionary<string, string?>? _overrides;
    public string MongoConnectionString => _runner?.ConnectionString ?? throw NotStarted();

    public WebApplicationFactory<GskusController> Factory => _factory ?? throw NotStarted();

    public IMongoDatabase Database =>
        Factory.Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);

    /// <summary>MOD-0290-FU04 S2: settings a derived host adds on top of these (none here).</summary>
    protected virtual IReadOnlyDictionary<string, string?> AdditionalSettings { get; } = new Dictionary<string, string?>();

    // S2 FIX1: a derived host's own replacements, after the base ones.
    protected virtual void ConfigureMoreServices(IServiceCollection services) { }

    public async Task InitializeAsync()
    {
        var previous = new Dictionary<string, string?>();
        try
        {
            _runner = MongoRunner.Run(new MongoRunnerOptions
            {
                BinaryDirectory = ResolveLocalMongoBinaryDirectory(),
                UseSingleNodeReplicaSet = true,
                // LSKU FIX4 item 1: failCommand failpoints (configureFailPoint) on this throwaway mongod only.
                AdditionalArguments = "--setParameter enableTestCommands=1"
            });
            if (new MongoUrl(_runner.ConnectionString).Server.Port == SharedMongoPort)
                throw new InvalidOperationException("The throwaway mongod bound the SHARED port 27017. Refusing to run.");
            await new MongoClient(_runner.ConnectionString).DropDatabaseAsync(DatabaseName);

            _jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var overrides = new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Test",
                // The git-ignored Development local file (keys, client secret) is never read by a test host.
                ["DevelopmentLocalSettings__Enabled"] = "false",
                ["Mongo__ConnectionString"] = _runner.ConnectionString,
                ["Mongo__DatabaseName"] = DatabaseName,
                ["JwtSettings__Secret"] = _jwtSecret,
                ["JwtSettings__Issuer"] = Issuer,
                ["JwtSettings__Audience"] = Audience,
                ["PlatformRegistration__BaseUrl"] = "",
                // The real approval-time resolver client, pointed at the in-process Platform answer.
                // S2 FIX1 item 1: https — the item-list clients refuse plain http outside Development (the GSKU clients take both).
                ["VerifiedGskuResolver__PlatformBaseAddress"] = "https://platform.test/",
                ["VerifiedGskuResolver__Timeout"] = "00:00:02",
                ["VerifiedGskuResolver__CredentialIdentifier"] = "http-host-resolver",
                ["VerifiedGskuResolver__CredentialSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                // The three GSKU flows as the tracked development settings name them (B6), with this host's approver.
                ["FirstGskuIdentityWorkflow__Enabled"] = "true",
                ["FirstGskuIdentityWorkflow__TemplateCode"] = "GSKU-IDENTITY",
                ["FirstGskuIdentityWorkflow__ReasonCode"] = "FIRST_GSKU_IDENTITY_APPROVAL",
                ["FirstGskuIdentityWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["FirstGskuIdentityWorkflowWorker__Enabled"] = "true",
                ["FirstGskuIdentityWorkflowWorker__LeaseOwner"] = "mdm-http-test",
                ["FirstGskuIdentityWorkflowWorker__LeaseSeconds"] = "10",
                ["FirstGskuIdentityWorkflowWorker__RetryDelaySeconds"] = "1",
                ["GskuCorrectionWorkflow__Enabled"] = "true",
                ["GskuCorrectionWorkflow__TemplateCode"] = "GSKU-CORRECTION",
                ["GskuCorrectionWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["GskuCorrectionWorkflowWorker__Enabled"] = "true",
                ["GskuCorrectionWorkflowWorker__LeaseSeconds"] = "10",
                ["GskuCorrectionWorkflowWorker__RetryDelaySeconds"] = "1",
                // FIX3 item 1: retries back off (doubling); in this host never longer than 2 s.
                ["GskuCorrectionWorkflowWorker__MaxRetryDelaySeconds"] = "2",
                // FIX3 S8: a start's "nothing arrived" is trusted after 2 s here (the fake engine answers at once).
                ["GskuCorrectionWorkflowWorker__StartSettleSeconds"] = "2",
                ["GskuRetirementRequestWorkflow__Enabled"] = "true",
                ["GskuRetirementRequestWorkflow__TemplateCode"] = "GSKU-RETIREMENT-REQUEST",
                ["GskuRetirementRequestWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["GskuRetirementRequestWorkflowWorker__Enabled"] = "true",
                ["GskuRetirementRequestWorkflowWorker__LeaseSeconds"] = "10",
                ["GskuRetirementRequestWorkflowWorker__RetryDelaySeconds"] = "1",
                // FIX3 item 1: retries back off (doubling); in this host never longer than 2 s.
                ["GskuRetirementRequestWorkflowWorker__MaxRetryDelaySeconds"] = "2",
                // FIX3 S8: a start's "nothing arrived" is trusted after 2 s here (the fake engine answers at once).
                ["GskuRetirementRequestWorkflowWorker__StartSettleSeconds"] = "2",
                // FIX3 item 1: a low retry limit for the retirement request, so a test can reach it in a few cycles.
                ["GskuRetirementRequestWorkflowWorker__QuarantineAfterFailedAttempts"] = "4",
                // BL-552 FIX1 items 4 and 7: the Global Product correction over HTTP and its runner. The hosted worker
                // polls once an hour (its cycles are run by the tests themselves, through RunAsync).
                ["GlobalProductCorrectionWorkflow__Enabled"] = "true",
                ["GlobalProductCorrectionWorkflow__TemplateCode"] = "GP-CORRECTION",
                ["GlobalProductCorrectionWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["GlobalProductCorrectionWorkflowWorker__Enabled"] = "true",
                ["GlobalProductCorrectionWorkflowWorker__PollIntervalSeconds"] = "3600",
                ["GlobalProductCorrectionWorkflowWorker__LeaseSeconds"] = "10",
                ["GlobalProductCorrectionWorkflowWorker__RetryDelaySeconds"] = "1",
                ["GlobalProductCorrectionWorkflowWorker__MaxRetryDelaySeconds"] = "2",
                ["GlobalProductCorrectionWorkflowWorker__StartSettleSeconds"] = "2",
                ["GlobalProductCorrectionWorkflowWorker__QuarantineAfterFailedAttempts"] = "4",
                // BL-552 flow 2: the Global Product retirement request over HTTP (flow 2 FIX1 item 2) and its runner,
                // driven by the tests the same way.
                ["GlobalProductRetirementRequestWorkflow__Enabled"] = "true",
                ["GlobalProductRetirementRequestWorkflow__TemplateCode"] = "GP-RETIREMENT",
                ["GlobalProductRetirementRequestWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["GlobalProductRetirementRequestWorkflowWorker__Enabled"] = "true",
                ["GlobalProductRetirementRequestWorkflowWorker__PollIntervalSeconds"] = "3600",
                ["GlobalProductRetirementRequestWorkflowWorker__LeaseSeconds"] = "10",
                ["GlobalProductRetirementRequestWorkflowWorker__RetryDelaySeconds"] = "1",
                ["GlobalProductRetirementRequestWorkflowWorker__MaxRetryDelaySeconds"] = "2",
                ["GlobalProductRetirementRequestWorkflowWorker__StartSettleSeconds"] = "2",
                ["GlobalProductRetirementRequestWorkflowWorker__QuarantineAfterFailedAttempts"] = "4",
                // BL-552 flow 3: the Global Product identity approval over HTTP and its runner (driven by the tests).
                ["ProductIdentityWorkflow__Enabled"] = "true",
                ["ProductIdentityWorkflow__GlobalProductTemplateCode"] = "GP-IDENTITY",
                ["ProductIdentityWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["ProductIdentityWorkflow__ReasonCode"] = "GLOBAL_PRODUCT_IDENTITY_APPROVAL",
                ["ProductIdentityWorkflowWorker__Enabled"] = "true",
                ["ProductIdentityWorkflowWorker__LeaseOwner"] = "mdm-http-test-gp-identity",
                ["ProductIdentityWorkflowWorker__PollIntervalSeconds"] = "3600",
                ["ProductIdentityWorkflowWorker__LeaseSeconds"] = "10",
                ["ProductIdentityWorkflowWorker__RetryDelaySeconds"] = "1",
                ["ProductIdentityWorkflowWorker__ServiceFaultMaxRetryDelaySeconds"] = "2",
                ["ProductIdentityWorkflowWorker__StartSettleSeconds"] = "2",
                ["ProductIdentityWorkflowWorker__QuarantineAfterFailedAttempts"] = "4",
                // LSKU FIX1: the two LSKU flows as the tracked development settings name them (B4), this host's approver,
                // the same short schedules as the GSKU flows above.
                ["LskuIdentityWorkflow__Enabled"] = "true",
                ["LskuIdentityWorkflow__TemplateCode"] = "LSKU-IDENTITY",
                ["LskuIdentityWorkflow__ReasonCode"] = "LSKU_IDENTITY_APPROVAL",
                ["LskuIdentityWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["LskuIdentityWorkflowWorker__Enabled"] = "true",
                ["LskuIdentityWorkflowWorker__LeaseOwner"] = "mdm-http-test-lsku",
                ["LskuIdentityWorkflowWorker__LeaseSeconds"] = "10",
                ["LskuIdentityWorkflowWorker__RetryDelaySeconds"] = "1",
                // LSKU FIX2 item 10: "nothing arrived" is trusted after 2 s here.
                ["LskuIdentityWorkflowWorker__StartSettleSeconds"] = "2",
                ["LskuRetirementRequestWorkflow__Enabled"] = "true",
                ["LskuRetirementRequestWorkflow__TemplateCode"] = "LSKU-RETIREMENT-REQUEST",
                ["LskuRetirementRequestWorkflow__CandidatePrincipalIds__0"] = Approver.ToString("D"),
                ["LskuRetirementRequestWorkflowWorker__Enabled"] = "true",
                ["LskuRetirementRequestWorkflowWorker__LeaseSeconds"] = "10",
                ["LskuRetirementRequestWorkflowWorker__RetryDelaySeconds"] = "1",
                ["LskuRetirementRequestWorkflowWorker__MaxRetryDelaySeconds"] = "2",
                ["LskuRetirementRequestWorkflowWorker__StartSettleSeconds"] = "2",
                ["LskuRetirementRequestWorkflowWorker__QuarantineAfterFailedAttempts"] = "4"
            };
            // MOD-0290-FU04 S2: a derived host adds its own settings (ItemActivationHttpHost switches the item approval on).
            foreach (var (key, value) in AdditionalSettings) overrides[key] = value;
            _overrides = overrides;
            foreach (var (key, value) in overrides)
            {
                previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            _factory = new WebApplicationFactory<GskusController>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");
                builder.ConfigureTestServices(services =>
                {
                    // No background schedule (a test calls the runners) and no outbound push to Platform.
                    // LSKU FIX3 item 7: what the REAL Program registered as hosted services is kept for the tests
                    // (a worker whose registration is lost is a gauge nobody runs).
                    foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                    {
                        if (hosted.ImplementationType is { } type) RegisteredHostedServices.Add(type);
                        services.Remove(hosted);
                    }
                    services.RemoveAll<IProductIdentityWorkflowClient>();
                    services.AddSingleton<IProductIdentityWorkflowClient>(Engine);
                    services.RemoveAll<IVerifiedGskuReferenceResolver>();
                    services.AddSingleton<IVerifiedGskuReferenceResolver>(References);
                    // The approval-time reference check stays the REAL client; only Platform's answer is faked.
                    services.RemoveAll<IWorkflowVerifiedMarketServiceIdentityProvider>();
                    services.AddSingleton<IWorkflowVerifiedMarketServiceIdentityProvider>(ServiceIdentity);
                    services.AddHttpClient(nameof(Diten.MdmService.Infrastructure.ReferenceData.PlatformWorkflowVerifiedGskuResolverClient))
                        .ConfigurePrimaryHttpMessageHandler(() => PlatformReference);
                    services.RemoveAll<IProductIdentityWorkflowTransportReadiness>();
                    services.AddSingleton<IProductIdentityWorkflowTransportReadiness, ReadyTransport>();
                    services.RemoveAll<IPlatformAuditForwarder>();
                    services.AddSingleton<IPlatformAuditForwarder>(Audit);
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(Clock);
                    // LSKU FIX1: Platform's market reference answers (the resolvers are clients of that service).
                    services.RemoveAll<IVerifiedMarketReferenceResolver>();
                    services.AddSingleton<IVerifiedMarketReferenceResolver>(Markets);
                    services.RemoveAll<IWorkflowVerifiedMarketReferenceResolver>();
                    services.AddSingleton<IWorkflowVerifiedMarketReferenceResolver>(Markets);
                    // FG FIX1 review R2 / item 8: the enforced-scope answer for the tenants a test sets up.
                    services.RemoveAll<Diten.MdmService.Application.Contracts.Authorization.ITrustedLegalEntityScopeProvider>();
                    services.AddSingleton<Diten.MdmService.Application.Contracts.Authorization.ITrustedLegalEntityScopeProvider>(Scopes);
                    // LSKU FIX4 item 6: what the host logs, for "said once" assertions.
                    services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(Logs);
                    // MOD-0290-FU04 S1: the item lists and the stock answer (production: both fail closed).
                    services.RemoveAll<IItemReferenceResolver>();
                    services.AddSingleton<IItemReferenceResolver>(ItemReferences);
                    // S2 FIX1 item 1: the approval worker's item lists go through the REAL service-identity client; only
                    // Platform's endpoint is faked (it answers from ItemReferences and checks the service token).
                    services.AddHttpClient(nameof(Diten.MdmService.Infrastructure.ReferenceData.PlatformWorkflowItemReferenceResolverClient))
                        .ConfigurePrimaryHttpMessageHandler(() => ItemListPlatform);
                    ConfigureMoreServices(services);
                    services.RemoveAll<Diten.MdmService.Application.Contracts.Inventory.IInventoryStockAvailabilityClient>();
                    services.AddSingleton<Diten.MdmService.Application.Contracts.Inventory.IInventoryStockAvailabilityClient>(Stock);
                });
            });
            _ = _factory.Server;

            // Second line of defence: the built host really took the throwaway server and database.
            var port = _factory.Services.GetRequiredService<IMongoClient>().Settings.Servers.Single().Port;
            if (port != new MongoUrl(_runner.ConnectionString).Server.Port || port == SharedMongoPort)
                throw new InvalidOperationException($"The host did not take the throwaway Mongo (port {port}). Refusing to run.");
            using var scope = _factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName;
            if (database != DatabaseName)
                throw new InvalidOperationException($"The host did not take the throwaway database ({database}). Refusing to run.");
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
        finally
        {
            foreach (var (key, value) in previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            try { await _factory.DisposeAsync(); }
            catch (Exception exception) { Console.Error.WriteLine($"[MdmHttpHost] factory disposal failed: {exception}"); }
            _factory = null;
        }
        if (_standalone is not null)
        {
            try { _standalone.Dispose(); }
            catch (Exception exception) { Console.Error.WriteLine($"[MdmHttpHost] standalone disposal failed: {exception}"); }
            _standalone = null;
        }
        if (_runner is not null)
        {
            try { _runner.Dispose(); }
            catch (Exception exception) { Console.Error.WriteLine($"[MdmHttpHost] runner disposal failed: {exception}"); }
            _runner = null;
        }
    }

    /// <summary>
    /// WP-MDM-BL598-01 — a second host built like this one (same settings, same fakes, same token key) with some services
    /// replaced (e.g. a Mongo client that cannot reach its server). The caller disposes it.
    /// </summary>
    public WebApplicationFactory<GskusController> Variant(Action<IServiceCollection> replace)
    {
        var overrides = _overrides ?? throw NotStarted();
        var previous = new Dictionary<string, string?>();
        foreach (var (key, value) in overrides)
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
        try
        {
            var variant = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(replace));
            _ = variant.Server;
            return variant;
        }
        finally
        {
            foreach (var (key, value) in previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>A tenant user's token as AuthService writes it (sub, actor_type, tenant_id, one claim per permission).</summary>
    public string Token(Guid tenantId, Guid userId, params string[] permissions) =>
        TokenOf("tenant_user", tenantId, userId, permissions);

    /// <summary>A token of any actor type (a service, a system actor) — for the doors that must refuse them too.</summary>
    public string TokenOf(string actorType, Guid tenantId, Guid userId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("actor_type", actorType),
            new("tenant_id", tenantId.ToString("D"))
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret ?? throw NotStarted()));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }

    public HttpClient Client(string? token)
    {
        var client = Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>One cycle of a recovery runner, exactly as its worker would call it.</summary>
    public Task RunAsync<TRunner>(Func<TRunner, Task> cycle) where TRunner : notnull =>
        cycle(Factory.Services.GetRequiredService<TRunner>());

    private static InvalidOperationException NotStarted() => new("MdmHttpHost has not been started.");

    private static string ResolveLocalMongoBinaryDirectory()
    {
        var executable = OperatingSystem.IsWindows() ? "mongod.exe" : "mongod";
        foreach (var variable in new[] { "DITEN_TEST_MONGOD", "DITEN_ITEST_MONGOD_BIN_DIR" })
        {
            var configured = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(configured)) continue;
            var directory = File.Exists(configured) ? Path.GetDirectoryName(configured)! : configured;
            if (File.Exists(Path.Combine(directory, executable))) return directory;
        }
        foreach (var candidate in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Concat(["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin"]))
        {
            if (File.Exists(Path.Combine(candidate, executable))) return candidate;
        }
        throw new InvalidOperationException(
            "The MDM HTTP tests start their OWN throwaway mongod (never the shared 27017) and need a local mongod binary: "
            + "set DITEN_TEST_MONGOD or install MongoDB.");
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MdmHttpCollection : ICollectionFixture<MdmHttpHost>
{
    public const string Name = "MDM HTTP host";
}

internal static class Envelope
{
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static string? FirstError(JsonElement body) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty("errors", out var errors)
        && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? errors[0].GetString()
            : null;

    public static JsonContent Json(object value) => JsonContent.Create(value);
}

/// <summary>The real clock plus an offset a test sets for its own duration (Shift returns the reset).</summary>
public sealed class ShiftableClock : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow().AddTicks(Interlocked.Read(ref _offsetTicks));

    public IDisposable Shift(TimeSpan by)
    {
        Interlocked.Add(ref _offsetTicks, by.Ticks);
        return new Reset(this, by.Ticks);
    }

    private sealed class Reset(ShiftableClock clock, long ticks) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) Interlocked.Add(ref clock._offsetTicks, -ticks);
        }
    }
}

/// <summary>Per tenant: the subject's legal entities for any product permission (canonical order), or unavailable.</summary>
public sealed class FakeLegalEntityScopes : Diten.MdmService.Application.Contracts.Authorization.ITrustedLegalEntityScopeProvider
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Guid[]> _tenants = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (int Status, string Code)> _refusals = new();

    public void Answer(Guid tenantId, params Guid[] legalEntityIds) =>
        _tenants[tenantId] = legalEntityIds.OrderBy(x => x.ToString("D"), StringComparer.Ordinal).ToArray();

    /// <summary>MOD-0290-FU04 S4 FIX1: the provider refuses this tenant with a status of its own (401 / 403 / 504 …).</summary>
    public void Refuse(Guid tenantId, int status, string code) => _refusals[tenantId] = (status, code);

    // ⚠ S4 FIX1 B2 (BL-592): this fake answers EVERY permission key, mdm.product-master.read included. The real
    // provider (Platform's trusted scope credential) has a fixed list of keys that does not hold it yet, so in an
    // Enforced tenant the real answer is a refusal. No test here can see that; it is a go-live prerequisite.
    public Task<Diten.MdmService.Application.Contracts.Authorization.TrustedLegalEntityScopeProviderResult> ResolveAsync(
        Guid expectedTenantId, Guid expectedSubjectId, string moduleCode, string permissionKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_refusals.TryGetValue(expectedTenantId, out var refusal)
            ? new Diten.MdmService.Application.Contracts.Authorization.TrustedLegalEntityScopeProviderResult(
                false, refusal.Status, refusal.Code, expectedTenantId, expectedSubjectId, moduleCode, permissionKey, null, [])
            : _tenants.TryGetValue(expectedTenantId, out var ids)
            ? Diten.MdmService.Application.Contracts.Authorization.TrustedLegalEntityScopeProviderResult.Success(
                expectedTenantId, expectedSubjectId, moduleCode, permissionKey, DateTimeOffset.UtcNow, ids)
            : new Diten.MdmService.Application.Contracts.Authorization.TrustedLegalEntityScopeProviderResult(
                false, 503, "LEGAL_ENTITY_SCOPE_PROVIDER_UNAVAILABLE", expectedTenantId, expectedSubjectId,
                moduleCode, permissionKey, null, []));
}

public sealed class LogSink : Microsoft.Extensions.Logging.ILoggerProvider
{
    public System.Collections.Concurrent.ConcurrentQueue<(Microsoft.Extensions.Logging.LogLevel Level, string Category, string Message)> Entries { get; } = new();

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);

    public void Dispose() { }

    private sealed class Sink(LogSink owner, string category) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Information;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Entries.Enqueue((logLevel, category, formatter(state, exception)));
        }
    }
}
