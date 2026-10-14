namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3 (BE-a) — <b>VisitExecution</b>: the record of the rep actually DOING a planned visit — start (server time,
/// location against the institution, optional photo), the live capture (time per product / slide, interest marks,
/// notes, co-visitors), the finish (server time) and the doctor's signature. ONE per planned visit (unique
/// <c>(TenantId, PlannedVisitId)</c>); a separate aggregate from the plan atom (never mutated here) and from the
/// <see cref="VisitReport"/> compliance record, which reads it (W1 / W2 rules: a started visit is not marked missed, an
/// unfinished one is not reported, the report's <c>ExecutedAt</c> is this <see cref="StartedAt"/>).
/// <para><b>Time.</b> <see cref="StartedAt"/> / <see cref="FinishedAt"/> are written by the SERVER clock only; a client
/// instant is never accepted. They are lone DateTimeOffsets — never index keys and never co-sorted (the CRM
/// parallel-arrays 500).</para>
/// <para><b>Evidence.</b> The photo and the signature bytes live in the MOD-0262-FU01 document repository (scope
/// <c>CrmVisitEvidence</c>); this record keeps only the content id, which is NEVER returned to a client.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.</para>
/// </summary>
public sealed class VisitExecution : EntityBase
{
    public Guid PlannedVisitId { get; set; }

    /// <summary>The rep who started the visit (the plan's resource = the caller).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary><see cref="VisitExecutionStatus"/> — <c>started</c> → <c>finished</c>, no reverse.</summary>
    public string Status { get; set; } = VisitExecutionStatus.Started;

    /// <summary><see cref="VisitExecutionKinds"/> — <c>in_person</c> | <c>remote</c>.</summary>
    public string Kind { get; set; } = VisitExecutionKinds.InPerson;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int? DurationSeconds { get; set; }

    public VisitExecutionLocation Location { get; set; } = new();
    public VisitExecutionEvidence? Photo { get; set; }
    public VisitExecutionSignature? Signature { get; set; }

    public List<VisitExecutionProductTiming> ProductTimings { get; set; } = new();
    public List<VisitExecutionSlideTiming> SlideTimings { get; set; } = new();
    public List<VisitExecutionMark> Marks { get; set; } = new();
    public string? Note { get; set; }

    /// <summary>The rep's own per-product notes: returned to their author only, empty to anyone else.</summary>
    public List<VisitExecutionPersonalNote> PersonalNotes { get; set; } = new();

    /// <summary>Tenant user ids (≤ 3, never the rep) who joined the visit — e.g. the manager on a joint visit.</summary>
    public List<string> CoVisitorUserIds { get; set; } = new();

    public bool ShortVisit { get; set; }

    /// <summary><see cref="VisitExecutionFlags"/> — the evidence warnings a manager sees.</summary>
    public List<string> Flags { get; set; } = new();

    /// <summary>WP-VW-W3 (W3-BE-c) — the approved, active time-boxed exception the start ran under (null = none) and the
    /// rules it relaxed (<see cref="VisitEvidenceExceptionRules"/>). Set by the start only.</summary>
    public Guid? ExceptionId { get; set; }
    public List<string> RelaxedRules { get; set; } = new();

    /// <summary>WP-VW-W3E (W3E-BE-a, §2.4) — the institution QR code at the start (null on a start before W3E).</summary>
    public VisitExecutionQr? Qr { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsStarted() => string.Equals(Status, VisitExecutionStatus.Started, StringComparison.Ordinal);
    public bool IsFinished() => string.Equals(Status, VisitExecutionStatus.Finished, StringComparison.Ordinal);
}

/// <summary>Where the rep was at the start, measured against the institution (Haversine).</summary>
public sealed class VisitExecutionLocation
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public int? DistanceMeters { get; set; }

    /// <summary>The radius in force at the start (a later policy change does not re-judge an old visit).</summary>
    public int RadiusMeters { get; set; }

    /// <summary><see cref="VisitLocationModes"/> in force at the start.</summary>
    public string Mode { get; set; } = VisitLocationModes.Required;

    /// <summary><see cref="VisitLocationStatus"/>.</summary>
    public string Status { get; set; } = VisitLocationStatus.Missing;

    /// <summary>The device's reason when there was no fix: <c>denied</c> | <c>unavailable</c> | <c>timeout</c>.</summary>
    public string? Error { get; set; }
}

/// <summary>The start photo: a pointer into the document repository. <see cref="ContentId"/> never leaves the server.</summary>
public sealed class VisitExecutionEvidence
{
    public Guid ContentId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public long ByteSize { get; set; }

    /// <summary>The device's capture instant (informative only; the start time is the server's).</summary>
    public DateTimeOffset? CapturedAt { get; set; }
}

/// <summary>The doctor's signature (png in the document repository). Taken once, after the finish.</summary>
public sealed class VisitExecutionSignature
{
    public Guid ContentId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public bool Confirmed { get; set; }

    /// <summary><see cref="VisitSignatureContexts"/> — <c>sample</c> when any product was marked
    /// <c>sample_request</c>, else <c>approval</c>.</summary>
    public string Context { get; set; } = VisitSignatureContexts.Approval;

    public DateTimeOffset SignedAt { get; set; }
}

public sealed class VisitExecutionProductTiming
{
    public Guid ProductId { get; set; }
    public int Seconds { get; set; }
}

public sealed class VisitExecutionSlideTiming
{
    public Guid ProductId { get; set; }
    public Guid StepId { get; set; }
    public int Seconds { get; set; }
}

public sealed class VisitExecutionMark
{
    public Guid ProductId { get; set; }

    /// <summary><see cref="VisitInterestValues"/> or null.</summary>
    public string? Interest { get; set; }

    public List<string> Flags { get; set; } = new();

    /// <summary>A value of the <c>visit-objection-category</c> reference set, or null.</summary>
    public string? ObjectionCategory { get; set; }
}

public sealed class VisitExecutionPersonalNote
{
    public Guid ProductId { get; set; }
    public string Text { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// In-domain, fail-closed vocabularies (published on the visit-execution contract).
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

public static class VisitExecutionStatus
{
    public const string Started = "started";
    public const string Finished = "finished";

    public static readonly IReadOnlyList<string> All = new[] { Started, Finished };
}

public static class VisitExecutionKinds
{
    public const string InPerson = "in_person";
    public const string Remote = "remote";

    public static readonly IReadOnlyList<string> All = new[] { InPerson, Remote };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

public static class VisitInterestValues
{
    public const string Interested = "interested";
    public const string Neutral = "neutral";
    public const string NotInterested = "not_interested";

    public static readonly IReadOnlyList<string> All = new[] { Interested, Neutral, NotInterested };
}

/// <summary>The per-product marks a rep sets during the presentation.</summary>
public static class VisitMarkFlags
{
    public const string Question = "question";
    public const string Objection = "objection";
    public const string SampleRequest = "sample_request";
    public const string LiteratureRequest = "literature_request";
    public const string FollowUp = "follow_up";

    public static readonly IReadOnlyList<string> All = new[] { Question, Objection, SampleRequest, LiteratureRequest, FollowUp };
}

/// <summary>The evidence warnings recorded on an execution (what a manager reviews).</summary>
public static class VisitExecutionFlags
{
    public const string LocationFar = "location_far";
    public const string LocationMissing = "location_missing";
    public const string LocationInaccurate = "location_inaccurate";
    public const string AccountLocationUnknown = "account_location_unknown";
    public const string ShortVisit = "short_visit";

    /// <summary>WP-VW-W3 (W3-BE-c) — the start ran under an approved time-boxed exception (see ExceptionId / RelaxedRules).</summary>
    public const string StartedWithException = "started_with_exception";

    /// <summary>WP-VW-W3E (W3E-BE-a, §2.4) — the QR verdicts that start with a flag (K-W3E-6 / K-W3E-7).</summary>
    public const string QrNotIssued = "qr_not_issued";
    public const string QrInvalid = "qr_invalid";
    public const string QrWrongAccount = "qr_wrong_account";
    public const string QrRevoked = "qr_revoked";
    public const string QrUnverifiable = "qr_unverifiable";

    public static readonly IReadOnlyList<string> All = new[]
    {
        LocationFar, LocationMissing, LocationInaccurate, AccountLocationUnknown, ShortVisit, StartedWithException,
        QrNotIssued, QrInvalid, QrWrongAccount, QrRevoked, QrUnverifiable
    };
}

public static class VisitLocationStatus
{
    public const string Ok = "ok";
    public const string Far = "far";
    public const string Missing = "missing";
    public const string Inaccurate = "inaccurate";
    public const string AccountUnknown = "account_unknown";
}

public static class VisitLocationErrors
{
    public const string Denied = "denied";
    public const string Unavailable = "unavailable";
    public const string Timeout = "timeout";

    public static readonly IReadOnlyList<string> All = new[] { Denied, Unavailable, Timeout };
}

public static class VisitSignatureContexts
{
    public const string Approval = "approval";
    public const string Sample = "sample";

    public static readonly IReadOnlyList<string> All = new[] { Approval, Sample };
}

/// <summary>Published ceilings of the execution write path.</summary>
public static class VisitExecutionLimits
{
    public const int MaxNoteLength = 2000;
    public const int MaxPersonalNoteLength = 1000;
    public const int MaxCoVisitors = 3;
    public const int MaxSeconds = 86400;

    /// <summary>The evidence file ceiling (the Platform CrmVisitEvidence scope rule: 10 MB).</summary>
    public const long MaxEvidenceBytes = 10L * 1024 * 1024;

    public const string ObjectionCategorySet = "visit-objection-category";
}
