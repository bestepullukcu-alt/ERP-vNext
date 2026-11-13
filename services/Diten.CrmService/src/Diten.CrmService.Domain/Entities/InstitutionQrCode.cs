namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.2, K-W3E-1 / K-W3E-2) — <b>InstitutionQrCode</b>: ONE issuance of an institution's QR code. The
/// sticker at the institution carries a SIGNED, VERSIONED payload (<c>DQR1.&lt;account&gt;.&lt;version&gt;.&lt;keyId&gt;.&lt;sig&gt;</c>);
/// the payload itself is never stored (it is re-derived from the fields + the tenant key on print).
/// <para><b>One active code per institution</b> (partial unique index <c>(TenantId, AccountId)</c> where
/// <c>Status = "active"</c> — an equality filter, never <c>$ne</c>). "Regenerate" revokes the active row and inserts the
/// next version, so every older sticker answers <c>revoked</c> at once; "reprint" prints the same row. Revoked rows stay
/// (the history); there is no delete.</para>
/// <para><b>Version naming.</b> <see cref="CodeVersion"/> is the code's business version (1, 2, … per institution — the
/// <c>version</c> of the wire and of the payload); <see cref="EntityBase.Version"/> stays the technical concurrency token.</para>
/// <para>The <see cref="Account"/> aggregate gets no field: the code lives here, keyed by the account id.</para>
/// </summary>
public sealed class InstitutionQrCode : EntityBase
{
    public Guid AccountId { get; set; }

    /// <summary>The code's version for this institution (1-based, +1 per regenerate / re-issue after a revoke).</summary>
    public int CodeVersion { get; set; }

    /// <summary><see cref="InstitutionQrCodeStatuses"/>.</summary>
    public string Status { get; set; } = InstitutionQrCodeStatuses.Active;

    /// <summary>The signing key the payload was signed with (rotation: an old key keeps verifying its codes).</summary>
    public string KeyId { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }
    public string? IssuedBy { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevokeReason { get; set; }

    /// <summary>The last successful scan at a visit start (atomic, best effort — never blocks a start).</summary>
    public DateTimeOffset? LastScannedAt { get; set; }
    public int ScanCount { get; set; }

    public bool IsActive() => string.Equals(Status, InstitutionQrCodeStatuses.Active, StringComparison.Ordinal);
}

public static class InstitutionQrCodeStatuses
{
    public const string Active = "active";
    public const string Revoked = "revoked";

    /// <summary>The list filter only: an institution with no code at all (never stored).</summary>
    public const string None = "none";

    public static readonly IReadOnlyList<string> All = new[] { Active, Revoked };
}

/// <summary>WP-VW-W3E (§2.2) — one APPEND-ONLY row of an institution's QR history. Never updated, never deleted.</summary>
public sealed class InstitutionQrEvent : EntityBase
{
    public Guid AccountId { get; set; }

    /// <summary>The code the event is about (null for none).</summary>
    public Guid? QrCodeId { get; set; }

    /// <summary><see cref="InstitutionQrEventKinds"/>.</summary>
    public string Kind { get; set; } = InstitutionQrEventKinds.Issued;

    public int CodeVersion { get; set; }
    public DateTimeOffset At { get; set; }
    public string? By { get; set; }
    public string? Reason { get; set; }

    /// <summary>WP-VW-W3E (W3E-BE-c, D-4) — a <c>printed</c> event's paper (a4 | a5 | labels4 | labels8) and copies (1–50);
    /// null when the print did not say (an older client, every other kind).</summary>
    public string? Paper { get; set; }

    public int? Copies { get; set; }
}

public static class InstitutionQrEventKinds
{
    public const string Issued = "issued";
    public const string Regenerated = "regenerated";
    public const string Revoked = "revoked";
    public const string Printed = "printed";
    public const string BulkIssued = "bulk_issued";

    public static readonly IReadOnlyList<string> All = new[] { Issued, Regenerated, Revoked, Printed, BulkIssued };
}

/// <summary>WP-VW-W3E (§2.1, K-W3E-1) — the tenant's QR setting on the evidence policy.</summary>
public static class VisitQrModes
{
    public const string Off = "off";
    public const string Optional = "optional";
    public const string Required = "required";

    public static readonly IReadOnlyList<string> All = new[] { Off, Optional, Required };
}

/// <summary>WP-VW-W3E (§2.2) — the verdict of a scanned payload at a visit start (<c>VisitExecution.Qr.Status</c>).</summary>
public static class VisitQrScanStatuses
{
    public const string Ok = "ok";
    public const string Missing = "missing";
    public const string Invalid = "invalid";
    public const string WrongAccount = "wrong_account";
    public const string Revoked = "revoked";
    public const string NotIssued = "not_issued";
    public const string NotApplicable = "not_applicable";
    public const string Unverifiable = "unverifiable";

    public static readonly IReadOnlyList<string> All = new[] { Ok, Missing, Invalid, WrongAccount, Revoked, NotIssued, NotApplicable, Unverifiable };
}

/// <summary>How a scanned code matched the expected institution: the institution itself or one of its parents (K-W3E-5).</summary>
public static class VisitQrMatchedVia
{
    public const string Self = "self";
    public const string Parent = "parent";
}

/// <summary>WP-VW-W3E (§2.4) — the QR evidence of a visit start (embedded in <see cref="VisitExecution"/>).</summary>
public sealed class VisitExecutionQr
{
    /// <summary><see cref="VisitQrScanStatuses"/>.</summary>
    public string Status { get; set; } = VisitQrScanStatuses.NotApplicable;

    /// <summary>The institution of the scanned code (same tenant only; null when nothing valid was scanned).</summary>
    public Guid? AccountId { get; set; }

    public int? Version { get; set; }

    /// <summary>The client's scan instant — informative only (the server's StartedAt is the evidence).</summary>
    public DateTimeOffset? ScannedAt { get; set; }

    /// <summary><see cref="VisitQrMatchedVia"/> (only for <c>ok</c>).</summary>
    public string? MatchedVia { get; set; }
}
