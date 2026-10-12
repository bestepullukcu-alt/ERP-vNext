using System.Collections.Concurrent;
using System.Security.Cryptography;
using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Features.Auth.Services;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 2 — A SIGN-IN THAT FINDS NO ACCOUNT TAKES AS LONG AS ONE THAT DOES. Verifying a
/// password is the slow part of a sign-in (BCrypt, work factor 12: ~200–300 ms); a door that answered an unknown address
/// (or an account of another kind) BEFORE verifying anything answered measurably faster — the time told an address that
/// exists from one that does not, whatever the body said. Those branches now verify the submitted password against a
/// fixed hash made once, with the same hasher (so the same work factor), and throw the result away.
/// </summary>
public static class SignInTiming
{
    // One per hasher TYPE: a hash made by one hasher is only ever verified by that kind of hasher (a test double's hash
    // is never handed to BCrypt).
    private static readonly ConcurrentDictionary<Type, string> NoAccountHash = new();

    public static void VerifyAgainstNoAccount(IPasswordHasher hasher, string? password)
        => _ = hasher.Verify(password ?? string.Empty, HashFor(hasher));

    /// <summary>FIX3 item 7 — made at start (<c>SignInTimingWarmup</c>): otherwise the FIRST unknown address after a start
    /// would also pay for making the hash, and be slower than every other answer.</summary>
    public static void Warm(IPasswordHasher hasher) => _ = HashFor(hasher);

    public static bool IsWarm(IPasswordHasher hasher) => NoAccountHash.ContainsKey(hasher.GetType());

    private static string HashFor(IPasswordHasher hasher) => NoAccountHash.GetOrAdd(hasher.GetType(),
        _ => hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))));
}
