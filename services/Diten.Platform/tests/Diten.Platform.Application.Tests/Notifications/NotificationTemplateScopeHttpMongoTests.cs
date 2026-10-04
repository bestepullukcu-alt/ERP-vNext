using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Observability;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-PLATFORM-SCOPE-SMALL-01 (A) — a notification template found BY ID is touched only inside the route's scope: the
/// id-only routes reach platform defaults, a tenant's override is reached under ITS tenant. Measured over HTTP on the
/// real NotificationsController and real Mongo (a test-owned replica set). Two tenants own an override each; one
/// platform default exists.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class NotificationTemplateScopeHttpMongoTests
{
    private const string UpdateBody =
        """{"isPlatformDefault":false,"templateKey":"welcome","channel":"Email","locale":"en","subjectTemplate":"Changed","bodyHtmlTemplate":"<p>Changed</p>","bodyTextTemplate":null,"variables":[],"status":"Draft","semanticVersion":null}""";

    [Fact]
    public async Task A_template_is_read_by_id_only_in_its_own_scope()
    {
        await using var host = await Host.StartAsync();

        // a tenant's override: not by the id-only route, not under another tenant — only under its own
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("GET", $"templates/by-id/{Host.TemplateA:D}"));
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("GET", $"tenant-settings/{Host.TenantB:D}/templates/by-id/{Host.TemplateA:D}"));
        Assert.Equal(HttpStatusCode.OK, await host.StatusAsync("GET", $"tenant-settings/{Host.TenantA:D}/templates/by-id/{Host.TemplateA:D}"));
        // a platform default: by the id-only route, never as a tenant's
        Assert.Equal(HttpStatusCode.OK, await host.StatusAsync("GET", $"templates/by-id/{Host.PlatformTemplate:D}"));
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("GET", $"tenant-settings/{Host.TenantA:D}/templates/by-id/{Host.PlatformTemplate:D}"));
    }

    [Fact]
    public async Task Another_tenants_template_is_not_archived_by_id_and_nothing_is_written()
    {
        await using var host = await Host.StartAsync();
        var before = await host.TemplateDocumentAsync(Host.TemplateA);

        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("POST", $"templates/{Host.TemplateA:D}/archive"));
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("POST", $"tenant-settings/{Host.TenantB:D}/templates/{Host.TemplateA:D}/archive"));
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("POST", $"tenant-settings/{Host.TenantA:D}/templates/{Host.PlatformTemplate:D}/archive"));

        Assert.Equal(before, await host.TemplateDocumentAsync(Host.TemplateA));
        Assert.Equal(NotificationTemplateStatus.Active, (await host.TemplateAsync(Host.PlatformTemplate)).Status);
    }

    [Fact]
    public async Task A_tenants_template_is_archived_under_its_own_tenant_and_the_record_names_that_tenant()
    {
        await using var host = await Host.StartAsync();

        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync("POST", $"tenant-settings/{Host.TenantA:D}/templates/{Host.TemplateA:D}/archive"));

        Assert.Equal(NotificationTemplateStatus.Archived, (await host.TemplateAsync(Host.TemplateA)).Status);
        var record = Assert.Single(await host.AuditOutboxAsync(), json => json.Contains("notifications.template.archived", StringComparison.Ordinal));
        Assert.Contains(Host.TenantA.ToString("D"), record, StringComparison.OrdinalIgnoreCase);
        // CT — filed UNDER tenant A, not merely mentioning it: a platform-global record would still carry A as its target.
        var message = Assert.Single(await host.AuditMessagesAsync(), m => m.EntityId == Host.TemplateA);
        Assert.Equal(Host.TenantA, message.TenantId);
    }

    [Fact]
    public async Task A_platform_default_is_archived_by_id_and_its_record_is_platform_global()
    {
        await using var host = await Host.StartAsync();

        Assert.Equal(HttpStatusCode.NoContent, await host.StatusAsync("POST", $"templates/{Host.PlatformTemplate:D}/archive"));

        Assert.Equal(NotificationTemplateStatus.Archived, (await host.TemplateAsync(Host.PlatformTemplate)).Status);
        var message = Assert.Single(await host.AuditMessagesAsync(), m => m.EntityId == Host.PlatformTemplate);
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, message.TenantId);
    }

    [Fact]
    public async Task A_platform_default_is_updated_by_id_and_stays_a_platform_default()
    {
        await using var host = await Host.StartAsync();

        Assert.Equal(HttpStatusCode.OK, await host.StatusAsync("PUT", $"templates/{Host.PlatformTemplate:D}",
            UpdateBody.Replace("\"isPlatformDefault\":false", "\"isPlatformDefault\":true", StringComparison.Ordinal)));

        var stored = await host.TemplateAsync(Host.PlatformTemplate);
        Assert.Equal("Changed", stored.SubjectTemplate);
        Assert.Null(stored.TenantId);
        Assert.True(stored.IsPlatformDefault);
    }

    [Fact]
    public async Task Another_tenants_template_is_not_taken_over_by_an_update_and_nothing_is_written()
    {
        await using var host = await Host.StartAsync();
        var before = await host.TemplateDocumentAsync(Host.TemplateA);

        // the platform route would have turned it into a platform default; tenant B's route would have moved it to B
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("PUT", $"templates/{Host.TemplateA:D}",
            UpdateBody.Replace("\"isPlatformDefault\":false", "\"isPlatformDefault\":true", StringComparison.Ordinal)));
        Assert.Equal(HttpStatusCode.NotFound, await host.StatusAsync("PUT", $"tenant-settings/{Host.TenantB:D}/templates/{Host.TemplateA:D}", UpdateBody));

        Assert.Equal(before, await host.TemplateDocumentAsync(Host.TemplateA));
    }

    [Fact]
    public async Task A_tenants_template_is_updated_under_its_own_tenant()
    {
        await using var host = await Host.StartAsync();

        Assert.Equal(HttpStatusCode.OK, await host.StatusAsync("PUT", $"tenant-settings/{Host.TenantA:D}/templates/{Host.TemplateA:D}", UpdateBody));

        var stored = await host.TemplateAsync(Host.TemplateA);
        Assert.Equal("Changed", stored.SubjectTemplate);
        Assert.Equal(Host.TenantA, stored.TenantId);
    }

    // ── host ────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-scope-small-test";
        private const string Audience = "diten-platform-scope-small-test";
        private const string Secret = "WP-PLATFORM-SCOPE-SMALL-01 template scope signing key, test only, 0123456789";

        public static readonly Guid TenantA = Guid.Parse("70170170-0000-4000-8000-0000000000a1");
        public static readonly Guid TenantB = Guid.Parse("70170170-0000-4000-8000-0000000000b2");
        public static readonly Guid TemplateA = Guid.Parse("70170170-0000-4000-8000-0000000001a1");
        public static readonly Guid TemplateB = Guid.Parse("70170170-0000-4000-8000-0000000001b2");
        public static readonly Guid PlatformTemplate = Guid.Parse("70170170-0000-4000-8000-0000000001c3");

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;

        public IMongoDatabase Database { get; }

        private Host(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
            Database = database;
            var dbContext = new PlatformDbContext(mongo.Client, database);

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddProblemDetails();
                    services.AddExceptionHandler<Diten.Platform.API.Middleware.GlobalExceptionHandler>();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
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
                        });
                    // As the production policy (Infrastructure DependencyInjection.AddInfrastructure) for a platform admin.
                    services.AddAuthorization(options => options.AddPolicy("PlatformActor", policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireClaim("actor_type", "platform_admin");
                    }));
                    services.AddHttpContextAccessor();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<ICorrelationContext, CorrelationContext>();

                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
                    services.AddScoped<AuditOutboxRepository>();
                    services.AddScoped<ITransactionalAuditOutboxStore>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(NotificationsController).Assembly));
                        manager.FeatureProviders.Add(new OnlyController(typeof(NotificationsController)));
                    });
                })
                .Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public static async Task<Host> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                         .Where(c => c.Name == PlatformCollections.NotificationTemplates))
            {
                await collection.ApplyAsync(database, CancellationToken.None);
            }

            var templates = database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);
            await templates.InsertManyAsync(
            [
                Template(TemplateA, TenantA, platformDefault: false),
                Template(TemplateB, TenantB, platformDefault: false),
                Template(PlatformTemplate, null, platformDefault: true)
            ]);
            return new Host(mongo, database);
        }

        private static NotificationTemplate Template(Guid id, Guid? tenantId, bool platformDefault) => new()
        {
            Id = id, TenantId = tenantId, IsPlatformDefault = platformDefault, TemplateKey = "welcome",
            Channel = NotificationChannelCode.Email, Locale = "en", SubjectTemplate = "Welcome", BodyHtmlTemplate = "<p>Welcome</p>",
            Status = NotificationTemplateStatus.Active
        };

        public async Task<HttpStatusCode> StatusAsync(string method, string path, string? json = null)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
            var request = new HttpRequestMessage(new HttpMethod(method), "/api/platform/notifications/" + path);
            if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return (await client.SendAsync(request)).StatusCode;
        }

        public async Task<NotificationTemplate> TemplateAsync(Guid id) =>
            await Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates).Find(t => t.Id == id).SingleAsync();

        public async Task<string> TemplateDocumentAsync(Guid id) =>
            (await Database.GetCollection<BsonDocument>(PlatformCollections.NotificationTemplates)
                .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync()).ToJson();

        public async Task<IReadOnlyList<string>> AuditOutboxAsync() =>
            (await Database.GetCollection<BsonDocument>("audit_outbox").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Select(d => d.ToJson()).ToList();

        public async Task<IReadOnlyList<AuditOutboxMessage>> AuditMessagesAsync() =>
            await Database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox).Find(FilterDefinition<AuditOutboxMessage>.Empty).ToListAsync();

        private static string Token() =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience,
                [
                    new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, "platform.admin@scope.test"),
                    new Claim("actor_type", "platform_admin")
                ],
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            return _mongo.DisposeAsync();
        }

        private sealed class OnlyController(Type controller) : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
        }
    }
}
