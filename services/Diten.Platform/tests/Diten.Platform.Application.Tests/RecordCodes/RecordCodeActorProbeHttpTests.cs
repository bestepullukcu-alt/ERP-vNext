using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.API.Observability;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Diten.Platform.Infrastructure.Services.Mdm;
using Diten.Platform.Infrastructure.Settings;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.RecordCodes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/*
 * MOD-0040-FU02 DL-1 — FIRST COMMIT GROUP (AC-41): THE ACTOR OF A RECORD-CODE ALLOCATION IS THE FORWARDED USER.
 *
 * The allocate endpoint is called service to service; the caller forwards the user's bearer token. The audit record must
 * name that user (ActorType = TenantUser, ActorId = the token's sub) — not "Service", not "System" (owner decision K-8r).
 * The in-transaction door names its actor from ITenantAuthorizationContext, which reads the principal when it is built,
 * so the executor makes the validated token the request's user before anything downstream is resolved.
 *
 * Measured over real HTTP through the shipped controller and executor, the production MediatR pipeline, the production
 * in-transaction door (CanonicalTransactionalAuditOutboxWriter) over the production outbox repository and transaction
 * executor on a real replica set, then the production outbox processor into audit_events.
 *
 * Two hosts. (1) The Platform pipeline: the authentication middleware runs first, as UsePlatformAccessPipeline does — in
 * production it already makes the bearer token the request's user. (2) No authentication middleware: only the executor's
 * own explicit validation and its HttpContext.User assignment can name the actor, so this host is where removing that
 * assignment (sabotage S-46) is seen.
 */
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class RecordCodeActorProbeHttpTests : IClassFixture<ReplicaSetClassFixture>
{
    private static readonly Guid Tenant = Guid.Parse("40fc0002-0000-4000-8000-0000000000a1");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000b1");

    private readonly DisposableMongoReplicaSet _mongo;

    public RecordCodeActorProbeHttpTests(ReplicaSetClassFixture replicaSet) => _mongo = replicaSet.Mongo;

    [Fact]
    public async Task Through_the_platform_pipeline_the_record_names_the_forwarded_tenant_user()
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_actor_pipeline");
        using var host = new RecordCodeHttpHost(_mongo.Client, database, authenticationMiddleware: true);
        await ArrangeAsync(host);

        var response = await host.AllocateAsync(
            RecordCodeHttpHost.TenantUserToken(Tenant, User, "finance-treasury.payment-batches.create"), ProbeBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertOneRecordNamingTheForwardedUserAsync(host);
    }

    [Fact]
    public async Task Without_the_authentication_middleware_the_executor_alone_makes_the_forwarded_user_the_actor()
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_actor_executor");
        using var host = new RecordCodeHttpHost(_mongo.Client, database, authenticationMiddleware: false);
        await ArrangeAsync(host);

        var response = await host.AllocateAsync(
            RecordCodeHttpHost.TenantUserToken(Tenant, User, "finance-treasury.payment-batches.create"), ProbeBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertOneRecordNamingTheForwardedUserAsync(host);
    }

    private static readonly Guid Company = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");

    private static async Task ArrangeAsync(RecordCodeHttpHost host)
    {
        await PlatformSchemaManifest.ApplyAsync(host.Database, new[] { SchemaProfile.RecordCodes, SchemaProfile.AccessGovernance });
        await host.SeedTenantAsync(Tenant);
        host.Mdm.Company(Company, "MYG");
    }

    private const string ProbeBody =
        "{\"recordType\":\"PAY\",\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"idempotencyKey\":\"probe-1\"}";

    private static async Task AssertOneRecordNamingTheForwardedUserAsync(RecordCodeHttpHost host)
    {
        var outbox = Assert.Single(await host.Database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox)
            .Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
        Assert.StartsWith("record-code:allocated:", outbox["IdempotencyKey"].AsString);

        Assert.Equal(1, await host.ProcessOutboxAsync());
        var audit = Assert.Single(await host.AuditEventsAsync());
        Assert.Equal(AuditActorType.TenantUser, audit.ActorType);
        Assert.Equal(User, audit.ActorId);
        Assert.Equal(Tenant, audit.TenantId);
        Assert.Equal(Tenant, audit.TargetTenantId);
        Assert.Equal("AllocateRecordCodeCommand", audit.RequestType);
        Assert.Equal("RecordCodeAllocation", audit.EntityType);
        Assert.Equal(AuditOperation.Create, audit.Operation);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(AuditCategory.PlatformConfiguration, audit.Category);
    }
}


/// <summary>What a <see cref="RecordCodeHttpHost"/> is built with; the defaults are production's.</summary>
internal sealed class RecordCodeHostOptions
{
    public bool AuthenticationMiddleware { get; init; } = true;
    public string OrgPrefix { get; init; } = "GMG";
    public int PermitsPerMinute { get; init; } = RecordCodeAllocateRateLimiter.DefaultPermitsPerMinute;

    /// <summary>The platform-administrator record the policies find for an administrator token (null: none).</summary>
    public ActorType? AdministratorRecord { get; init; } = ActorType.PlatformAdmin;

    public IPlatformTransactionFaultProbe? FaultProbe { get; init; }
    public Action<IServiceCollection>? ConfigureServices { get; init; }
}

/// <summary>
/// MOD-0040-FU02 — a real HTTP host for the record-code endpoints over a real replica-set database: the shipped controllers,
/// executor, parser and rate limiter, the production MediatR pipeline (<c>AddApplication</c>), the production repositories,
/// <see cref="PlatformTransactionExecutor"/>, in-transaction door and refusal writer, the production
/// <c>TenantResolutionMiddleware</c>, platform-actor policies and <c>GlobalExceptionHandler</c>, and the production MDM
/// short-code reader over a fake MDM (<see cref="FakeMdm"/>). Tokens are signed with this host's own test key and validated
/// with the Platform's settings (<c>MapInboundClaims = false</c>).
/// </summary>
internal sealed class RecordCodeHttpHost : IDisposable
{
    public const string FinanceIdentifier = "record-code-finance-test";
    public const string FinanceSecret = "record-code-finance-secret-test";
    public const string MdmIdentifier = "record-code-mdm-test";
    public const string MdmSecret = "record-code-mdm-secret-test";
    public const string AllocatePath = "/api/internal/record-codes/allocate";
    public const string Issuer = "diten-auth-record-code-test";
    public const string Audience = "diten-platform-record-code-test";
    private const string Secret = "record code http host, test only, 0123456789abcdef";

    private readonly TestServer _server;

    public RecordCodeHttpHost(IMongoClient client, IMongoDatabase database, bool authenticationMiddleware = true)
        : this(client, database, new RecordCodeHostOptions { AuthenticationMiddleware = authenticationMiddleware })
    {
    }

    public RecordCodeHttpHost(IMongoClient client, IMongoDatabase database, RecordCodeHostOptions options)
    {
        Database = database;
        var dbContext = new PlatformDbContext(client, database);
        var administrators = new Moq.Mock<IPlatformAdministratorRepository>();
        if (options.AdministratorRecord is { } recordType)
        {
            administrators.Setup(x => x.GetByNormalizedEmailAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<CancellationToken>()))
                .ReturnsAsync((string email, CancellationToken _) => new PlatformAdministrator
                {
                    Email = email, NormalizedEmail = email, ActorType = recordType,
                    AllowedTenantIds = recordType == ActorType.PartnerAdmin ? [Guid.NewGuid()] : [],
                    Status = AdministratorStatus.Active
                });
        }

        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(jwt =>
                    {
                        jwt.MapInboundClaims = false;
                        jwt.TokenValidationParameters = ValidationParameters();
                    });
                services.AddAuthorization(Diten.Platform.Infrastructure.DependencyInjection.AddPlatformActorPolicies);
                services.AddPlatformActorTenantScope();
                services.AddSingleton(administrators.Object);
                services.AddHttpContextAccessor();
                services.AddProblemDetails();
                services.AddExceptionHandler<Diten.Platform.API.Middleware.GlobalExceptionHandler>();

                services.AddApplication();
                services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                services.AddScoped<ITenantContext, TenantContext>();
                services.AddSingleton<IAuditOutboxWriter>(new InMemoryAuditOutbox());
                services.AddScoped<ICorrelationContext, CorrelationContext>();

                services.AddSingleton<IPlatformDbContext>(dbContext);
                services.AddScoped<AuditOutboxRepository>();
                services.AddScoped<ITransactionalAuditOutboxStore>(provider => provider.GetRequiredService<AuditOutboxRepository>());
                services.AddScoped<IPlatformTransactionExecutor>(_ => new PlatformTransactionExecutor(dbContext, options.FaultProbe));
                services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
                services.AddScoped<IRecordCodeCounterRepository, RecordCodeCounterRepository>();
                services.AddScoped<IRecordCodeAllocationRepository, RecordCodeAllocationRepository>();
                services.AddScoped<IRecordCodeGroupPrefixRepository, RecordCodeGroupPrefixRepository>();
                services.Configure<DocumentCodingOptions>(coding => coding.OrgPrefix = options.OrgPrefix);
                services.Configure<MdmServiceOptions>(mdm => mdm.BaseUrl = "http://mdm.record-code.test/");
                services.AddHttpClient<ILegalEntityShortCodeReader, MdmLegalEntityShortCodeReader>()
                    .ConfigurePrimaryHttpMessageHandler(() => Mdm);

                services.AddSingleton(TimeProvider.System);
                services.Configure<TrustedRecordCodeCredentialOptions>(credentials =>
                {
                    credentials.FinanceTreasury = Binding(FinanceIdentifier, FinanceSecret, "DITENFINANCETREASURYSERVICE",
                        "PAY", "APD", "ARI", "CPS", "CTP");
                    credentials.Mdm = Binding(MdmIdentifier, MdmSecret, "DITENMDMSERVICE", "BNK", "BAC", "AGR");
                });
                services.AddSingleton<ITrustedRecordCodeCredentialAuthenticator, TrustedRecordCodeCredentialAuthenticator>();
                services.AddScoped<ITrustedRecordCodeJwtContext, TrustedRecordCodeJwtContext>();
                services.AddScoped<ITrustedRecordCodeRequestExecutor, TrustedRecordCodeRequestExecutor>();
                services.AddSingleton(new RecordCodeAllocateRateLimiter(options.PermitsPerMinute));

                services.AddControllers().AddApplicationPart(typeof(InternalRecordCodesController).Assembly);

                options.ConfigureServices?.Invoke(services);
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseRouting();
                if (options.AuthenticationMiddleware)
                {
                    app.UseAuthentication();
                }

                app.UseTenantResolution();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
    }

    public IMongoDatabase Database { get; }

    /// <summary>A host over a fresh database with the record-code and audit schema applied and the tenants seeded Active.</summary>
    public static async Task<RecordCodeHttpHost> StartAsync(
        DisposableMongoReplicaSet mongo, string databaseName, RecordCodeHostOptions? options = null, params Guid[] activeTenants)
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(mongo, databaseName);
        await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.RecordCodes, SchemaProfile.AccessGovernance });
        var host = new RecordCodeHttpHost(mongo.Client, database, options ?? new RecordCodeHostOptions());
        foreach (var tenant in activeTenants)
        {
            await host.SeedTenantAsync(tenant);
        }

        return host;
    }

    /// <summary>The allocate body as the wire carries it; null members are left out (a v0 body has no prefixScope).</summary>
    public static string Body(string recordType, string? prefixScope, Guid? legalEntityId, string idempotencyKey)
    {
        var members = new List<string> { "\"recordType\":\"" + recordType + "\"" };
        if (prefixScope is not null) members.Add("\"prefixScope\":\"" + prefixScope + "\"");
        if (legalEntityId is not null) members.Add("\"legalEntityId\":\"" + legalEntityId + "\"");
        members.Add("\"idempotencyKey\":\"" + idempotencyKey + "\"");
        return "{" + string.Join(",", members) + "}";
    }

    /// <summary>The envelope's status, reason_code and data (code, sequence) — or the ProblemDetails' reason_code.</summary>
    public static async Task<RecordCodeAnswer> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(string.IsNullOrEmpty(text) ? "{}" : text);
        var root = document.RootElement;
        string? reason = root.TryGetProperty("reason_code", out var r) && r.ValueKind == System.Text.Json.JsonValueKind.String ? r.GetString() : null;
        string? code = null;
        long? sequence = null;
        string[] dataMembers = [];
        if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            dataMembers = data.EnumerateObject().Select(member => member.Name).ToArray();
            code = data.TryGetProperty("code", out var c) ? c.GetString() : null;
            sequence = data.TryGetProperty("sequence", out var q) ? q.GetInt64() : null;
        }

        var isEnvelope = root.TryGetProperty("isSuccessful", out _);
        return new RecordCodeAnswer((int)response.StatusCode, reason, code, sequence, dataMembers, isEnvelope, text);
    }

    /// <summary>The fake MDM lookup-validation the production reader calls.</summary>
    public FakeMdm Mdm { get; } = new();

    public IServiceProvider Services => _server.Services;

    /// <summary>The Platform's token validation settings, over this host's test signing key.</summary>
    public static TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
        ClockSkew = TimeSpan.Zero
    };

    public static TrustedRecordCodeCredentialBinding Binding(
        string identifier, string secret, string consumerService, params string[] recordTypes) => new()
    {
        Identifier = identifier,
        ActiveSecret = secret,
        ConsumerService = consumerService,
        AllowedAudience = TrustedRecordCodeCredentialAuthenticator.Audience,
        AllowedRecordTypes = [.. recordTypes]
    };

    /// <summary>POSTs <paramref name="json"/> as the FinanceTreasury consumer (unless other credential headers are given).</summary>
    public async Task<HttpResponseMessage> AllocateAsync(
        string? bearerToken,
        string json,
        string identifier = FinanceIdentifier,
        string secret = FinanceSecret,
        Action<HttpRequestMessage>? configure = null,
        string path = AllocatePath)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(TrustedRecordCodeRequestExecutor.CredentialIdHeader, identifier);
        request.Headers.TryAddWithoutValidation(TrustedRecordCodeRequestExecutor.CredentialSecretHeader, secret);
        request.Headers.TryAddWithoutValidation(
            TrustedRecordCodeRequestExecutor.AudienceHeader, TrustedRecordCodeCredentialAuthenticator.Audience);
        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        configure?.Invoke(request);
        return await _server.CreateClient().SendAsync(request);
    }

    /// <summary>As the MDM consumer.</summary>
    public Task<HttpResponseMessage> AllocateAsMdmAsync(string? bearerToken, string json) =>
        AllocateAsync(bearerToken, json, MdmIdentifier, MdmSecret);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? bearerToken, string? json = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await _server.CreateClient().SendAsync(request);
    }

    /// <summary>A tenant-user token as AuthService mints it, holding exactly the permissions named.</summary>
    public static string TenantUserToken(Guid tenant, Guid user, params string[] permissions) =>
        Token("tenant_user", tenant.ToString(), user, permissions);

    /// <summary>A signed token with the given <c>actor_type</c> and, when not null, that literal <c>tenant_id</c>.</summary>
    public static string Token(string actorType, string? tenantClaim, Guid user, params string[] permissions) =>
        Token(actorType, tenantClaim, user.ToString(), permissions);

    /// <summary>A tenant-user token carrying exactly the extra claims given (claim type, value) — e.g. a <c>permissions</c> claim.</summary>
    public static string TenantUserTokenWithClaims(Guid tenant, Guid user, params (string Type, string Value)[] extra) =>
        Token("tenant_user", tenant.ToString(), user.ToString(), [], extra);

    public static string Token(string? actorType, string? tenantClaim, string? subject, params string[] permissions) =>
        Token(actorType, tenantClaim, subject, permissions, []);

    private static string Token(string? actorType, string? tenantClaim, string? subject, string[] permissions, (string Type, string Value)[] extra)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Email, "record.code@tenant.example"),
            new(JwtRegisteredClaimNames.GivenName, "Record"),
            new(JwtRegisteredClaimNames.FamilyName, "Code")
        };
        if (subject is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
        if (actorType is not null) claims.Add(new Claim("actor_type", actorType));
        if (tenantClaim is not null) claims.Add(new Claim("tenant_id", tenantClaim));
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        claims.AddRange(extra.Select(claim => new Claim(claim.Type, claim.Value)));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A tenant row in the registry, as the Platform keeps it.</summary>
    public Task SeedTenantAsync(Guid tenantId, TenantStatus status = TenantStatus.Active, bool deleted = false) =>
        Database.GetCollection<Tenant>("tenants").InsertOneAsync(new Tenant
        {
            Id = tenantId, Code = "RC" + tenantId.ToString("N")[..6], Slug = "rc-" + tenantId.ToString("N")[..8], Name = "Record code",
            DisplayName = "Record code", Domain = tenantId.ToString("N")[..8] + ".local", Status = status, IsDeleted = deleted
        });

    /// <summary>The tenant's group-prefix setting as an alignment would leave it (locked when <paramref name="locked"/>).</summary>
    public Task SeedGroupPrefixAsync(Guid tenantId, string prefix, bool locked = false) =>
        Database.GetCollection<RecordCodeGroupPrefix>("record_code_group_prefixes").InsertOneAsync(new RecordCodeGroupPrefix
        {
            TenantId = tenantId, GroupPrefix = prefix, AlignedAtUtc = DateTime.UtcNow, AlignedBy = "seed",
            LockedAtUtc = locked ? DateTime.UtcNow : null
        });

    public IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    public Task<long> CountAsync(string collection) =>
        Collection(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    /// <summary>One pass of the production outbox processor (the worker's unit of work).</summary>
    public Task<int> ProcessOutboxAsync()
    {
        var tenantContext = new TenantContext();
        var dbContext = new PlatformDbContext(Database.Client, Database);
        return new AuditOutboxProcessor(new AuditOutboxRepository(dbContext), new AuditEventRepository(Database, tenantContext),
            tenantContext, new AuditOutboxPayloadMapper(),
            new AuditOutboxWorkerOptions { BatchSize = 200, MaxAttempts = 5, InitialRetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(5) },
            NullLogger<AuditOutboxProcessor>.Instance).ProcessBatchAsync();
    }

    public async Task<IReadOnlyList<AuditEvent>> AuditEventsAsync() =>
        await Database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents).Find(FilterDefinition<AuditEvent>.Empty).ToListAsync();

    public async Task<IReadOnlyList<AuditEvent>> DeliveredAuditEventsAsync()
    {
        await ProcessOutboxAsync();
        return await AuditEventsAsync();
    }

    public void Dispose() => _server.Dispose();
}

/// <summary>
/// A fake MDM lookup-validation: one answer per legal entity (default 404), and every request it received with the
/// <c>X-Tenant-Id</c> and <c>Authorization</c> it carried.
/// </summary>
internal sealed class FakeMdm : HttpMessageHandler
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Func<Guid, HttpResponseMessage>> _answers = new();

    public System.Collections.Concurrent.ConcurrentQueue<(Guid LegalEntityId, string? Tenant, string? Authorization)> Requests { get; } = new();

    /// <summary>Held before answering (a slow MDM); null = answer at once.</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Awaited before answering, when set (a test holds a request at step 11).</summary>
    public Func<Task>? Gate { get; set; }

    public void Company(Guid legalEntityId, string code, string? approvalStatusJson = null, string lifecycle = "ACTIVE", bool referenceable = true) =>
        _answers[legalEntityId] = id => Json(200,
            "{\"data\":{\"legalEntityId\":\"" + id + "\",\"code\":" + System.Text.Json.JsonSerializer.Serialize(code)
            + ",\"legalName\":\"Co\",\"displayName\":null,\"lifecycleState\":\"" + lifecycle + "\",\"referenceable\":"
            + (referenceable ? "true" : "false") + (approvalStatusJson is null ? string.Empty : ",\"approvalStatus\":" + approvalStatusJson)
            + "},\"statusCode\":200,\"isSuccessful\":true,\"errors\":[]}");

    public void Answer(Guid legalEntityId, int statusCode, string body = "{}") =>
        _answers[legalEntityId] = _ => Json(statusCode, body);

    public void Throw(Guid legalEntityId) =>
        _answers[legalEntityId] = _ => throw new HttpRequestException("MDM is down (test).");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = Guid.Parse(segments[2]);
        Requests.Enqueue((id,
            request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? string.Join(",", tenant) : null,
            request.Headers.Authorization?.ToString()));
        if (Gate is { } gate) await gate();
        if (Delay is { } delay) await Task.Delay(delay, cancellationToken);
        return _answers.TryGetValue(id, out var answer) ? answer(id) : Json(404, "{}");
    }

    private static HttpResponseMessage Json(int statusCode, string body) =>
        new((HttpStatusCode)statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

internal sealed record RecordCodeAnswer(
    int Status, string? Reason, string? Code, long? Sequence, string[] DataMembers, bool IsEnvelope, string Raw);
