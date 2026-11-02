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
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
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

        var response = await host.AllocateAsync(RecordCodeHttpHost.TenantUserToken(Tenant, User), ProbeBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertOneRecordNamingTheForwardedUserAsync(host);
    }

    [Fact]
    public async Task Without_the_authentication_middleware_the_executor_alone_makes_the_forwarded_user_the_actor()
    {
        var database = await ReplicaSetClassFixture.FreshDatabaseAsync(_mongo, "diten_record_code_actor_executor");
        using var host = new RecordCodeHttpHost(_mongo.Client, database, authenticationMiddleware: false);

        var response = await host.AllocateAsync(RecordCodeHttpHost.TenantUserToken(Tenant, User), ProbeBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertOneRecordNamingTheForwardedUserAsync(host);
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

/// <summary>
/// MOD-0040-FU02 — a real HTTP host for the record-code endpoints over a real replica-set database: the shipped
/// controllers and executor, the production MediatR pipeline (<c>AddApplication</c>), the production in-transaction door
/// over the production outbox repository and <see cref="PlatformTransactionExecutor"/>. Tokens are signed with this host's
/// own test key and validated with the Platform's settings (<c>MapInboundClaims = false</c>).
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

    public RecordCodeHttpHost(
        IMongoClient client,
        IMongoDatabase database,
        bool authenticationMiddleware = true,
        Action<IServiceCollection>? configureServices = null)
    {
        Database = database;
        var dbContext = new PlatformDbContext(client, database);

        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters = ValidationParameters();
                    });
                services.AddAuthorization();
                services.AddHttpContextAccessor();

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
                services.AddScoped<IPlatformTransactionExecutor, PlatformTransactionExecutor>();

                services.AddSingleton(TimeProvider.System);
                services.Configure<TrustedRecordCodeCredentialOptions>(options =>
                {
                    options.FinanceTreasury = Binding(FinanceIdentifier, FinanceSecret, "DITENFINANCETREASURYSERVICE",
                        "PAY", "APD", "ARI", "CPS", "CTP");
                    options.Mdm = Binding(MdmIdentifier, MdmSecret, "DITENMDMSERVICE", "BNK", "BAC", "AGR");
                });
                services.AddSingleton<ITrustedRecordCodeCredentialAuthenticator, TrustedRecordCodeCredentialAuthenticator>();
                services.AddScoped<ITrustedRecordCodeJwtContext, TrustedRecordCodeJwtContext>();
                services.AddScoped<ITrustedRecordCodeRequestExecutor, TrustedRecordCodeRequestExecutor>();

                services.AddControllers().AddApplicationPart(typeof(InternalRecordCodesController).Assembly);

                configureServices?.Invoke(services);
            })
            .Configure(app =>
            {
                app.UseRouting();
                if (authenticationMiddleware)
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
        Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AllocatePath)
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

    /// <summary>A tenant-user token as AuthService mints it, holding exactly the permissions named.</summary>
    public static string TenantUserToken(Guid tenant, Guid user, params string[] permissions) =>
        Token("tenant_user", tenant.ToString(), user, permissions);

    /// <summary>A signed token with the given <c>actor_type</c> and, when not null, that literal <c>tenant_id</c>.</summary>
    public static string Token(string actorType, string? tenantClaim, Guid user, params string[] permissions) =>
        Token(actorType, tenantClaim, user.ToString(), permissions);

    public static string Token(string? actorType, string? tenantClaim, string? subject, params string[] permissions)
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

    /// <summary>One pass of the production outbox processor (the worker's unit of work).</summary>
    public Task<int> ProcessOutboxAsync()
    {
        var tenantContext = new TenantContext();
        var dbContext = new PlatformDbContext(Database.Client, Database);
        return new AuditOutboxProcessor(new AuditOutboxRepository(dbContext), new AuditEventRepository(Database, tenantContext),
            tenantContext, new AuditOutboxPayloadMapper(),
            new AuditOutboxWorkerOptions { BatchSize = 50, MaxAttempts = 5, InitialRetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(5) },
            NullLogger<AuditOutboxProcessor>.Instance).ProcessBatchAsync();
    }

    public async Task<IReadOnlyList<AuditEvent>> AuditEventsAsync() =>
        await Database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents).Find(FilterDefinition<AuditEvent>.Empty).ToListAsync();

    public void Dispose() => _server.Dispose();
}
