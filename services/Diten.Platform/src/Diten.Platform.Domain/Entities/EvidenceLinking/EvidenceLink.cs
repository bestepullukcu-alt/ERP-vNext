using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.EvidenceLinking;

/// <summary>
/// MOD-0031 (Evidence Linking, slice 1) — an immutable link between a governed object owned by ANY module (first
/// consumer: CRM claim / claim country version) and the evidence that supports it: a PINNED version of a MOD-0029
/// controlled document, or a MOD-0029-FU14 external register entry.
/// <para>
/// The link stores identifiers and display snapshots only — never the document's content or file ("no document storage
/// duplication"). There is no update: a correction is <c>remove</c> + a new link, and a removed link stays in the
/// collection with its removal reason, so the history is auditable.
/// </para>
/// <para>
/// Completeness rules ("≥1 evidence before approval") belong to the CONSUMER; this aggregate decides nothing about them.
/// </para>
/// </summary>
public sealed class EvidenceLink : TenantScopedEntity
{
    public required EvidenceObjectRef ObjectRef { get; set; }

    /// <summary><see cref="EvidenceDocumentKinds"/> — controlled | external.</summary>
    public required string DocumentKind { get; set; }

    /// <summary>ControlledDocument id or ExternalDocumentRegisterEntry id.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>ControlledDocumentVersion id — required for controlled, null for external.</summary>
    public Guid? DocumentVersionId { get; set; }

    /// <summary>Snapshot: "v5" for controlled, the external SourceVersion for external.</summary>
    public string? DocumentVersionLabel { get; set; }

    /// <summary>Snapshot of the document title at link time.</summary>
    public required string DocumentTitle { get; set; }

    /// <summary>An <c>evidence-type</c> reference value (Global BRD set).</summary>
    public required string EvidenceTypeCode { get; set; }

    public required EvidenceLocator Locator { get; set; }

    /// <summary>The wording (in the object's text) this evidence supports. At most 10.</summary>
    public List<EvidenceSupportedSpan> SupportedSpans { get; set; } = [];

    /// <summary><see cref="EvidenceLinkStatuses"/> — active | removed.</summary>
    public string Status { get; set; } = EvidenceLinkStatuses.Active;

    /// <summary>
    /// Deterministic hash of object + document + version + page + quote. A partial UNIQUE index over
    /// <c>{TenantId, ActiveDedupKey}</c> where <c>Status == active</c> makes "the same active link twice" impossible at the
    /// storage level; the handler's pre-check is only the friendly path.
    /// </summary>
    public required string ActiveDedupKey { get; set; }

    public string? LinkedBy { get; set; }
    public Guid? LinkedByUserId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }

    public string? RemovedBy { get; set; }
    public Guid? RemovedByUserId { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public string? RemovalReason { get; set; }

    public bool IsActive => Status == EvidenceLinkStatuses.Active;
}

/// <summary>The governed object the evidence supports. Opaque to Platform — the owning module defines the ids.</summary>
public sealed class EvidenceObjectRef
{
    /// <summary>Owning module (≤32, lower case), e.g. <c>crm</c>.</summary>
    public required string Module { get; set; }

    /// <summary>Object type (≤64, lower case), e.g. <c>claim</c> / <c>claim-country-version</c>.</summary>
    public required string ObjectType { get; set; }

    /// <summary>Object id (≤64).</summary>
    public required string ObjectId { get; set; }

    /// <summary>Optional object version (≤32), e.g. the claim's business version.</summary>
    public string? ObjectVersion { get; set; }
}

/// <summary>Where in the document the evidence is. <see cref="Quote"/> is mandatory.</summary>
public sealed class EvidenceLocator
{
    public string? Section { get; set; }
    public string? Page { get; set; }
    public string? Table { get; set; }
    public required string Quote { get; set; }
}

/// <summary>A span of the object's wording that this evidence supports (per language).</summary>
public sealed class EvidenceSupportedSpan
{
    public required string LanguageCode { get; set; }
    public required string Text { get; set; }
    public int? Start { get; set; }
    public int? End { get; set; }
}

public static class EvidenceDocumentKinds
{
    public const string Controlled = "controlled";
    public const string External = "external";

    public static bool IsValid(string? value) => value is Controlled or External;
}

public static class EvidenceLinkStatuses
{
    public const string Active = "active";
    public const string Removed = "removed";
}

/// <summary>Raised by the repository when the storage-level uniqueness of an active link is violated.</summary>
public sealed class EvidenceLinkDuplicateException : Exception
{
    public EvidenceLinkDuplicateException(Exception inner)
        : base("An identical active evidence link already exists.", inner) { }
}
