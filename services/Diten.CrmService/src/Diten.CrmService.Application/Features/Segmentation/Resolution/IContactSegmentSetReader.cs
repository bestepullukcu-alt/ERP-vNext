namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// WP-VP-3D — the bulk segment seam the period-status reader consumes (an interface so the reader's read count can be
/// pinned in a test). The production implementation is <see cref="ContactSegmentSetReader"/>.
/// </summary>
public interface IContactSegmentSetReader
{
    Task<ContactSegmentSet> ReadAsync(
        Guid tenantId, IReadOnlyCollection<Guid> contactIds, DateTimeOffset effectiveAt, CancellationToken cancellationToken);
}
