using System.Reflection;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// MOD-0024 DEV-2 — the single additive internal endpoint that resolves display names for Platform.
///
/// <para>It is a deliberate widening of what an internal-key caller can read, so the boundaries are tested as
/// hard as the behaviour: cross-tenant isolation, the key gate, and the response carrying nothing but id and
/// name.</para>
/// </summary>
public sealed class InternalUsersControllerTests
{
    private const string ApiKey = "internal-key";
    private const string Header = "X-Internal-Api-Key";

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // ── The boundary that matters most ────────────────────────────────────────

    [Fact]
    public async Task Tenant_A_can_never_resolve_a_tenant_B_name()
    {
        var alice = NewUser("alice@a.test", "Alice", "Adams", TenantA);
        var bob = NewUser("bob@b.test", "Bob", "Baker", TenantB);
        var controller = Build(authorized: true, alice, bob);

        // Ask, from tenant A's context, for BOTH ids — including B's.
        var result = await controller.GetDisplayNames(TenantA, $"{alice.Id},{bob.Id}", CancellationToken.None);

        var rows = Rows(result);
        Assert.Single(rows);
        Assert.Equal(alice.Id, rows[0].Id);
        // Bob is simply absent — no name, no acknowledgement that the id exists at all.
        Assert.DoesNotContain(rows, r => r.Id == bob.Id);
    }

    [Fact]
    public async Task Asking_ONLY_for_a_foreign_id_returns_nothing()
    {
        var bob = NewUser("bob@b.test", "Bob", "Baker", TenantB);
        var controller = Build(authorized: true, bob);

        var result = await controller.GetDisplayNames(TenantA, bob.Id.ToString(), CancellationToken.None);

        Assert.Empty(Rows(result));
    }

    // ── The key gate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_missing_or_wrong_internal_key_is_rejected()
    {
        var alice = NewUser("alice@a.test", "Alice", "Adams", TenantA);
        var controller = Build(authorized: false, alice);

        var result = await controller.GetDisplayNames(TenantA, alice.Id.ToString(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── The response carries nothing else ─────────────────────────────────────

    [Fact]
    public void The_response_shape_is_ONLY_id_and_display_name()
    {
        // A future field added here silently widens what every S2S caller can read.
        var properties = typeof(InternalUserDisplayNameDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        Assert.Equal(new[] { "DisplayName", "Id" }, properties);
    }

    [Fact]
    public async Task No_email_phone_role_or_status_reaches_the_caller()
    {
        var alice = NewUser("alice@a.test", "Alice", "Adams", TenantA);
        var controller = Build(authorized: true, alice);

        var result = await controller.GetDisplayNames(TenantA, alice.Id.ToString(), CancellationToken.None);

        var json = System.Text.Json.JsonSerializer.Serialize(Rows(result));
        Assert.DoesNotContain("alice@a.test", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lastLogin", json, StringComparison.OrdinalIgnoreCase);
    }

    // ── Input handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_missing_tenant_is_rejected_rather_than_searching_everywhere()
    {
        var controller = Build(authorized: true, NewUser("alice@a.test", "Alice", "Adams", TenantA));

        var result = await controller.GetDisplayNames(Guid.Empty, Guid.NewGuid().ToString(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task An_empty_id_set_is_rejected_so_this_cannot_become_a_directory_dump()
    {
        var controller = Build(authorized: true, NewUser("alice@a.test", "Alice", "Adams", TenantA));

        var result = await controller.GetDisplayNames(TenantA, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Resolving_many_ids_reads_the_tenant_ONCE_not_once_per_id()
    {
        var users = Enumerable.Range(0, 25)
            .Select(i => NewUser($"u{i}@a.test", $"User{i}", "Test", TenantA))
            .ToArray();
        var repository = new FakeUserRepository(users);
        var controller = Build(repository, authorized: true);

        var result = await controller.GetDisplayNames(
            TenantA, string.Join(',', users.Select(u => u.Id)), CancellationToken.None);

        Assert.Equal(25, Rows(result).Count);
        // 25 names, one repository read — the sweep is per page, never per id.
        Assert.Equal(1, repository.GetAllCallCount);
    }

    [Fact]
    public async Task A_user_with_no_name_falls_back_to_the_username_not_the_id()
    {
        var nameless = NewUser("nameless@a.test", string.Empty, string.Empty, TenantA);
        var controller = Build(authorized: true, nameless);

        var result = await controller.GetDisplayNames(TenantA, nameless.Id.ToString(), CancellationToken.None);

        var row = Assert.Single(Rows(result));
        Assert.NotEqual(nameless.Id.ToString(), row.DisplayName);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<InternalUserDisplayNameDto> Rows(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<List<InternalUserDisplayNameDto>>(ok.Value);
    }

    private static User NewUser(string email, string first, string last, Guid tenantId)
        => new(email, "hash", first, last, tenantId);

    private static InternalUsersController Build(bool authorized, params User[] users)
        => Build(new FakeUserRepository(users), authorized);

    private static InternalUsersController Build(FakeUserRepository repository, bool authorized)
    {
        var controller = new InternalUsersController(
            new FakeInternalEventAuthService(authorized ? ApiKey : null),
            repository,
            NullLogger<InternalUsersController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[Header] = ApiKey;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    /// <summary>Mirrors the real repository's tenant filter — the scoping under test lives in that argument.</summary>
    private sealed class FakeUserRepository(params User[] seed) : IUserRepository
    {
        private readonly List<User> _users = [.. seed];

        public int GetAllCallCount { get; private set; }

        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
        {
            GetAllCallCount++;
            var rows = _users
                .Where(u => u.TenantId == tenantId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
            return Task.FromResult(rows);
        }

        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult((long)_users.Count(u => u.TenantId == tenantId));

        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct)
            => throw new NotSupportedException("The display-name endpoint must not read users by email.");

        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
            => throw new NotSupportedException("Per-id reads would be the N+1 this endpoint exists to avoid.");

        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<User> CreateAsync(User user, CancellationToken ct) => throw new NotSupportedException("read-only endpoint");
        public Task<User> UpdateAsync(User user, CancellationToken ct) => throw new NotSupportedException("read-only endpoint");
        public Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct) => throw new NotSupportedException("read-only endpoint");
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException("read-only endpoint");
    }

    private sealed class FakeInternalEventAuthService(string? expectedKey) : IInternalEventAuthService
    {
        public bool IsAuthorized(string? apiKey)
            => expectedKey is not null && apiKey == expectedKey;
    }
}
