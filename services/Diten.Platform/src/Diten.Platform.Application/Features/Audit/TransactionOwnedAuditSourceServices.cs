namespace Diten.Platform.Application.Features.Audit;

/// <summary>
/// MOD-0040-FU02 (K-9) — the CLOSED set of source-service names the in-transaction door accepts on an intent: the Platform
/// itself and the record-code consumers' credential bindings. Compared exactly (ordinal, no trimming). The set grows only
/// with a revision of the pack that owns it (DL-8 adds <c>Diten.InventoryService</c>).
/// </summary>
public static class TransactionOwnedAuditSourceServices
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        CanonicalTransactionalAuditOutboxWriter.SourceService,
        "Diten.FinanceTreasuryService",
        "Diten.MdmService"
    };

    public static bool Contains(string? name) => name is not null && Names.Contains(name);
}
