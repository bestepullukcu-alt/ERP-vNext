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
        "Form.Product", "Form.Sku", "Form.LotNumber", "Form.ManufacturerLot", "Form.SupplierLot", "Form.ManufacturingDate",
        "Form.ExpiryDate", "Form.RetestDate", "Form.Country", "Form.Coa", "Form.ReasonCounter", "Form.NoChanges",
        "Form.Duplicate", "Col.Status",
        "History.Action.Created", "History.Action.Updated", "History.Action.StatusChanged", "History.Action.Linked",
        "History.Action.Migrated", "History.Action.SystemExpired", "History.System", "History.Page",
        "Validation.Required", "Validation.LotFormat", "Validation.DatesOrder", "Validation.ReasonMin", "Validation.FixFields",
        "Status.Dialog.Title", "Status.Dialog.Reason", "Status.Dialog.Confirm", "Status.Dialog.ConfirmBlock", "Status.Dialog.Impact",
        "Status.Dialog.PickOne", "Status.Dialog.AcknowledgeRequired", "Status.Dialog.Done", "Cancel"
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
