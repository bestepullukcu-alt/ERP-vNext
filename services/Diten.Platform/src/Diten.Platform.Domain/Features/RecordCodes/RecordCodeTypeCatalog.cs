using Diten.Platform.Domain.Enums.RecordCodes;

namespace Diten.Platform.Domain.Features.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §4.4 (K-12 b) — the record types the shared service issues: every AUTOMATIC row of RCS-001 §3 (with
/// <c>AGR</c> in place of <c>CTR</c>, K-7) and <c>PLN</c> (§5.3). A test pins it to the rule file. Anything else — the
/// KALSIN types, the İKİSİ DE types, the old <c>CTR</c>, WMS types not yet in §3 — is unknown (422).
///
/// <para><see cref="RecordCodeTypeDefinition.AllowedCaller"/> is the audit source-service name of the only consumer that
/// may ask for the type. <c>ORG</c> / <c>POS</c> (DL-2, A-8 = LegalEntity, K-15) are asked for by <c>Diten.Platform</c>
/// itself through the in-process port only: no HTTP credential binding carries them, so the internal endpoint still
/// answers 403 (contract 1.0.0 §6). <c>PLN</c> has no caller (403) until DL-4.</para>
/// </summary>
public static class RecordCodeTypeCatalog
{
    private const string FinanceTreasury = "Diten.FinanceTreasuryService";
    private const string Mdm = "Diten.MdmService";
    private const string Platform = "Diten.Platform";

    public static IReadOnlyList<RecordCodeTypeDefinition> All { get; } =
    [
        new("ORG", "Organizasyon Birimi", "OTOMATİK", "MOD-0288", [RecordCodePrefixScope.LegalEntity], Platform,
            "platform.organization-units.create"),
        new("POS", "Pozisyon (kadro)", "OTOMATİK", "MOD-0288", [RecordCodePrefixScope.LegalEntity], Platform,
            "platform.positions.create"),
        new("PAY", "Ödeme Partisi", "OTOMATİK", "MOD-0126", [RecordCodePrefixScope.LegalEntity], FinanceTreasury,
            "finance-treasury.payment-batches.create"),
        new("APD", "AP Belgesi", "OTOMATİK", "MOD-0119", [RecordCodePrefixScope.LegalEntity], FinanceTreasury,
            "finance-treasury.ap-invoices.create"),
        new("ARI", "AR Alacak Faturası", "OTOMATİK", "MOD-0120", [RecordCodePrefixScope.LegalEntity], FinanceTreasury,
            "finance-treasury.receivable-invoices.create"),
        new("CPS", "Nakit Pozisyonu Anlık Görüntüsü", "OTOMATİK", "MOD-0124", [RecordCodePrefixScope.LegalEntity], FinanceTreasury,
            "finance-treasury.cash-positions.create"),
        new("CTP", "Nakit Eşik Politikası", "OTOMATİK", "MOD-0124", [RecordCodePrefixScope.LegalEntity], FinanceTreasury,
            "finance-treasury.cash-position-threshold-policies.create"),
        new("BNK", "Banka", "OTOMATİK", "MOD-0289", [RecordCodePrefixScope.Group], Mdm, "mdm.banks.create"),
        new("BAC", "Banka Hesabı", "OTOMATİK", "MOD-0289", [RecordCodePrefixScope.LegalEntity, RecordCodePrefixScope.Group], Mdm,
            "mdm.bank-accounts.create"),
        new("AGR", "Sözleşme", "OTOMATİK", "MOD-0217", [RecordCodePrefixScope.LegalEntity], Mdm, "mdm.contracts.create"),
        new("PLN", "Abonelik Planı", "İKİSİ DE", "Platform", [RecordCodePrefixScope.Platform], null, null)
    ];

    private static readonly IReadOnlyDictionary<string, RecordCodeTypeDefinition> ByType =
        All.ToDictionary(definition => definition.RecordType, StringComparer.Ordinal);

    public static bool TryGet(string recordType, out RecordCodeTypeDefinition definition)
    {
        if (recordType is not null && ByType.TryGetValue(recordType, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }
}

public sealed record RecordCodeTypeDefinition(
    string RecordType,
    string Name,
    string Class,
    string OwnerModule,
    IReadOnlyList<RecordCodePrefixScope> AllowedScopes,
    string? AllowedCaller,
    string? RequiredPermission)
{
    public bool Allows(RecordCodePrefixScope scope) => AllowedScopes.Contains(scope);
}
