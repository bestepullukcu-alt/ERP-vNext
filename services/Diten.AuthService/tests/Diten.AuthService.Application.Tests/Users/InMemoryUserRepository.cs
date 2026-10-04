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

    /// <summary>BL-529 — how many conditional writes to refuse first (a password changed between the read and the write).</summary>
    public int PasswordChangedConflicts { get; set; }

    public int ConditionalWrites { get; private set; }

    public int LoginOutcomeWrites { get; private set; }

    public Task RecordLoginOutcomeAsync(User user, Guid tenantId, CancellationToken ct)
    {
        LoginOutcomeWrites++;
        return Task.CompletedTask;
    }

    /// <summary>BL-529 — the account write itself fails (the store is down) after the sessions were ended.</summary>
    public bool ThrowOnConditionalWrite { get; set; }

    public Action? OnConditionalWrite { get; set; }

    public Task<bool> TryUpdateForTenantIfPasswordHashAsync(User user, Guid tenantId, string expectedPasswordHash, CancellationToken ct)
    {
        ConditionalWrites++;
        OnConditionalWrite?.Invoke();
        if (ThrowOnConditionalWrite) throw new InvalidOperationException("user write failed");
        if (PasswordChangedConflicts > 0)
        {
            PasswordChangedConflicts--;
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public int ResetTokenWrites { get; private set; }

    public Task<bool> TryUpdateForTenantIfResetTokenAsync(User user, Guid tenantId, string expectedResetTokenHash, CancellationToken ct)
        => Task.FromResult(true);

    public Task<bool> SetPasswordResetTokenAsync(Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted);
        if (user is null) return Task.FromResult(false);
        user.SetPasswordResetToken(tokenHash, expiresAtUtc);
        ResetTokenWrites++;
        return Task.FromResult(true);
    }

    // Mirrors the store's $inc: the stored row counts, whatever copy the caller holds.
    public Task<LoginFailureOutcome> RecordLoginFailureAsync(Guid userId, Guid tenantId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct)
    {
        var user = _users.FirstOrDefault(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted);
        if (user is null) return Task.FromResult(new LoginFailureOutcome(0, null));
        user.RecordLoginFailure(maxFailedAttempts, lockoutDurationMinutes);
        return Task.FromResult(new LoginFailureOutcome(user.FailedLoginAttempts, user.LockoutEnd));
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
