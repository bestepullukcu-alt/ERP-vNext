using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Meetings;

/// <summary>
/// MOD-0357 S2 — the tenant setting a meeting is created against (pack §3/§4/§22 D1/D8).
/// </summary>
public sealed class MeetingType : TenantScopedEntity
{
    /// <summary>Trim, max 200, tenant-unique (enforced by a unique index AND a pre-check — pack §12).</summary>
    public required string Name { get; set; }

    /// <summary>Pre-fills a new meeting of this type's agenda; the instance stays independently editable
    /// afterward (K8) — no live binding back to the template. At most 20 lines, each at most 200 chars.</summary>
    public List<string> AgendaTemplate { get; set; } = [];

    /// <summary>FK → MOD-0024 <c>TaskType</c> — the type a meeting-born action defaults to (S4, not resolved
    /// here). Read-only reference: this slice never validates it against the Tasks task-type catalogue, since
    /// no meeting-born task is created in S2.</summary>
    public Guid? DefaultActionTaskTypeId { get; set; }

    /// <summary>Default <c>false</c>; the <c>MGMT-REVIEW</c> type is the one QA-set exception (§22 D1) — this
    /// slice does not seed that exception, it only carries the field and lets QA set it explicitly (K8).</summary>
    public bool IsQualityRecord { get; set; }

    /// <summary>Default <c>false</c> — the signing mechanism itself is deferred (pack §20).</summary>
    public bool RequiresESignature { get; set; }

    public bool AttendanceMandatory { get; set; }
}
