using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Controllers.ESignature;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.ESignature.Handlers;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Diten.Platform.Infrastructure.Services.ESignature;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E2 — one HTTP host per test class over a TEST-OWNED single-node replica set (never 27017; transactions need
/// a replica set). What runs is production: JWT bearer validation with the Platform's settings, the real tenant middleware,
/// <c>[HasPermission]</c>, the shipped controllers, the MediatR pipeline (<c>AddApplication</c>), the ESign repositories,
/// the schema manifest's ESignature + AccessGovernance indexes, the transactional audit outbox and the local-disk content
/// store (in a temp root this class owns). Fakes stand only at the edges this slice does not own: AuthService's ticket
/// consume, the subject provider (a tenant-aware in-memory test provider), MDM / AuthService user and company lookups,
/// and the SEC-002 data scope.
/// </summary>
public sealed class ESignatureTestHost : IAsyncLifetime
{
    private const string Issuer = "diten-auth-esign-test";
    private const string Audience = "diten-platform-esign-test";
    private const string Secret = "esign e2 test host secret, test only, 0123456789abcdef";
    public const string InternalKey = "esign-e2-internal-key-test-only-0123456789";

    private DisposableMongoReplicaSet? _mongo;
    private TestServer? _server;

    public IMongoDatabase Database { get; private set; } = null!;
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "diten-esign-e2-tests", Path.GetRandomFileName());
    public FakeTicketConsumer Tickets { get; } = new();
    public TestSubjectProvider Subjects { get; } = new();
    public FakeUserReferences Users { get; } = new();
    public FakeLegalEntities LegalEntities { get; } = new();
    public FakeDataScopes Scopes { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();
    public IServiceProvider Services => _server!.Services;

    public async Task InitializeAsync()
    {
        _mongo = await DisposableMongoReplicaSet.StartAsync();
        Database = _mongo.CreateDatabase();
        await PlatformSchemaManifest.ApplyAsync(Database, new[] { SchemaProfile.ESignature, SchemaProfile.AccessGovernance });
        Directory.CreateDirectory(StorageRoot);

        var dbContext = new PlatformDbContext(_mongo.Client, Database);
        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureLogging(logging => logging.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace))
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthService:InternalApiKey"] = InternalKey
            }))
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                        ValidIssuer = Issuer, ValidAudience = Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                        ClockSkew = TimeSpan.Zero
                    };
                });
                services.AddAuthorization();
                services.AddHttpContextAccessor();

                services.AddApplication();
                services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                services.AddScoped<ITenantContext, TenantContext>();
                services.AddScoped<ICorrelationContext>(_ =>
                {
                    var correlation = new CorrelationContext();
                    correlation.SetCorrelationId("esign-e2-test");
                    return correlation;
                });

                // Persistence — the production classes over the test-owned replica set.
                services.AddSingleton<IPlatformDbContext>(dbContext);
                services.AddScoped<IPlatformTransactionExecutor>(_ => new PlatformTransactionExecutor(dbContext));
                services.AddScoped<IESignatureRecordRepository, ESignatureRecordRepository>();
                services.AddScoped<IESignatureInvalidationRepository, ESignatureInvalidationRepository>();
                services.AddScoped<IESignatureExportRepository, ESignatureExportRepository>();
                services.AddScoped<IESignaturePolicyChangeRepository, ESignaturePolicyChangeRepository>();
                services.AddScoped<IESignRequestRepository, ESignRequestRepository>();
                services.AddScoped<IESignPolicyRepository, ESignPolicyRepository>();
                services.AddScoped<IESignMeaningRepository, ESignMeaningRepository>();
                services.AddScoped<IESignActionRepository, ESignActionRepository>();
                services.AddScoped<IESignNumberGenerator, ESignNumberGenerator>();
                services.AddScoped<AuditOutboxRepository>();
                services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                services.AddScoped<ITransactionalAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                services.Configure<ContentStorageOptions>(o => o.RootPath = StorageRoot);
                services.AddScoped<IContentStorageGateway, LocalFileSystemContentStorageGateway>();
                services.AddSingleton<IESignaturePdfRenderer, MigraDocESignaturePdfRenderer>();
                services.AddScoped<IESignRequestLanguage, HttpESignRequestLanguage>();

                // Edges.
                services.AddSingleton<IESignTicketConsumer>(Tickets);
                services.AddSingleton(Subjects);
                // Only the test module here: the Document Management provider needs the FU23 repositories (its own test).
                services.RemoveAll<ISignableSubjectProvider>();
                services.AddScoped<ISignableSubjectProvider>(sp => new TenantBoundTestProvider(Subjects, sp.GetRequiredService<ITenantContext>()));
                services.AddSingleton<IRemoteSignableSubjectClient>(new NoRemoteProviders());
                services.AddSingleton<IUserReferenceValidator>(Users);
                services.AddSingleton<ILegalEntityReferenceValidator>(LegalEntities);
                services.AddSingleton<IDataScopeResolver>(Scopes);

                services.AddControllers().AddApplicationPart(typeof(ESignatureSignaturesController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseTenantResolution();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
    }

    public async Task DisposeAsync()
    {
        _server?.Dispose();
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }

        try
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public HttpClient Client(string? token = null, string? acceptLanguage = "tr")
    {
        var client = _server!.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        }

        return client;
    }

    public static string Token(Guid tenant, Guid user, IEnumerable<string> permissions, string actorType = "tenant_user", string? name = "Ayşe Demir")
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.ToString()),
            new(JwtRegisteredClaimNames.Email, $"user.{user:N}@tenant.example"),
            new("name", name ?? "Signer"),
            new("actor_type", actorType),
            new("tenant_id", tenant.ToString()),
            new("pwd_change_required", "false")
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var token = new JwtSecurityToken(Issuer, Audience, claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ── edges ────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>AuthService's consume, as a registry of issued tickets: one consume per ticket, bound to tenant + user + intent.</summary>
public sealed class FakeTicketConsumer : IESignTicketConsumer
{
    private readonly ConcurrentDictionary<string, (Guid Tenant, Guid User, string Intent, ESignatureReauthKind Kind, DateTime VerifiedAt)> _issued = new();
    private int _calls;

    public bool Unavailable { get; set; }
    public int Calls => _calls;

    public string Issue(Guid tenant, Guid user, string intent = "sign", ESignatureReauthKind kind = ESignatureReauthKind.Password, DateTime? verifiedAt = null)
    {
        var ticket = Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _issued[ticket] = (tenant, user, intent, kind, verifiedAt ?? DateTime.UtcNow);
        return ticket;
    }

    public Task<TicketConsumeResult> ConsumeAsync(string ticket, Guid tenantId, Guid userId, string intent, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        if (Unavailable)
        {
            return Task.FromResult(TicketConsumeResult.Unavailable);
        }

        if (_issued.TryGetValue(ticket, out var issued) && issued.Tenant == tenantId && issued.User == userId && issued.Intent == intent
            && _issued.TryRemove(ticket, out _))
        {
            return Task.FromResult(new TicketConsumeResult(TicketConsumeOutcome.Consumed, issued.Kind, issued.VerifiedAt, DateTime.UtcNow));
        }

        return Task.FromResult(TicketConsumeResult.Invalid);
    }
}

/// <summary>In-memory records of a fictional consumer module <c>esign-test</c>, keyed by tenant — a record of tenant A does not exist for tenant B.</summary>
public sealed class TestSubjectProvider
{
    public const string Module = "esign-test";
    public const string SubjectType = "esign-test.document";

    private readonly ConcurrentDictionary<(Guid Tenant, string Id), SignableSubject> _records = new();

    public string Add(Guid tenant, Guid? legalEntityId = null, Dictionary<string, string?>? fields = null)
    {
        var id = "DOC-" + Guid.NewGuid().ToString("N")[..10];
        _records[(tenant, id)] = new SignableSubject($"Test document {id}", [new SignableSubjectLine("Ürün", "Parasetamol 500 mg")], legalEntityId,
            fields ?? new Dictionary<string, string?> { ["status"] = "draft", ["quantity"] = "1200", ["lot"] = "PRS-26A014" });
        return id;
    }

    public void Change(Guid tenant, string id, string field, string? value)
    {
        var current = _records[(tenant, id)];
        var fields = new Dictionary<string, string?>(current.Fields) { [field] = value };
        _records[(tenant, id)] = current with { Fields = fields };
    }

    public void Remove(Guid tenant, string id) => _records.TryRemove((tenant, id), out _);

    public SignableSubjectResolution Resolve(Guid tenant, string type, string id) =>
        type == SubjectType && _records.TryGetValue((tenant, id), out var subject)
            ? SignableSubjectResolution.Found(subject)
            : SignableSubjectResolution.NotFound;
}

internal sealed class TenantBoundTestProvider(TestSubjectProvider records, ITenantContext tenant) : ISignableSubjectProvider
{
    public string Module => TestSubjectProvider.Module;

    public Task<SignableSubjectResolution> ResolveAsync(string subjectType, string subjectId, CancellationToken ct) =>
        Task.FromResult(records.Resolve(tenant.TenantId, subjectType, subjectId));
}

internal sealed class NoRemoteProviders : IRemoteSignableSubjectClient
{
    public bool IsConfigured(string module) => false;

    public Task<SignableSubjectResolution> ResolveAsync(string module, string subjectType, string subjectId, CancellationToken ct) =>
        Task.FromResult(SignableSubjectResolution.NotFound);
}

public sealed class FakeUserReferences : IUserReferenceValidator
{
    public ConcurrentDictionary<Guid, bool> Known { get; } = new();

    public Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Known.ContainsKey(userId)
            ? Response<UserReferenceDto>.Success(new UserReferenceDto(userId, true))
            : Response<UserReferenceDto>.Fail("not found", 404));
}

public sealed class FakeLegalEntities : ILegalEntityReferenceValidator
{
    public ConcurrentDictionary<Guid, string> Known { get; } = new();

    public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
        Task.FromResult(Known.TryGetValue(legalEntityId, out var name)
            ? Response<LegalEntityReferenceDto>.Success(new LegalEntityReferenceDto(legalEntityId, name, name, "Active", true))
            : Response<LegalEntityReferenceDto>.Fail("not found", 404));
}

public sealed class FakeDataScopes : IDataScopeResolver
{
    public ConcurrentDictionary<Guid, Guid[]> LegalEntitiesByUser { get; } = new();

    public Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken ct)
    {
        IReadOnlyList<EntitlementDataScope> scopes = LegalEntitiesByUser.TryGetValue(userId, out var ids)
            ? ids.Select(id => new EntitlementDataScope(EntitlementDataScopeKind.LegalEntity, id, null)).ToList()
            : [];
        return Task.FromResult(scopes);
    }
}

/// <summary>Every log line of the host, rendered with its arguments — the log-capture guard reads this.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public string Text => string.Join("\n", _lines);

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            lines.Enqueue($"{logLevel} {category} {formatter(state, exception)} {properties} {exception}");
        }
    }
}
