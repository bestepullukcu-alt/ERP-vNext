namespace Diten.MdmService.Domain.Entities;

using System.Text;
using System.Security.Cryptography;

public sealed class ProductLegalEntityScopeInventorySnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset ObservedAtUtc { get; set; }
    public string CanonicalPayload { get; set; } = string.Empty;
    public string StableFactsHash { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;

    public void EnsureValid()
    {
        if (SchemaVersion != CurrentSchemaVersion || ObservedAtUtc.Offset != TimeSpan.Zero
            || string.IsNullOrEmpty(CanonicalPayload) || CanonicalPayload[^1] != '\n'
            || CanonicalPayload.Contains('\r')
            || Encoding.UTF8.GetByteCount(CanonicalPayload) > ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes
            || !Hex(StableFactsHash) || !Hex(SnapshotHash)
            || !string.Equals(SnapshotHash, Sha(CanonicalPayload), StringComparison.Ordinal)
            || !TryBuildStablePayload(CanonicalPayload, out var stablePayload)
            || !string.Equals(StableFactsHash, Sha(stablePayload), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PRODUCT_SCOPE_INVENTORY_SNAPSHOT_INVALID");
        }
    }

    private static bool TryBuildStablePayload(string payload, out string stablePayload)
    {
        const string observedPrefix = "observedUtcTicks=";
        var lines = payload.Split('\n', StringSplitOptions.None);
        if (lines.Length < 2 || lines[^1].Length != 0
            || lines[..^1].Count(line => line.StartsWith(observedPrefix, StringComparison.Ordinal)) != 1)
        {
            stablePayload = string.Empty;
            return false;
        }

        stablePayload = string.Join(
            '\n',
            lines[..^1].Where(line => !line.StartsWith(observedPrefix, StringComparison.Ordinal))) + "\n";
        return true;
    }

    private static string Sha(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool Hex(string value) => value.Length == 64 && value.All(Uri.IsHexDigit)
        && string.Equals(value, value.ToUpperInvariant(), StringComparison.Ordinal);
}
