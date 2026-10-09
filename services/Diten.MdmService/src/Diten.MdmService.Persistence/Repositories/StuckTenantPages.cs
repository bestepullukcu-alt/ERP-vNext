using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// BL-552 flow 3 FIX3 item 1: a stuck-tenant page made of two streams, each read through its own partial index in
/// tenant order. One <c>$or</c> query with the tenant sort is not served by two partial indexes — the planner takes
/// the unique (TenantId, OperationId) index for its order and walks every request, the finished history included
/// (measured: 109 keys for 4 answers). Two single-index reads merged here examine only what is stuck.
/// </summary>
internal static class StuckTenantPages
{
    /// <summary>
    /// MongoDB's order for a Standard (subtype 4) UUID: its 16 bytes compared as stored (big-endian). The cursor is
    /// passed back as <c>$gt</c>, so the merge must agree with the server's order — written out from the stored bytes
    /// rather than resting on <see cref="Guid.CompareTo(Guid)"/> happening to agree.
    /// </summary>
    public static readonly IComparer<Guid> MongoOrder = Comparer<Guid>.Create((left, right) =>
        left.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(right.ToByteArray(bigEndian: true)));

    /// <summary>
    /// Each stream is at most <paramref name="limit"/> tenant ids (one per stuck request) in server order. Everything up
    /// to the merged <paramref name="limit"/>-th id is in one of the two streams (a stream that stopped short is
    /// exhausted), so that id is a safe <c>$gt</c> cursor; fewer than <paramref name="limit"/> means both ran out.
    /// </summary>
    public static GlobalProductIdentityWorkflowTenantPartitionPage Merge(
        IReadOnlyList<Guid> first, IReadOnlyList<Guid> second, int limit)
    {
        var merged = first.Concat(second).OrderBy(id => id, MongoOrder).Take(limit).ToList();
        return new(merged.Distinct().ToArray(), merged.Count == limit ? merged[^1] : null);
    }
}
