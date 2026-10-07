namespace Diten.MdmService.Domain.Entities;

public sealed class ProductLegalEntityScopeWriterLease
{
    public const int DurationSeconds = 120;

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX2 (6) — the expiry is stamped by one MDM instance's clock and read by another's. Another
    /// command (or a fence) treats a lease as gone only this much after its expiry, so a few seconds of drift between two
    /// instances never lets two writers hold one product.
    /// </summary>
    public const int ClockSkewSeconds = 5;

    /// <summary>
    /// The longest lease a stored record may carry. A read accepts any duration up to this (a lease written while
    /// <see cref="DurationSeconds"/> had another value stays readable); only a new lease is stamped with the constant.
    /// </summary>
    public const int MaximumDurationSeconds = 3600;

    public Guid Token { get; set; }
    public long Generation { get; set; }
    public Guid CommandId { get; set; }
    public Guid ActorId { get; set; }
    public string MutationKind { get; set; } = string.Empty;
    public string PayloadFingerprint { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string? PreWriteStateHash { get; set; }

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 B4 — what the write touches ("GlobalProductId:{id}", "Command:{id}", …). Two writers
    /// conflict only when their keys meet; writers of different products proceed side by side. Empty on a lease written
    /// before FIX1 (it then conflicts with every other writer, as the one-slot lease did).
    /// </summary>
    public List<string> ObjectKeys { get; set; } = [];

    public const int MaximumObjectKeys = 16;

    public bool Overlaps(ProductLegalEntityScopeWriterLease other)
        => ObjectKeys.Count == 0 || other.ObjectKeys.Count == 0
           || ObjectKeys.Any(key => other.ObjectKeys.Contains(key, StringComparer.Ordinal));

    /// <summary>The holder's own view: its lease has run out (a repeat of the command then takes a fresh lease).</summary>
    /// <summary>
    /// FIX3 (3) — a lease that names no object (a create: "Command:{id}" only). It protects no product from another writer
    /// (nobody's keys meet it); it is kept only so a fence waits for a create that may have half-written. Such leases fill
    /// their OWN bucket (<c>MaximumObjectlessWriterLeases</c>), so a burst of failed creates cannot refuse product writers.
    /// </summary>
    public static bool NamesNoObject(ProductLegalEntityScopeWriterLease lease)
        => lease.ObjectKeys.Count > 0
           && lease.ObjectKeys.All(key => key.StartsWith("Command:", StringComparison.Ordinal));

    public bool IsExpiredAt(DateTimeOffset serverNowUtc) => ExpiresAtUtc <= serverNowUtc;

    /// <summary>Everyone else's view: the lease is gone, clock drift included — only then is it taken over or swept.</summary>
    public bool IsExpiredForOthersAt(DateTimeOffset serverNowUtc) => ExpiresAtUtc.AddSeconds(ClockSkewSeconds) <= serverNowUtc;

    public bool SameCommandAs(ProductLegalEntityScopeWriterLease other)
        => CommandId == other.CommandId && ActorId == other.ActorId
           && string.Equals(MutationKind, other.MutationKind, StringComparison.Ordinal)
           && string.Equals(PayloadFingerprint, other.PayloadFingerprint, StringComparison.Ordinal);

    public bool BaselineBound => PreWriteStateHash is not null;

    public void EnsureValid()
    {
        if (Token == Guid.Empty || Generation <= 0 || CommandId == Guid.Empty || ActorId == Guid.Empty
            || !Exact(MutationKind, 128) || !Exact(PayloadFingerprint, 64)
            || !Exact(Owner, 160) || AcquiredAtUtc.Offset != TimeSpan.Zero
            || ExpiresAtUtc.Offset != TimeSpan.Zero
            || ExpiresAtUtc <= AcquiredAtUtc
            || ExpiresAtUtc > AcquiredAtUtc.AddSeconds(MaximumDurationSeconds)
            || PreWriteStateHash is not null && !Exact(PreWriteStateHash, 64)
            || ObjectKeys is null || ObjectKeys.Count > MaximumObjectKeys
            || ObjectKeys.Any(key => !Exact(key, 96))
            || ObjectKeys.Distinct(StringComparer.Ordinal).Count() != ObjectKeys.Count)
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_WRITER_LEASE_INVALID");
        }
    }

    private static bool Exact(string value, int length) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= length
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
