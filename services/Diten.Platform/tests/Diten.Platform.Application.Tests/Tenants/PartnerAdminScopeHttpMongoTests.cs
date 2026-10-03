using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers.Admin;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
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

namespace Diten.Platform.Application.Tests.Tenants;

/// <summary>
/// BL-521 — a partner administrator reaches only the tenants its administrator record allows. Measured over HTTP on the
/// REAL tenant controller, the production platform-actor policies (DependencyInjection.AddPlatformActorPolicies), the
/// production tenant-resolution middleware and real Mongo (a test-owned replica set — never the shared database).
/// Two tenants: A (allowed) and B (not).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PartnerAdminScopeHttpMongoTests
{
    // ── family 1: the tenant list and its counts ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_partner_lists_and_counts_only_its_allowed_tenants()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        var list = await host.ReadAsync(await host.Client(partner).GetAsync("/api/admin/tenants"));
        var stats = await host.ReadAsync(await host.Client(partner).GetAsync("/api/admin/tenants/stats"));

        var ids = list.GetProperty("data").GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([Host.TenantA], ids);
        Assert.Equal(1, list.GetProperty("data").GetProperty("totalCount").GetInt64());
        Assert.Equal(1, stats.GetProperty("data").GetProperty("total").GetInt64());
    }

    // ── family 2: a tenant's own endpoints ──────────────────────────────────────────────────────────────

    public static TheoryData<string, string, string?> TenantEndpoints() => new()
    {
        { "GET", "", null },
        { "PUT", "", """{"name":"Changed","displayName":"Changed","domain":"changed.local"}""" },
        { "PUT", "/branding", """{"logoUrl":"https://x.test/l.png"}""" },
        { "POST", "/suspend", """{"reason":"x"}""" },
        { "POST", "/reactivate", """{"reason":"x"}""" },
        { "GET", "/modules", null },
        { "GET", "/users/summary", null },
        { "GET", "/admin-users", null },
        { "POST", "/admin-users", """{"email":"a@b.test","displayName":"X"}""" },
        { "GET", "/settings", null },
        { "PUT", "/settings", """{"language":"tr","timezone":"Europe/Istanbul","currency":"TRY","environment":"Production"}""" },
        { "GET", "/login-settings", null },
        { "PUT", "/login-settings", "{}" },
    };

    [Theory]
    [MemberData(nameof(TenantEndpoints))]
    public async Task A_tenant_outside_the_partners_scope_is_not_found_on_every_endpoint_and_nothing_is_written(string method, string suffix, string? body)
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        var before = await host.TenantDocumentAsync(Host.TenantB);

        var response = await host.SendAsync(partner, method, $"/api/admin/tenants/{Host.TenantB:D}{suffix}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await host.TenantDocumentAsync(Host.TenantB));
        // the attempt is recorded as a platform security event naming the tenant it aimed at
        var record = Assert.Single(await host.SecurityEventsAsync());
        Assert.Contains(Host.TenantB.ToString("D"), record, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_allowed_tenant_answers_the_partner()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);

        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantA:D}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantA:D}/settings", null)).StatusCode);
        Assert.Empty(await host.SecurityEventsAsync());
    }

    // ── fail closed ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_partner_allowed_no_tenant_sees_none()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([]);

        await AssertSeesNothingAsync(host, partner);
    }

    [Fact]
    public async Task A_partner_whose_record_cannot_be_read_sees_none()
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]);
        // the record no longer deserializes — the scope must not fall open
        await host.Database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators).UpdateOneAsync(
            new BsonDocument("NormalizedEmail", partner.Email), new BsonDocument("$set", new BsonDocument("AllowedTenantIds", "not a list")));

        await AssertSeesNothingAsync(host, partner);
    }

    private static async Task AssertSeesNothingAsync(Host host, Host.Actor partner)
    {
        var list = await host.ReadAsync(await host.Client(partner).GetAsync("/api/admin/tenants"));
        var stats = await host.ReadAsync(await host.Client(partner).GetAsync("/api/admin/tenants/stats"));
        Assert.Empty(list.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(0, list.GetProperty("data").GetProperty("totalCount").GetInt64());
        Assert.Equal(0, stats.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await host.SendAsync(partner, "GET", $"/api/admin/tenants/{Host.TenantA:D}", null)).StatusCode);
    }

    // ── what stays with platform administrators ─────────────────────────────────────────────────────────

    public static TheoryData<string, string, string?> PlatformOnlyEndpoints() => new()
    {
        { "POST", "/api/admin/tenants", """{"code":"NEW","name":"New"}""" },
        { "DELETE", $"/api/admin/tenants/{Host.TenantA:D}", null },
        { "DELETE", "/api/admin/tenants/bulk", $$"""{"ids":["{{Host.TenantA:D}}"]}""" },
        { "GET", "/api/platform/module-catalog", null },
        { "GET", "/api/platform/subscription-plans", null },
        { "GET", "/api/platform/administrators", null },
        { "POST", "/api/platform/administrators", """{"email":"new.admin@x.test","displayName":"X","actorType":"PlatformAdmin"}""" },
    };

    [Theory]
    [MemberData(nameof(PlatformOnlyEndpoints))]
    public async Task Creating_or_deleting_tenants_and_every_global_object_refuse_a_partner(string method, string path, string? body)
    {
        await using var host = await Host.StartAsync();
        var partner = await host.PartnerAsync([Host.TenantA]); // even for its OWN tenant

        var response = await host.SendAsync(partner, method, path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await host.TenantDocumentAsync(Host.TenantA));
    }

    // ── no regression for platform administrators ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_platform_administrator_still_sees_and_reaches_every_tenant()
    {
        await using var host = await Host.StartAsync();
        var admin = await host.PlatformAdminAsync();

        var list = await host.ReadAsync(await host.Client(admin).GetAsync("/api/admin/tenants"));
        var stats = await host.ReadAsync(await host.Client(admin).GetAsync("/api/admin/tenants/stats"));

        Assert.Equal(2, list.GetProperty("data").GetProperty("totalCount").GetInt64());
        Assert.Equal(2, stats.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await host.SendAsync(admin, "GET", $"/api/admin/tenants/{Host.TenantB:D}", null)).StatusCode);
        Assert.Empty(await host.SecurityEventsAsync());
    }

    // ── host ────────────────────────────────────────────────────────────────────────────────────────────

    internal sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-bl521-test";
        private const string Audience = "diten-platform-bl521-test";
        private const string Secret = "BL-521 partner scope http round trip signing key, test only, 0123456789";

        public static readonly Guid TenantA = Guid.Parse("52152152-0000-4000-8000-0000000000a1");
        public static readonly Guid TenantB = Guid.Parse("52152152-0000-4000-8000-0000000000b2");

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;

        public IMongoDatabase Database { get; }

        public sealed record Actor(string ActorType, Guid UserId, string Email);

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
                    // THE production policies — not a copy.
                    services.AddAuthorization(Diten.Platform.Infrastructure.DependencyInjection.AddPlatformActorPolicies);
                    services.AddHttpContextAccessor();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    // As production (Program.cs): the request's partner scope.
                    services.AddScoped<IPlatformActorTenantScope, PlatformActorTenantScope>();

                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
                    services.AddScoped<IPlatformAdministratorRepository, PlatformAdministratorRepository>();
                    services.AddScoped<AuditOutboxRepository>();
                    services.AddScoped<ITransactionalAuditOutboxStore>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TenantsController).Assembly));
                        manager.FeatureProviders.Add(new OnlyControllers(
                            typeof(TenantsController), typeof(ModuleCatalogController), typeof(SubscriptionPlansController), typeof(AdministratorsController)));
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
                         .Where(c => c.Name is PlatformCollections.Tenants or PlatformCollections.PlatformAdministrators))
            {
                await collection.ApplyAsync(database, CancellationToken.None);
            }

            var host = new Host(mongo, database);
            foreach (var tenant in new[] { TenantA, TenantB })
            {
                var tail = tenant.ToString("N")[^8..];
                await database.GetCollection<Tenant>(PlatformCollections.Tenants).InsertOneAsync(new Tenant
                {
                    Id = tenant, Code = "T" + tail, Slug = "t" + tail, Name = "Tenant " + tail, DisplayName = "Tenant " + tail,
                    Domain = tail + ".local", Status = TenantStatus.Active
                });
            }

            return host;
        }

        public Task<Actor> PartnerAsync(IReadOnlyList<Guid> allowed) => SeedAsync("partner_admin", ActorType.PartnerAdmin, allowed);
        public Task<Actor> PlatformAdminAsync() => SeedAsync("platform_admin", ActorType.PlatformAdmin, []);

        private async Task<Actor> SeedAsync(string claim, ActorType type, IReadOnlyList<Guid> allowed)
        {
            var actor = new Actor(claim, Guid.NewGuid(), $"{claim}.{Guid.NewGuid():N}@bl521.test");
            await Database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators).InsertOneAsync(new PlatformAdministrator
            {
                Email = actor.Email, NormalizedEmail = actor.Email, UserName = actor.Email, NormalizedUserName = actor.Email,
                DisplayName = claim, ActorType = type, PartnerId = type == ActorType.PartnerAdmin ? Guid.NewGuid() : null,
                AllowedTenantIds = allowed.ToList(), Status = AdministratorStatus.Active,
                InvitationStatus = AdministratorInvitationStatus.Accepted, LastLoginAtUtc = DateTimeOffset.UtcNow
            });
            return actor;
        }

        public HttpClient Client(Actor actor)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(actor));
            return client;
        }

        public Task<HttpResponseMessage> SendAsync(Actor actor, string method, string path, string? json)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Client(actor).SendAsync(request);
        }

        public async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
            return JsonDocument.Parse(body).RootElement;
        }

        public async Task<string?> TenantDocumentAsync(Guid id) =>
            (await Database.GetCollection<BsonDocument>(PlatformCollections.Tenants)
                .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).FirstOrDefaultAsync())?.ToJson();

        /// <summary>The out-of-scope attempts waiting in the audit outbox (the central door's store).</summary>
        public async Task<IReadOnlyList<string>> SecurityEventsAsync() =>
            (await Database.GetCollection<BsonDocument>("audit_outbox").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
                .Select(d => d.ToJson())
                .Where(json => json.Contains(PartnerAdminScopeAttribute.OutOfScopeRequestType, StringComparison.Ordinal))
                .ToList();

        private static string Token(Actor actor) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience,
                [
                    new Claim(JwtRegisteredClaimNames.Sub, actor.UserId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, actor.Email),
                    new Claim("actor_type", actor.ActorType)
                ],
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            return _mongo.DisposeAsync();
        }

        private sealed class OnlyControllers(params Type[] controllers) : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) => controllers.Contains(typeInfo.AsType());
        }
    }
}
