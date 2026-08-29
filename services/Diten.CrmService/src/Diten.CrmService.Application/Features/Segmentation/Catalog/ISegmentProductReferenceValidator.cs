namespace Diten.CrmService.Application.Features.Segmentation.Catalog;

/// <summary>
/// MOD-0167 FU02 class-X seam: proves that a criterion VALUE (an MDM global-product / product / brand id) exists and is
/// referenceable, before the criterion is allowed to be authored. It <b>never derives membership</b> — membership for
/// <c>concept.affinity</c> comes from the in-service ConceptGraph (D-PRODUCT), not from MDM.
/// <para><b>Fail-closed contract (D6):</b> <see cref="Outcome.NotFound"/> is a 400 (the rule is not authorable) and
/// <see cref="Outcome.Unavailable"/> is a 503 with <b>no partial result and nothing persisted</b> — the implementation
/// is always called BEFORE the insert/replace. The transport profile mirrors the Working Calendar legal-entity
/// validator verbatim: no cache, 3s total timeout, 1 transient retry, Authorization / X-Tenant-Id / X-Correlation-Id
/// forwarded, always through the Gateway (never a service port).</para>
/// </summary>
public interface ISegmentProductReferenceValidator
{
    /// <summary>What the reference proof concluded. There is no fourth value: "probably fine" is not an outcome.</summary>
    public enum Outcome
    {
        /// <summary>The reference exists and is referenceable for this tenant.</summary>
        Valid = 0,

        /// <summary>The dependency answered, and the reference does not exist. The criterion is not authorable (400).</summary>
        NotFound = 1,

        /// <summary>The dependency could not answer (timeout / 5xx / auth rejection / malformed body). 503, nothing
        /// persisted, never treated as valid.</summary>
        Unavailable = 2
    }

    /// <summary><paramref name="referenceKind"/> is one of the
    /// <see cref="SegmentAttributeCatalog.ReferenceKindGlobalProduct"/> /
    /// <see cref="SegmentAttributeCatalog.ReferenceKindProduct"/> /
    /// <see cref="SegmentAttributeCatalog.ReferenceKindBrand"/> constants declared by the catalog.</summary>
    Task<Outcome> ValidateAsync(string referenceKind, Guid referenceId, CancellationToken cancellationToken);
}
