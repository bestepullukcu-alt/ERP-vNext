namespace Diten.CrmService.Application.Common.ReferenceValidation;

/// <summary>One published MOD-0048 / PSS-012 reference value as read for the workbook helper sheet.</summary>
public sealed record ReferenceValueSnapshot(
    string ValueCode,
    string? DisplayName,
    string? Description,
    bool IsActive,
    bool IsDeprecated,
    IReadOnlyDictionary<string, string>? Attributes);

/// <summary>
/// A whole published set. <see cref="IsPublished"/> is false when the operator has not published the set yet — the
/// caller writes a NOT_PUBLISHED marker row and leaves the dropdown empty. It NEVER substitutes a local list.
/// </summary>
public sealed record ReferenceSetSnapshot(string SetCode, bool IsPublished, IReadOnlyList<ReferenceValueSnapshot> Values)
{
    public static ReferenceSetSnapshot NotPublished(string setCode)
        => new(setCode, false, Array.Empty<ReferenceValueSnapshot>());
}

/// <summary>
/// Reads ALL published values of a MOD-0048 set (the list, not a single-value check) so the import template and the
/// export workbook can ship a ReferenceData helper sheet and in-cell dropdowns. Separate from
/// <see cref="IReferenceDataValidator"/> (single value) and <see cref="IReferenceMetadataReader"/> (one value's
/// attributes) so existing consumers/tests are unaffected. Implemented in Infrastructure over the Gateway consumer
/// endpoint — CRM never seeds or hardcodes reference values.
/// </summary>
public interface IReferenceDataCatalogReader
{
    Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken);
}
