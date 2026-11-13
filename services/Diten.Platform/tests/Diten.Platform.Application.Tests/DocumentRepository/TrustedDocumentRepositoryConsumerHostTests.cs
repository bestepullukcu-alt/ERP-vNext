using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 — THE REAL HTTP HOST for the trusted consumer surface (pack §17 "Authorization"): generic host +
/// TestServer, the shipped controller, the production registration (<c>AddTrustedServiceTokenValidation</c> +
/// <c>AddTrustedDocumentRepositoryConsumer</c>), the production access pipeline (<c>UsePlatformAccessPipeline</c>), the
/// production MediatR pipeline and audit service (<c>AddApplication</c>), the REAL local-filesystem provider behind a
/// recorder, and a repository that is either in memory or the real Mongo repository (<see cref="HostOptions.Objects"/>).
/// Service tokens are RS256, signed with a test key whose public half is the host's configuration.
/// <para>⚠ TestServer does not implement <c>IHttpMaxRequestBodySizeFeature</c>: <c>[RequestSizeLimit]</c> is measured on
/// the endpoint metadata, never by a host case (§8.1 "Boyut").</para>
/// </summary>
internal sealed class TrustedDocumentRepositoryConsumerHost : IAsyncDisposable
{
    public const string ServiceIssuer = "https://auth.service.local";
    public const string KeyId = "dl2-current";
    public const string UserIssuer = "diten-auth-dl2-user";
    public const string UserAudience = "diten-platform-dl2-user";
    public const string UserSecret = "MOD-0262-FU01 DL-2 user jwt signing key, test only, 0123456789abcdef";
    public const string Module = "MOD-0217";
    public const string BasePath = "/api/internal/v1/document-repository/trusted-consumer/objects";

    public static readonly RSA SigningKey = RSA.Create(2048);

    // Exported ONCE: tokens are minted by many hosts in parallel, and one RSA object is not used concurrently.
    private static readonly RSAParameters SigningParameters = SigningKey.ExportParameters(true);
    public static readonly string PublicKeyPem = SigningKey.ExportSubjectPublicKeyInfoPem();

    private readonly IHost _host;

    private TrustedDocumentRepositoryConsumerHost(IHost host, HostOptions options, string root)
    {
        _host = host;
        Options = options;
        Root = root;
    }

    public HostOptions Options { get; }
    public string Root { get; }
    public RecordingStorage Storage => Options.Storage;
    public TestAuditWriter Audit => Options.Audit;
    public CapturedLogs Logs => Options.Logs;
    public ObjectHooks Hooks => Options.Hooks;
    public IServiceProvider Services => _host.Services;
    public SemaphoreSlim Completed { get; } = new(0);

    public static async Task<TrustedDocumentRepositoryConsumerHost> StartAsync(HostOptions? options = null)
    {
        options ??= new HostOptions();
        var root = options.Root ?? Path.Combine(Path.GetTempPath(), "DitenDl2Trusted", Guid.NewGuid().ToString("N"));
        options.Root = root;
        TrustedDocumentRepositoryConsumerHost? self = null;

        var configuration = new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = UserIssuer,
            ["JwtSettings:Audience"] = UserAudience,
            ["JwtSettings:Secret"] = UserSecret
        };
        if (options.ConfigureScheme)
        {
            configuration[$"{TrustedServiceTokenValidationOptions.SectionName}:Issuer"] = ServiceIssuer;
            configuration[$"{TrustedServiceTokenValidationOptions.SectionName}:CurrentKeyId"] = KeyId;
            configuration[$"{TrustedServiceTokenValidationOptions.SectionName}:CurrentPublicKeyPem"] = PublicKeyPem;
        }

        for (var i = 0; i < options.Entries.Count; i++)
        {
            var entry = options.Entries[i];
            var prefix = $"{TrustedDocumentRepositoryConsumerAuthorizationOptions.SectionName}:Entries:{i}";
            configuration[$"{prefix}:ClientId"] = entry.ClientId.ToString("D");
            configuration[$"{prefix}:ServiceName"] = entry.ServiceName;
            configuration[$"{prefix}:Audience"] = entry.Audience;
            configuration[$"{prefix}:ConsumerModule"] = entry.ConsumerModule;
            for (var s = 0; s < entry.Scopes.Count; s++)
            {
                configuration[$"{prefix}:Scopes:{s}"] = entry.Scopes[s];
            }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(configuration).Build();

        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(config);
                    services.AddRouting();
                    services.AddLogging(logging => logging.AddProvider(options.Logs));
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(jwt =>
                        {
                            jwt.MapInboundClaims = false;
                            jwt.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = UserIssuer,
                                ValidAudience = UserAudience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserSecret)),
                                ClockSkew = TimeSpan.Zero
                            };
                        });
                    if (options.SlowAuthentication)
                    {
                        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SlowAuthenticationHandler>(
                            TrustedServiceTokenValidationExtensions.DocumentRepositoryAuthenticationScheme, _ => { });
                    }
                    else
                    {
                        services.AddTrustedServiceTokenValidation(config);
                    }

                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    services.AddPeopleSearchRateLimit();
                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new Diten.Platform.Application.Tests.Tasks.FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<IActorPermissionContext, ClaimsActorPermissionContext>();
                    services.AddScoped<Diten.Platform.API.Observability.ICorrelationContext, Diten.Platform.API.Observability.CorrelationContext>();
                    services.AddSingleton<IAuditOutboxWriter>(sp => options.AuditWriter?.Invoke(sp) ?? options.Audit);

                    services.Configure<ContentStorageOptions>(storage =>
                    {
                        storage.RootPath = root;
                        storage.MaxFileSizeBytes = options.MaxFileSizeBytes;
                        storage.AllowedExtensions = [".pdf", ".txt"];
                        storage.AllowedMediaTypes = [];
                    });
                    services.AddSingleton(options.Storage);
                    services.AddScoped<IContentStorageGateway>(sp =>
                    {
                        options.Storage.Inner ??= new LocalFileSystemContentStorageGateway(
                            sp.GetRequiredService<IOptions<ContentStorageOptions>>(),
                            NullLogger<LocalFileSystemContentStorageGateway>.Instance);
                        return options.Storage;
                    });
                    services.AddScoped<IRepositoryObjectRepository>(sp => new HookedObjects(
                        options.Objects?.Invoke(sp) ?? new InMemoryObjects(options.Store, sp.GetRequiredService<ITenantContext>()),
                        options.Hooks,
                        sp.GetRequiredService<IHttpContextAccessor>()));
                    services.AddScoped<Diten.Platform.Application.Features.DocumentRepository.Services.DocumentRepositoryService>();

                    services.AddTrustedDocumentRepositoryConsumer(config);
                    services.AddControllers().AddApplicationPart(typeof(InternalTrustedDocumentRepositoryConsumerController).Assembly);
                    options.ConfigureServices?.Invoke(services);
                })
                .Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        var signal = context.Request.Headers.ContainsKey(TestBodyFaults.SignalHeader)
                            || context.Request.Headers.ContainsKey(TestBodyFaults.Header);
                        try { await next(); }
                        finally { if (signal) { self?.Completed.Release(); } }
                    });
                    app.Use(async (context, next) =>
                    {
                        TestBodyFaults.Apply(context);
                        await next();
                    });
                    options.BeforePipeline?.Invoke(app);
                    app.UseRouting();
                    app.UsePlatformAccessPipeline();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Build();

        await host.StartAsync();
        self = new TrustedDocumentRepositoryConsumerHost(host, options, root);
        return self;
    }

    public HttpClient Client() => _host.GetTestServer().CreateClient();

    // ── Requests.

    public async Task<HttpResponseMessage> UploadAsync(
        string? token,
        HttpContent content,
        Action<HttpRequestMessage>? edit = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BasePath) { Content = content };
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }

        edit?.Invoke(request);
        return await Client().SendAsync(request);
    }

    public Task<HttpResponseMessage> GetAsync(string? token, Guid contentId, Action<HttpRequestMessage>? edit = null) =>
        SendAsync(HttpMethod.Get, $"{BasePath}/{contentId:D}", token, edit);

    public Task<HttpResponseMessage> ContentAsync(string? token, Guid contentId, Action<HttpRequestMessage>? edit = null) =>
        SendAsync(HttpMethod.Get, $"{BasePath}/{contentId:D}/content", token, edit);

    public Task<HttpResponseMessage> CompensateAsync(string? token, Guid contentId, Action<HttpRequestMessage>? edit = null) =>
        SendAsync(HttpMethod.Post, $"{BasePath}/{contentId:D}/compensate", token, edit);

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? token, Action<HttpRequestMessage>? edit = null, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }

        edit?.Invoke(request);
        return await Client().SendAsync(request);
    }

    /// <summary>Uploads a small valid file and returns its content id.</summary>
    public async Task<Guid> UploadOkAsync(string token, string text = "contract body", string fileName = "contract.pdf")
    {
        using var response = await UploadAsync(token, Form.Valid(Encoding.UTF8.GetBytes(text), fileName));
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await ContentIdAsync(response);
    }

    public static async Task<Guid> ContentIdAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("contentId").GetGuid();
    }

    public static async Task<string?> ReasonCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
    }

    /// <summary>The audit records this host's requests produced, as <c>audit_events</c> would hold them.</summary>
    public IReadOnlyList<AuditEvent> Records() => Audit.Writes.Select(InMemoryAuditOutbox.ToAuditEvent).ToList();

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        try { if (Directory.Exists(Root)) { Directory.Delete(Root, recursive: true); } } catch { /* best effort */ }
    }

    // ── Tokens.

    /// <summary>A service token exactly as Auth mints it (MOD-0033-FU02: ten claims, 300 s, RS256).</summary>
    public static string ServiceToken(
        Guid clientId,
        Guid tenantId,
        string audience = TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience,
        Action<List<Claim>>? edit = null,
        RSA? key = null,
        int lifetimeSeconds = 300,
        int issuedAtOffsetSeconds = 0)
    {
        var now = DateTimeOffset.UtcNow.AddSeconds(issuedAtOffsetSeconds);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId.ToString("D")),
            new("actor_type", "service"),
            new("service_name", TrustedServiceTokenValidationExtensions.RequiredServiceName),
            new("tenant_id", tenantId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        edit?.Invoke(claims);
        var jwt = new JwtSecurityToken(
            ServiceIssuer,
            audience,
            claims,
            now.UtcDateTime,
            now.AddSeconds(lifetimeSeconds).UtcDateTime,
            new SigningCredentials(new RsaSecurityKey(key?.ExportParameters(true) ?? SigningParameters) { KeyId = KeyId },
                SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    /// <summary>A person's token as AuthService mints it, validated by the host's default scheme.</summary>
    public static string UserToken(Guid tenantId, Guid userId, string actorType = "tenant_user", params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, "dl2.person@tenant.example"),
            new("name", "Dl2 Person"),
            new(JwtRegisteredClaimNames.GivenName, "Dl2"),
            new(JwtRegisteredClaimNames.FamilyName, "Person"),
            new("actor_type", actorType),
            new("tenant_id", tenantId.ToString()),
            new("pwd_change_required", "false")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var jwt = new JwtSecurityToken(
            UserIssuer, UserAudience, claims, expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserSecret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public static ConsumerEntry Entry(Guid clientId, params string[] scopes) =>
        new(clientId, TrustedServiceTokenValidationExtensions.RequiredServiceName,
            TrustedServiceTokenValidationExtensions.DocumentRepositoryRequiredAudience, Module,
            scopes.Length == 0 ? ["Documents"] : scopes.ToList());
}

internal sealed record ConsumerEntry(Guid ClientId, string ServiceName, string Audience, string ConsumerModule, List<string> Scopes);

internal sealed class HostOptions
{
    public bool ConfigureScheme { get; init; } = true;
    public bool SlowAuthentication { get; init; }
    public List<ConsumerEntry> Entries { get; init; } = [];
    public string? Root { get; set; }
    public long MaxFileSizeBytes { get; init; } = 268_435_456;
    public RecordingStorage Storage { get; init; } = new();
    public TestAuditWriter Audit { get; init; } = new();
    public Func<IServiceProvider, IAuditOutboxWriter>? AuditWriter { get; init; }
    public CapturedLogs Logs { get; init; } = new();
    public InMemoryObjectStore Store { get; init; } = new();
    public ObjectHooks Hooks { get; init; } = new();
    public Func<IServiceProvider, IRepositoryObjectRepository>? Objects { get; init; }
    public Action<IServiceCollection>? ConfigureServices { get; init; }
    public Action<IApplicationBuilder>? BeforePipeline { get; init; }
}

/// <summary>Multipart bodies written part by part, in the order given (duplicates and odd orders allowed).</summary>
internal static class Form
{
    public static readonly Guid OwningItem = Guid.Parse("26202620-0000-4000-8000-000000000001");
    public static readonly Guid OwningVersion = Guid.Parse("26202620-0000-4000-8000-000000000002");
    public static readonly Guid Company = Guid.Parse("26202620-0000-4000-8000-000000000003");

    public static MultipartFormDataContent Valid(byte[] file, string fileName = "contract.pdf", string scope = "Documents") =>
        Parts(
            ("owningItemId", OwningItem.ToString("D")),
            ("owningVersionId", OwningVersion.ToString("D")),
            ("companyId", Company.ToString("D")),
            ("scope", scope),
            File(file, fileName));

    public static (string Name, object Value) File(byte[] bytes, string fileName = "contract.pdf") =>
        ("file", (bytes, fileName));

    public static (string Name, object Value) File(HttpContent content, string fileName = "contract.pdf") =>
        ("file", (content, fileName));

    public static MultipartFormDataContent Parts(params (string Name, object Value)[] parts)
    {
        var content = new MultipartFormDataContent("dl2-boundary-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, value) in parts)
        {
            switch (value)
            {
                case string text:
                    content.Add(new StringContent(text, Encoding.UTF8), name);
                    break;
                case ValueTuple<byte[], string> file:
                    var bytes = new ByteArrayContent(file.Item1);
                    bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                    content.Add(bytes, name, file.Item2);
                    break;
                case ValueTuple<HttpContent, string> stream:
                    stream.Item1.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                    content.Add(stream.Item1, name, stream.Item2);
                    break;
            }
        }

        return content;
    }

    /// <summary>Same fields as <see cref="Valid"/>, as (name, value) pairs to rearrange.</summary>
    public static (string Name, object Value)[] Fields(string scope = "Documents") =>
    [
        ("owningItemId", OwningItem.ToString("D")),
        ("owningVersionId", OwningVersion.ToString("D")),
        ("companyId", Company.ToString("D")),
        ("scope", scope)
    ];
}

/// <summary>Records every provider call; the real local-filesystem provider does the work.</summary>
internal sealed class RecordingStorage : IContentStorageGateway
{
    public IContentStorageGateway? Inner { get; set; }
    public int StoreCalls;
    public ConcurrentQueue<CancellationToken> DeleteTokens { get; } = new();
    public ConcurrentQueue<bool> DeleteResults { get; } = new();

    /// <summary>"Sahte sağlayıcı belirteci gözler" (AC-33 (4)): a cancelled token makes the delete throw.</summary>
    public bool DeleteObservesToken { get; set; }

    public Task<Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default)
    {
        Interlocked.Increment(ref StoreCalls);
        return Inner!.StoreAsync(request, ct);
    }

    public Task<Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default) =>
        Inner!.OpenReadAsync(storageProvider, objectKey, ct);

    public async Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default)
    {
        DeleteTokens.Enqueue(ct);
        if (DeleteObservesToken)
        {
            ct.ThrowIfCancellationRequested();
        }

        var deleted = await Inner!.TryDeleteAsync(storageProvider, objectKey, ct);
        DeleteResults.Enqueue(deleted);
        return deleted;
    }
}

/// <summary>The audit outbox in memory. <see cref="ObservesToken"/> = the production writer's default (a cancelled token
/// throws before anything is written; inference, §8.1) — the two "test writer" forms of AC-33 (4) / AC-35 (f) (g).</summary>
internal sealed class TestAuditWriter : IAuditOutboxWriter
{
    private readonly List<AuditOutboxWriteRequest> _writes = [];

    public bool ObservesToken { get; set; }
    public bool Throws { get; set; }

    public IReadOnlyList<AuditOutboxWriteRequest> Writes
    {
        get { lock (_writes) { return _writes.ToList(); } }
    }

    public Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
    {
        if (ObservesToken)
        {
            ct.ThrowIfCancellationRequested();
        }

        if (Throws)
        {
            throw new InvalidOperationException("audit outbox down (test)");
        }

        lock (_writes) { _writes.Add(request); }
        return Task.FromResult(true);
    }
}

internal sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public int Count(LogLevel level, string fragment) =>
        Entries.Count(e => e.Level == level && e.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public void Dispose() { }

    private sealed class Logger(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Entries.Enqueue((category, logLevel, formatter(state, exception)));
    }
}

/// <summary>Rows shared by every request (and by two hosts, for the client-renewal case).</summary>
internal sealed class InMemoryObjectStore
{
    public List<RepositoryObject> Rows { get; } = [];
}

/// <summary>Tenant-scoped like the Mongo repository: the ambient tenant and, except the DL-2 read, IsDeleted == false.</summary>
internal sealed class InMemoryObjects(InMemoryObjectStore store, ITenantContext tenant) : IRepositoryObjectRepository
{
    public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (store.Rows) { store.Rows.Add(repositoryObject); }
        return Task.FromResult(repositoryObject);
    }

    public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (store.Rows)
        {
            return Task.FromResult(store.Rows.FirstOrDefault(r =>
                r.TenantId == tenant.TenantId && r.ContentId == contentId && !r.IsDeleted));
        }
    }

    public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default)
    {
        lock (store.Rows)
        {
            return Task.FromResult<IReadOnlyList<RepositoryObject>>(store.Rows
                .Where(r => r.TenantId == tenant.TenantId && r.OwningItemId == owningItemId && !r.IsDeleted).ToList());
        }
    }

    public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (store.Rows)
        {
            var row = store.Rows.FirstOrDefault(r => r.TenantId == tenant.TenantId && r.ContentId == contentId && !r.IsDeleted);
            if (row is null) { return Task.FromResult(false); }
            row.IsDeleted = true;
            return Task.FromResult(true);
        }
    }

    public Task<RepositoryObject?> GetByContentIdIncludingCompensatedAsync(Guid contentId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (store.Rows)
        {
            return Task.FromResult(store.Rows.FirstOrDefault(r => r.TenantId == tenant.TenantId && r.ContentId == contentId));
        }
    }
}

/// <summary>
/// Hooks around the repository for the timing cases (AC-33, AC-35 (g)): each hook runs BEFORE the real call and may abort
/// the request (<c>HttpContext.Abort()</c>) or replace the answer. Tokens are recorded.
/// </summary>
internal sealed class ObjectHooks
{
    public ConcurrentQueue<CancellationToken> CreateTokens { get; } = new();
    public ConcurrentQueue<CancellationToken> MarkTokens { get; } = new();
    public int IncludingCompensatedReads;
    public Action<HttpContext?>? BeforeCreate { get; set; }
    public Action<HttpContext?>? BeforeMark { get; set; }
    public bool? MarkResult { get; set; }

    /// <summary>Called with the 1-based number of the DL-2 read; may abort, or return true to answer "no row".</summary>
    public Func<int, HttpContext?, bool>? BeforeIncludingCompensatedRead { get; set; }

    /// <summary>Answers the n-th DL-2 read with this row state instead (null = as stored).</summary>
    public Func<int, RepositoryObject?, RepositoryObject?>? RewriteIncludingCompensatedRead { get; set; }
}

internal sealed class HookedObjects(IRepositoryObjectRepository inner, ObjectHooks hooks, IHttpContextAccessor accessor)
    : IRepositoryObjectRepository
{
    public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
    {
        hooks.CreateTokens.Enqueue(ct);
        hooks.BeforeCreate?.Invoke(accessor.HttpContext);
        return inner.CreateAsync(repositoryObject, ct);
    }

    public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default) =>
        inner.GetByContentIdAsync(contentId, ct);

    public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) =>
        inner.GetByOwningItemAsync(owningItemId, ct);

    public async Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
    {
        hooks.MarkTokens.Enqueue(ct);
        hooks.BeforeMark?.Invoke(accessor.HttpContext);
        var marked = await inner.MarkCompensatedAsync(contentId, ct);
        return hooks.MarkResult ?? marked;
    }

    public async Task<RepositoryObject?> GetByContentIdIncludingCompensatedAsync(Guid contentId, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref hooks.IncludingCompensatedReads);
        if (hooks.BeforeIncludingCompensatedRead?.Invoke(n, accessor.HttpContext) == true)
        {
            return null;
        }

        var row = await inner.GetByContentIdIncludingCompensatedAsync(contentId, ct);
        return hooks.RewriteIncludingCompensatedRead is { } rewrite ? rewrite(n, row) : row;
    }
}

/// <summary>AC-27 — the "slow authentication" double: answers after 6 s, far past the 2 s budget (a 504 well before 6 s
/// proves the budget cut it, not the double's own answer).</summary>
internal sealed class SlowAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(6));
        return AuthenticateResult.Fail("slow");
    }
}

/// <summary>
/// AC-35 (c) (d) (f) — replaces the request body when the test names a fault in <c>X-Test-Body-Fault</c>: after 1 MiB
/// the body throws an <c>IOException</c> (<c>io</c>), aborts and throws <c>OperationCanceledException(RequestAborted)</c>
/// (<c>abort</c>), or aborts and then throws an <c>IOException</c> (<c>abort-io</c>). TestHost implements
/// <c>IHttpRequestLifetimeFeature</c>, so <c>Abort()</c> cancels <c>RequestAborted</c>.
/// </summary>
internal static class TestBodyFaults
{
    public const string Header = "X-Test-Body-Fault";

    /// <summary>A request carrying this header releases <c>Completed</c> when the server side has fully finished.</summary>
    public const string SignalHeader = "X-Test-Signal";

    public static void Apply(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(Header, out var fault))
        {
            context.Request.Body = new FaultingBody(context, context.Request.Body, fault.ToString());
        }
    }

    private sealed class FaultingBody(HttpContext context, Stream inner, string fault) : Stream
    {
        private const long FaultAfter = 1_048_576;
        private long _bodyRead;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _bodyRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_bodyRead >= FaultAfter)
            {
                switch (fault)
                {
                    case "io":
                        throw new IOException("Unexpected end of request content (test).");
                    case "abort":
                        context.Abort();
                        throw new OperationCanceledException(context.RequestAborted);
                    case "abort-io":
                        context.Abort();
                        throw new IOException("Connection reset (test).");
                }
            }

            var read = await inner.ReadAsync(buffer, cancellationToken);
            _bodyRead += read;
            return read;
        }
    }
}
