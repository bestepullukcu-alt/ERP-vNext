namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// Every attribute value the evaluator may need for ONE candidate, already resolved from the bulk Phase-1/1.5/2 reads.
/// The evaluator is therefore a pure function: it performs no I/O, which makes the N+1 ban structural rather than a
/// matter of discipline.
/// <para>Values are keyed by <b>criteria NodeId</b>, not by attribute code. Two predicates can name the same attribute
/// and still ask different questions — a different consent channel, a different account attribute key, a different
/// product for <c>concept.affinity</c> — so per-node slots are the only keying that cannot silently merge two
/// questions into one. Expensive derivations are still computed once and shared: the de-duplication happens in the
/// source reader, where it belongs, not in the key.</para>
/// <para>Three states are represented, and they are not the same thing: a VALUE (possibly the empty set, which is how
/// "the record has no value" is expressed), an UNRESOLVED marker with the reason the source could not answer (the
/// candidate is then eliminated fail-closed while the resolution still completes), and an ADVISORY reason that
/// explains a legitimate negative — for example consent answering <c>blocked</c> — so an elimination is never merely
/// "criteria_not_matched" when a better explanation exists.</para>
/// </summary>
public sealed class SegmentAttributeValueSet
{
    private static readonly IReadOnlyList<string?> None = Array.Empty<string?>();

    private readonly Dictionary<Guid, IReadOnlyList<string?>> _values = new();
    private readonly Dictionary<Guid, string> _unresolved = new();
    private readonly Dictionary<Guid, string> _advisory = new();

    public SegmentAttributeValueSet(Guid subjectId, string subjectType)
    {
        SubjectId = subjectId;
        SubjectType = subjectType;
    }

    public Guid SubjectId { get; }

    public string SubjectType { get; }

    public void SetValues(Guid nodeId, IEnumerable<string?> values)
        => _values[nodeId] = values.ToList();

    public void SetValue(Guid nodeId, string? value)
        => _values[nodeId] = value is null ? None : new[] { value };

    /// <summary>The source could not answer for this candidate. The candidate is eliminated with this reason code and
    /// the resolution completes: an in-service uncertainty is an ANSWER, not a failure.</summary>
    public void MarkUnresolved(Guid nodeId, string reasonCode)
        => _unresolved[nodeId] = reasonCode;

    /// <summary>Explains a legitimate negative. Appended only when the predicate actually fails.</summary>
    public void SetAdvisory(Guid nodeId, string reasonCode)
        => _advisory[nodeId] = reasonCode;

    public bool TryGetUnresolved(Guid nodeId, out string reasonCode)
        => _unresolved.TryGetValue(nodeId, out reasonCode!);

    public IReadOnlyList<string?> GetValues(Guid nodeId)
        => _values.TryGetValue(nodeId, out var values) ? values : None;

    public string? GetAdvisory(Guid nodeId)
        => _advisory.TryGetValue(nodeId, out var reason) ? reason : null;
}
