using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Users;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users;

    /// <summary>FIX7 item 3 - plays a tenant actor for <see cref="VisibleUserIdsAsync"/>: the platform accounts are not among the visible ids.</summary>
    public bool HidesPlatformAccounts { get; init; }

    public InMemoryUserRepository(IEnumerable<User> users)
    {
        _users = users.ToList();
    }

    public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(u =>
            u.Email == email && u.TenantId == tenantId && !u.IsDeleted);

        return Task.FromResult(user);
    }

    public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(u =>
            u.NormalizedUserName == normalizedUserName && u.TenantId == tenantId && !u.IsDeleted);

        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(u =>
            u.Id == id && u.TenantId == tenantId && !u.IsDeleted);

        return Task.FromResult(user);
    }

    public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct)
    {
        var user = string.IsNullOrWhiteSpace(tokenHash)
            ? null
            : _users.FirstOrDefault(u => u.PasswordResetTokenHash == tokenHash && !u.IsDeleted);

        return Task.FromResult(user);
    }

    public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var users = _users.Where(u => u.TenantId == tenantId && !u.IsDeleted);
        return Task.FromResult(users);
    }

    // Mirrors UserRepository.SearchActiveAsync's contract (tenant + not deleted + ACTIVE; name tokens; limit) so the
    // handler tests exercise the same shape. The Mongo implementation itself is proven by AccountKindEndpointTests.
    public Task<IReadOnlyList<User>> SearchActiveAsync(Guid tenantId, string? term, int limit, CancellationToken ct)
    {
        var tokens = (term ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IReadOnlyList<User> users = _users
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive)
            .Where(u => tokens.All(t =>
                u.FirstName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
        return Task.FromResult(users);
    }

    public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var count = _users.LongCount(u => u.TenantId == tenantId && !u.IsDeleted);
        return Task.FromResult(count);
    }

    public Task<User> CreateAsync(User user, CancellationToken ct)
    {
        _users.Add(user);
        return Task.FromResult(user);
    }

    public Task<User> UpdateAsync(User user, CancellationToken ct) => Task.FromResult(user);

    public Task<User> UpdateForTenantAsync(User user, Guid tenantId, CancellationToken ct) => Task.FromResult(user);

    public Task SetPlatformActorTypeAsync(Guid userId, Guid tenantId, string actorType, CancellationToken ct)
    {
        _users.FirstOrDefault(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted)?.SetPlatformActorType(actorType);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Guid>> VisibleUserIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        IReadOnlyCollection<Guid> visible = HidesPlatformAccounts
            ? userIds.Where(id => !_users.Any(u => u.Id == id && u.TenantId == tenantId && u.IsPlatformAccount)).ToList()
            : userIds;
        return Task.FromResult(visible);
    }

    public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(u => u.Id == id && u.TenantId == tenantId);
        if (user is not null)
        {
            user.IsDeleted = true;
        }

        return Task.CompletedTask;
    }
}
