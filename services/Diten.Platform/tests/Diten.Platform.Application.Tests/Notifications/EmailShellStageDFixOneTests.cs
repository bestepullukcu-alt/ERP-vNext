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
    [InlineData(AdminInvitationTrigger.Operator, "operator-invite")]
    [InlineData(AdminInvitationTrigger.TenantCreatedEvent, "tenant-created-event")]
    public async Task The_invitation_tells_AuthService_who_is_asking(AdminInvitationTrigger trigger, string wire)
    {
        var auth = new ScriptedAuth(accountExists: false);
        await Invitations(auth, "Production", "https://app.example.test").InviteAsync(StageDTenant(), StageDAdmin(), trigger, CancellationToken.None);

        Assert.Equal(wire, Assert.Single(auth.Triggers));
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
        var handler = new InviteTenantAdminUserCommandHandler(
            new OneTenant(tenant),
            Recorder<ICurrentUserContext>.Create((name, _) => name == "get_ActorName" ? "operator@platform.test" : null),
            Invitations(auth, "Production", "http://localhost:5001"),
            Recorder<IQuotaService>.Create((name, _) => throw new InvalidOperationException("the quota is not asked here: " + name)),
            new StageDEnvironment("Production"),
            NullLogger<InviteTenantAdminUserCommandHandler>.Instance);

        var response = await handler.Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.Equal(0, auth.Calls);                       // nothing reset, nothing created
        Assert.False(response.IsSuccessful);
        Assert.Equal(422, response.StatusCode);
        Assert.Equal(TenantAdminSetPasswordLink.ReasonRootLoopback, response.ReasonCode);
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
                NullLogger<EmailDispatchJob>.Instance, rig.Templates, new EmailTemplateRenderer(), rig.Composer, null, ledger)
            .HandleAsync(new EmailDispatchJobArgs(rig.TenantId, row.Id), new BackgroundJobContext(), CancellationToken.None);

        var (tenantId, email, reason) = Assert.Single(ledger.Calls);
        Assert.Equal(rig.TenantId, tenantId);
        Assert.Equal(row.To[0].Email, email);
        Assert.Equal(EmailDispatchJob.ReasonActionLinkNotRetryable, reason);
    }

    [Fact]
    public async Task The_ledger_writes_the_failed_step_and_tells_the_operator_to_invite_again()
    {
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        tenant.AdminUsers.Add(admin);
        tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = "admin-invitation", Label = "Initial Admin Invitation", Status = "Completed" });

        await new TenantAdminInvitationLedger(new OneTenant(tenant))
            .RecordUndeliveredAsync(tenant.Id, admin.Email, EmailDispatchJob.ReasonActionLinkNotRetryable, CancellationToken.None);

        var step = Assert.Single(tenant.ProvisioningSteps);
        Assert.Equal("Failed", step.Status);
        Assert.Contains("Invite", step.Detail);
        Assert.Contains(EmailDispatchJob.ReasonActionLinkNotRetryable, step.Detail);
        Assert.Contains(tenant.ActivityTimeline, e => e.EventType == "tenant.admin_user.invitation_undelivered");
    }

    // ---------------------------------------------------------------- helpers

    private static Tenant StageDTenant() => new()
    {
        Id = Guid.NewGuid(), Code = "S2DF", Slug = "s2df", Name = "s2df", DisplayName = "Stage D fix", Domain = "s2df.test",
        Region = "EU", Environment = "Production", DefaultLanguage = "en"
    };

    private static TenantAdminUser StageDAdmin() => new() { Id = Guid.NewGuid(), Name = "First Admin", Email = "first@tenant.test" };

    private static AdminUserInvitationService Invitations(ScriptedAuth auth, string environment, string root, IMediator? mediator = null) => new(
        new SingleClientFactory(auth),
        mediator ?? new DispatchRecorder(),
        Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.test", InternalApiKey = "stage-d-test-only-key", FrontendBaseUrl = root }),
        NullLogger<AdminUserInvitationService>.Instance,
        new StageDEnvironment(environment));

    /// <summary>AuthService's tenant-admin-invited door: records the trigger; answers a token, or "the account exists".</summary>
    private sealed class ScriptedAuth(bool accountExists) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string?> Triggers { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Triggers.Add(body.RootElement.TryGetProperty("trigger", out var t) ? t.GetString() : null);
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

    private sealed class RecordingLedger : ITenantAdminInvitationLedger
    {
        public List<(Guid TenantId, string Email, string Reason)> Calls { get; } = [];

        public Task RecordUndeliveredAsync(Guid tenantId, string adminEmail, string reasonCode, CancellationToken ct)
        {
            Calls.Add((tenantId, adminEmail, reasonCode));
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
