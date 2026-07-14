using System.Net;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

// MOD-0027-FU04C — the admin invite dispatches by canonical eventCode (tenant.user.invited) via the FU04B adapter
// command, not by a raw templateKey, and stays fail-soft (a notification failure never fails the provisioned invite).
public sealed class AdminUserInvitationServiceTests
{
    [Fact]
    public async Task InviteAsync_DispatchesByEventCode_WithAlignedVariables()
    {
        var mediator = new RecordingMediator();
        var service = CreateService(mediator);
        var tenant = CreateTenant();
        var adminUser = new TenantAdminUser { Id = Guid.NewGuid(), Name = "Ada Admin", Email = "ada@example.com" };

        var result = await service.InviteAsync(tenant, adminUser, default);

        Assert.True(result.InvitationEmailSent);
        Assert.Empty(mediator.QueueCommands); // no direct QueueEmailNotificationCommand
        var request = Assert.Single(mediator.DispatchCommands).Request;
        Assert.Equal("tenant.user.invited", request.EventCode);
        Assert.Equal(tenant.Id, request.TenantId);
        Assert.Equal("ada@example.com", Assert.Single(request.To).Email);
        Assert.Equal("Grand Medical Group", request.Variables["TenantDisplayName"]);
        Assert.Equal("Ada Admin", request.Variables["RecipientName"]);
        Assert.Equal("ada@example.com", request.Variables["Email"]);
        Assert.Equal("TmpPw123!", request.Variables["TemporaryPassword"]);
        Assert.True(request.Variables.ContainsKey("LoginUrl"));
    }

    [Fact]
    public async Task InviteAsync_IsFailSoft_WhenNotificationFails()
    {
        var mediator = new RecordingMediator
        {
            Response = Response<NotificationDispatchDto>.Fail("event not active", 409, "EVENT_NOT_ACTIVE")
        };
        var service = CreateService(mediator);
        var tenant = CreateTenant();
        var adminUser = new TenantAdminUser { Id = Guid.NewGuid(), Name = "Ada", Email = "ada@example.com" };

        // No throw: provisioning already succeeded; only the email flag reflects the failure.
        var result = await service.InviteAsync(tenant, adminUser, default);

        Assert.True(result.UserProvisioned);
        Assert.False(result.InvitationEmailSent);
        Assert.Single(mediator.DispatchCommands); // attempted via eventCode
    }

    private static AdminUserInvitationService CreateService(RecordingMediator mediator)
    {
        var httpFactory = new StubHttpClientFactory(new StubHandler(
            HttpStatusCode.OK, """{"userProvisioned":true,"temporaryPassword":"TmpPw123!","message":null}"""));
        var authOptions = Options.Create(new AuthServiceOptions
        {
            BaseUrl = "http://auth.local",
            InternalApiKey = "internal-key",
            TenantLoginUrlTemplate = "https://{tenantDomain}/account/login?tenantId={tenantId}"
        });
        return new AdminUserInvitationService(httpFactory, mediator, authOptions, NullLogger<AdminUserInvitationService>.Instance);
    }

    private static Tenant CreateTenant() => new()
    {
        Id = Guid.NewGuid(),
        Code = "GMG",
        Slug = "gmg",
        Name = "GMG",
        DisplayName = "Grand Medical Group",
        Domain = "gmg.example.com",
        Region = "US",
        Environment = "Production",
        DefaultLanguage = "en"
    };

    private sealed class RecordingMediator : IMediator
    {
        public List<QueueEmailNotificationCommand> QueueCommands { get; } = [];
        public List<DispatchNotificationByEventCodeCommand> DispatchCommands { get; } = [];
        public Response<NotificationDispatchDto> Response { get; set; } = Response<NotificationDispatchDto>.Success(202);

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            switch (request)
            {
                case DispatchNotificationByEventCodeCommand d:
                    DispatchCommands.Add(d);
                    return Task.FromResult((TResponse)(object)Response);
                case QueueEmailNotificationCommand q:
                    QueueCommands.Add(q);
                    return Task.FromResult((TResponse)(object)Response);
                default:
                    throw new NotSupportedException($"RecordingMediator does not handle {request.GetType().Name}.");
            }
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly StubHandler _handler;
        public StubHttpClientFactory(StubHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = new Uri("http://auth.local") };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public StubHandler(HttpStatusCode status, string json) { _status = status; _json = json; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_json, Encoding.UTF8, "application/json") });
    }
}
