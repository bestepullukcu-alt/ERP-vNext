using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitWorkspace;

/// <summary>
/// W2-BE-c (C1) — the short-lived memory of the workspace's draft previews (singleton). A preview runs the whole planning
/// engine (selection, frequency, content, consent, availability, capacity, working calendar, day balance, route) and took
/// ≈ 7 s per calendar read; the same plan asked again inside the TTL answers from here.
/// <para><b>Bounded:</b> an entry lives <see cref="Ttl"/>; at most <see cref="MaxEntries"/> entries (the oldest go first).
/// <b>Tenant-isolated:</b> the tenant id is the first part of every key, and a read never crosses it.</para>
/// </summary>
public sealed class WorkspacePreviewCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    public const int MaxEntries = 256;

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, VisitPlanPreview Preview)> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public bool TryGet(string key, DateTimeOffset now, out VisitPlanPreview preview)
    {
        preview = default!;
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (now - entry.At > Ttl)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        preview = entry.Preview;
        return true;
    }

    public void Set(string key, VisitPlanPreview preview, DateTimeOffset now)
    {
        foreach (var stale in _entries.Where(e => now - e.Value.At > Ttl).Select(e => e.Key).ToList())
        {
            _entries.TryRemove(stale, out _);
        }

        while (_entries.Count >= MaxEntries)
        {
            var oldest = _entries.OrderBy(e => e.Value.At).Select(e => e.Key).FirstOrDefault();
            if (oldest is null || !_entries.TryRemove(oldest, out _))
            {
                break;
            }
        }

        _entries[key] = (now, preview);
    }
}

/// <summary>
/// W2-BE-c (C1) — the cached preview source the workspace calendar and the reschedule options both use. The key is
/// <c>tenant | session id | session version | today (UTC) | written-visit stamp</c>:
/// <list type="bullet">
/// <item><b>session id + version</b> — every plan change (selection, products, pins, time pins, week extras, approve /
/// reopen, manual order) is a version-checked session write, so it bumps the version;</item>
/// <item><b>today (UTC)</b> — the engine starts the horizon today and derives past / current weeks from it;</item>
/// <item><b>written-visit stamp</b> — a hash of the rep's planned visits (id, version, status, day) and their reports
/// (id, version, status, outcome): the preview counts approved / written visits as fixed load and "done" by their
/// reports, so a cancel, an outcome, a submit, a reschedule or an unplanned visit changes the key.</item>
/// </list>
/// Inputs NOT in the key (master / configuration data, bounded by the TTL instead): accounts / contacts (location,
/// status, names), frequency policies, segments, consent, availability, cycle period + capacity, the working calendar,
/// content (plays, journeys, paths, product names), territory coverage, pharmacy links, route defaults.
/// </summary>
public sealed class CachedWorkspacePlanPreviewSource : IWorkspacePlanPreviewSource
{
    private readonly IWorkspacePlanPreviewSource _inner;
    private readonly WorkspacePreviewCache _cache;
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly IVisitReportRepository _reports;
    private readonly TimeProvider _clock;

    // The stamp of one rep, computed once per request (this source is scoped).
    private readonly Dictionary<string, string> _stamps = new(StringComparer.Ordinal);

    public CachedWorkspacePlanPreviewSource(
        IWorkspacePlanPreviewSource inner,
        WorkspacePreviewCache cache,
        ITenantContext tenant,
        IPlannedVisitRepository plannedVisits,
        IVisitReportRepository reports,
        TimeProvider? clock = null)
    {
        _inner = inner;
        _cache = cache;
        _tenant = tenant;
        _plannedVisits = plannedVisits;
        _reports = reports;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<VisitPlanPreview?> PreviewAsync(PlanningSession session, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return await _inner.PreviewAsync(session, cancellationToken);
        }

        var now = _clock.GetUtcNow();
        var key = KeyOf(tenantId, session, PlanningWeekCalendar.Today(now),
            await StampAsync(tenantId, session.ResourceId, cancellationToken));
        if (_cache.TryGet(key, now, out var cached))
        {
            return cached;
        }

        var preview = await _inner.PreviewAsync(session, cancellationToken);
        if (preview is not null)
        {
            _cache.Set(key, preview, now);
        }

        return preview;
    }

    /// <summary>The cache key (see the class note).</summary>
    public static string KeyOf(Guid tenantId, PlanningSession session, DateOnly today, string stamp)
        => string.Join('|', tenantId.ToString("N"), session.Id.ToString("N"), session.Version, today.ToString("yyyy-MM-dd"), stamp);

    private async Task<string> StampAsync(Guid tenantId, string resourceId, CancellationToken cancellationToken)
    {
        if (_stamps.TryGetValue(resourceId, out var known))
        {
            return known;
        }

        var visits = (await _plannedVisits.ListAsync(tenantId, cancellationToken))
            .Where(v => string.Equals(v.Resource.ResourceId, resourceId, StringComparison.Ordinal))
            .OrderBy(v => v.Id)
            .ToList();
        var reports = (await _reports.ListByPlannedVisitIdsAsync(tenantId, visits.Select(v => v.Id).ToList(), cancellationToken))
            .OrderBy(r => r.Id)
            .ToList();

        var text = new StringBuilder();
        foreach (var v in visits)
        {
            text.Append(v.Id.ToString("N")).Append(':').Append(v.Version).Append(':').Append(v.PlanStatus)
                .Append(':').Append(v.PlannedDate.ToString("yyyy-MM-dd")).Append(';');
        }

        text.Append('#');
        foreach (var r in reports)
        {
            text.Append(r.Id.ToString("N")).Append(':').Append(r.Version).Append(':').Append(r.ReportStatus)
                .Append(':').Append(r.ExecutionOutcome).Append(';');
        }

        var stamp = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16];
        _stamps[resourceId] = stamp;
        return stamp;
    }
}
