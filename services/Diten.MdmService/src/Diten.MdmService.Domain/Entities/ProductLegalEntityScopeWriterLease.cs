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

    public bool BaselineBound => PreWriteStateHash is not null;

    public void EnsureValid()
    {
        if (Token == Guid.Empty || Generation <= 0 || CommandId == Guid.Empty || ActorId == Guid.Empty
            || !Exact(MutationKind, 128) || !Exact(PayloadFingerprint, 64)
            || !Exact(Owner, 160) || AcquiredAtUtc.Offset != TimeSpan.Zero
            || ExpiresAtUtc.Offset != TimeSpan.Zero
            || ExpiresAtUtc != AcquiredAtUtc.AddSeconds(DurationSeconds)
            || PreWriteStateHash is not null && !Exact(PreWriteStateHash, 64))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_WRITER_LEASE_INVALID");
        }
    }

    private static bool Exact(string value, int length) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= length
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);
}
