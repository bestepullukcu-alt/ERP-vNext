namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>The production <see cref="IContactSegmentSetReader"/>: <see cref="ContactSegmentMemberships.DeriveManyAsync"/>
/// over the registered segment repository and membership resolver. Reads only.</summary>
public sealed class ContactSegmentSetReader : IContactSegmentSetReader
{
    private readonly Domain.Repositories.ISegmentRepository _segments;
    private readonly SegmentMembershipResolver _resolver;

    public ContactSegmentSetReader(Domain.Repositories.ISegmentRepository segments, SegmentMembershipResolver resolver)
    {
        _segments = segments;
        _resolver = resolver;
    }

    public Task<ContactSegmentSet> ReadAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
        => ContactSegmentMemberships.DeriveManyAsync(_segments, _resolver, tenantId, contactIds, effectiveAt, cancellationToken);
}
