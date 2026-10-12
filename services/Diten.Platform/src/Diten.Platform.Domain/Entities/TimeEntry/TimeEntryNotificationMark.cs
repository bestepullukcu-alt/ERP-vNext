using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T3 (pack §21.3 N6) — "this notification was handed over once". Unique per (tenant, kind, key) and
/// claimed BEFORE the send: a crash after the claim loses one e-mail, it never duplicates one. <see cref="Kind"/> is the
/// event code; <see cref="Key"/> names what it was about (week revision and submission, person-week, meeting-person-status)
/// and who it went to. Never updated, never deleted.
/// </summary>
public sealed class TimeEntryNotificationMark : TenantScopedEntity
{
    public required string Kind { get; set; }

    public required string Key { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
