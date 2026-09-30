using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Middleware;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Application.Services;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// ONE disposable mongod for every MOD-0280-FU01 HTTP test — started on a free port, thrown away at the end. Nothing
/// touches the shared dev instance on 27017. A FIXED database name (DB-010): isolation is by TenantId inside it, and only
/// the profiles the module actually reads are built (never the whole schema).
/// </summary>
public sealed class TimeEntryMongoFixture : IAsyncLifetime
{
    private DisposableStandaloneMongo? _mongo;

    public IPlatformDbContext DbContext { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PlatformTestSerializers.Register();
        _mongo = await DisposableStandaloneMongo.StartAsync();

        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{_mongo.Port}/?directConnection=true");
#pragma warning disable CS0618
        settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        Database = client.GetDatabase("diten_platform_standalone_time_entry");
        DbContext = new PlatformDbContext(client, Database);

        await PlatformSchemaManifest.ApplyAsync(Database,
        [
            SchemaProfile.TimeEntry, SchemaProfile.WorkflowWorkCenter, SchemaProfile.Eventing, SchemaProfile.Organization,
            SchemaProfile.Meetings
        ]);
    }

    public async Task DisposeAsync()
    {
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class TimeEntryMongoCollection : ICollectionFixture<TimeEntryMongoFixture>
{
    public const string Name = "time-entry-mongo";
}

/// <summary>A clock the test moves. Every "now" the module reads comes from here — never the machine's clock.</summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>Records every audit append the REAL <see cref="AuditBehavior{TRequest,TResponse}"/> makes.</summary>
public sealed class CapturingAuditService : IAuditService
{
    private readonly List<AuditAppendRequest> _requests = [];

    public IReadOnlyList<AuditAppendRequest> Requests
    {
        get { lock (_requests) { return _requests.ToList(); } }
    }

    public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
    {
        lock (_requests)
        {
            _requests.Add(request);
            return Task.FromResult(AuditAppendResult.Queued($"t:{_requests.Count}"));
        }
    }
}

/// <summary>Names the people the tests use; anyone else stays unnamed (never the id in the name's place).</summary>
public sealed class TestDisplayNames : IUserDisplayNameResolver
{
    public Dictionary<Guid, string> Names { get; } = new();

    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            userIds.Where(Names.ContainsKey).ToDictionary(id => id, id => Names[id]));
}

/// <summary>
/// Weekends off, everything else a working day, except the dates the test names: <see cref="Holidays"/> (full, off) and
/// <see cref="HalfDays"/> (a half-day holiday — reported, like the real engine does, as a WORKING day that carries a
/// half-day holiday). Resolved throughout; the calendar engine has its own suite.
/// </summary>
public sealed class StubWorkingCalendar : IWorkingCalendarProvider
{
    public HashSet<DateOnly> Holidays { get; } = [];
    public HashSet<DateOnly> HalfDays { get; } = [];

    /// <summary>T2b — dates the calendar cannot answer for (no calendar for the country): reported unresolved, as the real
    /// engine does, so the week shows the "calendar not defined" notice.</summary>
    public HashSet<DateOnly> Unresolved { get; } = [];

    public Task<WorkingDayResult> IsWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        HolidayInfo? holiday = null;
        bool working;
        if (HalfDays.Contains(date))
        {
            holiday = new HolidayInfo(Guid.NewGuid(), "HALF", "Arife", date, date, "PublicHoliday", true, false);
            working = true;
        }
        else if (Holidays.Contains(date))
        {
            holiday = new HolidayInfo(Guid.NewGuid(), "FULL", "Bayram", date, date, "PublicHoliday", false, false);
            working = false;
        }
        else
        {
            working = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        }

        if (Unresolved.Contains(date))
        {
            return Task.FromResult(new WorkingDayResult(
                WorkingCalendarResolution.CalendarMissing, null, date, scope.CountryCode, null, null, null, "stub", []));
        }

        return Task.FromResult(new WorkingDayResult(
            WorkingCalendarResolution.Resolved, working, date, scope.CountryCode, Guid.NewGuid(), null, holiday, "stub", []));
    }

    public Task<HolidayLookupResult> GetHolidayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<WorkingDateResult> NextWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<WorkingDateResult> AddWorkingDaysAsync(DateOnly start, int days, WorkingCalendarScope scope, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<WorkingDayCountResult> WorkingDaysBetweenAsync(DateOnly from, DateOnly to, WorkingCalendarScope scope, CancellationToken ct = default)
        => throw new NotSupportedException();
}

/// <summary>The two production crash seams, driven by the test: throw between two writes, or interleave another run.</summary>
public sealed class TestProbes : ITimesheetSubmissionProbe, ITimesheetFinalizationProbe
{
    public Func<Guid, Task>? AfterApprovalStarted { get; set; }
    public Func<Guid, Task>? BeforeTaskTotalWrite { get; set; }
    public Func<Guid, Task>? BeforeWithdrawCancel { get; set; }

    public Task AfterApprovalStartedAsync(Guid weekId, CancellationToken ct)
        => AfterApprovalStarted?.Invoke(weekId) ?? Task.CompletedTask;

    public Task BeforeWithdrawCancelAsync(Guid weekId, CancellationToken ct)
        => BeforeWithdrawCancel?.Invoke(weekId) ?? Task.CompletedTask;

    /// <summary>CT acceptance (T1b v3): throw here to make the save/submit timer-draft recompute fail.</summary>
    public Func<Guid, string, Task>? BeforeWeekDraftRecompute { get; set; }

    public Task BeforeWeekDraftRecomputeAsync(Guid userId, string weekKey, CancellationToken ct)
        => BeforeWeekDraftRecompute?.Invoke(userId, weekKey) ?? Task.CompletedTask;

    public Task BeforeTaskTotalWriteAsync(Guid taskItemId, CancellationToken ct)
        => BeforeTaskTotalWrite?.Invoke(taskItemId) ?? Task.CompletedTask;
}

/// <summary>Counts the morning notifications instead of sending them (the real one needs AuthService and a template).</summary>
public sealed class CountingAutoCloseNotifier : ITimerAutoCloseNotifier
{
    private readonly List<Guid> _segments = [];

    public IReadOnlyList<Guid> Segments
    {
        get { lock (_segments) { return _segments.ToList(); } }
    }

    /// <summary>People whose notification throws — a stand-in for "something failed for this one person" (v3 G4).</summary>
    public HashSet<Guid> ThrowFor { get; } = [];

    public Task NotifyAsync(Diten.Platform.Domain.Entities.TimeEntry.TimerSegment segment, CancellationToken ct = default)
    {
        if (ThrowFor.Contains(segment.UserId))
        {
            throw new InvalidOperationException("test: this person's notification fails");
        }

        lock (_segments)
        {
            _segments.Add(segment.Id);
        }

        return Task.CompletedTask;
    }
}

/// <summary>The caller as the audit behaviour reads it: an authenticated tenant user, straight from the token.</summary>
internal sealed class HttpPrincipal(IHttpContextAccessor accessor) : ITenantAuthorizationContext
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public Guid TenantId => Guid.TryParse(User?.FindFirst("tenant_id")?.Value, out var id) ? id : Guid.Empty;
    public Guid UserId => Guid.TryParse(User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : Guid.Empty;
    public string? ActorType => User?.FindFirst("actor_type")?.Value;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public bool IsPlatformAdmin => false;
    public IReadOnlyList<string> PermissionKeys => [];
    public IReadOnlyList<Guid> RoleIds => [];
    public IReadOnlyList<string> RoleNames => [];
    public IReadOnlyList<Guid> OrgUnitIds => [];
    public IReadOnlyList<Guid> PositionIds => [];
    public Guid? LegalEntityId => null;
    public string? Country => null;
    public IReadOnlyList<Guid> ManagerChain => [];
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// The REAL wire for MOD-0280-FU01: routing, JWT authentication, tenant resolution from the token, the real
/// <see cref="HasPermissionAttribute"/>, the real MediatR pipeline (validation + audit behaviours), the real
/// <see cref="GlobalExceptionHandler"/>, the module's OWN registration (<see cref="TimeEntryModuleRegistration"/>),
/// the real MOD-0023 handlers and the real Mongo repositories. Only the working calendar, display names, the audit sink
/// and the clock are test doubles.
/// </summary>
public sealed class TimeEntryHost : IDisposable
{
    private const string Issuer = "diten-auth-time-entry";
    private const string Audience = "diten-platform-time-entry";
    private const string Secret = "MOD-0280-FU01 T1a signing key, test only, 0123456789abcdef0123456789";

    private readonly TestServer _server;

    /// <param name="configure">T2a — runs after the module's own registration, so a test can wrap a port (e.g. count
    /// the task-port reads) without touching production wiring.</param>
    public TimeEntryHost(IPlatformDbContext db, Action<IServiceCollection>? configure = null)
    {
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
                services.AddAuthorization();
                services.AddHttpContextAccessor();
                services.AddProblemDetails();
                services.AddExceptionHandler<GlobalExceptionHandler>();

                services.AddScoped<ITenantContext, TenantContext>();
                services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                services.AddScoped<ICorrelationContext>(_ =>
                {
                    var correlation = new CorrelationContext();
                    correlation.SetCorrelationId(Guid.NewGuid().ToString());
                    return correlation;
                });
                services.AddScoped<IActorPermissionContext>(sp =>
                    new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));
                services.AddScoped<ITenantAuthorizationContext>(sp =>
                    new HttpPrincipal(sp.GetRequiredService<IHttpContextAccessor>()));

                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IAuditService>(Audit);
                services.AddSingleton<AuditBehaviorOptions>();
                services.AddSingleton<IUserDisplayNameResolver>(Names);
                services.AddSingleton<IWorkingCalendarProvider>(Calendar);
                services.AddSingleton(db);

                // ── The REAL stores, in the request's tenant ────────────────────────────────────────────────────
                services.AddScoped<ITimesheetWeekRepository, TimesheetWeekRepository>();
                services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
                services.AddScoped<IWorkCategoryRepository, WorkCategoryRepository>();
                services.AddScoped<ITimeEntrySettingsRepository, TimeEntrySettingsRepository>();
                services.AddScoped<ILegalEntityTimeSettingRepository, LegalEntityTimeSettingRepository>();
                services.AddScoped<ITaskTimeTotalRepository, TaskTimeTotalRepository>();
                services.AddScoped<ITimerSegmentRepository, TimerSegmentRepository>();
                services.AddScoped<ITimeSuggestionRepository, TimeSuggestionRepository>();
                services.AddScoped<IMeetingRepository, MeetingRepository>();
                services.AddScoped<IMeetingAttendeeRepository, MeetingAttendeeRepository>();
                services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
                services.AddScoped<IWorkflowTemplateVersionRepository, WorkflowTemplateVersionRepository>();
                services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
                services.AddScoped<IApprovalTaskRepository, ApprovalTaskRepository>();
                services.AddScoped<IRuntimeAssignmentSnapshotRepository, RuntimeAssignmentSnapshotRepository>();
                services.AddScoped<IWorkflowTransitionLogRepository, WorkflowTransitionLogRepository>();
                services.AddScoped<ISlaEscalationRuleRepository, SlaEscalationRuleRepository>();
                services.AddScoped<ITaskTransitionRepository, TaskTransitionRepository>();
                services.AddScoped<ITaskItemRepository, TaskItemRepository>();
                services.AddScoped<IPositionRepository, PositionRepository>();
                services.AddScoped<IPositionAssignmentRepository, PositionAssignmentRepository>();
                services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
                services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();
                services.AddScoped<IConsumedEventRepository, ConsumedEventRepository>();
                services.AddScoped<ConsumedEventStore>();

                // ── The REAL seams the module reads ─────────────────────────────────────────────────────────────
                services.AddScoped<ITaskSeatDirectory, TaskSeatDirectory>();
                services.AddScoped<IWorkingHoursProvider, WorkingHoursProvider>();

                // ── MOD-0024's REAL read-access rule (F5): data legs, team and scope legs are the production classes;
                //    only the data-scope source and the notification service (pool holders) are doubles ──────────
                services.AddScoped<ITaskWatcherRepository, TaskWatcherRepository>();
                services.AddSingleton<IDataScopeResolver>(new Tasks.FakeDataScopeResolver());
                services.AddSingleton<ITaskNotificationService>(new Tasks.FakeTaskNotificationService());
                services.AddScoped<ITaskAssignmentScopeResolver, TaskAssignmentScopeResolver>();
                services.AddScoped<ITaskTeamResolver, TaskTeamResolver>();
                services.AddScoped<ITaskReadAccessPolicy, TaskReadAccessPolicy>();

                // ── T1b: MOD-0024's REAL transition path (TasksController → TransitionTaskItemHandler → the repository
                //    choke point → the observer), so "start the task" is measured on the wire, not simulated ─────────
                services.AddScoped<ITaskLifecycleService, TaskLifecycleService>();
                services.AddScoped<IChecklistRunRepository, ChecklistRunRepository>();
                services.AddScoped<ITaskChecklistService, TaskChecklistService>();
                services.AddScoped<IWorkflowTransitionGate, WorkflowTransitionGate>();
                services.AddScoped<ITaskDependencyRepository, TaskDependencyRepository>();
                services.AddScoped<ITaskTypeRepository, TaskTypeRepository>();
                services.AddScoped<ITaskFieldDefinitionRepository, TaskFieldDefinitionRepository>();
                services.AddScoped<ITaskRecordSourceRegistry>(_ => new TaskRecordSourceRegistry([]));
                services.AddScoped<ITaskFieldDefinitionService, TaskFieldDefinitionService>();
                services.AddScoped<ITaskAttachmentRepository, TaskAttachmentRepository>();
                // The task detail read (GET /api/v1/tasks/{id}) — one of the D7 read sites.
                services.AddOptions<TaskApprovalOptions>();
                services.AddScoped<ITaskApprovalService, TaskApprovalService>();

                // ── The two crash seams, registered before the module so its no-op defaults do not apply ────────────
                services.AddSingleton<ITimesheetSubmissionProbe>(Probes);
                services.AddSingleton<ITimesheetFinalizationProbe>(Probes);

                // ── T1b: the morning notification is counted, not sent; the time-tracking switch is the test's ────────
                services.AddSingleton<ITimerAutoCloseNotifier>(Notifier);
                services.AddSingleton(TimeTracking);

                // ── The module's own registration, the one production calls ─────────────────────────────────────
                services.AddTimeEntryModule();
                configure?.Invoke(services);

                var application = typeof(TimeEntryPermissions).Assembly;
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(application);
                    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                    cfg.AddOpenBehavior(typeof(AuditBehavior<,>));
                });
                services.AddValidatorsFromAssembly(application);
                services.AddControllers().AddApplicationPart(typeof(TimeEntryController).Assembly);
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

    public TestClock Clock { get; } = new();
    public CapturingAuditService Audit { get; } = new();
    public TestDisplayNames Names { get; } = new();
    public StubWorkingCalendar Calendar { get; } = new();
    public TestProbes Probes { get; } = new();
    public CountingAutoCloseNotifier Notifier { get; } = new();

    /// <summary>The §5.1 item 3 switch as production registers it (off); a test turns it on to measure T2's data path.</summary>
    public TaskTimeTrackingOptions TimeTracking { get; } = new() { DeclareTimeTracking = false };

    public IServiceProvider Services => _server.Services;

    public string Token(Guid user, Guid tenant, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.ToString()),
            new(JwtRegisteredClaimNames.Email, $"{user:N}@tenant.example"),
            new("tenant_id", tenant.ToString()),
            new("actor_type", "tenant_user")
        };
        claims.AddRange(permissions.Select(key => new Claim("permission", key)));

        // The token's lifetime is the machine's clock (JWT validation reads it), not the module's test clock.
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Task<ApiResult> GetAsync(string path, string bearer) => SendAsync(HttpMethod.Get, path, bearer, null);

    public Task<ApiResult> PostAsync(string path, string bearer, object? body = null) => SendAsync(HttpMethod.Post, path, bearer, body ?? new { });

    public Task<ApiResult> PutAsync(string path, string bearer, object body) => SendAsync(HttpMethod.Put, path, bearer, body);

    public Task<ApiResult> DeleteAsync(string path, string bearer) => SendAsync(HttpMethod.Delete, path, bearer, null);

    private async Task<ApiResult> SendAsync(HttpMethod method, string path, string bearer, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var response = await _server.CreateClient().SendAsync(request);
        return new ApiResult(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public void Dispose() => _server.Dispose();
}

/// <summary>A response as the tests read it: the status, the body, and the two envelope fields they assert on.</summary>
public sealed record ApiResult(HttpStatusCode Status, string Body)
{
    public string? ReasonCode
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Body))
            {
                return null;
            }

            using var document = JsonDocument.Parse(Body);
            return document.RootElement.TryGetProperty("reason_code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
    }

    public JsonElement Data
    {
        get
        {
            using var document = JsonDocument.Parse(Body);
            return document.RootElement.GetProperty("data").Clone();
        }
    }

    public override string ToString() => $"{(int)Status} {Body}";
}
