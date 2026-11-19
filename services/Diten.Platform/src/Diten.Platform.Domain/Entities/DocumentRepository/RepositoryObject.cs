using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — the repository's own system-of-record row for one stored binary.
/// <para>
/// This is the store side of the pointer: a consumer embeds a <c>ContentRef</c>, while the repository keeps
/// this row so it can answer "which objects do I actually own?" independently of any consumer. That is what
/// makes MOD-0262-FU03 (Repository Health) able to reconcile both directions — orphan metadata (a row with no
/// physical object) and orphan binaries (a physical object with no row).
/// </para>
/// <para>
/// <b>Tenant-owned</b>, therefore <see cref="TenantScopedEntity"/> (the concrete tenant-aware base in
/// <c>Diten.Platform.Common.Persistence</c>) — not <c>GlobalEntity</c>: a stored binary always belongs to
/// exactly one tenant. <see cref="ObjectKey"/> additionally carries the tenant, so isolation is structural and
/// does not rely on the query filter alone.
/// </para>
/// <para>
/// ⛔ There is no destruction state on this entity. Physical destruction is MOD-0262-FU05 and must consume
/// MOD-0029-FU15 disposition markers with a legal-hold re-verification at execution time (DCP-008 AD-6).
/// Soft delete (<c>IsDeleted</c>) here only marks compensation for a failed metadata commit.
/// </para>
/// </summary>
public sealed class RepositoryObject : TenantScopedEntity
{
    /// <summary>Store-assigned content identity. Unique per tenant; this is the only id a client ever sees.</summary>
    public required Guid ContentId { get; set; }

    /// <summary>Provider discriminator, e.g. <c>local-filesystem</c>.</summary>
    public required string StorageProvider { get; set; }

    /// <summary>⛔ Internal storage detail. Never serialised to a client and never accepted as input.</summary>
    public required string ObjectKey { get; set; }

    /// <summary>Logical partition name (<c>documents</c> / <c>templates</c>), stored by name so the enum stays additive.</summary>
    public required string Scope { get; set; }

    /// <summary>The consumer record this object belongs to (e.g. a controlled document or template id).</summary>
    public Guid OwningItemId { get; set; }

    /// <summary>The consumer record's version this object belongs to.</summary>
    public Guid OwningVersionId { get; set; }

    public required string FileName { get; set; }

    public required string MediaType { get; set; }

    public long ByteSize { get; set; }

    /// <summary>Lowercase-hex SHA-256 computed by the store while streaming. A caller-supplied value is never trusted.</summary>
    public required string Checksum { get; set; }

    public Guid? CompanyId { get; set; }

    /// <summary>
    /// MOD-0262-FU01 DL-2 — the trusted consumer service token's subject (<c>sub</c>) that wrote this row; null on the user and
    /// in-process paths. Trace and audit ONLY — never part of a visibility decision (r4, F-2).
    /// </summary>
    public Guid? TrustedConsumerClientId { get; set; }

    /// <summary>
    /// MOD-0262-FU01 DL-2 — the closed-list consumer module (v1: <c>MOD-0217</c>) that owns this row; null on the user and
    /// in-process paths. Visibility is bound to it: the trusted path sees only its own module's rows, the user path never sees
    /// a row where it is set (K-C).
    /// </summary>
    public string? TrustedConsumerModule { get; set; }
}
