namespace Diten.MdmService.Domain.Entities;

public sealed class ProductLegalEntityScopeWriterLease
{
    public const int DurationSeconds = 120;

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

    public bool IsExpiredAt(DateTimeOffset serverNowUtc) => ExpiresAtUtc <= serverNowUtc;

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
            || ExpiresAtUtc != AcquiredAtUtc.AddSeconds(DurationSeconds)
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
