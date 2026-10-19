namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W5 (W5-BE, K-N6) — ONE row of the CRM visit notification log (<c>crm_visit_notifications</c>): a visit report
/// e-mail (summary / adverse event / off-label request) or an overdue reminder. Two jobs: (a) once per subject — the
/// database refuses a second row for the same (tenant, event, subject), so the same report never sends the same e-mail
/// twice; (b) the source of the in-app "sent e-mails" list (the Platform's dispatch list is not open to tenants).
/// <para>No e-mail address is ever stored: the recipients are a SPEC (the rep's line manager, a tenant mailbox by name)
/// resolved by the Platform at send time, and the resolved summary keeps user ids + how they were found.</para>
/// <para>The queue fields (<see cref="NextAttemptTicks"/>, <see cref="ClaimedUntilTicks"/>, <see cref="CreatedTicks"/>)
/// are UTC ticks — scalar, so the scheduler's atomic claim can filter on them (a DateTimeOffset is a BSON array).</para>
/// </summary>
public sealed class VisitNotification : EntityBase
{
    /// <summary><see cref="VisitNotificationEvents"/>.</summary>
    public string EventCode { get; set; } = string.Empty;

    /// <summary>The report (summary / adverse / off-label) or the planned visit (overdue) — the once-per-subject key.</summary>
    public Guid SubjectId { get; set; }

    public Guid PlannedVisitId { get; set; }
    public Guid? VisitReportId { get; set; }

    /// <summary>The rep (the plan's resource = a user id).</summary>
    public string ResourceId { get; set; } = string.Empty;

    public DateOnly VisitDate { get; set; }

    /// <summary><see cref="VisitNotificationStatuses"/>.</summary>
    public string Status { get; set; } = VisitNotificationStatuses.Queued;

    /// <summary>Why a recipient is missing (<c>pv-route-pending</c>, <c>msl-route-pending</c>) or what the last failure was.</summary>
    public List<string> Notes { get; set; } = new();

    /// <summary>Who should get it — resolved by the Platform at send time (no address stored).</summary>
    public List<VisitNotificationRecipient> Recipients { get; set; } = new();

    /// <summary>What the Platform answered: who it resolved, and how (user id + via; never an address).</summary>
    public List<VisitNotificationResolvedRecipient> Resolved { get; set; } = new();

    /// <summary>The template variables (doctor / institution, visit date, rep, products, note, link — no patient data).</summary>
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.Ordinal);

    public List<string> DispatchIds { get; set; } = new();
    public string CorrelationId { get; set; } = string.Empty;

    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    public long CreatedTicks { get; set; }

    /// <summary>When a failed row may be tried again (null = never again: sent / no recipient / attempts exhausted).</summary>
    public long? NextAttemptTicks { get; set; }

    /// <summary>A claim: until these ticks the row belongs to <see cref="ClaimedBy"/> (another worker skips it).</summary>
    public long ClaimedUntilTicks { get; set; }
    public string? ClaimedBy { get; set; }
}

/// <summary>A recipient SPEC: the line manager of a user, a user, or a tenant mailbox by name (its address read from the
/// tenant settings at send time).</summary>
public sealed class VisitNotificationRecipient
{
    /// <summary><c>to</c> | <c>cc</c>.</summary>
    public string Role { get; set; } = VisitNotificationRoles.To;

    /// <summary><see cref="VisitNotificationRecipientKinds"/>.</summary>
    public string Kind { get; set; } = VisitNotificationRecipientKinds.LineManager;

    /// <summary>The user (for <c>line-manager</c>: whose manager; for <c>user</c>: the user).</summary>
    public string? UserId { get; set; }

    /// <summary>For <c>mailbox</c>: <c>pv</c> | <c>msl</c>.</summary>
    public string? Mailbox { get; set; }
}

public sealed class VisitNotificationResolvedRecipient
{
    public string Role { get; set; } = VisitNotificationRoles.To;
    public string? UserId { get; set; }

    /// <summary><c>direct</c> | <c>line-manager</c> | <c>manager-chain</c> | <c>email</c> (the Platform's words).</summary>
    public string Via { get; set; } = string.Empty;
}

/// <summary>WP-VW-W5 (K-N5) — the tenant's visit notification settings (<c>crm_visit_notification_settings</c>, one per
/// tenant; absent = the defaults). The PV / MSL mailboxes are the interim route until K-W8 is decided.</summary>
public sealed class VisitNotificationSettings : EntityBase
{
    public string? PvEmail { get; set; }
    public string? MslEmail { get; set; }

    /// <summary>The scheduler's "report overdue" reminder to the line manager.</summary>
    public bool OverdueReminders { get; set; } = true;

    /// <summary>The report summary to the line manager on submit (adverse event / off-label always go: safety).</summary>
    public bool ReportSummary { get; set; } = true;

    public string? UpdatedBy { get; set; }
}

public static class VisitNotificationEvents
{
    public const string ReportSubmitted = "crm.visit-report.submitted";
    public const string AdverseEvent = "crm.visit-report.adverse-event";
    public const string OffLabelRequest = "crm.visit-report.off-label-request";
    public const string ReportOverdue = "crm.visit-report.overdue";

    public static readonly IReadOnlyList<string> All = new[] { ReportSubmitted, AdverseEvent, OffLabelRequest, ReportOverdue };
}

public static class VisitNotificationStatuses
{
    public const string Queued = "queued";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string NoRecipient = "no-recipient";

    /// <summary>Sent to the line manager only: the PV / MSL mailbox is not set yet (K-N5).</summary>
    public const string RoutePending = "route-pending";

    public static readonly IReadOnlyList<string> All = new[] { Queued, Sent, Failed, NoRecipient, RoutePending };
}

public static class VisitNotificationRoles
{
    public const string To = "to";
    public const string Cc = "cc";
}

public static class VisitNotificationRecipientKinds
{
    public const string LineManager = "line-manager";
    public const string User = "user";
    public const string Mailbox = "mailbox";
}

public static class VisitNotificationMailboxes
{
    public const string Pv = "pv";
    public const string Msl = "msl";
}

public static class VisitNotificationNotes
{
    public const string PvRoutePending = "pv-route-pending";
    public const string MslRoutePending = "msl-route-pending";
    public const string PlatformUnavailable = "platform-unavailable";
    public const string AttemptsExhausted = "attempts-exhausted";
}
