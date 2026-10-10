using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// WP-EMAIL-LANG-01 FIX1 — a signed-in user's own writes (today: <c>PUT api/auth/me/preferences</c>) are limited PER USER,
/// with the same fixed-window primitives as <see cref="PasswordDoorRateLimiter"/>. The partition is (tenant, user) from the
/// signed token — never a value the client wrote — and is kept only as a SHA-256.
/// </summary>
public sealed class SelfServiceRateLimiter : IDisposable
{
    public const int DefaultPreferencesLimit = 10;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);

    private readonly PartitionedRateLimiter<string> _preferences;

    public SelfServiceRateLimiter()
        : this(DefaultPreferencesLimit, DefaultWindow)
    {
    }

    public SelfServiceRateLimiter(int preferencesLimit, TimeSpan window)
    {
        Window = window;
        _preferences = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, preferencesLimit),
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public TimeSpan Window { get; }

    /// <summary>True = go on; false = 429.</summary>
    public bool TryAcquirePreferences(Guid tenantId, Guid userId)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"preferences|{tenantId:D}|{userId:D}")));
        using var lease = _preferences.AttemptAcquire(key);
        return lease.IsAcquired;
    }

    public void Dispose() => _preferences.Dispose();
}
