using Diten.ProcurementService.Application.Common;

namespace Diten.ProcurementService.Api.Tests.Sourcing;

/// <summary>
/// Test double for the PRODUCT-MASTER (MOD-0290) consume seam. A configurable known-set lets tests drive both the
/// fail-closed 404 path (an itemId NOT in the known-set → reported unknown) and the happy path (known items pass) —
/// the vacuity control (K3). The production default (PermissiveProductReferenceValidator) reports nothing unknown
/// because 0290 is not yet wired; these tests pin the fail-closed behaviour the real gateway will enforce.
/// </summary>
public sealed class FakeProductReferenceValidator : IProductReferenceValidator
{
    private readonly HashSet<string> _known;

    /// <summary>knownItemIds = null → permissive (everything known, mirrors production default).</summary>
    public FakeProductReferenceValidator(IEnumerable<string>? knownItemIds = null)
    {
        _known = knownItemIds is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(knownItemIds, StringComparer.Ordinal);
        Permissive = knownItemIds is null;
    }

    private bool Permissive { get; }

    public Task<IReadOnlyList<string>> GetUnknownItemIdsAsync(IReadOnlyList<string> itemIds, CancellationToken cancellationToken = default)
    {
        if (Permissive)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
        var unknown = itemIds.Where(id => !_known.Contains(id)).ToList();
        return Task.FromResult<IReadOnlyList<string>>(unknown);
    }
}
