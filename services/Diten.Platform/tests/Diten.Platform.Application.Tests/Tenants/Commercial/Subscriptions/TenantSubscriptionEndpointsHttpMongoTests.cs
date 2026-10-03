using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
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
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants.Commercial.Subscriptions;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — the seven tenant-subscription endpoints
/// (<see cref="TenantCommercialSubscriptionsController"/>), over real HTTP, the real MediatR pipeline and a real
/// replica set.
///
/// <para><b>What was wrong (measured with this file on the commit before the fix).</b> All eight subscription
/// commands carry <c>ITransactionOwnedAuditCommand</c> since 2026-08-31, but <c>AuditBehavior</c>'s allow-list never
/// named them: every endpoint answered <c>400 "Transaction-owned audit is not authorized for …"</c> before its
/// handler ran, and nothing was written. The endpoints had been dead for a month.</para>
///
/// <para><b>What must hold now.</b> The endpoint works, the subscription changes, and ONE audit record reaches
/// <c>audit_events</c> naming the administrator and the status before and after. And — because these endpoints are
/// being switched back on — who may call them is measured here too: a tenant user is refused, a platform actor who is
/// not an active administrator is refused, and another tenant's subscription is not reachable through this tenant's
/// path.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TenantSubscriptionEndpointsHttpMongoTests
{
    private const string AuditGateRefusal = "Transaction-owned audit is not authorized";

    /// <summary>The seven write endpoints: path suffix, body, the status it needs, the status it leaves, the command.</summary>
    public static TheoryData<string, string, string, string, string> LifecycleEndpoints() => new()
    {
        { "suspend", """{"reason":"audit proof","rowVersion":null}""", "Active", "Suspended", "SuspendTenantSubscriptionCommand" },
        { "reactivate", """{"reason":"audit proof","rowVersion":null}""", "Suspended", "Active", "ReactivateTenantSubscriptionCommand" },
        { "cancel", """{"cancellationReason":"audit proof","cancelAtPeriodEnd":false,"rowVersion":null}""", "Active", "Cancelled", "CancelTenantSubscriptionCommand" },
        { "expire", "null", "Active", "Expired", "ExpireTenantSubscriptionCommand" },
        { "renew", """{"newPeriodEndUtc":"2028-01-01T00:00:00+00:00","rowVersion":null}""", "Active", "Active", "RenewTenantSubscriptionCommand" },
    };

    [Theory]
    [MemberData(nameof(LifecycleEndpoints))]
    public async Task A_lifecycle_endpoint_works_and_its_audit_record_reaches_audit_events_once(
        string action, string body, string from, string to, string command)
    {
        await using var host = await Host.StartAsync();
        var subscription = await host.SeedAsync(Host.Tenant, Enum.Parse<TenantSubscriptionStatus>(from));

        var response = await host.PostAsync(Host.AdministratorToken(), Host.Tenant, $"{subscription.Id:D}/{action}", body);
        var text = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode,
            $"POST …/{action} answered {(int)response.StatusCode}: {text} — stored status: {(await host.StoredAsync(subscription.Id)).Status}");
        Assert.Equal(to, (await host.StoredAsync(subscription.Id)).Status.ToString());

        Assert.Equal(1, await host.ProcessAuditOutboxAsync());
        var audit = Assert.Single(await host.AuditEventsAsync());
        Assert.Equal(command, audit.RequestType);
        Assert.Equal(AuditActorType.PlatformAdministrator, audit.ActorType);
        Assert.Equal(Host.Administrator, audit.ActorId);
        Assert.Equal(AuditCategory.SubscriptionBilling, audit.Category);
        Assert.Equal("TenantSubscription", audit.EntityType);
        Assert.Equal(subscription.Id, audit.EntityId);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(Host.Tenant, audit.TenantId);
        Assert.Equal(Host.Tenant, audit.TargetTenantId);
        Assert.Equal(from, audit.BeforeState!["Status"]);
        Assert.Equal(to, audit.AfterState!["Status"]);
        Assert.Equal(0, await host.DeadLettersAsync());

        Assert.Equal(0, await host.ProcessAuditOutboxAsync());
        Assert.Single(await host.AuditEventsAsync());
    }

    /// <summary>
    /// The audit gate itself, on all seven endpoints (assign and activate included — their handlers need a quota
    /// service this file does not model, so only the gate is asserted for them): no endpoint is refused for being a
    /// transaction-owned command that the pipeline does not know.
    /// </summary>
    [Theory]
    [InlineData("", """{"planId":"50050050-0000-4000-8000-0000000000c3","isTrial":false,"trialEndDateUtc":null,"currentPeriodStartUtc":"2026-10-02T00:00:00+00:00","currentPeriodEndUtc":"2027-10-02T00:00:00+00:00","source":null}""")]
    [InlineData("{0}/activate", """{"currentPeriodStartUtc":"2026-10-02T00:00:00+00:00","currentPeriodEndUtc":"2027-10-02T00:00:00+00:00","rowVersion":null}""")]
    [InlineData("{0}/suspend", """{"reason":"x","rowVersion":null}""")]
    [InlineData("{0}/reactivate", """{"reason":"x","rowVersion":null}""")]
    [InlineData("{0}/cancel", """{"cancellationReason":"x","cancelAtPeriodEnd":false,"rowVersion":null}""")]
    [InlineData("{0}/renew", """{"newPeriodEndUtc":"2028-01-01T00:00:00+00:00","rowVersion":null}""")]
    [InlineData("{0}/expire", "null")]
    public async Task No_subscription_endpoint_is_refused_by_the_audit_pipeline_gate(string route, string body)
    {
        await using var host = await Host.StartAsync();
        var subscription = await host.SeedAsync(Host.Tenant, TenantSubscriptionStatus.Active);

        var response = await host.PostAsync(Host.AdministratorToken(), Host.Tenant, string.Format(route, subscription.Id.ToString("D")), body);

        Assert.DoesNotContain(AuditGateRefusal, await response.Content.ReadAsStringAsync());
    }

    // ── who may call them ───────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, string> EveryWriteEndpoint() => new()
    {
        { "", """{"planId":"50050050-0000-4000-8000-0000000000c3","isTrial":false,"trialEndDateUtc":null,"currentPeriodStartUtc":"2026-10-02T00:00:00+00:00","currentPeriodEndUtc":"2027-10-02T00:00:00+00:00","source":null}""" },
        { "{0}/activate", """{"currentPeriodStartUtc":"2026-10-02T00:00:00+00:00","currentPeriodEndUtc":"2027-10-02T00:00:00+00:00","rowVersion":null}""" },
        { "{0}/suspend", """{"reason":"x","rowVersion":null}""" },
        { "{0}/reactivate", """{"reason":"x","rowVersion":null}""" },
        { "{0}/cancel", """{"cancellationReason":"x","cancelAtPeriodEnd":false,"rowVersion":null}""" },
        { "{0}/renew", """{"newPeriodEndUtc":"2028-01-01T00:00:00+00:00","rowVersion":null}""" },
        { "{0}/expire", "null" },
    };

    [Theory]
    [MemberData(nameof(EveryWriteEndpoint))]
    public async Task A_tenant_user_is_refused_even_with_the_permission_key_in_the_token_and_for_their_own_tenant(string route, string body)
    {
        await using var host = await Host.StartAsync();
        var subscription = await host.SeedAsync(Host.Tenant, TenantSubscriptionStatus.Active);

        var response = await host.PostAsync(Host.TenantUserToken(Host.Tenant), Host.Tenant, string.Format(route, subscription.Id.ToString("D")), body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await host.AssertUntouchedAsync(subscription);
    }

    [Theory]
    [MemberData(nameof(EveryWriteEndpoint))]
    public async Task A_platform_actor_who_is_not_an_active_administrator_is_refused(string route, string body)
    {
        await using var host = await Host.StartAsync();
        var subscription = await host.SeedAsync(Host.Tenant, TenantSubscriptionStatus.Active);

        var response = await host.PostAsync(Host.UnknownPlatformActorToken(), Host.Tenant, string.Format(route, subscription.Id.ToString("D")), body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await host.AssertUntouchedAsync(subscription);
    }

    [Theory]
    [MemberData(nameof(EveryWriteEndpoint))]
    public async Task Nobody_signed_in_is_refused(string route, string body)
    {
        await using var host = await Host.StartAsync();
        var subscription = await host.SeedAsync(Host.Tenant, TenantSubscriptionStatus.Active);

        var response = await host.PostAsync(null, Host.Tenant, string.Format(route, subscription.Id.ToString("D")), body);

        // 401 from the bearer challenge or 403 from the PlatformActor policy, depending on which answers first in the
        // host — either way nobody anonymous gets in.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        await host.AssertUntouchedAsync(subscription);
    }

    /// <summary>A subscription is addressed THROUGH its tenant: another tenant's id in the path does not find it.</summary>
    [Theory]
    [InlineData("suspend", """{"reason":"x","rowVersion":null}""")]
    [InlineData("reactivate", """{"reason":"x","rowVersion":null}""")]
    [InlineData("cancel", """{"cancellationReason":"x","cancelAtPeriodEnd":false,"rowVersion":null}""")]
    [InlineData("renew", """{"newPeriodEndUtc":"2028-01-01T00:00:00+00:00","rowVersion":null}""")]
    [InlineData("expire", "null")]
    public async Task Another_tenants_subscription_is_not_reachable_through_this_tenants_path(string action, string body)
    {
        await using var host = await Host.StartAsync();
        var theirs = await host.SeedAsync(Host.OtherTenant, TenantSubscriptionStatus.Active);

        var response = await host.PostAsync(Host.AdministratorToken(), Host.Tenant, $"{theirs.Id:D}/{action}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await host.AssertUntouchedAsync(theirs);
        Assert.Equal(0, await host.CountAsync("audit_outbox"));
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-intx-test";
        private const string Audience = "diten-platform-intx-test";
        private const string Secret = "WP-PLATFORM-AUDIT-INTX-01 subscription endpoints signing key, test only, 0123456789";
        private const string AdministratorEmail = "platform.admin@di10.test";

        public static readonly Guid Tenant = Guid.Parse("60060060-0000-4000-8000-0000000000a1");
        public static readonly Guid OtherTenant = Guid.Parse("60060060-0000-4000-8000-0000000000b2");
        public static readonly Guid Administrator = Guid.Parse("60060060-0000-4000-8000-0000000000d4");
        public static readonly Guid PlanId = Guid.Parse("50050050-0000-4000-8000-0000000000c3");

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
                    services.AddScoped<IDataScopeResolver>(_ => new Diten.Platform.Application.Tests.Tasks.FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();

                    // Real persistence, over the test-owned replica set; the audit door wired as production wires it.
                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();
                    services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
                    services.AddScoped<IPlatformTransactionExecutor, PlatformTransactionExecutor>();
                    services.AddScoped<IEntitlementStateVersionRepository, EntitlementStateVersionRepository>();
                    services.AddScoped<AuditOutboxRepository>();
                    services.AddScoped<ITransactionalAuditOutboxStore>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                    services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());

                    // Not this file's subject.
                    services.AddSingleton<ITransactionalIntegrationEventWriter, SilentEvents>();
                    services.AddSingleton(Plans());
                    services.AddSingleton(Administrators());
                    services.AddSingleton(new Mock<IQuotaService>().Object);

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TenantCommercialSubscriptionsController).Assembly));
                        manager.FeatureProviders.Add(new OnlyController(typeof(TenantCommercialSubscriptionsController)));
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
            var host = new Host(mongo, mongo.CreateDatabase());
            foreach (var tenant in new[] { Tenant, OtherTenant })
            {
                await host.Database.GetCollection<Tenant>("tenants").InsertOneAsync(new Tenant
                {
                    Id = tenant, Code = "T" + tenant.ToString("N")[..6], Slug = "t" + tenant.ToString("N")[..6], Name = "Tenant",
                    DisplayName = "Tenant", Domain = tenant.ToString("N")[..6] + ".local", Status = TenantStatus.Active
                });
            }

            return host;
        }

        public async Task<TenantSubscription> SeedAsync(Guid tenantId, TenantSubscriptionStatus status)
        {
            var subscription = new TenantSubscription
            {
                Id = Guid.NewGuid(), TenantId = tenantId, PlanId = PlanId, Status = status,
                CurrentPeriodStartUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CurrentPeriodEndUtc = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
                RowVersion = Guid.NewGuid().ToByteArray(), UpdatedBy = "seed"
            };
            await Database.GetCollection<TenantSubscription>("tenant_subscriptions").InsertOneAsync(subscription);
            return subscription;
        }

        public async Task<TenantSubscription> StoredAsync(Guid id) =>
            await Database.GetCollection<TenantSubscription>("tenant_subscriptions").Find(x => x.Id == id).SingleAsync();

        public async Task AssertUntouchedAsync(TenantSubscription seeded)
        {
            var stored = await StoredAsync(seeded.Id);
            Assert.Equal(seeded.Status, stored.Status);
            Assert.Equal(seeded.RowVersion, stored.RowVersion);
            Assert.Equal(0, await CountAsync("audit_outbox"));
        }

        public Task<long> CountAsync(string collection) =>
            Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        public Task<HttpResponseMessage> PostAsync(string? token, Guid tenantId, string suffix, string json)
        {
            var client = _server.CreateClient();
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var path = $"/api/platform/tenants/{tenantId:D}/commercial/subscription" + (suffix.Length == 0 ? string.Empty : "/" + suffix);
            return client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
        }

        /// <summary>One pass of the PRODUCTION outbox processor — the unit of work the hosted worker repeats.</summary>
        public Task<int> ProcessAuditOutboxAsync()
        {
            var context = new PlatformDbContext(_mongo.Client, Database);
            var tenantContext = new TenantContext();
            return new AuditOutboxProcessor(new AuditOutboxRepository(context), new AuditEventRepository(Database, tenantContext), tenantContext,
                new AuditOutboxPayloadMapper(),
                new AuditOutboxWorkerOptions { BatchSize = 10, MaxAttempts = 5, InitialRetryDelay = TimeSpan.FromSeconds(1), MaxRetryDelay = TimeSpan.FromSeconds(5) },
                NullLogger<AuditOutboxProcessor>.Instance).ProcessBatchAsync();
        }

        public async Task<IReadOnlyList<AuditEvent>> AuditEventsAsync() =>
            await Database.GetCollection<AuditEvent>("audit_events").Find(FilterDefinition<AuditEvent>.Empty).ToListAsync();

        public Task<long> DeadLettersAsync() =>
            Database.GetCollection<BsonDocument>("audit_outbox").CountDocumentsAsync(new BsonDocument("Status", 5));

        public static string AdministratorToken() => Token(
            new Claim(JwtRegisteredClaimNames.Sub, Administrator.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, AdministratorEmail),
            new Claim("actor_type", "platform_admin"));

        /// <summary>A platform-actor token for an address no administrator record exists for.</summary>
        public static string UnknownPlatformActorToken() => Token(
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "nobody@di10.test"),
            new Claim("actor_type", "platform_admin"));

        /// <summary>A tenant user who even carries every subscription permission key in the token.</summary>
        public static string TenantUserToken(Guid tenantId) => Token(
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "tenant.user@di10.test"),
                new Claim("actor_type", "tenant_user"),
                new Claim("tenant_id", tenantId.ToString())
            }.Concat(new[] { "assign", "activate", "cancel", "renew", "expire", "suspend", "reactivate" }
                .Select(action => new Claim("permission", $"platform.tenants.commercial.subscription.{action}"))).ToArray());

        private static string Token(params Claim[] claims)
        {
            var key = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience, claims, notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: key));
        }

        private static ISubscriptionPlanRepository Plans()
        {
            var plans = new Mock<ISubscriptionPlanRepository>();
            plans.Setup(x => x.GetByIdAsync(PlanId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionPlan { Id = PlanId, Code = "PRO", Name = "Pro", IsActive = true });
            return plans.Object;
        }

        // The production permission filter asks this repository whether the platform actor is an ACTIVE administrator.
        private static IPlatformAdministratorRepository Administrators()
        {
            var administrators = new Mock<IPlatformAdministratorRepository>();
            administrators.Setup(x => x.GetByNormalizedEmailAsync(AdministratorEmail, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlatformAdministrator
                {
                    Status = AdministratorStatus.Active,
                    InvitationStatus = AdministratorInvitationStatus.Accepted,
                    LastLoginAtUtc = DateTimeOffset.UtcNow
                });
            return administrators.Object;
        }

        public async ValueTask DisposeAsync()
        {
            _server.Dispose();
            await _mongo.DisposeAsync();
        }

        private sealed class OnlyController(Type controller) : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
        }

        private sealed class SilentEvents : ITransactionalIntegrationEventWriter
        {
            public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
                EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent =>
                Task.FromResult(new EventEnvelope<TEvent>(
                    new EventMetadata(options.EventId!.Value, @event.EventName, @event.EventVersion, options.CorrelationId!.Value, null,
                        options.TenantId, options.Producer!, options.OccurredAtUtc!.Value), @event));
        }
    }
}
