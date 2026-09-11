using System.Net.Http.Headers;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Diten.AuthService.Persistence.Settings;
using EphemeralMongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the ISOLATED real-provider acceptance fixture.
///
/// <para>WHAT IT IS. A test-owned, throwaway <c>mongod</c> (EphemeralMongo: its own process, its own random port,
/// its own temp data directory — never the shared localhost:27017) plus the REAL AuthService Api hosted in-process
/// (WebApplicationFactory&lt;Program&gt;). Every request in the tests that use it goes over HTTP through JWT
/// validation, tenant resolution and [HasPermission] — the production pipeline, not a controller called by hand.</para>
///
/// <para>WHAT IT NEVER TOUCHES (PPM approval boundary 1). The DefaultTenant, the seeded admin, any existing role or
/// user, the shared dev database on 5056/27017. Everything the tests read or write lives in a DISPOSABLE tenant (a
/// fresh Guid per run) inside a database with a FIXED name that is dropped and recreated per run (DB-010: a fixed
/// suffix, not a Guid in the database name). The production seeder does run at host start — into this throwaway
/// database only — and that is deliberate: it is how the tests prove the two new keys are in the catalog.</para>
///
/// <para>SECRETS. No fake secret is introduced. The host runs as Development and reads the repository's own
/// appsettings.Development.json (the local-development JWT secret that every service on this machine already
/// shares). Only these keys are overridden: the Mongo connection string + database name, the eventing transport
/// (InMemory — no RabbitMQ broker in a test), SMTP disabled, and the tenant-resolution dev bypass OFF so a request
/// without a tenant is refused exactly as in production.</para>
///
/// <para>⚠ HOW THE OVERRIDES REACH THE HOST — measured, not assumed. AuthService's <c>AddPersistence</c> reads the
/// connection string and builds the MongoClient at service-REGISTRATION time, inside Program.cs. With minimal hosting,
/// WebApplicationFactory's <c>ConfigureAppConfiguration</c>/<c>UseSetting</c> overrides are applied when the host is
/// BUILT — after Program.cs has already run — so the first attempt started the real Api against
/// <c>localhost:27017/diten_auth_v3</c> and the fixture's own guard refused it. Process environment variables
/// (<c>MongoDbSettings__ConnectionString</c>, …) are read by <c>WebApplication.CreateBuilder</c> itself, before any
/// registration, so they are the one channel that works here. They are set immediately before the host starts and
/// removed on dispose; the guard below still verifies what the host actually took.</para>
/// </summary>
public static class AccountKindAcceptance
{
    /// <summary>Fixed per DB-010: dropped and recreated on every run, never suffixed with a Guid.</summary>
    public const string DatabaseName = "diten_auth_itest_account_kind";

    public const string KindAdminRole = "kind-admin";
    public const string PmoRole = "pmo";
    public const string CreatorRole = "creator";
    public const string SeedActor = "account-kind-acceptance";
    public const string DisposablePassword = "Disposable#Acceptance2026!";

    /// <summary>A disposable user the seed created — id and the facts a test needs to name it.</summary>
    public sealed record SeedUser(Guid Id, string Email, string FirstName, string LastName);

    /// <summary>
    /// The disposable world: one tenant, four roles' worth of actors, six subjects. Tokens are real AuthService
    /// tokens (ITokenService from the host's own DI, signed with the host's own key) carrying the permission claims
    /// the role grants actually resolve to — the same claim set [HasPermission] evaluates.
    /// </summary>
    public sealed record Seed(
        Guid TenantId,
        Guid ForeignTenantId,
        SeedUser Human,
        SeedUser Unknown,
        SeedUser Service,
        SeedUser Passive,
        SeedUser Foreign,
        SeedUser Mutable,
        SeedUser KindAdmin,
        SeedUser Pmo,
        SeedUser Creator,
        SeedUser NoPermission,
        string KindAdminToken,
        string PmoToken,
        string CreatorToken,
        string NoPermissionToken);

    public sealed class AuthTestHost : IAsyncLifetime
    {
        private IMongoRunner? _runner;
        private WebApplicationFactory<Program>? _factory;
        private Seed? _seed;
        private readonly Dictionary<string, string?> _previousEnvironment = new();

        public string ConnectionString => _runner?.ConnectionString ?? throw NotStarted();
        public WebApplicationFactory<Program> Factory => _factory ?? throw NotStarted();
        public Seed Seeded => _seed ?? throw NotStarted();
        public List<string> MongoLog { get; } = new();

        /// <summary>
        /// The isolated database, for direct reads (audit rows) — resolved from the HOST's own DI so the reads use the
        /// exact client settings and Guid representation the production code writes with. (Measured: a second
        /// MongoClient with default settings read zero audit rows while the row was there — the Guid encoding differed.)
        /// </summary>
        public IMongoDatabase Database => Factory.Services.GetRequiredService<IMongoDatabase>();

        /// <summary>Convenience for tests that do not use the xunit fixture protocol.</summary>
        public static async Task<AuthTestHost> StartAsync()
        {
            var host = new AuthTestHost();
            await host.InitializeAsync();
            return host;
        }

        public async Task InitializeAsync()
        {
            var binaryDirectory = ResolveLocalMongoBinaryDirectory();
            // EphemeralMongo.Core 1.x: synchronous Run; the option is spelled StandardOuputLogger in this version.
            _runner = MongoRunner.Run(new MongoRunnerOptions
            {
                BinaryDirectory = binaryDirectory,
                StandardOuputLogger = line => MongoLog.Add("[mongod:out] " + line),
                StandardErrorLogger = line => MongoLog.Add("[mongod:err] " + line)
            });
            Console.WriteLine($"[AccountKindAcceptance] ephemeral mongod STARTED at {_runner.ConnectionString} from {binaryDirectory} (test-owned process; not the shared 27017)");

            // Drop → create: the fixed-name database starts empty on every run, whatever a previous run left.
            var client = new MongoClient(_runner.ConnectionString);
            await client.DropDatabaseAsync(DatabaseName);
            await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

            var overrides = new Dictionary<string, string?>
            {
                ["MongoDbSettings__ConnectionString"] = _runner.ConnectionString,
                ["MongoDbSettings__DatabaseName"] = DatabaseName,
                ["Eventing__Transport"] = "InMemory",
                ["Smtp__Enabled"] = "false",
                ["TenantResolution__DevBypassEnabled"] = "false",
                ["Observability__Metrics__Enabled"] = "false",
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            };
            foreach (var (key, value) in overrides)
            {
                _previousEnvironment[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
            });

            // Force the host to build now so a startup failure surfaces here, with its message, not in a test.
            _ = _factory.Server;

            var settings = _factory.Services.GetRequiredService<MongoDbSettings>();
            if (settings.ConnectionString != _runner.ConnectionString || settings.DatabaseName != DatabaseName)
            {
                throw new InvalidOperationException(
                    $"The host did not take the isolated Mongo settings (got {settings.ConnectionString}/{settings.DatabaseName}). Refusing to run against anything else.");
            }

            Console.WriteLine($"[AccountKindAcceptance] AuthService test host READY on {settings.DatabaseName}");
            _seed = await SeedAsync(this);
        }

        public async Task DisposeAsync()
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
                _factory = null;
            }

            foreach (var (key, previous) in _previousEnvironment)
            {
                Environment.SetEnvironmentVariable(key, previous);
            }

            _previousEnvironment.Clear();

            if (_runner is not null)
            {
                try
                {
                    await new MongoClient(_runner.ConnectionString).DropDatabaseAsync(DatabaseName);
                }
                catch
                {
                    // best effort; the runner deletes its data directory anyway
                }

                _runner.Dispose();
                _runner = null;
                Console.WriteLine("[AccountKindAcceptance] ephemeral mongod STOPPED and its data directory removed");
            }
        }

        /// <summary>An HTTP client against the in-process host, optionally authenticated as a disposable actor.</summary>
        public HttpClient Client(string? bearerToken = null, Guid? tenantHeader = null, string? correlationId = null)
        {
            var client = Factory.CreateClient();
            if (!string.IsNullOrEmpty(bearerToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (tenantHeader is { } tenant)
            {
                client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString("D"));
            }

            if (!string.IsNullOrEmpty(correlationId))
            {
                client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
            }

            return client;
        }

        /// <summary>
        /// A client pointed at a port nothing listens on. Proves the negative: a "real HTTP" test against a dead
        /// endpoint fails with a connection error, not with a green from a mock.
        /// </summary>
        public static HttpClient DeadClient() => new()
        {
            BaseAddress = new Uri("http://127.0.0.1:1/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static InvalidOperationException NotStarted() => new("AuthTestHost has not been started.");

        /// <summary>
        /// The directory holding the machine's own <c>mongod</c> binary: DITEN_ITEST_MONGOD_BIN_DIR, then every PATH
        /// entry, then the Homebrew/system locations. EphemeralMongo.Core 1.x ships no binaries and downloads none
        /// (see the csproj note), so a machine without mongod cannot run this fixture — it says so instead of
        /// silently falling back to the shared server. Every existing Mongo test in this repository already needs a
        /// mongod on the machine, so CI and every dev box satisfy this. The PROCESS is still the fixture's own.
        /// </summary>
        private static string ResolveLocalMongoBinaryDirectory()
        {
            var executable = OperatingSystem.IsWindows() ? "mongod.exe" : "mongod";

            var configured = Environment.GetEnvironmentVariable("DITEN_ITEST_MONGOD_BIN_DIR");
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(Path.Combine(configured, executable)))
            {
                return configured;
            }

            var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var candidate in pathEntries.Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" }))
            {
                if (File.Exists(Path.Combine(candidate, executable)))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                "AccountKindAcceptance needs a local mongod binary to start its OWN throwaway server (it never uses the shared 27017). "
                + "Install MongoDB (brew install mongodb-community) or set DITEN_ITEST_MONGOD_BIN_DIR to the directory containing mongod.");
        }
    }

    /// <summary>
    /// Builds the disposable world through the host's OWN repositories (no raw collection writes for the seed itself),
    /// so what the tests see is what the production code wrote. Roles are ordinary tenant roles, never SuperAdmin-like:
    /// <c>kind-admin</c> holds account-kind.manage + users.create + users.read; <c>pmo</c> holds users.lookup only;
    /// <c>creator</c> holds users.create only (to prove the create-time 403 over HTTP); the no-permission actor holds
    /// nothing. Every grant is a MANUAL grant — the explicit-grant-only key is never handed out any other way.
    /// </summary>
    public static async Task<Seed> SeedAsync(AuthTestHost host)
    {
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var stamp = Guid.NewGuid().ToString("N")[..8];

        using var scope = host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);

        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var permissions = sp.GetRequiredService<IPermissionRepository>();
        var rolePermissions = sp.GetRequiredService<IRolePermissionRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var tokens = sp.GetRequiredService<ITokenService>();

        // The two keys must already be in the catalog — the production seeder put them there at host start.
        var manage = await permissions.GetByKeyAsync(ExplicitGrantOnlyPermissions.UsersAccountKindManage, CancellationToken.None)
            ?? throw new InvalidOperationException("auth.users.account-kind.manage is not in the seeded catalog.");
        var lookup = await permissions.GetByKeyAsync("auth.users.lookup", CancellationToken.None)
            ?? throw new InvalidOperationException("auth.users.lookup is not in the seeded catalog.");
        var create = await permissions.GetByKeyAsync("auth.users.create", CancellationToken.None)
            ?? throw new InvalidOperationException("auth.users.create is not in the seeded catalog.");
        var read = await permissions.GetByKeyAsync("auth.users.read", CancellationToken.None)
            ?? throw new InvalidOperationException("auth.users.read is not in the seeded catalog.");

        var kindAdminRole = await roles.CreateAsync(new Role(KindAdminRole, "Kind Admin (disposable)", "acceptance fixture", tenantId), CancellationToken.None);
        var pmoRole = await roles.CreateAsync(new Role(PmoRole, "PMO (disposable)", "acceptance fixture", tenantId), CancellationToken.None);
        var creatorRole = await roles.CreateAsync(new Role(CreatorRole, "Creator (disposable)", "acceptance fixture", tenantId), CancellationToken.None);

        foreach (var permission in new[] { manage, create, read })
        {
            await rolePermissions.AssignAsync(RolePermission.ManualGrant(kindAdminRole.Id, permission.Id, tenantId, SeedActor), CancellationToken.None);
        }

        await rolePermissions.AssignAsync(RolePermission.ManualGrant(pmoRole.Id, lookup.Id, tenantId, SeedActor), CancellationToken.None);
        await rolePermissions.AssignAsync(RolePermission.ManualGrant(creatorRole.Id, create.Id, tenantId, SeedActor), CancellationToken.None);

        async Task<SeedUser> NewUser(string slug, string first, string last, Guid ownerTenant, AccountKind kind, bool active = true)
        {
            var user = new User($"{slug}.{stamp}@acceptance.invalid", hasher.Hash(DisposablePassword), first, last, ownerTenant);
            user.ConfirmEmail();
            // Tests are the ONLY place outside the SetAccountKind handler where a kind literal may appear
            // (AccountKindCreationPathsGuardTests scans src, not tests) — the fixture must create classified subjects.
            user.SetAccountKind(kind);
            if (!active)
            {
                user.Deactivate();
            }

            var created = await users.CreateAsync(user, CancellationToken.None);
            return new SeedUser(created.Id, created.Email, created.FirstName, created.LastName);
        }

        // Subjects (what the tests ask ABOUT).
        var human = await NewUser("human", "Zehra", "Yıldız", tenantId, AccountKind.Human);
        var unknown = await NewUser("unknown", "Umut", "Kaya", tenantId, AccountKind.Unknown);
        var service = await NewUser("service", "Integration", "Bot", tenantId, AccountKind.Service);
        var passive = await NewUser("passive", "Pasif", "Demir", tenantId, AccountKind.Human, active: false);
        var foreign = await NewUser("foreign", "Fatma", "Öztürk", foreignTenantId, AccountKind.Human);
        var mutable = await NewUser("mutable", "Mert", "Çelik", tenantId, AccountKind.Unknown);

        // Actors (who ASKS). They are also ordinary users of the disposable tenant.
        var kindAdmin = await NewUser("kind-admin", "Kind", "Admin", tenantId, AccountKind.Unknown);
        var pmo = await NewUser("pmo", "Pmo", "Reader", tenantId, AccountKind.Unknown);
        var creator = await NewUser("creator", "Only", "Creator", tenantId, AccountKind.Unknown);
        var noPermission = await NewUser("nobody", "No", "Permission", tenantId, AccountKind.Unknown);

        await userRoles.AssignAsync(new UserRole(kindAdmin.Id, kindAdminRole.Id, tenantId, SeedActor), CancellationToken.None);
        await userRoles.AssignAsync(new UserRole(pmo.Id, pmoRole.Id, tenantId, SeedActor), CancellationToken.None);
        await userRoles.AssignAsync(new UserRole(creator.Id, creatorRole.Id, tenantId, SeedActor), CancellationToken.None);

        async Task<string> TokenFor(SeedUser actor, Role? role)
        {
            var entity = await users.GetByIdAndTenantAsync(actor.Id, tenantId, CancellationToken.None)
                ?? throw new InvalidOperationException("seeded actor vanished");
            var roleNames = role is null ? Array.Empty<string>() : new[] { role.Name };
            var permissionKeys = role is null
                ? Array.Empty<string>()
                : (await rolePermissions.GetPermissionsByRoleAsync(role.Id, tenantId, CancellationToken.None)).ToArray();
            return tokens.GenerateAccessToken(entity, roleNames, permissionKeys, expiresInMinutes: 60);
        }

        return new Seed(
            tenantId,
            foreignTenantId,
            human, unknown, service, passive, foreign, mutable,
            kindAdmin, pmo, creator, noPermission,
            KindAdminToken: await TokenFor(kindAdmin, kindAdminRole),
            PmoToken: await TokenFor(pmo, pmoRole),
            CreatorToken: await TokenFor(creator, creatorRole),
            NoPermissionToken: await TokenFor(noPermission, null));
    }
}
