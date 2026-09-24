using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.DevEnablementService.Api.Controllers;
using Diten.DevEnablementService.Api.ModuleRegistration;
using Diten.DevEnablementService.Domain.Entities;
using EphemeralMongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.ServerList;

/// <summary>
/// WP-UI-LIST-SERVER-01 — the ISOLATED real-provider host for the server-mode list tests. Copied in shape from
/// AuthService's Testing/AccountKindAcceptance.cs (the pattern the work package names), trimmed to what this service needs.
///
/// <para>WHAT IT IS. A test-owned, throwaway <c>mongod</c> (EphemeralMongo: its own process, random port, temp data
/// directory) plus the REAL DevEnablement Api hosted in-process. Every request goes over HTTP through JWT validation,
/// tenant resolution and [HasPermission] — the production pipeline.</para>
///
/// <para>WHAT IT NEVER TOUCHES. The shared localhost:27017, the DefaultTenant, any real user. The two tenants are fresh
/// Guids per run; the database has a FIXED name dropped at start (DB-010). The dev tenant bypass is OFF (a request
/// without a tenant is refused as in production). The JWT secret is a random, process-local value — never the
/// repository's shared development secret. The module self-registration push to Platform is removed from the host.</para>
///
/// <para>⚠ HOW THE OVERRIDES REACH THE HOST (measured in the Auth fixture, same here): AddPersistence builds the
/// MongoClient at REGISTRATION time inside Program.cs, before WebApplicationFactory's configuration hooks apply, so the
/// overrides travel as process environment variables. They are set inside a lock and RESTORED before the lock is
/// released (the Auth fixture's C3 lesson): a running host has already captured its configuration by then.</para>
///
/// <para>⚠ ONE HOST PER TEST PROCESS. AddPersistence registers a global Guid BSON serializer; building a second host
/// in the same process throws. Every test class uses this host through <see cref="ServerListCollection"/>.</para>
/// </summary>
public sealed class DevEnablementTestHost : IAsyncLifetime
{
    public const string DatabaseName = "diten_deven_itest_server_list";
    public const string CollectionName = "golden_reference_compact";
    private const string Issuer = "diten-auth-service";
    private const string Audience = "diten-erp";
    private const int SharedMongoPort = 27017;

    private static readonly SemaphoreSlim StartLock = new(1, 1);

    private IMongoRunner? _runner;
    private WebApplicationFactory<GoldenReferenceCompactController>? _factory;
    private string? _jwtSecret;

    public Guid TenantA { get; } = Guid.NewGuid();
    public Guid TenantB { get; } = Guid.NewGuid();

    public WebApplicationFactory<GoldenReferenceCompactController> Factory => _factory ?? throw NotStarted();

    public IMongoCollection<GoldenReferenceCompact> Collection =>
        Factory.Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName).GetCollection<GoldenReferenceCompact>(CollectionName);

    public async Task InitializeAsync()
    {
        await StartLock.WaitAsync();
        var previous = new Dictionary<string, string?>();
        try
        {
            var binaryDirectory = ResolveLocalMongoBinaryDirectory();
            _runner = MongoRunner.Run(new MongoRunnerOptions { BinaryDirectory = binaryDirectory });
            if (new MongoUrl(_runner.ConnectionString).Server.Port == SharedMongoPort)
                throw new InvalidOperationException($"The ephemeral mongod bound the SHARED port {SharedMongoPort}. Refusing to run.");

            await new MongoClient(_runner.ConnectionString).DropDatabaseAsync(DatabaseName);

            _jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var overrides = new Dictionary<string, string?>
            {
                ["Mongo__ConnectionString"] = _runner.ConnectionString,
                ["Mongo__DatabaseName"] = DatabaseName,
                ["JwtSettings__Secret"] = _jwtSecret,
                ["JwtSettings__Issuer"] = Issuer,
                ["JwtSettings__Audience"] = Audience,
                ["TenantResolution__DevBypassEnabled"] = "false",
                ["PlatformRegistration__BaseUrl"] = "http://127.0.0.1:1",
                ["WorkItemReferenceProvider__Enabled"] = "false",
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            };
            foreach (var (key, value) in overrides)
            {
                previous[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            _factory = new WebApplicationFactory<GoldenReferenceCompactController>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureTestServices(services =>
                {
                    // No outbound push to Platform from a test host.
                    var registration = services.Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationType == typeof(ModuleRegistrationHostedService)).ToList();
                    registration.ForEach(d => services.Remove(d));
                });
            });
            _ = _factory.Server;

            // Second line of defense: the host really took the isolated settings.
            var client = _factory.Services.GetRequiredService<IMongoClient>();
            var server = client.Settings.Server;
            var expected = new MongoUrl(_runner.ConnectionString).Server;
            if (server.Port != expected.Port || server.Port == SharedMongoPort)
                throw new InvalidOperationException($"The host did not take the isolated Mongo settings (got port {server.Port}). Refusing to run.");
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
                if (db.DatabaseNamespace.DatabaseName != DatabaseName)
                    throw new InvalidOperationException($"The host did not take the isolated database (got {db.DatabaseNamespace.DatabaseName}). Refusing to run.");
            }
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
            StartLock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            try { await _factory.DisposeAsync(); } catch (Exception ex) { Console.Error.WriteLine($"[DevEnablementTestHost] factory disposal failed: {ex}"); }
            _factory = null;
        }

        if (_runner is not null)
        {
            try { _runner.Dispose(); } catch (Exception ex) { Console.Error.WriteLine($"[DevEnablementTestHost] runner disposal failed: {ex}"); }
            _runner = null;
        }
    }

    /// <summary>A token for a user of <paramref name="tenantId"/> holding exactly <paramref name="permissions"/>.</summary>
    public string TokenFor(Guid tenantId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId.ToString())
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret ?? throw NotStarted()));
        var token = new JwtSecurityToken(Issuer, Audience, claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient Client(string? bearerToken)
    {
        var client = Factory.CreateClient();
        if (bearerToken is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }

    private static InvalidOperationException NotStarted() => new("DevEnablementTestHost has not been started.");

    private static string ResolveLocalMongoBinaryDirectory()
    {
        var executable = OperatingSystem.IsWindows() ? "mongod.exe" : "mongod";
        var configured = Environment.GetEnvironmentVariable("DITEN_ITEST_MONGOD_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, executable)))
            return configured;

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var candidate in pathEntries.Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" }))
        {
            if (File.Exists(Path.Combine(candidate, executable)))
                return candidate;
        }

        throw new InvalidOperationException(
            "The server-list tests need a local mongod binary to start their OWN throwaway server (never the shared 27017). "
            + "Install MongoDB (brew install mongodb-community) or set DITEN_ITEST_MONGOD_BIN_DIR.");
    }
}

[CollectionDefinition(Name)]
public sealed class ServerListCollection : ICollectionFixture<DevEnablementTestHost>
{
    public const string Name = "DevEnablementServerList";
}
