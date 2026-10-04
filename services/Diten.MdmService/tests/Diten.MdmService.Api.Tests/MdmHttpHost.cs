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
public sealed class MdmHttpHost : IAsyncLifetime
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
    public FakeReferenceData References { get; } = new();
    public RecordingAuditForwarder Audit { get; } = new();
    public FakePlatformReferenceEndpoint PlatformReference { get; } = new();
    public string MongoConnectionString => _runner?.ConnectionString ?? throw NotStarted();

    public WebApplicationFactory<GskusController> Factory => _factory ?? throw NotStarted();

    public IMongoDatabase Database =>
        Factory.Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);

    public async Task InitializeAsync()
    {
        var previous = new Dictionary<string, string?>();
        try
        {
            _runner = MongoRunner.Run(new MongoRunnerOptions
            {
                BinaryDirectory = ResolveLocalMongoBinaryDirectory(),
                UseSingleNodeReplicaSet = true
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
                ["VerifiedGskuResolver__PlatformBaseAddress"] = "http://platform.test/",
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
                ["GskuRetirementRequestWorkflowWorker__QuarantineAfterFailedAttempts"] = "4"
            };
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
                    foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                        services.Remove(hosted);
                    services.RemoveAll<IProductIdentityWorkflowClient>();
                    services.AddSingleton<IProductIdentityWorkflowClient>(Engine);
                    services.RemoveAll<IVerifiedGskuReferenceResolver>();
                    services.AddSingleton<IVerifiedGskuReferenceResolver>(References);
                    // The approval-time reference check stays the REAL client; only Platform's answer is faked.
                    services.RemoveAll<IWorkflowVerifiedMarketServiceIdentityProvider>();
                    services.AddSingleton<IWorkflowVerifiedMarketServiceIdentityProvider, FakeServiceIdentity>();
                    services.AddHttpClient(nameof(Diten.MdmService.Infrastructure.ReferenceData.PlatformWorkflowVerifiedGskuResolverClient))
                        .ConfigurePrimaryHttpMessageHandler(() => PlatformReference);
                    services.RemoveAll<IProductIdentityWorkflowTransportReadiness>();
                    services.AddSingleton<IProductIdentityWorkflowTransportReadiness, ReadyTransport>();
                    services.RemoveAll<IPlatformAuditForwarder>();
                    services.AddSingleton<IPlatformAuditForwarder>(Audit);
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
