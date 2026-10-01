using System.Reflection;
using System.Security.Claims;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Application.Tests.Users;

[CollectionDefinition("LocalTenantAdminResetEnvironment", DisableParallelization = true)]
public sealed class LocalTenantAdminResetEnvironmentCollection;

[Collection("LocalTenantAdminResetEnvironment")]
public sealed class LocalTenantAdminPasswordResetControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("74355e70-4c7d-410c-8cf6-db5fe3b9547f");
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Operator = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string TargetEmail = "sku-local-admin@example.test";

    [Fact]
    public async Task Exact_reset_updates_only_existing_target_user_and_returns_one_time_link()
    {
        var target = ExistingTarget();
        var platformUser = ExistingOperator();
        var writes = 0;
        var users = Repo<IUserRepository>((method, args) => method switch
        {
            "GetByIdAndTenantAsync" when (Guid)args![0]! == Operator && (Guid)args[1]! == PlatformTenant => (object)Task.FromResult<User?>(platformUser),
            "GetByIdAndTenantAsync" when (Guid)args![0]! == target.Id && (Guid)args[1]! == Tenant => (object)Task.FromResult<User?>(target),
            "UpdateForTenantAsync" when ReferenceEquals(args![0], target) && (Guid)args[1]! == Tenant => Updated(),
            _ => throw new InvalidOperationException($"Unexpected user repository call: {method}")
        });
        Task<User> Updated() { writes++; return Task.FromResult(target); }
        var controller = Controller(users, target, ["Admin"], [new TenantUserMembership(target.Id, Tenant, TargetEmail)]);
        using var scope = EnableFor(target.Id);

        var result = await controller.Reset(Tenant, new LocalTenantAdminResetRequest(TargetEmail), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("account/set-password", ok.Value!.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, writes);
        Assert.True(target.MustChangePassword);
        Assert.Equal("hash:one-time-token", target.PasswordResetTokenHash);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Missing_admin_role_rejects_without_any_write()
    {
        var target = ExistingTarget();
        var writes = 0;
        var users = Repo<IUserRepository>((method, args) => method switch
        {
            "GetByIdAndTenantAsync" when (Guid)args![0]! == Operator => (object)Task.FromResult<User?>(ExistingOperator()),
            "GetByIdAndTenantAsync" when (Guid)args![0]! == target.Id => (object)Task.FromResult<User?>(target),
            "UpdateForTenantAsync" => UnexpectedWrite(),
            _ => throw new InvalidOperationException(method)
        });
        Task<User> UnexpectedWrite() { writes++; return Task.FromResult(target); }
        var controller = Controller(users, target, ["Member"], [new TenantUserMembership(target.Id, Tenant, TargetEmail)]);
        using var scope = EnableFor(target.Id);

        Assert.IsType<NotFoundResult>(await controller.Reset(Tenant, new LocalTenantAdminResetRequest(TargetEmail), CancellationToken.None));
        Assert.Equal(0, writes);
        Assert.Null(target.PasswordResetTokenHash);
    }

    [Fact]
    public async Task Wrong_tenant_is_rejected_before_repository_access()
    {
        var users = Repo<IUserRepository>((method, _) => throw new InvalidOperationException(method));
        var target = ExistingTarget();
        var controller = Controller(users, target, ["Admin"], []);
        using var scope = EnableFor(target.Id);

        Assert.IsType<NotFoundResult>(await controller.Reset(Guid.NewGuid(), new LocalTenantAdminResetRequest(TargetEmail), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_membership_rejects_before_target_write()
    {
        var target = ExistingTarget();
        var writes = 0;
        var users = Repo<IUserRepository>((method, args) => method switch
        {
            "GetByIdAndTenantAsync" when (Guid)args![0]! == Operator => (object)Task.FromResult<User?>(ExistingOperator()),
            "GetByIdAndTenantAsync" when (Guid)args![0]! == target.Id => (object)Task.FromResult<User?>(target),
            "UpdateForTenantAsync" => UnexpectedWrite(),
            _ => throw new InvalidOperationException(method)
        });
        Task<User> UnexpectedWrite() { writes++; return Task.FromResult(target); }
        var controller = Controller(users, target, ["Admin"], []);
        using var scope = EnableFor(target.Id);

        Assert.IsType<NotFoundResult>(await controller.Reset(Tenant, new LocalTenantAdminResetRequest(TargetEmail), CancellationToken.None));
        Assert.Equal(0, writes);
        Assert.Null(target.PasswordResetTokenHash);
    }

    private static LocalTenantAdminPasswordResetController Controller(IUserRepository users, User target,
        string[] roles, TenantUserMembership[] memberships)
    {
        var userRoles = Repo<IUserRoleRepository>((method, _) => method == "GetRolesByUserAsync"
            ? Task.FromResult<IEnumerable<string>>(roles) : throw new InvalidOperationException(method));
        var memberRepo = Repo<ITenantUserMembershipRepository>((method, _) => method == "GetByUserIdAsync"
            ? Task.FromResult<IReadOnlyList<TenantUserMembership>>(memberships) : throw new InvalidOperationException(method));
        var admins = Repo<IPlatformAdministratorStatusClient>((method, _) => method == "IsActiveAsync"
            ? Task.FromResult(true) : throw new InvalidOperationException(method));
        var tokens = Repo<ITokenService>((method, _) => method == "GenerateRefreshToken"
            ? "one-time-token" : throw new InvalidOperationException(method));
        var hasher = Repo<IRefreshTokenHasher>((method, _) => method == "Hash"
            ? "hash:one-time-token" : throw new InvalidOperationException(method));
        var links = Repo<ITenantUserInvitationEmailService>((method, _) => method == "BuildTenantSetPasswordUrl"
            ? "http://localhost:5001/account/set-password?token=one-time-token" : throw new InvalidOperationException(method));
        var controller = new LocalTenantAdminPasswordResetController(new DevelopmentEnvironment(), users, userRoles,
            memberRepo, admins, tokens, hasher, links);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Operator.ToString()), new Claim(ClaimTypes.Email, "admin@platform.test"),
            new Claim("actor_type", "platform_admin"), new Claim("tenant_id", PlatformTenant.ToString()),
            new Claim("pwd_change_required", "false")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return controller;
    }

    private static User ExistingTarget()
    {
        var user = new User(TargetEmail, "existing-hash", "Tenant", "Admin", Tenant);
        user.ConfirmEmail();
        return user;
    }

    private static User ExistingOperator() => new("admin@platform.test", "existing-hash", "Platform", "Admin", PlatformTenant);

    private static IDisposable EnableFor(Guid userId) => new EnvironmentScope(userId);

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();
        public EnvironmentScope(Guid userId)
        {
            Set("ENABLED", "true"); Set("TENANT_ID", Tenant.ToString()); Set("USER_ID", userId.ToString());
            Set("OPERATOR_ID", Operator.ToString()); Set("EMAIL", TargetEmail);
        }
        private void Set(string suffix, string value)
        {
            var key = "DITEN_LOCAL_TENANT_ADMIN_RESET_" + suffix;
            _previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
        public void Dispose()
        {
            foreach (var (key, value) in _previous) Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    public class RepositoryProxy : DispatchProxy
    {
        private Func<string, object?[]?, object?> _invoke = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _invoke(targetMethod!.Name, args);
        public static T Create<T>(Func<string, object?[]?, object?> invoke) where T : class
        {
            var proxy = Create<T, RepositoryProxy>();
            ((RepositoryProxy)(object)proxy)._invoke = invoke;
            return proxy;
        }
    }

    private static T Repo<T>(Func<string, object?[]?, object?> invoke) where T : class => RepositoryProxy.Create<T>(invoke);
}
