namespace Diten.CrmService.Application.Common;

/// <summary>
/// WP-VP-4G (F4-4) — the display NAMES of MDM Global Products, for READ models only (plan session picks, the visit
/// preview's product items / distribution / overflow, the planned visit's content items).
/// <para><b>One bulk read per call</b> for every id asked (never one call per product). <b>Fail-open:</b> a product the
/// master does not know, or a master that cannot be reached, simply has no entry — the caller shows the code; a plan read
/// never fails because of a name. A name is never taken from a client.</para>
/// </summary>
public interface IProductNameReader
{
    Task<IReadOnlyDictionary<Guid, string>> ReadNamesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}

/// <summary>No master wired: no names (the code is shown).</summary>
public sealed class NullProductNameReader : IProductNameReader
{
    public Task<IReadOnlyDictionary<Guid, string>> ReadNamesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
