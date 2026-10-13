namespace Diten.Web.Models.Traceability;

/// <summary>
/// The resx keys each MOD-0174 page hands its script (pack §11.6 names). The page's <c>_IndexL10n</c> serializes exactly
/// these (key → localized value); the scripts read them by the same dotted name. The tests measure both directions: every
/// key here is in the en / tr resx, and every <c>t('…')</c> a script asks for is listed here.
/// </summary>
public static class TraceL10nKeys
{
    private static readonly string[] Common =
    [
        "Status.Draft", "Status.Released", "Status.Blocked", "Status.Expired",
        "Serial.Status.Active", "Serial.Status.Consumed", "Serial.Status.Blocked",
        "Expiry.Past", "Expiry.Days", "Retest.Past",
        "StockStatus.AVAILABLE", "StockStatus.QUALITY_INSPECTION", "StockStatus.QUARANTINE", "StockStatus.BLOCKED",
        "StockStatus.IN_TRANSIT", "StockStatus.DAMAGED", "StockStatus.EXPIRED",
        "Error.Generic", "Error.ServiceUnavailable", "Error.Correlation", "Error.NoPermission", "Error.SessionExpired",
        "Searching", "NoResults", "Col.Actions"
    ];

    public static readonly IReadOnlyList<string> Lots =
    [
        .. Common,
        "Action.NEWLOT", "Action.VIEWLOT", "Action.EDITLOT", "Action.EDITRELEASEDLOT", "Action.CHANGELOTSTATUS", "Action.LOTMOVES",
        "Action.LOTTRACEOPEN", "Action.OPENINLEDGER", "Action.VIEWALLSERIALS",
        "Detail.Title", "Detail.Unusable.Blocked", "Detail.Unusable.Expired", "Detail.SerialsMore",
        "Info.Tracking", "Info.Source", "Info.Source.GoodsReceipt", "Info.Source.Production", "Info.Source.Opening",
        "Info.Source.Manual", "Info.Source.Migration", "Info.None", "Info.CreatedBy",
        "Tracking.Lot", "Tracking.LotAndSerial", "Tracking.None",
        "Form.Product", "Form.Sku", "Level.Gsku", "Level.Lsku", "Level.FinishedGood", "Level.Item", "Form.LotNumber", "Form.ManufacturerLot", "Form.SupplierLot", "Form.ManufacturingDate",
        "Form.ExpiryDate", "Form.RetestDate", "Form.Country", "Form.Coa", "Form.ReasonCounter", "Form.NoChanges",
        "Form.Duplicate", "Col.Status",
        "History.Action.Created", "History.Action.Updated", "History.Action.StatusChanged", "History.Action.Linked",
        "History.Action.Migrated", "History.Action.SystemExpired", "History.System", "History.Page",
        // WP-0174-FU5 F-1: the history labels its fields and values like the screen (no raw field / status / country code).
        "History.Field.TypedText", "History.Field.GenealogyParent",
        .. LotFormRules.Countries.Select(code => "Country." + code),
        "Validation.Required", "Validation.LotFormat", "Validation.DatesOrder", "Validation.ReasonMin", "Validation.FixFields",
        "Status.Dialog.Title", "Status.Dialog.Reason", "Status.Dialog.Confirm", "Status.Dialog.ConfirmBlock", "Status.Dialog.Impact",
        "Status.Dialog.PickOne", "Status.Dialog.AcknowledgeRequired", "Status.Dialog.Done", "Cancel",
        // MOD-0175 Q5 (pack §11.4): the lot release that waits for a quality approval (TRACE 202) + the lot page badge.
        "Status.Dialog.Pending.Title", "Status.Dialog.Pending.Text", "Status.Dialog.Pending.Link", "Quality.PendingApproval", "Quality.PendingApproval.Title"
    ];

    public static readonly IReadOnlyList<string> Serials =
    [
        .. Common,
        "Serial.Status.Active", "Serial.Status.Consumed", "Serial.Status.Blocked", "Action.SERIALLOT", "Action.SERIALMOVES"
    ];

    public static readonly IReadOnlyList<string> LotMovements =
    [
        .. Common,
        "Badge.Automatic", "Action.VIEWRECEIPT", "Action.RECEIPTINLEDGER", "Chip.Serial", "Chip.SerialLast", "Empty.PickLot",
        "Movement.OPENING_BALANCE", "Movement.GOODS_RECEIPT_PO", "Movement.GOODS_RECEIPT_PROD", "Movement.GOODS_ISSUE_SALES",
        "Movement.GOODS_ISSUE_PROD", "Movement.WAREHOUSE_TRANSFER", "Movement.LOCATION_TRANSFER", "Movement.STATUS_TRANSFER",
        "Movement.CUSTOMER_RETURN", "Movement.SUPPLIER_RETURN", "Movement.COUNT_GAIN", "Movement.COUNT_LOSS", "Movement.SCRAP",
        "Movement.WRITE_OFF", "Movement.REVERSAL"
    ];

    public static readonly IReadOnlyList<string> LotTrace =
    [
        .. Common,
        "Node.Receipt", "Node.Opening", "Node.Consumed", "Node.Used", "Node.Shipment", "Back.None", "Fwd.None",
        "Total.PerUnit", "Hint.Reroot", "Action.TRACELOTDETAIL", "Empty.PickLot"
    ];

    /// <summary>WP-0174-P4 — Elle Girilmiş Lotları Aktarma (resx family <c>LotMigrationIndex</c>, pack §11.6).</summary>
    public static readonly IReadOnlyList<string> LotMigration =
    [
        .. Common,
        "Action.APPLYALLSUGGESTIONS", "Action.CONFIRMMIGRATION", "Action.APPLYSUGGESTION", "Action.LINKEXISTINGLOT",
        "Action.NEWLOTFROMTEXT", "Action.MARKTYPO", "Action.CHANGEDECISION",
        "Banner.Done", "Kpi.Moves", "Col.Action",
        "Suggest.Exact", "Suggest.CaseOnly", "Suggest.Similar", "Suggest.SimilarRow", "Suggest.None", "Suggest.NewDraft",
        "Decision.Pending", "Decision.Linked", "Decision.New", "Decision.Typo",
        "Pick.TitleLink", "Pick.TitleTypo", "Pick.Rows", "Pick.Lots",
        "Confirm.Title", "Confirm.Count", "Confirm.Existing", "Confirm.New", "Confirm.Danger", "Confirm.Reason", "Confirm.Button",
        "Toast.Done", "Validation.ReasonMin", "Cancel"
    ];

    /// <summary>The Stock Ledger form's lot / serial vocabulary (resx family <c>LotFieldIndex</c>, owner decision 2026-10-06).</summary>
    public static readonly IReadOnlyList<string> LotField =
    [
        "Lot.Placeholder", "Lot.None", "Lot.FromSource", "Lot.BlockedDisabled", "Lot.ExpiredDisabled", "Lot.RetestWarn",
        "Lot.RetestConfirm", "Lot.RetestConfirmRequired", "Lot.Unavailable", "Lot.TypedBadge", "Lot.TypedHint", "Lot.Created",
        "Serial.Add", "Serial.RangeAdd", "Serial.RangePreview", "Serial.ScanHint", "Serial.Duplicates", "Serial.BadFormat",
        "Serial.Counter", "Serial.CounterOk", "Serial.CounterOver", "Serial.CounterMore", "Serial.Clear", "Serial.Empty",
        "Serial.Added", "Serial.Unreadable", "Serial.CheckUnavailable", "Serial.ScanNeedsLot"
    ];
}
