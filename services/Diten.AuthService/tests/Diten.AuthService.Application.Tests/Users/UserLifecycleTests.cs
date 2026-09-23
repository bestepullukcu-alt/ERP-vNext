using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-INVITED-LIFECYCLE-01 — the invited account's life, at handler level (hand-written fakes, codebase
/// convention). The real-Mongo and HTTP evidence is <see cref="UserLifecycleMongoTests"/>.
///
/// <list type="number">
/// <item>Status is DERIVED: Invited (password never set) · Inactive · Active, and Invited wins over IsActive.</item>
/// <item>No administrator activates an invited account — neither the kebab's enable nor the edit form's switch — and
/// the refusal carries the stable code USER_INVITATION_PENDING.</item>
/// <item>A taken live e-mail is 409 USER_EMAIL_TAKEN from the probe AND from the index race, never a 500.</item>
/// </list>
/// </summary>
public sealed class UserLifecycleTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // ── the derived status ──

    [Fact]
    public void A_fresh_invitation_is_Invited_not_Inactive()
    {
        var user = Invited();

        Assert.True(user.IsInvitationPending());
        Assert.False(user.IsActive);
        Assert.Equal(UserLifecycle.StatusInvited, UserLifecycle.StatusOf(user));
    }

    [Fact]
    public void An_invited_account_someone_already_activated_still_reads_Invited_because_it_cannot_sign_in()
    {
        var user = Invited();
        user.Activate(); // the measured bad state: IsActive=true, MustChangePassword=true, EmailConfirmed=false

        Assert.Equal(UserLifecycle.StatusInvited, UserLifecycle.StatusOf(user));
    }

    [Fact]
    public void Redeeming_the_link_ends_the_invitation()
    {
        var user = Invited();
        user.UpdatePassword("hash:real");
        user.ClearPasswordChangeRequirement();
        user.Activate();
        user.ConfirmEmail(); // exactly SetTenantPasswordCommandHandler's sequence

        Assert.False(user.IsInvitationPending());
        Assert.Equal(UserLifecycle.StatusActive, UserLifecycle.StatusOf(user));
    }

    [Fact]
    public void An_administrator_switched_off_account_is_Inactive()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        user.ConfirmEmail();
        user.Deactivate();

        Assert.Equal(UserLifecycle.StatusInactive, UserLifecycle.StatusOf(user));
    }

    [Fact]
    public void Look_alikes_are_not_invitations()
    {
        // Admin reset of a redeemed account: must change password again, but it HAS one (email confirmed).
        var reset = new User("r@acme.test", "hash:x", "R", "Eset", TenantA);
        reset.ConfirmEmail();
        reset.RequirePasswordChange(null);
        // A provisioned tenant admin: a temporary password + forced change, email confirmed (InternalEventsController).
        var provisioned = new User("p@acme.test", "hash:temp", "P", "Rov", TenantA);
        provisioned.ConfirmEmail();
        provisioned.RequirePasswordChange(DateTime.UtcNow.AddDays(1));
        // Self-service create: a password from the start, no forced change.
        var selfService = new User("s@acme.test", "hash:x", "S", "Elf", TenantA);

        Assert.All(new[] { reset, provisioned, selfService }, u =>
        {
            Assert.False(u.IsInvitationPending());
            Assert.Equal(UserLifecycle.StatusActive, UserLifecycle.StatusOf(u));
        });
    }

    [Fact]
    public void A_self_service_account_that_signed_in_and_then_got_an_admin_reset_is_not_an_invitation()
    {
        // CT guard (2026-09-23): the third fact. Self-service create never confirms the e-mail, and an admin reset
        // forces a change — so with the first two facts alone this account would read Invited, "Activate" would
        // vanish from the kebab and the enable door would answer 409. What tells it apart is that it has been used.
        var user = new User("u@acme.test", "hash:x", "U", "Sed", TenantA);
        user.RecordLoginSuccess();
        user.RequirePasswordChange(null); // AdminResetPasswordCommandHandler

        Assert.False(user.EmailConfirmed);
        Assert.True(user.MustChangePassword);
        Assert.False(user.IsInvitationPending());
        Assert.Equal(UserLifecycle.StatusActive, UserLifecycle.StatusOf(user));
    }

    // ── activation: both administrator doors refuse an invited account ──

    [Fact]
    public async Task Enable_on_an_invited_account_is_409_USER_INVITATION_PENDING_and_writes_nothing()
    {
        var user = Invited();
        var repo = new CountingUserRepository([user]);

        var result = await StatusHandler(repo).Handle(new SetUserActiveStatusCommand(user.Id, true), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(UserLifecycle.InvitationPendingCode, Assert.Single(result.ErrorCodes).Code);
        Assert.False(user.IsActive);
        Assert.Equal(0, repo.Writes);
    }

    [Fact]
    public async Task Enable_on_a_switched_off_normal_account_still_works()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        user.ConfirmEmail();
        user.Deactivate();

        var result = await StatusHandler(new CountingUserRepository([user])).Handle(new SetUserActiveStatusCommand(user.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Update_switching_an_invited_account_on_is_409_USER_INVITATION_PENDING_and_writes_nothing()
    {
        var user = Invited();
        var repo = new CountingUserRepository([user]);

        var result = await UpdateHandler(repo).Handle(new UpdateUserCommand(user.Id, "New", "Name", true), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(UserLifecycle.InvitationPendingCode, Assert.Single(result.ErrorCodes).Code);
        Assert.False(user.IsActive);
        Assert.Equal("In", user.FirstName); // the profile half was refused too
        Assert.Equal(0, repo.Writes);
    }

    [Theory]
    [InlineData(false, false)] // invited, switch off (the edit form hides the switch → posts false): plain save
    [InlineData(true, true)]   // invited but already (wrongly) active, re-sent as active: not a transition
    public async Task Update_that_does_not_switch_an_invited_account_on_is_saved(bool alreadyActive, bool requested)
    {
        var user = Invited();
        if (alreadyActive) user.Activate();
        var repo = new CountingUserRepository([user]);

        var result = await UpdateHandler(repo).Handle(new UpdateUserCommand(user.Id, "New", "Name", requested), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("New", user.FirstName);
        Assert.Equal(UserLifecycle.StatusInvited, result.Data!.Status);
        Assert.Equal(1, repo.Writes);
    }

    // ── the e-mail: probe and index race both answer 409 USER_EMAIL_TAKEN ──

    [Fact]
    public async Task Create_with_a_live_users_email_is_409_USER_EMAIL_TAKEN()
    {
        var live = new User("taken@acme.test", "hash:x", "L", "Ive", TenantA);
        var email = new FakeInvitationEmailService();

        var result = await CreateHandler(new CountingUserRepository([live]), email)
            .Handle(new CreateUserCommand("taken@acme.test", null, "N", "Ew"), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(UserLifecycle.EmailTakenCode, Assert.Single(result.ErrorCodes).Code);
        Assert.Empty(email.Sends);
    }

    [Theory]
    [InlineData(null)]          // invitation path
    [InlineData("Sup3rSecret!")] // self-service path
    public async Task Create_that_loses_the_index_race_is_409_USER_EMAIL_TAKEN_not_a_500(string? password)
    {
        var repo = new CountingUserRepository([]) { FailInsertWithDuplicateEmail = true };
        var email = new FakeInvitationEmailService();

        var result = await CreateHandler(repo, email)
            .Handle(new CreateUserCommand("race@acme.test", password, "R", "Ace"), CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(UserLifecycle.EmailTakenCode, Assert.Single(result.ErrorCodes).Code);
        Assert.Empty(email.Sends); // no invitation for an account that was never created
    }

    [Fact]
    public async Task A_new_invitation_answers_with_the_Invited_status()
    {
        var result = await CreateHandler(new CountingUserRepository([]), new FakeInvitationEmailService())
            .Handle(new CreateUserCommand("new@acme.test", null, "N", "Ew"), CancellationToken.None);

        Assert.Equal(201, result.StatusCode);
        Assert.Equal(UserLifecycle.StatusInvited, result.Data!.Status);
    }

    // ── source guard: every administrator activation in the Users feature asks the lifecycle rule ──

    [Fact]
    public void Every_Users_handler_that_activates_asks_RefusesActivation_first()
    {
        // The one legitimate activation of an invited account is its owner redeeming the link.
        const string redeem = "SetTenantPasswordCommandHandler.cs";
        var handlers = Directory.EnumerateFiles(Path.Combine(SrcRoot(), "Diten.AuthService.Application", "Features", "Users", "Handlers"), "*.cs", SearchOption.AllDirectories)
            .Select(f => (Name: Path.GetFileName(f), Body: WithoutComments(File.ReadAllText(f))))
            .Where(f => Regex.IsMatch(f.Body, @"\.Activate\s*\(\s*\)"))
            .ToArray();

        Assert.Contains(handlers, h => h.Name == "SetUserActiveStatusCommandHandler.cs");
        Assert.Contains(handlers, h => h.Name == "UpdateUserCommandHandler.cs");
        var unguarded = handlers
            .Where(h => h.Name != redeem && !h.Body.Contains("UserLifecycle.RefusesActivation(", StringComparison.Ordinal))
            .Select(h => h.Name)
            .ToArray();
        Assert.True(unguarded.Length == 0,
            "these Users handlers activate an account without asking UserLifecycle.RefusesActivation — an invited account "
            + "could be switched on and read Active while unable to sign in:\n" + string.Join("\n", unguarded));
    }

    // ── wiring ──

    private static User Invited()
    {
        // CreateUserCommandHandler.CreateByInvitationAsync's shape.
        var user = new User("in@acme.test", "hash:placeholder", "In", "Vited", TenantA);
        user.Deactivate();
        user.RequirePasswordChange(null);
        return user;
    }

    private static SetUserActiveStatusCommandHandler StatusHandler(IUserRepository repo)
        => new(repo, new NoRefreshTokens(), TenantContextFor(TenantA), NullLogger<SetUserActiveStatusCommandHandler>.Instance);

    private static UpdateUserCommandHandler UpdateHandler(IUserRepository repo)
        => new(repo, new NoRolesRepository(), TenantContextFor(TenantA), new AccountKindWriter(new NoAudit()), NullLogger<UpdateUserCommandHandler>.Instance);

    private static CreateUserCommandHandler CreateHandler(IUserRepository repo, FakeInvitationEmailService email)
        => new(repo, new FakePasswordHasher(), new FakePasswordPolicyService(), TenantContextFor(TenantA), new FakeTokenService(),
            new FakeRefreshTokenHasher(), new FakeHostEnvironment(), email, NullLogger<CreateUserCommandHandler>.Instance);

    private static ITenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    private static string SrcRoot()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location);
        while (directory is not null)
        {
            var sibling = Path.Combine(directory, "src", "Diten.AuthService.Application");
            if (Directory.Exists(sibling))
            {
                return Path.Combine(directory, "src");
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("services/Diten.AuthService/src was not found above the test assembly.");
    }

    private static string WithoutComments(string source)
    {
        var noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"//[^\r\n]*", string.Empty);
    }

    // ── fakes ──

    /// <summary>InMemoryUserRepository plus a write counter and an optional E11000-on-email insert failure.</summary>
    private sealed class CountingUserRepository(IEnumerable<User> users) : IUserRepository
    {
        private readonly InMemoryUserRepository _inner = new(users);
        public int Writes { get; private set; }
        public bool FailInsertWithDuplicateEmail { get; init; }

        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => _inner.GetByEmailAndTenantAsync(email, tenantId, ct);
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => _inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => _inner.GetByIdAndTenantAsync(id, tenantId, ct);
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => _inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => _inner.GetAllByTenantAsync(tenantId, page, pageSize, ct);
        public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct) => _inner.SearchActiveAsync(tenantId, term, limit, ct);
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => _inner.GetCountByTenantAsync(tenantId, ct);
        public Task<User> CreateAsync(User user, CancellationToken ct)
        {
            if (FailInsertWithDuplicateEmail)
            {
                throw new DuplicateUserEmailException(new InvalidOperationException("E11000 (simulated)"));
            }

            Writes++;
            return _inner.CreateAsync(user, ct);
        }
        public Task<User> UpdateAsync(User user, CancellationToken ct) { Writes++; return _inner.UpdateAsync(user, ct); }
        public Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct) { Writes++; return _inner.UpdateForTenantAsync(user, tenantId, ct); }
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => _inner.SoftDeleteAsync(id, tenantId, ct);
    }

    private sealed class NoRefreshTokens : IRefreshTokenRepository
    {
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NoRolesRepository : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }

    private sealed class NoAudit : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
    {
        public string Hash(string token) => "sha:" + token;
    }

    private sealed class FakeTokenService : ITokenService
    {
        private int _counter;
        public string GenerateRefreshToken() => "rt-" + Interlocked.Increment(ref _counter);
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw new NotSupportedException();
        public System.Security.Claims.ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw new NotSupportedException();
    }

    private sealed class FakePasswordPolicyService : IPasswordPolicyService
    {
        public Task ValidateTenantPasswordAsync(Guid tenantId, Guid? userId, string password, string context, CancellationToken ct) => Task.CompletedTask;
        public string GenerateTemporaryPassword(TenantLoginSettingsSnapshot settings) => throw new NotSupportedException();
    }

    private sealed class FakeInvitationEmailService : ITenantUserInvitationEmailService
    {
        public List<(string Email, string Token)> Sends { get; } = [];
        public string BuildTenantSetPasswordUrl(string email, string setupToken) => $"http://localhost:5001/account/set-password?email={email}&token={setupToken}";
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            Sends.Add((email, setupToken));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
