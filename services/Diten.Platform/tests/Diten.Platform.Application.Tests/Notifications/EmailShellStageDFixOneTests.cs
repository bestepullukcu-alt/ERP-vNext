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
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Eventing;
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

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, stage D fix round 1 (P-2026-10-07-EMAIL-S2D-FIX1), Platform side.</summary>
public sealed partial class EmailShellDispatchTests
{
    // ---------------------------------------------------------------- 1: the caller says who it is; the event only creates

    [Theory]
    [InlineData(AdminInvitationTrigger.Operator, "/internal/events/tenant-admin-invited", "operator-invite")]
    [InlineData(AdminInvitationTrigger.TenantCreatedEvent, "/internal/events/tenant-admin-created", null)] // FIX2 (3): its own door
    [InlineData(AdminInvitationTrigger.Unspecified, "/internal/events/tenant-admin-created", null)]        // FIX2 K8: zero is safe
    public async Task The_invitation_goes_to_the_door_of_whoever_is_asking(AdminInvitationTrigger trigger, string path, string? wire)
    {
        var auth = new ScriptedAuth(accountExists: false);
        await Invitations(auth, "Production", "https://app.example.test").InviteAsync(StageDTenant(), StageDAdmin(), trigger, CancellationToken.None);

        Assert.Equal(path, Assert.Single(auth.Paths));
        Assert.Equal(wire, Assert.Single(auth.Triggers));
    }

    [Fact]
    public void The_zero_trigger_is_not_the_operator() =>
        Assert.Equal(AdminInvitationTrigger.Unspecified, default(AdminInvitationTrigger));

    [Fact]
    public async Task An_AuthService_without_the_create_only_door_fails_the_event_closed()
    {
        // FIX2 (3) — an older AuthService answers 404 on tenant-admin-created: the call throws (the transport retries) and
        // never falls back to the operator's door.
        var auth = new ScriptedAuth(accountExists: false) { Status = HttpStatusCode.NotFound };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Invitations(auth, "Production", "https://app.example.test")
            .InviteAsync(StageDTenant(), StageDAdmin(), AdminInvitationTrigger.TenantCreatedEvent, CancellationToken.None));
        Assert.Equal(["/internal/events/tenant-admin-created"], auth.Paths);
    }

    [Fact]
    public async Task A_token_less_answer_that_does_not_say_the_account_exists_is_not_trusted()
    {
        // CT E5 — 200 without a token is "the account exists" only when AuthService says exactly that.
        var auth = new ScriptedAuth(accountExists: false) { TokenLessMessage = "processed" };
        var mediator = new DispatchRecorder();

        var result = await Invitations(auth, "Production", "https://app.example.test", mediator)
            .InviteAsync(StageDTenant(), StageDAdmin(), AdminInvitationTrigger.TenantCreatedEvent, CancellationToken.None);

        Assert.Equal(AdminInvitationRefusals.AuthAnswerInvalid, result.EmailRefusalCode);
        Assert.False(result.InvitationEmailSent);
        Assert.Empty(mediator.Requests);
    }

    [Fact]
    public async Task AuthService_refusing_the_platform_tenant_reaches_the_operator_by_name()
    {
        var auth = new ScriptedAuth(accountExists: false) { Status = HttpStatusCode.BadRequest, Body = "{\"message\":\"x\",\"code\":\"PLATFORM_TENANT_REFUSED\"}" };
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        tenant.AdminUsers.Add(admin);

        var response = await OperatorHandler(tenant, Invitations(auth, "Production", "https://app.example.test"))
            .Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.Equal(422, response.StatusCode);
        Assert.Equal(AdminInvitationRefusals.PlatformTenantRefused, response.ReasonCode);
    }

    [Fact]
    public async Task An_account_that_already_exists_on_the_event_path_gets_no_link_and_no_mail()
    {
        var auth = new ScriptedAuth(accountExists: true);
        var mediator = new DispatchRecorder();

        var result = await Invitations(auth, "Production", "https://app.example.test", mediator)
            .InviteAsync(StageDTenant(), StageDAdmin(), AdminInvitationTrigger.TenantCreatedEvent, CancellationToken.None);

        Assert.Equal(AdminInvitationRefusals.AccountExists, result.EmailRefusalCode);
        Assert.False(result.InvitationEmailSent);
        Assert.Null(result.SetPasswordUrl);
        Assert.Empty(mediator.Requests);
    }

    // ---------------------------------------------------------------- 2: the root before AuthService, and the operator is told

    [Fact]
    public async Task The_operators_invite_with_a_loopback_root_outside_development_never_reaches_AuthService_and_says_why()
    {
        var auth = new ScriptedAuth(accountExists: false);
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited; // already counted: the users quota asks for nothing more
        tenant.AdminUsers.Add(admin);
        var handler = OperatorHandler(tenant, Invitations(auth, "Production", "http://localhost:5001"));

        var response = await handler.Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.Equal(0, auth.Calls);                       // nothing reset, nothing created
        Assert.False(response.IsSuccessful);
        Assert.Equal(422, response.StatusCode);
        Assert.Equal(TenantAdminSetPasswordLink.ReasonRootLoopback, response.ReasonCode);
    }

    [Fact]
    public async Task A_refused_root_is_found_before_the_users_quota_is_asked()
    {
        // FIX2 K11 — an administrator not yet counted would make the operator's "Invite" ask the users quota; a root that
        // may not send must be found first (the quota double throws if it is asked at all).
        var auth = new ScriptedAuth(accountExists: false);
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.PendingInvitation;
        tenant.AdminUsers.Add(admin);

        var response = await OperatorHandler(tenant, Invitations(auth, "Production", "https://0.0.0.0"))
            .Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.Equal(TenantAdminSetPasswordLink.ReasonRootLoopback, response.ReasonCode);
        Assert.Equal(0, auth.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_operators_invite_writes_the_step_and_says_whether_the_mail_left_in_every_environment(bool mailLeaves)
    {
        // FIX2 K6 + K14 — Production: no link is ever returned, but EmailSent is (false after a reset with no mail).
        var auth = new ScriptedAuth(accountExists: false);
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        tenant.AdminUsers.Add(admin);
        var mediator = new DispatchRecorder { Fails = !mailLeaves };

        var response = await OperatorHandler(tenant, Invitations(auth, "Production", "https://app.example.test", mediator))
            .Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(mailLeaves, response.Data!.EmailSent);
        Assert.Null(response.Data.LoginUrl); // never a link outside Development
        var step = Assert.Single(tenant.ProvisioningSteps, s => s.Key == "admin-invitation");
        Assert.Equal(mailLeaves ? "Completed" : "Failed", step.Status);
    }

    // ---------------------------------------------------------------- 3: InMemory outside Development is said once

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task With_events_in_memory_the_missing_automatic_invitation_is_said_once_at_start_outside_development(string environment, bool warned)
    {
        var logger = new LinesLogger<TenantAdminInvitationModeNotice>();

        await new TenantAdminInvitationModeNotice(new StageDEnvironment(environment), logger).StartAsync(CancellationToken.None);

        Assert.Equal(warned, logger.Entries.Any(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning
            && e.Line.Contains("automatic_off", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("RabbitMQ", false)]
    [InlineData("InMemory", true)]
    public void The_in_memory_notice_is_registered_on_the_in_memory_branch_only(string transport, bool registered)
    {
        // FIX2 (4) — measured on the production registration (the branch AddInfrastructure takes).
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        Diten.Platform.Infrastructure.DependencyInjection.AddPlatformEventTransport(services, new RabbitMqEventingOptions { Transport = transport });

        Assert.Equal(registered, services.Any(d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(TenantAdminInvitationModeNotice)));
    }

    // ---------------------------------------------------------------- K4: an undeliverable invitation leaves its mark

    [Fact]
    public async Task An_invitation_whose_link_cannot_be_resent_marks_the_tenants_admin_invitation_step()
    {
        var rig = new Rig();
        var template = rig.AddSeeded(NotificationTemplateSeed.TenantInvite("en"));
        var row = rig.AddFailedDispatch(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["TenantId"] = rig.TenantId.ToString(), ["TenantDisplayName"] = "Diten Pharma",
            ["SetPasswordUrl"] = "[REDACTED]", ["LinkExpiresAtUtc"] = "2026-10-14 09:30"
        }), template.Id);
        row.TemplateKey = "tenant.invite.email";
        var ledger = new RecordingLedger();

        await new EmailDispatchJob(
                rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
                new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider), new ValidatingMediator(rig.Dispatches),
                NullLogger<EmailDispatchJob>.Instance, ledger, rig.Templates, new EmailTemplateRenderer(), rig.Composer)
            .HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        var (tenantId, email, reason, dispatchId) = Assert.Single(ledger.Calls);
        Assert.Equal(rig.TenantId, tenantId);
        Assert.Equal(row.Id, dispatchId); // FIX3 (1): the failing dispatch, by identity — never a clock
        Assert.Equal(row.To[0].Email, email);
        Assert.Equal(EmailDispatchJob.ReasonActionLinkNotRetryable, reason);
    }

    [Fact]
    public async Task Another_mail_whose_link_cannot_be_resent_does_not_touch_the_tenants_invitation()
    {
        // CT E6 — only tenant.invite.email marks the tenant; another template's link (here: the same seeded content under
        // another key, standing for e.g. a user's reset mail) closes its own row and nothing else.
        var rig = new Rig();
        var template = rig.AddSeeded(NotificationTemplateSeed.TenantInvite("en"));
        var row = rig.AddFailedDispatch(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["TenantId"] = rig.TenantId.ToString(), ["TenantDisplayName"] = "Diten Pharma",
            ["SetPasswordUrl"] = "[REDACTED]", ["LinkExpiresAtUtc"] = "2026-10-14 09:30"
        }), template.Id);
        row.TemplateKey = "platform.users.password-reset";
        var ledger = new RecordingLedger();

        await new EmailDispatchJob(
                rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
                new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider), new ValidatingMediator(rig.Dispatches),
                NullLogger<EmailDispatchJob>.Instance, ledger, rig.Templates, new EmailTemplateRenderer(), rig.Composer)
            .HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        Assert.Equal(EmailDispatchJob.ReasonActionLinkNotRetryable, row.ErrorCode); // the row is closed by name
        Assert.Empty(ledger.Calls);
    }

    [Fact]
    public void The_retry_job_cannot_be_composed_without_the_ledger()
    {
        // FIX2 K12 — required: a composition without it fails instead of never marking a tenant.
        var rig = new Rig();
        Assert.Throws<ArgumentNullException>(() => new EmailDispatchJob(
            rig.Dispatches, new TenantMessagingSettingsResolver(rig.Settings),
            new NotificationsSmtpIntegrationTests.TestProviderResolver(rig.Provider), new ValidatingMediator(rig.Dispatches),
            NullLogger<EmailDispatchJob>.Instance, null!));
    }

    [Fact]
    public async Task The_ledger_writes_the_failed_step_and_tells_the_operator_to_invite_again()
    {
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        admin.InvitedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var dispatchId = Guid.NewGuid();
        admin.LastInvitationDispatchId = dispatchId; // this dispatch is the current invitation
        tenant.AdminUsers.Add(admin);
        tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = "admin-invitation", Label = "Initial Admin Invitation", Status = "Completed" });

        await new TenantAdminInvitationLedger(new OneTenant(tenant))
            .RecordUndeliveredAsync(tenant.Id, admin.Email, EmailDispatchJob.ReasonActionLinkNotRetryable, dispatchId, CancellationToken.None);

        var step = Assert.Single(tenant.ProvisioningSteps);
        Assert.Equal("Failed", step.Status);
        Assert.Contains("Invite", step.Detail);
        Assert.Contains(EmailDispatchJob.ReasonActionLinkNotRetryable, step.Detail);
        Assert.Contains(tenant.ActivityTimeline, e => e.EventType == "tenant.admin_user.invitation_undelivered");
    }

    [Theory]
    [InlineData(TenantAdminUserStatus.Active, false)]  // the administrator got in after all
    [InlineData(TenantAdminUserStatus.Invited, true)]  // a NEWER invitation (another dispatch) is the current one
    public async Task The_ledger_leaves_the_step_alone_when_this_invitation_is_no_longer_the_current_state(TenantAdminUserStatus status, bool newerInvitation)
    {
        // FIX2 K5, by identity since FIX3 (1).
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = status;
        var failingDispatch = Guid.NewGuid();
        admin.InvitedAt = DateTimeOffset.UtcNow;
        admin.LastInvitationDispatchId = newerInvitation ? Guid.NewGuid() : failingDispatch;
        tenant.AdminUsers.Add(admin);
        tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = "admin-invitation", Label = "Initial Admin Invitation", Status = "Completed" });

        await new TenantAdminInvitationLedger(new OneTenant(tenant))
            .RecordUndeliveredAsync(tenant.Id, admin.Email, EmailDispatchJob.ReasonActionLinkNotRetryable, failingDispatch, CancellationToken.None);

        Assert.Equal("Completed", Assert.Single(tenant.ProvisioningSteps).Status);
        Assert.Empty(tenant.ActivityTimeline);
    }

    // ---------------------------------------------------------------- helpers

    private static Tenant StageDTenant() => new()
    {
        Id = Guid.NewGuid(), Code = "S2DF", Slug = "s2df", Name = "s2df", DisplayName = "Stage D fix", Domain = "s2df.test",
        Region = "EU", Environment = "Production", DefaultLanguage = "en"
    };

    private static TenantAdminUser StageDAdmin() => new() { Id = Guid.NewGuid(), Name = "First Admin", Email = "first@tenant.test" };

    private static AdminUserInvitationService Invitations(
        ScriptedAuth auth, string environment, string root, IMediator? mediator = null, Microsoft.Extensions.Logging.ILogger<AdminUserInvitationService>? logger = null) => new(
        new SingleClientFactory(auth),
        mediator ?? new DispatchRecorder(),
        Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "stage-d-test-only-key", FrontendBaseUrl = root }),
        logger ?? NullLogger<AdminUserInvitationService>.Instance,
        new StageDEnvironment(environment));

    /// <summary>AuthService's tenant-admin-invited door: records the trigger; answers a token, or "the account exists".</summary>
    private sealed class ScriptedAuth(bool accountExists) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string?> Triggers { get; } = [];
        public List<string> Paths { get; } = [];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public string? Body { get; init; }
        public string? TokenLessMessage { get; init; }

        /// <summary>FIX3 (2) — a 200 whose body is exactly this (e.g. a live token with an expiry that cannot be read).</summary>
        public string? OkBody { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Paths.Add(request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Triggers.Add(body.RootElement.TryGetProperty("trigger", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null);
            if (Status != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(Status) { Content = new StringContent(Body ?? "{}", Encoding.UTF8, "application/json") };
            }

            if (OkBody is not null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody, Encoding.UTF8, "application/json") };
            }

            if (TokenLessMessage is not null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                    JsonSerializer.Serialize(new { userProvisioned = true, setupToken = (string?)null, setupExpiresAtUtc = (DateTime?)null, message = TokenLessMessage }),
                    Encoding.UTF8, "application/json") };
            }

            var answer = accountExists
                ? JsonSerializer.Serialize(new { userProvisioned = false, setupToken = (string?)null, setupExpiresAtUtc = (DateTime?)null, message = "account_exists" })
                : JsonSerializer.Serialize(new { userProvisioned = true, setupToken = "stage-d-fix1-" + Guid.NewGuid().ToString("N"), setupExpiresAtUtc = DateTime.UtcNow.AddDays(7), message = "processed" });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class StageDEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.Platform.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private InviteTenantAdminUserCommandHandler OperatorHandler(
        Tenant tenant, AdminUserInvitationService invitations, ITenantRegistryRepository? tenants = null,
        Microsoft.Extensions.Logging.ILogger<InviteTenantAdminUserCommandHandler>? logger = null) => new(
        tenants ?? new OneTenant(tenant),
        Recorder<ICurrentUserContext>.Create((name, _) => name == "get_ActorName" ? "operator@platform.test" : null),
        invitations,
        Recorder<IQuotaService>.Create((name, _) => throw new InvalidOperationException("the quota is not asked here: " + name)),
        new StageDEnvironment("Production"),
        logger ?? NullLogger<InviteTenantAdminUserCommandHandler>.Instance);

    private sealed class RecordingLedger : ITenantAdminInvitationLedger
    {
        public List<(Guid TenantId, string Email, string Reason, Guid DispatchId)> Calls { get; } = [];

        public Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, Guid dispatchId, CancellationToken ct)
        {
            Calls.Add((tenantId, adminEmail, reasonCode, dispatchId));
            return Task.CompletedTask;
        }
    }

    /// <summary>One tenant, by reference: what the code under test writes is what the test reads.</summary>
    private sealed class OneTenant(Tenant tenant) : ITenantRegistryRepository
    {
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(id == tenant.Id ? tenant : null);
        public Task UpdateAsync(Tenant t, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Tenant> CreateAsync(Tenant t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
