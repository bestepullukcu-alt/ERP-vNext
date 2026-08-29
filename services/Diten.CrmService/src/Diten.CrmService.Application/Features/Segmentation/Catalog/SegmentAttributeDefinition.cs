using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Catalog;

/// <summary>
/// One declared attribute. <see cref="AttributeClass"/> is the EVALUATION class (N/J/D). A non-null
/// <see cref="CrossServiceReferenceKind"/> means the criterion VALUE is additionally proven cross-service (class X) at
/// authoring time — that validation never derives membership, it only decides whether the rule is authorable.
/// <para><see cref="ValueSource"/> (P1a) says where a legitimate VALUE comes from, so an editor can offer the right
/// input. It is DESCRIPTIVE: it narrows nothing the runtime accepts, and free text stays valid everywhere.</para>
/// </summary>
public sealed record SegmentAttributeDefinition(
    string AttributeCode,
    string AttributeClass,
    string Source,
    string ValueType,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> RequiredParameters,
    IReadOnlyList<string> OptionalParameters,
    IReadOnlyList<string> AllowedSubjectTypes,
    string? CrossServiceReferenceKind,
    SegmentAttributeValueSource ValueSource)
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
