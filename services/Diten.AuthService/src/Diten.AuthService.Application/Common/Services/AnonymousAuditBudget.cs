namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 3 — AN ANONYMOUS REQUEST CANNOT WRITE AN AUDIT ROW EACH TIME IT IS SENT. The sign-in
/// door answers a tenant whose login settings cannot be read (an unknown tenant GUID, or Platform not answering) with
/// LOGIN_UNAVAILABLE and records it; before, every such request wrote one row — anyone could fill the audit log with junk
/// GUIDs. Now at most <see cref="RowsPerWindow"/> such rows per <see cref="Window"/> across ALL tenants (one counter, no
/// per-key memory a flood could grow); the rest are dropped — the row is an operational signal ("settings could not be
/// read"), not a per-request record. The answer itself is unchanged.
/// </summary>
public sealed class AnonymousAuditBudget
{
    public const int RowsPerWindow = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static AnonymousAuditBudget SettingsReadFailed { get; } = new(TimeProvider.System);

    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private DateTimeOffset _windowStart = DateTimeOffset.MinValue;
    private int _used;

    public AnonymousAuditBudget(TimeProvider time) => _time = time;

    public bool TryTake()
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (now - _windowStart >= Window)
            {
                _windowStart = now;
                _used = 0;
            }
            if (_used >= RowsPerWindow) return false;
            _used++;
            return true;
        }
    }
}
