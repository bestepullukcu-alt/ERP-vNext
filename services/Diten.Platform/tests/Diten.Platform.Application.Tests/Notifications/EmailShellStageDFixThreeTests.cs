using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>BL-454 — WP-EMAIL-SHELL-01 slice 2, stage D fix round 3 (P-2026-10-07-EMAIL-S2D-FIX3), Platform side.</summary>
public sealed partial class EmailShellDispatchTests
{
    // ---------------------------------------------------------------- 1: the ledger, by identity, in production order

    [Fact]
    public async Task An_invitations_own_undeliverable_mail_marks_the_step_failed_in_production_order()
    {
        // The order production runs in: the invitation queues its dispatch FIRST, the step and the invitation time are
        // written AFTER (with "now"), and only then can the dispatch fail for good. A clock comparison never marked here.
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        tenant.AdminUsers.Add(admin);
        tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = TenantProvisioningStep.AdminInvitationKey, Label = "Initial Admin Invitation" });
        var mediator = new DispatchRecorder();

        var response = await OperatorHandler(tenant, Invitations(new ScriptedAuth(accountExists: false), "Production", "https://app.example.test", mediator))
            .Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);
        Assert.True(response.IsSuccessful);
        var dispatchId = Assert.Single(mediator.DispatchIds);
        Assert.Equal("Completed", Assert.Single(tenant.ProvisioningSteps).Status);
        Assert.Equal(dispatchId, admin.LastInvitationDispatchId);

        // … the e-mail job gives up on that dispatch (it hands the ledger the dispatch's id) …
        await new TenantAdminInvitationLedger(new OneTenant(tenant))
            .RecordUndeliveredAsync(tenant.Id, admin.Email, EmailDispatchJob.ReasonActionLinkNotRetryable, dispatchId, CancellationToken.None);

        var step = Assert.Single(tenant.ProvisioningSteps);
        Assert.Equal("Failed", step.Status);
        Assert.Contains(EmailDispatchJob.ReasonActionLinkNotRetryable, step.Detail);
        Assert.Contains(tenant.ActivityTimeline, e => e.EventType == "tenant.admin_user.invitation_undelivered");
    }

    [Fact]
    public async Task An_older_invitations_undeliverable_mail_leaves_a_newer_invitation_alone()
    {
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        tenant.AdminUsers.Add(admin);
        var mediator = new DispatchRecorder();
        var handler = OperatorHandler(tenant, Invitations(new ScriptedAuth(accountExists: false), "Production", "https://app.example.test", mediator));

        await handler.Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None); // the first link
        await handler.Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None); // "Invite" again
        Assert.Equal(2, mediator.DispatchIds.Count);

        // The FIRST dispatch fails for good — after the newer invitation was sent.
        await new TenantAdminInvitationLedger(new OneTenant(tenant))
            .RecordUndeliveredAsync(tenant.Id, admin.Email, EmailDispatchJob.ReasonActionLinkNotRetryable, mediator.DispatchIds[0], CancellationToken.None);

        Assert.Equal("Completed", Assert.Single(tenant.ProvisioningSteps, s => s.Key == TenantProvisioningStep.AdminInvitationKey).Status);
        Assert.DoesNotContain(tenant.ActivityTimeline, e => e.EventType == "tenant.admin_user.invitation_undelivered");
    }

    [Fact]
    public async Task A_record_from_before_the_dispatch_id_is_marked_so_a_locked_out_administrator_is_visible()
    {
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        admin.InvitedAt = DateTimeOffset.UtcNow;
        tenant.AdminUsers.Add(admin); // LastInvitationDispatchId absent
        tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = TenantProvisioningStep.AdminInvitationKey, Label = "Initial Admin Invitation", Status = "Completed" });

        await new TenantAdminInvitationLedger(new OneTenant(tenant))
            .RecordUndeliveredAsync(tenant.Id, admin.Email, EmailDispatchJob.ReasonActionLinkNotRetryable, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Failed", Assert.Single(tenant.ProvisioningSteps).Status);
    }

    // ---------------------------------------------------------------- 2: no token in the log

    [Fact]
    public async Task A_2xx_answer_that_cannot_be_read_is_never_written_to_the_log()
    {
        var token = "stage-d-fix3-" + Guid.NewGuid().ToString("N");
        var auth = new ScriptedAuth(accountExists: false)
        {
            OkBody = "{\"userProvisioned\":true,\"setup" + "Token\":\"" + token + "\",\"setupExpiresAtUtc\":\"not-a-date\",\"message\":\"processed\"}"
        };
        var logger = new LinesLogger<AdminUserInvitationService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Invitations(auth, "Production", "https://app.example.test", logger: logger)
            .InviteAsync(StageDTenant(), StageDAdmin(), AdminInvitationTrigger.TenantCreatedEvent, CancellationToken.None));

        Assert.NotEmpty(logger.Lines);
        Assert.DoesNotContain(logger.Lines, line => line.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Lines, line => line.Contains("not-a-date", StringComparison.Ordinal)); // no part of the body
        Assert.Contains(logger.Lines, line => line.Contains(AdminUserInvitationService.ReasonAuthAnswerUnreadable, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_answer_is_logged_short_and_with_its_token_masked()
    {
        var token = "stage-d-fix3-" + Guid.NewGuid().ToString("N");
        var auth = new ScriptedAuth(accountExists: false)
        {
            Status = HttpStatusCode.InternalServerError,
            Body = "{\"error\":\"boom\",\"setup" + "Token\":\"" + token + "\",\"detail\":\"" + new string('x', 600) + "\"}"
        };
        var logger = new LinesLogger<AdminUserInvitationService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Invitations(auth, "Production", "https://app.example.test", logger: logger)
            .InviteAsync(StageDTenant(), StageDAdmin(), AdminInvitationTrigger.TenantCreatedEvent, CancellationToken.None));

        var line = Assert.Single(logger.Lines, l => l.Contains("StatusCode=500", StringComparison.Ordinal));
        Assert.DoesNotContain(token, line, StringComparison.Ordinal);
        Assert.Contains("***", line, StringComparison.Ordinal);
        Assert.Contains("boom", line, StringComparison.Ordinal); // the reason is still there
        Assert.DoesNotContain(new string('x', AdminUserInvitationService.MaxLoggedBodyLength), line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"refreshToken\": \"abc.def\"}", "abc.def")]
    [InlineData("error: access_token=abc.def&x=1", "abc.def")]
    [InlineData("{\"setupToken\":\"a\\\"b\"}", "a\\\"b")]
    public void A_logged_error_body_masks_every_token_like_value(string body, string secret)
    {
        var logged = AdminUserInvitationService.LoggableErrorBody(body);

        Assert.DoesNotContain(secret, logged, StringComparison.Ordinal);
        Assert.Contains("***", logged, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- the operator's door

    [Fact]
    public async Task The_operators_door_answering_the_account_exists_is_a_refusal_by_name()
    {
        // Unreachable with the right AuthService (its operator door resets): defence, not a success.
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        tenant.AdminUsers.Add(admin);

        var response = await OperatorHandler(tenant, Invitations(new ScriptedAuth(accountExists: true), "Production", "https://app.example.test"))
            .Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(AdminInvitationRefusals.AccountExists, response.ReasonCode);
        Assert.Equal("Failed", Assert.Single(tenant.ProvisioningSteps, s => s.Key == TenantProvisioningStep.AdminInvitationKey).Status);
    }

    [Fact]
    public async Task A_step_that_cannot_be_written_is_an_error_and_the_operator_is_told()
    {
        var tenant = StageDTenant();
        var admin = StageDAdmin();
        admin.Status = TenantAdminUserStatus.Invited;
        tenant.AdminUsers.Add(admin);
        var logger = new LinesLogger<Diten.Platform.Application.Features.Tenants.Handlers.InviteTenantAdminUserCommandHandler>();

        var response = await OperatorHandler(
                tenant, Invitations(new ScriptedAuth(accountExists: false), "Production", "https://app.example.test"),
                new StepWriteFails(tenant), logger)
            .Handle(new InviteTenantAdminUserCommand(tenant.Id, admin.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful); // the invitation itself was done
        Assert.True(response.Data!.InvitationStepNotRecorded);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Line.Contains("step_write_failed", StringComparison.Ordinal));
    }

    /// <summary>The tenant reads and saves; only the invitation step's targeted write fails.</summary>
    private sealed class StepWriteFails(Tenant tenant) : ITenantRegistryRepository
    {
        public Task RecordAdminInvitationAsync(
            Guid tenantId, Guid? adminUserId, string stepKey, string stepStatus, string detail, DateTimeOffset at,
            bool stampInvitedAt, Guid? invitationDispatchId, TenantActivityEvent activity, CancellationToken ct = default) =>
            throw new InvalidOperationException("the step write fails here");

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
