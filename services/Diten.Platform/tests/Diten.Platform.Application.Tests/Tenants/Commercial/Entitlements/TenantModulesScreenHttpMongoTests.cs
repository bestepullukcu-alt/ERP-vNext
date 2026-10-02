using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
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
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Entitlements;

/// <summary>
/// BL-500 — the tenant's Modules tab, measured the way the screen uses it: a PLATFORM actor's signed token → the real
/// <c>TenantResolutionMiddleware</c> (which puts the request in the platform context) → the real
/// <see cref="TenantModuleEntitlementsController"/> → the production MediatR pipeline and handlers → the real
/// <see cref="TenantModuleEntitlementRepository"/>, transaction executor, version counter and audit outbox over a
/// test-owned MongoDB replica set.
///
/// <para><b>Why HTTP.</b> The suspend that failed on the screen passed every existing test: those build the
/// repository with <c>TenantContext.SetTenant(tenantId)</c>, the one context a platform administrator's request never
/// has. Only a request that goes through the middleware reproduces the context the failure lives in.</para>
///
/// <para>Doubled: the module catalogue, the subscription plan lookup, the quota service (always grants) and the
/// integration-event writer (records nothing). None of them is this file's subject.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TenantModulesScreenHttpMongoTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public TenantModulesScreenHttpMongoTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private static readonly Guid Tenant = Guid.Parse("50050050-0000-4000-8000-0000000000a1");
    private static readonly Guid OtherTenant = Guid.Parse("50050050-0000-4000-8000-0000000000b2");

    [Fact]
    public async Task A_platform_administrator_suspends_an_active_add_on_and_the_list_shows_it_suspended()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);

        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.True(row.IsEnabled);
        Assert.NotNull(row.RowVersion);

        // Exactly what the screen sends: the row's own version, as it arrived in the list.
        var response = await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = row.ModuleCode,
            physicalEntitlementId = row.PhysicalEntitlementId,
            reason = "Suspended from the Modules tab",
            rowVersion = row.RowVersion
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.False(after.IsEnabled);
        Assert.False((await host.StoredAsync(seeded.Id)).IsEnabled);
    }

    [Fact]
    public async Task A_suspended_add_on_is_enabled_again()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon, enabled: false);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);

        var response = await host.PostAsync(Tenant, $"{seeded.Id:D}/enable", row.RowVersion);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True((await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id).IsEnabled);
    }

    [Fact]
    public async Task An_expired_add_on_offers_a_new_date_first_and_never_enable_and_extending_it_makes_it_active()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon, expiry: DateTimeOffset.UtcNow.AddDays(-3));

        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Equal("Expired", row.EffectiveAccess);
        // The row is switched ON; it is expired. "Enable" would change nothing — it is not offered.
        Assert.Equal(["extendExpiry", "disable"], row.AllowedActions);

        var response = await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new
        {
            expiryDateUtc = DateTimeOffset.UtcNow.AddDays(30),
            reason = (string?)null,
            rowVersion = row.RowVersion
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Equal("Active", after.EffectiveAccess);
        Assert.True(after.IsEnabled);
        Assert.Equal(["disable", "extendExpiry"], after.AllowedActions);
    }

    [Fact]
    public async Task An_existing_manual_override_is_suspended_by_module_code()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "CRM", EntitlementSource.ManualOverride);

        // The path the plan row takes when an override already exists: no row id, only the module.
        var response = await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "CRM",
            physicalEntitlementId = (Guid?)null,
            reason = "Suspended by module",
            rowVersion = (string?)null
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await host.StoredAsync(seeded.Id)).IsEnabled);
    }

    [Fact]
    public async Task Suspending_a_plan_module_blocks_it_and_the_override_row_carries_the_way_back()
    {
        await using var host = await Host.StartAsync();
        var plan = (await host.ListAsync(Tenant)).Single(r => r.IsProjectionRow);
        Assert.Equal(Host.PlanModule, plan.ModuleCode);
        Assert.Equal(["disable"], plan.AllowedActions);

        var response = await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = plan.ModuleCode,
            physicalEntitlementId = (Guid?)null,
            reason = "Not for this tenant",
            rowVersion = (string?)null
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rows = await host.ListAsync(Tenant);
        var planAfter = rows.Single(r => r.IsProjectionRow);
        Assert.Equal("BlockedByOverride", planAfter.EffectiveAccess);
        // A plan's module comes with the plan: once blocked there is nothing left to do on the plan's own line.
        Assert.Empty(planAfter.AllowedActions!);
        var overrideRow = rows.Single(r => !r.IsProjectionRow);
        Assert.Equal(["enable", "extendExpiry", "removeOverride"], overrideRow.AllowedActions);
    }

    [Fact]
    public async Task Removing_a_manual_override_takes_the_row_away()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "CRM", EntitlementSource.ManualOverride);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Equal(["disable", "extendExpiry", "removeOverride"], row.AllowedActions);

        var response = await host.DeleteAsync(Tenant, $"{seeded.Id:D}/manual-override", new { rowVersion = row.RowVersion });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await host.ListAsync(Tenant), r => r.PhysicalEntitlementId == seeded.Id);
    }

    [Fact]
    public async Task A_baseline_module_offers_no_action_and_the_server_still_refuses_its_removal_with_a_code()
    {
        await using var host = await Host.StartAsync();
        // A stray override row on a baseline module — what the owner met on the Task Center line.
        var seeded = await host.SeedAsync(Tenant, Host.BaselineModule, EntitlementSource.ManualOverride);

        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        Assert.Empty(row.AllowedActions!);

        // The rule itself did not move: the server refuses, now with a code a screen can translate.
        var remove = await Host.ReadRefusalAsync(await host.DeleteAsync(Tenant, $"{seeded.Id:D}/manual-override", new { rowVersion = row.RowVersion }));
        Assert.Equal(HttpStatusCode.Conflict, remove.Status);
        Assert.Equal("ENTITLEMENT_MODULE_BASELINE", remove.Code);

        var disable = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = Host.BaselineModule, physicalEntitlementId = seeded.Id, reason = "x", rowVersion = row.RowVersion
        }));
        Assert.Equal(HttpStatusCode.Conflict, disable.Status);
        Assert.Equal("ENTITLEMENT_MODULE_BASELINE", disable.Code);
        Assert.True((await host.StoredAsync(seeded.Id)).IsEnabled);
    }

    [Fact]
    public async Task A_stale_screen_is_refused_with_its_own_code_and_the_row_is_left_alone()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);
        var staleVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var disable = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new
        {
            moduleCode = "GOLDENSLIM", physicalEntitlementId = seeded.Id, reason = "x", rowVersion = staleVersion
        }));
        var expiry = await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{seeded.Id:D}/expiry", new
        {
            expiryDateUtc = DateTimeOffset.UtcNow.AddDays(5), reason = (string?)null, rowVersion = staleVersion
        }));

        Assert.Equal(HttpStatusCode.Conflict, disable.Status);
        Assert.Equal("ENTITLEMENT_STALE", disable.Code);
        Assert.Equal(HttpStatusCode.Conflict, expiry.Status);
        Assert.Equal("ENTITLEMENT_STALE", expiry.Code);
        var stored = await host.StoredAsync(seeded.Id);
        Assert.True(stored.IsEnabled);
        Assert.Null(stored.ExpiryDateUtc);
        Assert.Equal(seeded.RowVersion, stored.RowVersion);
    }

    [Fact]
    public async Task Another_tenants_row_id_under_this_tenants_route_is_not_found_and_stays_untouched()
    {
        await using var host = await Host.StartAsync();
        var theirs = await host.SeedAsync(OtherTenant, "CRM", EntitlementSource.ManualOverride);
        var version = Convert.ToBase64String(theirs.RowVersion);

        var refusals = new[]
        {
            await Host.ReadRefusalAsync(await host.PostAsync(Tenant, "disable", new { moduleCode = "CRM", physicalEntitlementId = theirs.Id, reason = "x", rowVersion = version })),
            await Host.ReadRefusalAsync(await host.PostAsync(Tenant, $"{theirs.Id:D}/enable", version)),
            await Host.ReadRefusalAsync(await host.PatchAsync(Tenant, $"{theirs.Id:D}/expiry", new { expiryDateUtc = DateTimeOffset.UtcNow.AddDays(5), reason = (string?)null, rowVersion = version })),
            await Host.ReadRefusalAsync(await host.DeleteAsync(Tenant, $"{theirs.Id:D}/manual-override", new { rowVersion = version }))
        };

        Assert.All(refusals, refusal =>
        {
            Assert.Equal(HttpStatusCode.NotFound, refusal.Status);
            Assert.Equal("ENTITLEMENT_NOT_FOUND", refusal.Code);
        });
        var stored = await host.StoredAsync(theirs.Id);
        Assert.True(stored.IsEnabled);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.ExpiryDateUtc);
        Assert.Equal(theirs.RowVersion, stored.RowVersion);
        Assert.DoesNotContain(await host.ListAsync(Tenant), r => r.PhysicalEntitlementId == theirs.Id);
    }

    [Fact]
    public async Task Adding_a_module_twice_is_refused_with_a_code_and_a_baseline_module_cannot_be_added()
    {
        await using var host = await Host.StartAsync();
        object Add(string code) => new { moduleCode = code, source = "Addon", isEnabled = true, expiryDateUtc = (DateTimeOffset?)null, reason = (string?)null, rowVersion = (string?)null };

        var first = await host.PostAsync(Tenant, null, Add("CRM"));
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());

        var second = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, null, Add("CRM")));
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal("ENTITLEMENT_ALREADY_EXISTS", second.Code);

        var baseline = await Host.ReadRefusalAsync(await host.PostAsync(Tenant, null, Add(Host.BaselineModule)));
        Assert.Equal(HttpStatusCode.Conflict, baseline.Status);
        Assert.Equal("ENTITLEMENT_MODULE_BASELINE", baseline.Code);
    }

    [Fact]
    public async Task K1_measure_what_an_entitlement_change_leaves_in_the_audit_outbox_and_whether_it_reaches_audit_events()
    {
        await using var host = await Host.StartAsync();
        var seeded = await host.SeedAsync(Tenant, "GOLDENSLIM", EntitlementSource.Addon);
        var row = (await host.ListAsync(Tenant)).Single(r => r.PhysicalEntitlementId == seeded.Id);
        var response = await host.PostAsync(Tenant, "disable", new { moduleCode = "GOLDENSLIM", physicalEntitlementId = seeded.Id, reason = "audit measure", rowVersion = row.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var outbox = host.Database.GetCollection<BsonDocument>("audit_outbox");
        var before = await outbox.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        _output.WriteLine("OUTBOX BEFORE WORKER: " + before.ToJson());

        var context = new PlatformDbContext(host.MongoClient, host.Database);
        var tenantContext = new TenantContext();
        var processor = new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxProcessor(
            new AuditOutboxRepository(context),
            new AuditEventRepository(host.Database, tenantContext),
            tenantContext,
            new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxPayloadMapper(),
            new Diten.Platform.Infrastructure.Services.Audit.AuditOutboxWorkerOptions { BatchSize = 10, MaxAttempts = 5, InitialRetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(5) },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Infrastructure.Services.Audit.AuditOutboxProcessor>.Instance);
        var processed = await processor.ProcessBatchAsync();
        _output.WriteLine("PROCESSED: " + processed);
        _output.WriteLine("OUTBOX AFTER WORKER: " + (await outbox.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()).ToJson());
        _output.WriteLine("AUDIT EVENTS: " + (await host.Database.GetCollection<BsonDocument>("audit_events").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()).ToJson());
    }

    private sealed record Row(
        string ModuleCode,
        string DisplaySource,
        Guid? PhysicalEntitlementId,
        bool IsEnabled,
        DateTimeOffset? ExpiryDateUtc,
        string EffectiveAccess,
        bool IsProjectionRow,
        string? RowVersion,
        IReadOnlyList<string>? AllowedActions);

    private sealed record Envelope<T>(T? Data, bool IsSuccessful, IReadOnlyList<string>? Errors, string? Reason_Code);

    private sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-bl500-test";
        private const string Audience = "diten-platform-bl500-test";
        private const string Secret = "BL-500 tenant modules http round trip signing key, test only, 0123456789";

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;

        public IMongoDatabase Database { get; }
        public IMongoClient MongoClient => _mongo.Client;

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
                    // The production policy (Infrastructure DependencyInjection.AddInfrastructure).
                    services.AddAuthorization(options => options.AddPolicy("PlatformActor", policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireAssertion(context =>
                        {
                            var actorType = context.User.Claims
                                .FirstOrDefault(claim => string.Equals(claim.Type, "actor_type", StringComparison.OrdinalIgnoreCase))
                                ?.Value;
                            return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
                        });
                    }));
                    services.AddHttpContextAccessor();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();

                    // Real persistence, over the test-owned replica set.
                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<ITenantModuleEntitlementRepository, TenantModuleEntitlementRepository>();
                    services.AddScoped<IPlatformTransactionExecutor, PlatformTransactionExecutor>();
                    services.AddScoped<IEntitlementStateVersionRepository, EntitlementStateVersionRepository>();
                    services.AddScoped<AuditOutboxRepository>();
                    services.AddScoped<ITransactionalAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());

                    // Not this file's subject.
                    services.AddSingleton<ITransactionalIntegrationEventWriter, SilentEvents>();
                    services.AddSingleton(GrantingQuota());
                    services.AddSingleton(Catalogue());
                    services.AddSingleton(CatalogueContract());
                    services.AddSingleton(Subscriptions());
                    services.AddSingleton(Plans());

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TenantModuleEntitlementsController).Assembly));
                        manager.FeatureProviders.Add(new OnlyController(typeof(TenantModuleEntitlementsController)));
                    });
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

        public static async Task<Host> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            return new Host(mongo, mongo.CreateDatabase());
        }

        public const string PlanModule = "PLANMOD";
        public const string BaselineModule = "TASKCENTER";
        public static readonly Guid PlanId = Guid.Parse("50050050-0000-4000-8000-0000000000c3");

        public async Task<TenantModuleEntitlement> SeedAsync(
            Guid tenantId, string moduleCode, EntitlementSource source, bool enabled = true, DateTimeOffset? expiry = null)
        {
            var entitlement = new TenantModuleEntitlement
            {
                TenantId = tenantId,
                ModuleCode = moduleCode,
                Source = source,
                IsEnabled = enabled,
                ExpiryDateUtc = expiry,
                Reason = "seeded"
            };
            await Database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements").InsertOneAsync(entitlement);
            return entitlement;
        }

        public async Task<TenantModuleEntitlement> StoredAsync(Guid id) =>
            await Database.GetCollection<TenantModuleEntitlement>("tenant_module_entitlements").Find(x => x.Id == id).SingleAsync();

        public Task<long> CountAsync(string collection) =>
            Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        public async Task<IReadOnlyList<Row>> ListAsync(Guid tenantId)
        {
            var response = await Client().GetAsync(Path(tenantId, null));
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
            return JsonSerializer.Deserialize<Envelope<List<Row>>>(body, Json)!.Data!;
        }

        public Task<HttpResponseMessage> PostAsync(Guid tenantId, string? suffix, object? body) =>
            Client().PostAsync(Path(tenantId, suffix), JsonContent.Create(body, options: Json));

        public Task<HttpResponseMessage> PatchAsync(Guid tenantId, string suffix, object body) =>
            Client().PatchAsync(Path(tenantId, suffix), JsonContent.Create(body, options: Json));

        public Task<HttpResponseMessage> DeleteAsync(Guid tenantId, string suffix, object body) =>
            Client().SendAsync(new HttpRequestMessage(HttpMethod.Delete, Path(tenantId, suffix))
            {
                Content = JsonContent.Create(body, options: Json)
            });

        public static async Task<(HttpStatusCode Status, string? Code, string Body)> ReadRefusalAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            string? code = null;
            if (body.Length > 0)
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("reason_code", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    code = value.GetString();
                }
            }

            return (response.StatusCode, code, body);
        }

        private static string Path(Guid tenantId, string? suffix) =>
            $"/api/platform/tenants/{tenantId:D}/commercial/module-entitlements" + (suffix is null ? string.Empty : "/" + suffix);

        private HttpClient Client()
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", PlatformToken());
            return client;
        }

        public static readonly Guid Administrator = Guid.Parse("50050050-0000-4000-8000-0000000000d4");

        private static string PlatformToken()
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Administrator.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "platform.admin@di10.test"),
                new Claim("actor_type", "platform_admin")
            };
            var key = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience, claims, notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: key));
        }

        private static IModuleCatalogRepository Catalogue()
        {
            var catalogue = new Mock<IModuleCatalogRepository>();
            catalogue.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string code, CancellationToken _) => new ModuleCatalogItem
                {
                    ModuleCode = code,
                    ModuleName = code,
                    DisplayName = code,
                    IsBaseline = string.Equals(code, BaselineModule, StringComparison.OrdinalIgnoreCase),
                    IsCoreModule = false
                });
            return catalogue.Object;
        }

        private static IPlatformCatalogContract CatalogueContract()
        {
            var contract = new Mock<IPlatformCatalogContract>();
            contract.Setup(x => x.GetAssignableModulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AssignableModuleInfo>());
            return contract.Object;
        }

        private static ITenantSubscriptionRepository Subscriptions()
        {
            var subscriptions = new Mock<ITenantSubscriptionRepository>();
            subscriptions.Setup(x => x.GetCurrentByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid tenantId, CancellationToken _) => new TenantSubscription { TenantId = tenantId, PlanId = PlanId });
            return subscriptions.Object;
        }

        private static ISubscriptionPlanRepository Plans()
        {
            var plans = new Mock<ISubscriptionPlanRepository>();
            plans.Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { IncludedModuleKeys = [PlanModule] });
            return plans.Object;
        }

        public async ValueTask DisposeAsync()
        {
            _server.Dispose();
            await _mongo.DisposeAsync();
        }
    }

    private sealed class OnlyController(Type controller) : ControllerFeatureProvider
    {
        protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
    }

    private sealed class SilentEvents : ITransactionalIntegrationEventWriter
    {
        public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(
            IPlatformTransactionSession session, TEvent @event, EventPublishOptions options, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent =>
            Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(Guid.NewGuid(), @event.EventName, @event.EventVersion, Guid.NewGuid(), null, options.TenantId, "test", DateTimeOffset.UtcNow),
                @event));
    }

    /// <summary>The plan has room: every consume and release is granted. Limits have their own tests.</summary>
    private static IQuotaService GrantingQuota()
    {
        static Response<QuotaMutationDto> Granted(Guid tenantId) =>
            Response<QuotaMutationDto>.Success(new QuotaMutationDto(tenantId, QuotaKeys.ModulesMax, 1, 100, 1, true, null));

        var quota = new Mock<IQuotaService>();
        quota.Setup(x => x.TryConsumeEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<TryConsumeQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPlatformTransactionSession _, TryConsumeQuotaRequest request, CancellationToken _) => Granted(request.TenantId));
        quota.Setup(x => x.ReleaseEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<ReleaseQuotaRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPlatformTransactionSession _, ReleaseQuotaRequest request, CancellationToken _) => Granted(request.TenantId));
        quota.Setup(x => x.RecalculateEntitlementAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<RecalculateQuotaUsageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<QuotaStatusDto>.Fail("not measured here", 404));
        return quota.Object;
    }
}
