using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Catalog;

/// <summary>
/// One declared attribute. <see cref="AttributeClass"/> is the EVALUATION class (N/J/D). A non-null
/// <see cref="CrossServiceReferenceKind"/> means the criterion VALUE is additionally proven cross-service (class X) at
/// authoring time — that validation never derives membership, it only decides whether the rule is authorable.
/// <para><see cref="ValueSource"/> (P1a) says where a legitimate VALUE comes from, so an editor can offer the right
/// input. It is DESCRIPTIVE: it narrows nothing the runtime accepts, and free text stays valid everywhere.</para>
/// <para><see cref="Domain"/> is a PRESENTATION-ONLY business grouping (the optgroup a criteria editor renders the
/// attribute under, e.g. doctor-profile / consent / workplace). It is descriptive metadata: it is never read by
/// validation or evaluation and narrows nothing the runtime accepts.</para>
/// <para><see cref="ParameterValueSources"/> (WP-SEG-G) is an ADDITIVE per-parameter value-source map (parameter name →
/// where a legitimate value comes from), so an editor can offer the right input for a PARAMETER just as
/// <see cref="ValueSource"/> does for the main value. It is DESCRIPTIVE and OPTIONAL: it is null by default, the
/// <see cref="RequiredParameters"/> / <see cref="OptionalParameters"/> name lists are unchanged, and a parameter with no
/// entry keeps its plain-input behaviour. It narrows nothing the runtime accepts — free text stays valid.</para>
/// </summary>
public sealed record SegmentAttributeDefinition(
    string AttributeCode,
    string Domain,
    string AttributeClass,
    string Source,
    string ValueType,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> RequiredParameters,
    IReadOnlyList<string> OptionalParameters,
    IReadOnlyList<string> AllowedSubjectTypes,
    string? CrossServiceReferenceKind,
    SegmentAttributeValueSource ValueSource,
    IReadOnlyDictionary<string, SegmentAttributeValueSource>? ParameterValueSources = null)
{
    /// <summary>True when the VALUE crosses a process boundary for validation (class X on top of the evaluation class).</summary>
    public bool RequiresCrossServiceValueValidation => CrossServiceReferenceKind is not null;

    /// <summary>The declared class as the contract publishes it, e.g. "D" or "D+X".</summary>
    public string DeclaredClass => RequiresCrossServiceValueValidation
        ? $"{AttributeClass}+{SegmentAttributeCatalog.ClassCrossService}"
        : AttributeClass;

    public bool SupportsOperator(string? op)
        => Operators.Contains(SegmentOperators.Normalize(op), StringComparer.OrdinalIgnoreCase);

    public bool AppliesToSubjectType(string? subjectType)
        => !string.IsNullOrWhiteSpace(subjectType)
           && AllowedSubjectTypes.Contains(subjectType.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
}
