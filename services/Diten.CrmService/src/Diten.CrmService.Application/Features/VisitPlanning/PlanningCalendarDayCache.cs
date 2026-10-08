using System.Collections.Concurrent;
using Diten.CrmService.Application.Features.CycleCapacity.Read;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-3B (4b) — a short-lived memory of the platform calendar's RESOLVED per-day answers for the visit planner, so a
/// plan's second preview (every manual reorder is one), and its week approval, ask the platform nothing.
/// <para><b>Tenant-keyed.</b> The key is (tenant, country, legal entity, date): a tenant's override can never answer
/// another tenant (the lookup-cache leak this repo already paid for once).</para>
/// <para><b>Short, on purpose.</b> An entry lives <see cref="Lifetime"/>; a holiday published or an override added is
/// therefore seen within minutes. Only resolved answers are stored — a refusal or an unreachable calendar is asked again
/// next time. The capacity estimate's counter stays cacheless; this cache sits only in front of the planner's per-day
/// question. Singleton; bounded by <see cref="MaxEntries"/> (cleared when exceeded).</para>
/// </summary>
public sealed class PlanningCalendarDayCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public const int MaxEntries = 50_000;

    private readonly ConcurrentDictionary<Key, (WorkingDayCheckResult Answer, DateTimeOffset Expires)> _entries = new();
    private readonly Func<DateTimeOffset> _now;

    public PlanningCalendarDayCache() : this(() => DateTimeOffset.UtcNow)
    {
    }

    public PlanningCalendarDayCache(Func<DateTimeOffset> now) => _now = now;

    public readonly record struct Key(Guid TenantId, string CountryCode, Guid? LegalEntityId, DateOnly Date);

    public bool TryGet(Key key, out WorkingDayCheckResult answer)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.Expires > _now())
        {
            answer = entry.Answer;
            return true;
        }

        answer = null!;
        return false;
    }

    public void Set(Key key, WorkingDayCheckResult answer)
    {
        if (_entries.Count >= MaxEntries)
        {
            _entries.Clear();
        }

        _entries[key] = (answer, _now() + Lifetime);
    }
}
