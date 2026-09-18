using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Providers;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// DCP-004 "Decision amendment 2026-09-15" (BL-410 + BL-417) — THE WORK CENTER FOR EVERY TENANT USER, ON THE WIRE.
///
/// <para><b>What changed.</b> <c>GET work-items/mine</c> and <c>team-availability</c> need no key; the single read
/// needs <c>platform.tasks.read</c> but no longer <c>inbox.view</c>; a dispatcher that names no key is refused; the
/// task read rule admits a manager to what their Ekibim list shows.</para>
///
/// <para><b>What is real.</b> A SIGNED token → JWT bearer validation with the Platform's settings
/// (<c>MapInboundClaims = false</c>) → the production <c>UseTenantResolution()</c> (BL-413's actor check) →
/// <c>[HasPermission]</c> → the real <see cref="WorkItemsController"/> → the real handlers
/// (<see cref="GetMyWorkItemsHandler"/>, <see cref="GetMyTeamAvailabilityHandler"/>,
/// <see cref="GetTaskWorkItemByIdHandler"/>) → the real <see cref="TaskWorkItemProvider"/>, the real
/// <see cref="TaskReadAccessPolicy"/>, the real <see cref="TaskTeamResolver"/> over the real
/// <see cref="TaskAssignmentScopeResolver"/> over the real <see cref="OrgDataScopeResolver"/>, the real
/// <see cref="CurrentUserContext"/> reading <c>sub</c> off the validated token, and the three real dispatchers. Only
/// the stores (in-memory org chart and tasks) and the command bus behind the dispatchers are doubles.</para>
///
/// <para><b>The org world.</b> Boss manages Report (same legal entity) and ForeignReport (another legal entity, a unit
/// Boss has no grant on). Loner holds a position with nobody under it. Stranger is in the other legal entity and
/// reports to nobody Boss manages.</para>
/// </summary>
public sealed class WorkItemsForEveryTenantUserHttpTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    private static readonly Guid HomeLegalEntity = Guid.Parse("41000000-0000-4000-8000-00000000000a");
    private static readonly Guid ForeignLegalEntity = Guid.Parse("41000000-0000-4000-8000-00000000000b");
    private static readonly Guid HomeUnit = Guid.Parse("41000000-0000-4000-8000-0000000000a1");
    private static readonly Guid ForeignUnit = Guid.Parse("41000000-0000-4000-8000-0000000000b1");

    private static readonly Guid BossPosition = Guid.Parse("41000000-0000-4000-8000-000000000b05");
    private static readonly Guid ReportPosition = Guid.Parse("41000000-0000-4000-8000-000000000a11");
    private static readonly Guid ForeignReportPosition = Guid.Parse("41000000-0000-4000-8000-000000000f11");
    private static readonly Guid StrangerPosition = Guid.Parse("41000000-0000-4000-8000-000000000057");
    private static readonly Guid LonerPosition = Guid.Parse("41000000-0000-4000-8000-000000000101");

    private static readonly Guid Boss = Guid.Parse("41000000-0000-4000-8000-00000000b055");
    private static readonly Guid Report = Guid.Parse("41000000-0000-4000-8000-00000000c0de");
    private static readonly Guid ForeignReport = Guid.Parse("41000000-0000-4000-8000-00000000f0de");
    private static readonly Guid Stranger = Guid.Parse("41000000-0000-4000-8000-00000000dead");
    private static readonly Guid Loner = Guid.Parse("41000000-0000-4000-8000-000000001011");

    // ── GET mine — every signed-in tenant user, no key ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_tenant_user_with_zero_permission_claims_reads_only_their_own_inbox()
    {
        using var host = new Host();

        var (status, body) = await host.GetAsync("/api/v1/work-items/mine", host.Token(Loner));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([host.LonerTask.Id], ItemIds(body));
    }

    [Fact]
    public async Task Team_scope_for_a_user_with_no_subordinates_is_200_and_empty()
    {
        using var host = new Host();

        var (status, body) = await host.GetAsync("/api/v1/work-items/mine?scope=team", host.Token(Loner));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(ItemIds(body));
    }

    [Fact]
    public async Task Team_scope_for_a_manager_with_zero_permission_claims_lists_exactly_the_subordinates_rows()
    {
        using var host = new Host();

        var (status, body) = await host.GetAsync("/api/v1/work-items/mine?scope=team", host.Token(Boss));

        Assert.Equal(HttpStatusCode.OK, status);
        // Both legal entities; not the manager's own task, not the stranger's, not the loner's.
        Assert.Equal(
            new[] { host.ReportTask.Id, host.ForeignReportTask.Id }.OrderBy(id => id),
            ItemIds(body).OrderBy(id => id));
    }

    [Fact]
    public async Task Team_availability_follows_the_org_chart_with_no_key()
    {
        using var host = new Host();

        var (lonerStatus, lonerBody) = await host.GetAsync("/api/v1/work-items/team-availability", host.Token(Loner));
        var (bossStatus, bossBody) = await host.GetAsync("/api/v1/work-items/team-availability", host.Token(Boss));

        Assert.Equal(HttpStatusCode.OK, lonerStatus);
        Assert.Equal((false, 0), TeamAvailability(lonerBody));
        Assert.Equal(HttpStatusCode.OK, bossStatus);
        Assert.Equal((true, 2), TeamAvailability(bossBody));
    }

    // ── POST actions — opening the inbox authorizes nothing ──────────────────────────────────────────────────

    public static TheoryData<string, string> EveryDispatchableAction()
    {
        var data = new TheoryData<string, string>();
        foreach (var dispatcher in RealDispatchers(mediator: null!))
        {
            foreach (var code in dispatcher.SupportedActionCodes)
            {
                data.Add(dispatcher.ProviderCode, code);
            }
        }

        return data;
    }

    [Fact]
    public void The_action_theory_covers_all_three_sources()
    {
        // Non-vacuity for the theory below: tasks, workflow approval and meeting invitations all contribute rows.
        var providers = EveryDispatchableAction().Select(row => (string)row[0]).ToHashSet();
        Assert.Equal(
            new[] { WorkItemContract.ProviderCodeMeetings, WorkItemContract.ProviderCodeTasks, WorkItemContract.ProviderCodeWorkflow }
                .OrderBy(p => p),
            providers.OrderBy(p => p));
    }

    [Theory]
    [MemberData(nameof(EveryDispatchableAction))]
    public async Task Every_dispatchable_action_is_refused_without_its_source_key_and_sends_no_command(
        string providerCode, string actionCode)
    {
        using var host = new Host();

        var (status, body) = await host.PostActionAsync(host.LonerTask.Id, actionCode, providerCode, host.Token(Loner));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(WorkItemActionReasonCodes.ActionForbidden, ReasonCode(body));
        Assert.Empty(host.Commands.Sent);
    }

    [Theory]
    [InlineData(WorkItemContract.ProviderCodeTasks, "accept")]
    [InlineData(WorkItemContract.ProviderCodeWorkflow, "approve")]
    [InlineData(WorkItemContract.ProviderCodeMeetings, "acceptInvite")]
    public async Task The_same_action_passes_the_gate_when_the_caller_holds_its_source_key(
        string providerCode, string actionCode)
    {
        // The gate above is a gate, not a wall: the source module's OWN key opens it.
        using var host = new Host();
        var key = RealDispatchers(mediator: null!).Single(d => d.ProviderCode == providerCode).RequiredPermission(actionCode)!;

        var (status, body) = await host.PostActionAsync(host.LonerTask.Id, actionCode, providerCode, host.Token(Loner, key));

        Assert.NotEqual(HttpStatusCode.Forbidden, status);
        Assert.NotEqual(WorkItemActionReasonCodes.ActionForbidden, ReasonCode(body));
    }

    [Fact]
    public async Task A_dispatcher_that_names_no_key_is_refused_even_for_a_caller_holding_every_key()
    {
        using var host = new Host();
        var everyKey = RealDispatchers(mediator: null!)
            .SelectMany(d => d.SupportedActionCodes.Select(d.RequiredPermission))
            .OfType<string>()
            .Distinct()
            .ToArray();

        var (status, body) = await host.PostActionAsync(
            host.LonerTask.Id, BlankKeyDispatcher.Action, BlankKeyDispatcher.Code, host.Token(Loner, everyKey));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(WorkItemActionReasonCodes.ActionForbidden, ReasonCode(body));
        Assert.Equal(0, host.BlankKey.DispatchCount);
    }

    // ── GET {id} — tasks.read + the read rule, no inbox key ──────────────────────────────────────────────────

    [Fact]
    public async Task The_single_read_is_403_without_tasks_read_even_for_the_assignee()
    {
        using var host = new Host();

        var (status, body) = await host.GetAsync($"/api/v1/work-items/{host.LonerTask.Id}", host.Token(Loner));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.DoesNotContain(host.LonerTask.Title, body);
    }

    [Fact]
    public async Task The_single_read_is_200_with_tasks_read_alone_for_a_readable_task()
    {
        using var host = new Host();

        var (status, body) = await host.GetAsync(
            $"/api/v1/work-items/{host.LonerTask.Id}", host.Token(Loner, TaskPermissions.Read));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{host.LonerTask.Id}\"", body);
    }

    [Fact]
    public async Task Unreadable_missing_and_other_tenant_tasks_answer_one_byte_identical_404()
    {
        using var host = new Host();
        var token = host.Token(Loner, TaskPermissions.Read);

        var (unreadableStatus, unreadable) = await host.GetAsync($"/api/v1/work-items/{host.StrangerTask.Id}", token);
        var (missingStatus, missing) = await host.GetAsync($"/api/v1/work-items/{Guid.NewGuid()}", token);
        var (foreignStatus, foreign) = await host.GetAsync($"/api/v1/work-items/{host.OtherTenantTask.Id}", token);

        Assert.Equal(HttpStatusCode.NotFound, unreadableStatus);
        Assert.Equal(HttpStatusCode.NotFound, missingStatus);
        Assert.Equal(HttpStatusCode.NotFound, foreignStatus);
        Assert.Equal(missing, unreadable);
        Assert.Equal(missing, foreign);
        Assert.DoesNotContain(host.StrangerTask.Title, unreadable);
    }

    [Fact]
    public async Task Every_task_on_the_managers_team_list_opens_by_id_and_a_strangers_task_does_not()
    {
        // BL-417 (a) parity on the wire: the list and the detail page give one answer, across two legal entities.
        using var host = new Host();
        var token = host.Token(Boss, TaskPermissions.Read);

        var (_, teamBody) = await host.GetAsync("/api/v1/work-items/mine?scope=team", token);
        var teamIds = ItemIds(teamBody);
        Assert.Contains(host.ForeignReportTask.Id, teamIds);

        foreach (var id in teamIds)
        {
            var (status, _) = await host.GetAsync($"/api/v1/work-items/{id}", token);
            Assert.True(status == HttpStatusCode.OK, $"task {id} is on the team list but the single read answered {(int)status}");
        }

        var (strangerStatus, _) = await host.GetAsync($"/api/v1/work-items/{host.StrangerTask.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, strangerStatus);
    }

    // ── BL-413 unchanged: only a tenant_user reaches any of this ─────────────────────────────────────────────

    public static TheoryData<string> RefusedActorTypes() => new() { "platform_admin", "partner_admin", "" };

    [Theory]
    [MemberData(nameof(RefusedActorTypes))]
    public async Task A_non_tenant_or_missing_actor_type_is_refused_on_every_work_item_route(string actorType)
    {
        using var host = new Host();
        // A token that NAMES the tenant, so the refusal is the actor check and not a missing-tenant 400.
        var token = host.TokenWithActor(Loner, actorType.Length == 0 ? null : actorType, TaskPermissions.Read);

        foreach (var path in new[]
                 {
                     "/api/v1/work-items/mine", "/api/v1/work-items/mine?scope=team",
                     "/api/v1/work-items/team-availability", $"/api/v1/work-items/{host.LonerTask.Id}"
                 })
        {
            var (status, body) = await host.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.Forbidden, status);
            Assert.Contains("Forbidden Actor", body);
        }

        var (postStatus, postBody) = await host.PostActionAsync(
            host.LonerTask.Id, "accept", WorkItemContract.ProviderCodeTasks, token);
        Assert.Equal(HttpStatusCode.Forbidden, postStatus);
        Assert.Contains("Forbidden Actor", postBody);
        Assert.Empty(host.Commands.Sent);
    }

    [Fact]
    public async Task Without_a_token_the_inbox_is_401()
    {
        using var host = new Host();

        // The gateway and the Web proxy always name the tenant. Without it the middleware answers 400 Missing Tenant
        // before [Authorize] is reached (TenantResolutionMiddleware, BL-413 note), which is not the question here.
        var (status, _) = await host.GetAsync("/api/v1/work-items/mine", bearer: null, tenantHeader: Tenant);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private static IWorkItemActionDispatcher[] RealDispatchers(IMediator mediator) =>
    [
        new TaskWorkItemActionDispatcher(mediator),
        new WorkflowApprovalWorkItemActionDispatcher(mediator),
        new MeetingWorkItemActionDispatcher(mediator)
    ];

    private static List<Guid> ItemIds(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(item => Guid.Parse(item.GetProperty("id").GetString()!))
            .ToList();
    }

    private static (bool HasTeam, int MemberCount) TeamAvailability(string body)
    {
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        return (data.GetProperty("hasTeam").GetBoolean(), data.GetProperty("memberCount").GetInt32());
    }

    private static string? ReasonCode(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
    }

    private sealed class Host : IDisposable
    {
        private const string Issuer = "diten-auth-wcn-every-user-test";
        private const string Audience = "diten-platform-wcn-every-user-test";
        private const string Secret = "WP-WCN-INBOX-FOR-EVERY-TENANT-USER-01 signing key, test only, 0123456789abcdef";

        private readonly TestServer _server;

        public TaskItem LonerTask { get; } = NewTask(Loner, HomeUnit, "loner's own task");
        public TaskItem BossTask { get; } = NewTask(Boss, HomeUnit, "the manager's own task");
        public TaskItem ReportTask { get; } = NewTask(Report, HomeUnit, "home subordinate's own task");
        public TaskItem ForeignReportTask { get; } = NewTask(ForeignReport, ForeignUnit, "foreign subordinate's own task");
        public TaskItem StrangerTask { get; } = NewTask(Stranger, ForeignUnit, "a stranger's task nobody else may read");
        public TaskItem OtherTenantTask { get; } = NewTask(Loner, HomeUnit, "another tenant's task", TaskTestData.OtherTenant);

        public RecordingCommandBus Commands { get; } = new();

        public BlankKeyDispatcher BlankKey { get; } = new();

        public Host()
        {
            var units = new FakeOrganizationUnitRepository(
                Unit(HomeUnit, "FAC-A", HomeLegalEntity),
                Unit(ForeignUnit, "FAC-B", ForeignLegalEntity));
            var positions = new FakePositionRepository(
                Position(BossPosition, HomeUnit, "Boss"),
                Position(ReportPosition, HomeUnit, "Analyst", reportsTo: BossPosition),
                Position(ForeignReportPosition, ForeignUnit, "Plant Manager", reportsTo: BossPosition),
                Position(StrangerPosition, ForeignUnit, "Stranger"),
                Position(LonerPosition, HomeUnit, "Loner"));
            var seats = new FakePositionAssignmentRepository(
                Seat(Boss, BossPosition),
                Seat(Report, ReportPosition),
                Seat(ForeignReport, ForeignReportPosition),
                Seat(Stranger, StrangerPosition),
                Seat(Loner, LonerPosition));
            var tasks = new FakeTaskItemRepository(LonerTask, BossTask, ReportTask, ForeignReportTask, StrangerTask, OtherTenantTask);
            var watchers = new FakeTaskWatcherRepository();
            var dispatchers = RealDispatchers(Commands).Append(BlankKey).ToArray();

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    // The Platform's bearer settings (Infrastructure DependencyInjection.AddInfrastructure).
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

                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr");
                        return correlation;
                    });
                    services.AddScoped<IActorPermissionContext>(sp =>
                        new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));

                    // The org chart, resolved per request for whoever the token says the caller is.
                    services.AddScoped<IDataScopeResolver>(_ =>
                        new OrgDataScopeResolver(units, positions, seats, new ReferenceableLegalEntities()));
                    services.AddScoped<ITaskAssignmentScopeResolver>(sp => new TaskAssignmentScopeResolver(
                        sp.GetRequiredService<IDataScopeResolver>(),
                        positions,
                        units,
                        sp.GetRequiredService<ITenantContext>(),
                        sp.GetRequiredService<ICurrentUserContext>()));
                    services.AddScoped<ITaskTeamResolver>(sp =>
                        new TaskTeamResolver(sp.GetRequiredService<ITaskAssignmentScopeResolver>(), seats));
                    services.AddScoped<ITaskReadAccessPolicy>(sp => new TaskReadAccessPolicy(
                        tasks,
                        watchers,
                        new TaskNotificationService(
                            new RecordingNotificationDispatchAdapter(),
                            new FakeNotificationLocaleResolver(),
                            new FakeTaskNotificationRecipientResolver(),
                            seats,
                            new FakeUserNotificationRepository(),
                            new FakeTenantContext(Tenant),
                            NullLogger<TaskNotificationService>.Instance),
                        units,
                        sp.GetRequiredService<ITaskAssignmentScopeResolver>(),
                        sp.GetRequiredService<ITaskTeamResolver>(),
                        sp.GetRequiredService<IActorPermissionContext>(),
                        sp.GetRequiredService<ICurrentUserContext>()));
                    services.AddScoped<IEnumerable<IWorkItemProvider>>(sp => new IWorkItemProvider[]
                    {
                        new TaskWorkItemProvider(
                            tasks,
                            seats,
                            new TaskLifecycleService(),
                            new TaskAssignmentResolver(),
                            new FakeUserDisplayNameResolver(),
                            new FakeChecklistRunRepository(),
                            new FakeTaskApprovalService(),
                            new FakeTaskDependencyRepository(),
                            new FakeTaskCommentRepository(),
                            new FakeTaskTransitionRepository(),
                            new FakeTaskPersonalOverlayRepository(),
                            watchers,
                            sp.GetRequiredService<IActorPermissionContext>(),
                            positions,
                            units,
                            SlaForTests.Real(),
                            new FakeTaskFieldDefinitionRepository(),
                            new FakeTaskTypeRepository(),
                            teamResolver: sp.GetRequiredService<ITaskTeamResolver>())
                    });
                    services.AddScoped<IEnumerable<IWorkItemActionDispatcher>>(_ => dispatchers);
                    services.AddScoped<IMediator>(sp => new QueryRouter(sp, tasks));
                    services.AddControllers().AddApplicationPart(typeof(WorkItemsController).Assembly);
                })
                .Configure(app =>
                {
                    // Same order as Diten.Platform.API Program.cs: authentication, tenant resolution, authorization.
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        /// <summary>A tenant token as AuthService's TokenService writes one: sub, tenant_id, actor_type, permission claims.</summary>
        public string Token(Guid user, params string[] keys) => TokenWithActor(user, "tenant_user", keys);

        /// <summary>
        /// Named apart from <c>Token</c> on purpose: as an overload taking <c>(Guid, string?, params string[])</c>,
        /// <c>Token(user, "platform.tasks.read")</c> bound the KEY to the actor type and every keyed case read 403.
        /// </summary>
        public string TokenWithActor(Guid user, string? actorType, params string[] keys)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, "wcn.every.user@tenant.example"),
                new("tenant_id", Tenant.ToString())
            };
            if (actorType is not null)
            {
                claims.Add(new Claim("actor_type", actorType));
            }

            claims.AddRange(keys.Select(key => new Claim("permission", key)));

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<(HttpStatusCode Status, string Body)> GetAsync(string path, string? bearer, Guid? tenantHeader = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (bearer is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            }

            if (tenantHeader is not null)
            {
                request.Headers.Add("X-Tenant-Id", tenantHeader.Value.ToString());
            }

            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public async Task<(HttpStatusCode Status, string Body)> PostActionAsync(
            Guid itemId, string actionCode, string providerCode, string bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/work-items/{itemId}/actions/{actionCode}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { providerCode, payload = new { expectedVersion = 1 } }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public void Dispose() => _server.Dispose();

        private static TaskItem NewTask(Guid assignee, Guid unit, string title, Guid? tenant = null) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenant ?? Tenant,
            Title = title,
            Lifecycle = TaskLifecycle.Open,
            Priority = TaskPriority.Medium,
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = assignee,
            CreatedByUserId = assignee,
            OrganizationUnitId = unit,
            CreatedBy = "tester",
            Version = 1
        };

        private static PositionAssignment Seat(Guid userId, Guid positionId) => new()
        {
            TenantId = Tenant,
            PositionId = positionId,
            UserId = userId,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
        };

        private static Position Position(Guid id, Guid unitId, string name, Guid? reportsTo = null) => new()
        {
            Id = id,
            TenantId = Tenant,
            Code = name.Replace(' ', '-').ToUpperInvariant(),
            Name = name,
            OrganizationUnitId = unitId,
            ReportsToPositionId = reportsTo,
            Status = PositionStatus.Active
        };

        private static OrganizationUnit Unit(Guid id, string code, Guid legalEntityId) => new()
        {
            Id = id,
            TenantId = Tenant,
            Code = code,
            Name = code,
            LegalEntityId = legalEntityId,
            Status = OrgUnitStatus.Active
        };
    }

    /// <summary>Every legal entity in this world is referenceable, as a live Legal Entity master would answer.</summary>
    private sealed class ReferenceableLegalEntities : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
            => Task.FromResult(Response<LegalEntityReferenceDto>.Success(
                new LegalEntityReferenceDto(legalEntityId, "Legal entity", null, "Active", Referenceable: true)));
    }

    /// <summary>Routes the three read queries this controller sends to their REAL handlers; anything else is a test bug.</summary>
    private sealed class QueryRouter(IServiceProvider services, FakeTaskItemRepository tasks) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object response = request switch
            {
                GetMyWorkItemsQuery query => await new GetMyWorkItemsHandler(
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>(),
                    services.GetRequiredService<ICurrentUserContext>(),
                    Options.Create(new WorkAggregationResilienceOptions()),
                    NullLogger<GetMyWorkItemsHandler>.Instance).Handle(query, ct),
                GetMyTeamAvailabilityQuery query => await new GetMyTeamAvailabilityHandler(
                    services.GetRequiredService<ITaskTeamResolver>()).Handle(query, ct),
                GetTaskWorkItemByIdQuery query => await new GetTaskWorkItemByIdHandler(
                    tasks,
                    services.GetRequiredService<ITaskReadAccessPolicy>(),
                    services.GetRequiredService<ICurrentUserContext>(),
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>()).Handle(query, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };
            return (TResponse)response;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }

    /// <summary>
    /// The command bus behind the dispatchers. Records what a dispatcher sent and answers success, so "no command
    /// was sent" is observable; whether the module then accepts is the module's own suite.
    /// </summary>
    private sealed class RecordingCommandBus : IMediator
    {
        public List<object> Sent { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Sent.Add(request);
            var successful = typeof(TResponse)
                .GetMethod("Success", [typeof(int), typeof(string)])!
                .Invoke(null, [200, "corr"]);
            return Task.FromResult((TResponse)successful!);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    /// <summary>A dispatcher that claims an action and names no key for it.</summary>
    private sealed class BlankKeyDispatcher : IWorkItemActionDispatcher
    {
        public const string Code = "blank-key-probe";
        public const string Action = "doIt";

        public int DispatchCount { get; private set; }

        public string ProviderCode => Code;

        public IReadOnlyCollection<string> SupportedActionCodes { get; } = [Action];

        public bool CanDispatch(string actionCode) => actionCode == Action;

        public string? RequiredPermission(string actionCode) => null;

        public Task<Response<WorkItemActionResultDto>> DispatchAsync(
            WorkItemActionDispatchRequest request, CancellationToken ct = default)
        {
            DispatchCount++;
            return Task.FromResult(Response<WorkItemActionResultDto>.Success(
                new WorkItemActionResultDto(request.ItemId.ToString(), Code, request.ActionCode), 200, "corr"));
        }
    }
}
