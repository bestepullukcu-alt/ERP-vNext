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
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// BL-531 — the Meetings attendee lookup is the task approver picker's SEARCH (BL-512), and the Meetings reads name
/// the people they already carry, so no Meetings screen needs the directory. Measured over HTTP on the real
/// MeetingsController / MeetingSeriesController / TasksController, the real permission filter, tenant resolution,
/// people-search rate limit, real Mongo (a test-owned replica set) and the REAL AuthService name client — only
/// AuthService itself is played by a handler that, like the real one, answers for the tenant it is asked about.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class MeetingAttendeeSearchHttpMongoTests
{
    [Theory]
    [InlineData("")]
    [InlineData("?search=a")]
    public async Task An_attendee_lookup_without_a_two_character_search_is_400_and_never_the_whole_list(string query)
    {
        await using var host = await Host.StartAsync();

        var (status, code, body) = await host.GetAsync("/api/v1/meetings/lookups/attendees" + query);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(DecisionMakerLookup.ReasonCodes.SearchTooShort, code);
        Assert.DoesNotContain("Planner", body);
    }

    [Fact]
    public async Task An_attendee_search_answers_at_most_twenty_rows_of_exactly_four_fields()
    {
        await using var host = await Host.StartAsync();

        var (status, _, body) = await host.GetAsync("/api/v1/meetings/lookups/attendees?search=planlama");

        Assert.Equal(HttpStatusCode.OK, status);
        var people = Host.People(body);
        Assert.Equal(DecisionMakerLookup.MaximumResults, people.Count);
        Assert.All(people, row => Assert.Equal(
            ["displayName", "organizationUnitName", "positionName", "userId"],
            row.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Attendee_ids_never_answer_another_tenants_user()
    {
        await using var host = await Host.StartAsync();

        var (status, _, body) = await host.GetAsync($"/api/v1/meetings/lookups/attendees?ids={Host.Organizer},{Host.OtherTenantPerson}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([Host.Organizer], Host.People(body).Select(p => p.GetProperty("userId").GetGuid()));
    }

    [Fact]
    public async Task The_task_and_meeting_searches_share_ONE_bucket_the_sixty_first_is_429_and_another_user_is_not_affected()
    {
        await using var host = await Host.StartAsync();
        var me = Guid.NewGuid();

        // ATT-FIX1 — 60 a minute (CT decision): 40 task searches + 20 meeting searches, then the 61st is refused.
        Assert.Equal(60, PeopleSearchRateLimit.PermitsPerMinute);
        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/api/v1/tasks/lookups/decision-makers?search=pl", me)).Status);
        }

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/api/v1/meetings/lookups/attendees?search=pl", me)).Status);
        }

        var refused = await host.GetAsync("/api/v1/meetings/lookups/attendees?search=pl", me);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.Status);
        Assert.Equal(DecisionMakerLookup.ReasonCodes.RateLimited, refused.Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.GetAsync("/api/v1/tasks/lookups/decision-makers?search=pl", me)).Status);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/api/v1/meetings/lookups/attendees?search=pl", Guid.NewGuid())).Status);
    }

    [Fact]
    public async Task The_meeting_read_names_its_organizer_and_attendees_in_one_call_and_never_another_tenants_user()
    {
        await using var host = await Host.StartAsync();
        host.AuthCalls.Clear();

        var (status, _, body) = await host.GetAsync($"/api/v1/meetings/{host.MeetingId}");

        Assert.True(status == HttpStatusCode.OK, $"{(int)status}: {body}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal("Organizatör Kişi", data.GetProperty("organizerDisplayName").GetString());
        var names = data.GetProperty("attendees").EnumerateArray()
            .ToDictionary(a => a.GetProperty("userId").GetGuid(), a => a.GetProperty("displayName").GetString());
        Assert.Equal("Katılımcı Kişi", names[Host.Attendee]);
        Assert.Null(names[Host.OtherTenantPerson]);
        Assert.Single(host.AuthCalls);
        Assert.All(host.AuthCalls, call => Assert.Equal(Host.TenantA, call.Tenant));
    }

    [Fact]
    public async Task The_list_series_and_report_reads_name_the_organizer_and_the_series_its_attendees()
    {
        await using var host = await Host.StartAsync();

        var list = await host.GetAsync("/api/v1/meetings?pageSize=50");
        Assert.True(list.Status == HttpStatusCode.OK, list.Body);
        var row = JsonDocument.Parse(list.Body).RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Organizatör Kişi", row.GetProperty("organizerDisplayName").GetString());

        var series = await host.GetAsync($"/api/v1/meetings/series/{host.SeriesId}");
        Assert.True(series.Status == HttpStatusCode.OK, series.Body);
        var s = JsonDocument.Parse(series.Body).RootElement.GetProperty("data");
        Assert.Equal("Organizatör Kişi", s.GetProperty("organizerDisplayName").GetString());
        var seriesNames = s.GetProperty("attendees").EnumerateArray()
            .Select(a => (a.GetProperty("userId").GetGuid(), a.GetProperty("displayName").GetString())).ToList();
        Assert.Equal([(Host.Attendee, (string?)"Katılımcı Kişi"), (Host.OtherTenantPerson, (string?)null)], seriesNames);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("O"));
        var report = await host.GetAsync($"/api/v1/meetings/report?from={from}&to={to}");
        Assert.True(report.Status == HttpStatusCode.OK, report.Body);
        var reportRow = JsonDocument.Parse(report.Body).RootElement.GetProperty("data").GetProperty("meetings").EnumerateArray().Single();
        Assert.Equal("Organizatör Kişi", reportRow.GetProperty("organizerDisplayName").GetString());
    }

    // ATT-FIX1 (2) — an AuthService that accepts and hangs costs a read at most the name timeout, never 30–100 s.
    [Fact]
    public async Task A_hanging_AuthService_costs_the_meeting_read_at_most_the_name_timeout_and_the_read_still_answers()
    {
        await using var host = await Host.StartAsync();
        host.AuthHangs = true;
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var (status, _, body) = await host.GetAsync($"/api/v1/meetings/{host.MeetingId}");

        clock.Stop();
        Assert.True(status == HttpStatusCode.OK, body);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"the read took {clock.Elapsed}");
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("organizerDisplayName").ValueKind);
    }

    // ATT-FIX1 (7) — a read whose caller shows no names resolves none; one list page is bounded on the server.
    [Fact]
    public async Task A_list_asked_without_names_and_the_series_list_ask_AuthService_nothing()
    {
        await using var host = await Host.StartAsync();
        host.AuthCalls.Clear();

        var list = await host.GetAsync("/api/v1/meetings?pageSize=50&includeNames=false");
        var series = await host.GetAsync("/api/v1/meetings/series");

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(HttpStatusCode.OK, series.Status);
        Assert.Empty(host.AuthCalls);
        var row = JsonDocument.Parse(list.Body).RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, row.GetProperty("organizerDisplayName").ValueKind);
    }

    [Fact]
    public async Task One_list_page_answers_at_most_two_hundred_rows_whatever_is_asked()
    {
        await using var host = await Host.StartAsync();
        var typeId = Guid.NewGuid();
        await host.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings).InsertManyAsync(Enumerable.Range(0, 230)
            .Select(i => new Meeting
            {
                TenantId = Host.TenantA, Title = $"Toplu {i}", MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow.AddDays(3), EndAt = DateTimeOffset.UtcNow.AddDays(3).AddHours(1),
                OrganizerUserId = Host.Organizer, IdempotencyKey = Guid.NewGuid().ToString("N")
            }));

        var (status, _, body) = await host.GetAsync("/api/v1/meetings?pageSize=1000&includeNames=false");

        Assert.Equal(HttpStatusCode.OK, status);
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        Assert.Equal(MeetingListLimits.MaxPageSize, data.GetProperty("items").GetArrayLength());
        Assert.Equal(231, data.GetProperty("totalCount").GetInt32());
    }

    // ATT-FIX1 (8a) — a reader never learns the title of a meeting they cannot open through a follow-up link.
    [Fact]
    public async Task A_follow_up_shows_the_source_title_only_to_a_reader_who_may_see_the_source()
    {
        await using var host = await Host.StartAsync();
        var reader = Guid.NewGuid();
        var meetings = host.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings);
        var secretId = Guid.NewGuid();
        await meetings.InsertManyAsync(
        [
            new Meeting
            {
                Id = secretId, TenantId = Host.TenantA, Title = "Gizli kurul", MeetingTypeId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow.AddDays(-7), EndAt = DateTimeOffset.UtcNow.AddDays(-7).AddHours(1),
                OrganizerUserId = Host.Organizer, IdempotencyKey = Guid.NewGuid().ToString("N")
            },
            new Meeting
            {
                TenantId = Host.TenantA, Title = "Okurun toplantısı", MeetingTypeId = Guid.NewGuid(), FollowUpOfMeetingId = secretId,
                StartAt = DateTimeOffset.UtcNow.AddDays(5), EndAt = DateTimeOffset.UtcNow.AddDays(5).AddHours(1),
                OrganizerUserId = reader, IdempotencyKey = Guid.NewGuid().ToString("N")
            }
        ]);

        var asReader = await host.GetAsync("/api/v1/meetings?pageSize=50&includeNames=false", reader, readAll: false);
        var asReadAll = await host.GetAsync("/api/v1/meetings?pageSize=50&includeNames=false");

        Assert.Equal(HttpStatusCode.OK, asReader.Status);
        Assert.DoesNotContain("Gizli kurul", asReader.Body);
        var mine = JsonDocument.Parse(asReader.Body).RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Single();
        Assert.Equal(secretId, mine.GetProperty("followUpOfMeetingId").GetGuid());
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("followUpOfMeetingTitle").ValueKind);
        Assert.Contains("Gizli kurul", asReadAll.Body);
    }

    // ATT-FIX1 — only the rows of the PAGE answered are named, never every meeting the list filtered.
    [Fact]
    public async Task Only_the_organizers_of_the_answered_page_are_named()
    {
        await using var host = await Host.StartAsync();
        await host.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings).InsertOneAsync(new Meeting
        {
            TenantId = Host.TenantA, Title = "Sonraki", MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(9), EndAt = DateTimeOffset.UtcNow.AddDays(9).AddHours(1),
            OrganizerUserId = Host.Attendee, IdempotencyKey = Guid.NewGuid().ToString("N")
        });
        host.AuthCalls.Clear();

        var (status, _, _) = await host.GetAsync("/api/v1/meetings?pageSize=1");

        Assert.Equal(HttpStatusCode.OK, status);
        var call = Assert.Single(host.AuthCalls);
        Assert.Equal([Host.Attendee], call.Ids.Split(',').Select(Guid.Parse));
    }

    // ATT-FIX2 (2) — the minutes read names every version's people in ONE bounded call: a hanging AuthService
    // costs it the bound, not 100 s per chunk per version.
    [Fact]
    public async Task A_hanging_AuthService_costs_the_minutes_read_at_most_the_bound()
    {
        await using var host = await Host.StartAsync();
        var minutes = host.Database.GetCollection<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions);
        foreach (var number in new[] { 1, 2, 3 })
        {
            await minutes.InsertOneAsync(new MeetingMinutesVersion
            {
                TenantId = Host.TenantA, MeetingId = host.MeetingId, VersionNumber = number,
                Attendance = [new MinutesAttendanceRecord { AttendeeUserId = Host.Attendee, Status = AttendanceStatus.Present }],
                PublishedByUserId = Host.Organizer
            });
        }

        host.AuthHangs = true;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var (status, _, body) = await host.GetAsync($"/api/v1/meetings/{host.MeetingId}/minutes");
        clock.Stop();

        Assert.True(status == HttpStatusCode.OK, body);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"the minutes read took {clock.Elapsed}");
        Assert.Equal(3, JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("versions").GetArrayLength());
    }

    [Fact]
    public async Task A_hanging_AuthService_costs_the_assignment_list_at_most_the_bound()
    {
        await using var host = await Host.StartAsync();
        host.AuthHangs = true;
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var (status, _, body) = await host.GetAsync("/api/v1/tasks/lookups/assignable-people");

        clock.Stop();
        Assert.True(status == HttpStatusCode.OK, body);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"the assignment list took {clock.Elapsed}");
        // Not vacuous: the list HAD people to name (so the hanging name request was really made).
        Assert.NotEmpty(JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("people").EnumerateArray());
    }

    // ATT-FIX2 (3) — the DETAIL read applies the list's rule: a linked meeting's title only for a reader who may open it.
    [Fact]
    public async Task The_detail_names_a_linked_meeting_only_for_a_reader_who_may_open_it()
    {
        await using var host = await Host.StartAsync();
        var reader = Guid.NewGuid();
        var meetings = host.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings);
        var secretBefore = Guid.NewGuid();
        var mine = Guid.NewGuid();
        await meetings.InsertManyAsync(
        [
            new Meeting
            {
                Id = secretBefore, TenantId = Host.TenantA, Title = "Gizli önceki", MeetingTypeId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow.AddDays(-7), EndAt = DateTimeOffset.UtcNow.AddDays(-7).AddHours(1),
                OrganizerUserId = Host.Organizer, IdempotencyKey = Guid.NewGuid().ToString("N")
            },
            new Meeting
            {
                Id = mine, TenantId = Host.TenantA, Title = "Okurun", MeetingTypeId = Guid.NewGuid(), FollowUpOfMeetingId = secretBefore,
                StartAt = DateTimeOffset.UtcNow.AddDays(5), EndAt = DateTimeOffset.UtcNow.AddDays(5).AddHours(1),
                OrganizerUserId = reader, IdempotencyKey = Guid.NewGuid().ToString("N")
            },
            new Meeting
            {
                TenantId = Host.TenantA, Title = "Gizli sonraki", MeetingTypeId = Guid.NewGuid(), FollowUpOfMeetingId = mine,
                StartAt = DateTimeOffset.UtcNow.AddDays(12), EndAt = DateTimeOffset.UtcNow.AddDays(12).AddHours(1),
                OrganizerUserId = Host.Organizer, IdempotencyKey = Guid.NewGuid().ToString("N")
            }
        ]);

        var asReader = await host.GetAsync($"/api/v1/meetings/{mine}", reader, readAll: false);
        var asReadAll = await host.GetAsync($"/api/v1/meetings/{mine}");

        Assert.True(asReader.Status == HttpStatusCode.OK, asReader.Body);
        var data = JsonDocument.Parse(asReader.Body).RootElement.GetProperty("data");
        Assert.Equal(secretBefore, data.GetProperty("followUpOfMeetingId").GetGuid());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("followUpOfMeetingTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("followedByMeetingTitle").ValueKind);
        Assert.DoesNotContain("Gizli", asReader.Body);
        Assert.Contains("Gizli önceki", asReadAll.Body);
        Assert.Contains("Gizli sonraki", asReadAll.Body);
    }

    // ATT-FIX2 (4) — same-moment meetings keep one order across pages; a titles-only read names and types nobody.
    [Fact]
    public async Task Same_moment_meetings_never_swap_between_pages_and_a_titles_only_read_is_light()
    {
        await using var host = await Host.StartAsync();
        var at = DateTimeOffset.UtcNow.AddDays(30);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        await host.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings).InsertManyAsync(ids.Select(id => new Meeting
        {
            Id = id, TenantId = Host.TenantA, Title = "Aynı an", MeetingTypeId = Guid.NewGuid(),
            StartAt = at, EndAt = at.AddHours(1), OrganizerUserId = Host.Organizer, IdempotencyKey = Guid.NewGuid().ToString("N")
        }));

        var paged = new List<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var (_, _, body) = await host.GetAsync($"/api/v1/meetings?pageSize=1&page={page}&includeNames=false");
            paged.Add(JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetGuid());
        }

        Assert.Equal(ids.OrderBy(id => id).ToList(), paged);

        host.AuthCalls.Clear();
        var light = await host.GetAsync("/api/v1/meetings?pageSize=50&titlesOnly=true");
        Assert.Equal(HttpStatusCode.OK, light.Status);
        Assert.Empty(host.AuthCalls);
        Assert.All(JsonDocument.Parse(light.Body).RootElement.GetProperty("data").GetProperty("items").EnumerateArray(),
            row => Assert.Equal(string.Empty, row.GetProperty("meetingTypeName").GetString()));
    }

    // ── host ────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Host : IAsyncDisposable
    {
        private const string Issuer = "diten-auth-bl531-test";
        private const string Audience = "diten-platform-bl531-test";
        private const string Secret = "BL-531 meetings attendee search signing key, test only, 0123456789";

        public static readonly Guid TenantA = Guid.Parse("53153153-0000-4000-8000-0000000000a1");
        public static readonly Guid TenantB = Guid.Parse("53153153-0000-4000-8000-0000000000b2");
        public static readonly Guid Organizer = Guid.Parse("53153153-0000-4000-8000-00000000c001");
        public static readonly Guid Attendee = Guid.Parse("53153153-0000-4000-8000-00000000c002");
        public static readonly Guid OtherTenantPerson = Guid.Parse("53153153-0000-4000-8000-00000000c003");
        /// <summary>The home unit, granted as the caller's data scope so the ASSIGNMENT list has people to name.</summary>
        public static readonly Guid HomeUnit = Guid.Parse("53153153-0000-4000-8000-0000000000e1");

        private readonly DisposableMongoReplicaSet _mongo;
        private readonly TestServer _server;
        private readonly FakeAuth _auth;

        public Guid MeetingId { get; private init; }
        public Guid SeriesId { get; private init; }
        public List<(Guid Tenant, string Ids)> AuthCalls => _auth.Calls;

        private Host(DisposableMongoReplicaSet mongo, IMongoDatabase database, FakeAuth auth)
        {
            _mongo = mongo;
            _auth = auth;
            var dbContext = new PlatformDbContext(mongo.Client, database);

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddMemoryCache();
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
                    services.AddScoped<IDataScopeResolver>(_ => new Diten.Platform.Application.Tests.Tasks.FakeDataScopeResolver(
                        new Diten.Platform.Common.Authorization.EntitlementDataScope(
                            Diten.Platform.Common.Authorization.EntitlementDataScopeKind.OrgUnit, HomeUnit, "HOME")));
                    services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<Diten.Platform.API.Observability.ICorrelationContext, Diten.Platform.API.Observability.CorrelationContext>();
                    services.AddScoped<IActorPermissionContext, ClaimsActorPermissionContext>();

                    services.AddSingleton<IPlatformDbContext>(dbContext);
                    services.AddScoped<IPositionRepository, PositionRepository>();
                    services.AddScoped<IPositionAssignmentRepository, PositionAssignmentRepository>();
                    services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
                    services.AddScoped<ITaskTransitionRepository, TaskTransitionRepository>();
                    services.AddScoped<ITaskItemRepository, TaskItemRepository>();
                    services.AddScoped<IRecordLinkRepository, RecordLinkRepository>();
                    services.AddScoped<IMeetingRepository, MeetingRepository>();
                    services.AddScoped<IMeetingAttendeeRepository, MeetingAttendeeRepository>();
                    services.AddScoped<IAgendaItemRepository, AgendaItemRepository>();
                    services.AddScoped<IMeetingTypeRepository, MeetingTypeRepository>();
                    services.AddScoped<IMeetingMinutesVersionRepository, MeetingMinutesVersionRepository>();
                    services.AddScoped<IMeetingSeriesRepository, MeetingSeriesRepository>();

                    // THE production name client; only AuthService behind it is played (per tenant, like the real one).
                    services.AddSingleton<IHttpClientFactory>(auth);
                    services.AddSingleton<IOptions<AuthServiceOptions>>(Options.Create(new AuthServiceOptions
                    {
                        BaseUrl = "http://auth.bl531.test", InternalApiKey = "bl531-test-key-not-a-secret"
                    }));
                    services.AddScoped<AuthUserDisplayNameClient>();
                    services.AddScoped<IUserDisplayNameResolver>(sp => sp.GetRequiredService<AuthUserDisplayNameClient>());
                    services.AddScoped<IUserDisplayNameChecker>(sp => sp.GetRequiredService<AuthUserDisplayNameClient>());
                    services.AddSingleton<Diten.Platform.Application.Contracts.Audit.IAuditOutboxWriter>(new Diten.Platform.Application.Tests.Audit.InMemoryAuditOutbox());

                    services.AddControllers().ConfigureApplicationPartManager(manager =>
                    {
                        manager.ApplicationParts.Clear();
                        manager.ApplicationParts.Add(new AssemblyPart(typeof(TasksController).Assembly));
                        manager.FeatureProviders.Add(new OnlyControllers(typeof(TasksController), typeof(MeetingsController), typeof(MeetingSeriesController)));
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
            var auth = new FakeAuth();
            var units = database.GetCollection<OrganizationUnit>(PlatformCollections.OrganizationUnits);
            var positions = database.GetCollection<Position>(PlatformCollections.Positions);
            var assignments = database.GetCollection<PositionAssignment>(PlatformCollections.PositionAssignments);

            async Task PersonAsync(Guid tenant, Guid unitId, Guid userId, string name, string position)
            {
                var positionId = Guid.NewGuid();
                await positions.InsertOneAsync(new Position
                {
                    Id = positionId, TenantId = tenant, Code = "P" + positionId.ToString("N")[..8], Name = position,
                    OrganizationUnitId = unitId, Status = PositionStatus.Active
                });
                await assignments.InsertOneAsync(new PositionAssignment
                {
                    TenantId = tenant, PositionId = positionId, UserId = userId, EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1)
                });
                auth.Add(tenant, userId, name);
            }

            var home = HomeUnit;
            var tenantBUnit = Guid.NewGuid();
            await units.InsertManyAsync(
            [
                new OrganizationUnit { Id = home, TenantId = TenantA, Code = "TR-PLAN", Name = "Planlama", LegalEntityId = Guid.NewGuid() },
                new OrganizationUnit { Id = tenantBUnit, TenantId = TenantB, Code = "B", Name = "Başka", LegalEntityId = Guid.NewGuid() }
            ]);
            await PersonAsync(TenantA, home, Organizer, "Organizatör Kişi", "Kalite Müdürü");
            await PersonAsync(TenantA, home, Attendee, "Katılımcı Kişi", "Kalite Uzmanı");
            for (var i = 0; i < 21; i++)
            {
                await PersonAsync(TenantA, home, Guid.NewGuid(), $"Planner {i:00}", "Planlama Uzmanı");
            }

            await PersonAsync(TenantB, tenantBUnit, OtherTenantPerson, "Başka Kiracı", "Kalite Müdürü");

            // One meeting and one series of tenant A that (by a stale or forged id) name tenant B's user: the reads
            // must not turn that id into tenant B's name.
            var typeId = Guid.NewGuid();
            await database.GetCollection<MeetingType>(PlatformCollections.MeetingTypes)
                .InsertOneAsync(new MeetingType { Id = typeId, TenantId = TenantA, Name = "Kalite" });
            var meetingId = Guid.NewGuid();
            await database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings).InsertOneAsync(new Meeting
            {
                Id = meetingId, TenantId = TenantA, Title = "Kalite gözden geçirme", MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow.AddDays(1), EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString("N")
            });
            await database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees).InsertManyAsync(
            [
                new MeetingAttendee { TenantId = TenantA, MeetingId = meetingId, UserId = Attendee },
                new MeetingAttendee { TenantId = TenantA, MeetingId = meetingId, UserId = OtherTenantPerson }
            ]);
            var seriesId = Guid.NewGuid();
            await database.GetCollection<MeetingSeries>(PlatformCollections.MeetingSeries).InsertOneAsync(new MeetingSeries
            {
                Id = seriesId, TenantId = TenantA, Name = "Aylık kalite", MeetingTypeId = typeId,
                StartsAt = DateTimeOffset.UtcNow.AddDays(2), OrganizerUserId = Organizer, AttendeeUserIds = [Attendee, OtherTenantPerson]
            });

            return new Host(mongo, database, auth) { MeetingId = meetingId, SeriesId = seriesId, Database = database };
        }

        public bool AuthHangs { get => _auth.Hang; set => _auth.Hang = value; }
        public IMongoDatabase Database { get; private init; } = null!;

        public async Task<(HttpStatusCode Status, string? Code, string Body)> GetAsync(string path, Guid? userId = null, bool readAll = true)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId ?? Organizer, readAll));
            var response = await client.GetAsync(path);
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

        private static string Token(Guid userId, bool readAll) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience,
                new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new Claim("actor_type", "tenant_user"),
                    new Claim("tenant_id", TenantA.ToString()),
                    new Claim("permission", TaskPermissions.Create),
                    new Claim("permission", TaskPermissions.Assign),
                    new Claim("permission", MeetingPermissions.Create),
                    new Claim("permission", MeetingPermissions.Read),
                    new Claim("permission", MeetingPermissions.SeriesManage)
                }.Concat(readAll ? [new Claim("permission", MeetingPermissions.ReadAll)] : Array.Empty<Claim>()),
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        public ValueTask DisposeAsync()
        {
            _server.Dispose();
            return _mongo.DisposeAsync();
        }

        /// <summary>AuthService's display-name endpoint, played the way the real one behaves: it answers ONLY for the
        /// tenant in the query string (the one the name client took from the server-side tenant context).</summary>
        private sealed class FakeAuth : HttpMessageHandler, IHttpClientFactory
        {
            private readonly Dictionary<(Guid Tenant, Guid User), string> _names = new();

            public List<(Guid Tenant, string Ids)> Calls { get; } = [];
            /// <summary>AuthService accepts the connection and never answers (the "started before mongod" state).</summary>
            public bool Hang { get; set; }

            public void Add(Guid tenant, Guid user, string name)
            {
                lock (_names) { _names[(tenant, user)] = name; }
            }

            public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (Hang) { await Task.Delay(Timeout.Infinite, ct); }
                var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
                var tenant = Guid.Parse(query["tenantId"]!);
                var ids = query["ids"] ?? string.Empty;
                lock (Calls) { Calls.Add((tenant, ids)); }
                List<object> rows;
                lock (_names)
                {
                    rows = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(Guid.Parse)
                        .Where(id => _names.ContainsKey((tenant, id)))
                        .Select(id => (object)new { id, displayName = _names[(tenant, id)] })
                        .ToList();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(rows), Encoding.UTF8, "application/json")
                };
            }
        }

        private sealed class OnlyControllers(params Type[] controllers) : ControllerFeatureProvider
        {
            protected override bool IsController(System.Reflection.TypeInfo typeInfo) => controllers.Contains(typeInfo.AsType());
        }
    }
}
