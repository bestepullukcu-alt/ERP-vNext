using System.Collections.Concurrent;

namespace Diten.Platform.Application.Features.Tasks;

/// <summary>
/// BL-512 FIX1 — the decision-makers directory (every live person of a tenant, named) kept for a short while, so a
/// reader typing "Ka… Kal… Kali…" does not make the server rebuild the whole directory and ask AuthService for every
/// name on each pause (up to 30 times a minute per person).
///
/// <para>Keyed by TENANT only, because the decision list is the same for everyone in a tenant (it is exempt from the
/// company scope — no caller-specific narrowing). Short-lived (<see cref="Lifetime"/>), so a new hire or a closed
/// position shows up within a minute; bounded (<see cref="MaximumTenants"/> entries), so memory cannot grow with the
/// number of tenants. In process: each Platform instance keeps its own — the same posture as the display-name cache
/// it sits on top of (AuthUserDisplayNameClient), and no new infrastructure.</para>
/// </summary>
public sealed class DecisionMakerDirectoryCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    public const int MaximumTenants = 256;

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly TimeProvider _clock;

    public DecisionMakerDirectoryCache(TimeProvider clock) => _clock = clock;

    public bool TryGet(Guid tenantId, out IReadOnlyList<AssignablePersonDto> rows)
    {
        rows = [];
        if (tenantId == Guid.Empty || !_entries.TryGetValue(tenantId, out var entry)) { return false; }
        if (_clock.GetUtcNow() - entry.At >= Lifetime)
        {
            _entries.TryRemove(new KeyValuePair<Guid, Entry>(tenantId, entry));
            return false;
        }

        rows = entry.Rows;
        return true;
    }

    public void Set(Guid tenantId, IReadOnlyList<AssignablePersonDto> rows)
    {
        if (tenantId == Guid.Empty) { return; }
        var now = _clock.GetUtcNow();
        if (_entries.Count >= MaximumTenants && !_entries.ContainsKey(tenantId))
        {
            // Expired entries go first; if none, the oldest one makes room.
            foreach (var stale in _entries.Where(e => now - e.Value.At >= Lifetime).ToList())
            {
                _entries.TryRemove(stale);
            }

            if (_entries.Count >= MaximumTenants)
            {
                var oldest = _entries.OrderBy(e => e.Value.At).FirstOrDefault();
                if (oldest.Key != Guid.Empty) { _entries.TryRemove(oldest); }
            }
        }

        _entries[tenantId] = new Entry(now, rows);
    }

    public int Count => _entries.Count;

    private sealed record Entry(DateTimeOffset At, IReadOnlyList<AssignablePersonDto> Rows);
}
