using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S4, K9 — real routing, real <c>[HasPermission]</c> enforcement, mirroring <see cref="MeetingHttpTests"/>'
/// own harness. Every bridge endpoint stacks a Meetings permission with an ORDINARY Tasks permission (AND
/// semantics — <c>HasPermissionAttribute</c>'s own doc comment): this suite proves the Meetings half of that
/// stack, alone, grants nothing over <c>TaskItem</c> — the whole reason the stack exists (pack §14).
/// </summary>
public sealed class MeetingTaskBridgeHttpTests
{
    private static readonly Guid Actor = TaskTestData.Me;

    [Fact]
    public async Task CreateTaskFromMeeting_needs_BOTH_permissions_meetings_update_alone_grants_nothing()
    {
        using var host = new Host();
        var typeId = await host.SeedTypeAsync();
        var meetingId = await host.SeedMeetingAsync(typeId);
        var body = new { title = "Bir görev", idempotencyKey = "k" };

        var meetingsOnly = await host.PostAsync(
            $"/api/v1/meetings/{meetingId}/tasks", body, MeetingPermissions.Update);
        Assert.Equal(HttpStatusCode.Forbidden, meetingsOnly.StatusCode);

        var tasksOnly = await host.PostAsync(
            $"/api/v1/meetings/{meetingId}/tasks", body, TaskPermissions.Create);
        Assert.Equal(HttpStatusCode.Forbidden, tasksOnly.StatusCode);

        var both = await host.PostAsync(
            $"/api/v1/meetings/{meetingId}/tasks", body, MeetingPermissions.Update, TaskPermissions.Create);
        Assert.NotEqual(HttpStatusCode.Forbidden, both.StatusCode);
        Assert.Equal(HttpStatusCode.Created, both.StatusCode);
    }

    [Fact]
    public async Task LinkExistingTask_needs_BOTH_permissions_meetings_update_alone_grants_nothing()
    {
        using var host = new Host();
        var typeId = await host.SeedTypeAsync();
        var meetingId = await host.SeedMeetingAsync(typeId);
        var taskId = host.SeedTask();

        var meetingsOnly = await host.PostAsync(
            $"/api/v1/meetings/{meetingId}/tasks/{taskId}/link", null, MeetingPermissions.Update);
        Assert.Equal(HttpStatusCode.Forbidden, meetingsOnly.StatusCode);

        var tasksOnly = await host.PostAsync(
            $"/api/v1/meetings/{meetingId}/tasks/{taskId}/link", null, TaskPermissions.Read);
        Assert.Equal(HttpStatusCode.Forbidden, tasksOnly.StatusCode);

        var both = await host.PostAsync(
            $"/api/v1/meetings/{meetingId}/tasks/{taskId}/link", null, MeetingPermissions.Update, TaskPermissions.Read);
        Assert.NotEqual(HttpStatusCode.Forbidden, both.StatusCode);
        Assert.Equal(HttpStatusCode.OK, both.StatusCode);
    }

    [Fact]
    public async Task ScheduleReviewMeetingForTask_needs_BOTH_permissions_meetings_create_alone_grants_nothing()
    {
        using var host = new Host();
        var typeId = await host.SeedTypeAsync();
        var taskId = host.SeedTask();
        var body = new
        {
            meetingTypeId = typeId, startAt = DateTimeOffset.UtcNow, endAt = DateTimeOffset.UtcNow.AddHours(1),
            title = (string?)null, idempotencyKey = "k"
        };

        var meetingsOnly = await host.PostAsync(
            $"/api/v1/meetings/tasks/{taskId}/schedule-review-meeting", body, MeetingPermissions.Create);
        Assert.Equal(HttpStatusCode.Forbidden, meetingsOnly.StatusCode);

        var tasksOnly = await host.PostAsync(
            $"/api/v1/meetings/tasks/{taskId}/schedule-review-meeting", body, TaskPermissions.Read);
        Assert.Equal(HttpStatusCode.Forbidden, tasksOnly.StatusCode);

        var both = await host.PostAsync(
            $"/api/v1/meetings/tasks/{taskId}/schedule-review-meeting", body, MeetingPermissions.Create, TaskPermissions.Read);
        Assert.NotEqual(HttpStatusCode.Forbidden, both.StatusCode);
        Assert.Equal(HttpStatusCode.Created, both.StatusCode);
    }

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;
        private readonly FakeMeetingRepository _meetings = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeMeetingTypeRepository _types = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeAgendaItemRepository _agenda = new() { Tenant = TaskTestData.Tenant };
        private FakeTaskItemRepository _tasks = new();
        private readonly FakeRecordLinkRepository _links = new();

        public Host()
        {
            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = HeaderAuthentication.SchemeName;
                            options.DefaultChallengeScheme = HeaderAuthentication.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>(HeaderAuthentication.SchemeName, _ => { });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr");
                        return correlation;
                    });
                    services.AddScoped<IMediator>(_ => new RoutingMediator(this));
                    services.AddControllers().AddApplicationPart(typeof(MeetingsController).Assembly);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public async Task<Guid> SeedTypeAsync()
        {
            var type = new MeetingType { TenantId = TaskTestData.Tenant, Name = "MGMT-REVIEW " + Guid.NewGuid() };
            _types.Seed(type);
            return await Task.FromResult(type.Id);
        }

        public async Task<Guid> SeedMeetingAsync(Guid typeId)
        {
            var meeting = new Meeting
            {
                TenantId = TaskTestData.Tenant, Title = "T", MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow.AddDays(1), EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                OrganizerUserId = Actor, IdempotencyKey = Guid.NewGuid().ToString()
            };
            _meetings.Seed(meeting);
            return await Task.FromResult(meeting.Id);
        }

        public Guid SeedTask()
        {
            var task = new TaskItem
            {
                TenantId = TaskTestData.Tenant, Title = "Bir görev",
                AssignmentTarget = TaskAssignmentTarget.Person, AssigneeUserId = Actor, CreatedByUserId = Actor,
                OrganizationUnitId = Guid.NewGuid(), Lifecycle = TaskLifecycle.Open
            };
            // FakeTaskItemRepository only takes its seed at construction, so the field is reassigned rather
            // than mutated — RoutingMediator reads it through `host`, never captures the old instance.
            _tasks = new FakeTaskItemRepository(task);
            return task.Id;
        }

        public async Task<HttpResponseMessage> PostAsync(string path, object? body, params string[] grantedPermissions)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add(
                HeaderAuthentication.PermissionsHeader,
                grantedPermissions.Length == 0 ? "platform.meetings.__none__" : string.Join(' ', grantedPermissions));

            var request = new HttpRequestMessage(HttpMethod.Post, path);
            if (body is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            else
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            return await client.SendAsync(request);
        }

        public void Dispose() => _server.Dispose();

        private sealed class RoutingMediator(Host host) : IMediator
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            {
                object result = request switch
                {
                    CreateTaskFromMeetingCommand cmd => new CreateTaskFromMeetingHandler(
                        host._meetings, host._types, host._agenda,
                        new FakeMeetingMinutesVersionRepository { Tenant = TaskTestData.Tenant },
                        new RecordLinkService(host._links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(Actor)),
                        new MeetingIdempotencyKeyResolver(), new FakeCurrentUserContext(Actor), this).Handle(cmd, ct),
                    LinkExistingTaskCommand cmd => new LinkExistingTaskHandler(
                        host._meetings, host._agenda, host._tasks,
                        new RecordLinkService(host._links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(Actor)))
                        .Handle(cmd, ct),
                    ScheduleReviewMeetingForTaskCommand cmd => new ScheduleReviewMeetingForTaskHandler(
                        host._tasks,
                        new RecordLinkService(host._links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(Actor)),
                        new MeetingIdempotencyKeyResolver(), new FakeCurrentUserContext(Actor), this).Handle(cmd, ct),
                    // The two bridge handlers above delegate to MOD-0024/MOD-0357's OWN ordinary create commands
                    // (K2 — no second create path). Canned here, the same isolation the handler-level
                    // MeetingTaskBridgeCommandHandlerTests already takes: this suite's OWN concern is the HTTP
                    // permission gate, never MOD-0024's create-validation, which has its own suite.
                    CreateTaskItemCommand cmd => Task.FromResult(
                        Response<Guid>.Success(Guid.NewGuid(), 201, cmd.CorrelationId)),
                    CreateMeetingCommand cmd => Task.FromResult(Response<MeetingDto>.Success(
                        new MeetingDto(
                            Guid.NewGuid(), cmd.Request.Title, cmd.Request.MeetingTypeId, "Tür",
                            cmd.Request.StartAt, cmd.Request.EndAt, cmd.Request.Location, Actor,
                            cmd.Request.Description, cmd.Request.FollowUpOfMeetingId, MeetingLifecycle.Scheduled,
                            null, 1, [], []),
                        201, cmd.CorrelationId)),
                    _ => throw new NotSupportedException($"RoutingMediator does not support {request.GetType().Name}.")
                };
                return (Task<TResponse>)result;
            }

            public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

            public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
                => throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
                => throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();

            public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

            public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
                where TNotification : INotification => throw new NotSupportedException();
        }
    }

    private sealed class HeaderAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(PermissionsHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("actor_type", "tenant_user"),
                new("tenant_id", TaskTestData.Tenant.ToString()),
                new("sub", Actor.ToString())
            };
            foreach (var key in Request.Headers[PermissionsHeader].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim("permission", key));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
