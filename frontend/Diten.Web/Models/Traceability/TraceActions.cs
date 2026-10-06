using Diten.Web.Services;

namespace Diten.Web.Models.Traceability;

/// <summary>MOD-0174 permission keys the lot / serial screens gate on (pack §14) — defined by MOD-0174, not new keys.</summary>
public static class TracePermissionKeys
{
    public const string LotsRead = "trace.lots.read";
    public const string LotsCreate = "trace.lots.create";
    public const string LotsUpdate = "trace.lots.update";
    public const string LotsUpdateReleased = "trace.lots.update-released";
    public const string LotsChangeStatus = "trace.lots.change-status";
    public const string LotsMigrate = "trace.lots.migrate";
    public const string SerialsRead = "trace.serials.read";

    /// <summary>MOD-0173 keys the screens read on top (pack §11.1: the lot movement rows are ledger rows).</summary>
    public const string StockRead = "inventory.stock.read";
    public const string MovementsCreate = "inventory.movements.create";
}

/// <summary>
/// THE button table of pack §14 (rows of the LOT-SERIAL-TRACKING manifest), in one place: every button the P3 screens
/// draw — server-rendered or by script — is looked up here by its action code, and is drawn only for a holder of its
/// key (absent, not disabled — UAS-001 §6). The table is measured against the manifest provider source by the tests,
/// so a button cannot gain a key the catalog does not know. The migration screen's actions (P4) are listed so the
/// table stays the whole manifest; P3 draws none of them.
/// <para>
/// <c>NEWLOTQUICK</c> is NOT here (CT-D5a): the Stock Ledger "+ Yeni lot" shortcut is an in-form control of the lot field,
/// drawn for a holder of <see cref="TracePermissionKeys.LotsCreate"/> — no manifest action, on no manifest.
/// </para>
/// </summary>
public static class TraceActions
{
    public static readonly IReadOnlyDictionary<string, string> KeyOf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["NEWLOT"] = TracePermissionKeys.LotsCreate,
        ["VIEWLOT"] = TracePermissionKeys.LotsRead,
        ["EDITLOT"] = TracePermissionKeys.LotsUpdate,
        ["EDITRELEASEDLOT"] = TracePermissionKeys.LotsUpdateReleased,
        ["CHANGELOTSTATUS"] = TracePermissionKeys.LotsChangeStatus,
        ["LOTMOVES"] = TracePermissionKeys.LotsRead,
        ["LOTTRACEOPEN"] = TracePermissionKeys.LotsRead,
        // FU-0174-CAT-01: the ledger-opening buttons carry trace.lots.read; the target page enforces inventory.stock.read.
        ["OPENINLEDGER"] = TracePermissionKeys.LotsRead,
        ["VIEWALLSERIALS"] = TracePermissionKeys.SerialsRead,
        ["SERIALLOT"] = TracePermissionKeys.LotsRead,
        ["SERIALMOVES"] = TracePermissionKeys.LotsRead,
        ["VIEWRECEIPT"] = TracePermissionKeys.LotsRead,
        ["RECEIPTINLEDGER"] = TracePermissionKeys.LotsRead,
        ["TRACELOTDETAIL"] = TracePermissionKeys.LotsRead,
        // P4 (LotMigration screen) — declared by the manifest, not drawn in P3.
        ["APPLYALLSUGGESTIONS"] = TracePermissionKeys.LotsMigrate,
        ["CONFIRMMIGRATION"] = TracePermissionKeys.LotsMigrate,
        ["APPLYSUGGESTION"] = TracePermissionKeys.LotsMigrate,
        ["LINKEXISTINGLOT"] = TracePermissionKeys.LotsMigrate,
        ["NEWLOTFROMTEXT"] = TracePermissionKeys.LotsMigrate,
        ["MARKTYPO"] = TracePermissionKeys.LotsMigrate,
        ["CHANGEDECISION"] = TracePermissionKeys.LotsMigrate
    };

    /// <summary>The action codes of the migration screen (slice P4) — in the table, never drawn by P3.</summary>
    public static readonly IReadOnlySet<string> MigrationOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        "APPLYALLSUGGESTIONS", "CONFIRMMIGRATION", "APPLYSUGGESTION", "LINKEXISTINGLOT", "NEWLOTFROMTEXT", "MARKTYPO", "CHANGEDECISION"
    };

    public static bool Allowed(IPermissionSnapshot permissions, string actionCode)
        => KeyOf.TryGetValue(actionCode, out var key) && permissions.Has(key);

    /// <summary>
    /// The lot edit button for a lot in <paramref name="status"/>: <c>EDITLOT</c> for a Draft lot, <c>EDITRELEASEDLOT</c> for
    /// every other status (K-C, CT-D8).
    /// </summary>
    public static string EditActionFor(string? status) => string.Equals(status, "Draft", StringComparison.Ordinal) ? "EDITLOT" : "EDITRELEASEDLOT";

    /// <summary>The codes a page hands its script (the script asks <c>window.TraceActions.can(code)</c>, never a key).</summary>
    public static IReadOnlyDictionary<string, bool> AllowedFor(IPermissionSnapshot permissions)
        => KeyOf.Where(pair => !MigrationOnly.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => permissions.Has(pair.Value), StringComparer.Ordinal);
}
