using System.Security.Cryptography;
using System.Text;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// WP-EMAIL-LANG-01 FIX1 — a signed-in user's own writes (today: <c>PUT api/auth/me/preferences</c>) are limited PER USER,
/// in fixed windows like <see cref="PasswordDoorRateLimiter"/>. The partition is (tenant, user) from the signed token — never
/// a value the client wrote — and is kept only as a SHA-256.
/// <para><b>Its own window (FIX3).</b> The framework's fixed-window lease reports whole windows as its RetryAfter, not the
/// time left in the current one; so the window is counted here, from this class's own clock, and a refusal says exactly
/// how long is left.</para>
/// <para><b>Per instance (FIX2).</b> The counts live in this process's memory: with N AuthService instances behind a load
/// balancer one user can make up to N × <see cref="DefaultPreferencesLimit"/> writes per <see cref="DefaultWindow"/>. That is
/// the same as <see cref="PasswordDoorRateLimiter"/>, and enough for its purpose (a flood, not a precise quota).</para>
/// </summary>
public sealed class SelfServiceRateLimiter : IDisposable
{
    public const int DefaultPreferencesLimit = 10;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);

    private readonly int _limit;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, (DateTimeOffset Start, int Count)> _windows = new(StringComparer.Ordinal);
    private DateTimeOffset _lastForget = DateTimeOffset.MinValue;

    public SelfServiceRateLimiter()
        : this(DefaultPreferencesLimit, DefaultWindow)
    {
    }

    public SelfServiceRateLimiter(int preferencesLimit, TimeSpan window, TimeProvider? clock = null)
    {
        _limit = Math.Max(1, preferencesLimit);
        Window = window;
        _clock = clock ?? TimeProvider.System;
    }

    public TimeSpan Window { get; }

    /// <summary>True = go on; false = 429, with <paramref name="retryAfter"/> the time left in this user's current window.</summary>
    public bool TryAcquirePreferences(Guid tenantId, Guid userId, out TimeSpan retryAfter)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"preferences|{tenantId:D}|{userId:D}")));
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (!_windows.TryGetValue(key, out var window) || now - window.Start >= Window)
            {
                ForgetEndedWindows(now);
                window = (now, 0);
            }

            if (window.Count < _limit)
            {
                _windows[key] = (window.Start, window.Count + 1);
                retryAfter = TimeSpan.Zero;
                return true;
            }

            retryAfter = window.Start + Window - now;
            return false;
        }
    }

    // Only users who wrote in the last window are remembered; the look is taken at most once a window.
    private void ForgetEndedWindows(DateTimeOffset now)
    {
        if (now - _lastForget < Window)
        {
            return;
        }

        _lastForget = now;
        foreach (var ended in _windows.Where(pair => now - pair.Value.Start >= Window).Select(pair => pair.Key).ToList())
        {
            _windows.Remove(ended);
        }
    }

    public void Dispose()
    {
    }
}
