using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
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
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-512 — the approver / reviewer picker is SEARCH-ONLY, measured over HTTP on the real TasksController, the real
/// permission filter, the real tenant-resolution middleware, the real people-search rate limit and real Mongo (a
/// test-owned replica set — never the shared database). Tenant A has: İlker Şahin (Kalite Müdürü, in ANOTHER company
/// of the group), 21 planners, one person whose position has ended; tenant B has one person.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class DecisionMakerSearchHttpMongoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  a  ")]
    public async Task A_request_without_a_search_of_at_least_two_characters_is_400_and_never_the_whole_list(string? search)
    {
        await using var host = await Host.StartAsync();

        var (status, code, body) = await host.GetAsync(search is null ? "" : $"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("PEOPLE_SEARCH_TOO_SHORT", code);
        Assert.DoesNotContain("Planner", body);
    }

    [Fact]
    public async Task Two_characters_find_a_person_and_each_row_has_exactly_four_fields()
    {
        await using var host = await Host.StartAsync();

        var (status, _, body) = await host.GetAsync("?search=il");

        Assert.True(status == HttpStatusCode.OK, $"{(int)status}: {body}");
        var person = Assert.Single(Host.People(body));
        Assert.Equal(new[] { "displayName", "organizationUnitName", "positionName", "userId" },
            person.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(Host.Ilker, person.GetProperty("userId").GetGuid());
        Assert.Equal("Kalite Müdürü", person.GetProperty("positionName").GetString());
    }

    [Fact]
    public async Task Twenty_one_matches_answer_twenty()
    {
        await using var host = await Host.StartAsync();

        var (status, _, body) = await host.GetAsync("?search=planlama");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(20, Host.People(body).Count);
    }

    [Theory]
    [InlineData("ILKER")]
    [InlineData("ılker")]
    [InlineData("İLKER")]
    [InlineData("SAHIN")]
    [InlineData("şahin")]
    [InlineData("kalite mudur")]
    public async Task Turkish_letters_and_case_are_folded_one_explicit_way(string search)
    {
        await using var host = await Host.StartAsync();

        var (status, _, body) = await host.GetAsync($"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(Host.Ilker, Assert.Single(Host.People(body)).GetProperty("userId").GetGuid());
    }

    // BL-512 FIX1 — Ğ, Ö and Ç (and Ü) folded both ways, in a name and in a position name.
    [Theory]
    [InlineData("GOKCE OGUTCU")]
    [InlineData("gökçe öğütçü")]
    [InlineData("GÖKÇE ÖĞÜTÇÜ")]
    [InlineData("cagri merkezi")]
    [InlineData("ÇAĞRI MERKEZİ")]
    public async Task G_O_and_C_with_their_marks_are_folded_too(string search)
    {
        await using var host = await Host.StartAsync();

        var (status, _, body) = await host.GetAsync($"?search={Uri.EscapeDataString(search)}");

        Assert.True(status == HttpStatusCode.OK, body);
        Assert.Equal(Host.Gokce, Assert.Single(Host.People(body)).GetProperty("userId").GetGuid());
    }

    // BL-512 FIX1 — typing "Ka… Kal…" does not rebuild the directory (and ask AuthService for every name) per pause.
    [Fact]
    public async Task Two_searches_in_a_row_build_the_directory_once()
    {
        await using var host = await Host.StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("?search=il")).Status);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("?search=pl")).Status);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync($"?ids={Host.Ilker}")).Status);

        Assert.Equal(1, host.NameSource.Calls);
    }

    // BL-512 FIX1 — names that cannot be read are a 503 the screen can say, never "person not found" rows; and that
    // state is not kept, so the next search after AuthService is back answers normally.
    [Fact]
    public async Task When_the_names_cannot_be_read_the_search_is_503_and_nothing_is_kept()
    {
        await using var host = await Host.StartAsync();
        host.NameSource.Unavailable = true;

        var (status, code, body) = await host.GetAsync("?search=il");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal(DecisionMakerLookup.ReasonCodes.DirectoryUnavailable, code);
        Assert.DoesNotContain("Kalite", body);

        host.NameSource.Unavailable = false;
        var again = await host.GetAsync("?search=il");
        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Contains(Host.People(again.Body), p => p.GetProperty("userId").GetGuid() == Host.Ilker);
    }

    [Fact]
    public async Task Ids_answer_only_live_people_of_this_tenant_and_another_company_is_still_found()
    {
        await using var host = await Host.StartAsync();

        // İlker works for another company of the group (approval is scope-exempt, BL-057); the others must drop.
        var ids = string.Join(",", Host.Ilker, Host.OtherTenantPerson, Host.EndedPerson, Guid.NewGuid());
        var (status, _, body) = await host.GetAsync($"?ids={ids}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new[] { Host.Ilker }, Host.People(body).Select(p => p.GetProperty("userId").GetGuid()));
    }

    [Fact]
    public async Task Eleven_ids_or_a_search_together_with_ids_are_400()
    {
        await using var host = await Host.StartAsync();

        var eleven = string.Join(",", Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()));
        Assert.Equal("PEOPLE_LOOKUP_TOO_MANY_IDS", (await host.GetAsync($"?ids={eleven}")).Code);
        Assert.Equal("PEOPLE_LOOKUP_SEARCH_AND_IDS", (await host.GetAsync($"?search=il&ids={Host.Ilker}")).Code);
        Assert.Equal("PEOPLE_LOOKUP_IDS_INVALID", (await host.GetAsync("?ids=not-a-guid")).Code);
    }

    [Fact]
    public async Task The_thirty_first_search_in_a_minute_is_429_and_another_user_is_not_affected()
    {
        await using var host = await Host.StartAsync();
        var me = Guid.NewGuid();
        Assert.Equal(30, PeopleSearchRateLimit.PermitsPerMinute); // the prompt's number, pinned: the loop below reads the constant

        for (var i = 0; i < PeopleSearchRateLimit.PermitsPerMinute; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("?search=il", me)).Status);
        }

        var refused = await host.GetAsync("?search=il", me);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.Status);
        Assert.Equal("PEOPLE_SEARCH_RATE_LIMITED", refused.Code);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("?search=il", Guid.NewGuid())).Status);
    }

    // ── host ────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-bl512-test";
        private const string Audience = "diten-platform-bl512-test";
        private const string Secret = "BL-512 decision makers search signing key, test only, 0123456789";

        public static readonly Guid TenantA = Guid.Parse("51251251-0000-4000-8000-0000000000a1");
        public static readonly Guid TenantB = Guid.Parse("51251251-0000-4000-8000-0000000000b2");
        public static readonly Guid Ilker = Guid.Parse("51251251-0000-4000-8000-00000000c001");
        public static readonly Guid EndedPerson = Guid.Parse("51251251-0000-4000-8000-00000000c002");
        public static readonly Guid OtherTenantPerson = Guid.Parse("51251251-0000-4000-8000-00000000c003");
        public static readonly Guid Gokce = Guid.Parse("51251251-0000-4000-8000-00000000c004");

        public FixedNames NameSource { get; } = new();

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;
        private static readonly Dictionary<Guid, string> Names = new();

        private Host(DisposableMongoReplicaSet mongo, IMongoDatabase database)
        {
            _mongo = mongo;
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
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    // THE production limiter registration (Program.cs calls the same extension).
                    services.AddPeopleSearchRateLimit();

                    services.AddApplication();
                    services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<Diten.Platform.API.Observability.ICorrelationContext, Diten.Platform.API.Observability.CorrelationContext>();
                    services.AddScoped<IActorPermissionContext, ClaimsActorPermissionContext>();

                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<IPositionRepository, PositionRepository>();
                    services.AddScoped<IPositionAssignmentRepository, PositionAssignmentRepository>();
                    services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
                    // AuthService is not running here: the names are the test's own.
                    services.AddSingleton<IUserDisplayNameResolver>(NameSource);
                    services.AddSingleton<Diten.Platform.Application.Contracts.Audit.IAuditOutboxWriter>(new Diten.Platform.Application.Tests.Audit.InMemoryAuditOutbox());

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TasksController).Assembly));
                        manager.FeatureProviders.Add(new OnlyController(typeof(TasksController)));
                    });
                })
                .Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.UseRouting();
                    app.UsePlatformAccessPipeline();   // THE production order (Program.cs calls the same method)
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public static async Task<Host> StartAsync()
        {
            var mongo = await DisposableMongoReplicaSet.StartAsync();
            var database = mongo.CreateDatabase();
            var units = database.GetCollection<OrganizationUnit>(PlatformCollections.OrganizationUnits);
            var positions = database.GetCollection<Position>(PlatformCollections.Positions);
            var assignments = database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments);

            async Task<Guid> PersonAsync(Guid tenant, Guid unitId, Guid userId, string name, string position, bool ended = false)
            {
                var positionId = Guid.NewGuid();
                await positions.InsertOneAsync(new Position
                {
                    Id = positionId, TenantId = tenant, Code = "P" + positionId.ToString("N")[..8], Name = position,
                    OrganizationUnitId = unitId, Status = PositionStatus.Active
                });
                await assignments.InsertOneAsync(new PositionAssignment
                {
                    TenantId = tenant, PositionId = positionId, UserId = userId,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1),
                    EffectiveTo = ended ? DateTimeOffset.UtcNow.AddDays(-1) : null
                });
                lock (Names) { Names[userId] = name; }
                return userId;
            }

            var home = Guid.NewGuid();
            var otherCompany = Guid.NewGuid();
            var tenantBUnit = Guid.NewGuid();
            await units.InsertManyAsync(
            [
                new OrganizationUnit { Id = home, TenantId = TenantA, Code = "TR-PLAN", Name = "Planlama", LegalEntityId = Guid.NewGuid() },
                new OrganizationUnit { Id = otherCompany, TenantId = TenantA, Code = "AZ-QA", Name = "Kalite", LegalEntityId = Guid.NewGuid() },
                new OrganizationUnit { Id = tenantBUnit, TenantId = TenantB, Code = "B", Name = "Başka", LegalEntityId = Guid.NewGuid() }
            ]);
            await PersonAsync(TenantA, otherCompany, Ilker, "İlker Şahin", "Kalite Müdürü");
            await PersonAsync(TenantA, home, Gokce, "Gökçe Öğütçü", "Çağrı Merkezi Uzmanı");
            for (var i = 0; i < 21; i++)
            {
                await PersonAsync(TenantA, home, Guid.NewGuid(), $"Planner {i:00}", "Planlama Uzmanı");
            }

            await PersonAsync(TenantA, home, EndedPerson, "Eski Çalışan", "Planlama Uzmanı", ended: true);
            await PersonAsync(TenantB, tenantBUnit, OtherTenantPerson, "İlker Başka", "Kalite Müdürü");
            return new Host(mongo, database);
        }

        public async Task<(HttpStatusCode Status, string? Code, string Body)> GetAsync(string query, Guid? userId = null)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId ?? Guid.NewGuid()));
            var response = await client.GetAsync("/api/v1/tasks/lookups/decision-makers" + query);
            var body = await response.Content.ReadAsStringAsync();
            string? code = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var json = JsonDocument.Parse(body);
                foreach (var name in new[] { "reasonCode", "reason_code" })
                {
                    if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty(name, out var value)
                        && value.ValueKind == JsonValueKind.String)
                    {
                        code = value.GetString();
                    }
                }
            }

            return (response.StatusCode, code, body);
        }

        public static IReadOnlyList<JsonElement> People(string body) =>
            JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("people").EnumerateArray().Select(p => p.Clone()).ToList();

        private static string Token(Guid userId) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience,
                [
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new Claim("actor_type", "tenant_user"),
                    new Claim("tenant_id", TenantA.ToString()),
                    new Claim("permission", TaskPermissions.Create)
                ],
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            return _mongo.DisposeAsync();
        }

        /// <summary>AuthService's names, counted; <see cref="Unavailable"/> answers nothing, as the real client does
        /// when AuthService cannot be reached.</summary>
        public sealed class FixedNames : IUserDisplayNameResolver
        {
            private int _calls;
            public int Calls => _calls;
            public bool Unavailable { get; set; }

            public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _calls);
                if (Unavailable) { return Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>()); }
                lock (Names)
                {
                    IReadOnlyDictionary<Guid, string> found = userIds.Where(Names.ContainsKey).ToDictionary(id => id, id => Names[id]);
                    return Task.FromResult(found);
                }
            }
        }

        private sealed class OnlyController(Type controller) : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
        }
    }
}
