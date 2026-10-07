using System.Net;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, stage D: the tenant administrator's invitation carries the way in.</summary>
public sealed partial class EmailShellDispatchTests
{
    private const string InviteToken = "stage-d-test-only-setup-token";
    private const string InviteExpiry = "2026-10-14 09:30";

    public static TheoryData<string> InviteLanguages() => new() { "en", "tr", "fr", "es", "zh", "ar", "ru" };

    // ---------------------------------------------------------------- the mail: link, expiry, no password, 7 languages

    [Theory]
    [MemberData(nameof(InviteLanguages))]
    public async Task The_tenant_admin_invitation_carries_the_set_password_link_and_its_expiry_and_the_row_keeps_neither_token(string language)
    {
        var rig = new Rig { TenantLocale = language };
        var seeded = rig.AddSeeded(NotificationTemplateSeed.TenantInvite(language));
        var link = $"https://app.example.test/account/set-password?email=first%40tenant.test&token={InviteToken}";

        await rig.QueueAsync(language, InviteVariables(link));

        var sent = rig.Sent;
        Assert.Equal(seeded.SubjectTemplate, sent.Subject);
        Assert.Contains(seeded.Shell!.HeadingTemplate!, sent.TextBody);
        Assert.Contains(seeded.Shell.ActionLabel!, sent.TextBody);           // the button, in this language
        Assert.Contains(link, sent.TextBody);                                 // and the address under it
        Assert.Contains(WebUtility.HtmlEncode(link), sent.HtmlBody);
        Assert.Contains(InviteExpiry, sent.TextBody);                         // until when it works
        Assert.DoesNotContain("TemporaryPassword", sent.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html", sent.HtmlBody, StringComparison.OrdinalIgnoreCase);

        var row = Assert.Single(rig.Dispatches.Items);
        Assert.DoesNotContain(InviteToken, row.VariablesJson);
        Assert.DoesNotContain(InviteToken, row.BodyHtmlPreview ?? string.Empty);
        Assert.DoesNotContain(InviteToken, row.BodyTextPreview ?? string.Empty);
        Assert.DoesNotContain(InviteToken, row.Subject);
    }

    [Fact]
    public async Task An_invitation_without_its_link_is_refused_rather_than_sent_without_a_way_in()
    {
        var rig = new Rig();
        rig.AddSeeded(NotificationTemplateSeed.TenantInvite("en"));
        var variables = InviteVariables("https://app.example.test/account/set-password?token=x");
        variables.Remove("SetPasswordUrl");

        var response = await rig.QueueAsync("en", variables);

        Assert.False(response.IsSuccessful);
        Assert.Null(rig.Transport.LastSentMessage);
    }

    [Fact]
    public void Every_invitation_language_has_the_link_the_expiry_and_no_password()
    {
        foreach (var language in NotificationTemplateSeed.TenantLocales)
        {
            var template = NotificationTemplateSeed.TenantInvite(language);
            Assert.Equal("1.2.0", template.SemanticVersion);
            Assert.Equal("SetPasswordUrl", template.Shell!.ActionUrlVariable);
            Assert.False(string.IsNullOrWhiteSpace(template.Shell.ActionLabel));
            Assert.Contains(template.Shell.InfoRows, r => r.ValueTemplate == "{{LinkExpiresAtUtc}}");
            Assert.Equal(
                ["LinkExpiresAtUtc", "SetPasswordUrl", "TenantDisplayName", "TenantId"],
                template.Variables.Where(v => v.IsRequired).Select(v => v.Name).OrderBy(x => x, StringComparer.Ordinal));
            Assert.DoesNotContain("TemporaryPassword", template.BodyHtmlTemplate + template.BodyTextTemplate, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- a retry cannot rebuild the link: closed, by name

    [Fact]
    public async Task A_retry_of_an_invitation_whose_link_was_masked_is_closed_by_name_and_sends_nothing()
    {
        var rig = new Rig();
        var template = rig.AddSeeded(NotificationTemplateSeed.TenantInvite("en"));
        var stored = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["TenantId"] = Guid.NewGuid().ToString(),
            ["TenantDisplayName"] = "Diten Pharma",
            ["SetPasswordUrl"] = "[REDACTED]",
            ["LinkExpiresAtUtc"] = InviteExpiry
        });
        var row = rig.AddFailedDispatch(stored, template.Id);
        var mediator = new ValidatingMediator(rig.Dispatches);

        await new EmailDispatchJob(
                rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
                new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider), mediator,
                NullLogger<EmailDispatchJob>.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer)
            .HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Empty(mediator.Refusals);
        Assert.Null(rig.Transport.LastSentMessage);
        Assert.Equal(NotificationDispatchStatus.Failed, row.Status);
        Assert.Equal(EmailDispatchJob.ReasonActionLinkNotRetryable, row.ErrorCode);
        Assert.NotNull(row.PermanentlyFailedNotifiedAt); // permanent: no retry will ever send it
    }

    // ---------------------------------------------------------------- the link's root: configuration, never a silent localhost

    [Theory]
    [InlineData("Production", "http://localhost:5001", TenantAdminSetPasswordLink.ReasonRootLoopback)]
    [InlineData("Production", "https://localhost.", TenantAdminSetPasswordLink.ReasonRootLoopback)]      // FIX1 K2: trailing dot
    [InlineData("Production", "http://app.example.test", TenantAdminSetPasswordLink.ReasonRootNotHttps)] // FIX1 K2: https only
    [InlineData("Production", "http://127.0.0.1:5001", TenantAdminSetPasswordLink.ReasonRootLoopback)]
    [InlineData("Production", "https://tenant.localhost", TenantAdminSetPasswordLink.ReasonRootLoopback)]
    [InlineData("Production", "", TenantAdminSetPasswordLink.ReasonRootMissing)]
    [InlineData("Staging", "not a url", TenantAdminSetPasswordLink.ReasonRootMissing)]
    [InlineData("Production", "https://app.example.test", null)]
    [InlineData("Development", "http://localhost:5001", null)]
    public async Task The_invitation_link_comes_from_configuration_and_a_loopback_or_missing_root_outside_development_sends_nothing(
        string environment, string root, string? refusal)
    {
        var mediator = new DispatchRecorder();
        var auth = new FakeAuthInvite();
        var service = new AdminUserInvitationService(
            new SingleClientFactory(auth),
            mediator,
            Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "stage-d-test-only-key", FrontendBaseUrl = root }),
            NullLogger<AdminUserInvitationService>.Instance,
            new NamedEnvironment(environment));
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Code = "S2D", Slug = "s2d", Name = "s2d", DisplayName = "Stage D", Domain = "s2d.test",
            Region = "EU", Environment = "Production", DefaultLanguage = "tr"
        };
        var admin = new TenantAdminUser { Id = Guid.NewGuid(), Name = "First Admin", Email = "first@tenant.test" };

        var result = await service.InviteAsync(tenant, admin, AdminInvitationTrigger.Operator, CancellationToken.None);

        // FIX1 (2) — the root is checked FIRST: a refused root never reaches AuthService (nothing is reset).
        Assert.Equal(refusal is null ? 1 : 0, auth.Calls);
        Assert.Equal(refusal, result.EmailRefusalCode);
        if (refusal is not null)
        {
            Assert.False(result.InvitationEmailSent);
            Assert.Null(result.SetPasswordUrl);
            Assert.Empty(mediator.Requests);
            return;
        }

        var request = Assert.Single(mediator.Requests);
        Assert.Equal("tenant.user.invited", request.EventCode);
        Assert.Equal("tr", request.Locale);
        var url = Assert.IsType<string>(request.Variables["SetPasswordUrl"]);
        Assert.StartsWith(root.TrimEnd('/') + "/account/set-password?email=first%40tenant.test&token=", url);
        Assert.EndsWith(Uri.EscapeDataString(auth.Token), url);
        Assert.Equal(auth.ExpiresAt.ToString("yyyy-MM-dd HH:mm"), request.Variables["LinkExpiresAtUtc"]);
        Assert.Equal(tenant.Id, request.Variables["TenantId"]);
        Assert.False(request.Variables.ContainsKey("TemporaryPassword"));
        Assert.True(result.InvitationEmailSent);
    }

    private static Dictionary<string, object?> InviteVariables(string link) => new()
    {
        ["TenantId"] = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ["TenantDisplayName"] = "Diten Pharma",
        ["SetPasswordUrl"] = link,
        ["LinkExpiresAtUtc"] = InviteExpiry
    };

    /// <summary>AuthService's tenant-admin-invited door: a token and its expiry, never a password.</summary>
    private sealed class FakeAuthInvite : HttpMessageHandler
    {
        public string Token { get; } = "stage-d-" + Guid.NewGuid().ToString("N");
        public DateTime ExpiresAt { get; } = new(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.EndsWith("/internal/events/tenant-admin-invited", request.RequestUri!.AbsolutePath);
            var body = JsonSerializer.Serialize(new { userProvisioned = true, setupToken = Token, setupExpiresAtUtc = ExpiresAt, message = "processed" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class NamedEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.Platform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class DispatchRecorder : IMediator
    {
        public List<NotificationEventDispatchRequest> Requests { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is DispatchNotificationByEventCodeCommand dispatch)
            {
                Requests.Add(dispatch.Request);
            }

            return Task.FromResult((TResponse)(object)Response<NotificationDispatchDto>.Success(202));
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
