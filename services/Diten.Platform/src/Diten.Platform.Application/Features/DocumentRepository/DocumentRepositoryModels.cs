using Diten.Platform.Application.Contracts.DocumentRepository;

namespace Diten.Platform.Application.Features.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — permission keys for the Internal Document Repository Service.
/// <para>
/// Namespace is <c>platform.document-repository.*</c>, LOCKED by CT decision OD-3 (2026-09-07). PKS-001 §4
/// assigns each first segment to exactly one owning module, so MOD-0029's <c>platform.document-management.*</c>
/// is deliberately <b>not</b> extended — the repository owns its own namespace.
/// </para>
/// <para>Format per PKS-001 §1: lowercase-dotted, ≥3 segments, hyphen as the in-segment word separator.</para>
/// </summary>
public static class DocumentRepositoryPermissions
{
    /// <summary>Tier-1 <c>read</c> — read pointer metadata and stream content.</summary>
    public const string Read = "platform.document-repository.read";

    /// <summary>Tier-3 <c>manage</c> — store objects and run compensation delete.</summary>
    public const string Manage = "platform.document-repository.manage";

    // ── WP-VW-W3-PLT — scope-bound keys for CRM visit evidence (ContentStorageScope.CrmVisitEvidence). A field rep
    //    is never given the tenant-wide read/manage above (user decision 2026-10-12); these keys open ONLY the
    //    visit-evidence scope and never any other one. The scope and owner decision is made by
    //    DocumentRepositoryService (AD-5); the controller attribute only admits "general OR scope-bound".

    /// <summary>Stores objects in the CrmVisitEvidence scope only, and compensation-deletes only evidence the
    /// caller created. Writing to any other scope with this key is refused (403).</summary>
    public const string VisitEvidenceWrite = "platform.document-repository.visit-evidence.write";

    /// <summary>Reads metadata and content of CrmVisitEvidence objects the caller created, and nothing else. Any
    /// other object answers 404, exactly like another tenant's object.</summary>
    public const string VisitEvidenceRead = "platform.document-repository.visit-evidence.read";

    /// <summary>Reads metadata and content of every CrmVisitEvidence object in the caller's tenant (manager /
    /// reviewer). Opens no other scope.</summary>
    public const string VisitEvidenceReadAll = "platform.document-repository.visit-evidence.read-all";

    // ── Reserved for later follow-ups. Declared here so the namespace is not fragmented later, and
    //    deliberately NOT implemented by FU01.
    //
    //    platform.document-repository.health.read              → MOD-0262-FU03 (Repository Health)
    //    platform.document-repository.evidence-package.read    → MOD-0262-FU04 (Evidence Package Storage)
    //    platform.document-repository.evidence-package.manage  → MOD-0262-FU04
    //    platform.document-repository.purge.execute            → MOD-0262-FU05
    //
    // ⛔ AD-7: `purge.execute` is a SEPARATE key and is never implied by `manage`. Destroying records is a
    //    segregated duty, not an administration action. It is not defined in FU01 on purpose: defining it here
    //    would let an implementer wire a destruction path before MOD-0030 owns the retention decision.
}

/// <summary>Client-facing pointer metadata. ⛔ <c>ObjectKey</c> is absent by design and never leaves the service.</summary>
public sealed record RepositoryObjectModel(
    Guid ContentId,
    string StorageProvider,
    string Scope,
    Guid OwningItemId,
    Guid OwningVersionId,
    string FileName,
    string MediaType,
    long ByteSize,
    string Checksum,
    DateTimeOffset CreatedAt,
    string CreatedBy);

/// <summary>
/// Input for a store operation. The payload is a forward-only <see cref="Stream"/> (AD-4) — there is no
/// <c>byte[]</c> and no base64 member, so a binary payload can never travel inside a JSON body.
/// </summary>
public sealed record StoreRepositoryObjectInput(
    ContentStorageScope Scope,
    Guid CompanyId,
    Guid OwningItemId,
    Guid OwningVersionId,
    string FileName,
    string? MediaType,
    Stream Content);

/// <summary>Resolved handle used by the controller to stream content out. Internal to the service boundary.</summary>
public sealed record RepositoryObjectContentHandle(
    Stream Content,
    string MediaType,
    string FileName,
    long ByteSize);
