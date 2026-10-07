namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX4 (2, 6) — the store failed AFTER a lease write was sent (connection lost, write concern,
/// timeout): the lease may or may not be in the list. Not a rollout record that cannot be read — the caller answers
/// "ambiguous, retry" and counts the lease as possibly held. The read that preceded the write succeeded.
/// </summary>
public sealed class ProductLegalEntityScopeLeaseWriteAmbiguousException(Exception inner)
    : Exception("PRODUCT_SCOPE_WRITER_LEASE_WRITE_AMBIGUOUS", inner);
