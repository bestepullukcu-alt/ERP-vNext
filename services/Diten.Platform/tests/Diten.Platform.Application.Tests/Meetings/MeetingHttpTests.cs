using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
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
/// MOD-0357 S2, AC9/AC12 evidence requirement E4 — real <c>MeetingsController</c>, real routing, real
/// <c>[HasPermission]</c> enforcement; only the repositories and the eligibility seam are doubles (the same
/// posture <c>RelatedRecordsHttpTests</c> — S1 — already took).
/// </summary>
public sealed class MeetingHttpTests
{
    private static readonly Guid Organizer = TaskTestData.Me;

    [Fact]
    public async Task Create_then_GetById_round_trips_and_the_wire_is_camelCase()
    {
        using var host = new Host();
        var typeId = await host.SeedTypeAsync();

        var createBody = new
        {
            title = "Aylık QA Toplantısı",
            meetingTypeId = typeId,
            startAt = DateTimeOffset.UtcNow,
            endAt = DateTimeOffset.UtcNow.AddHours(1),
            location = (string?)null,
            organizerUserId = Organizer,
            description = (string?)null,
            followUpOfMeetingId = (Guid?)null,
            attendeeUserIds = (Guid[]?)null
        };

        var createResponse = await host.PostAsync("/api/v1/meetings", createBody, MeetingPermissions.Create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createBodyText = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"organizerUserId\"", createBodyText);
        Assert.DoesNotContain("\"OrganizerUserId\"", createBodyText);

        using var doc = System.Text.Json.JsonDocument.Parse(createBodyText);
        var id = doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var getResponse = await host.GetAsync($"/api/v1/meetings/{id}", MeetingPermissions.Read);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBodyText = await getResponse.Content.ReadAsStringAsync();
        Assert.Contains(id.ToString(), getBodyText);
    }

    [Fact]
    public async Task GetById_without_the_read_permission_is_403()
    {
        using var host = new Host();
        var typeId = await host.SeedTypeAsync();
        var meetingId = await host.SeedMeetingAsync(typeId, Organizer);

        var response = await host.GetAsync($"/api/v1/meetings/{meetingId}", grantedPermission: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_without_a_token_is_401()
    {
        using var host = new Host();
        var client = host.CreateAnonymousClient();

        var response = await client.GetAsync("/api/v1/meetings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_a_stale_ExpectedVersion_is_409()
    {
        using var host = new Host();
        var typeId = await host.SeedTypeAsync();
        var meetingId = await host.SeedMeetingAsync(typeId, Organizer);

        var body = new
        {
            title = "Yeni Başlık", meetingTypeId = typeId,
            startAt = DateTimeOffset.UtcNow, endAt = DateTimeOffset.UtcNow.AddHours(1),
            location = (string?)null, description = (string?)null, expectedVersion = 999
        };

        var response = await host.PutAsync($"/api/v1/meetings/{meetingId}", body, MeetingPermissions.Update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(MeetingReasonCodes.ConcurrencyConflict, text);
    }

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;
        private readonly FakeMeetingRepository _meetings = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeMeetingTypeRepository _types = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeMeetingAttendeeRepository _attendees = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeAgendaItemRepository _agenda = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeEligibilityMediator _eligibility = new();

        public Host()
        {
            _eligibility.EligibleUserIds.Add(Organizer);

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

        public async Task<Guid> SeedMeetingAsync(Guid typeId, Guid organizerId)
        {
            var meeting = new Meeting
            {
                TenantId = TaskTestData.Tenant, Title = "T", MeetingTypeId = typeId,
                StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
                OrganizerUserId = organizerId, IdempotencyKey = Guid.NewGuid().ToString()
            };
            _meetings.Seed(meeting);
            return await Task.FromResult(meeting.Id);
        }

        public async Task<HttpResponseMessage> PostAsync(string path, object body, string? grantedPermission)
            => await SendAsync(HttpMethod.Post, path, body, grantedPermission);

        public async Task<HttpResponseMessage> PutAsync(string path, object body, string? grantedPermission)
            => await SendAsync(HttpMethod.Put, path, body, grantedPermission);

        public async Task<HttpResponseMessage> GetAsync(string path, string? grantedPermission)
            => await SendAsync(HttpMethod.Get, path, null, grantedPermission);

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, string? grantedPermission)
        {
            var client = _server.CreateClient();
            if (grantedPermission is not null)
            {
                client.DefaultRequestHeaders.Add(HeaderAuthentication.PermissionsHeader, grantedPermission);
            }
            else
            {
                client.DefaultRequestHeaders.Add(HeaderAuthentication.PermissionsHeader, "platform.meetings.__none__");
            }

            var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await client.SendAsync(request);
        }

        public HttpClient CreateAnonymousClient() => _server.CreateClient();

        public void Dispose() => _server.Dispose();

        private sealed class RoutingMediator(Host host) : IMediator
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            {
                object result = request switch
                {
                    CreateMeetingCommand cmd => new CreateMeetingHandler(
                        host._meetings, host._types, host._attendees,
                        new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(Organizer),
                        new MeetingIdempotencyKeyResolver(), host._eligibility).Handle(cmd, ct),
                    GetMeetingByIdQuery query => new GetMeetingByIdHandler(
                        host._meetings, host._types, host._attendees, host._agenda,
                        new FakeCurrentUserContext(Organizer), new FakeActorPermissionContext()).Handle(query, ct),
                    UpdateMeetingCommand cmd => new UpdateMeetingHandler(host._meetings, host._types).Handle(cmd, ct),
                    CancelMeetingCommand cmd => new CancelMeetingHandler(host._meetings).Handle(cmd, ct),
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
                new("sub", Organizer.ToString())
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
