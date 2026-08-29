namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// The resolved attribute values for the WHOLE candidate set of one resolution, produced by a fixed, small number of
/// bulk reads (one per source). The evaluator only ever reads from here, so no code path can accidentally reach a
/// repository once per candidate.
/// </summary>
public sealed class SegmentAttributeContext
{
    private readonly IReadOnlyDictionary<Guid, SegmentAttributeValueSet> _bySubject;

    public SegmentAttributeContext(IReadOnlyDictionary<Guid, SegmentAttributeValueSet> bySubject)
        => _bySubject = bySubject;

    public static SegmentAttributeContext Empty { get; } =
        new(new Dictionary<Guid, SegmentAttributeValueSet>());

    /// <summary>The value set for a candidate. A subject that was never loaded gets an EMPTY set rather than null, so
    /// evaluation stays total and a missing candidate can never be mistaken for a match.</summary>
    public SegmentAttributeValueSet For(Guid subjectId, string subjectType)
        => _bySubject.TryGetValue(subjectId, out var set)
            ? set
            : new SegmentAttributeValueSet(subjectId, subjectType);
}
