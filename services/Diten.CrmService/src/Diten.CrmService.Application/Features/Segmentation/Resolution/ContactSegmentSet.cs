namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// WP-VP-3D — the ACTIVE contact segments of a set of doctors (<see cref="ContactSegmentMemberships.DeriveManyAsync"/>):
/// per doctor the member segment ids, plus the display name of every segment that has at least one member here. Every
/// requested doctor is a key (an empty list when it is in no segment).
/// </summary>
public sealed record ContactSegmentSet(
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> SegmentIdsByContact,
    IReadOnlyDictionary<Guid, string> SegmentNames)
{
    public static readonly ContactSegmentSet Empty = new(
        new Dictionary<Guid, IReadOnlyList<Guid>>(), new Dictionary<Guid, string>());

    public IReadOnlyList<Guid> For(Guid contactId)
        => SegmentIdsByContact.TryGetValue(contactId, out var ids) ? ids : Array.Empty<Guid>();
}
