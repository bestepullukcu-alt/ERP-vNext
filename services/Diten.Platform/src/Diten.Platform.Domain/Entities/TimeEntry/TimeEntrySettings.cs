using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.7) — one row per tenant. The time-admin pool is the last approver fallback (D6) and the
/// people who may reopen old weeks hold <c>time-entry.weeks.reopen</c>; the pool position only answers "who approves
/// when nobody up the chain can".
/// </summary>
public sealed class TimeEntrySettings : TenantScopedEntity
{
    public Guid? TimeAdminPoolPositionId { get; set; }

    /// <summary>T3 (pack §21.3 N3) — the Monday reminder for last week's missing timesheet. OFF until the time admin
    /// turns it on; a row written before T3 has no such field and reads as off.</summary>
    public bool WeeklyReminderEnabled { get; set; }
}

/// <summary>
/// MOD-0280-FU01 (pack §4.7, D12, R7) — the timer switch of one legal entity. NO ROW MEANS OFF, for every legal
/// entity, whatever its country: a default that needs no country lookup cannot be wrong by data. Switching on
/// requires a reason. T1a only stores it; the timer itself is T1b.
/// </summary>
public sealed class LegalEntityTimeSetting : TenantScopedEntity
{
    public required Guid LegalEntityId { get; set; }

    public bool TimerEnabled { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    public Guid ChangedByUserId { get; set; }

    /// <summary>Required when switching on; the legal basis the tenant admin recorded (§22).</summary>
    public string? Reason { get; set; }
}
