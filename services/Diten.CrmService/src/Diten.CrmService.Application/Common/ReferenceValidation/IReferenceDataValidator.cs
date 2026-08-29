namespace Diten.CrmService.Application.Common.ReferenceValidation;

/// <summary>Outcome of validating a value against a MOD-0048 / PSS-012 published reference set.</summary>
public enum ReferenceValidationStatus
{
    /// <summary>Value exists and is active (not deprecated) in the set's published values.</summary>
    Valid,

    /// <summary>Value is not present, or is present but deprecated → not selectable for new/updated records.</summary>
    InvalidValue,

    /// <summary>The required set itself is not published yet (operator authoring pending). Controlled dependency.</summary>
    SetMissing
}

public sealed record ReferenceValidationResult(ReferenceValidationStatus Status, string SetCode, string Value);

/// <summary>
/// Consumer seam for MOD-0048 / PSS-012 Business Reference Data published values
/// (<c>GET /sets/{setCode}/published-values</c>). CRM never seeds or hardcodes reference values;
/// it only reads published values through this seam. Implemented in Infrastructure via the Gateway.
/// </summary>
public interface IReferenceDataValidator
{
    Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the per-value <c>attributes</c> metadata of a MOD-0048 published value (e.g. account-relationship-type's
/// <c>direction</c> / <c>inverseLabelCode</c> / <c>selfAllowed</c>). Returns null when the set is unpublished, the
/// value is absent, or it carries no attributes — the caller then degrades (never a hardcoded metadata fallback).
/// Separate from <see cref="IReferenceDataValidator"/> so existing consumers/tests are unaffected.
/// </summary>
public interface IReferenceMetadataReader
{
    Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken cancellationToken);
}
