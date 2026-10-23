using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.SandopPlans;
using CapacityDomain = Diten.SupplyChainService.Domain.Features.CapacityPlans;
using CapacityInfra = Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using SandopDomain = Diten.SupplyChainService.Domain.Features.SandopPlans;
using SandopInfra = Diten.SupplyChainService.Infrastructure.Features.SandopPlans;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// The bounded-slice HTTP host for MOD-0190 and MOD-0192. Their acceptance lines ask for things only a
/// request can answer — 404 for a foreign scope, 403 for a missing permission, the correlation/401 fallback,
/// and the six declared operations of each module — and until now neither suite ever sent one: the whole of
/// SandopPlans and CapacityPlans drives repositories directly.
///
/// This host does NOT change the production composition. Program.cs still excludes both modules from MediatR
/// and still refuses to register the demand fixture reader, because the only implementation is a
/// test-only fixture and a module that silently refuses everything is worse than one plainly unreachable
/// (K4, ledger Q273). The §22 composition uptake gate (Q292) binds the production host and stays shut; the
/// owner read it on 2026-10-11 as not binding a test host, which is what this is.
///
/// Two seams make it work, both measured on 2026-10-11:
///   * Program.cs:115 hands ComposedFeatureControllerFilter the live IServiceCollection, and the filter runs
///     when controllers are first discovered — after ConfigureServices. Registering the fixture readers and
///     the handlers here is therefore enough for it to keep both controllers.
///   * The MediatR exclusion in Application/DependencyInjection.cs is a private static array, so it cannot be
///     overridden. The handlers are registered directly instead, which also avoids a second AddMediatR call
///     duplicating every pipeline behaviour.
///
/// The scope is not free: CapacityPlans' fixture readers are authorities only in one exact scope, so every
/// request here is scoped to <see cref="Tenant"/>/<see cref="LegalEntity"/>, and SandopPlans' demand fixture
/// is registered for that same pair so one token serves both modules.
/// </summary>
public sealed class SandopCapacityHttpHost : WebApplicationFactory<Program>
{
    private const string Secret = "sandop-capacity-bounded-slice-host-signing-key-01234567";

    /// <summary>The one scope the CapacityPlans fixtures recognise. Anything else is a foreign scope by design.</summary>
    public static Guid Tenant => CapacityInfra.DemandFixtureReader.Tenant;
    public static Guid LegalEntity => CapacityInfra.DemandFixtureReader.LegalEntity;

    public Guid Actor { get; } = Guid.NewGuid();

    private readonly Dictionary<string, string?> _settings;

    public SandopCapacityHttpHost()
    {
        // One lane mongod serves the host; both modules' collections live in its own database. There is no
        // default: 27017-27021 is the operational band and the suites fail closed without a lane URI.
        var mongo = Environment.GetEnvironmentVariable("MVP6_MOD0190_MONGO_URI")
                    ?? Environment.GetEnvironmentVariable("MVP6_MOD0192_MONGO_URI")
                    ?? throw new InvalidOperationException(
                        "Set MVP6_MOD0190_MONGO_URI (or MVP6_MOD0192_MONGO_URI) to the lane's loopback replica set; "
                        + "scripts/test-env/mvp6-test-mongo-env.sh prints it. There is no fallback port.");
        _settings = new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = mongo,
            ["Mongo:DatabaseName"] = "DitenSupplyChain_Mod0190_0192_Http_Test"
        };
        foreach (var (key, value) in AuthServiceToken.JwtSettings(Secret)) _settings[key] = value;
        DropOwnCollections(mongo);
    }

    /// <summary>
    /// The host's database outlives the process, and a plan is unique per horizon and demand version, so without
    /// this the second run of the suite answers 409 SANDOP_PLAN_ALREADY_EXISTS on every create — green alone,
    /// red the next time, which is the worst kind of test. The CapacityPlans suites drop their collections the
    /// same way. Only this host's own database is touched; nothing else on the lane mongod is.
    /// </summary>
    private static int _dropped;

    private void DropOwnCollections(string mongo)
    {
        // Each test class gets its own IClassFixture instance, so a second host would otherwise drop the
        // first one's data while its tests are still running. Once per process is the whole intent.
        if (Interlocked.Exchange(ref _dropped, 1) == 1) return;
        var database = new MongoDB.Driver.MongoClient(mongo).GetDatabase(_settings["Mongo:DatabaseName"]);
        var names = database.ListCollectionNames();
        while (names.MoveNext())
            foreach (var name in names.Current)
                database.DropCollection(name);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(_settings));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(_settings));
        builder.ConfigureServices(s =>
        {
            s.StubLegalEntityValidation();
            s.AddSandopPersistence();
            s.AddCapacityPersistence();
            s.AddScoped<SandopDomain.IDemandFixtureReader>(_ => new SandopInfra.DemandFixtureReader(
                [new SandopInfra.DemandFixture(Tenant, LegalEntity, "dp-2027", "3", "sha256:abc", true)]));
            s.AddScoped<CapacityDomain.IDemandFixtureReader, CapacityInfra.DemandFixtureReader>();
            s.AddScoped<CapacityDomain.IConstraintFixtureReader, CapacityInfra.ConstraintFixtureReader>();
            RegisterExcludedHandlers(s);
        });
    }

    /// <summary>
    /// Application/DependencyInjection.cs excludes both feature namespaces from MediatR's TypeEvaluator.
    /// The array is private static, so the handlers are registered by hand rather than by a second AddMediatR,
    /// which would register every IPipelineBehavior a second time and run validation twice per request.
    /// </summary>
    private static void RegisterExcludedHandlers(IServiceCollection services)
    {
        string[] namespaces =
        [
            "Diten.SupplyChainService.Application.Features.SandopPlans",
            "Diten.SupplyChainService.Application.Features.CapacityPlans"
        ];
        var candidates = typeof(RequestContext).Assembly.GetTypes().Where(t =>
            t is { IsClass: true, IsAbstract: false } && t.Namespace is { } ns
            && namespaces.Any(n => ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal)));
        var registered = 0;
        foreach (var type in candidates)
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType
                         && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                             || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>))))
            {
                services.AddTransient(contract, type);
                registered++;
            }
        if (registered == 0)
            throw new InvalidOperationException(
                "No SandopPlans/CapacityPlans request handler was found to register. Either the namespaces moved "
                + "or the handlers were deleted; this host would otherwise start with both controllers dropped.");
    }

    /// <summary>
    /// The service's own database, resolved from its container, so a test reads back exactly what the request
    /// wrote rather than a second client's view of it.
    /// </summary>
    public MongoDB.Driver.IMongoDatabase Database => Services.GetRequiredService<MongoDB.Driver.IMongoDatabase>();

    /// <summary>
    /// The token AuthService issues since R-2: tenant, actor and permissions — no legal_entity_id. Both gates were
    /// aligned to that on 2026-10-18 (owner decision); before, they required the claim and refused every real user,
    /// which this host could not see because it minted the claim itself. The legal entity travels in
    /// X-Legal-Entity-Id (see <see cref="Send"/>).
    /// </summary>
    public string Token(Guid? tenant = null, IEnumerable<string>? permissions = null, Guid? actor = null) =>
        AuthServiceToken.Mint(Secret, tenant ?? Tenant, actor ?? Actor, permissions ?? AllPermissions);

    /// <summary>The MDM legal-entity stub every request of this host consults; a test may set its outcome.</summary>
    public StubLegalEntityScopeValidator LegalEntityValidator =>
        (StubLegalEntityScopeValidator)Services.GetRequiredService<ILegalEntityScopeValidator>();

    public static IReadOnlyList<string> AllPermissions =>
    [
        SandopInfra.SandopPermissions.Read, SandopInfra.SandopPermissions.Create,
        SandopInfra.SandopPermissions.Capture, SandopInfra.SandopPermissions.SignOff,
        CapacityInfra.CapacityPermissions.Read, CapacityInfra.CapacityPermissions.Create,
        CapacityInfra.CapacityPermissions.ScenarioCreate, CapacityInfra.CapacityPermissions.Evaluate
    ];

    public sealed record Reply(int Status, string? Body, string? Correlation)
    {
        public JsonNode? Json => string.IsNullOrWhiteSpace(Body) ? null : JsonNode.Parse(Body);
        // Both modules nest the code: {"error":{"code":…},"contractVersion":"v1"}. Reading a top-level
        // "code" silently returns null for every real error envelope, which makes an assertion on it vacuous.
        public string? Code => Json?["error"]?["code"]?.GetValue<string>();
    }

    /// <summary>
    /// Both gates read X-Tenant-Id and X-Legal-Entity-Id through a helper rather than the Headers indexer, so a
    /// grep for Headers["..."] misses them (it did, on 2026-10-11). X-Legal-Entity-Id is required by both (400
    /// INVALID_REQUEST when absent) and is the legal entity's only source since the R-2 alignment; a tenant header that
    /// disagrees with the token is 403 FORBIDDEN, so a foreign-tenant test moves the token AND the header together.
    /// </summary>
    public async Task<Reply> Send(string method, string path, JsonNode? body = null, string? token = null,
        string? idempotencyKey = "k", string? correlation = null, Action<HttpRequestMessage>? alter = null,
        Guid? tenant = null, Guid? legalEntity = null)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", (tenant ?? Tenant).ToString());
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", (legalEntity ?? LegalEntity).ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation ?? Guid.NewGuid().ToString());
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        alter?.Invoke(request);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var header = response.Headers.TryGetValues("X-Correlation-Id", out var values) ? values.FirstOrDefault() : null;
        return new Reply((int)response.StatusCode, text, header);
    }

    private static int _horizon;

    /// <summary>
    /// A plan is unique per horizon and demand version, so every call gets its own horizon year — otherwise the
    /// second create in a run answers 409 SANDOP_PLAN_ALREADY_EXISTS and the test reads as a routing failure.
    /// </summary>
    public static JsonObject SandopPlan()
    {
        var year = 2100 + Interlocked.Increment(ref _horizon);
        return new JsonObject
        {
            ["name"] = $"FY{year}", ["horizonStart"] = $"{year}-01-01", ["horizonEnd"] = $"{year}-12-31",
            ["demandPlanId"] = "dp-2027", ["demandPlanVersion"] = "3"
        };
    }

    public static JsonObject CapacityPlan() => new()
    {
        ["name"] = "Exact fixture", ["horizonStart"] = "2027-01-01", ["horizonEnd"] = "2027-12-31",
        ["demandPlanId"] = "dp-2027", ["demandPlanVersion"] = "3",
        ["sourceCapturedAt"] = "2026-09-15T09:45:00Z", ["sourceChecksum"] = "sha256:ee56d4f9a3c8"
    };
}
