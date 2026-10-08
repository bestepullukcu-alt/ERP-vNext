using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.ESignature;

/// <summary>
/// MOD-0022 — ONE electronic signature (pack §4 "ESignatureRecord", Part 11 §11.50 / §11.70). INSERT-ONLY: every
/// property is init-only, the repository interface has no update / replace / delete member (architecture test), and an
/// invalidation is a separate <see cref="ESignatureInvalidation"/> row. Verification recomputes, it never writes back.
///
/// <para><see cref="SubjectHash"/> binds the record to the subject as the owning module's provider described it at the
/// moment of signing; <see cref="SignatureDigest"/> binds every field below (including the image hash, KS-10) together.
/// Unlike FU23 there is no status field to flip.</para>
/// </summary>
public sealed class ESignatureRecord : TenantScopedEntity
{
    public required string SignatureNumber { get; init; }

    // ── what was signed (snapshot from the provider; never from the client) ──────────────────────────────
    public required string Module { get; init; }
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string SubjectDisplay { get; init; }
    public List<ESignatureSummaryLine> SubjectSummary { get; init; } = [];
    public Guid? LegalEntityId { get; init; }
    public string? ActionCode { get; init; }
    public Dictionary<string, string?> Context { get; init; } = new();

    // ── meaning, as the signer saw it ────────────────────────────────────────────────────────────────────
    public required string MeaningCode { get; init; }
    public required string MeaningTextAtSign { get; init; }
    public required string MeaningLanguage { get; init; }

    // ── who and when (JWT sub, server clock) ─────────────────────────────────────────────────────────────
    public required string SignerUserId { get; init; }
    public required string SignerLogin { get; init; }
    public required string SignerNameAtSign { get; init; }
    public required DateTime SignedAtUtc { get; init; }
    public string? Note { get; init; }

    // ── binding ──────────────────────────────────────────────────────────────────────────────────────────
    public string SubjectHashAlgorithm { get; init; } = ESignatureDigest.SubjectHashAlgorithm;
    public required string SubjectHash { get; init; }
    public required string SignatureDigest { get; init; }

    // ── re-authentication proof (from the consumed AuthService ticket) ───────────────────────────────────
    public required ESignatureReauthKind ReauthKind { get; init; }
    public required DateTime PasswordVerifiedAtUtc { get; init; }

    /// <summary>SHA-256 of the ticket (never the ticket): proves which re-authentication was spent, discloses nothing.</summary>
    public required string TicketRef { get; init; }

    public ESignatureSecondFactor SecondFactor { get; init; } = ESignatureSecondFactor.None;

    // ── appearance (KS-8 / KS-9 / KS-10) ─────────────────────────────────────────────────────────────────
    public ESignatureAppearanceType AppearanceType { get; init; } = ESignatureAppearanceType.None;
    public Guid? AppearanceSampleId { get; init; }
    public string? AppearanceContentId { get; init; }
    public string? AppearanceStorageProvider { get; init; }
    public string? AppearanceStoragePath { get; init; }
    public string? AppearanceVectorJson { get; init; }
    public string? AppearanceImageHash { get; init; }

    public Guid? RequestId { get; init; }
    public required ESignaturePolicySnapshot PolicySnapshot { get; init; }

    public required string IdempotencyKey { get; init; }
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>The digest input of this record — used at signing and recomputed by verification.</summary>
    public SignatureDigestInput DigestInput() => new(
        SignatureNumber, TenantId, Module, SubjectType, SubjectId, SubjectHash, LegalEntityId, ActionCode, Context,
        MeaningCode, MeaningTextAtSign, SignerUserId, SignerNameAtSign, SignedAtUtc, ESignatureWire.Name(ReauthKind),
        PasswordVerifiedAtUtc, ESignatureWire.Name(AppearanceType), AppearanceSampleId, AppearanceImageHash, RequestId);
}

public sealed record ESignatureSummaryLine(string Label, string Value);

/// <summary>The policy that applied when the record was made (pack §4 <c>PolicySnapshot</c>).</summary>
public sealed record ESignaturePolicySnapshot(
    ESignaturePolicyScope Scope,
    Guid? LegalEntityId,
    int PolicyVersion,
    ESignatureVisualMode VisualMode,
    bool ReauthEvery,
    int ReauthWindowMinutes);

/// <summary>MOD-0022 — insert-only invalidation of a signature (at most one per signature, unique index).</summary>
public sealed class ESignatureInvalidation : TenantScopedEntity
{
    public required Guid SignatureId { get; init; }
    public required string Reason { get; init; }
    public required string InvalidatedBy { get; init; }
    public required string InvalidatedByName { get; init; }
    public required DateTime InvalidatedAtUtc { get; init; }
    public required string TicketRef { get; init; }
    public required string IdempotencyKey { get; init; }
}

/// <summary>MOD-0022 — insert-only export log (KS-6): every PDF / CSV export is one row, with the file's SHA-256.</summary>
public sealed class ESignatureExport : TenantScopedEntity
{
    public required string ExportNumber { get; init; }
    public required ESignatureExportFormat Format { get; init; }
    public required string FilterJson { get; init; }
    public required string FilterText { get; init; }
    public required int RecordCount { get; init; }
    public required string ByUserId { get; init; }
    public required string ByName { get; init; }
    public required DateTime AtUtc { get; init; }
    public required string FileHash { get; init; }
    public required string IdempotencyKey { get; init; }
}

/// <summary>MOD-0022 — insert-only policy history row (reason ≥ 10, human-readable diff lines).</summary>
public sealed class ESignaturePolicyChange : TenantScopedEntity
{
    public required ESignaturePolicyScope Scope { get; init; }
    public Guid? LegalEntityId { get; init; }
    public List<string> Changes { get; init; } = [];
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public required string Reason { get; init; }
    public required string ChangedBy { get; init; }
    public required string ChangedByName { get; init; }
    public required DateTime ChangedAtUtc { get; init; }
}
