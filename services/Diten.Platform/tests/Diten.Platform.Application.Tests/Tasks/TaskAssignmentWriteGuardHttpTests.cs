using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Tasks.Validators;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static Diten.Platform.Application.Tests.Tasks.AssignmentWorld;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-057 at the write, measured on the WIRE: <c>POST api/v1/tasks</c> through the real routing, the real
/// authentication/authorization middleware, the real <see cref="HasPermissionAttribute"/>, the real
/// <see cref="ClaimsActorPermissionContext"/> reading the request's claims, and the real validation pipeline in
/// front of the real handler.
///
/// <para>Why this level and not only the handler: a green handler test once coexisted with a route that never
/// reached it. Here the permission the guard asks for comes from the token's claims, exactly as in production —
/// a double cannot be kinder than the real seam.</para>
/// </summary>
public sealed class TaskAssignmentWriteGuardHttpTests
{
    private const string CreateOnly = TaskPermissions.Create;
    private const string CreateAndAssign = TaskPermissions.Create + " " + TaskPermissions.Assign;

    [Fact]
    public async Task A_task_for_a_person_OUTSIDE_my_scope_is_400_with_the_code_and_nothing_is_stored()
    {
        using var host = new Host();

        var response = await host.PostAsync(CreateAndAssign, TaskAssignmentTarget.Person, person: ForeignPeer);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(TaskReasonCodes.AssigneeNotAssignable, await ReasonCodeAsync(response));
        Assert.Empty(host.Tasks.Items);
    }

    [Fact]
    public async Task A_task_for_a_pool_OUTSIDE_my_scope_is_400_with_the_code_and_nothing_is_stored()
    {
        using var host = new Host();

        var response = await host.PostAsync(CreateOnly, TaskAssignmentTarget.PositionPool, pool: ForeignPeerPosition);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(TaskReasonCodes.PositionNotAssignable, await ReasonCodeAsync(response));
        Assert.Empty(host.Tasks.Items);
    }

    [Fact]
    public async Task A_task_for_somebody_else_WITHOUT_assign_is_403_and_nothing_is_stored()
    {
        // The route lets this caller in (they hold Create). The refusal is the handler's, read from the claims.
        using var host = new Host();

        var response = await host.PostAsync(CreateOnly, TaskAssignmentTarget.Person, person: Colleague);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(host.Tasks.Items);
    }

    [Fact]
    public async Task A_task_for_MYSELF_is_201_without_assign()
    {
        using var host = new Host();

        var selfAssigned = await host.PostAsync(CreateOnly, TaskAssignmentTarget.SelfAssigned);
        var namedSelf = await host.PostAsync(CreateOnly, TaskAssignmentTarget.Person, person: Me);

        Assert.Equal(HttpStatusCode.Created, selfAssigned.StatusCode);
        Assert.Equal(HttpStatusCode.Created, namedSelf.StatusCode);
        Assert.Equal(2, host.Tasks.Items.Count);
    }

    [Fact]
    public async Task A_task_for_a_person_INSIDE_my_scope_is_201()
    {
        // Non-vacuity: without it, every refusal above could be a route that refuses everything.
        using var host = new Host();

        var response = await host.PostAsync(CreateAndAssign, TaskAssignmentTarget.Person, person: ForeignReport);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ForeignReport, Assert.Single(host.Tasks.Items).AssigneeUserId);
    }

    private static async Task<string?> ReasonCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("reason_code").GetString();
    }

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;

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
                        .AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>(
                            HeaderAuthentication.SchemeName, _ => { });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr");
                        return correlation;
                    });
                    // The production seam, reading the request's own claims.
                    services.AddScoped<IActorPermissionContext, ClaimsActorPermissionContext>();
                    services.AddScoped<IMediator>(provider =>
                        new CreateThroughPipeline(Handler(provider.GetRequiredService<IActorPermissionContext>())));
                    services.AddControllers().AddApplicationPart(typeof(TasksController).Assembly);
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

        public FakeTaskItemRepository Tasks { get; } = new();

        public async Task<HttpResponseMessage> PostAsync(
            string permissions, TaskAssignmentTarget target, Guid? person = null, Guid? pool = null)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add(HeaderAuthentication.PermissionsHeader, permissions);

            return await client.PostAsJsonAsync("/api/v1/tasks", new CreateTaskItemRequest(
                Title: "Kapsam denemesi",
                Description: null,
                Priority: TaskPriority.Medium,
                AssignmentTarget: target,
                AssigneeUserId: person,
                PoolPositionId: pool,
                OrganizationUnitId: null,
                DueAt: DateTimeOffset.UtcNow.AddDays(3),
                StartAt: null,
                PlannedDate: null,
                EstimateHours: null,
                Tags: null,
                ReviewRequired: false,
                ApprovalRequired: false,
                ApprovalManagerUserId: null,
                EmailNotificationsEnabled: false,
                DelegationAllowed: true,
                FieldValues: null,
                Watchers: null));
        }

        public void Dispose() => _server.Dispose();

        private CreateTaskItemHandler Handler(IActorPermissionContext permissions)
        {
            var seats = SeatRepository();
            var positions = PositionRepository();
            var units = UnitRepository();
            var scopes = ScopeResolver(positions, units, Me);
            var me = new FakeCurrentUserContext(Me);

            return new CreateTaskItemHandler(
                Tasks,
                new FakeTaskAssignmentRepository(),
                new FakeTaskWatcherRepository(),
                positions,
                units,
                seats,
                new TaskFieldDefinitionService(
                    new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new TaskLifecycleService(),
                new FakeTaskApprovalService(),
                new FakeChecklistTemplateRepository(),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new FakeTaskNotificationService(),
                me,
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<CreateTaskItemHandler>.Instance,
                TaskDocumentFreezerDoubles.OverAnEmptyRegister(),
                new TaskAssignmentGuard(seats, positions, units, scopes, permissions, me),
                new TaskAssignmentDirection(scopes, seats, me),
                new RecordingUpwardRequests());
        }
    }

    /// <summary>
    /// The production <see cref="ValidationBehavior{TRequest,TResponse}"/> over the production validator, then the
    /// real handler — so the response shape is the one the wire actually carries.
    /// </summary>
    private sealed class CreateThroughPipeline(CreateTaskItemHandler handler) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is CreateTaskItemCommand command
                ? (Task<TResponse>)(object)new ValidationBehavior<CreateTaskItemCommand, Response<Guid>>(
                        [new CreateTaskItemValidator()])
                    .Handle(command, () => handler.Handle(command, ct), ct)
                : throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.");

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

    /// <summary>A tenant user whose permission claims are whatever the test puts in one header.</summary>
    private sealed class HeaderAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new("actor_type", "tenant_user"),
                new("tenant_id", TaskTestData.Tenant.ToString()),
                new("sub", Me.ToString())
            };

            foreach (var key in Request.Headers[PermissionsHeader].ToString()
                         .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim("permission", key));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
