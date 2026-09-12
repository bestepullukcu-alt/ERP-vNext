using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
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

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S1, AC6 — the field on the WIRE. Real routing, real <c>WorkItemsController</c>, real
/// <c>GetMyWorkItemsHandler</c>, real <c>TaskWorkItemProvider</c>; only the repositories and the "meetings"
/// resolver are doubles (MOD-0357's own meeting record does not exist until S2).
/// </summary>
public sealed class RelatedRecordsHttpTests
{
    private static readonly Guid TaskId = Guid.Parse("55555555-0000-0000-0000-000000000005");
    private static readonly Guid MeetingId = Guid.Parse("66666666-0000-0000-0000-000000000006");

    [Fact]
    public async Task GetMine_returns_relatedRecords_camelCase_populated_from_a_seeded_link()
    {
        using var host = new Host();
        var link = new RecordLink
        {
            TenantId = TaskTestData.Tenant,
            SourceModuleCode = RecordLinkModuleCodes.Meetings,
            SourceRecordId = MeetingId,
            TargetModuleCode = RecordLinkModuleCodes.Tasks,
            TargetRecordId = TaskId,
            LinkType = RecordLinkTypes.Agenda,
            CreatedByUserId = TaskTestData.Me,
            CreatedBy = "test"
        };
        host.Links.Seed(link);
        host.MeetingsResolver.Known[MeetingId] = new RelatedRecordSummary("Aylık QA Toplantısı", "/Meetings/" + MeetingId);

        var response = await host.GetMineAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"relatedRecords\":[{", body);
        Assert.Contains("\"type\":\"meetings\"", body);
        Assert.Contains("\"title\":\"Aylık QA Toplantısı\"", body);
        Assert.Contains($"\"link\":\"/Meetings/{MeetingId}\"", body);
        // PascalCase would mean the DTO's own casing leaked past the API's naming policy.
        Assert.DoesNotContain("\"RelatedRecords\"", body);
    }

    [Fact]
    public async Task GetMine_omits_relatedRecords_entirely_when_the_task_has_no_link()
    {
        using var host = new Host();

        var response = await host.GetMineAsync();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("relatedRecords", body);
    }

    [Fact]
    public async Task GetMine_without_a_token_is_401()
    {
        using var host = new Host();
        var client = host.CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/work-items/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;

        public Host()
        {
            var task = new TaskItem
            {
                Id = TaskId,
                TenantId = TaskTestData.Tenant,
                Title = "İlişkili görev",
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = Guid.NewGuid(),
                Lifecycle = TaskLifecycle.Open
            };
            Tasks = new FakeTaskItemRepository(task);
            Links = new FakeRecordLinkRepository();
            MeetingsResolver = new FakeRelatedRecordResolver(RecordLinkModuleCodes.Meetings, new Dictionary<Guid, RelatedRecordSummary>());

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
                    services.AddScoped<ICurrentUserContext>(_ => new FakeCurrentUserContext(TaskTestData.Me));
                    services.AddScoped<IEnumerable<IWorkItemProvider>>(_ => new[] { (IWorkItemProvider)Provider() });
                    services.AddScoped<IEnumerable<IWorkItemActionDispatcher>>(_ => Array.Empty<IWorkItemActionDispatcher>());
                    services.AddScoped<IMediator>(sp => new SingleQueryMediator(new GetMyWorkItemsHandler(
                        sp.GetRequiredService<IEnumerable<IWorkItemProvider>>(),
                        sp.GetRequiredService<ICurrentUserContext>(),
                        Options.Create(new WorkAggregationResilienceOptions()),
                        NullLogger<GetMyWorkItemsHandler>.Instance)));
                    services.AddControllers().AddApplicationPart(typeof(WorkItemsController).Assembly);
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

        public FakeTaskItemRepository Tasks { get; }

        public FakeRecordLinkRepository Links { get; }

        public FakeRelatedRecordResolver MeetingsResolver { get; }

        public async Task<HttpResponseMessage> GetMineAsync()
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add(
                HeaderAuthentication.PermissionsHeader,
                $"{WorkAggregationPermissions.InboxView} {TaskPermissions.Update} {TaskPermissions.Claim} "
                + $"{TaskPermissions.Complete} {TaskPermissions.Cancel} {TaskPermissions.Delete} {TaskPermissions.Assign}");
            return await client.GetAsync("/api/v1/work-items/mine");
        }

        public HttpClient CreateAnonymousClient() => _server.CreateClient();

        public void Dispose() => _server.Dispose();

        private TaskWorkItemProvider Provider()
        {
            var me = new FakeCurrentUserContext(TaskTestData.Me);
            return new TaskWorkItemProvider(
                Tasks,
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                new FakeTaskCommentRepository(),
                new FakeTaskTransitionRepository(),
                new FakeTaskPersonalOverlayRepository(),
                new FakeTaskWatcherRepository(),
                TaskActors.PermitAll(),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(),
                SlaForTests.Real(),
                new FakeTaskFieldDefinitionRepository(),
                new FakeTaskTypeRepository(),
                teamResolver: null,
                recordLinks: new RecordLinkService(Links, new FakeTenantContext(TaskTestData.Tenant), me),
                relatedRecordResolvers: new FakeRelatedRecordResolverRegistry(MeetingsResolver));
        }
    }

    /// <summary>Routes exactly one query — `GetMyWorkItemsQuery` — to the real handler. No ValidationBehavior
    /// pipeline needed: this query carries no FluentValidation validator.</summary>
    private sealed class SingleQueryMediator(GetMyWorkItemsHandler handler) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is GetMyWorkItemsQuery query
                ? (Task<TResponse>)(object)handler.Handle(query, ct)
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

    /// <summary>A tenant user whose permission claims are whatever the test puts in one header — the same
    /// shape <c>TaskLifecycleAuthorityHttpTests</c> already uses.</summary>
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
                new("sub", TaskTestData.Me.ToString())
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
