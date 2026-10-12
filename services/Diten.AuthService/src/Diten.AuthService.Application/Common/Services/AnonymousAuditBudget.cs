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

    // FIX3 item 4 — ONE budget for every anonymous row of the sign-in door (settings unreadable, e-mail sign-in switched
    // off): a flood of either fills the same window.
    public static AnonymousAuditBudget SignInDoor { get; } = new(TimeProvider.System);

    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private DateTimeOffset _windowStart = DateTimeOffset.MinValue;
    private int _used;
    private long _dropped;

    public AnonymousAuditBudget(TimeProvider time) => _time = time;

    /// <summary>True = write the row. FIX3 item 4 — a dropped row is not silent: <paramref name="firstDrop"/> is true for the
    /// first row dropped in a window (say that the budget is full), <paramref name="droppedInLastWindow"/> carries, once, how
    /// many rows the window that just ended dropped.</summary>
    public bool TryTake(out bool firstDrop, out long droppedInLastWindow)
    {
        lock (_gate)
        {
            firstDrop = false;
            droppedInLastWindow = 0;
            var now = _time.GetUtcNow();
            if (now - _windowStart >= Window)
            {
                droppedInLastWindow = _dropped;
                _windowStart = now;
                _used = 0;
                _dropped = 0;
            }
            if (_used >= RowsPerWindow)
            {
                firstDrop = _dropped == 0;
                _dropped++;
                return false;
            }
            _used++;
            return true;
        }
    }

    public bool TryTake() => TryTake(out _, out _);
}
