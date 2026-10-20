using System.Globalization;
using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitEvidenceFlags;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Features.VisitNotifications;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// WP-VW-W5 (W5-BE) — the CRM visit notifications: the report summary, the adverse event, the off-label request (on
// submit) and the overdue reminder (the scheduler), sent ONLY through the Platform's internal event dispatch (K-N1), the
// recipient resolved by the Platform (K-N2), logged once per subject in crm_visit_notifications (K-N6). A notification
// never breaks the visit flow (K-N8): a failure is a "failed" row the scheduler tries again.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════

public static class VisitNotificationPermissions
{
    public const string Read = "crm.visit-notifications.read";
    public const string SettingsManage = "crm.visit-notifications.settings.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, SettingsManage };
}

public static class VisitNotificationErrors
{
    public const string Invalid = "visit_notification_invalid";        // 400
    public const string NotFound = "visit_notification_not_found";     // 404
    public const string EmailInvalid = "visit_notification_email_invalid"; // 400
}

/// <summary>The scheduler's and the sender's numbers (K-N7 / K-N8).</summary>
public static class VisitNotificationLimits
{
    public const string SourceModule = "MOD-0155";
    public const int MaxAttempts = 5;

    /// <summary>The wait after the n-th failure: 5, 10, 20, 40 minutes.</summary>
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromMinutes(5 * Math.Pow(2, Math.Max(0, attempts - 1)));

    /// <summary>A queued row older than this lost its sender (the process died between insert and send).</summary>
    public static readonly TimeSpan StaleQueued = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(2);

    /// <summary>The overdue sweep looks at plans of the last N days (deadline passed) — never the whole history.</summary>
    public const int OverdueLookbackDays = 7;

    public const int MaxClaimsPerTick = 50;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}

// ── the Platform internal dispatch (the PLT-NOTIFY contract, K-N1) ─────────────────────────────────────────────────

/// <summary>A recipient on the wire: exactly one of <see cref="UserId"/>, <see cref="LineManagerOf"/>, <see cref="Email"/>.</summary>
public sealed record PlatformNotificationRecipient(string? UserId = null, string? LineManagerOf = null, string? Email = null,
    string? DisplayName = null, string? Locale = null);

public sealed record PlatformNotificationRequest(
    Guid TenantId, string EventCode, string SourceModule, IReadOnlyList<PlatformNotificationRecipient> To,
    IReadOnlyList<PlatformNotificationRecipient> Cc, IReadOnlyDictionary<string, string> Variables, string CorrelationId,
    string? CausationId = null,
    /// <summary>W5-FIX (contract annex) — the user a reply goes to (the representative); the Platform resolves the address.
    /// Null — not a user id — and the field is not sent at all.</summary>
    string? ReplyToUserId = null);

public sealed record PlatformResolvedRecipient(string? UserId, string Via);

/// <summary><paramref name="Available"/> false = the Platform could not be asked / refused (the row fails, retried).
/// <paramref name="Status"/> = the Platform's answer (<c>no-recipient</c> = nobody resolved; anything else = handed over).</summary>
public sealed record PlatformNotificationResult(
    bool Available, string? Status, IReadOnlyList<string> DispatchIds, IReadOnlyList<PlatformResolvedRecipient> To,
    IReadOnlyList<PlatformResolvedRecipient> Cc, string? Error = null)
{
    public static PlatformNotificationResult Unavailable(string error) => new(false, null, [], [], [], error);
}

/// <summary>WP-VW-W5 (K-N1 / K-N8) — <c>POST api/internal/notifications/events/dispatch</c> on the Platform directly (not
/// the Gateway), with the internal API key — works without a request (the scheduler). Never throws: every failure is
/// "unavailable".</summary>
public interface IPlatformNotificationClient
{
    Task<PlatformNotificationResult> DispatchAsync(PlatformNotificationRequest request, CancellationToken cancellationToken);
}

// ── the sender ────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Queues a notification once per subject and sends it; sends (again) a row the scheduler claimed.</summary>
public sealed class VisitNotificationSender
{
    private readonly IVisitNotificationRepository _rows;
    private readonly IVisitNotificationSettingsRepository _settings;
    private readonly IPlatformNotificationClient _platform;
    private readonly TimeProvider _clock;

    public VisitNotificationSender(IVisitNotificationRepository rows, IVisitNotificationSettingsRepository settings,
        IPlatformNotificationClient platform, TimeProvider? clock = null)
    {
        _rows = rows;
        _settings = settings;
        _platform = platform;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Inserts the row (the database refuses a second one for the same subject → nothing sent) and sends it.
    /// True when this call queued it.</summary>
    public async Task<bool> QueueAndSendAsync(VisitNotification row, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        row.CreatedAt = now;
        row.CreatedTicks = now.UtcTicks;
        row.Status = VisitNotificationStatuses.Queued;
        row.CorrelationId = string.IsNullOrEmpty(row.CorrelationId) ? CorrelationOf(row) : row.CorrelationId;

        // W5-FIX — a report already mailed under the event's legacy (hyphenated) code is not mailed again under today's:
        // the unique index sees two different codes, so the old spelling is asked for here.
        foreach (var alias in VisitNotificationEvents.Aliases(row.EventCode).Where(code => code != row.EventCode))
        {
            if (await _rows.ExistsAsync(row.TenantId, alias, row.SubjectId, ct))
            {
                return false;
            }
        }

        if (!await _rows.TryInsertAsync(row, ct))
        {
            return false;
        }

        await SendAsync(row, ct);
        return true;
    }

    /// <summary>One send attempt: the recipients built from the spec (the mailboxes read from the settings NOW), the
    /// Platform's answer written to the row. A failure counts an attempt and schedules the next one (at most
    /// <see cref="VisitNotificationLimits.MaxAttempts"/>).</summary>
    public async Task SendAsync(VisitNotification row, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(row.TenantId, ct);
        var (to, cc, notes) = Build(row.Recipients, settings);
        foreach (var note in notes.Where(n => !row.Notes.Contains(n)))
        {
            row.Notes.Add(note);
        }

        var version = row.Version;
        var now = _clock.GetUtcNow();
        row.Attempts++;
        var result = to.Count == 0 && cc.Count == 0
            ? new PlatformNotificationResult(true, VisitNotificationStatuses.NoRecipient, [], [], [])
            // W5-FIX — always today's code (a legacy row retried by the scheduler would be refused by the catalogue), and the
            // reply goes to the representative when the row's representative is a user id.
            : await _platform.DispatchAsync(new PlatformNotificationRequest(row.TenantId, VisitNotificationEvents.Canonical(row.EventCode),
                VisitNotificationLimits.SourceModule, to, cc, row.Variables, row.CorrelationId,
                ReplyToUserId: ReplyToOf(row)), ct);
        Apply(row, result, now);
        row.ClaimedUntilTicks = 0;
        row.ClaimedBy = null;
        row.UpdatedAt = now;
        await _rows.ReplaceAsync(row, version, ct);
    }

    /// <summary>Pure: the Platform's answer onto the row.</summary>
    public static void Apply(VisitNotification row, PlatformNotificationResult result, DateTimeOffset now)
    {
        if (!result.Available)
        {
            row.Status = VisitNotificationStatuses.Failed;
            row.LastError = result.Error ?? VisitNotificationNotes.PlatformUnavailable;
            row.NextAttemptTicks = row.Attempts >= VisitNotificationLimits.MaxAttempts
                ? null
                : (now + VisitNotificationLimits.Backoff(row.Attempts)).UtcTicks;
            if (row.NextAttemptTicks is null && !row.Notes.Contains(VisitNotificationNotes.AttemptsExhausted))
            {
                row.Notes.Add(VisitNotificationNotes.AttemptsExhausted);
            }

            return;
        }

        row.NextAttemptTicks = null;
        row.LastError = null;
        row.DispatchIds = result.DispatchIds.ToList();
        row.Resolved = result.To.Select(r => new VisitNotificationResolvedRecipient { Role = VisitNotificationRoles.To, UserId = r.UserId, Via = r.Via })
            .Concat(result.Cc.Select(r => new VisitNotificationResolvedRecipient { Role = VisitNotificationRoles.Cc, UserId = r.UserId, Via = r.Via }))
            .ToList();
        if (string.Equals(result.Status, VisitNotificationStatuses.NoRecipient, StringComparison.OrdinalIgnoreCase))
        {
            row.Status = VisitNotificationStatuses.NoRecipient;
            return;
        }

        row.SentAt = now;
        row.Status = row.Notes.Any(n => n is VisitNotificationNotes.PvRoutePending or VisitNotificationNotes.MslRoutePending)
            ? VisitNotificationStatuses.RoutePending
            : VisitNotificationStatuses.Sent;
    }

    /// <summary>Pure (K-N5): the spec → the wire recipients. A mailbox whose address is not set is dropped with its
    /// route-pending note; when nobody is left in "to", the "cc" moves up (the line manager gets it).</summary>
    public static (List<PlatformNotificationRecipient> To, List<PlatformNotificationRecipient> Cc, List<string> Notes) Build(
        IReadOnlyList<VisitNotificationRecipient> spec, VisitNotificationSettings? settings)
    {
        var to = new List<PlatformNotificationRecipient>();
        var cc = new List<PlatformNotificationRecipient>();
        var notes = new List<string>();
        foreach (var r in spec)
        {
            PlatformNotificationRecipient? wire = r.Kind switch
            {
                VisitNotificationRecipientKinds.LineManager when !string.IsNullOrWhiteSpace(r.UserId) => new(LineManagerOf: r.UserId),
                VisitNotificationRecipientKinds.User when !string.IsNullOrWhiteSpace(r.UserId) => new(UserId: r.UserId),
                VisitNotificationRecipientKinds.Mailbox => MailboxOf(r.Mailbox, settings, notes),
                _ => null
            };
            if (wire is not null)
            {
                (r.Role == VisitNotificationRoles.Cc ? cc : to).Add(wire);
            }
        }

        if (to.Count == 0 && cc.Count > 0)
        {
            to.AddRange(cc);
            cc.Clear();
        }

        return (to, cc, notes);
    }

    private static PlatformNotificationRecipient? MailboxOf(string? mailbox, VisitNotificationSettings? settings, List<string> notes)
    {
        var (address, pending) = mailbox == VisitNotificationMailboxes.Pv
            ? (settings?.PvEmail, VisitNotificationNotes.PvRoutePending)
            : (settings?.MslEmail, VisitNotificationNotes.MslRoutePending);
        if (string.IsNullOrWhiteSpace(address))
        {
            notes.Add(pending);
            return null;
        }

        return new PlatformNotificationRecipient(Email: address.Trim());
    }

    /// <summary>W5-FIX — every visit e-mail (summary, adverse event, off-label, overdue) is answered to the representative
    /// (K-N4); a representative id that is not a user id (Guid) sends no reply address.</summary>
    public static string? ReplyToOf(VisitNotification row)
        => Guid.TryParse(row.ResourceId, out var userId) && userId != Guid.Empty ? userId.ToString("D") : null;

    /// <summary>Deterministic: the same subject's event always carries the same correlation id (an idempotency hint).</summary>
    public static string CorrelationOf(VisitNotification row) => $"crm:{row.EventCode}:{row.SubjectId:D}";
}

// ── the visit report e-mails (on submit) and the overdue reminder ──────────────────────────────────────────────────

/// <summary>The report's e-mails on submit — called AFTER the report is stored, by both submit paths.</summary>
public interface IVisitReportNotifier
{
    /// <summary>Never throws: a failure is logged and left to the scheduler (K-N8).</summary>
    Task ReportSubmittedAsync(Guid tenantId, PlannedVisitEntity plan, VisitReportEntity report, CancellationToken ct);

    /// <summary>The same, reading the stored report and its plan first (the legacy submit path, which returns an id).</summary>
    Task ReportSubmittedAsync(Guid tenantId, Guid reportId, CancellationToken ct);

    Task ReportOverdueAsync(Guid tenantId, PlannedVisitEntity plan, CancellationToken ct);
}

public sealed class VisitReportNotifier : IVisitReportNotifier
{
    private readonly VisitNotificationSender _sender;
    private readonly IVisitNotificationSettingsRepository _settings;
    private readonly IContactRepository _contacts;
    private readonly IAccountRepository _accounts;
    private readonly IVisitNotificationLinks _links;
    private readonly IVisitReportRepository _reports;
    private readonly IPlannedVisitRepository _plans;
    private readonly ILogger<VisitReportNotifier> _logger;

    public VisitReportNotifier(VisitNotificationSender sender, IVisitNotificationSettingsRepository settings, IContactRepository contacts,
        IAccountRepository accounts, IVisitNotificationLinks links, IVisitReportRepository reports, IPlannedVisitRepository plans,
        ILogger<VisitReportNotifier>? logger = null)
    {
        _sender = sender;
        _settings = settings;
        _contacts = contacts;
        _accounts = accounts;
        _links = links;
        _reports = reports;
        _plans = plans;
        _logger = logger ?? NullLogger<VisitReportNotifier>.Instance;
    }

    public async Task ReportSubmittedAsync(Guid tenantId, Guid reportId, CancellationToken ct)
    {
        try
        {
            var report = await _reports.GetByIdAsync(tenantId, reportId, ct);
            var plan = report is null ? null : await _plans.GetByIdAsync(tenantId, report.PlannedVisitId, ct);
            if (report is not null && plan is not null)
            {
                await ReportSubmittedAsync(tenantId, plan, report, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Visit report notification failed for report {ReportId}; the report stands.", reportId);
        }
    }

    public async Task ReportSubmittedAsync(Guid tenantId, PlannedVisitEntity plan, VisitReportEntity report, CancellationToken ct)
    {
        try
        {
            // only a submitted COMPLETED visit is a report summary (a "not done" / rescheduled outcome is not)
            if (!report.IsSubmitted() || !report.IsCompleted() || report.TenantId != tenantId || plan.TenantId != tenantId)
            {
                return;
            }

            var settings = await _settings.GetAsync(tenantId, ct);
            var rep = RepOf(plan, report);
            var common = await CommonAsync(tenantId, plan, rep, ct);
            var products = Products(plan, report);
            if (settings?.ReportSummary ?? true)
            {
                var vars = new Dictionary<string, string>(common, StringComparer.Ordinal)
                {
                    ["products"] = products,
                    ["note"] = report.General?.GeneralNote?.Trim() ?? string.Empty,
                    ["adverseEvent"] = Bool(report.Safety?.AdverseEvent),
                    ["offLabelRequest"] = Bool(report.Safety?.OffLabelRequest),
                    ["followUpDate"] = report.FollowUp?.SuggestedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty
                };
                await _sender.QueueAndSendAsync(Row(tenantId, VisitNotificationEvents.ReportSubmitted, report.Id, plan, report, rep, vars,
                    [LineManager(VisitNotificationRoles.To, rep)]), ct);
            }

            if (report.Safety?.AdverseEvent == true)
            {
                var vars = new Dictionary<string, string>(common, StringComparer.Ordinal)
                {
                    ["products"] = products,
                    ["adverseEventNote"] = report.Safety.AdverseEventNote?.Trim() ?? string.Empty
                };
                await _sender.QueueAndSendAsync(Row(tenantId, VisitNotificationEvents.AdverseEvent, report.Id, plan, report, rep, vars,
                    [Mailbox(VisitNotificationMailboxes.Pv), LineManager(VisitNotificationRoles.Cc, rep)]), ct);
            }

            // the off-label question is a doctor report's (a pharmacy report carries none)
            if (report.Safety?.OffLabelRequest == true)
            {
                var vars = new Dictionary<string, string>(common, StringComparer.Ordinal) { ["products"] = products };
                await _sender.QueueAndSendAsync(Row(tenantId, VisitNotificationEvents.OffLabelRequest, report.Id, plan, report, rep, vars,
                    [Mailbox(VisitNotificationMailboxes.Msl), LineManager(VisitNotificationRoles.Cc, rep)]), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // K-N8 — the report is already stored; the e-mail is a side effect (a row left queued is retried)
            _logger.LogWarning(ex, "Visit report notification failed for report {ReportId}; the report stands.", report.Id);
        }
    }

    public async Task ReportOverdueAsync(Guid tenantId, PlannedVisitEntity plan, CancellationToken ct)
    {
        try
        {
            var rep = plan.Resource.ResourceId;
            var vars = new Dictionary<string, string>(await CommonAsync(tenantId, plan, rep, ct), StringComparer.Ordinal)
            {
                ["deadline"] = VisitReportDeadline.For(plan.PlannedDate).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            };
            await _sender.QueueAndSendAsync(Row(tenantId, VisitNotificationEvents.ReportOverdue, plan.Id, plan, null, rep, vars,
                [LineManager(VisitNotificationRoles.To, rep)]), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Visit overdue notification failed for plan {PlanId}.", plan.Id);
        }
    }

    private static string RepOf(PlannedVisitEntity plan, VisitReportEntity report)
        => string.IsNullOrWhiteSpace(report.ReportedByResourceId) ? plan.Resource.ResourceId : report.ReportedByResourceId;

    /// <summary>The variables every visit e-mail carries: the rep, the doctor / institution (names only — no patient data),
    /// the visit date and the Web link.</summary>
    private async Task<Dictionary<string, string>> CommonAsync(Guid tenantId, PlannedVisitEntity plan, string rep, CancellationToken ct)
    {
        var contact = plan.ContactId is { } c ? await _contacts.GetByIdAsync(tenantId, c, ct) : null;
        var accountId = plan.AccountId ?? (plan.ContactId is null ? plan.TargetId : (Guid?)null);
        var account = accountId is { } a ? await _accounts.GetByIdAsync(tenantId, a, ct) : null;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["repName"] = string.IsNullOrWhiteSpace(plan.Resource.DisplayName) ? rep : plan.Resource.DisplayName.Trim(),
            ["doctorName"] = contact is null ? string.Empty : NameOf(contact),
            ["institutionName"] = account?.AccountName?.Trim() ?? string.Empty,
            ["visitDate"] = plan.PlannedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["reportUrl"] = _links.ReportUrl(plan.Id)
        };
    }

    private static string NameOf(Domain.Entities.Contact c)
        => !string.IsNullOrWhiteSpace(c.DisplayName) ? c.DisplayName.Trim()
            : string.Join(" ", new[] { c.ProfessionalTitle, c.FirstName, c.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

    /// <summary>The report's products by their code (the plan's content items carry it), comma-separated.</summary>
    private static string Products(PlannedVisitEntity plan, VisitReportEntity report)
    {
        var codes = (plan.ContentItems ?? []).Where(i => !string.IsNullOrWhiteSpace(i.ProductCode))
            .GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.First().ProductCode!.Trim());
        return string.Join(", ", report.ProductSections.Select(s => codes.TryGetValue(s.ProductId, out var code) ? code : s.ProductId.ToString("D")).Distinct());
    }

    private static string Bool(bool? value) => value == true ? "true" : "false";

    private static VisitNotificationRecipient LineManager(string role, string userId)
        => new() { Role = role, Kind = VisitNotificationRecipientKinds.LineManager, UserId = userId };

    private static VisitNotificationRecipient Mailbox(string mailbox)
        => new() { Role = VisitNotificationRoles.To, Kind = VisitNotificationRecipientKinds.Mailbox, Mailbox = mailbox };

    private static VisitNotification Row(Guid tenantId, string eventCode, Guid subject, PlannedVisitEntity plan, VisitReportEntity? report,
        string rep, Dictionary<string, string> vars, List<VisitNotificationRecipient> recipients) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, EventCode = eventCode, SubjectId = subject, PlannedVisitId = plan.Id, VisitReportId = report?.Id,
        ResourceId = rep, VisitDate = plan.PlannedDate, Variables = vars, Recipients = recipients
    };
}

/// <summary>The Web links an e-mail carries (configured base URL + the page path).</summary>
public interface IVisitNotificationLinks
{
    string ReportUrl(Guid plannedVisitId);
}

// ── the scheduler's work (K-N7) ───────────────────────────────────────────────────────────────────────────────────

/// <summary>One tick of the scheduler: the overdue sweep, then the due retries. The hosted service calls it on its timer;
/// tests call it with their own clock.</summary>
public sealed class VisitNotificationWorker
{
    private readonly IVisitNotificationRepository _rows;
    private readonly IVisitNotificationSettingsRepository _settings;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IVisitReportNotifier _notifier;
    private readonly VisitNotificationSender _sender;
    private readonly ILogger<VisitNotificationWorker> _logger;

    public VisitNotificationWorker(IVisitNotificationRepository rows, IVisitNotificationSettingsRepository settings, IPlannedVisitRepository plans,
        IVisitReportRepository reports, IVisitReportNotifier notifier, VisitNotificationSender sender, ILogger<VisitNotificationWorker>? logger = null)
    {
        _rows = rows;
        _settings = settings;
        _plans = plans;
        _reports = reports;
        _notifier = notifier;
        _sender = sender;
        _logger = logger ?? NullLogger<VisitNotificationWorker>.Instance;
    }

    public sealed record TickResult(int Overdue, int Retried);

    public async Task<TickResult> RunOnceAsync(DateTimeOffset now, string workerId, CancellationToken ct)
    {
        var overdue = 0;
        foreach (var tenantId in await _settings.ListTenantsWithOverdueRemindersAsync(ct))
        {
            try
            {
                overdue += await SweepOverdueAsync(tenantId, now, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Overdue sweep failed for tenant {TenantId}.", tenantId);
            }
        }

        var retried = 0;
        for (var i = 0; i < VisitNotificationLimits.MaxClaimsPerTick; i++)
        {
            var row = await _rows.ClaimNextAsync(now.UtcTicks, (now - VisitNotificationLimits.StaleQueued).UtcTicks,
                (now + VisitNotificationLimits.ClaimFor).UtcTicks, workerId, VisitNotificationLimits.MaxAttempts, ct);
            if (row is null)
            {
                break;
            }

            await _sender.SendAsync(row, ct);
            retried++;
        }

        return new TickResult(overdue, retried);
    }

    /// <summary>The tenant's visits whose 48-hour report window closed in the last
    /// <see cref="VisitNotificationLimits.OverdueLookbackDays"/> days with no submitted report (the work status
    /// <c>expired</c> — <see cref="VisitWorkStatus.NeedsManagerAttention"/>); each one reminds once (the log's key).</summary>
    public async Task<int> SweepOverdueAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var plans = (await _plans.ListByDateRangeAsync(tenantId, today.AddDays(-VisitNotificationLimits.OverdueLookbackDays), today, ct))
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActivePlan() && VisitReportDeadline.IsPassed(p.PlannedDate, now))
            .ToList();
        if (plans.Count == 0)
        {
            return 0;
        }

        var reports = (await _reports.ListByPlannedVisitIdsAsync(tenantId, plans.Select(p => p.Id).ToList(), ct))
            .GroupBy(r => r.PlannedVisitId).ToDictionary(g => g.Key, g => g.First());
        var count = 0;
        foreach (var plan in plans)
        {
            if (VisitWorkStatus.NeedsManagerAttention(VisitWorkStatus.Derive(plan, reports.GetValueOrDefault(plan.Id), now)))
            {
                await _notifier.ReportOverdueAsync(tenantId, plan, ct);
                count++;
            }
        }

        return count;
    }
}

// ── the settings (K-N5) ───────────────────────────────────────────────────────────────────────────────────────────

public sealed record VisitNotificationSettingsDto(
    string? PvEmail, string? MslEmail, bool OverdueReminders, bool ReportSummary, int Version, bool IsDefault,
    DateTimeOffset? UpdatedAt, string? UpdatedBy);

public sealed record VisitNotificationSettingsInput(
    string? PvEmail, string? MslEmail, bool? OverdueReminders, bool? ReportSummary, int? ExpectedVersion);

public sealed record GetVisitNotificationSettingsQuery : IRequest<Response<VisitNotificationSettingsDto>>;

public sealed record UpdateVisitNotificationSettingsCommand(VisitNotificationSettingsInput Input)
    : IRequest<Response<VisitNotificationSettingsDto>>, IAuditableCommand, IAuditDetailsProvider
{
    /// <summary>WP-VW-W6 F8-a (AUD-001 K4, path b) — the tenant's notification settings (MOD-0155, W5): one per tenant.</summary>
    public AuditDescriptor DescribeAudit()
        => new(AuditCategory.Crm, AuditOperation.Update, VisitNotificationSettingsHandlers.AuditEntity, "MOD-0155");

    /// <summary>The NAMES of the fields the request sets — a mailbox is personal data and never enters the record.</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Set("expectedVersion", Input?.ExpectedVersion);
        if (Input is { } input)
        {
            if (input.PvEmail is not null) details.Changed(nameof(input.PvEmail));
            if (input.MslEmail is not null) details.Changed(nameof(input.MslEmail));
            if (input.OverdueReminders is not null) details.Changed(nameof(input.OverdueReminders));
            if (input.ReportSummary is not null) details.Changed(nameof(input.ReportSummary));
        }

        details.ObjectVersion = (response as Response<VisitNotificationSettingsDto>)?.Data?.Version;
    }
}

public static class VisitNotificationRules
{
    public const int MaxEmailLength = 254;

    /// <summary>One mailbox address (no display name, no list): null / blank = not set.</summary>
    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var v = value.Trim();
        if (v.Length > MaxEmailLength || v.Contains(' ') || v.Contains(',') || v.Contains(';'))
        {
            return false;
        }

        try
        {
            var parsed = new System.Net.Mail.MailAddress(v);
            var at = v.LastIndexOf('@');
            return string.Equals(parsed.Address, v, StringComparison.Ordinal) && at > 0 && v.IndexOf('.', at) > at + 1 && !v.EndsWith('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string? Norm(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static VisitNotificationSettingsDto ToDto(VisitNotificationSettings? s)
        => s is null
            ? new VisitNotificationSettingsDto(null, null, true, true, 0, true, null, null)
            : new VisitNotificationSettingsDto(s.PvEmail, s.MslEmail, s.OverdueReminders, s.ReportSummary, s.Version, false, s.UpdatedAt, s.UpdatedBy);
}

public sealed class VisitNotificationSettingsHandlers :
    IRequestHandler<GetVisitNotificationSettingsQuery, Response<VisitNotificationSettingsDto>>,
    IRequestHandler<UpdateVisitNotificationSettingsCommand, Response<VisitNotificationSettingsDto>>
{
    public const string AuditEvent = "crm.visit-notifications.settings.written";
    public const string AuditEntity = "VisitNotificationSettings";

    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitNotificationSettingsRepository _settings;
    private readonly IVisitEvidenceAuditPublisher _audit;
    private readonly TimeProvider _clock;

    public VisitNotificationSettingsHandlers(ITenantContext tenant, IActorContext actor, IVisitNotificationSettingsRepository settings,
        IVisitEvidenceAuditPublisher audit, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _settings = settings;
        _audit = audit;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<VisitNotificationSettingsDto>> Handle(GetVisitNotificationSettingsQuery request, CancellationToken ct)
        => _tenant.TenantId is not { } tenantId
            ? Response<VisitNotificationSettingsDto>.Fail("Tenant context is required.", 400)
            : Response<VisitNotificationSettingsDto>.Success(VisitNotificationRules.ToDto(await _settings.GetAsync(tenantId, ct)));

    /// <summary>PUT: absent fields keep their value; an e-mail field sent as "" clears it. Versioned (409); audited.</summary>
    public async Task<Response<VisitNotificationSettingsDto>> Handle(UpdateVisitNotificationSettingsCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitNotificationSettingsDto>.Fail("Tenant context is required.", 400);
        }

        var input = request.Input;
        if (!VisitNotificationRules.IsValidEmail(input.PvEmail) || !VisitNotificationRules.IsValidEmail(input.MslEmail))
        {
            return WorkflowApprovalRules.Fail<VisitNotificationSettingsDto>(VisitNotificationErrors.EmailInvalid,
                $"pvEmail / mslEmail must each be one e-mail address (≤ {VisitNotificationRules.MaxEmailLength}).", 400);
        }

        var stored = await _settings.GetAsync(tenantId, ct);
        if (stored is not null && stored.TenantId != tenantId)
        {
            stored = null;
        }

        if (stored is null ? input.ExpectedVersion is { } e && e != 0 : input.ExpectedVersion != stored.Version)
        {
            return Conflict();
        }

        var settings = stored ?? new VisitNotificationSettings { Id = Guid.NewGuid(), TenantId = tenantId };
        var version = settings.Version;
        if (input.PvEmail is not null) settings.PvEmail = VisitNotificationRules.Norm(input.PvEmail);
        if (input.MslEmail is not null) settings.MslEmail = VisitNotificationRules.Norm(input.MslEmail);
        if (input.OverdueReminders is { } overdue) settings.OverdueReminders = overdue;
        if (input.ReportSummary is { } summary) settings.ReportSummary = summary;
        settings.UpdatedAt = _clock.GetUtcNow();
        settings.UpdatedBy = _actor.ActorName;

        var written = stored is null ? await _settings.InsertAsync(settings, ct) : await _settings.ReplaceAsync(settings, version, ct);
        if (!written)
        {
            return Conflict();
        }

        // the audit detail says WHICH mailbox is set, never the address
        await _audit.PublishAsync(AuditEvent, tenantId, AuditEntity, settings.Id, settings.Version,
            $"pvEmail={(settings.PvEmail is null ? "unset" : "set")};mslEmail={(settings.MslEmail is null ? "unset" : "set")};" +
            $"overdueReminders={settings.OverdueReminders.ToString().ToLowerInvariant()};reportSummary={settings.ReportSummary.ToString().ToLowerInvariant()}", ct);
        return Response<VisitNotificationSettingsDto>.Success(VisitNotificationRules.ToDto(settings));
    }

    private static Response<VisitNotificationSettingsDto> Conflict()
        => Response<VisitNotificationSettingsDto>.Fail(
            new[] { "The settings changed since they were loaded. Reload and try again.", Diten.CrmService.Application.Features.VisitExecution.VisitExecutionErrorCodes.ConcurrencyConflict }, 409);
}

// ── the list (K-N9) ───────────────────────────────────────────────────────────────────────────────────────────────

public sealed record VisitNotificationRecipientDto(string Role, string Kind, string? UserId, string? Mailbox);

public sealed record VisitNotificationResolvedDto(string Role, string? UserId, string Via);

public sealed record VisitNotificationRowDto(
    Guid Id, string EventCode, string Status, Guid PlannedVisitId, Guid? VisitReportId, string ResourceId, string? ResourceName,
    DateOnly VisitDate, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, int Attempts, IReadOnlyList<string> Notes);

public sealed record VisitNotificationPageDto(IReadOnlyList<VisitNotificationRowDto> Items, long Total, int Page, int PageSize);

/// <summary>The preview: the event, the status, who it was for (spec + what the Platform resolved — no address) and the
/// template variables.</summary>
public sealed record VisitNotificationDetailDto(
    VisitNotificationRowDto Row, IReadOnlyList<VisitNotificationRecipientDto> Recipients, IReadOnlyList<VisitNotificationResolvedDto> Resolved,
    IReadOnlyDictionary<string, string> Variables, string? LastError);

public sealed record ListVisitNotificationsQuery(
    DateOnly? From = null, DateOnly? To = null, string? EventCode = null, string? Status = null, string? ResourceId = null,
    int? Page = null, int? PageSize = null) : IRequest<Response<VisitNotificationPageDto>>;

public sealed record GetVisitNotificationQuery(Guid Id) : IRequest<Response<VisitNotificationDetailDto>>;

public sealed class VisitNotificationQueryHandlers :
    IRequestHandler<ListVisitNotificationsQuery, Response<VisitNotificationPageDto>>,
    IRequestHandler<GetVisitNotificationQuery, Response<VisitNotificationDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IVisitNotificationRepository _rows;
    private readonly VisitEvidenceFlagScopeResolver _scope;
    private readonly TimeProvider _clock;

    public VisitNotificationQueryHandlers(ITenantContext tenant, ICallerScope caller, IVisitNotificationRepository rows,
        VisitEvidenceFlagScopeResolver scope, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _caller = caller;
        _rows = rows;
        _scope = scope;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>K-N9: the settings key sees the tenant; otherwise the caller's own visits + the caller's team's (the
    /// K-W7 team rule of the evidence flags).</summary>
    private async Task<VisitEvidenceFlagScope> ScopeAsync(Guid tenantId, CancellationToken ct)
        => await _scope.ResolveAsync(tenantId, _clock.GetUtcNow(), _caller.HasPermission(VisitNotificationPermissions.SettingsManage), ct);

    public async Task<Response<VisitNotificationPageDto>> Handle(ListVisitNotificationsQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitNotificationPageDto>.Fail("Tenant context is required.", 400);
        }

        if (request.PageSize is < 1 or > VisitNotificationLimits.MaxPageSize || request.Page is < 1
            || request.From is { } f && request.To is { } t && f > t
            || request.EventCode is { Length: > 0 } ev && !VisitNotificationEvents.All.Contains(VisitNotificationEvents.Canonical(ev.Trim()))
            || request.Status is { Length: > 0 } st && !VisitNotificationStatuses.All.Contains(st))
        {
            return WorkflowApprovalRules.Fail<VisitNotificationPageDto>(VisitNotificationErrors.Invalid,
                $"page ≥ 1, pageSize 1–{VisitNotificationLimits.MaxPageSize}, from ≤ to, eventCode / status from their lists.", 400);
        }

        var scope = await ScopeAsync(tenantId, ct);
        var resource = string.IsNullOrWhiteSpace(request.ResourceId) ? null : request.ResourceId.Trim();
        var page = request.Page ?? 1;
        var size = request.PageSize ?? VisitNotificationLimits.DefaultPageSize;
        if (resource is not null && !scope.Sees(resource))
        {
            return Response<VisitNotificationPageDto>.Success(new VisitNotificationPageDto([], 0, page, size)); // nothing outside the scope
        }

        var eventCode = NullIfEmpty(request.EventCode) is { } code ? VisitNotificationEvents.Canonical(code) : null;
        var filter = new VisitNotificationFilter(request.From, request.To, eventCode, NullIfEmpty(request.Status), resource,
            scope.All ? null : scope.Reps.ToList());
        var (items, total) = await _rows.ListAsync(tenantId, filter, (page - 1) * size, size, ct);
        return Response<VisitNotificationPageDto>.Success(new VisitNotificationPageDto(
            items.Where(r => r.TenantId == tenantId).Select(r => ToRow(r, scope)).ToList(), total, page, size));
    }

    public async Task<Response<VisitNotificationDetailDto>> Handle(GetVisitNotificationQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitNotificationDetailDto>.Fail("Tenant context is required.", 400);
        }

        var row = await _rows.GetByIdAsync(tenantId, request.Id, ct);
        var scope = await ScopeAsync(tenantId, ct);
        if (row is null || row.TenantId != tenantId || !scope.Sees(row.ResourceId))
        {
            return WorkflowApprovalRules.Fail<VisitNotificationDetailDto>(VisitNotificationErrors.NotFound, "Not found.", 404); // never a leak
        }

        return Response<VisitNotificationDetailDto>.Success(new VisitNotificationDetailDto(
            ToRow(row, scope),
            row.Recipients.Select(r => new VisitNotificationRecipientDto(r.Role, r.Kind, r.UserId, r.Mailbox)).ToList(),
            row.Resolved.Select(r => new VisitNotificationResolvedDto(r.Role, r.UserId, r.Via)).ToList(),
            row.Variables, row.LastError));
    }

    private static VisitNotificationRowDto ToRow(VisitNotification r, VisitEvidenceFlagScope scope)
        => new(r.Id, VisitNotificationEvents.Canonical(r.EventCode), r.Status, r.PlannedVisitId, r.VisitReportId, r.ResourceId,
            r.Variables.TryGetValue("repName", out var name) && !string.IsNullOrWhiteSpace(name) ? name : scope.NameOf(r.ResourceId), r.VisitDate,
            r.CreatedAt, r.SentAt, r.Attempts, r.Notes);

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
