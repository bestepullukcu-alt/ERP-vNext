using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Users;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users;

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

    public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var users = _users.Where(u => u.TenantId == tenantId && !u.IsDeleted);
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
