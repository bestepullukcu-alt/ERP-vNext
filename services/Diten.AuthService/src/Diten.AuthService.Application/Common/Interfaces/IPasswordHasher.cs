namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX6 S2 — what a request path calls. The production hasher waits for its CPU slot
    /// WITHOUT holding a thread (the BCrypt work itself stays synchronous); a hasher without a slot hashes at once.</summary>
    Task<string> HashAsync(string password, CancellationToken cancellationToken = default) => Task.FromResult(Hash(password));

    /// <inheritdoc cref="HashAsync"/>
    Task<bool> VerifyAsync(string password, string hash, CancellationToken cancellationToken = default)
        => Task.FromResult(Verify(password, hash));
}
