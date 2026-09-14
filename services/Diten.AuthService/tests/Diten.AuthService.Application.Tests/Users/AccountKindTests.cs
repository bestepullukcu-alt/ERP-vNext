using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Validators;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — the entity, the two commands and the classification right, at handler level.
/// Hand-written fakes (codebase convention). The HTTP-level behaviour is AccountKindEndpointTests.
/// </summary>
public sealed class AccountKindTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // ── the entity ──

    [Fact]
    public void A_new_user_is_Unknown_by_construction()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);

        Assert.Equal(AccountKind.Unknown, user.AccountKind);
        Assert.Equal(0, (int)AccountKind.Unknown); // legacy documents without the field read as Unknown
    }

    [Fact]
    public void SetAccountKind_changes_the_kind_and_stamps_UpdatedAt()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        var before = user.UpdatedAt;

        user.SetAccountKind(AccountKind.Service);

        Assert.Equal(AccountKind.Service, user.AccountKind);
        Assert.NotNull(user.UpdatedAt);
        Assert.NotEqual(before, user.UpdatedAt);
    }

    // MOD0024-TASK-READ-ACCESS-01 (BL-349, owner decision 2026-09-13) added platform.tasks.read-all as the
    // set's third owner-decided key — count and membership below updated to match; the two original assertions
    // are unchanged otherwise.
    // WP-PSS-MOD0024-BL392-WORK-REPORT-READ-EXPLICIT-01 (BL-392, owner decision 2026-09-14) added
    // platform.tasks.work-report.read-tenant-wide as the fourth — same treatment.
    [Fact]
    public void The_explicit_grant_only_set_is_exactly_the_four_owner_decided_keys()
    {
        Assert.True(ExplicitGrantOnlyPermissions.Keys.Contains("auth.users.account-kind.manage"));
        Assert.True(ExplicitGrantOnlyPermissions.Keys.Contains("AUTH.USERS.ACCOUNT-KIND.MANAGE")); // case-insensitive, like the catalog
        Assert.True(ExplicitGrantOnlyPermissions.Keys.Contains("ppm.portfolios.assign-owner"));
        Assert.True(ExplicitGrantOnlyPermissions.Keys.Contains("platform.tasks.read-all"));
        Assert.True(ExplicitGrantOnlyPermissions.Keys.Contains("platform.tasks.work-report.read-tenant-wide"));
        Assert.Equal(4, ExplicitGrantOnlyPermissions.Keys.Count);
        Assert.False(ExplicitGrantOnlyPermissions.Keys.Contains("auth.users.lookup")); // lookup is an ORDINARY tenant key
    }

    // ── SetAccountKindCommand: validator ──

    [Theory]
    [InlineData("Human")]
    [InlineData("human")]
    [InlineData("SERVICE")]
    [InlineData(" Unknown ")]
    public void Set_validator_accepts_any_casing_of_an_enum_name(string kind)
    {
        var result = new SetAccountKindCommandValidator().Validate(new SetAccountKindCommand(Guid.NewGuid(), kind));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Theory]
    [InlineData("Robot")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")] // a number parses with Enum.TryParse but is not a NAME — refused
    [InlineData("Human,Service")]
    public void Set_validator_refuses_anything_that_is_not_an_enum_name(string kind)
    {
        var result = new SetAccountKindCommandValidator().Validate(new SetAccountKindCommand(Guid.NewGuid(), kind));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == SetAccountKindCommandValidator.KindMessage);
    }

    [Fact]
    public void Set_validator_requires_a_user_id()
    {
        var result = new SetAccountKindCommandValidator().Validate(new SetAccountKindCommand(Guid.Empty, "Human"));

        Assert.False(result.IsValid);
    }

    // ── SetAccountKindCommand: handler ──

    [Fact]
    public async Task Set_missing_and_cross_tenant_targets_get_the_identical_404()
    {
        var foreign = new User("f@acme.test", "hash:x", "F", "Oreign", TenantB);
        foreign.SetAccountKind(AccountKind.Human);
        var audit = new CapturingAudit();
        var handler = SetHandler(new InMemoryUserRepository([foreign]), audit);

        var missing = await handler.Handle(new SetAccountKindCommand(Guid.NewGuid(), "Human"), CancellationToken.None);
        var crossTenant = await handler.Handle(new SetAccountKindCommand(foreign.Id, "Human"), CancellationToken.None);

        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(404, crossTenant.StatusCode);
        Assert.Equal(missing.Errors, crossTenant.Errors);
        Assert.Equal(missing.ErrorCodes, crossTenant.ErrorCodes);
        Assert.Null(missing.Data);
        Assert.Null(crossTenant.Data);
        Assert.Equal(AccountKind.Human, foreign.AccountKind); // untouched
        Assert.Empty(audit.Records);
    }

    [Fact]
    public async Task Set_changes_the_kind_persists_tenant_scoped_and_audits_old_to_new_with_correlation()
    {
        var user = new User("u@acme.test", "hash:x", "Umut", "Kaya", TenantA);
        var repo = new TrackingUserRepository([user]);
        var audit = new CapturingAudit();
        var handler = SetHandler(repo, audit);

        var result = await handler.Handle(new SetAccountKindCommand(user.Id, "human", "corr-123"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Human", result.Data!.AccountKind);
        Assert.Equal(user.Id, result.Data.UserId);
        Assert.True(result.Data.Active);
        Assert.Equal(AccountKind.Human, user.AccountKind);
        Assert.Equal((user.Id, TenantA), repo.UpdatedForTenant); // tenant-scoped write, not the unscoped one

        var record = Assert.Single(audit.Records);
        Assert.Equal(SetAccountKindCommandHandler.AuditEventName, record.EventName);
        Assert.Equal(TenantA, record.TenantId);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(record.Metadata));
        var m = doc.RootElement;
        Assert.Equal(user.Id.ToString(), m.GetProperty("targetUserId").GetString());
        Assert.Equal(TenantA.ToString(), m.GetProperty("tenantId").GetString());
        Assert.Equal("Unknown", m.GetProperty("previousKind").GetString());
        Assert.Equal("Human", m.GetProperty("newKind").GetString());
        Assert.Equal("corr-123", m.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Set_to_the_same_kind_is_idempotent_and_writes_no_audit()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        user.SetAccountKind(AccountKind.Service);
        var repo = new TrackingUserRepository([user]);
        var audit = new CapturingAudit();
        var handler = SetHandler(repo, audit);

        var result = await handler.Handle(new SetAccountKindCommand(user.Id, "Service"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Service", result.Data!.AccountKind);
        Assert.Null(repo.UpdatedForTenant);
        Assert.Empty(audit.Records);
    }

    [Fact]
    public async Task Set_handler_refuses_an_undefined_kind_even_if_the_validator_were_bypassed()
    {
        var user = new User("u@acme.test", "hash:x", "U", "Ser", TenantA);
        var handler = SetHandler(new InMemoryUserRepository([user]), new CapturingAudit());

        var result = await handler.Handle(new SetAccountKindCommand(user.Id, "Robot"), CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(AccountKind.Unknown, user.AccountKind);
    }

    // ── CreateUserCommand: the separate classification right ──

    [Fact]
    public async Task Create_with_a_kind_but_without_the_manage_right_is_403_PERM_DENIED_and_creates_nothing()
    {
        var repo = new InMemoryUserRepository([]);
        var email = new FakeInvitationEmailService();
        var handler = CreateHandler(repo, email);

        var result = await handler.Handle(
            new CreateUserCommand("x@acme.test", null, "X", "Y", AccountKind: "Human", CallerCanManageAccountKind: false),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(CreateUserCommandHandler.PermissionDeniedCode, Assert.Single(result.ErrorCodes).Code);
        Assert.Empty(await repo.GetAllByTenantAsync(TenantA, 1, 50, CancellationToken.None));
        Assert.Empty(email.Sends);
    }

    [Fact]
    public async Task Create_with_a_kind_and_the_manage_right_classifies_the_new_account()
    {
        var repo = new InMemoryUserRepository([]);
        var handler = CreateHandler(repo, new FakeInvitationEmailService());

        var result = await handler.Handle(
            new CreateUserCommand("s@acme.test", null, "S", "V", AccountKind: "service", CallerCanManageAccountKind: true),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Service", result.Data!.AccountKind);
        var created = Assert.Single(await repo.GetAllByTenantAsync(TenantA, 1, 50, CancellationToken.None));
        Assert.Equal(AccountKind.Service, created.AccountKind);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("   ", false)]
    public async Task Create_without_a_kind_is_Unknown_whatever_the_callers_rights(string? kind, bool canManage)
    {
        var repo = new InMemoryUserRepository([]);
        var handler = CreateHandler(repo, new FakeInvitationEmailService());

        var result = await handler.Handle(
            new CreateUserCommand("u@acme.test", "Sup3rSecret!", "U", "K", AccountKind: kind, CallerCanManageAccountKind: canManage),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Unknown", result.Data!.AccountKind);
        Assert.Equal(AccountKind.Unknown, Assert.Single(await repo.GetAllByTenantAsync(TenantA, 1, 50, CancellationToken.None)).AccountKind);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Human", true)]
    [InlineData("service", true)]
    [InlineData("Robot", false)]
    [InlineData("2", false)]
    public void Create_validator_vets_the_kind_only_when_one_is_supplied(string? kind, bool expectedValid)
    {
        var result = new CreateUserCommandValidator().Validate(new CreateUserCommand("u@acme.test", null, "U", "K", kind));

        Assert.Equal(expectedValid, result.IsValid);
    }

    // ── wiring ──

    private static SetAccountKindCommandHandler SetHandler(IUserRepository repo, CapturingAudit audit)
        => new(repo, TenantContextFor(TenantA), audit, NullLogger<SetAccountKindCommandHandler>.Instance);

    private static CreateUserCommandHandler CreateHandler(InMemoryUserRepository repo, FakeInvitationEmailService email)
        => new(
            repo,
            new FakePasswordHasher(),
            new FakePasswordPolicyService(),
            TenantContextFor(TenantA),
            new FakeTokenService(),
            new FakeRefreshTokenHasher(),
            new FakeHostEnvironment(),
            email,
            NullLogger<CreateUserCommandHandler>.Instance);

    private static ITenantContext TenantContextFor(Guid tenantId)
    {
        var ctx = new TestTenantContext();
        ctx.SetTenant(tenantId);
        return ctx;
    }

    // ── fakes ──

    private sealed class CapturingAudit : IRbacAuditRecorder
    {
        public List<(string EventName, Guid TenantId, object Metadata)> Records { get; } = [];

        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
        {
            Records.Add((eventName, tenantId, metadata));
            return Task.CompletedTask;
        }
    }

    /// <summary>InMemoryUserRepository plus a record of the TENANT-SCOPED update the handler must use.</summary>
    private sealed class TrackingUserRepository(IEnumerable<User> users) : IUserRepository
    {
        private readonly InMemoryUserRepository _inner = new(users);
        public (Guid UserId, Guid TenantId)? UpdatedForTenant { get; private set; }

        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => _inner.GetByEmailAndTenantAsync(email, tenantId, ct);
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => _inner.GetByUserNameAndTenantAsync(normalizedUserName, tenantId, ct);
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => _inner.GetByIdAndTenantAsync(id, tenantId, ct);
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => _inner.GetByPasswordResetTokenHashAsync(tokenHash, ct);
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => _inner.GetAllByTenantAsync(tenantId, page, pageSize, ct);
        public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct) => _inner.SearchActiveAsync(tenantId, term, limit, ct);
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => _inner.GetCountByTenantAsync(tenantId, ct);
        public Task<User> CreateAsync(User user, CancellationToken ct) => _inner.CreateAsync(user, ct);
        public Task<User> UpdateAsync(User user, CancellationToken ct) => throw new InvalidOperationException("The kind change must use the tenant-scoped update.");
        public Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct)
        {
            UpdatedForTenant = (user.Id, tenantId);
            return Task.FromResult(user);
        }
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => _inner.SoftDeleteAsync(id, tenantId, ct);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => hash == "hash:" + password;
    }

    private sealed class FakeRefreshTokenHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
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
        public List<(string email, string token)> Sends { get; } = [];
        public string BuildTenantSetPasswordUrl(string email, string setupToken) => $"http://localhost:5001/account/set-password?email={email}&token={setupToken}";
        public Task SendTenantUserInvitationAsync(string email, string setupToken, CancellationToken ct)
        {
            Sends.Add((email, setupToken));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext { get; private set; }
        public Guid? TargetTenantId { get; private set; }
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; IsPlatformContext = false; TargetTenantId = null; }
        public void SetPlatformContext(Guid targetTenantId) { _tenantId = targetTenantId; IsResolved = true; IsPlatformContext = true; TargetTenantId = targetTenantId; }
    }
}
