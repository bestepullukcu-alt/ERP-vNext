using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>WP-VW-W5 — the notification log in memory with the production contract: one row per (tenant, event, subject),
/// a versioned replace, an atomic claim (one lock).</summary>
internal sealed class FakeNotificationRows : IVisitNotificationRepository
{
    private readonly object _lock = new();
    public List<VisitNotification> Items { get; } = new();

    /// <summary>A storage failure on insert (the "never break the report" test).</summary>
    public bool ThrowOnInsert { get; set; }

    public Task<bool> TryInsertAsync(VisitNotification row, CancellationToken ct)
    {
        if (ThrowOnInsert) throw new InvalidOperationException("notification store down");
        lock (_lock)
        {
            if (Items.Any(r => r.TenantId == row.TenantId && r.EventCode == row.EventCode && r.SubjectId == row.SubjectId)) return Task.FromResult(false);
            Items.Add(row);
            return Task.FromResult(true);
        }
    }

    public Task<bool> ExistsAsync(Guid tenantId, string eventCode, Guid subjectId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(Items.Any(r => r.TenantId == tenantId && r.EventCode == eventCode && r.SubjectId == subjectId));
        }
    }

    public Task<bool> ReplaceAsync(VisitNotification row, int expectedVersion, CancellationToken ct)
    {
        lock (_lock)
        {
            var index = Items.FindIndex(r => r.Id == row.Id && r.TenantId == row.TenantId);
            if (index < 0 || Items[index].Version != expectedVersion) return Task.FromResult(false);
            row.Version = expectedVersion + 1;
            Items[index] = row;
            return Task.FromResult(true);
        }
    }

    public Task<VisitNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

    public Task<(IReadOnlyList<VisitNotification> Items, long Total)> ListAsync(Guid tenantId, VisitNotificationFilter f, int skip, int take, CancellationToken ct)
    {
        var rows = Items.Where(r => r.TenantId == tenantId
                                    && (f.From is null || r.VisitDate >= f.From) && (f.To is null || r.VisitDate <= f.To)
                                    && (f.EventCode is null || VisitNotificationEvents.Aliases(f.EventCode).Contains(r.EventCode)) && (f.Status is null || r.Status == f.Status)
                                    && (f.ResourceId is null || r.ResourceId == f.ResourceId) && (f.Reps is null || f.Reps.Contains(r.ResourceId)))
            .OrderByDescending(r => r.CreatedTicks).ToList();
        return Task.FromResult<(IReadOnlyList<VisitNotification>, long)>((rows.Skip(skip).Take(take).ToList(), rows.Count));
    }

    public Task<VisitNotification?> ClaimNextAsync(long nowTicks, long staleQueuedBeforeTicks, long claimUntilTicks, string claimer, int maxAttempts, CancellationToken ct)
    {
        lock (_lock)
        {
            var row = Items.Where(r => !r.IsDeleted && r.ClaimedUntilTicks <= nowTicks
                                       && (r.Status == VisitNotificationStatuses.Failed && r.NextAttemptTicks <= nowTicks && r.Attempts < maxAttempts
                                           || r.Status == VisitNotificationStatuses.Queued && r.CreatedTicks <= staleQueuedBeforeTicks))
                .OrderBy(r => r.CreatedTicks).FirstOrDefault();
            if (row is not null)
            {
                row.ClaimedUntilTicks = claimUntilTicks;
                row.ClaimedBy = claimer;
            }

            return Task.FromResult(row);
        }
    }
}

internal sealed class FakeNotificationSettings : IVisitNotificationSettingsRepository
{
    public List<VisitNotificationSettings> Items { get; } = new();

    public Task<VisitNotificationSettings?> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == tenantId));

    public Task<bool> InsertAsync(VisitNotificationSettings entity, CancellationToken ct)
    {
        if (Items.Any(s => s.TenantId == entity.TenantId)) return Task.FromResult(false);
        Items.Add(entity);
        return Task.FromResult(true);
    }

    public Task<bool> ReplaceAsync(VisitNotificationSettings entity, int expectedVersion, CancellationToken ct)
    {
        var index = Items.FindIndex(s => s.Id == entity.Id && s.TenantId == entity.TenantId);
        if (index < 0 || StaleVersion == expectedVersion) return Task.FromResult(false);
        entity.Version = expectedVersion + 1;
        Items[index] = entity;
        return Task.FromResult(true);
    }

    public int? StaleVersion { get; set; }

    public Task<IReadOnlyList<Guid>> ListTenantsWithOverdueRemindersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Guid>>(Items.Where(s => s.OverdueReminders).Select(s => s.TenantId).ToList());
}

/// <summary>The Platform's internal dispatch in memory: records every request; answers sent (one dispatch per call)
/// unless told otherwise.</summary>
internal sealed class FakePlatform : IPlatformNotificationClient
{
    public List<PlatformNotificationRequest> Requests { get; } = new();
    public bool Down { get; set; }
    public bool Throws { get; set; }
    public string Status { get; set; } = "queued";

    public Task<PlatformNotificationResult> DispatchAsync(PlatformNotificationRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        if (Throws) throw new HttpRequestException("platform exploded");
        if (Down) return Task.FromResult(PlatformNotificationResult.Unavailable("platform-503"));
        return Task.FromResult(new PlatformNotificationResult(true, Status, [Guid.NewGuid().ToString("D")],
            request.To.Select(r => new PlatformResolvedRecipient(r.LineManagerOf is null ? r.UserId : "manager-of-" + r.LineManagerOf, r.LineManagerOf is null ? (r.Email is null ? "direct" : "email") : "line-manager")).ToList(),
            request.Cc.Select(r => new PlatformResolvedRecipient(r.LineManagerOf is null ? r.UserId : "manager-of-" + r.LineManagerOf, r.LineManagerOf is null ? "email" : "line-manager")).ToList()));
    }
}

internal sealed class FixedLinks : IVisitNotificationLinks
{
    public string ReportUrl(Guid plannedVisitId) => "https://crm.test/CRM/VisitReport?plannedVisitId=" + plannedVisitId.ToString("D");
}

internal sealed class W5Clock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Value { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Value;
}

/// <summary>The notifier + sender + worker over one set of fakes.</summary>
internal sealed class W5Notifications
{
    public FakeNotificationRows Rows { get; } = new();
    public FakeNotificationSettings Settings { get; } = new();
    public FakePlatform Platform { get; } = new();
    public W5Clock Clock { get; }

    public W5Notifications(DateTimeOffset now) => Clock = new W5Clock(now);

    public VisitNotificationSender Sender() => new(Rows, Settings, Platform, Clock);

    public VisitReportNotifier Notifier(IContactRepository contacts, IAccountRepository accounts, IVisitReportRepository reports, IPlannedVisitRepository plans)
        => new(Sender(), Settings, contacts, accounts, new FixedLinks(), reports, plans);

    public VisitNotificationWorker Worker(IContactRepository contacts, IAccountRepository accounts, IVisitReportRepository reports, IPlannedVisitRepository plans)
        => new(Rows, Settings, plans, reports, Notifier(contacts, accounts, reports, plans), Sender());
}
