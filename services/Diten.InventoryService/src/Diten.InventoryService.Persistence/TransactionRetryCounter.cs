using System.Collections.Concurrent;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// Counts how often the driver RE-RAN a transaction body after a transient error (Mongo WriteConflict), per kind
/// (<c>posting</c>, <c>location</c>). The ledger posting and the location lifecycle both write the location document,
/// so a race between them shows up here (MOD-0173-FU02 AC-16); also logged at Information.
/// </summary>
public sealed class TransactionRetryCounter
{
    public const string Posting = "posting";
    public const string Location = "location";

    /// <summary>MOD-0173-FU03 §8.2: the reservation repository's create / release / batch transactions.</summary>
    public const string Reservation = "reservation";

    private readonly ConcurrentDictionary<string, long> _retries = new(StringComparer.Ordinal);

    public void Retried(string kind) => _retries.AddOrUpdate(kind, 1, (_, n) => n + 1);

    public long Count(string kind) => _retries.TryGetValue(kind, out var n) ? n : 0;
}
