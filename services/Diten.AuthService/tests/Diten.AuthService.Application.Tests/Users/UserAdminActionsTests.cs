using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Diten.AuthService.Application.Tests.Testing;

namespace Diten.AuthService.Application.Tests.Users;

// MOD-0018 Users Admin actions: enable/disable (with refresh revoke on disable) and admin reset
// (active → force-change; pending → 409). Mirrors the invitation test fakes (hand-written, no Moq).
public sealed class UserAdminActionsTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // ── Disable / Enable ──
    [Fact]
    public async Task Disable_deactivates_and_revokes_refresh_tokens()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA); // active
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = StatusHandler(new InMemoryUserRepository([user]), refreshTokens);

        var result = await handler.Handle(new SetUserActiveStatusCommand(user.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.False(user.IsActive);
        Assert.Equal((user.Id, TenantA), refreshTokens.RevokeAllCall); // sessions terminated, tenant-scoped
    }

    [Fact]
    public async Task Enable_activates_without_revoking()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        user.Deactivate();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = StatusHandler(new InMemoryUserRepository([user]), refreshTokens);

        var result = await handler.Handle(new SetUserActiveStatusCommand(user.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(user.IsActive);
        Assert.Null(refreshTokens.RevokeAllCall); // enabling never revokes
    }

    [Fact]
    public async Task Disable_then_enable_round_trip_revokes_only_on_disable()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = StatusHandler(new InMemoryUserRepository([user]), refreshTokens);

        await handler.Handle(new SetUserActiveStatusCommand(user.Id, false), CancellationToken.None);
        Assert.False(user.IsActive);
        Assert.Equal(1, refreshTokens.RevokeAllCount);

        await handler.Handle(new SetUserActiveStatusCommand(user.Id, true), CancellationToken.None);
        Assert.True(user.IsActive);
        Assert.Equal(1, refreshTokens.RevokeAllCount); // still 1 — enable did not revoke
    }

    [Fact]
    public async Task SetActiveStatus_user_not_found_returns_404()
    {
        var handler = StatusHandler(new InMemoryUserRepository([]), new FakeRefreshTokenRepository());

        var result = await handler.Handle(new SetUserActiveStatusCommand(Guid.NewGuid(), false), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Admin reset ──
    [Fact]
    public async Task AdminReset_for_active_user_issues_token_and_forces_change()
    {
        var hasher = new FakeRefreshTokenHasher();
        var user = new User("active@acme.test", "hash:x", "A", "C", TenantA); // active, no MustChangePassword
        var email = new FakeInvitationEmailService();
        var handler = ResetHandler(new InMemoryUserRepository([user]), email, hasher);

        var before = DateTime.UtcNow;
        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.SetupUrl)); // dev: copyable link surfaced
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordResetTokenHash)); // token issued
        Assert.InRange(user.PasswordResetTokenExpiresAt!.Value, before.AddDays(7).AddMinutes(-2), before.AddDays(7).AddMinutes(2));
        Assert.True(user.MustChangePassword); // force-change applied
        Assert.Single(email.Sends);
    }

    [Fact]
    public async Task AdminReset_for_pending_user_conflicts_and_leaves_token_untouched()
    {
        var hasher = new FakeRefreshTokenHasher();
        var user = new User("pending@acme.test", "hash:x", "P", "C", TenantA);
        user.SetPasswordResetToken(hasher.Hash("orig"), DateTime.UtcNow.AddDays(3));
        user.RequirePasswordChange(null); // pending invitation
        var email = new FakeInvitationEmailService();
        var handler = ResetHandler(new InMemoryUserRepository([user]), email, hasher);

        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(hasher.Hash("orig"), user.PasswordResetTokenHash); // unchanged
        Assert.Empty(email.Sends);
    }

    [Fact]
    public async Task AdminReset_user_not_found_returns_404()
    {
        var handler = ResetHandler(new InMemoryUserRepository([]), new FakeInvitationEmailService(), new FakeRefreshTokenHasher());

        var result = await handler.Handle(new AdminResetPasswordCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Builders ──
    private static SetUserActiveStatusCommandHandler StatusHandler(InMemoryUserRepository repo, FakeRefreshTokenRepository refreshTokens) =>
        new(repo, refreshTokens, TenantContextFor(TenantA), new SomeoneElse(), UserAuditForTests.None(), new RecordingUserQuotaClient(), NullLogger<SetUserActiveStatusCommandHandler>.Instance);

    /// <summary>The signed-in administrator is never one of the rows these tests act on (the self-deactivation
    /// refusal has its own file, <c>SetUserActiveStatusGuardTests</c>).</summary>
    private sealed class SomeoneElse : ICurrentUserAccessor
    {
        public Guid? UserId { get; } = Guid.NewGuid();
    }

    private static AdminResetPasswordCommandHandler ResetHandler(InMemoryUserRepository repo, FakeInvitationEmailService email, FakeRefreshTokenHasher hasher) =>
        new(repo, TenantContextFor(TenantA), new FakeTokenService(), hasher, email, new FakeHostEnvironment(isDevelopment: true), UserAuditForTests.None(), NullLogger<AdminResetPasswordCommandHandler>.Instance,
            new ResetHasher(), new FakeRefreshTokenRepository(), new SomeoneElse());

    private static TestTenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TestTenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    // ── Inline fakes ──
    private sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
    }

    private sealed class FakeTokenService : ITokenService
    {
        private int _counter;
        public string GenerateRefreshToken() => "rt-" + System.Threading.Interlocked.Increment(ref _counter);
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw new NotSupportedException();
        public System.Security.Claims.ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
    }

    private sealed class FakeInvitationEmailService : ITenantUserInvitationEmailService
    {
        public List<(string email, string token)> Sends { get; } = [];
        public string BuildTenantSetPasswordUrl(string email, string setupToken) =>
            $"http://localhost:5001/account/set-password?email={email}&token={setupToken}";
        // BL-454 — no default on the interface any more: this double records a reset exactly like an invitation.
        public Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct) =>
            SendTenantUserInvitationAsync(email, setupToken, ct);
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            Sends.Add((email, setupToken));
            return Task.CompletedTask;
        }
    }

    /// <summary>BL-529 — the reset handler hashes a random secret into an unusable password; any stable hash will do.</summary>
    private sealed class ResetHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public (Guid userId, Guid tenantId)? RevokeAllCall { get; private set; }
        public int RevokeAllCount { get; private set; }
        public Task<long> RevokeLiveSessionsAsync(Guid userId, Guid tenantId, string reason, CancellationToken ct) => RevokeAllByUserAsync(userId, tenantId, ct);
        public Task<bool> TryRotateAsync(Guid tokenId, string replacedByTokenHash, string? revokedByIp, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> RevokeIfLiveAsync(string token, string reason, CancellationToken ct) => Task.FromResult(true);
        /// <summary>BL-529 — how many live sessions the revoke reports ending.</summary>
        public long Revoked { get; set; }
        public Action? OnRevoke { get; set; }
        public Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            OnRevoke?.Invoke();
            RevokeAllCall = (userId, tenantId);
            RevokeAllCount++;
            return Task.FromResult(Revoked);
        }
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeHostEnvironment(bool isDevelopment) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? "Development" : "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => SetTenant(targetTenantId);
    }

    // ── BL-529: the reset's conditional write, its audit row in every case, and no reset of oneself ──

    [Fact]
    public async Task AdminReset_reads_again_when_the_password_changed_between_its_read_and_its_write()
    {
        var user = new User("race@acme.test", "hash:x", "Ra", "Ce", TenantA);
        var repo = new InMemoryUserRepository([user]) { PasswordChangedConflicts = 1 };
        var audit = new RecordingUserAudit();
        var handler = ResetHandlerWith(repo, audit, new FakeRefreshTokenRepository(), new SomeoneElse());

        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, repo.ConditionalWrites); // refused once (stale read), written on the second read
        Assert.Equal("reset", Assert.Single(audit.Rows).Metadata["outcome"]);
    }

    [Fact]
    public async Task AdminReset_whose_account_keeps_changing_is_refused_with_a_code_and_audited()
    {
        var user = new User("race@acme.test", "hash:x", "Ra", "Ce", TenantA);
        var repo = new InMemoryUserRepository([user]) { PasswordChangedConflicts = AdminPasswordReset.MaxAttempts };
        var audit = new RecordingUserAudit();
        var handler = ResetHandlerWith(repo, audit, new FakeRefreshTokenRepository(), new SomeoneElse());

        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == UserErrorCodes.ResetConflict);
        Assert.Equal(AdminPasswordReset.MaxAttempts, repo.ConditionalWrites);
        Assert.Equal("conflict", Assert.Single(audit.Rows).Metadata["outcome"]);
    }

    [Fact]
    public async Task AdminReset_whose_account_write_fails_is_still_audited_and_ends_no_session()
    {
        var user = new User("down@acme.test", "hash:x", "Do", "Wn", TenantA);
        var repo = new InMemoryUserRepository([user]) { ThrowOnConditionalWrite = true };
        var audit = new RecordingUserAudit();
        var refreshTokens = new FakeRefreshTokenRepository { Revoked = 2 };
        var handler = ResetHandlerWith(repo, audit, refreshTokens, new SomeoneElse());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None));

        // FIX2 — the sessions are swept only after the password is stored: a failed write ends none, and says so.
        var row = Assert.Single(audit.Rows);
        Assert.Equal(UserAuditEvents.PasswordResetByAdmin, row.EventName);
        Assert.Equal(0L, row.Metadata["sessionsRevoked"]);
        Assert.Equal("failed", row.Metadata["outcome"]);
        Assert.Equal(0, refreshTokens.RevokeAllCount);
        Assert.False(Assert.Single(audit.Succeeded));
    }

    [Fact]
    public async Task AdminReset_of_ones_own_account_is_refused_before_anything_is_written()
    {
        var self = new User("me@acme.test", "hash:x", "M", "E", TenantA);
        var repo = new InMemoryUserRepository([self]);
        var audit = new RecordingUserAudit();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = ResetHandlerWith(repo, audit, refreshTokens, new SignedIn(self.Id));

        var result = await handler.Handle(new AdminResetPasswordCommand(self.Id), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == AdminResetPasswordCommandHandler.SelfResetCode);
        Assert.Equal("hash:x", self.PasswordHash);
        Assert.Equal(0, refreshTokens.RevokeAllCount);
        Assert.Equal(0, repo.ConditionalWrites);
        Assert.Empty(audit.Rows);
    }

    private static AdminResetPasswordCommandHandler ResetHandlerWith(
        InMemoryUserRepository repo, IUserAuditRecorder audit, FakeRefreshTokenRepository refreshTokens, ICurrentUserAccessor currentUser) =>
        new(repo, TenantContextFor(TenantA), new FakeTokenService(), new FakeRefreshTokenHasher(), new FakeInvitationEmailService(),
            new FakeHostEnvironment(isDevelopment: true), audit, NullLogger<AdminResetPasswordCommandHandler>.Instance,
            new ResetHasher(), refreshTokens, currentUser);

    private sealed class SignedIn(Guid id) : ICurrentUserAccessor
    {
        public Guid? UserId => id;
    }

    private sealed class RecordingUserAudit : IUserAuditRecorder
    {
        public List<(string EventName, IReadOnlyDictionary<string, object?> Metadata)> Rows { get; } = [];
        public List<bool> Succeeded { get; } = [];
        public List<bool> CancelledTokens { get; } = [];

        public Task RecordAsync(string eventName, Guid tenantId, Guid targetUserId, IReadOnlyDictionary<string, object?> metadata, CancellationToken ct = default)
            => RecordAsync(eventName, tenantId, targetUserId, metadata, succeeded: true, ct);

        public Task RecordAsync(string eventName, Guid tenantId, Guid targetUserId, IReadOnlyDictionary<string, object?> metadata, bool succeeded, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); // a real store refuses a cancelled token
            Rows.Add((eventName, metadata));
            Succeeded.Add(succeeded);
            CancelledTokens.Add(ct.IsCancellationRequested);
            return Task.CompletedTask;
        }
    }

    // ── BL-529 FIX2 item 6: the audit row outlives a cancelled request; the central log gets the real outcome ──

    [Fact]
    public async Task AdminReset_cancelled_after_its_write_still_writes_the_audit_row()
    {
        var user = new User("cancel@acme.test", "hash:x", "Ca", "Ncel", TenantA);
        var audit = new RecordingUserAudit();
        using var cts = new CancellationTokenSource();
        var handler = new AdminResetPasswordCommandHandler(new InMemoryUserRepository([user]), TenantContextFor(TenantA), new FakeTokenService(),
            new FakeRefreshTokenHasher(), new CancellingInvitationEmail(cts), new FakeHostEnvironment(isDevelopment: false), audit,
            NullLogger<AdminResetPasswordCommandHandler>.Instance, new ResetHasher(), new FakeRefreshTokenRepository { Revoked = 1 }, new SomeoneElse());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new AdminResetPasswordCommand(user.Id), cts.Token));

        var row = Assert.Single(audit.Rows);
        Assert.Equal(1L, row.Metadata["sessionsRevoked"]);
        Assert.False(Assert.Single(audit.CancelledTokens));
    }

    [Fact]
    public async Task AdminReset_reports_a_conflict_as_failed_and_a_reset_as_succeeded()
    {
        var conflicted = new User("c@acme.test", "hash:x", "C", "C", TenantA);
        var conflictAudit = new RecordingUserAudit();
        await ResetHandlerWith(new InMemoryUserRepository([conflicted]) { PasswordChangedConflicts = AdminPasswordReset.MaxAttempts },
            conflictAudit, new FakeRefreshTokenRepository(), new SomeoneElse()).Handle(new AdminResetPasswordCommand(conflicted.Id), CancellationToken.None);
        Assert.False(Assert.Single(conflictAudit.Succeeded));

        var reset = new User("r@acme.test", "hash:x", "R", "R", TenantA);
        var resetAudit = new RecordingUserAudit();
        await ResetHandlerWith(new InMemoryUserRepository([reset]), resetAudit, new FakeRefreshTokenRepository(), new SomeoneElse())
            .Handle(new AdminResetPasswordCommand(reset.Id), CancellationToken.None);
        Assert.True(Assert.Single(resetAudit.Succeeded));
    }

    [Fact]
    public async Task The_user_audit_recorder_forwards_the_real_outcome()
    {
        var forwarder = new RecordingPlatformAuditForwarder();
        var recorder = UserAuditForTests.Over(new NoRbac(), forwarder);

        await recorder.RecordAsync(UserAuditEvents.PasswordResetByAdmin, TenantA, Guid.NewGuid(), new Dictionary<string, object?>(), succeeded: false, CancellationToken.None);
        await recorder.RecordAsync(UserAuditEvents.PasswordResetByAdmin, TenantA, Guid.NewGuid(), new Dictionary<string, object?>(), CancellationToken.None);

        Assert.Equal([UserAuditEvents.OutcomeFailed, UserAuditEvents.OutcomeSucceeded], forwarder.Events.Select(e => e.Outcome).ToArray());
    }

    [Fact]
    public async Task The_reset_scans_the_sessions_only_after_its_write()
    {
        var user = new User("order@acme.test", "hash:x", "Or", "Der", TenantA);
        var log = new List<string>();
        var repo = new InMemoryUserRepository([user]) { OnConditionalWrite = () => log.Add("write") };
        var refreshTokens = new FakeRefreshTokenRepository { OnRevoke = () => log.Add("scan") };

        await ResetHandlerWith(repo, new RecordingUserAudit(), refreshTokens, new SomeoneElse()).Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.Equal(["write", "scan"], log);
    }

    [Fact]
    public async Task The_user_audit_recorder_never_throws_when_its_local_write_fails()
    {
        // BL-529 FIX3 — the row is written in AdminPasswordReset's finally, after the reset stood: a failing local write
        // must not turn a completed reset into a 500.
        var forwarder = new RecordingPlatformAuditForwarder();
        var recorder = UserAuditForTests.Over(new ThrowingRbac(), forwarder);

        await recorder.RecordAsync(UserAuditEvents.PasswordResetByAdmin, TenantA, Guid.NewGuid(), new Dictionary<string, object?>(), CancellationToken.None);

        Assert.Single(forwarder.Events); // the central log still gets it
    }

    private sealed class ThrowingRbac : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
            => throw new InvalidOperationException("authAuditLogs is down");
    }

    [Fact]
    public async Task AdminReset_is_decided_against_the_stored_account_not_the_callers_copy()
    {
        // BL-529 FIX4 — the double decides like the store: another administrator deactivated the account after this reset
        // read it; the write is refused and the reset stops as a conflict (nothing mailed).
        var user = new User("stored@acme.test", "hash:x", "St", "Ored", TenantA);
        var repo = new InMemoryUserRepository([user]);
        repo.ChangeStored(user.Id, isActive: false, deactivatedByAdministrator: true);
        var email = new FakeInvitationEmailService();
        var handler = new AdminResetPasswordCommandHandler(repo, TenantContextFor(TenantA), new FakeTokenService(), new FakeRefreshTokenHasher(),
            email, new FakeHostEnvironment(isDevelopment: true), new RecordingUserAudit(), NullLogger<AdminResetPasswordCommandHandler>.Instance,
            new ResetHasher(), new FakeRefreshTokenRepository(), new SomeoneElse());

        var result = await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Empty(email.Sends);
    }

    private sealed class NoRbac : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CancellingInvitationEmail(CancellationTokenSource cts) : ITenantUserInvitationEmailService
    {
        public string BuildTenantSetPasswordUrl(string email, string setupToken) => "http://link";
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            cts.Cancel(); // the caller gives up after the reset was written
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        // BL-454 (lane) — a reset is sent in its own words; the same cancellation behaviour.
        public Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct)
            => SendTenantUserInvitationAsync(email, setupToken, ct);
    }

    // ── CT acceptance, WP-AUTH-PLATFORM-LINKS-01 (2026-09-25) ────────────────────────────────────────────────────

    [Fact]
    public async Task An_admin_reset_whose_email_fails_in_production_is_still_audited()
    {
        var user = new User("reset@acme.test", "hash:x", "Re", "Set", TenantA);
        var repo = new InMemoryUserRepository([user]);
        var local = new EventNames();
        var handler = new AdminResetPasswordCommandHandler(repo, TenantContextFor(TenantA), new FakeTokenService(), new FakeRefreshTokenHasher(),
            new ThrowingInvitationEmail(), new FakeHostEnvironment(isDevelopment: false), UserAuditForTests.Over(local),
            NullLogger<AdminResetPasswordCommandHandler>.Instance, new ResetHasher(), new FakeRefreshTokenRepository(), new SomeoneElse());

        await Assert.ThrowsAnyAsync<Exception>(() => handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None));

        Assert.Contains(UserAuditEvents.PasswordResetByAdmin, local.Names);
    }

    [Fact]
    public async Task An_admin_reset_asks_for_the_reset_mail_and_never_for_the_invitation()
    {
        // BL-454 — the purpose is stated by the caller: the e-mail service is not left to guess it from the record.
        var user = new User("reset@acme.test", "hash:x", "Re", "Set", TenantA);
        user.ConfirmEmail();
        var mail = new PurposeRecordingEmail();
        var handler = new AdminResetPasswordCommandHandler(new InMemoryUserRepository([user]), TenantContextFor(TenantA), new FakeTokenService(), new FakeRefreshTokenHasher(),
            mail, new FakeHostEnvironment(isDevelopment: false), UserAuditForTests.Over(new EventNames()),
            NullLogger<AdminResetPasswordCommandHandler>.Instance, new ResetHasher(), new FakeRefreshTokenRepository(), new SomeoneElse());

        await handler.Handle(new AdminResetPasswordCommand(user.Id), CancellationToken.None);

        Assert.Equal(["reset"], mail.Purposes);
    }

    private sealed class PurposeRecordingEmail : ITenantUserInvitationEmailService
    {
        public List<string> Purposes { get; } = [];
        public string BuildTenantSetPasswordUrl(string email, string setupToken) => "http://localhost/set-password";
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct) { Purposes.Add("invitation"); return Task.CompletedTask; }
        public Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct) { Purposes.Add("reset"); return Task.CompletedTask; }
    }

    private sealed class EventNames : IRbacAuditRecorder
    {
        public List<string> Names { get; } = [];
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) { Names.Add(eventName); return Task.CompletedTask; }
    }

    private sealed class ThrowingInvitationEmail : ITenantUserInvitationEmailService
    {
        public string BuildTenantSetPasswordUrl(string email, string setupToken) => "http://localhost/set-password";
        // BL-454 — no default on the interface any more: this double fails a reset exactly like an invitation.
        public Task SendTenantUserPasswordResetAsync(string email, string setupToken, CancellationToken ct) =>
            SendTenantUserInvitationAsync(email, setupToken, ct);
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct) => throw new InvalidOperationException("SMTP down");
    }
}
