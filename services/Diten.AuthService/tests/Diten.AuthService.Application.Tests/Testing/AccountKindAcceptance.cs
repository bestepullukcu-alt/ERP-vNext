using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Diten.AuthService.Persistence.Settings;
using EphemeralMongo;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (R6) — the out-of-process acceptance host reuses this fixture's seed and
// pre-flight logic (mongod start, guard checks, JWT-secret generation, repository-based SeedAsync) rather than
// copying it. It needs `internal` access to a handful of TEST-ONLY members (the generated secret, the seed-only
// disposal split below) that must never become part of this class's PUBLIC contract — InternalsVisibleTo, not a
// visibility change, keeps that boundary intact.
[assembly: InternalsVisibleTo("Diten.AuthService.AccountKindAcceptanceHost")]
// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T11) — the committable test project needs AccountKindSeedFailedException and
// BeforeSeedHookForTesting to test the seed-failure DETECTION mechanism directly, in-process.
[assembly: InternalsVisibleTo("Diten.AuthService.AccountKindAcceptanceHost.Tests")]

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T11) — thrown ONLY when the fixture's own <c>SeedAsync</c> (repository
/// writes + the read-back checks that verify the production DataSeeder's catalog keys actually landed) fails,
/// never when mongod itself fails to start. The out-of-process host (W2) catches this type specifically to
/// report protocol error code <c>seed-failed</c> (exit 4), distinct from <c>mongo-start-failed</c> (exit 2) for
/// every other failure in the same start sequence.
/// </summary>
internal sealed class AccountKindSeedFailedException : Exception
{
    public AccountKindSeedFailedException(string message, Exception inner) : base(message, inner)
    {
    }
}

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
/// <para>SECRETS. The Mongo connection string + database name, the eventing transport (InMemory — no RabbitMQ
/// broker in a test), SMTP disabled, and the tenant-resolution dev bypass OFF (a request without a tenant is
/// refused exactly as in production) are overridden. The JWT signing secret is ALSO overridden — see C1 §4 below
/// — so the repository's shared appsettings.Development.json secret is never used here either.</para>
///
/// <para>⚠ HOW THE OVERRIDES REACH THE HOST — measured, not assumed. AuthService's <c>AddPersistence</c> reads the
/// connection string and builds the MongoClient at service-REGISTRATION time, inside Program.cs. With minimal hosting,
/// WebApplicationFactory's <c>ConfigureAppConfiguration</c>/<c>UseSetting</c> overrides are applied when the host is
/// BUILT — after Program.cs has already run — so the first attempt started the real Api against
/// <c>localhost:27017/diten_auth_v3</c> and the fixture's own guard refused it. Process environment variables
/// (<c>MongoDbSettings__ConnectionString</c>, …) are read by <c>WebApplication.CreateBuilder</c> itself, before any
/// registration, so they are the one channel that works here. See C3 below for exactly how long they stay set.</para>
///
/// <para>⚠ C1 (PPM CT correction, an earlier round). The ORIGINAL target check — comparing what the built host's own
/// <see cref="MongoDbSettings"/> singleton resolved to, against the runner — ran AFTER
/// <c>WebApplicationFactory.Server</c> had already built the host, which means the production seeder had ALREADY
/// run against whatever database the host actually resolved by the time a mismatch would have been caught. Three
/// checks (<see cref="AccountKindAcceptanceGuard"/>) now run BEFORE the host is built — see §1 in
/// <see cref="InitializeCoreAsync"/> — so a wrong target is refused before a single line of production seed data is
/// written anywhere. The post-build check (§ "the host did not take…") is KEPT as a second, redundant line of
/// defense; it should now be unreachable in practice, and reachability would itself indicate the pre-flight checks
/// have a gap.</para>
///
/// <para>⚠ C3 (PPM CT correction, THIS round) — the override's LIFETIME is lock-scoped and short, not tied to
/// Dispose. A prior version released <see cref="AuthTestHost.StartLock"/> as soon as the host's own config was
/// captured, but left the OVERRIDDEN environment variables live until <c>DisposeAsync</c> restored them. That
/// window was exactly wide enough for a SECOND host, starting while the first was still running, to capture the
/// FIRST host's overrides as ITS OWN "previous" values — measured shape: host A starts (env now holds A's
/// overrides, lock released); host B starts (B's <c>_previousEnvironment</c> now holds A's overrides, not the
/// true baseline); A disposes (restores the true baseline — fine, by luck); B disposes (restores A's OWN
/// override values — a dead connection string and a discarded JWT secret — permanently, for the rest of the
/// process, however B happened to be constructed). The fix: <see cref="InitializeCoreAsync"/> now restores the
/// environment to the value it read BEFORE it ever returns — inside the SAME lock hold that set the overrides,
/// immediately after the host's own <see cref="MongoDbSettings"/> is captured and validated, before the lock is
/// released. A running host is unaffected (its configuration was already captured into its own DI container by
/// then — see T3). <see cref="DisposeAsync"/> no longer touches the environment AT ALL: by the time any code can
/// call it, restoration has already happened, inside Start.</para>
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
        // C1/C3 §3 — env vars are process-global, so two hosts starting in the same process CANNOT be allowed to
        // interleave their env-var mutation windows. [Collection("AccountKindAcceptance")] already serializes every
        // xunit test class that opts into it; this static lock is the belt-and-suspenders that also protects a
        // direct `new AuthTestHost()` used outside that collection (as the guard tests below do), and makes the
        // serialization independently verifiable rather than merely assumed from xunit's collection semantics.
        // C3 — the window this lock brackets is the FULL set-override → pre-flight → build-host → capture-config →
        // RESTORE-override span (see InitializeCoreAsync): the environment is back to its pre-Start value before
        // the lock is ever released, on every path, success or failure — see the class remarks.
        private static readonly SemaphoreSlim StartLock = new(1, 1);

        // C1 §3 — test-observable concurrency probe. ActiveCriticalSections is the number of AuthTestHost
        // instances CURRENTLY inside the env-var-mutation window (bracketed by the same StartLock hold);
        // MaxObservedConcurrentCriticalSections is the high-water mark since the last reset. Both are internal:
        // visible only to this test assembly, used exclusively by AccountKindAcceptanceGuardTests to prove two
        // concurrent Start() calls never actually overlap (StartLock guarantees it by construction — these fields
        // let a test OBSERVE that guarantee empirically, so removing the lock later turns the test red).
        internal static int ActiveCriticalSections;
        internal static int MaxObservedConcurrentCriticalSections;

        internal static void ResetConcurrencyProbeForTests()
        {
            Interlocked.Exchange(ref ActiveCriticalSections, 0);
            Interlocked.Exchange(ref MaxObservedConcurrentCriticalSections, 0);
        }

        private static void RecordActiveCriticalSectionHighWaterMark()
        {
            var observed = Volatile.Read(ref ActiveCriticalSections);
            int current;
            do
            {
                current = Volatile.Read(ref MaxObservedConcurrentCriticalSections);
                if (observed <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref MaxObservedConcurrentCriticalSections, observed, current) != current);
        }

        private IMongoRunner? _runner;
        private WebApplicationFactory<Program>? _factory;
        private Seed? _seed;
        private readonly Dictionary<string, string?> _previousEnvironment = new();
        private bool _lockHeld;
        private bool _isAcceptanceHostPath;

        public string ConnectionString => _runner?.ConnectionString ?? throw NotStarted();
        public WebApplicationFactory<Program> Factory => _factory ?? throw NotStarted();
        public Seed Seeded => _seed ?? throw NotStarted();
        public List<string> MongoLog { get; } = new();

        /// <summary>
        /// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (R6/C5) — TEST-ONLY. The mongod process id, for the out-of-process
        /// host to report over the private channel (protocol §7 <c>ready.mongodPid</c>) so a supervisor's PID+
        /// start-time verified cleanup can find it. <see cref="EphemeralMongo.IMongoRunner"/> does not expose the
        /// process id on its public surface (measured: <c>IMongoRunner.Process</c> does not compile — that member
        /// lives on an internal implementation type), so this resolves it the same way an external operator would:
        /// by asking the OS which process owns the LISTEN socket on the runner's own port. Not part of the
        /// fixture's external contract.
        /// </summary>
        internal int MongodProcessId
        {
            get
            {
                if (_runner is null)
                {
                    throw NotStarted();
                }

                var port = MongoUrl.Create(_runner.ConnectionString).Servers.Single().Port;
                return ResolveListenerProcessId(port);
            }
        }

        private static int ResolveListenerProcessId(int port)
        {
            var isWindows = OperatingSystem.IsWindows();
            var psi = isWindows
                ? new System.Diagnostics.ProcessStartInfo("netstat", "-ano")
                : new System.Diagnostics.ProcessStartInfo("lsof", $"-nP -iTCP:{port} -sTCP:LISTEN -t");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;

            using var probe = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("could not start the process-lookup probe (lsof/netstat).");
            var output = probe.StandardOutput.ReadToEnd();
            probe.WaitForExit(5000);

            if (isWindows)
            {
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains($":{port} ", StringComparison.Ordinal) && line.Contains("LISTENING", StringComparison.Ordinal))
                    {
                        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length > 0 && int.TryParse(fields[^1], out var winPid))
                        {
                            return winPid;
                        }
                    }
                }
            }
            else
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(line, out var pid))
                    {
                        return pid;
                    }
                }
            }

            throw new InvalidOperationException($"could not resolve the mongod process id listening on port {port}.");
        }

        /// <summary>
        /// TEST-ONLY (C1 §4/§5 leak guard). The random JWT secret THIS instance generated, exposed solely so
        /// AccountKindAcceptanceGuardTests can prove it never reaches Console output or <see cref="MongoLog"/>.
        /// Never used to build a token — every token in <see cref="Seed"/> comes from the host's own
        /// <c>ITokenService</c> (see <see cref="SeedAsync"/>), signed with this same secret but never printed.
        /// Not part of the fixture's external contract.
        /// </summary>
        internal string? GeneratedJwtSecretForLeakGuardOnly { get; private set; }

        /// <summary>
        /// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (C8) — a fresh, random, per-instance password for the 4 SEED ACTORS
        /// only (kindAdmin/pmo/creator/noPermission — the users a real login is performed as). Subjects (human,
        /// unknown, service, …) keep <see cref="DisposablePassword"/> since nothing ever logs in as them. Computed
        /// once per <see cref="AuthTestHost"/> instance so <see cref="SeedAsync"/> can hash the 4 actors with it
        /// while every other existing caller of this fixture (in-process tests that never perform a real login)
        /// is unaffected. Not part of the fixture's external contract.
        /// </summary>
        internal string ActorPassword { get; } = GenerateTestOnlyActorPassword();

        private static string GenerateTestOnlyActorPassword()
        {
            Span<byte> bytes = stackalloc byte[18];
            RandomNumberGenerator.Fill(bytes);
            // Base64 can contain '/' and '+' which some password policies special-case oddly; swap for a fixed,
            // still-random-looking separator so this never accidentally exercises a policy corner case unrelated
            // to what the acceptance host is testing. Always includes upper/lower/digit by construction (Base64
            // alphabet) plus one guaranteed special character appended, satisfying every PasswordPolicyService
            // class requirement the repo's own default policy could plausibly enable.
            return Convert.ToBase64String(bytes).Replace('/', 'x').Replace('+', 'y') + "!1Aa";
        }

        /// <summary>
        /// TEST-ONLY seam (C3, T5). When set, <see cref="DisposeAsync"/> calls this INSTEAD of
        /// <c>factory.DisposeAsync()</c> directly, so a test can inject a failure at that exact point while still
        /// disposing the real factory for real (the hook is expected to dispose it itself, then may throw) — proving
        /// runner cleanup and lock release still complete, and the exception still propagates, when factory disposal
        /// fails. Not part of the fixture's external contract.
        /// </summary>
        internal Func<WebApplicationFactory<Program>, Task>? DisposeFactoryHookForTesting { get; set; }

        /// <summary>
        /// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T11) — TEST-ONLY seam. When set, invoked AFTER the real host is
        /// built (the production DataSeeder has already run against it) but BEFORE this fixture's own
        /// <c>SeedAsync</c> — including its read-back checks that verify the production seeder's catalog keys
        /// actually landed. Lets a test simulate "the production seeder silently swallowed an error" (its own
        /// try/catch in Persistence/DependencyInjection.cs never surfaces one) by directly removing something
        /// the production seeder just wrote, WITHOUT touching production code: the read-back check that follows
        /// is then exercised against a genuinely corrupted catalog, the same way a real silent failure would
        /// leave it. Not part of the fixture's external contract.
        /// </summary>
        internal Func<AuthTestHost, Task>? BeforeSeedHookForTesting { get; set; }

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

        /// <summary>
        /// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (C9) — same as <see cref="StartAsync()"/>, but mongod's data
        /// directory is the supplied (supervisor-owned, already lstat-verified 0700) directory instead of
        /// EphemeralMongo's own auto-generated temp path. Not part of the IAsyncLifetime contract (that interface
        /// requires a strictly parameterless InitializeAsync — see below) and never called by any existing
        /// in-process xunit test.
        /// <para>G1 (Aşama G) — <paramref name="emptyContentRootDirectory"/> is the run-owned, EMPTY directory the
        /// prep host uses as its content root. Measured: the API's own Program.cs reads configuration EAGERLY
        /// (observability options, secret validation, JwtBearer issuer/audience, Mongo settings, DataSeeder) before
        /// <c>builder.Build()</c>, i.e. before any <c>ConfigureAppConfiguration</c> callback can clear sources — so
        /// the only way that window never sees the API's checked-in appsettings.json is a content root with nothing
        /// in it. A directory that has ANY entry is refused before mongod or the host is built.</para>
        /// </summary>
        public static async Task<AuthTestHost> StartWithMongoDataDirectoryAsync(
            string mongoDataDirectory, string emptyContentRootDirectory, Func<AuthTestHost, Task>? beforeSeedHookForTesting = null)
        {
            var host = new AuthTestHost { BeforeSeedHookForTesting = beforeSeedHookForTesting };
            await host.InitializeCoreAsync(injectFailureForTesting: false, mongoDataDirectory, emptyContentRootDirectory);
            return host;
        }

        public Task InitializeAsync() => InitializeCoreAsync(injectFailureForTesting: false, mongoDataDirectory: null);

        /// <summary>
        /// TEST-ONLY seam (never used by AccountKindEndpointTests or any production-facing test). Runs the EXACT
        /// same start sequence as <see cref="InitializeAsync"/> — real mongod, real env-var overrides, the same
        /// three pre-flight checks — but throws immediately after they pass, BEFORE the host is built. This lets
        /// AccountKindAcceptanceGuardTests observe the real cleanup path (env revert + mongod process killed +
        /// original exception propagated) without needing to actually corrupt the target (which, by construction,
        /// nothing in this process can do once the overrides are applied — see the class remarks). Not part of the
        /// fixture's external contract: <see cref="InitializeAsync"/>'s own signature and behavior are unchanged.
        /// </summary>
        internal Task InitializeAsync_ForTestingInjectedFailure() => InitializeCoreAsync(injectFailureForTesting: true, mongoDataDirectory: null);

        private async Task InitializeCoreAsync(bool injectFailureForTesting, string? mongoDataDirectory, string? contentRootDirectory = null)
        {
            // G3 (Aşama G) — recorded on the instance BEFORE anything can fail, so every diagnostic site below
            // (including DisposeResourcesAsync, which DisposeAsync also reaches later) knows which path it is on.
            _isAcceptanceHostPath = mongoDataDirectory is not null;

            await StartLock.WaitAsync().ConfigureAwait(false);
            _lockHeld = true;
            Interlocked.Increment(ref ActiveCriticalSections);
            RecordActiveCriticalSectionHighWaterMark();

            try
            {
                try
                {
                    // G1 (Aşama G) — refuse before mongod or the host exists: the prep host's content root must be
                    // a fully-qualified, existing directory with no entry at all (see StartWithMongoDataDirectoryAsync).
                    if (_isAcceptanceHostPath)
                    {
                        EnsureEmptyAcceptanceContentRoot(contentRootDirectory);
                    }

                    var binaryDirectory = ResolveLocalMongoBinaryDirectory();
                    // EphemeralMongo.Core 1.x: synchronous Run; the option is spelled StandardOuputLogger in this version.
                    var runnerOptions = new MongoRunnerOptions
                    {
                        BinaryDirectory = binaryDirectory,
                        StandardOuputLogger = line => MongoLog.Add("[mongod:out] " + line),
                        StandardErrorLogger = line => MongoLog.Add("[mongod:err] " + line)
                    };
                    if (mongoDataDirectory is not null)
                    {
                        runnerOptions.DataDirectory = mongoDataDirectory;
                    }

                    _runner = MongoRunner.Run(runnerOptions);

                    // C1 §1(b) — refuse BEFORE touching any data on the runner if it somehow bound the shared port.
                    AccountKindAcceptanceGuard.EnsureRunnerIsNotTheSharedServer(_runner.ConnectionString);

                    Console.WriteLine($"[AccountKindAcceptance] ephemeral mongod STARTED at {_runner.ConnectionString} from {binaryDirectory} (test-owned process; not the shared 27017)");

                    // Drop → create: the fixed-name database starts empty on every run, whatever a previous run left.
                    var client = new MongoClient(_runner.ConnectionString);
                    await client.DropDatabaseAsync(DatabaseName);
                    await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

                    // C1 §4 — a fresh, random, process-local JWT secret. Never printed (§5), never the repository's
                    // shared appsettings.Development.json value. Stored on the instance (not just the environment)
                    // so it is still readable for the leak guard even after C3 restores the environment below.
                    var jwtSecret = GenerateTestOnlyJwtSecret();
                    GeneratedJwtSecretForLeakGuardOnly = jwtSecret;

                    // F10 — ONLY the acceptance-host prep path (entered via StartWithMongoDataDirectoryAsync,
                    // i.e. mongoDataDirectory is not null) gets an isolated profile: a non-Development
                    // environment name (Development is what gates ASP.NET's own automatic AddUserSecrets call)
                    // and, below, a configuration chain built EXCLUSIVELY from these test-owned overrides — no
                    // appsettings.json/appsettings.Development.json read from the API's own content root, no
                    // user secrets. The three OTHER consumers of this fixture (AccountKindAcceptanceGuardTests,
                    // AccountKindEndpointTests, UserDisplayLabelEndpointTests — all reach this via
                    // InitializeAsync()/InitializeAsync_ForTestingInjectedFailure(), which always pass
                    // mongoDataDirectory: null) are UNCHANGED: they keep "Development" exactly as before.
                    var isAcceptanceHostPath = _isAcceptanceHostPath;
                    var hostEnvironmentName = isAcceptanceHostPath ? "AcceptanceHostPrep" : "Development";

                    var overrides = new Dictionary<string, string?>
                    {
                        ["MongoDbSettings__ConnectionString"] = _runner.ConnectionString,
                        ["MongoDbSettings__DatabaseName"] = DatabaseName,
                        ["Eventing__Transport"] = "InMemory",
                        ["Smtp__Enabled"] = "false",
                        ["TenantResolution__DevBypassEnabled"] = "false",
                        ["Observability__Metrics__Enabled"] = "false",
                        ["ASPNETCORE_ENVIRONMENT"] = hostEnvironmentName,
                        ["JwtSettings__Secret"] = jwtSecret
                    };

                    if (isAcceptanceHostPath)
                    {
                        // F10 — measured: with appsettings.Development.json/user secrets no longer read at all for
                        // this path, AuthService's own required-secret validation (SecretServiceCollectionExtensions)
                        // failed on InternalEventAuth:ApiKey / PlatformService:InternalApiKey — this prep host was
                        // SILENTLY relying on whatever non-test-owned source (checked-in appsettings.Development.json
                        // or real user secrets) happened to supply those two values before. Test-owned, disposable
                        // values close that gap; this prep host's own login-settings/internal-event calls are never
                        // actually exercised (only the production DataSeeder + this fixture's SeedAsync run here),
                        // so these only need to satisfy startup validation, not be reachable secrets.
                        overrides["InternalEventAuth__ApiKey"] = GenerateTestOnlyJwtSecret();
                        overrides["PlatformService__InternalApiKey"] = GenerateTestOnlyJwtSecret();

                        // G2(b)(i) (Aşama G) — the same sink-off values the API child already gets (Program.cs). They
                        // are set as process environment variables below, so the EAGER window (CreateBuilder's own
                        // environment source) sees them, and they are also the late window's only source. Measured:
                        // Observability:Tracing:OtlpExporterEnabled is NOT forced — its class default is false, the
                        // appsettings.json that set it is no longer read (G1), the host's environment allow-list
                        // removes any inherited value, and OTEL_SDK_DISABLED=true turns the OpenTelemetry SDK off
                        // wholesale even if an exporter were registered.
                        overrides["Observability__Seq__Enabled"] = "false";
                        overrides["OTEL_SDK_DISABLED"] = "true";
                    }
                    foreach (var (key, value) in overrides)
                    {
                        _previousEnvironment[key] = Environment.GetEnvironmentVariable(key);
                        Environment.SetEnvironmentVariable(key, value);
                    }

                    // C1 §1(a) — self-check: the loop above actually applied every override (catches a coding mistake
                    // in the loop itself, e.g. an exception mid-iteration leaving a later key unset).
                    foreach (var (key, value) in overrides)
                    {
                        AccountKindAcceptanceGuard.EnsureEnvironmentTookTheOverride(key, value);
                    }

                    // C1 §1(c) — resolve the SAME configuration chain Program.cs will read (appsettings.json →
                    // appsettings.Development.json → user secrets → environment variables), WITHOUT building the host,
                    // and refuse here if it does not already land on the isolated database. F10/G2(a) — for the
                    // acceptance path this preview must mirror what the REAL factory build below will actually do
                    // (the test-owned overrides only, as an in-memory source), or the §1(c) comparison would no
                    // longer mean what it claims to.
                    var preview = BuildEffectiveHostConfigurationPreview(isAcceptanceHostPath, overrides);
                    AccountKindAcceptanceGuard.EnsureEffectiveConfigurationTargetsTheIsolatedDatabase(
                        preview, _runner.ConnectionString, DatabaseName);

                    if (injectFailureForTesting)
                    {
                        throw new InvalidOperationException(
                            "TEST-INJECTED-FAILURE: AccountKindAcceptanceGuardTests simulated a startup failure after the pre-flight checks passed.");
                    }

                    _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                    {
                        builder.UseEnvironment(hostEnvironmentName);

                        if (isAcceptanceHostPath)
                        {
                            // G1 (Aşama G) — the EAGER window. UseContentRoot flows through WebApplicationFactory's
                            // deferred host builder as host configuration (a command-line argument to the API's
                            // CreateBuilder), exactly like UseEnvironment above — measured: the environment name
                            // already reached CreateBuilder this way. With an empty content root, CreateBuilder's own
                            // appsettings.json / appsettings.{Environment}.json sources find nothing, so every value
                            // Program.cs binds BEFORE Build (observability, secret validation, JwtBearer
                            // issuer/audience, Mongo, DataSeeder) comes only from the test-owned environment
                            // overrides set above.
                            builder.UseContentRoot(contentRootDirectory!);

                            // F10/G2(a) — the LATE window. Strip whatever the API's own CreateBuilder() added and
                            // rebuild from ONLY the fixture's own overrides dictionary as an in-memory source — no
                            // file provider and no environment-variable provider, so nothing else in this process's
                            // environment can reach the built host's configuration. ConfigureAppConfiguration
                            // callbacks registered here run at Build, after CreateBuilder assembled its defaults.
                            builder.ConfigureAppConfiguration((_, config) =>
                            {
                                config.Sources.Clear();
                                config.AddInMemoryCollection(ToConfigurationKeys(overrides));
                            });

                            // Measured (Aşama G): AddAuthentication registers DataProtection, whose hosted service
                            // creates a key ring at start under $HOME/.aspnet/DataProtection-Keys — a directory that is
                            // resolved ONCE per process and cached, so an in-process test that isolates HOME for one
                            // call left that key directory behind (dak-f10-home-*/.aspnet/DataProtection-Keys/key-*.xml)
                            // and a test that does not isolate HOME would write under the developer's real HOME. The
                            // prep host only seeds; its keys never need to outlive it, so they stay in memory.
                            builder.ConfigureServices(services => services.Configure<KeyManagementOptions>(
                                options => options.XmlRepository = new InMemoryXmlRepository()));
                        }
                    });

                    // Force the host to build now so a startup failure surfaces here, with its message, not in a test.
                    _ = _factory.Server;

                    // Kept as a second, redundant line of defense — see the class remarks on C1. Should be unreachable
                    // now that §1(c) already proved the effective configuration resolves correctly before this point.
                    var settings = _factory.Services.GetRequiredService<MongoDbSettings>();
                    if (settings.ConnectionString != _runner.ConnectionString || settings.DatabaseName != DatabaseName)
                    {
                        throw new InvalidOperationException(
                            $"The host did not take the isolated Mongo settings (got {settings.ConnectionString}/{settings.DatabaseName}). Refusing to run against anything else.");
                    }

                    Console.WriteLine($"[AccountKindAcceptance] AuthService test host READY on {settings.DatabaseName}");
                }
                catch
                {
                    // A failure anywhere above: kill whatever got created (factory/runner), each in its own
                    // try/catch (see DisposeResourcesAsync). A cleanup-step exception here is LOGGED, never
                    // allowed to replace the ORIGINAL startup failure — that is always what the caller sees
                    // (the bare `throw;` below), because it is what a caller like T4 actually asserts on.
                    try
                    {
                        await DisposeResourcesAsync(bestEffortDropDatabase: false).ConfigureAwait(false);
                    }
                    catch (Exception cleanupEx)
                    {
                        Console.Error.WriteLine(_isAcceptanceHostPath
                            ? AcceptancePathDiagnostic("startup-cleanup-failed", cleanupEx)
                            : "[AccountKindAcceptance] cleanup after a startup failure raised its own exception "
                              + $"(suppressed; the startup failure is what propagates): {cleanupEx}");
                    }

                    throw;
                }
            }
            finally
            {
                // C3 §1/§2 — THE FIX. The override's lifetime is lock-scoped and SHORT: restored HERE, inside the
                // SAME lock hold that set it, on EVERY path (success above, or the catch's rethrow) — never left
                // live until Dispose. This is what closes the bug: a second Start(), whenever it acquires the lock
                // next, always reads the TRUE pre-Start baseline as its own "previous", never a still-running
                // host's overrides. The lock itself is released in this SAME finally, unconditionally (§2 — "kilit
                // HER durumda finally'de bırakılır") so a startup failure can never leave the next Start() waiting.
                RestoreEnvironment();
                ReleaseStartLockIfHeld();
            }

            // Outside the lock, deliberately: by this point the environment is ALREADY back to its pre-Start value
            // (T3) and this host's OWN configuration is already captured inside `_factory`'s DI container, so
            // SeedAsync — which touches no environment variable — needs none of the lock's protection. A failure
            // here still cleans up the resources this Start() created (mirrors the pre-build catch above).
            try
            {
                if (BeforeSeedHookForTesting is { } beforeSeedHook)
                {
                    await beforeSeedHook(this);
                }

                _seed = await SeedAsync(this);
            }
            catch (Exception seedEx)
            {
                try
                {
                    await DisposeResourcesAsync(bestEffortDropDatabase: false).ConfigureAwait(false);
                }
                catch (Exception cleanupEx)
                {
                    Console.Error.WriteLine(_isAcceptanceHostPath
                        ? AcceptancePathDiagnostic("seed-cleanup-failed", cleanupEx)
                        : "[AccountKindAcceptance] cleanup after a seed failure raised its own exception "
                          + $"(suppressed; the seed failure is what propagates): {cleanupEx}");
                }

                // WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (T11) — wrapped in a distinct exception TYPE so the
                // out-of-process host (W2) can tell "mongod itself never came up" (mongo-start-failed) apart from
                // "mongod is fine but seeding on top of it failed" (seed-failed) — including the case where the
                // PRODUCTION DataSeeder silently swallowed its own error (Persistence/DependencyInjection.cs's
                // catch) and this fixture's own read-back checks above (the two ExplicitGrantOnlyPermissions
                // lookups) are what actually catches it. No production code changes; this is the detection.
                throw new AccountKindSeedFailedException(seedEx.Message, seedEx);
            }
        }

        /// <summary>
        /// C3 §3 — disposes the factory (via <see cref="DisposeFactoryHookForTesting"/> when a test has set one,
        /// otherwise directly) and the runner, EACH IN ITS OWN try/catch so a failure in one does not prevent the
        /// other from running. If more than one step throws, the FIRST such exception is rethrown — with its
        /// ORIGINAL stack trace, via <see cref="ExceptionDispatchInfo"/>, never wrapped in an
        /// <see cref="AggregateException"/> — and every later one is logged to <see cref="Console.Error"/> rather
        /// than silently lost. <paramref name="bestEffortDropDatabase"/> is true only on the normal (post-start)
        /// dispose path: a startup failure should not risk a second exception dropping a database that was never
        /// fully seeded, when the runner's own Dispose deletes its whole data directory anyway.
        /// </summary>
        private async Task DisposeResourcesAsync(bool bestEffortDropDatabase)
        {
            Exception? first = null;

            if (_factory is not null)
            {
                var factory = _factory;
                _factory = null;
                try
                {
                    if (DisposeFactoryHookForTesting is { } hook)
                    {
                        await hook(factory).ConfigureAwait(false);
                    }
                    else
                    {
                        await factory.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    first ??= ex;
                    Console.Error.WriteLine(_isAcceptanceHostPath
                        ? AcceptancePathDiagnostic("factory-disposal-failed", ex)
                        : $"[AccountKindAcceptance] factory disposal failed: {ex}");
                }
            }

            if (_runner is not null)
            {
                var runner = _runner;
                _runner = null;
                try
                {
                    if (bestEffortDropDatabase)
                    {
                        try
                        {
                            await new MongoClient(runner.ConnectionString).DropDatabaseAsync(DatabaseName).ConfigureAwait(false);
                        }
                        catch
                        {
                            // best effort; the runner deletes its data directory anyway
                        }
                    }

                    runner.Dispose();
                    Console.WriteLine(bestEffortDropDatabase
                        ? "[AccountKindAcceptance] ephemeral mongod STOPPED and its data directory removed"
                        : "[AccountKindAcceptance] ephemeral mongod STOPPED after a startup failure (data directory removed)");
                }
                catch (Exception ex)
                {
                    first ??= ex;
                    Console.Error.WriteLine(_isAcceptanceHostPath
                        ? AcceptancePathDiagnostic("runner-disposal-failed", ex)
                        : $"[AccountKindAcceptance] runner disposal failed: {ex}");
                }
            }

            if (first is not null)
            {
                ExceptionDispatchInfo.Capture(first).Throw();
            }
        }

        /// <summary>
        /// C3 — touches ONLY the factory, the runner and its database (via <see cref="DisposeResourcesAsync"/>).
        /// The environment is NEVER restored here: by the time anything can call Dispose, <see cref="InitializeCoreAsync"/>
        /// has ALREADY restored it, inside its own lock hold (see the class remarks). The lock is released
        /// unconditionally in <c>finally</c> — T5: even a factory-disposal failure must not leave a later Start()
        /// waiting on a lock this instance already finished with in practice (a no-op in the normal case, since
        /// InitializeCoreAsync already released it; kept as the same guaranteed-release discipline everywhere).
        /// </summary>
        public async Task DisposeAsync()
        {
            try
            {
                await DisposeResourcesAsync(bestEffortDropDatabase: true).ConfigureAwait(false);
            }
            finally
            {
                ReleaseStartLockIfHeld();
            }
        }

        /// <summary>
        /// WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (R6) — TEST-ONLY seam for the out-of-process acceptance host. Disposes
        /// ONLY the in-process WebApplicationFactory (the seed-time host); the ephemeral <see cref="_runner"/>
        /// (mongod) and its database are left running so a REAL, separate `dotnet Api.dll` process can attach to
        /// the exact same disposable Mongo the seed just wrote into. The caller becomes responsible for the
        /// runner's lifetime afterward — <see cref="ConnectionString"/> and <see cref="DatabaseName"/> stay valid,
        /// but <see cref="Factory"/>/<see cref="Database"/> throw once this returns. Not part of the fixture's
        /// external (production-test-facing) contract: <see cref="DisposeAsync"/>'s own behavior (both torn down
        /// together) is unchanged for every existing test.
        /// </summary>
        internal async Task DisposeFactoryOnlyAsync()
        {
            if (_factory is not null)
            {
                var factory = _factory;
                _factory = null;
                await factory.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void RestoreEnvironment()
        {
            foreach (var (key, previous) in _previousEnvironment)
            {
                Environment.SetEnvironmentVariable(key, previous);
            }

            _previousEnvironment.Clear();
        }

        private void ReleaseStartLockIfHeld()
        {
            if (_lockHeld)
            {
                _lockHeld = false;
                Interlocked.Decrement(ref ActiveCriticalSections);
                StartLock.Release();
            }
        }

        /// <summary>C1 §4 — ≥64 random bytes, base64-encoded (~88 chars, well above the 32-char minimum the
        /// secret validator enforces for JwtSettings:Secret). A fresh value every run.</summary>
        private static string GenerateTestOnlyJwtSecret()
        {
            Span<byte> bytes = stackalloc byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// C1 §1(c) — builds the SAME configuration chain <c>WebApplication.CreateBuilder</c> assembles inside
        /// Program.cs: appsettings.json → appsettings.{Environment}.json → user secrets (Development only, when the
        /// entry assembly carries a UserSecretsId) → environment variables. Environment variables are added LAST —
        /// by the time this runs the overrides above are already live in THIS process's environment, so the
        /// preview resolves to exactly what Program.cs will resolve once the host is actually built.
        /// </summary>
        private static IConfigurationRoot BuildEffectiveHostConfigurationPreview(
            bool isAcceptanceHostPath, IReadOnlyDictionary<string, string?> overrides)
        {
            var builder = new ConfigurationBuilder();

            // F10/G2(a) — the acceptance-host prep path never reads the API's own appsettings.json/
            // appsettings.Development.json, user secrets or the process environment; its preview is the same
            // in-memory override source the real build uses (see the call site).
            if (isAcceptanceHostPath)
            {
                builder.AddInMemoryCollection(ToConfigurationKeys(overrides));
                return builder.Build();
            }

            var apiContentRoot = ResolveApiContentRoot();
            builder
                .AddJsonFile(Path.Combine(apiContentRoot, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(apiContentRoot, "appsettings.Development.json"), optional: true, reloadOnChange: false);

            var userSecretsId = typeof(Program).Assembly.GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;
            if (!string.IsNullOrWhiteSpace(userSecretsId))
            {
                builder.AddUserSecrets(userSecretsId);
            }

            builder.AddEnvironmentVariables();
            return builder.Build();
        }

        /// <summary>G2(a) — environment-variable spelling ("A__B") to configuration-key spelling ("A:B"), the same
        /// translation the environment-variable provider applies, for the in-memory override source.</summary>
        internal static Dictionary<string, string?> ToConfigurationKeys(IReadOnlyDictionary<string, string?> overrides) =>
            overrides.ToDictionary(pair => pair.Key.Replace("__", ":", StringComparison.Ordinal), pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        /// <summary>G1 — fixed text only: no path, no directory listing, no exception text.</summary>
        private static void EnsureEmptyAcceptanceContentRoot(string? contentRootDirectory)
        {
            if (string.IsNullOrEmpty(contentRootDirectory)
                || !Path.IsPathFullyQualified(contentRootDirectory)
                || !Directory.Exists(contentRootDirectory)
                || Directory.EnumerateFileSystemEntries(contentRootDirectory).Any())
            {
                throw new InvalidOperationException(
                    "Refusing to start: the acceptance-host content root must be an existing, fully-qualified, empty directory.");
            }
        }

        /// <summary>G3 (Aşama G) — on the acceptance-host path a fixture diagnostic carries a fixed code and the
        /// exception's TYPE name only; never its message, inner exception or stack (those can carry paths,
        /// connection strings or test canaries). Every other consumer keeps its existing full text.</summary>
        private static string AcceptancePathDiagnostic(string fixedCode, Exception ex) =>
            $"[AccountKindAcceptance] {fixedCode} ({ex.GetType().Name})";

        /// <summary>Key ring storage for the prep host only — lives and dies with the host, never touches disk.</summary>
        private sealed class InMemoryXmlRepository : IXmlRepository
        {
            private readonly List<XElement> _elements = new();

            public IReadOnlyCollection<XElement> GetAllElements()
            {
                lock (_elements)
                {
                    return _elements.Select(element => new XElement(element)).ToList();
                }
            }

            public void StoreElement(XElement element, string friendlyName)
            {
                lock (_elements)
                {
                    _elements.Add(new XElement(element));
                }
            }
        }

        /// <summary>
        /// Locates <c>services/Diten.AuthService/src/Diten.AuthService.Api</c> — the SAME source directory
        /// WebApplicationFactory resolves as the host's content root (it reads the API project's own
        /// appsettings.json/appsettings.Development.json directly, not a copy in the test output directory), so the
        /// pre-flight preview reads the identical physical files Program.cs will read.
        /// </summary>
        private static string ResolveApiContentRoot()
        {
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            while (probe is not null)
            {
                var candidate = Path.Combine(probe.FullName, "services", "Diten.AuthService", "src", "Diten.AuthService.Api");
                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                {
                    return candidate;
                }

                probe = probe.Parent;
            }

            throw new DirectoryNotFoundException(
                "Diten.AuthService.Api's appsettings.json was not found above the test assembly — the C1 §1(c) "
                + "pre-flight target check cannot read the same configuration files Program.cs will read.");
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

        async Task<SeedUser> NewUser(string slug, string first, string last, Guid ownerTenant, AccountKind kind, bool active = true, string? password = null)
        {
            var user = new User($"{slug}.{stamp}@acceptance.invalid", hasher.Hash(password ?? DisposablePassword), first, last, ownerTenant);
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

        // Subjects (what the tests ask ABOUT). WP-INFRA-AUTH-ACCEPTANCE-HOST-01 (P5): also hashed with
        // host.ActorPassword — a fresh per-run random value, never DisposablePassword, never sent anywhere
        // (seed-ready's `subjects` carries only {userId}, no password field at all). Nothing ever logs in as a
        // subject; this only removes the shared constant from the acceptance host's data entirely.
        var human = await NewUser("human", "Zehra", "Yıldız", tenantId, AccountKind.Human, password: host.ActorPassword);
        var unknown = await NewUser("unknown", "Umut", "Kaya", tenantId, AccountKind.Unknown, password: host.ActorPassword);
        var service = await NewUser("service", "Integration", "Bot", tenantId, AccountKind.Service, password: host.ActorPassword);
        var passive = await NewUser("passive", "Pasif", "Demir", tenantId, AccountKind.Human, active: false, password: host.ActorPassword);
        var foreign = await NewUser("foreign", "Fatma", "Öztürk", foreignTenantId, AccountKind.Human, password: host.ActorPassword);
        var mutable = await NewUser("mutable", "Mert", "Çelik", tenantId, AccountKind.Unknown, password: host.ActorPassword);

        // Actors (who ASKS). They are also ordinary users of the disposable tenant. WP-INFRA-AUTH-ACCEPTANCE-HOST-01
        // (C8): hashed with host.ActorPassword (fresh per run), NOT the shared DisposablePassword constant — the
        // acceptance host performs a REAL login as these 4, and the constant must never be the credential a real
        // login accepts.
        var kindAdmin = await NewUser("kind-admin", "Kind", "Admin", tenantId, AccountKind.Unknown, password: host.ActorPassword);
        var pmo = await NewUser("pmo", "Pmo", "Reader", tenantId, AccountKind.Unknown, password: host.ActorPassword);
        var creator = await NewUser("creator", "Only", "Creator", tenantId, AccountKind.Unknown, password: host.ActorPassword);
        var noPermission = await NewUser("nobody", "No", "Permission", tenantId, AccountKind.Unknown, password: host.ActorPassword);

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

    // ── WP-INFRA-AUTH-DISPLAY-LABEL-01 additive fixture ─────────────────────────────────────────────────

    /// <summary>Four disposable subjects for the display-label endpoint's edge cases; see <see cref="SeedDisplayLabelSubjectsAsync"/>.</summary>
    public sealed record DisplayLabelSubjects(SeedUser Unnamed, SeedUser Whitespace, SeedUser EmailUserName, SeedUser LongName);

    // xUnit constructs a new UserDisplayLabelEndpointTests instance per test method, but the host (IClassFixture) is
    // shared across all of them — so this seeds the four subjects into the host's ALREADY-seeded tenant exactly once,
    // no matter how many times InitializeAsync calls it for that same host.
    private static readonly ConditionalWeakTable<AuthTestHost, Task<DisplayLabelSubjects>> DisplayLabelSubjectsCache = new();

    public static Task<DisplayLabelSubjects> SeedDisplayLabelSubjectsAsync(AuthTestHost host) =>
        DisplayLabelSubjectsCache.GetValue(host, static h => SeedDisplayLabelSubjectsCoreAsync(h));

    private static async Task<DisplayLabelSubjects> SeedDisplayLabelSubjectsCoreAsync(AuthTestHost host)
    {
        var tenantId = host.Seeded.TenantId;
        var stamp = Guid.NewGuid().ToString("N")[..8];

        using var scope = host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);

        var users = sp.GetRequiredService<IUserRepository>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        async Task<SeedUser> NewSubject(string slug, string first, string last, string? userName = null)
        {
            // P5 — host.ActorPassword (fresh per run), not the shared DisposablePassword constant.
            var user = new User($"{slug}.{stamp}@acceptance.invalid", hasher.Hash(host.ActorPassword), first, last, tenantId);
            user.ConfirmEmail();
            user.SetAccountKind(AccountKind.Human);
            if (userName is not null)
            {
                user.SetUserName(userName);
            }

            var created = await users.CreateAsync(user, CancellationToken.None);
            return new SeedUser(created.Id, created.Email, created.FirstName, created.LastName);
        }

        var unnamed = await NewSubject("displaylabel-unnamed", "", "");
        var whitespace = await NewSubject("displaylabel-whitespace", "  ", "\t");
        var emailUserName = await NewSubject(
            "displaylabel-emailusername", "", "",
            userName: $"displaylabel-emailusername.{stamp}@acceptance.invalid");
        var longName = await NewSubject("displaylabel-longname", new string('a', 150), new string('b', 150));

        return new DisplayLabelSubjects(unnamed, whitespace, emailUserName, longName);
    }
}
